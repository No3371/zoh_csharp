# Timeout Consistency: C# Runtime Implementation

> **Status:** Ready
> **Created:** 2026-03-20
> **Author:** agent
> **Source:** `2603201630-timeout-consistency-fixes-walkthrough.md`
> **Related Projex:** `2603201630-timeout-consistency-fixes-plan.md`
> **Worktree:** No

---

## Summary

Align the C# runtime's timeout handling with spec changes from the timeout consistency fixes. Seven verbs were updated in spec to standardize `timeout` parameter behavior (`?` acceptance, `Default to ?`, `<= 0` immediate-poll rule). The C# runtime has gaps across all three verb groups: WaitDriver ignores timeout entirely, std verb drivers don't pass timeout to HostRequest for scheduler enforcement, and channel verb drivers lack timeout parsing.

**Scope:** C# runtime timeout handling in 7 verb drivers + tests
**Estimated Changes:** ~7 source files, ~5 test files

---

## Objective

### Problem / Gap / Need

The spec guarantees consistent timeout behavior across all 7 timeout-capable verbs:
1. `timeout` accepts `double`, `*double`, or `?`
2. Default is `?` (no timeout)
3. `<= 0` triggers immediate timeout with `Info: timeout` diagnostic

The C# runtime diverges:

| Driver | Parses timeout | Passes to WaitRequest | `<= 0` emits diagnostic | `?` handled |
|--------|:-:|:-:|:-:|:-:|
| **WaitDriver** | No | No (`SignalRequest` gets no timeout) | N/A | N/A |
| **ConverseDriver** | Yes | No (`new HostRequest()`) | No — returns `Ok()` | Yes (fall-through) |
| **ChooseDriver** | Yes | No (`new HostRequest()`) | No — returns `Ok(Nothing)` | Yes (fall-through) |
| **ChooseFromDriver** | Yes | No (`new HostRequest()`) | Yes | Yes (fall-through) |
| **PromptDriver** | Yes | No (`new HostRequest()`) | Yes | Yes (fall-through) |
| **PullVerbDriver** | No | N/A (non-blocking) | N/A | N/A |
| **PushVerbDriver** | No | N/A (synchronous) | N/A | N/A |

### Success Criteria

- [ ] WaitDriver parses `timeout` named param and passes `TimeoutMs` to `SignalRequest`
- [ ] WaitDriver emits `Info: timeout` diagnostic on `WaitTimedOut`
- [ ] All 4 std verb drivers pass `timeoutMs` to `new HostRequest(timeoutMs)` for scheduler enforcement
- [ ] ConverseDriver and ChooseDriver `<= 0` immediate timeout emits `Info: timeout` diagnostic
- [ ] PullVerbDriver and PushVerbDriver parse `timeout` and handle `<= 0` with diagnostic
- [ ] All 7 drivers treat `?` (ZohNothing) as "no timeout" — `timeoutMs` stays `null`
- [ ] All existing tests pass
- [ ] New tests cover: timeout parsing, `<= 0` immediate timeout, `?` passthrough, `WaitTimedOut` diagnostic

### Out of Scope

- **Channel blocking infrastructure** — PullVerbDriver is explicitly non-blocking (`// For now: non-blocking pull. Blocking requires async redesign.`). Implementing suspend/continuation for channels requires `ChannelPullRequest`/`ChannelPushRequest` types, `Context.BlockOnRequest` handling, and a fulfillment mechanism (analogous to `SignalManager.Subscribe/Broadcast`). That is a separate plan.
- **Push `wait` parameter** — `wait: true` blocking semantics depend on channel blocking infrastructure.
- **Validator changes** — Current validators already handle timeout where present.
- **Execution-level timeout** (`RuntimeConfig.ExecutionTimeoutMs`) — separate concern.

---

## Context

### Current State

**Timeout infrastructure chain (already working):**
Driver parses timeout → converts seconds to ms → passes to `WaitRequest` subtype → `Context.BlockOnRequest` creates `WaitConditionState` → scheduler's `ResolveWait` calls `IsTimedOut` each tick → returns `WaitTimedOut` → continuation handles outcome.

All infrastructure types support optional timeout:
- `SignalRequest(string MessageName, double? TimeoutMs = null)` — `WaitRequest.cs:7`
- `HostRequest(double? TimeoutMs = null)` — `WaitRequest.cs:9`
- `SignalWaitCondition` / `HostWaitCondition` / `ChannelWaitCondition` — all have `IsTimedOut` — `WaitConditionState.cs`
- `ZohRuntime.ResolveWait` checks all three condition types — `ZohRuntime.cs:200-230`

The gap is purely at the driver layer.

**`?` handling:** When `timeout` resolves to `ZohNothing`, it fails the `if (tVal is ZohFloat)` and `else if (tVal is ZohInt)` checks, leaving `timeoutMs` as `null`. This correctly means "no timeout" with no code change needed.

