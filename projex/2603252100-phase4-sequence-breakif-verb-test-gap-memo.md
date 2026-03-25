# Memo: `sequence` + `breakif:` verb — test gap

> **Status:** Consumed — `2603252220-phase4-sequence-breakif-verb-test-patch.md`  
> **Date:** 2026-03-25  
> **Author:** Agent  
> **Source Type:** Issue  
> **Origin:** Audit of `2603251825-phase4-flowutils-breakif-verb-patch.md` vs umbrella Step 4 / Step 6 wording (“loop/sequence test”)

---

## Source

Umbrella plan Step 4 verification called for a loop **or sequence** test using `breakif: /verb;`. The breakif patch added `Loop_BreakIfVerb_UsesReturnedBoolean` and `Foreach_ContinueIfVerb_UsesReturnedBoolean` in `FlowTests.cs` but **no** dedicated `SequenceDriver` regression where `breakif:` is a verb. `SequenceDriver` uses `FlowUtils.ShouldBreak` (same code path as loop); coverage is logically shared, but the umbrella’s explicit “sequence” mention is unmatched by a named test.

---

## Context

`FlowUtils` is shared; risk is low. A small `FlowTests` case that runs `/sequence` with `breakif:` set to a verb returning false (or a staged true after N checks) would align docs, audit nav, and test names with the umbrella narrative. Deferred until someone wants doc/test parity rather than behavior work.

---

## Related Projex

- `2603252220-phase4-sequence-breakif-verb-test-patch.md` (consumes this memo)
- `20260227-phase4-control-flow-gaps-fix-plan.md`
- `2603251603-phase4-flowutils-breakif-verb-plan.md`
- `2603251825-phase4-flowutils-breakif-verb-patch.md`
