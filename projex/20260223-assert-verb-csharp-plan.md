# Plan: Assert Verb C# Implementation

> **Status:** Ready
> **Created:** 2026-02-23
> **Author:** agent
> **Source:** Direct request
> **Related Projex:** [20260223-assert-verb-impl-plan.md](../../impl/projex/closed/20260223-assert-verb-impl-plan.md), `20260207-csharp-runtime-nav.md`

---

## Summary

Implement the `Core.Assert` verb in the C# ZOH runtime. This involves creating the `AssertDriver`, registering it in the `VerbRegistry`, and adding comprehensive unit tests to ensure it behaves correctly according to the language and implementation specifications.

**Scope:** `c#` runtime source and tests
**Estimated Changes:** 2 new files, 1 modified file

---

## Objective

### Problem / Gap / Need
We recently added the `Core.Assert` verb to the ZOH language specification (`spec/2_verbs.md`) and the implementation specification (`impl/06_core_verbs.md`). The C# runtime needs to implement this new verb to remain conformant with the specifications.

### Success Criteria
- [ ] `AssertDriver` class implements `IVerbDriver` and is placed in `Zoh.Runtime.Verbs.Core`.
- [ ] `AssertDriver` evaluates the `subject` condition and `is:` parameter, mirroring `IfDriver`'s truthy and equality semantics.
- [ ] `AssertDriver` evaluates and formats the optional `message` argument only upon assertion failure.
- [ ] `AssertDriver` returns a `VerbResult.Fatal` with diagnostic code `"assertion_failed"` on condition mismatch.
- [ ] `VerbRegistry.cs` registers `Core.AssertDriver`.
- [ ] `AssertDriverTests.cs` contains tests covering success, failure, custom messages, and default values.

### Out of Scope
- Modifying other core verbs.
- Changing the script parser.

---

## Context

### Current State
`Core.Assert` is documented but missing from the runtime. `DebugDriver` currently handles diagnostic emission and `IfDriver` handles conditional logic. The new driver will bridge these concepts. 

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Verbs/Core/AssertDriver.cs` | Verb Driver | [NEW] Create implementation class |
| `c#/src/Zoh.Runtime/Verbs/VerbRegistry.cs` | Registry | [MODIFY] Register `AssertDriver` |
| `c#/tests/Zoh.Tests/Verbs/Core/AssertDriverTests.cs` | Tests | [NEW] Create unit tests |

### Dependencies
- **Requires:** Specifications from `spec/` and `impl/` (already completed).
- **Blocks:** Full standard conformance of the C# runtime.

### Constraints
- Must follow the `IVerbDriver` interface.
- Must emit exactly one fatal diagnostic on failure, with code `"assertion_failed"`.
- The `is:` parameter must default to `ZohBool.True`.

---

## Implementation

### Overview
Create `AssertDriver` and map its execution logic to `ValueResolver` for resolving the condition and comparing it. Once failure is detected, construct the diagnostic message and return `VerbResult.Fatal`. Hook it up in `VerbRegistry` and add XUnit tests.

### Step 1: Create AssertDriver.cs

**Objective:** Implement the assertion logic.

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Core/AssertDriver.cs`

**Changes:**

```csharp
// Before:
// (File does not exist)

// After (conceptual):
namespace Zoh.Runtime.Verbs.Core;

public class AssertDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "assert";

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (call.UnnamedParams.Length < 1)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /assert condition, [message]", call.Start));
        }

        var subjectValue = ValueResolver.Resolve(call.UnnamedParams[0], context);
        
        // Evaluate if needed
        if (subjectValue is ZohVerb subjectVerb) subjectValue = context.ExecuteVerb(subjectVerb.VerbValue, context).Value;
        if (subjectValue is ZohExpression expr) subjectValue = context.EvaluateExpression(expr.Ast);

        var compareParam = call.NamedParams.GetValueOrDefault("is");
        ZohValue compareValue = compareParam != null ? ValueResolver.Resolve(compareParam, context) : ZohBool.True;

        // Apply same validation as IfDriver for default true comparison
        if (compareParam == null && !(subjectValue is ZohBool) && !(subjectValue is ZohNothing))
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Condition must be boolean or nothing", call.Start));
        }

        if (!subjectValue.Equals(compareValue))
        {
            string message = "assertion failed";
            if (call.UnnamedParams.Length > 1)
            {
                var msgVal = ValueResolver.Resolve(call.UnnamedParams[1], context);
                // Expand expression/interpolation here depending on C# runtime utilities
                message = msgVal.ToString(); // Or use specific string extraction
            }
            
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "assertion_failed", message, call.Start));
        }

        return VerbResult.Ok();
    }
}
```

**Rationale:** Matches `spec/2_verbs.md` requirements for truthy evaluation, type checking, and fatal diagnostics.

**Verification:** Build the code to ensure it compiles.

---

### Step 2: Register the Driver

**Objective:** Make `/assert` available in the runtime.

**Files:**
- `c#/src/Zoh.Runtime/Verbs/VerbRegistry.cs`

**Changes:**

```csharp
// Before:
        Register(new Core.TypeDriver());

        Register(new Core.IncreaseDriver());

// After:
        Register(new Core.TypeDriver());
        Register(new Core.AssertDriver());

        Register(new Core.IncreaseDriver());
```

**Rationale:** Exposes the driver so scripts can call `/assert`.

**Verification:** Check if `GetDriver("core", "assert")` works internally.

---

### Step 3: Implement Unit Tests

**Objective:** Verify assertion behavior via tests.

**Files:**
- `c#/tests/Zoh.Tests/Verbs/Core/AssertDriverTests.cs`

**Changes:**

Implement XUnit fact methods testing:
1. `Assert_Truthy_Passes`
2. `Assert_Falsy_FailsWithFatal`
3. `Assert_Truthy_WithIsParameter_Passes`
4. `Assert_Mismatch_WithIsParameter_Fails`
5. `Assert_CustomMessage_IsIncludedInDiagnostic`
6. `Assert_NonBooleanWithoutIs_FailsWithInvalidType`

**Rationale:** Covers all specification boundary conditions.

**Verification:** Run `dotnet test --filter "FullyQualifiedName~AssertDriverTests"`

---

## Verification Plan

### Automated Checks
- [ ] `cd c# && dotnet build` (Must succeed without warnings)
- [ ] `cd c# && dotnet test --filter "FullyQualifiedName~AssertDriverTests"` (All 6 tests must pass)

### Manual Verification
- [ ] Verify `AssertDriver.cs` handles evaluation of verbs/expressions passed as condition.
- [ ] Verify semantic similarity to `IfDriver.cs`.

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `AssertDriver` implemented | Read `AssertDriver.cs` | Logic matches plan |
| Registered | `grep` `VerbRegistry.cs` | Contains `Register(new Core.AssertDriver())` |
| Tests Passing | `dotnet test` | Tests succeed and assert diagnostic code is `assertion_failed` |

---

## Rollback Plan
1. Delete `AssertDriver.cs` and `AssertDriverTests.cs`
2. Restore `VerbRegistry.cs`

---

## Notes
### Assumptions
- String interpolation at the C# level may not be fully implemented as a standalone utility method, so `ToString()` on the resolved message argument will suffice initially until the Interpolator is abstracted, aligning with how `DebugDriver` currently formats messages.
- The default `ZohBool.True` check ensures non-booleans fail unless `is:` is provided.
