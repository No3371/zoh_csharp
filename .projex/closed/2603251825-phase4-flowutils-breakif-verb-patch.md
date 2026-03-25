# Patch: Phase 4 `breakif` / `continueif` — execute verb conditions

> **Date:** 2026-03-25  
> **Author:** Agent  
> **Directive:** `patch-projex` — execute `2603251603-phase4-flowutils-breakif-verb-plan.md`  
> **Source Plan:** `2603251603-phase4-flowutils-breakif-verb-plan.md`  
> **Result:** Success

---

## Summary

`FlowUtils` now resolves `breakif:` / `continueif:` values the same way as other flow conditions: after `ValueResolver.Resolve`, a `ZohVerb` is executed via `context.ExecuteVerb(condVerb.VerbValue, context)` and `ValueOrNothing` is used for `IsTruthy()`. Without this, `ZohVerb` fell through to the default branch of `IsTruthy()` and was always treated as true. Added `Loop_BreakIfVerb_UsesReturnedBoolean` and `Foreach_ContinueIfVerb_UsesReturnedBoolean` in `FlowTests.cs`. Full suite: **713** tests pass.

---

## Changes

### `FlowUtils.cs`

**File:** `csharp/src/Zoh.Runtime/Verbs/Flow/FlowUtils.cs`  
**Change Type:** Modified  

**What changed:**

- Introduced `ResolveConditionValue` shared by `ShouldBreak` and `ShouldContinue`.
- After resolve, if value is `ZohVerb`, replace with `ExecuteVerb(...).ValueOrNothing` before `IsTruthy()`.
- Added `using Zoh.Runtime.Verbs` for `DriverResult.ValueOrNothing`; removed unused `System` / `System.Linq` usings.

**Why:** Spec expects the **returned** value of a verb condition, not the verb value’s default truthiness.

---

### `FlowTests.cs`

**File:** `csharp/tests/Zoh.Tests/Verbs/Flow/FlowTests.cs`  
**Change Type:** Modified  

**What changed:**

- `Loop_BreakIfVerb_UsesReturnedBoolean` — `breakif:` with a verb that always returns false; loop must run all iterations (would break immediately if the verb were not executed and `ZohVerb` were truthy).
- `Foreach_ContinueIfVerb_UsesReturnedBoolean` — `continueif:` verb returns true for the first two invocations then false; only the third item contributes to `sum` (30).
- Helpers: `BreakIfAlwaysFalseDriver`, `ContinueIfTwoThenFalseDriver`.

**Why:** Regression coverage for verb-as-condition execution for both named parameters.

---

## Verification

**Method:** `dotnet test --filter "FullyQualifiedName~FlowTests"` then `dotnet test` from `csharp/`.

**Result:**

```text
dotnet test --filter "FullyQualifiedName~FlowTests"  → 18 passed
dotnet test                                          → 713 passed
```

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|--------------|-------------|
| `2603251603-phase4-flowutils-breakif-verb-plan.md` | Source plan | Marked complete; linked this patch |
| `20260227-phase4-control-flow-gaps-fix-plan.md` | Related | Gap item addressed via this patch (FlowUtils verb conditions) |
| `2603251430-20260227-phase4-control-flow-gaps-fix-plan-review.md` | Related | Prior review noted missing verb execution — now implemented |

---

## Notes

`DriverResult.Suspend` from the condition verb is not propagated through the `bool` `ShouldBreak` / `ShouldContinue` API; `ValueOrNothing` is used, consistent with `WhileDriver` subject handling and `SwitchDriver` test-value verbs.