### Key Files

> Quick reference — detailed changes in Implementation steps below.

| File | Role | Change Summary |
|------|------|----------------|
| `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs` | Core.Wait driver | Add timeout parsing + pass to SignalRequest + diagnostic |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs` | Std.Converse driver | Pass timeoutMs to HostRequest; fix `<= 0` diagnostic |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | Std.Choose driver | Pass timeoutMs to HostRequest; fix `<= 0` diagnostic |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | Std.ChooseFrom driver | Pass timeoutMs to HostRequest |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs` | Std.Prompt driver | Pass timeoutMs to HostRequest |
| `src/Zoh.Runtime/Verbs/ChannelVerbs.cs` | Channel.Push + Channel.Pull | Add timeout parsing + `<= 0` handling |

### Dependencies

- **Requires:** Spec timeout consistency fixes (complete — `2603201630-timeout-consistency-fixes-walkthrough.md`)
- **Blocks:** Future channel blocking plan can build on timeout parsing added here

### Constraints

- Must not break existing non-blocking channel behavior
- Preserve seconds → milliseconds conversion convention used by all existing drivers
- `ZohNothing` timeout must remain "no timeout" (not error)

### Assumptions

- `GetNamedParam` is a method available on the driver classes (used by all 4 std drivers). WaitDriver and channel drivers may need a different access pattern — verify class hierarchy during execution. Fallback: access `call.NamedParams` directly.
- `ValueResolver.Resolve` returns `ZohNothing` for `?` literals.
- The `WaitTimedOut` outcome type is available in all continuation handlers (already used by std drivers).

### Impact Analysis

- **Direct:** 7 driver source files + test files
- **Adjacent:** `WaitRequest.cs`, `WaitConditionState.cs`, `Context.cs` — already support timeout; no changes needed
- **Downstream:** Host integrations receiving `HostRequest` will now get `TimeoutMs` values. Hosts already handle `WaitTimedOut` in their outcome matching since the continuation union includes it.

---

## Implementation

### Overview

Three independent groups (can be executed in any order):
1. **WaitDriver** — parse timeout, pass to SignalRequest, add diagnostic
2. **Std verb drivers** — pass timeoutMs to HostRequest, fix `<= 0` diagnostic gaps
3. **Channel verbs** — parse timeout, handle `<= 0` immediate timeout
4. **Tests** — depends on steps 1-3

### Step 1: WaitDriver — Add Timeout Parsing and Diagnostic

**Objective:** Make Core.Wait honor the `timeout` named parameter and emit `Info: timeout` diagnostic.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs`

**Changes:**

```csharp
// Before (lines 31-44):
string signalName = s.Value;

return new DriverResult.Suspend(new Continuation(
    new SignalRequest(signalName),
    outcome => outcome switch
    {
        WaitCompleted c => DriverResult.Complete.Ok(c.Value),
        WaitTimedOut => DriverResult.Complete.Ok(),
        WaitCancelled x => new DriverResult.Complete(
            ZohNothing.Instance,
            ImmutableArray.Create(new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start))),
        _ => DriverResult.Complete.Ok()
    }
));

// After:
string signalName = s.Value;

// Parse timeout named param
double? timeoutMs = null;
var timeoutAst = GetNamedParam(call, "timeout"); // verify helper availability; fallback: call.NamedParams
if (timeoutAst != null)
{
    var tVal = ValueResolver.Resolve(timeoutAst, ctx);
    if (tVal is ZohFloat f)
    {
        if (f.Value <= 0)
            return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
        timeoutMs = f.Value * 1000.0;
    }
    else if (tVal is ZohInt i)
    {
        if (i.Value <= 0)
            return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
        timeoutMs = i.Value * 1000.0;
    }
    // ZohNothing (?) falls through — timeoutMs stays null = no timeout
}

return new DriverResult.Suspend(new Continuation(
    new SignalRequest(signalName, timeoutMs),
    outcome => outcome switch
    {
        WaitCompleted c => DriverResult.Complete.Ok(c.Value),
        WaitTimedOut => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start))),
        WaitCancelled x => new DriverResult.Complete(
            ZohNothing.Instance,
            ImmutableArray.Create(new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start))),
        _ => DriverResult.Complete.Ok()
    }
));
```

**Rationale:** `SignalRequest` already accepts `TimeoutMs` as optional second parameter. The scheduler already handles `SignalWaitCondition.IsTimedOut`. Only driver-layer parsing is missing.

**Verification:**
- New test: `/wait "sig", timeout: 0;` → returns nothing + `Info: timeout` diagnostic
- New test: `/wait "sig", timeout: 5;` → `SignalRequest.TimeoutMs` == 5000

**If this fails:** Revert `WaitDriver.cs`. Self-contained change.

---

### Step 2: Std Verb Drivers — Pass TimeoutMs to HostRequest + Fix Diagnostics

**Objective:** All 4 std drivers pass parsed `timeoutMs` to `HostRequest` for scheduler enforcement. Fix ConverseDriver and ChooseDriver `<= 0` paths to emit `Info: timeout` diagnostic.
**Confidence:** High
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs`
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs`
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs`
- `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs`

