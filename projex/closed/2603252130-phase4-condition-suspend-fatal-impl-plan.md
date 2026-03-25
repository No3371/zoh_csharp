# Impl: Condition Verb Suspend & Fatal Propagation

> **Status:** Complete
> **Completed:** 2026-03-26
> **Walkthrough:** `2603252130-phase4-condition-suspend-fatal-impl-walkthrough.md`
> **Created:** 2026-03-25
> **Author:** Agent
> **Source:** `2603252130-phase4-condition-suspend-fatal-propagation-proposal.md`
> **Related Projex:** `2603252101-phase4-flowutils-condition-suspend-fatal-memo.md`, `20260227-phase4-control-flow-gaps-fix-plan.md`, `2603252130-condition-verb-suspend-fatal-spec-plan.md` (spec)
> **Requires:** `2603252130-condition-verb-suspend-fatal-spec-plan.md` (spec plan)
> **Worktree:** No

---

## Summary

Propagate `Suspend` and `IsFatal` from verb-condition execution in `FlowUtils.ResolveConditionValue` (used by `breakif`/`continueif`) and `WhileDriver`'s inline condition path. Currently both collapse `Suspend` → `ZohNothing` and discard `IsFatal` via `.ValueOrNothing`. `IfDriver` already propagates both correctly — this plan extends that pattern to all remaining condition-evaluation sites.

**Scope:** `csharp/src/Zoh.Runtime/Verbs/Flow/` (FlowUtils, WhileDriver, and callers) + `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs`.
**Estimated Changes:** 6 files, ~4 methods + ~5 test methods.

---

## Objective

### Problem / Gap / Need

`FlowUtils.ResolveConditionValue` calls `context.ExecuteVerb(...).ValueOrNothing` which:
- Collapses `DriverResult.Suspend` to `ZohNothing` → falsy → `breakif`/`continueif` never fires, suspension is lost.
- Discards `IsFatal` → loop body continues executing past a fatal condition.

`WhileDriver` has the identical pattern (line 40): `subjectVal = context.ExecuteVerb(vSubject.VerbValue, context).ValueOrNothing`.

`IfDriver` (lines 26–31) already handles both correctly:
```csharp
if (subjectResult is DriverResult.Suspend) return subjectResult;
if (subjectResult.IsFatal) return subjectResult;
conditionValue = subjectResult.ValueOrNothing;
```

### Success Criteria

- [x] `FlowUtils.ResolveConditionValue` returns `DriverResult` (not `ZohValue`), propagating suspend and fatal — implemented as `EvaluateCondition` + `EvaluateBreakIf`/`EvaluateContinueIf` (same behavior).
- [x] `FlowUtils.ShouldBreak` and `ShouldContinue` replaced with `EvaluateBreakIf`/`EvaluateContinueIf` returning `DriverResult?`.
- [x] `LoopDriver`, `ForeachDriver`, `SequenceDriver` propagate suspend/fatal from `EvaluateBreakIf`/`EvaluateContinueIf`.
- [x] `WhileDriver` propagates suspend/fatal from condition verb.
- [x] Tests verify: (a) fatal from `breakif` verb surfaces to caller, (b) suspend from `breakif` verb propagates, (c) fatal from `/while` condition verb surfaces, (d) suspend from `/while` condition verb propagates.
- [x] All existing tests pass (`dotnet test`).

### Out of Scope

