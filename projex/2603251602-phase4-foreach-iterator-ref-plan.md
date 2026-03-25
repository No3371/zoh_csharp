# Phase 4: `/foreach` Iterator Reference

> **Status:** Ready
> **Created:** 2026-03-25
> **Author:** Agent (split from `20260227-phase4-control-flow-gaps-fix-plan.md`)
> **Source:** Split of `20260227-phase4-control-flow-gaps-fix-plan.md` Step 3 + matching tests
> **Related Projex:** `20260227-phase4-control-flow-gaps-fix-plan.md`, `2603251430-20260227-phase4-control-flow-gaps-fix-plan-review.md`
> **Worktree:** Yes

---

## Summary

Treat the second parameter as `ValueAst.Reference` and bind iterator name from the reference identifier; stop resolving it to a string value (fixes `*it`).

**Scope:** `ForeachDriver.cs` + regression test.
**Estimated Changes:** 1 driver file + `FlowTests.cs`.

---

## Objective

### Problem / Gap / Need

`ValueResolver.Resolve` on iterator yields non-string or wrong path; spec expects a reference iterator name.

### Success Criteria

- [ ] Second param must be `ValueAst.Reference`; use `iteratorRef.Name` as bound variable name.
- [ ] Invalid second param → fatal with clear diagnostic (e.g. `invalid_type` / message per existing style).
- [ ] `/foreach *list, *it, /verb;;` works without `Variable name must be a string`.
- [ ] Map iteration and break/continue behavior unchanged.
- [ ] Full `dotnet test` passes.

### Out of Scope

- Other drivers — sibling plans.

---

## Context

### Key Files

| File | Role | Change Summary |
|------|------|----------------|
| `csharp/src/Zoh.Runtime/Verbs/Flow/ForeachDriver.cs` | `/foreach` | Iterator from reference AST |
| `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs` | Regressions | e.g. `Foreach_AcceptsReferenceIterator` |

### Dependencies

- **Requires:** None.
- **Parallel:** Other `260325160x-phase4-*-plan.md` splits.

---

## Implementation

### Step 1: `ForeachDriver.cs`

**Objective:** Reference-only iterator binding.

```csharp
if (call.UnnamedParams[1] is not ValueAst.Reference iteratorRef)
    return VerbResult.Fatal(new Diagnostic(..., "invalid_type", "Iterator must be a reference.", ...));
var varName = iteratorRef.Name;
```

**Verification:** New foreach test; existing foreach tests still pass.

### Step 2: `FlowTests.cs`

**Objective:** Regression with `*item` or `*it` iterator.

---

## Verification Plan

- [ ] `dotnet test --filter "FullyQualifiedName~Foreach"`
- [ ] `dotnet test`

---

## Rollback Plan

Revert `ForeachDriver.cs` and new test.

---

## Notes

### Open Questions

- [ ] None.
