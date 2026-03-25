# Phase 4: `/do` Execute Returned Verb

> **Status:** Complete
> **Created:** 2026-03-25
> **Completed:** 2026-03-25
> **Patch / walkthrough:** `2603251810-phase4-do-returned-verb-patch.md` (same folder)
> **Author:** Agent (split from `20260227-phase4-control-flow-gaps-fix-plan.md`)
> **Source:** Split of `20260227-phase4-control-flow-gaps-fix-plan.md` Step 5 + matching tests
> **Related Projex:** `20260227-phase4-control-flow-gaps-fix-plan.md`, `2603251430-20260227-phase4-control-flow-gaps-fix-plan-review.md`
> **Worktree:** No

---

## Summary

When the first `/do` argument executes and returns a `ZohVerb`, run that returned verb once and surface the second execution’s result (`spec/2_verbs.md` `/do /verb_returning;;`).

**Scope:** `DoDriver.cs` + `ControlFlowVerbsTests.cs` (existing home for `/do` tests).
**Estimated Changes:** 1 driver file + test file.

---

## Objective

### Problem / Gap / Need

`DoDriver` executes the first verb only; does not execute a verb **returned** by that run.

### Success Criteria

- [x] First arg resolves to `ZohVerb` → execute first verb.
- [x] If first result value is `ZohVerb`, execute second verb; return second result (respect fatals on first hop).
- [x] Single extra hop only (not unbounded recursion) — per umbrella plan assumption.
- [x] New test: `Do_ExecutesVerbReturnedByFirstExecution` (or equivalent).
- [x] Full `dotnet test` passes.

### Out of Scope

- Unbounded or recursive `/do` chains.
- Other drivers — sibling plans.

---

## Context

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `csharp/src/Zoh.Runtime/Verbs/Flow/DoDriver.cs` | `/do` | Second `ExecuteVerb` when first result is verb |
| `csharp/tests/Zoh.Tests/Verbs/Core/ControlFlowVerbsTests.cs` | `/do` tests | Returned-verb chain |

### Dependencies

- **Requires:** None.
- **Parallel:** Other `260325160x-phase4-*-plan.md` splits.

---

## Implementation

### Step 1: `DoDriver.cs`

**Objective:** Two-hop execution when applicable.

```csharp
if (val is not ZohVerb v)
    return fatal(invalid_type);

var first = context.ExecuteVerb(v.VerbValue, context);
if (first.IsFatal) return first;

if (first.Value is ZohVerb returnedVerb)
    return context.ExecuteVerb(returnedVerb.VerbValue, context);

return first;
```

Adjust to match exact types (`VerbResult`, `ValueOrNothing`, etc.) in codebase.

**Verification:** New test + existing `Do_ExecutesVerb` / related tests.

### Step 2: `ControlFlowVerbsTests.cs`

**Objective:** First execution returns verb; second returns terminal value.

---

## Verification Plan

- [x] `dotnet test --filter "FullyQualifiedName~ControlFlowVerbsTests"`
- [x] `dotnet test`

---

## Rollback Plan

Revert `DoDriver.cs` and new test.

---

## Notes

### Assumptions

- One follow-up execution is sufficient for spec examples.

### Open Questions

- [ ] None.
