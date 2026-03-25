# Memo: `breakif` / `continueif` verb — suspend & fatal vs `IfDriver` (audit **C**)

> **Date:** 2026-03-25  
> **Author:** Agent  
> **Source Type:** Issue  
> **Origin:** Post-patch audit (user request: “memo-projex C”)

---

## Source

`FlowUtils.ResolveConditionValue` executes a `ZohVerb` condition with `context.ExecuteVerb(...).ValueOrNothing` only. It does **not** return `DriverResult.Suspend` or propagate `IsFatal` the way `IfDriver` does for a verb **subject** (early return of `subjectResult`). For `Complete.Fatal`, `ValueOrNothing` is often `Nothing` → `IsTruthy()` false → loop/sequence continues without surfacing the fatal to the outer driver. Suspend collapses to “falsy” via `ValueOrNothing` on non-`Complete` results. Patch `2603251825-phase4-flowutils-breakif-verb-patch.md` Notes acknowledge suspend non-propagation and align with `WhileDriver` subject handling; fatal visibility remains an open product/spec question.

---

## Context

`ShouldBreak` / `ShouldContinue` expose `bool`, so propagating `DriverResult` would require API changes in callers (`LoopDriver`, `ForeachDriver`, `SequenceDriver`) or a side channel (e.g. diagnostics on context). No spec text was re-read for this memo; decision is deferred: either document as intentional (While-style) or plan a follow-up to match `IfDriver` semantics if spec requires it.

---

## Related Projex

- `20260227-phase4-control-flow-gaps-fix-plan.md`
- `2603251603-phase4-flowutils-breakif-verb-plan.md`
- `2603251825-phase4-flowutils-breakif-verb-patch.md`
- `2603252100-phase4-sequence-breakif-verb-test-gap-memo.md`
