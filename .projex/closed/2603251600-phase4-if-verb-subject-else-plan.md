# Phase 4: `/if` Verb Subject + Named `else`

> **Status:** Complete
> **Created:** 2026-03-25
> **Completed:** 2026-03-25
> **Walkthrough:** `2603251600-phase4-if-verb-subject-else-walkthrough.md`
> **Author:** Agent (split from `20260227-phase4-control-flow-gaps-fix-plan.md`)
> **Source:** Split of `20260227-phase4-control-flow-gaps-fix-plan.md` Step 1 + matching tests
> **Related Projex:** `20260227-phase4-control-flow-gaps-fix-plan.md` (umbrella), `2603251430-20260227-phase4-control-flow-gaps-fix-plan-review.md`
> **Worktree:** No

---

## Summary

Complete `/if` per `spec/2_verbs.md`: execute a verb **subject** before the default boolean/nothing guard and comparison; support named **`else:`** with positional third argument as fallback.

**Scope:** `IfDriver.cs` and `/if` regression tests only.
**Estimated Changes:** 1 driver file + additions to `FlowTests.cs`.

---

## Objective

### Problem / Gap / Need

- Verb subject resolves to `ZohVerb` but is not executed before `is:true` typing and branching.
- Named `else` is not read from `NamedParams`; only positional third unnamed param works.

### Success Criteria

- [x] After resolve, if subject is `ZohVerb`, execute it; use return value for default `is:true` guard and for `is:` comparison.
- [x] Named `else:` works; positional third arg remains when named `else` absent.
- [x] `/if *x, is: "a", /then;, else: /else;;` branches correctly (named `else` + `is:` semantics preserved; covered by `If_UsesNamedElse` and existing tests).
- [x] Non-bool/nothing evaluated subject with `is` omitted → fatal `invalid_type`.
- [x] Targeted and full `dotnet test` pass.

### Out of Scope

- Other flow drivers (`/switch`, `/foreach`, `breakif`, `/do`) — sibling plans.
- Parser/AST changes.

---

## Context

### Current State

`IfDriver.cs` reads named `is`, applies default `is:true` to **resolved** subject, but does not execute verb subjects. Does not read named `else`.

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `csharp/src/Zoh.Runtime/Verbs/Flow/IfDriver.cs` | `/if` | Execute verb subject before guard/compare; named `else` + positional fallback |
| `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs` | Regressions | `If_VerbSubject…`, `If_UsesNamedElse`, `If_DefaultComparison_InvalidTypeAfterSubjectEval` (names may vary) |

### Dependencies

- **Requires:** None.
- **Blocks:** None (parallel with other Phase 4 split plans).
- **Parallel:** `2603251601-phase4-switch-verb-case-plan.md`, `2603251602-phase4-foreach-iterator-ref-plan.md`, `2603251603-phase4-flowutils-breakif-verb-plan.md`, `2603251604-phase4-do-returned-verb-plan.md`

### Constraints

- Preserve named `is` and type-keyword `is` behavior.
- Diagnostics: `invalid_type`, `parameter_not_found` style consistent with codebase.

---

## Implementation

### Overview

Post-resolve: if `ZohVerb`, `ExecuteVerb` and replace subject with return value; then existing logic. Else branch: `NamedParams["else"]` else `UnnamedParams.Length >= 3`.

### Step 1: `IfDriver.cs`

**Objective:** Verb subject execution + named `else`.
**Depends on:** None

**Files:**

- `csharp/src/Zoh.Runtime/Verbs/Flow/IfDriver.cs`

**Changes (shape):**

```csharp
var subject = ValueResolver.Resolve(call.UnnamedParams[0], context);
if (subject is ZohVerb subjectVerb)
    subject = context.ExecuteVerb(subjectVerb.VerbValue, context).ValueOrNothing;

// existing named "is", default guard, type-keyword compare
// else: NamedParams["else"] else positional third unnamed param
```

**Verification:** New tests below; no regressions in existing `/if` tests.

### Step 2: Tests in `FlowTests.cs`

**Objective:** Lock semantics.

**Files:**

- `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs`

**Verification:** `dotnet test --filter "FullyQualifiedName~If_"` (or chosen test names).

---

## Verification Plan

### Automated Checks

- [x] `dotnet test --filter "FullyQualifiedName~FlowTests"` (or narrower if tests named consistently)
- [x] `dotnet test`

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Verb subject order | New test | Subject verb runs before branch |
| Named `else` | New test | Named branch taken |
| Default type guard | New test | `invalid_type` after evaluated non-bool subject |

---

## Rollback Plan

1. Revert `IfDriver.cs` and new/edited tests in `FlowTests.cs`.
2. Re-run tests.

---

## Notes

### Risks

- Scripts relying on wrong subject order may change behavior — mitigated by spec alignment and tests.

### Open Questions

- [ ] None.
