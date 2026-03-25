# Phase 4: `/switch` Verb Case Evaluation

> **Status:** Ready
> **Created:** 2026-03-25
> **Author:** Agent (split from `20260227-phase4-control-flow-gaps-fix-plan.md`)
> **Source:** Split of `20260227-phase4-control-flow-gaps-fix-plan.md` Step 2 + matching tests
> **Related Projex:** `20260227-phase4-control-flow-gaps-fix-plan.md`, `2603251430-20260227-phase4-control-flow-gaps-fix-plan-review.md`
> **Worktree:** No

---

## Summary

Execute verb-valued **case** operands before equality comparison, matching subject-side behavior already in `SwitchDriver`.

**Scope:** `SwitchDriver.cs` and one focused regression test.
**Estimated Changes:** 1 driver file + `FlowTests.cs`.

---

## Objective

### Problem / Gap / Need

Case values that resolve to `ZohVerb` are compared without execution; spec requires return values for comparison.

### Success Criteria

- [ ] Each case operand: resolve; if `ZohVerb`, execute and use return value for `Equals` against tested value.
- [ ] New test: verb case returns matching value → correct branch.
- [ ] Full `dotnet test` passes.

### Out of Scope

- Subject evaluation (already correct).
- `/if`, `/foreach`, `breakif`, `/do` — sibling plans.

---

## Context

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `csharp/src/Zoh.Runtime/Verbs/Flow/SwitchDriver.cs` | `/switch` | Execute verb cases before compare |
| `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs` | Regressions | e.g. `Switch_EvaluatesVerbCaseValues` |

### Dependencies

- **Requires:** None.
- **Parallel:** `2603251600-phase4-if-verb-subject-else-plan.md`, `2603251602-phase4-foreach-iterator-ref-plan.md`, `2603251603-phase4-flowutils-breakif-verb-plan.md`, `2603251604-phase4-do-returned-verb-plan.md`

---

## Implementation

### Step 1: `SwitchDriver.cs`

**Objective:** Verb case execution.

```csharp
var caseValue = ValueResolver.Resolve(call.UnnamedParams[caseIndex], context);
if (caseValue is ZohVerb caseVerb)
    caseValue = context.ExecuteVerb(caseVerb.VerbValue, context).Value;
if (caseValue.Equals(testValue)) ...
```

**Verification:** Existing switch tests + new verb-case test.

### Step 2: `FlowTests.cs`

**Objective:** `Switch_EvaluatesVerbCaseValues` (or equivalent).

---

## Verification Plan

- [ ] `dotnet test --filter "FullyQualifiedName~Switch_"`
- [ ] `dotnet test`

---

## Rollback Plan

Revert `SwitchDriver.cs` and the new test.

---

## Notes

### Open Questions

- [ ] None.
