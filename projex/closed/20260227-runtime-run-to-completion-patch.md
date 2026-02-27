# Patch: Add ZohRuntime.RunToCompletion(Context)

> **Date:** 2026-02-27
> **Author:** Agent
> **Directive:** Execute `20260226-runtime-run-to-completion-plan.md` (all objectives)
> **Source Plan:** `20260226-runtime-run-to-completion-plan.md`
> **Result:** Success

---

## Summary

Implemented `ZohRuntime.RunToCompletion(Context ctx)` as specified in `impl/09_runtime.md`. The method calls `Run(ctx)` and returns `ctx.LastResult`, providing a clean single-call synchronous execution path. Two unit tests were added; the initially drafted test was corrected because `/set` returns nothing — `/get` is used instead as the final verb to produce a return value.

---

## Changes

### ZohRuntime — New Method

**File:** `src/Zoh.Runtime/Execution/ZohRuntime.cs`
**Change Type:** Modified
**What Changed:**
- Added `RunToCompletion(Context ctx)` after the existing `Run(Context ctx)` method (line 187)

**Why:**
Spec-defined `runToCompletion(context: Context): Value` was missing from the C# implementation. Callers had no single-call way to execute a context and retrieve its result.

---

### RuntimeTests — New Tests

**File:** `tests/Zoh.Tests/Execution/RuntimeTests.cs`
**Change Type:** Modified
**What Changed:**
- Added `RunToCompletion_ReturnsLastResult` — sets a variable, then calls `/get *x;` as the last verb (since `/set` returns nothing); asserts result is `ZohInt(42)`
- Added `RunToCompletion_EmptyStory_ReturnsNothing` — empty story, asserts result is `ZohNothing.Instance`

**Why:**
Covers the happy path (verb returns a value) and the edge case (no verb produces a value → nothing returned).

---

## Verification

**Method:** `dotnet test --filter "FullyQualifiedName~RuntimeTests"`

**Result:**
```
已通過! - 失敗: 0，通過: 5，略過: 0，總計: 5，持續時間: 44 ms
```

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| `20260226-runtime-run-to-completion-plan.md` | Source plan | All objectives marked complete; status set to Complete; moved to closed/ |

---

## Notes

- The `c#/` directory is a separate git repo from the spec repo root — commits go to `S:/Repos/zoh/c#` (commit `f68e2b8`)
- `/set` returns nothing in Zoh; test was corrected to use `/get *x;` as the final verb
- `ctx.LastResult` is initialized to `ZohValue.Nothing` (non-nullable), so the `?? ZohNothing.Instance` guard is defensive only
