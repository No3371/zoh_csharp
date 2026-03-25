# Phase 4: `breakif` / `continueif` Verb Conditions

> **Status:** Ready
> **Created:** 2026-03-25
> **Author:** Agent (split from `20260227-phase4-control-flow-gaps-fix-plan.md`)
> **Source:** Split of `20260227-phase4-control-flow-gaps-fix-plan.md` Step 4 + matching tests
> **Related Projex:** `20260227-phase4-control-flow-gaps-fix-plan.md`, `2603251430-20260227-phase4-control-flow-gaps-fix-plan-review.md`
> **Worktree:** No

---

## Summary

`ShouldBreak` / `ShouldContinue` in `FlowUtils` must execute `ZohVerb` conditions and use the return value for truthiness (not treat the verb object as truthy).

**Scope:** `FlowUtils.cs` + loop/sequence tests.
**Estimated Changes:** 1 utility file + `FlowTests.cs`.

---

## Objective

### Problem / Gap / Need

Resolved `ZohVerb` in `breakif:` / `continueif:` is truthy without execution; spec uses returned value.

### Success Criteria

- [ ] After resolve, if `ZohVerb`, execute and use `.Value` (or consistent pattern with other drivers) for `IsTruthy()`.
- [ ] Same logic for `ShouldBreak` and `ShouldContinue`.
- [ ] New test(s): e.g. `Loop_BreakIfVerb_UsesReturnedBoolean` and optional `continueif` mirror.
- [ ] Full `dotnet test` passes.

### Out of Scope

- Other drivers — sibling plans.

---

## Context

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `csharp/src/Zoh.Runtime/Verbs/Flow/FlowUtils.cs` | `breakif` / `continueif` | Execute verb before truthiness |
| `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs` | Regressions | Verb condition tests |

### Dependencies

- **Requires:** None.
- **Parallel:** Other `260325160x-phase4-*-plan.md` splits.

---

## Implementation

### Step 1: `FlowUtils.cs`

**Objective:** Align with spec for verb conditions.

```csharp
var resolved = ValueResolver.Resolve(val, context);
if (resolved is ZohVerb condVerb)
    resolved = context.ExecuteVerb(condVerb.VerbValue, context).Value;
return resolved.IsTruthy();
```

Apply in both `ShouldBreak` and `ShouldContinue`.

**Verification:** New tests; existing loop/sequence tests pass.

### Step 2: `FlowTests.cs`

**Objective:** `breakif: /verb;` stops when returned boolean true; mirror for `continueif` if feasible.

---

## Verification Plan

- [ ] `dotnet test --filter "FullyQualifiedName~FlowTests"`
- [ ] `dotnet test`

---

## Rollback Plan

Revert `FlowUtils.cs` and new tests.

---

## Notes

### Risks

- Edge cases in control flow — keep change limited to verb execution; rely on existing loop tests.

### Open Questions

- [ ] None.
