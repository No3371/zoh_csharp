# Patch: Add RunToCompletion(string) Convenience Shorthand

> **Date:** 2026-02-27
> **Author:** Agent
> **Directive:** Execute all objectives of `20260226-runtime-run-to-completion-shorthand-plan.md`
> **Source Plan:** `20260226-runtime-run-to-completion-shorthand-plan.md`
> **Result:** Success

---

## Summary

Added `ZohRuntime.RunToCompletion(string source)` convenience overload that chains `LoadStory` → `CreateContext` → `Run` and returns the finished `Context`. Added two unit tests covering the primary use case (inspect variables post-run) and the empty-story edge case.

---

## Changes

### ZohRuntime — New Overload

**File:** `src/Zoh.Runtime/Execution/ZohRuntime.cs`
**Change Type:** Modified
**What Changed:**
- Added `RunToCompletion(string source)` method after the existing `RunToCompletion(Context ctx)` overload (line 194–200)

**Why:**
Eliminates the 3-line load/create/run boilerplate in tests and simple embedder use cases. Returns `Context` (not `ZohValue`) so callers can inspect variables, state, and diagnostics.

---

### RuntimeTests — New Tests

**File:** `tests/Zoh.Tests/Execution/RuntimeTests.cs`
**Change Type:** Modified
**What Changed:**
- `RunToCompletion_String_ExecutesAndReturnsContext` — runs a script, asserts `ctx.State == Terminated` and `ctx.Variables.Get("x") == ZohInt(99)`
- `RunToCompletion_String_EmptyStory_TerminatesCleanly` — runs an empty story, asserts `ctx.State == Terminated`

**Why:**
Covers the primary use case and the edge case as specified in the plan.

---

## Verification

**Method:** `dotnet test --filter "FullyQualifiedName~RuntimeTests" --no-build`

**Result:**
```
已通過! - 失敗: 0，通過: 7，略過: 0，總計: 7，持續時間: 36 ms
```

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| `20260226-runtime-run-to-completion-shorthand-plan.md` | Source plan | All objectives marked complete, status → Complete, moved to `projex/closed/` |

---

## Notes

- C# overload resolution distinguishes `RunToCompletion(string)` from `RunToCompletion(Context)` cleanly — no ambiguity.
- Committed to `c#` repo: `d9ae112`