**Changes:**

**2a. All 4 drivers — HostRequest timeout passthrough:**

Each driver creates `new HostRequest()` in its `Suspend` continuation. Change to `new HostRequest(timeoutMs)`.

```csharp
// Before (in each driver's Suspend):
new HostRequest(),

// After:
new HostRequest(timeoutMs),
```

Locations:
- `ConverseDriver.cs` ~line 113
- `ChooseDriver.cs` ~line 111
- `ChooseFromDriver.cs` ~line 109
- `PromptDriver.cs` ~line 61

**2b. ConverseDriver — fix `<= 0` immediate timeout diagnostic:**

```csharp
// Before (~line 64):
if (f.Value <= 0) return DriverResult.Complete.Ok(); // Immediate timeout, per spec

// After:
if (f.Value <= 0) return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
    new Diagnostic(DiagnosticSeverity.Info, "timeout", "Converse timed out", call.Start)));
```

Apply same fix for the `ZohInt` branch (~line 69).

**2c. ChooseDriver — fix `<= 0` immediate timeout diagnostic:**

```csharp
// Before:
if (f.Value <= 0) return DriverResult.Complete.Ok(ZohValue.Nothing);

// After:
if (f.Value <= 0) return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
    new Diagnostic(DiagnosticSeverity.Info, "timeout", "Choose timed out", call.Start)));
```

Apply same fix for the `ZohInt` branch.

**ChooseFromDriver and PromptDriver** already emit diagnostics via `CreateTimeoutResult` — no diagnostic fix needed, only the HostRequest passthrough (2a).

**Rationale:** `HostRequest(double? TimeoutMs = null)` already supports the parameter. `HostWaitCondition.IsTimedOut` and the scheduler at `ZohRuntime.cs:203` already return `WaitTimedOut`. The missing link is the single constructor argument.

**Verification:**
- Existing tests pass (no behavior change for `timeout: null` case — `HostRequest(null)` == `HostRequest()`)
- New test: Converse with `timeout: 0` → `Info: timeout` diagnostic
- New test: Choose with `timeout: 0` → `Info: timeout` diagnostic

**If this fails:** Revert individual driver files. Changes are independent per driver.

---

### Step 3: Channel Verbs — Add Timeout Parsing

**Objective:** PullVerbDriver and PushVerbDriver parse `timeout` named param, handle `<= 0` immediate timeout with diagnostic. Positive timeout values are stored but blocking behavior remains unchanged until channel blocking infrastructure is built.
**Confidence:** Medium — straightforward parsing, but positive timeout has no runtime effect yet.
**Depends on:** None

**Files:**
- `src/Zoh.Runtime/Verbs/ChannelVerbs.cs`

**Changes:**

**3a. PullVerbDriver — add timeout parsing (insert after channel validation, before TryPull):**

```csharp
// Insert after line 70 (channel existence check), before line 72 (TryPull):
double? timeoutMs = null;
// Access named params — verify exact API on VerbCallAst during execution
var timeoutParam = call.NamedParams.FirstOrDefault(p =>
    p.Name.Equals("timeout", StringComparison.OrdinalIgnoreCase));
if (timeoutParam != null)
{
    var tVal = Zoh.Runtime.Execution.ValueResolver.Resolve(timeoutParam.Value, context);
    if (tVal is ZohFloat f)
    {
        if (f.Value <= 0)
            return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
        timeoutMs = f.Value * 1000.0;
    }
    else if (tVal is ZohInt i)
    {
        if (i.Value <= 0)
            return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
        timeoutMs = i.Value * 1000.0;
    }
    // ZohNothing (?) falls through — no timeout
}

// Existing TryPull logic follows unchanged...
```

**3b. PushVerbDriver — add timeout parsing (insert after value resolution, before existence check):**

Same pattern as PullVerbDriver. Additionally parse `wait` named param as boolean (default `true`). When `wait` is `false`, timeout is ignored per spec.

```csharp
// After resolving the push value (line 41), before existence check (line 43):
bool shouldWait = true;
var waitParam = call.NamedParams.FirstOrDefault(p =>
    p.Name.Equals("wait", StringComparison.OrdinalIgnoreCase));
if (waitParam != null)
{
    var wVal = Zoh.Runtime.Execution.ValueResolver.Resolve(waitParam.Value, context);
    shouldWait = wVal.IsTruthy();
}

double? timeoutMs = null;
if (shouldWait)
{
    // Same timeout parsing pattern as PullVerbDriver (3a)
    // ... (identical block)
}
```

