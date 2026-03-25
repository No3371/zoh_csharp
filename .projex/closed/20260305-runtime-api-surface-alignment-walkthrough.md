# Walkthrough: C# Runtime API Surface Alignment

> **Execution Date:** 2026-03-05
> **Completed By:** Agent
> **Source Plan:** 20260305-runtime-api-surface-alignment-plan.md
> **Result:** Success

---

## Summary

All 5 plan steps executed cleanly. The C# runtime now exposes `ContextHandle`, `ExecutionResult`, `VariableAccessor`, and `WaitConditionState` as typed public types. `ZohRuntime` gained `StartContext`/`Tick`/`Resume`/`GetResult` as the new host API. Presentation handlers were updated to receive `ContextHandle`. Old API methods marked `[Obsolete]`. 616/616 tests pass.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Add `ContextHandle`, `ExecutionResult`, `VariableAccessor`, `WaitConditionState` types | Complete | All 4 files created |
| `ZohRuntime.StartContext` / `Tick` / `Resume` / `GetResult` | Complete | With `ResolveWait` scheduler |
| `RuntimeConfig.MaxStatementsPerTick` | Complete | Defaults to 0 (unlimited) |
| `Context.WaitCondition` typed, `BlockOnRequest` uses runtime time | Complete | `DateTimeOffset.UtcNow` eliminated |
| Presentation handlers receive `ContextHandle` | Complete | All 4 interfaces + drivers |
| Old API `[Obsolete]` | Complete | CS0618 warnings on old usage in tests |
| All existing tests pass | Complete | 615 pre-existing + 9 new = 616 |
| New API surface tests | Complete | `ApiSurfaceTests.cs` with 9 tests |

---

## Execution Detail

### Step 1: Add New Types

**Planned:** Create 4 files in `src/Zoh.Runtime/Execution/`.

**Actual:** Created exactly as specified. Build showed one expected error (`GetAllKeys` not yet on `VariableStore`) which Step 2 resolved. Pre-existing NuGet restore issue in environment — `--no-restore` bypasses it, no impact on build/test.

**Deviation:** None.

**Files Changed:**
| File | Change Type | Planned? |
|------|-------------|----------|
| `src/Zoh.Runtime/Execution/ContextHandle.cs` | Created | Yes |
| `src/Zoh.Runtime/Execution/ExecutionResult.cs` | Created | Yes |
| `src/Zoh.Runtime/Execution/VariableAccessor.cs` | Created | Yes |
| `src/Zoh.Runtime/Execution/WaitConditionState.cs` | Created | Yes |

---

### Step 2: Update Supporting Types

**Planned:** `MaxStatementsPerTick` on `RuntimeConfig`, `GetAllKeys()` on `VariableStore`, `Handle`/`ElapsedMsProvider` on `Context`, typed `WaitCondition`, rewrite `BlockOnRequest`.

**Actual:** All changes applied as specified. Also added `ElapsedMsProvider` to `Context.Clone()` — not in plan but required for correctness when forking contexts.

**Deviation:** `Context.Clone()` updated to copy `ElapsedMsProvider`. Minor unplanned addition; no plan impact.

**Files Changed:**
| File | Change Type | Planned? |
|------|-------------|----------|
| `src/Zoh.Runtime/Execution/RuntimeConfig.cs` | Modified | Yes |
| `src/Zoh.Runtime/Variables/VariableStore.cs` | Modified | Yes |
| `src/Zoh.Runtime/Execution/Context.cs` | Modified | Yes |

---

### Step 3: Update ZohRuntime — New API Surface

**Planned:** Add `_elapsedMs`, `_handles`, `CreateContextInternal`, `StartContext`, `Tick`, `ResolveWait`, `Resume(handle, value)`, `GetResult`, `Handles`. Mark old methods `[Obsolete]`.

**Actual:** Implemented exactly as specified. All new methods added. `[Obsolete]` on `CreateContext`, `Run`, `RunToCompletion` (both overloads).

**Deviation:** None.

**Files Changed:**
| File | Change Type | Planned? |
|------|-------------|----------|
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Modified | Yes |

---

### Step 4: Update Presentation Handler Interfaces and Drivers

