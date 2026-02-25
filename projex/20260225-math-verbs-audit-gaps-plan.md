# Math Verbs Audit Gaps Plan

> **Status:** Ready
> **Created:** 2026-02-25
> **Author:** Antigravity
> **Source:** Direct request from user based on C# Spec Audit Navigator
> **Related Projex:** [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md)

---

## Summary

This plan addresses C# Spec Audit Phase 3.3 (Mathematics Verbs) GAPs 1 and 2, ensuring that the `/increase` and `/decrease` verbs correctly perform strict type validation on their `amount` parameters and recursively execute `ZohVerb` literals when provided as an amount.

**Scope:** `IncreaseDriver.cs` and `CoreVerbTests.cs` (which also covers `/decrease`).
**Estimated Changes:** 2 files, 1 core method update, 3 new tests.

---

## Objective

### Problem / Gap / Need
- **GAP 1:** The `IncreaseDriver` (and by extension `DecreaseDriver`) silently ignores the `amount` parameter if it resolves to a non-numeric type (e.g., string) instead of throwing an `invalid_type` fatal diagnostic, defaulting to `1`.
- **GAP 2:** It does not recursively execute `ZohVerb` parameter inputs when a `/verb` literal is provided for the amount, leading to the same silent fallback to `1`.

### Success Criteria
- [ ] If an `/increase` or `/decrease` amount parameter is not an integer or float, it throws an `invalid_type` fatal diagnostic.
- [ ] If the amount parameter is a verb literal (e.g., `/rand 1, 10;`), the verb is correctly executed, and its return value is evaluated as the numeric amount.
- [ ] Existing core tests for `/increase` and `/decrease` continue to pass.
- [ ] New unit tests directly verifying the failure on invalid type and successful evaluation of verb literals are added to `CoreVerbTests.cs`.

### Out of Scope
- Changes to any other verbs.
- Enhancements to the `ValueResolver` framework.

---

## Context

### Current State
`IncreaseDriver.ModifyVariable` evaluates the `amount` parameter using `ValueResolver.Resolve`. Currently, if the resolved value is not a `ZohInt` or `ZohFloat`, `delta` remains `1` and the execution continues silently without throwing an error. Furthermore, if the amount resolves to a `ZohVerb`, it treats it as a non-numeric type and again silently uses a delta of `1`, rather than executing the verb to get its return value.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Verbs/Core/IncreaseDriver.cs` | Math verb logic | Update `ModifyVariable` to execute `ZohVerb` values and add strict `ZohInt`/`ZohFloat` type checking, returning an `invalid_type` diagnostic on failure. |
| `tests/Zoh.Tests/Verbs/CoreVerbTests.cs` | Unit tests for core verbs | Add tests for `invalid_type` rejection and verb literal execution for `amount`. |

### Dependencies
- **Requires:** The existing `TypeDriver` and `DebugDriver` tests pass.
- **Blocks:** Full completion of the Phase 3.3 audit sign-off.

### Constraints
- The `invalid_type` diagnostic must be a `DiagnosticSeverity.Fatal`.

---

## Implementation

### Overview
Modify `IncreaseDriver.ModifyVariable` to check if `amount` is a `ZohVerb`. If so, execute it and use its `ReturnValue` (or `Value`). Then, strictly check that `amount` is either a `ZohInt` or `ZohFloat`. Add corresponding tests to `CoreVerbTests.cs`.

### Step 1: Fix Value Resolution and Validation in IncreaseDriver

**Objective:** Execute verb literal amounts and validate numeric types.

**Files:**
- `src/Zoh.Runtime/Verbs/Core/IncreaseDriver.cs`

**Changes:**

```csharp
// Before:
            if (verb.UnnamedParams.Length > 1)
            {
                amount = ValueResolver.Resolve(verb.UnnamedParams[1], context);
            }
        }

        if (targetName == null)

