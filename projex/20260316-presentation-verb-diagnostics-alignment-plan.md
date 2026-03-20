# Presentation Verb Diagnostics Alignment — C# Implementation

> **Status:** In Progress
> **Created:** 2026-03-16
> **Author:** Claude
> **Source:** Spec commit `cd9af5f`; impl spec `10_std_verbs.md`
> **Related Projex:** `20260316-spec-catchup-followup.md`, `2603201630-timeout-consistency-fixes-plan.md`
> **Worktree:** Yes

> **Reviewed:** 2026-03-20 - `20260316-presentation-verb-diagnostics-alignment-plan-review.md`
> **Review Outcome:** Revised — P1 (timeout<=0) moved out of scope; Step 4 gap 2 removed; Step 3 PromptDriver return type clarified; ConverseDriver deviation noted

---

## Summary

Align ChooseDriver, ConverseDriver, PromptDriver, and ChooseFromDriver with the two-phase suspension spec. Three remaining gaps: visibility verb execution, empty choices warning, and incomplete resume outcome handling. Timeout<=0 diagnostic is tracked separately in `2603201630-timeout-consistency-fixes-plan.md`.

**Scope:** Presentation verb drivers only — no handler interface changes
**Estimated Changes:** 4 files modified

---

## Objective

### Problem / Gap / Need

The ChooseDriver spec (`impl/10_std_verbs.md`) was simplified in commit `cd9af5f` to align with the verb driver model. The C# drivers were partially updated to use `Suspend`/`Resume` but miss three diagnostic behaviors:

1. **VerbValue visibility:** Spec requires executing verb values for visibility checks. C# only calls `IsTruthy()`. Applies to `ChooseDriver` only — `ChooseFromDriver` receives an already-evaluated list of maps and does not invoke the evaluator for visibility.
2. **Empty choices:** Spec requires `WARNING("no_choices", "No visible choices")`. C# returns `Ok(Nothing)` silently. Applies to `ChooseDriver` and `ChooseFromDriver`.
3. **Resume outcome handling:** Spec differentiates `TimedOut` (INFO) and `Cancelled` (ERROR). C# uses `_ => Ok()` for all non-Completed outcomes. Applies to all four drivers.

Timeout<=0 diagnostic (formerly Gap 1) is out of scope here — addressed by spec plan `2603201630-timeout-consistency-fixes-plan.md` and its subsequent C# follow-up.

### Success Criteria

- [ ] `ChooseDriver` executes verb values for visibility checks
- [ ] `ChooseDriver` and `ChooseFromDriver` return WARNING for empty choices
- [ ] All four drivers differentiate `WaitTimedOut` (INFO) and `WaitCancelled` (ERROR) in resume handlers
- [ ] `dotnet test` passes

### Out of Scope

- Timeout <= 0 diagnostic (tracked in `2603201630-timeout-consistency-fixes-plan.md`)
- Handler interface changes (request record fields)
- ChooseValidator enhancements (text type validation)
- New presentation verbs

---

## Context

### Current State

All four drivers follow the same pattern: parse params → build request → call handler → return `Suspend(HostRequest, onFulfilled)`. The `onFulfilled` callback matches `WaitCompleted` but uses a catch-all `_ => Complete.Ok()` for other outcomes.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | `/choose` verb | All 4 fixes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs` | `/converse` verb | Fix timeout + resume |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs` | `/prompt` verb | Fix timeout + resume |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | `/choosefrom` verb | All 4 fixes (if present) |

### Dependencies

- **Requires:** None
- **Blocks:** Nothing

### Constraints

- Diagnostic codes must match spec: `"timeout"`, `"no_choices"`
- Severity levels must match spec: INFO for timeout, WARNING for empty choices, ERROR for cancelled

### Assumptions

- `DriverResult.Complete.WithDiagnostics` or equivalent helper exists (based on EraseDriver pattern)
- `WaitTimedOut` and `WaitCancelled` record types already defined in `WaitOutcome.cs`
- `ChooseFromDriver` iterates an already-evaluated `ZohList` of maps — it does not evaluate AST nodes for visibility (confirmed by review)
- `ConverseDriver` uses a bulk `ConverseRequest(..., contents)` rather than the spec's sequential `converseNext(...)` suspension model. This deviation is acknowledged and kept as-is since handler interface changes are out of scope.

### Impact Analysis

- **Direct:** Four presentation verb drivers
- **Adjacent:** Hosts that handle timeout/cancellation will now see proper diagnostics
- **Downstream:** Diagnostic consumers (logging, `/try` catch) get meaningful information instead of silent Nothing

---

## Implementation

### Overview