**Planned:** Change first parameter of `OnConverse`/`OnChoose`/`OnChooseFrom`/`OnPrompt` from `IExecutionContext` to `ContextHandle`. Pass `ctx.Handle!` in drivers.

**Actual:** Exact as planned. `using Zoh.Runtime.Execution;` already present in all interface files — no import changes needed.

**Deviation:** None.

**Files Changed:**
| File | Change Type | Planned? |
|------|-------------|----------|
| `src/Zoh.Runtime/Verbs/Standard/Presentation/IConverseHandler.cs` | Modified | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/IChooseHandler.cs` | Modified | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/IChooseFromHandler.cs` | Modified | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/IPromptHandler.cs` | Modified | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs` | Modified | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | Modified | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | Modified | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs` | Modified | Yes |

---

### Step 5: Update and Add Tests

**Planned:** Update 4 mock handler stubs from `IExecutionContext` to `ContextHandle`. Create `ApiSurfaceTests.cs`.

**Actual:** Mock stubs updated (parameter name only — none used the context/handle in the body). `ApiSurfaceTests.cs` created with 9 tests.

**Deviation:** `Tick_ResolvesSleepingContext` test used `/sleep 100;` in the plan (100 seconds = 100000ms; never resolves at tick totals of 50/110ms). Fixed to `/sleep 0.1;` (0.1s = 100ms). `SleepDriver` takes seconds and multiplies by 1000 internally.

**Files Changed:**
| File | Change Type | Planned? |
|------|-------------|----------|
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ConverseDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/PromptDriverTests.cs` | Modified | Yes |
| `tests/Zoh.Tests/Execution/ApiSurfaceTests.cs` | Created | Yes |

---

## Complete Change Log

> Derived from `git diff --stat main..HEAD`

### Files Created
| File | Purpose | In Plan? |
|------|---------|----------|
| `src/Zoh.Runtime/Execution/ContextHandle.cs` | Opaque handle exposing `Id`/`State` to host | Yes |
| `src/Zoh.Runtime/Execution/ExecutionResult.cs` | Result for terminated context (`Value`, `Diagnostics`, `Variables`) | Yes |
| `src/Zoh.Runtime/Execution/VariableAccessor.cs` | Lazy variable read access (`Get`, `Has`, `Keys`) | Yes |
| `src/Zoh.Runtime/Execution/WaitConditionState.cs` | Typed wait condition hierarchy (`Sleep`, `Host`, `Signal`, `Join`, `Channel`) | Yes |
| `tests/Zoh.Tests/Execution/ApiSurfaceTests.cs` | 9 tests covering new public API surface | Yes |