**3c. Add required using directives** to ChannelVerbs.cs if not already present:
- `using System.Collections.Immutable;`
- `using System.Linq;`

**Rationale:** Parsing timeout now delivers the `<= 0` immediate-poll rule and `?` acceptance. When channel blocking is implemented later, the `timeoutMs` value is already available to pass to a future `ChannelPullRequest`.

**Verification:**
- `dotnet test` — existing channel tests pass
- New test: `/pull <ch>, timeout: 0;` → nothing + `Info: timeout` diagnostic
- New test: `/push <ch>, *val, timeout: 0;` → nothing + `Info: timeout` diagnostic
- New test: `/pull <ch>, timeout: ?;` → same behavior as no timeout param

**If this fails:** Revert `ChannelVerbs.cs`. Self-contained.

---

### Step 4: Tests

**Objective:** Add test coverage for all timeout changes.
**Confidence:** High
**Depends on:** Steps 1-3

**Files:**
- `tests/Zoh.Tests/Verbs/Signals/WaitDriverTests.cs` (create if absent)
- `tests/Zoh.Tests/Verbs/Standard/Presentation/ConverseDriverTests.cs`
- `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs`
- `tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs`
- `tests/Zoh.Tests/Verbs/Standard/Presentation/PromptDriverTests.cs`

**Test matrix per verb:**

| Test case | Verifies |
|-----------|----------|
| `timeout: 0` → `Info: timeout` diagnostic | `<= 0` immediate-poll rule |
| `timeout: -1` → `Info: timeout` diagnostic | Negative values treated same as zero |
| `timeout: ?` → no timeout, no diagnostic | `?` means "no timeout" |
| `timeout: 5` → WaitRequest receives `TimeoutMs = 5000` | Seconds-to-ms conversion + passthrough |

Follow existing test patterns in each test file. Match setup/teardown conventions already in use.

**Verification:** `dotnet test` — all new and existing tests pass.

**If this fails:** Fix test assertions or revert corresponding driver changes.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` succeeds with no errors or warnings
- [ ] `dotnet test` — all existing tests pass (regression)
- [ ] `dotnet test` — all new timeout tests pass

### Manual Verification

- [ ] Grep `new HostRequest()` in std drivers — zero results (all replaced with `new HostRequest(timeoutMs)`)
- [ ] Grep `new SignalRequest(signalName)` in WaitDriver — includes `timeoutMs` argument
- [ ] All 7 drivers have timeout parsing blocks

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| WaitDriver parses timeout | Read WaitDriver.cs | `timeout` named param parsed, value passed to `SignalRequest` |
| WaitDriver emits Info:timeout | Test with `timeout: 0` | Diagnostic: severity=Info, code="timeout" |
| Std drivers pass timeoutMs to HostRequest | Grep `new HostRequest(` | All 4 include `timeoutMs` |
| Converse/Choose `<= 0` emits diagnostic | Test with `timeout: 0` | Info:timeout diagnostic present |
| Channel verbs handle `<= 0` | Test with `timeout: 0` | Info:timeout diagnostic present |
| `?` means no timeout | Test with `timeout: ?` | timeoutMs is null, no error, no diagnostic |
| All existing tests pass | `dotnet test` | 0 failures |

---

## Rollback Plan

1. `git checkout HEAD -- src/Zoh.Runtime/Verbs/` — revert all driver changes
2. Remove any newly created test files
3. `dotnet test` to confirm clean state

Each step is independently revertible — no cross-file dependencies introduced.

---

## Notes

### Risks

- **GetNamedParam availability on WaitDriver/ChannelVerbs**: These drivers may not have the same helper method as std drivers. Mitigation: verify class hierarchy during execution; use `call.NamedParams` directly as fallback (channel verbs already access `call.UnnamedParams` directly).
- **Positive timeout on non-blocking channel verbs**: Parsing `timeout: 5` on Pull/Push stores the value but has no blocking effect. This is a known limitation documented in Out of Scope. The parsed value will be used when channel blocking is implemented.

### Open Questions

- None.

### Future Work (out of scope)

- **Channel blocking infrastructure**: PullVerbDriver needs suspend/continuation for blocking waits. Requires `ChannelPullRequest`/`ChannelPushRequest` in `WaitRequest.cs`, handling in `Context.BlockOnRequest`, and a fulfillment mechanism in `ChannelManager` (analogous to `SignalManager.Subscribe`/`Broadcast`). The `ChannelWaitCondition` type and scheduler handling already exist in `WaitConditionState.cs` and `ZohRuntime.cs`.
- **Push `wait` param blocking**: `wait: true` (default) should block until consumed (rendezvous). Depends on channel blocking infrastructure.