Apply four targeted fixes across the presentation drivers. Each fix is a small, pattern-matched change.

### Step 1: Fix ChooseDriver — gaps 2, 3, 4

**Objective:** Spec alignment for `/choose`: verb visibility execution, empty choices warning, resume outcome differentiation.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs`

**Changes:**

**1a. VerbValue visibility execution:**
```csharp
// Before:
var visVal = ValueResolver.Resolve(args[i], ctx);
if (!visVal.IsTruthy()) continue;

// After:
var visVal = ValueResolver.Resolve(args[i], ctx);
if (visVal is ZohVerb verbVis)
{
    var visResult = ctx.ExecuteVerb(verbVis.VerbValue, ctx);
    visVal = visResult is DriverResult.Complete vc ? vc.Value : ZohValue.Nothing;
}
if (!visVal.IsTruthy()) continue;
```

**1b. Empty choices warning:**
```csharp
// Before:
if (choices.Count == 0 && timeoutMs == null)
    return DriverResult.Complete.Ok(ZohValue.Nothing);

// After:
if (choices.Count == 0)
    return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
        new Diagnostic(DiagnosticSeverity.Warning, "no_choices", "No visible choices", verb.Start)));
```

**1c. Resume outcome differentiation:**
```csharp
// Before:
outcome => outcome switch
{
    WaitCompleted c => DriverResult.Complete.Ok(c.Value),
    _ => DriverResult.Complete.Ok()
}

// After:
outcome => outcome switch
{
    WaitCompleted c => DriverResult.Complete.Ok(c.Value),
    WaitTimedOut => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
        new Diagnostic(DiagnosticSeverity.Info, "timeout", "Choose timed out", verb.Start))),
    WaitCancelled wc => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
        new Diagnostic(DiagnosticSeverity.Error, wc.Code, wc.Message, verb.Start))),
    _ => DriverResult.Complete.Ok()
}
```

**Verification:** Compile check. Test verb visibility returns false correctly excludes choice. Test empty choices returns WARNING. Test resume differentiation.

### Step 2: Fix ConverseDriver — resume outcome differentiation

**Objective:** Align `/converse` resume diagnostics.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs`

**Changes:**

Same pattern as Step 1c (resume outcome differentiation). Apply to the corresponding location in ConverseDriver.

**Verification:** Compile check. Resume differentiates `WaitTimedOut` (INFO) and `WaitCancelled` (ERROR) outcomes.

### Step 3: Fix PromptDriver — resume outcome differentiation

**Objective:** Align `/prompt` resume diagnostics.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs`

**Changes:**

Same pattern as Step 1c (resume outcome differentiation). When `WaitTimedOut`, return `ZohValue.Nothing` (not `ZohStr("")`) — spec's `info("timeout")` contract standardizes to `Nothing`. Apply to the resume handler in PromptDriver.

**Verification:** Compile check. Resume differentiates `WaitTimedOut` (INFO, `Nothing`) and `WaitCancelled` (ERROR) outcomes.

### Step 4: Fix ChooseFromDriver — gaps 3, 4

**Objective:** Align `/choosefrom` for empty choices warning and resume outcome differentiation.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs`

**Changes:**

Apply Step 1b (empty choices warning) and Step 1c (resume outcome differentiation). Do **not** apply Step 1a — `ChooseFromDriver` receives an already-evaluated `ZohList` of maps and checks the `visible` key directly; it does not evaluate AST nodes or execute verbs for visibility.

**Verification:** Compile check. Empty list returns WARNING. Resume differentiates outcomes.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` — all existing tests pass
- [ ] New tests: verb visibility returning false excludes choice (ChooseDriver)
- [ ] New tests: empty choices returns WARNING (ChooseDriver, ChooseFromDriver)
- [ ] New tests: resume with WaitTimedOut returns INFO, `Nothing`
- [ ] New tests: resume with WaitCancelled returns ERROR

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Verb visibility | Choice with verb visibility that returns false | Choice excluded |
| Empty choices | All choices filtered out | WARNING with code `"no_choices"` |
| Resume timeout | Fulfill with WaitTimedOut | INFO, `Nothing` |
| Resume cancel | Fulfill with WaitCancelled | ERROR with cancel code/message |

---

## Rollback Plan

1. Revert each driver file independently — changes are isolated per driver
2. No cross-file dependencies

---

## Notes

### Risks

- **ChooseFromDriver structure:** May differ from ChooseDriver more than expected. Mitigation: read the actual file during execution and adapt.
- **PromptDriver default value:** Spec may specify a default return value for timeout (empty string vs Nothing). Mitigation: check `impl/10_std_verbs.md` PromptDriver section during execution.

### Open Questions

None.
