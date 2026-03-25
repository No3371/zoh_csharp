# Patch walkthrough: Phase 4 `/do` — execute verb returned by first run

> **Execution date:** 2026-03-25  
> **Source plan:** `2603251604-phase4-do-returned-verb-plan.md` (closed with this patch)  
> **Directive:** `patch-projex`  
> **Base branch:** `main`  
> **Result:** Success

---

## Summary

`DoDriver` now performs a **single follow-up** `ExecuteVerb` when the first hop’s result value is a `ZohVerb`, matching `spec/2_verbs.md` (`/do` with a verb that returns another verb). `Suspend` and fatal results from the first hop propagate without a second hop. Added `Do_ExecutesVerbReturnedByFirstExecution` in `ControlFlowVerbsTests.cs`. Full suite: **711** tests pass.

---

## Objectives completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Two-hop execution when first result is `ZohVerb` | Complete | `DoDriver.cs` — `ValueOrNothing` + second `ExecuteVerb` |
| Fatals / suspend on first hop | Complete | Same pattern as `IfDriver` subject handling |
| Single extra hop only | Complete | No loop; second result not re-unwrapped |
| New test | Complete | `Do_ExecutesVerbReturnedByFirstExecution` |
| `dotnet test` | Complete | Filtered + full run |

---

## Execution detail

### Step 1: `DoDriver.cs`

**Planned:** After first `ExecuteVerb`, if `ValueOrNothing` is `ZohVerb`, call `ExecuteVerb` once more; return first result on fatal.

**Actual:** Added `using Zoh.Runtime.Verbs`; first result: return early on `DriverResult.Suspend` or `IsFatal`; if `first.ValueOrNothing is ZohVerb returnedVerb`, return `context.ExecuteVerb(returnedVerb.VerbValue, context)`; else return `first`.

**Deviation:** None.

**Files changed:**

| File | Change |
|------|--------|
| `src/Zoh.Runtime/Verbs/Flow/DoDriver.cs` | Two-hop `/do` execution |

### Step 2: `ControlFlowVerbsTests.cs`

**Planned:** Mock first execution returning a verb; second returning a terminal value.

**Actual:** `VerbExecutor` counts invocations: hop 1 → `Ok(ZohVerb.FromAst(innerCall))`, hop 2 → `Ok(ZohInt(99))`; assert `hop == 2` and final value `99`.

**Files changed:**

| File | Change |
|------|--------|
| `tests/Zoh.Tests/Verbs/Core/ControlFlowVerbsTests.cs` | New test + `using Zoh.Runtime.Verbs` |

---

## Verification

```text
dotnet test --filter "FullyQualifiedName~ControlFlowVerbsTests"  → 2 passed
dotnet test                                                      → 711 passed
```

---

## Commits

1. `projex(patch): phase4 /do execute verb returned by first run` — driver + test  
2. `projex(patch): add patch doc - phase4 do returned verb` — this file + plan update
