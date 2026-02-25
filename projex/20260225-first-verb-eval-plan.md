# First Verb Dynamic Evaluation Fix

> **Status:** In Progress
> **Created:** 2026-02-25
> **Author:** Antigravity
> **Source:** Direct Request (from audit roadmap `20260223-csharp-spec-audit-nav.md` Phase 3.2 GAP)
> **Related Projex:** [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md)

---

## Summary

This plan fixes a spec non-compliance gap in the C# reference implementation of the `/first` verb. Currently, `FirstDriver.cs` does not dynamically evaluate expression arguments (`ZohExpr`) or recursively execute verb literal arguments (`ZohVerb`). This plan updates `FirstDriver.cs` to explicitly evaluate/execute these types before checking for truthiness, aligning with the `Core.First` spec.

**Scope:** Updates to `FirstDriver.cs` and `CoreVerbTests.cs` in the C# runtime.
**Estimated Changes:** 2 files, 2 functions/methods.

---

## Objective

### Problem / Gap / Need
As identified in the C# Runtime Spec Audit (Phase 3.2), `FirstDriver.cs` yields unevaluated `ZohVerb` or `ZohExpr` objects directly instead of dynamically executing them per the `Core.First` spec ("In case of `/verb`, it takes the return value of the verb").

### Success Criteria
- [ ] `/first` dynamically executes a passed `ZohVerb` and evaluates its returned value instead of returning the verb object itself.
- [ ] `/first` dynamically evaluates a passed `ZohExpr` and evaluates its returned value instead of returning the expression object itself.
- [ ] Unit tests pass demonstrating this behavior.

### Out of Scope
- Fixing similar gaps in `/increase`, `/decrease`, or `/switch` (these should be handled in separate plans).

---

## Context

### Current State
In `FirstDriver.cs`, parameters are resolved using `ValueResolver.Resolve(param, context)`. If the parameter holds a verb block or expression, `Resolve` properly returns a `ZohVerb` or `ZohExpr` object. However, `FirstDriver` immediately returns this object if it's not `Nothing`, rather than evaluating its underlying logic.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Verbs/Core/FirstDriver.cs` | Implementation of `/first` | Add type checks for `ZohVerb` and `ZohExpr` to evaluate them dynamically before checking `IsNothing`. |
| `tests/Zoh.Tests/Verbs/CoreVerbTests.cs` | Tests for core verbs | Add a test `First_EvaluatesVerbsAndExpressionsDynamically` to verify correct behavior. |

### Dependencies
- **Requires:** None.
- **Blocks:** None.

### Constraints
- The evaluation must happen using `context.ExecuteVerb` for `ZohVerb` and `ExpressionEvaluator.Evaluate` for `ZohExpr`.

---

## Implementation

### Overview
We will intercept the resolved `ZohValue` in `FirstDriver.cs`. If it is a `ZohVerb`, we will execute it and use its return value. If it is a `ZohExpr`, we will evaluate it and use its return value.

### Step 1: Update FirstDriver.cs

**Objective:** Add dynamic evaluation logic to `FirstDriver`.

**Files:**
- `src/Zoh.Runtime/Verbs/Core/FirstDriver.cs`

**Changes:**

```csharp
// Before:
            var value = ValueResolver.Resolve(param, context);

            if (!value.IsNothing())
            {
                return VerbResult.Ok(value);
            }

// After:
            var value = ValueResolver.Resolve(param, context);

            if (value is ZohVerb vSubject)
            {
                value = context.ExecuteVerb(vSubject.VerbValue, context).Value;
            }
            else if (value is ZohExpr expr)
            {
                value = Zoh.Runtime.Expressions.ExpressionEvaluator.Evaluate(expr.ast, context);
            }

            if (!value.IsNothing())
            {
                return VerbResult.Ok(value);
            }
```

**Rationale:** Brings the driver logic in line with the "In case of `/verb`, it takes the return value of the verb" rule and standard expression evaluation logic (as seen in `SwitchDriver.cs`).

**Verification:** Build the solution (`dotnet build`) to ensure there are no compilation errors.

---

### Step 2: Add Unit Tests

**Objective:** Verify that `ZohVerb` and `ZohExpr` parameters are dynamically executed by `/first`.

**Files:**
- `tests/Zoh.Tests/Verbs/CoreVerbTests.cs`

**Changes:**
Add a new test inside the `CoreVerbTests` class testing `/first`:

```csharp
// Add:
[Fact]
public void First_EvaluatesVerbsAndExpressionsDynamically()
{
    var script = """
        ====> @main
        /set *expr_val, `1 + 2`;
        /set *verb_val, /return 42;;
        
        /first ?, *expr_val; -> *res1;
        /first ?, *verb_val; -> *res2;
        """;
    
    var store = RunScript(script);
    
    Assert.Equal(3L, store.Get("*res1").AsInteger());
    // Note: If /return is not available, we can test with a math expression or /sequence returning a value
    Assert.Equal(42L, store.Get("*res2").AsInteger()); // Depending on verb returned value
}
```
*(Refine the test code based on the available verbs for returning values)*

**Rationale:** Ensures regressions do not occur and formally asserts the spec behavior.

**Verification:** Run `dotnet test --filter="CoreVerbTests"` to verify the tests pass.

---

## Verification Plan

### Automated Checks
- [ ] Run `dotnet build` in `s:\repos\zoh\c#`.
- [ ] Run `dotnet test --filter="FullyQualifiedName~First"` in `s:\repos\zoh\c#` to see if tests pass.

### Manual Verification
- [ ] Review the test output properly evaluates and returns the extracted 64-bit integers rather than the `ZohVerb`/`ZohExpr` instance.
- [ ] (Optional) test directly locally using `zohc` or `zoh` runners if available.

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Dynamic Execution of Verbs by `/first` | Run the new test | `*res2` holds the value 42 rather than a `ZohVerb` object |
| Dynamic Execution of Exprs by `/first` | Run the new test | `*res1` holds the value 3 rather than a `ZohExpr` object |

---

## Rollback Plan

If implementation fails or causes issues:

1. Revert changes to `FirstDriver.cs`.
2. Delete the newly added tests in `CoreVerbTests.cs`.

---

## Notes

### Assumptions
- `ZohExpr` uses `expr.ast` to access its underlying AST.
- Returning a literal value from a verb can be done directly by the verb returning `VerbResult.Ok(val)` or via `/capture` context variables. ZOH `/sequence` will return the result of the last executed verb. We may use `/evaluate \`42\`;` as the verb object `*verb_val` if a simpler test is needed.

### Risks
- **Risk:** Type mismatch if `expr.Ast` implies a property `Ast` instead of the lowercase constructor parameter name `ast`. **Mitigation:** Rely on compilation checks, adjust case if necessary.

### Open Questions
- [ ] None.
