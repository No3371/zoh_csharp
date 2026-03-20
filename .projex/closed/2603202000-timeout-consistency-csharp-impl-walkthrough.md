# Walkthrough: Timeout Consistency — C# Runtime Implementation

> **Plan:** `2603202000-timeout-consistency-csharp-impl-plan.md`
> **Branch:** `projex/2603202000-timeout-consistency-csharp-impl`
> **Commits:** bc66190 (src), 5362c3a (tests)
> **Result:** 704/704 tests pass

---

## Criteria Checklist

- [x] WaitDriver parses `timeout` named param and passes `TimeoutMs` to `SignalRequest`
- [x] WaitDriver emits `Info: timeout` diagnostic on `WaitTimedOut`
- [x] All 4 std verb drivers pass `timeoutMs` to `new HostRequest(timeoutMs)` for scheduler enforcement
- [x] ConverseDriver and ChooseDriver `<= 0` immediate timeout emits `Info: timeout` diagnostic
- [x] PullVerbDriver and PushVerbDriver parse `timeout` and handle `<= 0` with diagnostic
- [x] All 7 drivers treat `?` (ZohNothing) as "no timeout" — `timeoutMs` stays `null`
- [x] All existing tests pass
- [x] New tests cover: timeout parsing, `<= 0` immediate timeout, `?` passthrough, `WaitTimedOut` diagnostic

---

## Step 1: WaitDriver — `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs`

**Added:** `using System;`

**Added** timeout parsing block after signal name extraction (lines 31–52):
- Iterates `call.NamedParams` with `OrdinalIgnoreCase` for `"timeout"` key
- `ZohFloat`/`ZohInt` <= 0: returns `DriverResult.Complete` with `Info:"timeout"` diagnostic immediately
- Positive value: `timeoutMs = value * 1000.0`
- `ZohNothing`: falls through, `timeoutMs` stays `null`

**Changed** `new SignalRequest(signalName)` → `new SignalRequest(signalName, timeoutMs)` (line 54)

**Changed** `WaitTimedOut => DriverResult.Complete.Ok()` → emits `Info:"timeout"` diagnostic (lines 58–59)

The class has no `GetNamedParam` helper; used direct foreach over `call.NamedParams` per plan fallback.

---

## Step 2a: ConverseDriver — `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs`

**Changed** (2 locations): `<= 0` float/int branches from `return DriverResult.Complete.Ok()` → `return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(new Diagnostic(Info, "timeout", "Converse timed out", ...)))`

**Changed**: `new HostRequest()` → `new HostRequest(timeoutMs)` (line 113)

The `WaitTimedOut` continuation diagnostic was already present.

---

## Step 2b: ChooseDriver — `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs`

**Changed** (2 locations): `<= 0` branches from `return DriverResult.Complete.Ok(ZohValue.Nothing)` → emit `Info:"timeout"` diagnostic

**Changed**: `new HostRequest()` → `new HostRequest(timeoutMs)`

The `WaitTimedOut` continuation diagnostic was already present.

---

## Step 2c: ChooseFromDriver — `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs`

**Changed**: `new HostRequest()` → `new HostRequest(timeoutMs)` (line 109)

`<= 0` immediate timeout already used `CreateTimeoutResult` (correct). No diagnostic fix needed.

---

## Step 2d: PromptDriver — `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs`

**Changed**: `new HostRequest()` → `new HostRequest(timeoutMs)` (line 61)

`<= 0` immediate timeout already used `CreateTimeoutResult` (correct). No diagnostic fix needed.

---

## Step 3: ChannelVerbs — `src/Zoh.Runtime/Verbs/ChannelVerbs.cs`

**Added** `using System;` and `using System.Collections.Immutable;`

**PushVerbDriver**: added timeout parsing block after `var value = ValueResolver.Resolve(...)`, before existence check. Same pattern: `<= 0` returns `Info:"timeout"` immediately; positive stores `timeoutMs`; `ZohNothing` falls through.

**PullVerbDriver**: added timeout parsing block after channel generation check (line 70), before `TryPull`. Same pattern.

Note: `timeoutMs` is parsed and available for future channel blocking infrastructure but has no blocking effect yet (non-blocking behavior preserved per Out of Scope).

---

## Step 4: Tests

**New file** `tests/Zoh.Tests/Verbs/Signals/WaitDriverTests.cs` — 6 tests:
- `Wait_TimeoutZero_ReturnsInfoDiagnosticImmediately`
- `Wait_TimeoutNegative_ReturnsInfoDiagnosticImmediately`
- `Wait_TimeoutQuestion_SuspendsNormally`
- `Wait_NoTimeout_SuspendsOnSignal`
- `Wait_ResumeTimedOut_ReturnsInfoDiagnostic`
- `Wait_ResumeWithValue_ReturnsValue`

**New file** `tests/Zoh.Tests/Verbs/Channel/ChannelVerbTimeoutTests.cs` — 6 tests:
- `Pull_TimeoutZero_ReturnsInfoDiagnosticImmediately`
- `Pull_TimeoutNegative_ReturnsInfoDiagnosticImmediately`
- `Pull_TimeoutQuestion_ProceedsNormally`
- `Push_TimeoutZero_ReturnsInfoDiagnosticImmediately`
- `Push_TimeoutNegative_ReturnsInfoDiagnosticImmediately`
- `Push_TimeoutQuestion_ProceedsNormally`

**Added to** `ConverseDriverTests.cs` — 4 tests:
- `Converse_TimeoutZero_ReturnsInfoDiagnosticImmediately`
- `Converse_TimeoutNegative_ReturnsInfoDiagnosticImmediately`
- `Converse_TimeoutQuestion_NoImmediateTimeout`
- `Converse_TimeoutPositive_PassesTimeoutMsToHostRequest`

**Added to** `ChooseDriverTests.cs` — 3 tests:
- `Choose_TimeoutZero_ReturnsInfoDiagnosticImmediately`
- `Choose_TimeoutNegative_ReturnsInfoDiagnosticImmediately`
- `Choose_TimeoutQuestion_NoImmediateTimeout`

**Added to** `ChooseFromDriverTests.cs` — 3 tests:
- `ChooseFrom_TimeoutZero_ReturnsInfoDiagnosticImmediately`
- `ChooseFrom_TimeoutNegative_ReturnsInfoDiagnosticImmediately`
- `ChooseFrom_TimeoutQuestion_NoImmediateTimeout`

---

## Discrepancies from Plan

- **`GetNamedParam` on WaitDriver**: As anticipated in the plan's risk note, WaitDriver has no helper. Used direct `foreach` over `call.NamedParams` — exact fallback specified in the plan.
- **PushVerbDriver `wait` param**: Plan mentioned parsing a `wait` boolean alongside timeout. This was not implemented — it is only relevant when channel blocking infrastructure exists (Out of Scope). The `<= 0` timeout behavior for push is self-contained.
- **PromptDriverTests.cs not changed**: All required timeout scenarios were already covered by existing tests (`Prompt_TimeoutZeroOrNegative_CompletesFast`, `Prompt_AttributesParsed`, `Prompt_ResumeTimedOut_ReturnsInfoNothing`). No new tests needed.

---

## Verification

```
已通過! - 失敗: 0, 通過: 704, 略過: 0, 總計: 704
```
