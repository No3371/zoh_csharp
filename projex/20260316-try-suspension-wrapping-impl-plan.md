# Try Suspension Wrapping — C# Implementation

> **Status:** In Progress
> **Created:** 2026-03-16
> **Author:** Claude
> **Source:** Spec commit `8a243ee`; impl spec `07_control_flow.md` lines 393–440
> **Related Projex:** `20260316-spec-catchup-followup.md`, `20260315-channel-semantics-try-suspension-plan.md` (closed, spec-side)
> **Worktree:** Yes
> **Reviewed:** 2026-03-20 — `20260320-try-suspension-wrapping-impl-plan-review.md`
> **Review Outcome:** Valid — proceed to execute. Minor: rename `catchHandler` → `catchVerb` in proposed code; note catch-suspension edge case as known pre-existing issue.

---

## Summary

Fix TryDriver to wrap suspended continuations so that `/try`'s catch/suppress/downgrade logic applies to the eventual post-resume result, not just the initial execution result. Currently, if the inner verb suspends, `/try` returns the Suspend unwrapped — fatals after resume bypass catch entirely.

**Scope:** `TryDriver.cs` only
**Estimated Changes:** 1 file modified

---

## Objective

### Problem / Gap / Need

`TryDriver.Execute` checks `result.IsFatal` after executing the inner verb. If the inner verb returns `Suspend` (e.g., `/pull`, `/wait`, `/sleep`, presentation verbs), `IsFatal` is false, so the Suspend passes through unwrapped. When the scheduler later resumes and the continuation produces a fatal, `/try` never sees it — the fatal terminates the context unhandled.

Spec (impl `07_control_flow.md`) defines a `handleTryResult` helper that intercepts `Suspend` results by wrapping the continuation's `onFulfilled` callback to recursively apply try logic.

### Success Criteria

- [ ] TryDriver wraps Suspend continuations with a `HandleTryResult` helper
- [ ] Wrapped continuations preserve the original `WaitRequest`
- [ ] Fatal diagnostics from resumed continuations are downgraded to errors
- [ ] Catch handler executes for fatals after resume
- [ ] `[suppress]` clears diagnostics from resumed results
- [ ] Chained suspensions (Suspend → resume → Suspend → resume) handled recursively
- [ ] All existing tests pass
- [ ] New tests cover suspension wrapping scenarios

### Out of Scope

