# Flow Verbs Test Coverage Plan

> **Status:** In Progress
> **Created:** 2026-03-15
> **Author:** Agent
> **Source:** Direct request (Unit Test Discovery)
> **Related Projex:** None
> **Worktree:** Yes

---

## Summary

This plan outlines the addition of missing unit tests for Flow verbs (`/loop`, `/if`, `/while`, `/sequence`) in the C# runtime. The new tests enforce behavior defined in the language specs (`07_control_flow.md`), including edge cases, invalid inputs, error propagation, and specific type-check validations that currently differ between the spec and the C# implementation.

**Scope:** `csharp/tests/Zoh.Tests/Verbs/Flow/` and `csharp/src/Zoh.Runtime/Verbs/Flow/`
**Estimated Changes:** 5 files, ~20 new tests, minor driver fixes.

---

## Objective

### Problem / Gap / Need
The current `FlowTests` suite only covers happy-path scenarios. Boundary analysis, branch analysis, and error cataloging reveal significant gaps in testing for invalid arguments, fatal error propagation, and strict type checking (e.g., enforcing that `/if` and `/while` conditions must be boolean or nothing when no comparison attribute is provided). Specifically, the C# drivers for `/if` and `/while` currently use generic truthy/equality checks instead of the strict type enforcement mandated by the spec.

### Success Criteria
- [ ] New unit tests are added for `/loop`, `/if`, `/while`, and `/sequence` covering missing branches and error conditions.
- [ ] Tests explicitly verify that fatal diagnostics are returned for invalid arguments (e.g., parameter count, type mismatches).
- [ ] The C# implementation of `IfDriver` and `WhileDriver` is updated to return `invalid_type` fatal diagnostics when the condition is not boolean/nothing and no `is:` attribute is specified, conforming to the spec.
- [ ] All new and existing tests in `Zoh.Tests.Verbs.Flow` pass.

### Out of Scope
- Adding tests for non-flow verbs.
- Changing the overall architecture of flow control drivers.

---

## Context

### Current State
`FlowTests.cs` tests basic execution of `If`, `Loop`, `While`, `Sequence`, `Foreach`, and `Switch`. However, `LoopDriver.cs`, `IfDriver.cs`, and `WhileDriver.cs` lack corresponding tests for early returns on invalid arguments. Furthermore, `IfDriver.cs` uses `IsTruthy()` and `WhileDriver.cs` uses generic `.Equals()` for condition evaluation, whereas `07_control_flow.md` dictates they must perform strict type checking against `BoolValue` unless a custom `is:` value is provided.

### Key Files

> Quick reference — detailed changes are in Implementation steps below.

| File | Role | Change Summary |
|------|------|----------------|
| `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs` | Flow unit tests | Add new test methods for edge cases and errors. |
| `csharp/src/Zoh.Runtime/Verbs/Flow/IfDriver.cs` | `/if` implementation | Update condition evaluation to enforce strict boolean/nothing types. |
| `csharp/src/Zoh.Runtime/Verbs/Flow/WhileDriver.cs` | `/while` implementation | Update condition evaluation to enforce strict boolean/nothing types. |

### Dependencies
- **Requires:** None
- **Blocks:** None

### Constraints
- Must maintain existing C# runtime and testing conventions (xUnit, AAA pattern).
- `If` and `While` spec alignment must not break existing valid uses of these verbs.

### Assumptions
- The guidelines in `07_control_flow.md` regarding strict boolean evaluation for `/if` and `/while` are the authoritative source of truth.

### Impact Analysis
- **Direct:** Flow verb drivers and their tests.
- **Adjacent:** Story scripts that incorrectly relied on truthy coercion for `/if` and `/while` may now fail with `invalid_type` fatals.
- **Downstream:** None.

---

## Implementation

### Overview
1. Update `IfDriver.cs` and `WhileDriver.cs` to strictly validate condition types as per the spec.
2. Add new test cases to `FlowTests.cs` (or create a dedicated `FlowErrorTests.cs`) covering the newly enforced behavior, missing parameter checks, and fatal propagation for all flow verbs.

### Step 1: Align IfDriver and WhileDriver with Spec