// After:
            if (verb.UnnamedParams.Length > 1)
            {
                amount = ValueResolver.Resolve(verb.UnnamedParams[1], context);
                
                // Address GAP 2: Execute verb literal if provided
                if (amount is ZohVerb v)
                {
                    var execResult = context.ExecuteVerb(v.VerbValue, context);
                    if (execResult.IsFatal) return execResult;
                    amount = execResult.Value ?? ZohNothing.Instance;
                }

                // Address GAP 1: Strict numeric type validation
                if (!(amount is ZohInt || amount is ZohFloat))
                {
                    return VerbResult.Fatal(new Diagnostic(
                        DiagnosticSeverity.Fatal, 
                        "invalid_type", 
                        $"Amount parameter must evaluate to an integer or float, got {amount.Type}", 
                        verb.Start));
                }
            }
        }

        if (targetName == null)
```

**Rationale:** `ValueResolver.Resolve` currently yields a `ZohVerb` object directly. The spec says verb parameters evaluate to their return values, so we execute it via `context.ExecuteVerb`. Afterward, the `amount` is strictly checked for `ZohInt` or `ZohFloat` compliance.

**Verification:** Run `dotnet test` on `Zoh.Tests` to verify no existing tests break due to this change.

---

### Step 2: Add Missing Unit Tests

**Objective:** Add specific protections for type checking and verb evaluation.

**Files:**
- `tests/Zoh.Tests/Verbs/CoreVerbTests.cs`

**Changes:**
Add inside the `#region Increase/Decrease Tests`:

```csharp
    [Fact]
    public void Increase_WithInvalidTypeAmount_Fails()
    {
        _context.Variables.Set("cnt", new ZohInt(5));

        // /increase *cnt "string"
        var call = MakeVerbCall("increase", new ValueAst.Reference("cnt"), new ValueAst.String("string"));
        var result = _increaseDriver.Execute(_context, call);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "invalid_type");
    }

    [Fact]
    public void Increase_WithVerbAmount_ExecutesVerb()
    {
        _context.Variables.Set("cnt", new ZohInt(5));

        // /increase *cnt, /rand 1, 10;; -> /rand returns 7 for instance, but we use an easier test verb like /get
        _context.Variables.Set("amt", new ZohInt(3));
        var getCall = MakeVerbCall("get", new ValueAst.Reference("amt"));

        var call = MakeVerbCall("increase", new ValueAst.Reference("cnt"), new ValueAst.Verb(getCall));
        var result = _increaseDriver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(8), _context.Variables.Get("cnt"));
    }
```

**Rationale:** The tests directly execute the conditions outlined in GAP 1 and GAP 2.

**Verification:** `dotnet test --filter "FullyQualifiedName~Increase_With"` should pass.

---

## Verification Plan

### Automated Checks
- [ ] `cd s:\repos\zoh\c# && dotnet build`
- [ ] `cd s:\repos\zoh\c# && dotnet test --filter "FullyQualifiedName~CoreVerbTests"`

### Manual Verification
- [ ] None needed. C# unit tests will fully validate the compiler and runtime boundaries of this language feature.

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Fails on non-numeric amount | Run `Increase_WithInvalidTypeAmount_Fails` | Diagnostic `invalid_type` is caught |
| Verb arguments are executed | Run `Increase_WithVerbAmount_ExecutesVerb` | Variable is correctly increased by the return value of the verb |

---

## Rollback Plan

If implementation fails or causes issues:

1. Revert changes to `IncreaseDriver.cs`.
2. Delete the new tests from `CoreVerbTests.cs`.

---

## Notes

### Assumptions
- `verb.UnnamedParams[1]` correctly represents the amount parameter position per the syntax.
- `ZohNothing.Instance` acts as a safe fallback for executed verbs that return `null` or nothing, since the check for numeric types immediately catches it.

### Risks
- None expected. This is a strictly bounding change that fails-fast on incorrect scripts rather than masking errors as it previously did.