- Channel semantics changes (already in spec, no C# runtime changes needed beyond this)
- Other verb drivers' suspension behavior

---

## Context

### Current State

`TryDriver.cs` (`src/Zoh.Runtime/Verbs/Core/TryDriver.cs`) executes the inner verb, then checks `if (result.IsFatal)`. Suspend results fall through to `return result;` at the bottom — no wrapping.

The continuation/suspension infrastructure is already in place:
- `Continuation(WaitRequest, Func<WaitOutcome, DriverResult>)` — `src/Zoh.Runtime/Verbs/Continuation.cs`
- `DriverResult.Suspend(Continuation, ImmutableArray<Diagnostic>)` — `src/Zoh.Runtime/Verbs/DriverResult.cs`
- `Context.Resume` invokes `PendingContinuation.OnFulfilled(outcome)` and calls `ApplyResult` — `src/Zoh.Runtime/Execution/Context.cs`

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Verbs/Core/TryDriver.cs` | `/try` verb driver | Extract `HandleTryResult`, add Suspend wrapping |

### Dependencies

- **Requires:** None — continuation infrastructure already exists
- **Blocks:** Nothing directly, but correctness prerequisite for any `/try` around suspending verbs

### Constraints

- Wrapped continuation must preserve original `Request` (same wait condition for scheduler)
- Catch handler suspension bypasses outer try's catch (correct behavior per spec)
- No changes to Context.Resume or the scheduler

### Assumptions

- `new Continuation(request, onFulfilled)` can be constructed freely (it's a record)
- Closures over `catchVerb`, `suppressing`, and `context` are safe — execution is single-threaded

### Impact Analysis

- **Direct:** TryDriver.cs
- **Adjacent:** None — the change is internal to the driver's result processing
- **Downstream:** All `/try` usage with suspending inner verbs now correctly handles fatals

---

## Implementation

### Overview

Extract the post-execution logic into a `HandleTryResult` private method. Add a Suspend check at the top that wraps the continuation. Existing Complete-phase logic (downgrade, catch, suppress) is unchanged.

### Step 1: Extract HandleTryResult and add Suspend wrapping

**Objective:** Refactor TryDriver to intercept Suspend results.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Core/TryDriver.cs`

**Changes:**

```csharp
// Before:
public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
{
    // ... parameter parsing ...
    var result = context.ExecuteVerb(targetVerb.VerbValue, context);

    if (result.IsFatal)
    {
        // ... downgrade/catch/suppress logic ...
    }
    return result;
}

// After:
public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
{
    // ... parameter parsing (unchanged) ...
    var result = context.ExecuteVerb(targetVerb.VerbValue, context);
    return HandleTryResult(result, catchVerb, suppressing, context, verb.Start);
}

private static DriverResult HandleTryResult(
    DriverResult result,
    ZohVerb? catchVerb,
    bool suppressDiagnostics,
    IExecutionContext context,
    TextPosition position)
{
    // Phase 1: Suspend — wrap continuation
    if (result is DriverResult.Suspend suspend)
    {
        var original = suspend.Continuation;
        var wrapped = new Continuation(
            original.Request,
            outcome =>
            {
                var nextResult = original.OnFulfilled(outcome);
                return HandleTryResult(nextResult, catchVerb, suppressDiagnostics, context, position);
            }
        );
        return new DriverResult.Suspend(wrapped, suspend.Diagnostics);
    }

    // Phase 2: Complete — existing downgrade/catch/suppress logic (unchanged)
    if (result.IsFatal)
    {
        var resultDiags = result is DriverResult.Complete rc
            ? rc.Diagnostics : ImmutableArray<Diagnostic>.Empty;

        if (catchVerb is not null)
        {
            var catchResult = context.ExecuteVerb(catchVerb.VerbValue, context);
            // Note: if catchResult is Suspend, the suspension bypasses outer try's catch (correct per spec).
            // Suspend from catch is returned as-is below via the final `return result;`.
            var catchValue = catchResult is DriverResult.Complete cc ? cc.Value : ZohNothing.Instance;
            var catchDiags = catchResult is DriverResult.Complete cd ? cd.Diagnostics : ImmutableArray<Diagnostic>.Empty;

            if (catchResult is DriverResult.Suspend)
                return catchResult;

            if (suppressDiagnostics)
                return new DriverResult.Complete(catchValue, catchDiags);

            return new DriverResult.Complete(
                catchValue,
                resultDiags
                    .Select(d => d.Severity == DiagnosticSeverity.Fatal
                        ? new Diagnostic(DiagnosticSeverity.Error, d.Code, d.Message, d.Position)
                        : d)
                    .Concat(catchDiags)
                    .ToImmutableArray());
        }

        if (suppressDiagnostics)
            return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray<Diagnostic>.Empty);

        return new DriverResult.Complete(
            ZohValue.Nothing,
            resultDiags
                .Select(d => d.Severity == DiagnosticSeverity.Fatal
                    ? new Diagnostic(DiagnosticSeverity.Error, d.Code, d.Message, d.Position)
                    : d)
                .ToImmutableArray());
    }

    return result;
}
```

**Rationale:** The recursive call in `HandleTryResult` handles chained suspensions (if `onFulfilled` returns another Suspend, it gets wrapped again). `position` is passed for future diagnostic use if needed.

**Spec model note:** The impl spec pseudocode uses `context.diagnostics.fatal` (a context-level mutable store). The C# runtime instead carries diagnostics on `DriverResult` (immutable, result-level). `result.IsFatal` is the correct C# equivalent — the two models are semantically equivalent in this single-threaded driver path.

**Verification:**
- Compile check
- Existing TryDriver tests pass (Complete-phase logic unchanged)
- New test: `/try /sleep 1;` wraps the Suspend — inspect returned DriverResult

**If this fails:** The only risk is if the closure captures something incorrectly. Since execution is single-threaded, closures over `catchVerb` and `context` are safe.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` — all existing tests pass (including `FlowErrorTests`)

### Manual Verification

- [ ] `/try /sleep 1;` returns `Suspend` with wrapped continuation (not raw)
- [ ] Wrapped continuation, when fulfilled, returns the inner result through try logic
- [ ] `/try /fatal_after_suspend;, catch: /handler;` — catch executes after resume
- [ ] `/try [suppress] /fatal_after_suspend;` — diagnostics cleared after resume
- [ ] Chained suspend: inner verb suspends twice → both wrapped correctly

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Suspend wrapping | Execute `/try` around suspending verb | Returns `DriverResult.Suspend` with different continuation reference |
| Fatal after resume | Resume with fatal-producing outcome | Fatal downgraded to error |
| Catch after resume | Resume with fatal, catch handler provided | Catch handler executes |
| Suppress after resume | Resume with fatal, `[suppress]` set | Diagnostics cleared |
| Chained suspend | Inner verb suspends, resume produces another suspend | Second suspend also wrapped |

---

## Rollback Plan

1. Revert `TryDriver.cs` to inline `Execute` method without `HandleTryResult`
2. Single file, single change — trivial revert

---

## Notes

### Risks

- **Closure lifetime:** The `catchVerb` ZohVerb captured in the closure must remain valid across ticks. Since ZohVerb is immutable data (VerbCallAst reference), this is safe.

### Known Limitations (pre-existing, out of scope)

- **Catch handler that itself suspends:** Before this change, a suspending catch handler would have its `Suspend` silently collapsed to `ZohNothing` inside the catch-value extraction logic. This plan adds an explicit `return catchResult;` guard for that case (see Phase 2 code), so the Suspend propagates correctly. This is a fix bundled with the refactor, not a new gap.

### Open Questions

None.
