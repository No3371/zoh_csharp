# Add Per-Statement State to Context (C# Implementation)

> **Status:** Complete
> **Created:** 2026-03-05
> **Author:** agent
> **Source:** 20260304-statement-cache-staging-eval.md
> **Completed:** 2026-03-05
> **Walkthrough:** 20260305-statement-state-impl-walkthrough.md
> **Related Projex:** 20260305-statement-state-spec-plan.md, 20260304-std-verbs-driver-alignment-plan-review.md

---

## Summary

Implement the `statementState` field specified in `impl/09_runtime.md` in the C# runtime. Add `StatementState` property to `Context.cs`, clear it in `ApplyResult` (Complete), `Terminate`, and `ExitStory`. Update the source eval's open questions to reflect the final design.

**Scope:** `csharp/src/Zoh.Runtime/Execution/Context.cs` + `projex/20260304-statement-cache-staging-eval.md`
**Estimated Changes:** 2 files

---

## Objective

### Problem / Gap / Need

The spec (once 20260305-statement-state-spec-plan.md lands) defines `statementState` on Context. The C# implementation needs to mirror it.

### Success Criteria

- [ ] `StatementState` property of type `Dictionary<string, object>?` on `Context`
- [ ] Cleared in `ApplyResult` Complete branch
- [ ] Cleared in `Terminate`
- [ ] Cleared in `ExitStory`
- [ ] NOT cleared in `ApplyResult` Suspend branch
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes (backward compatible — no existing code touches `StatementState`)
- [ ] Eval open questions updated

### Out of Scope

- Spec changes (separate plan: 20260305-statement-state-spec-plan.md)
- Rewriting drivers to use `StatementState`

---

## Context

### Current State

`Context.cs` has `PendingContinuation` and `ResumeToken` as waiting state. `ApplyResult` (L84–110) handles Complete (set result, advance IP) and Suspend (store continuation, block). `Terminate` (L203–214) runs defers and unsubscribes signals. `ExitStory` (L225–229) runs story defers and clears story variables.

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `csharp/src/Zoh.Runtime/Execution/Context.cs` | C# Context | Add property, clearing in 3 sites |
| `projex/20260304-statement-cache-staging-eval.md` | Source eval | Update open questions |

### Dependencies

- **Requires:** 20260305-statement-state-spec-plan.md (spec must define field first)
- **Blocks:** Alignment plan execution (drivers can use `StatementState` once available)

---

## Implementation

### Step 1: Add `StatementState` Property

**Objective:** Add the property to Context.

**Files:** `csharp/src/Zoh.Runtime/Execution/Context.cs`

**Changes:** Insert after `ResumeToken` (L35):

```csharp
// Before (L34–35):
    public Continuation? PendingContinuation { get; private set; }
    public int ResumeToken { get; private set; }

// After:
    public Continuation? PendingContinuation { get; private set; }
    public int ResumeToken { get; private set; }

    /// <summary>
    /// Per-statement driver-private scratch space. Persists across suspend/resume
    /// for the same statement. Cleared on Complete, Terminate, and ExitStory.
    /// </summary>
    public Dictionary<string, object>? StatementState { get; set; }
```

**Rationale:** `public set` — drivers write freely. Nullable — `null` means no state allocated.

**Verification:** `dotnet build` succeeds.

### Step 2: Add Clearing to `ApplyResult`, `Terminate`, `ExitStory`

**Objective:** Implement clearing semantics matching the spec.

**Files:** `csharp/src/Zoh.Runtime/Execution/Context.cs`

**Changes to `ApplyResult` (L84–110) — insert in Complete branch:**

```csharp
// Before (L88–91):
            case DriverResult.Complete c:
                LastResult = c.Value;
                LastDiagnostics = c.Diagnostics;
                if (c.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))

// After:
            case DriverResult.Complete c:
                LastResult = c.Value;
                LastDiagnostics = c.Diagnostics;
                StatementState = null;
                if (c.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
```

**Changes to `Terminate` (L203–214):**

```csharp
// Before (L210–211):
        ExecuteDefers(_contextDefers);

        SignalManager.UnsubscribeContext(this);

// After:
        ExecuteDefers(_contextDefers);

        StatementState = null;
        SignalManager.UnsubscribeContext(this);
```

**Changes to `ExitStory` (L225–229):**

```csharp
// Before (L227–228):
        ExecuteDefers(_storyDefers);
        Variables.ClearStory();

// After:
        ExecuteDefers(_storyDefers);
        StatementState = null;
        Variables.ClearStory();
```

**Rationale:** Three clearing sites: statement done (Complete), context done (Terminate), story exit (ExitStory). `ExitStory` is called by the jump driver on cross-story jumps — belt-and-suspenders alongside the `applyResult/Complete` clearing.

**Verification:** `dotnet build` && `dotnet test` — all existing tests pass unchanged.

### Step 3: Update Eval Open Questions

**Objective:** Reflect the final design in the source eval.

**Files:** `projex/20260304-statement-cache-staging-eval.md`

**Changes:** Both open questions resolved:

1. **Q1 (type system):** Resolved — host-native (`object`/`Dictionary<string, object>?`), not Zoh `Value`.
2. **Q2 (outcome delivery):** Resolved — N/A. Outcome delivered via normal `onFulfilled(outcome)` callback. The `Reinvoke` variant, nullable `onFulfilled`, `reinvoke` boolean, and `reinvoke()` method were all explored and rejected. `Continuation` stays unchanged. Staging is a driver-level convention using existing closures + `statementState`.

**Verification:** No unresolved open questions remain.

---

## Verification Plan

### Automated Checks

- [ ] `cd csharp && dotnet build` — compiles
- [ ] `cd csharp && dotnet test` — all tests pass

### Manual Verification

- [ ] `StatementState` property exists with correct type and xmldoc
- [ ] `ApplyResult` Complete branch: `StatementState = null` present
- [ ] `ApplyResult` Suspend branch: `StatementState` NOT touched
- [ ] `Terminate`: `StatementState = null` present
- [ ] `ExitStory`: `StatementState = null` present

---

## Rollback Plan

Remove property and three `= null` assignments. No existing code depends on `StatementState`.

---

## Notes

### Assumptions

- `ExitStory()` is called by the jump driver on cross-story jumps. Within-story jumps return `Complete`, clearing via `applyResult`.
- No existing tests or drivers reference `StatementState`, so the change is purely additive.