**Objective:** Enforce strict type checking for conditions out of the box.
**Confidence:** High
**Depends on:** None

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/Flow/IfDriver.cs`
- `csharp/src/Zoh.Runtime/Verbs/Flow/WhileDriver.cs`

**Changes:**
Modify the evaluation logic to check if `compareValue` is `true`. If so, verify that the resolved subject is either a `ZohBool` or `ZohNothing`. If not, return a fatal `invalid_type` diagnostic. For `IfDriver`, explicitly fetch the `is` named parameter (defaulting to `true`) identical to how `WhileDriver` functions, then compare.

**Rationale:** The spec (`07_control_flow.md`) explicitly demands: "Validate subject type when comparing to default (true): if compareValue == BoolValue(true): if subject is not BoolValue and not subject.isNothing(): return fatal...".

**Verification:** Code compiles.

**If this fails:** Revert driver changes.

---

### Step 2: Implement Missing Unit Tests

**Objective:** Add AAA-structured unit tests to achieve full coverage on branches and errors.
**Confidence:** High
**Depends on:** Step 1

**Files:**
- `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs` (or `FlowErrorTests.cs`)

**Changes:**
Add the following test methods:
- **Loop**:
  - `Loop_MissingParameters_ReturnsFatal`: Pass 0 arguments; assert `result.IsFatal` and `parameter_not_found`.
  - `Loop_InvalidIterationsType_ReturnsFatal`: Pass a string instead of an integer for the first argument; assert `invalid_type`.
  - `Loop_InvalidVerbType_ReturnsFatal`: Pass an integer instead of a verb for the second argument; assert `invalid_type`.
  - `Loop_ZeroIterations_DoesNotExecuteBody`: Pass `0` for iterations and a mock verb; assert the verb was not called.
  - `Loop_NegativeIterations_DoesNotExecuteBody`: Pass `-2` for iterations and a mock verb; assert the verb was not called.
  - `Loop_InfiniteIterations_ExecutesUntilBreak`: Pass `-1` for iterations, with a sequence that sets a break condition after 3 iterations; assert it ran exactly 3 times.
  - `Loop_BodyReturnsFatal_HaltsLoopAndReturnsFatal`: Pass a mock verb that returns fatal on the 2nd iteration; assert loop halts and outer result is fatal.

- **If**:
  - `If_MissingCondition_ReturnsFatal`: Pass 0 arguments; assert `parameter_not_found`.
  - `If_MissingThenVerb_ReturnsFatal`: Pass 1 argument; assert `parameter_not_found` or similar invalid state.
  - `If_ConditionNotBooleanAndDefaultCompare_ReturnsFatal`: Pass an integer condition without `is`; assert `invalid_type`.
  - `If_InvalidThenType_ReturnsFatal`: Pass `true` for condition and a string instead of a verb; assert `invalid_type`.
  - `If_ConditionFalseAndNoElse_ReturnsOk`: Pass `false` and a mock verb; assert OK and verb not called.
  - `If_InvalidElseType_ReturnsFatal`: Pass `false`, a verb, and a string instead of a verb; assert `invalid_type`.
  - `If_BodyReturnsFatal_PropagatesFatal`: Pass `true` and a fatal mock verb; assert outer result is fatal.

- **While**:
  - `While_MissingParameters_ReturnsFatal`: Pass 0 or 1 argument; assert error.
  - `While_InvalidVerbType_ReturnsFatal`: Pass `true` and a string instead of a verb; assert `invalid_type`.
  - `While_ConditionNotBooleanAndDefaultCompare_ReturnsFatal`: Pass an integer condition without `is`; assert `invalid_type`.
  - `While_ConditionInitiallyFalse_DoesNotExecuteBody`: Pass `false` and a mock verb; assert OK and verb not called.
  - `While_BodyReturnsFatal_HaltsAndPropagates`: Pass `true` and a fatal mock verb; assert outer result is fatal.

- **Sequence**:
  - `Sequence_EmptyArguments_ReturnsOk`: Pass 0 arguments; assert OK.
  - `Sequence_InvalidArgumentType_ReturnsFatal`: Pass a verb, then a string, then a verb; assert `invalid_type` and 3rd verb not executed.
  - `Sequence_VerbReturnsFatal_HaltsSequence`: Pass sequence of verbs where the 2nd is fatal; assert 3rd is not executed and overall is fatal.

**Rationale:** These cover the techniques outlined in the `unit-test` workflow: Boundary Value Analysis, Branch Analysis, and Error Cataloging.

**Verification:** `dotnet test --filter "Zoh.Tests.Verbs.Flow"` passes all tests.

**If this fails:** Debug tests or driver logic.

---

## Verification Plan

### Automated Checks
- [ ] `dotnet build` executes without errors.
- [ ] `dotnet test --filter "Zoh.Tests.Verbs.Flow"` reports 100% pass rate.
- [ ] No existing tests in other namespaces fail due to `If`/`While` strictness.

### Manual Verification
- [ ] Inspect test output to ensure all expected `invalid_type` and `parameter_not_found` diagnostics are generated correctly.

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Missing branches covered | Run tests | Tests for missing arguments, invalid types, and error propagation pass. |
| Strict type check enforced | Run `If_ConditionNotBoolean...` test | Returns fatal diagnostic instead of coercing value. |

---

## Rollback Plan

1. Revert `IfDriver.cs` and `WhileDriver.cs` changes.
2. Delete added tests from `FlowTests.cs`.

---

## Notes

### Risks
- **Risk 1**: Existing tests outside the `Flow` namespace might implicitly rely on `IsTruthy()` coercion for `/if` statements.
  - **Mitigation**: Run the entire `Zoh.Tests` suite and fix any tests that break by updating their `/if` subjects to emit explicit booleans or use the `is:` parameter.

### Open Questions
- [ ] None.