- Spec/impl-doc changes (separate plan: `2603252130-condition-verb-suspend-fatal-spec-plan.md`).
- `IfDriver` changes (already correct).
- `DoDriver` changes (already correct).
- `SwitchDriver` subject/case suspend/fatal (separate concern — subject already uses `.ValueOrNothing` like WhileDriver but is not part of this memo's scope).

---

## Context

### Current State

**`FlowUtils.cs` (entire file — 32 lines):**
- `ShouldBreak(call, context)` → `bool` — calls `ResolveConditionValue(...).IsTruthy()`.
- `ShouldContinue(call, context)` → `bool` — same pattern.
- `ResolveConditionValue(val, context)` → `ZohValue` — resolves, if verb, executes and takes `.ValueOrNothing`.

**`WhileDriver.cs` (line 38–41):**
```csharp
if (subjectVal is ZohVerb vSubject)
{
    subjectVal = context.ExecuteVerb(vSubject.VerbValue, context).ValueOrNothing;
}
```

**Callers of `ShouldBreak`/`ShouldContinue`:**
- `LoopDriver.cs:39` — `if (FlowUtils.ShouldBreak(call, context)) break;`
- `SequenceDriver.cs:20` — `if (FlowUtils.ShouldBreak(call, context)) break;`
- `ForeachDriver.cs:64` — `if (FlowUtils.ShouldBreak(call, context)) break;`
- `ForeachDriver.cs:69` — `if (FlowUtils.ShouldContinue(call, context)) continue;`

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `csharp/src/Zoh.Runtime/Verbs/Flow/FlowUtils.cs` | Break/continue condition evaluation | Replace `ShouldBreak`/`ShouldContinue` with `EvaluateBreakIf`/`EvaluateContinueIf` returning `DriverResult?` |
| `csharp/src/Zoh.Runtime/Verbs/Flow/LoopDriver.cs` | `/loop` | Update `ShouldBreak` call site |
| `csharp/src/Zoh.Runtime/Verbs/Flow/SequenceDriver.cs` | `/sequence` | Update `ShouldBreak` call site |
| `csharp/src/Zoh.Runtime/Verbs/Flow/ForeachDriver.cs` | `/foreach` | Update `ShouldBreak` + `ShouldContinue` call sites |
| `csharp/src/Zoh.Runtime/Verbs/Flow/WhileDriver.cs` | `/while` | Add suspend/fatal guard after condition verb execution |
| `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs` | Flow tests | New tests for suspend/fatal propagation |

### Dependencies

- **Requires:** `2603252130-condition-verb-suspend-fatal-spec-plan.md` (spec plan accepted).
- **Blocks:** None.

### Constraints

- `FlowUtils` methods are `internal`/`public static` — no public API boundary concern.
- Existing test patterns use `TestExecutionContext` with `RegisterDriver` for custom verb drivers and `VerbExecutor` mock.
- `DriverResult.Suspend` requires a `Continuation` with a `Request` (e.g. `SleepRequest`) — tests can use the same pattern as `TryTests.cs`.

### Assumptions

- A verb used in `breakif`/`continueif` that suspends is a valid scenario (e.g. channel read as condition).
- `SwitchDriver` condition handling is a separate concern (tracked independently if needed).

### Impact Analysis

- **Direct:** FlowUtils, WhileDriver, LoopDriver, ForeachDriver, SequenceDriver.
- **Adjacent:** Any future drivers calling `FlowUtils.ShouldBreak`/`ShouldContinue` — they must adopt the new API. But since we're renaming the methods, the compiler will catch this.
- **Downstream:** None — all changes are internal to `Zoh.Runtime`.

---

## Implementation

### Overview

Replace `FlowUtils.ShouldBreak`/`ShouldContinue` (returning `bool`) with `EvaluateBreakIf`/`EvaluateContinueIf` (returning `DriverResult?`). A `null` return means "no break/continue" (either no parameter present, or condition was falsy). A non-null `DriverResult` is either a signal to break/continue (`DriverResult.Complete.Ok()`), a suspend to propagate, or a fatal to propagate. Each driver call site inspects the result and propagates non-null suspend/fatal immediately. Then fix `WhileDriver`'s inline condition evaluation with the same suspend/fatal guard pattern.

---

### Step 1: Rewrite `FlowUtils.cs`

**Objective:** Replace the three methods with suspend/fatal-aware versions.
**Confidence:** High
**Depends on:** None

**File:** `csharp/src/Zoh.Runtime/Verbs/Flow/FlowUtils.cs`

**Changes:**

```csharp
// Before:
public static class FlowUtils
{
    public static bool ShouldBreak(VerbCallAst call, IExecutionContext context)
    {
        if (call.NamedParams.TryGetValue("breakif", out var val))
            return ResolveConditionValue(val, context).IsTruthy();
        return false;
    }

    public static bool ShouldContinue(VerbCallAst call, IExecutionContext context)
    {
        if (call.NamedParams.TryGetValue("continueif", out var val))
            return ResolveConditionValue(val, context).IsTruthy();
        return false;
    }

    private static ZohValue ResolveConditionValue(ValueAst val, IExecutionContext context)
    {
        var resolved = ValueResolver.Resolve(val, context);
        if (resolved is ZohVerb condVerb)
            resolved = context.ExecuteVerb(condVerb.VerbValue, context).ValueOrNothing;
        return resolved;
    }
}

// After:
public static class FlowUtils
{
    /// <summary>
    /// Returns null (no breakif param or condition falsy), DriverResult.Complete.Ok() (break),
    /// DriverResult.Suspend (propagate), or fatal DriverResult (propagate).
    /// </summary>
    public static DriverResult? EvaluateBreakIf(VerbCallAst call, IExecutionContext context)
    {
        if (!call.NamedParams.TryGetValue("breakif", out var val))
            return null;
        return EvaluateCondition(val, context);
    }

    /// <summary>
    /// Same contract as EvaluateBreakIf but for continueif.
    /// </summary>
    public static DriverResult? EvaluateContinueIf(VerbCallAst call, IExecutionContext context)
    {
        if (!call.NamedParams.TryGetValue("continueif", out var val))
            return null;
        return EvaluateCondition(val, context);
    }

    private static DriverResult? EvaluateCondition(ValueAst val, IExecutionContext context)
    {
        var resolved = ValueResolver.Resolve(val, context);
        if (resolved is ZohVerb condVerb)
        {
            var result = context.ExecuteVerb(condVerb.VerbValue, context);
            if (result is DriverResult.Suspend)
                return result;
            if (result.IsFatal)
                return result;
            resolved = result.ValueOrNothing;
        }
        return resolved.IsTruthy() ? DriverResult.Complete.Ok() : null;
    }
}
```

**Rationale:** Mirrors the `IfDriver` pattern. `null` = "no action", non-null = "act on this result". Callers pattern-match once instead of getting a `bool` that hides failures.

**Verification:** Compiles; existing callers will fail to compile (expected — fixed in Steps 2–4).

**If this fails:** Revert to original three-method form.

---

### Step 2: Update `LoopDriver.cs` call site

**Objective:** Replace `ShouldBreak` with `EvaluateBreakIf` and propagate suspend/fatal.
**Confidence:** High
**Depends on:** Step 1

**File:** `csharp/src/Zoh.Runtime/Verbs/Flow/LoopDriver.cs`

**Changes:**

```csharp
// Before (lines 39–42):
if (FlowUtils.ShouldBreak(call, context))
{
    break;
}

// After:
var breakResult = FlowUtils.EvaluateBreakIf(call, context);
if (breakResult is DriverResult.Suspend) return breakResult;
if (breakResult is { IsFatal: true }) return breakResult;
if (breakResult != null) break;
```

**Rationale:** Suspend/fatal from condition verb must surface to the caller rather than being swallowed.

**Verification:** Compiles; existing `Loop_BreakIfVerb_UsesReturnedBoolean` test still passes.

**If this fails:** Revert to `ShouldBreak` call.

---

### Step 3: Update `SequenceDriver.cs` call site

**Objective:** Same pattern as Step 2.
**Confidence:** High
**Depends on:** Step 1

**File:** `csharp/src/Zoh.Runtime/Verbs/Flow/SequenceDriver.cs`

**Changes:**

```csharp
// Before (lines 20–23):
if (FlowUtils.ShouldBreak(call, context))
{
    break;
}

// After:
var breakResult = FlowUtils.EvaluateBreakIf(call, context);
if (breakResult is DriverResult.Suspend) return breakResult;
if (breakResult is { IsFatal: true }) return breakResult;
if (breakResult != null) break;
```

**Verification:** `Sequence_BreakIfVerb_UsesReturnedBoolean` still passes.

**If this fails:** Revert to `ShouldBreak` call.

---

### Step 4: Update `ForeachDriver.cs` call sites

**Objective:** Replace both `ShouldBreak` and `ShouldContinue` with new API.
**Confidence:** High
**Depends on:** Step 1

**File:** `csharp/src/Zoh.Runtime/Verbs/Flow/ForeachDriver.cs`

**Changes:**

```csharp
// Before (lines 64–72):
if (FlowUtils.ShouldBreak(call, context))
{
    break;
}

if (FlowUtils.ShouldContinue(call, context))
{
    continue;
}

// After:
var breakResult = FlowUtils.EvaluateBreakIf(call, context);
if (breakResult is DriverResult.Suspend) return breakResult;
if (breakResult is { IsFatal: true }) return breakResult;
if (breakResult != null) break;

var continueResult = FlowUtils.EvaluateContinueIf(call, context);
if (continueResult is DriverResult.Suspend) return continueResult;
if (continueResult is { IsFatal: true }) return continueResult;
if (continueResult != null) continue;
```

**Verification:** Existing foreach tests pass.

**If this fails:** Revert to `ShouldBreak`/`ShouldContinue` calls.

---

### Step 5: Fix `WhileDriver.cs` condition evaluation

**Objective:** Add suspend/fatal guard after condition verb execution.
**Confidence:** High
**Depends on:** None (independent of FlowUtils changes)

**File:** `csharp/src/Zoh.Runtime/Verbs/Flow/WhileDriver.cs`

**Changes:**

```csharp
// Before (lines 37–41):
// If subject is a verb, execute it to get the value
if (subjectVal is ZohVerb vSubject)
{
    subjectVal = context.ExecuteVerb(vSubject.VerbValue, context).ValueOrNothing;
}

// After:
if (subjectVal is ZohVerb vSubject)
{
    var condResult = context.ExecuteVerb(vSubject.VerbValue, context);
    if (condResult is DriverResult.Suspend)
        return condResult;
    if (condResult.IsFatal)
        return condResult;
    subjectVal = condResult.ValueOrNothing;
}
```

**Rationale:** Identical pattern to `IfDriver` lines 26–31.

**Verification:** Existing while tests pass; new tests in Step 6 verify suspend/fatal.

**If this fails:** Revert to single `.ValueOrNothing` line.

---

### Step 6: Add tests in `FlowTests.cs`

**Objective:** Lock suspend and fatal propagation with regression tests.
**Confidence:** High
**Depends on:** Steps 1–5

**File:** `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs`

**Changes:**

Add test helper drivers (inner class pattern, matching `TryTests.cs` convention):

```csharp
private class SuspendingDriver : IVerbDriver
{
    public string Namespace => "test";
    public string Name => "suspend_cond";
    public DriverResult Execute(IExecutionContext context, VerbCallAst call) =>
        new DriverResult.Suspend(new Continuation(
            new SleepRequest(100),
            _ => DriverResult.Complete.Ok(ZohBool.True)
        ));
}

private class FatalCondDriver : IVerbDriver
{
    public string Namespace => "test";
    public string Name => "fatal_cond";
    public DriverResult Execute(IExecutionContext context, VerbCallAst call) =>
        DriverResult.Complete.Fatal(new Diagnostic(
            DiagnosticSeverity.Fatal, "test_fatal", "condition fatal", new TextPosition(0, 0, 0)));
}
```

Register in constructor alongside existing drivers. Add these test methods:

1. **`Loop_BreakIfVerb_PropagatesSuspend`** — `/loop -1, breakif: /suspend_cond;, /set *x, 1;;` → result is `DriverResult.Suspend`.
2. **`Loop_BreakIfVerb_PropagatesFatal`** — `/loop -1, breakif: /fatal_cond;, /set *x, 1;;` → result `IsFatal`.
3. **`Sequence_BreakIfVerb_PropagatesSuspend`** — `/sequence breakif: /suspend_cond;, /set *x, 1;, /set *x, 2;;` → result is `DriverResult.Suspend`.
4. **`While_ConditionVerb_PropagatesSuspend`** — `/while /suspend_cond;, /set *x, 1;;` → result is `DriverResult.Suspend`.
5. **`While_ConditionVerb_PropagatesFatal`** — `/while /fatal_cond;, /set *x, 1;;` → result `IsFatal`.

Each test constructs the `VerbCallAst` using the existing `CreateVerbCall` + `AddNamedParam` helpers, executes, and asserts the result type.

**Rationale:** Tests prove the previously-silent failure modes now surface correctly.

**Verification:** `dotnet test --filter "FullyQualifiedName~FlowTests"` — all new and existing tests pass.

**If this fails:** Revert test additions; revert Steps 1–5 if the failures indicate incorrect implementation.

---

## Verification Plan

### Automated Checks

- [x] `dotnet build` in `csharp/src/Zoh.Runtime` — compiles without errors.
- [x] `dotnet test --filter "FullyQualifiedName~FlowTests"` — all tests pass (24).
- [x] `dotnet test` — full suite passes (719, no regressions).

### Manual Verification

- [x] `FlowUtils` no longer exposes `ShouldBreak`/`ShouldContinue` (old API removed).
- [x] `WhileDriver` condition evaluation includes suspend/fatal guard.
- [x] Each driver call site has the three-line propagation pattern.

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `FlowUtils.EvaluateBreakIf` returns `DriverResult?` | Read `FlowUtils.cs` | New method signature |
| Loop propagates suspend from breakif | `Loop_BreakIfVerb_PropagatesSuspend` | `Assert.IsType<DriverResult.Suspend>` |
| Loop propagates fatal from breakif | `Loop_BreakIfVerb_PropagatesFatal` | `Assert.True(result.IsFatal)` |
| Sequence propagates suspend from breakif | `Sequence_BreakIfVerb_PropagatesSuspend` | `Assert.IsType<DriverResult.Suspend>` |
| While propagates suspend from condition | `While_ConditionVerb_PropagatesSuspend` | `Assert.IsType<DriverResult.Suspend>` |
| While propagates fatal from condition | `While_ConditionVerb_PropagatesFatal` | `Assert.True(result.IsFatal)` |
| No regressions | Full `dotnet test` | All pass |

---

## Rollback Plan

1. Restore `FlowUtils.cs` to `ShouldBreak`/`ShouldContinue`/`ResolveConditionValue` form.
2. Restore `LoopDriver.cs`, `SequenceDriver.cs`, `ForeachDriver.cs` call sites to `FlowUtils.ShouldBreak(call, context)`.
3. Restore `WhileDriver.cs` line 40 to `.ValueOrNothing`.
4. Remove new test methods and helper drivers from `FlowTests.cs`.
5. Run `dotnet test` to confirm baseline restored.

---

## Notes

### Risks

- **`ForeachDriver` conflict with `2603251602`:** The open foreach-iterator-reference plan touches `ForeachDriver` parameter parsing. The changes here are in the iteration loop body (break/continue guard), not parameter validation — low conflict risk. Coordinate merge order if both land close together.

### Open Questions

- None. Approach is the same pattern `IfDriver` already uses.