### Files Modified
| File | Changes | In Plan? |
|------|---------|----------|
| `src/Zoh.Runtime/Execution/Context.cs` | `Handle`/`ElapsedMsProvider` added; `WaitCondition` retyped; `BlockOnRequest` rewritten; `Clone()` updated | Yes |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | `CreateContextInternal`, `StartContext`, `Tick`, `ResolveWait`, `Resume(handle,value)`, `GetResult`, `Handles`; `[Obsolete]` on old methods | Yes |
| `src/Zoh.Runtime/Execution/RuntimeConfig.cs` | `MaxStatementsPerTick` property added | Yes |
| `src/Zoh.Runtime/Variables/VariableStore.cs` | `GetAllKeys()` method added | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/IConverseHandler.cs` | `IExecutionContext` → `ContextHandle` parameter | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/IChooseHandler.cs` | `IExecutionContext` → `ContextHandle` parameter | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/IChooseFromHandler.cs` | `IExecutionContext` → `ContextHandle` parameter | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/IPromptHandler.cs` | `IExecutionContext` → `ContextHandle` parameter | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs` | `ctx` → `ctx.Handle!` in handler call | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | `ctx` → `ctx.Handle!` in handler call | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | `ctx` → `ctx.Handle!` in handler call | Yes |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs` | `ctx` → `ctx.Handle!` in handler call | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ConverseDriverTests.cs` | Mock stub: `IExecutionContext` → `ContextHandle` | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs` | Mock stub: `IExecutionContext` → `ContextHandle` | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs` | Mock stub: `IExecutionContext` → `ContextHandle` | Yes |
| `tests/Zoh.Tests/Verbs/Standard/Presentation/PromptDriverTests.cs` | Mock stub: `IExecutionContext` → `ContextHandle` | Yes |

---

## Success Criteria Verification

### Acceptance Criteria Summary

| Criterion | Method | Result |
|-----------|--------|--------|
| `ContextHandle` type with `Id`, `State`, internal ctor | Read file | Pass |
| `ExecutionResult` type with `Value`, `Diagnostics`, `Variables` | Read file | Pass |
| `VariableAccessor` with `Get`, `Has`, `Keys` | Read file | Pass |
| `StartContext` returns `ContextHandle` | `StartContext_ReturnsHandle` test | Pass |
| `Tick` drives contexts | `Tick_DrivesContextToCompletion` test | Pass |
| `Tick` resolves sleep | `Tick_ResolvesSleepingContext` test | Pass |
| `Resume` unblocks host | `Resume_UnblocksWaitingHostContext` test | Pass |
| `GetResult` works | `GetResult_ReturnsValueAndVariables` test | Pass |
| `MaxStatementsPerTick` exists | Read `RuntimeConfig.cs` | Pass |
| Handler interfaces use `ContextHandle` | Read interface files | Pass |
| `Context.WaitCondition` is typed | Read `Context.cs` | Pass |
| No `DateTimeOffset`/`UtcNow` in `Context.cs` | Grep — zero hits | Pass |
| Old API `[Obsolete]` | CS0618 build warnings | Pass |
| All existing tests pass | `dotnet test` — 616 pass, 0 fail | Pass |

**Overall: 14/14 criteria passed**

---

## Deviations from Plan

### Sleep duration unit in `Tick_ResolvesSleepingContext`
- **Planned:** `/sleep 100;` with tick totals of 0/50/110ms
- **Actual:** `/sleep 0.1;` (0.1s = 100ms)
- **Reason:** `SleepDriver` interprets the parameter as seconds and multiplies by 1000. `/sleep 100;` = 100s = 100000ms — never resolves at 110ms total.
- **Impact:** None on runtime behavior; test intent preserved.
- **Recommendation:** Update plan to use `/sleep 0.1;`.

### `Context.Clone()` updated
- **Planned:** Not mentioned
- **Actual:** Added `ElapsedMsProvider = ElapsedMsProvider` to the clone initializer
- **Reason:** Forked contexts need the elapsed time provider to resolve sleep/signal timeouts correctly.
- **Impact:** Correctness fix; no observable behavior change in current tests.

---

## Key Insights

### Gotchas / Pitfalls

1. **`SleepDriver` takes seconds, not milliseconds**
   - The parameter to `/sleep` is in seconds; the driver multiplies by 1000 before creating `SleepRequest`. This is not obvious from the verb name. Tests using tick-based time need to account for this.

2. **`dotnet build` requires `--no-restore` in this environment**
   - A pre-existing NuGet restore error (`Value cannot be null. Parameter 'path1'`) blocks restore but not compilation. Always use `--no-restore` for build/test in this repo.

3. **Stale test binary**
   - After editing a test source, `dotnet test --no-build` runs the old binary. Always rebuild before testing.

### Technical Insights

- `Context.Clone()` is used by fork/call semantics — any new `Context` property that affects execution needs to be carried through `Clone()`.
- `[Obsolete]` on `CreateContext`/`Run`/`RunToCompletion` produces CS0618 warnings in 17 test files (95 usages) but is not an error — old tests continue to pass.

---

## Recommendations

### Immediate Follow-ups
- [ ] Test migration plan: move 95 old-API usages to `StartContext`/`Tick`/`Resume`
- [ ] Consider making `Context` internal once tests are migrated (requires `InternalsVisibleTo("Zoh.Tests")` for any tests that need direct context access)

### Future Considerations
- `MaxStatementsPerTick` enforcement in `Context.Run()` is deferred — currently config exists but is not read
- `WaitingChannelPush` state (separate channel semantics concern) was explicitly out of scope

---

## Related Projex

| Document | Status |
|----------|--------|
| `20260305-runtime-api-surface-alignment-plan.md` | Complete → moved to `closed/` |
| `20260304-runtime-api-surface-spec-plan-walkthrough.md` | Predecessor (spec-side) — already closed |
