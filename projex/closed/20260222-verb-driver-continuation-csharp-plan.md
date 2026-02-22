# C# Impl: Verb Driver Continuation — Adapt Spec Change

> **Status:** Complete
> **Completed:** 2026-02-22
> **Walkthrough:** [20260222-verb-driver-continuation-csharp-walkthrough.md](20260222-verb-driver-continuation-csharp-walkthrough.md)
> **Related Projex:** `20260222-scheduler-agnostic-continuation-plan.md`, `20260222-verb-driver-sync-eval.md`

---

## Summary

The impl spec now defines blocking verbs via a `Continuation` discriminated union returned from `ExecutionResult`, with `Context.block()` internalising all state management. This plan adapts the C# implementation to match: adds `VerbContinuation` type, adds `Continuation` to `VerbResult`, removes `SetState()` from `IExecutionContext`, adds `Context.Block()`, updates the run loop, and updates the three drivers that currently call `SetState()` directly (Sleep, Wait, Call). Channel blocking is out of scope.

**Scope:** `c#/src/Zoh.Runtime/` and `c#/tests/Zoh.Tests/` only.
**Changes:** 1 new file, 9 modified files (7 src + 2 tests).

---

## Objective

### Problem / Gap / Need
`SleepDriver`, `WaitDriver`, `CallDriver` call `ctx.SetState(X)` + set `ctx.WaitCondition` as side effects, then return `VerbResult.Ok()`. This couples every blocking driver to the C# tick-loop scheduler and exposes mutable scheduler state via `IExecutionContext.SetState()`.

### Success Criteria
- [ ] New `VerbContinuation` abstract record with `SleepContinuation`, `MessageContinuation`, `ContextContinuation` variants
- [ ] `VerbResult` has `Continuation` init property and `Yield(VerbContinuation)` factory method
- [ ] `IExecutionContext` no longer exposes `SetState()`
- [ ] `Context.Block(VerbContinuation)` applies blocking state and handles registrations
- [ ] `ZohRuntime.Run()` checks `result.Continuation != null` → calls `ctx.Block()` → breaks
- [ ] `SleepDriver`, `WaitDriver`, `CallDriver` return `VerbResult.Yield(...)` — no direct `SetState()` calls
- [ ] All existing tests pass (with `SleepTests` and `ConcurrencyTests` updated for new API)

### Out of Scope
- Channel blocking (`ChannelPullContinuation`, `ChannelPushContinuation`) — channel verbs are currently non-blocking; a separate plan will address this
- Async `ValueTask<VerbResult>` migration — this plan only adds Continuation; async is a follow-on
- `ExitDriver` (`ctx.Terminate()`) — terminal lifecycle action, not blocking, unchanged
- `ForkDriver`, `JumpDriver` — mutate IP/story (jumps), not blocking, unchanged

---

## Context

### Current State

**`IVerbDriver.cs`** — sync, returns `VerbResult`:
```csharp
VerbResult Execute(IExecutionContext context, VerbCallAst verbCall);
```

**`VerbResult.cs`** — value + diagnostics only, no continuation:
```csharp
public sealed record VerbResult(ZohValue Value, ImmutableArray<Diagnostic> Diagnostics)
```

**`IExecutionContext.cs`** — exposes `SetState()` to all drivers:
```csharp
void SetState(ContextState state);
```

**`ZohRuntime.cs` Run() loop** — detects blocking by state side-effect (lines ~199–210):
```csharp
result = driver.Execute(ctx, call);
// ... fatal check ...
ctx.LastResult = result.Value;
ctx.LastDiagnostics = result.Diagnostics;
// Then: auto-advance IP only if ctx.State == Running && IP unchanged
```

**`SleepDriver.cs`** — direct state mutation:
```csharp
ctx.SetState(ContextState.Sleeping);
ctx.WaitCondition = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);
return VerbResult.Ok();
```

**`WaitDriver.cs`** — subscribe + state mutation:
```csharp
ctx.SignalManager.Subscribe(signalName, ctx);
ctx.SetState(ContextState.WaitingMessage);
ctx.WaitCondition = signalName;
return VerbResult.Ok();
```

**`CallDriver.cs`** — schedule child + state mutation:
```csharp
ctx.ContextScheduler(newCtx);
ctx.SetState(ContextState.WaitingContext);
ctx.WaitCondition = newCtx;
return VerbResult.Ok();
```

### Key Files
| File | Role | Change |
|------|------|--------|
| `c#/src/Zoh.Runtime/Verbs/VerbContinuation.cs` | New: continuation type definitions | Create |
| `c#/src/Zoh.Runtime/Verbs/VerbResult.cs` | Result type | Add `Continuation` property + `Yield()` factory |
| `c#/src/Zoh.Runtime/Execution/IExecutionContext.cs` | Driver-facing context interface | Remove `SetState()` |
| `c#/src/Zoh.Runtime/Execution/Context.cs` | Concrete context | Add `Block(VerbContinuation)` |
| `c#/src/Zoh.Runtime/Execution/ZohRuntime.cs` | Run loop | Check `result.Continuation != null` |
| `c#/src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs` | Sleep blocking | Return `Yield(SleepContinuation)` |
| `c#/src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs` | Signal wait | Return `Yield(MessageContinuation)` |
| `c#/src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | Context wait | Return `Yield(ContextContinuation)` |
| `c#/tests/Zoh.Tests/Verbs/Flow/SleepTests.cs` | Sleep test | Update: assert `result.Continuation` not `ctx.State` |
| `c#/tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs` | Call/fork test | Update `Call_BlocksParent_AndSchedulesChild`: assert `result.Continuation` not `ctx.WaitCondition` |

### Dependencies
- **Requires:** Spec change `33fb91d` (already done)
- **Blocks:** Channel blocking plan (`ChannelPullContinuation`, `ChannelPushContinuation`)

---

## Implementation

### Step 1: Create `c#/src/Zoh.Runtime/Verbs/VerbContinuation.cs`

New file:

```csharp
using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs;

/// <summary>
/// Describes what a driver is waiting for. Returned via VerbResult.Continuation.
/// The runtime calls Context.Block(continuation) to apply blocking state.
/// Drivers must NOT call IExecutionContext.SetState() directly.
/// </summary>
public abstract record VerbContinuation;

/// <summary>/sleep — block for a fixed duration.</summary>
public sealed record SleepContinuation(double DurationMs) : VerbContinuation;

/// <summary>/wait — block until a named signal is received.</summary>
public sealed record MessageContinuation(string MessageName, double? TimeoutMs = null) : VerbContinuation;

/// <summary>/call — block until a child context terminates.</summary>
public sealed record ContextContinuation(IExecutionContext ChildContext) : VerbContinuation;
```

`ContextContinuation` uses `IExecutionContext` (not concrete `Context`) to avoid a circular dependency between `Verbs/` and `Execution/`. The runtime operates on the concrete `Context` type directly.

---

### Step 2: Update `c#/src/Zoh.Runtime/Verbs/VerbResult.cs`

Add `Continuation` as an `init` property (not a positional parameter — preserves full backward compatibility with record deconstruction) and a `Yield()` factory:

```csharp
// Add below the existing Fatal factory:
public VerbContinuation? Continuation { get; init; }

/// <summary>
/// Driver is yielding — context must block until continuation is fulfilled.
/// </summary>
public static VerbResult Yield(VerbContinuation continuation)
    => new(ZohNothing.Instance, ImmutableArray<Diagnostic>.Empty) { Continuation = continuation };
```

---

### Step 3: Remove `SetState()` from `c#/src/Zoh.Runtime/Execution/IExecutionContext.cs`

```csharp
// Remove this line:
void SetState(ContextState state);
```

`SignalManager` stores `HashSet<Context>` (concrete type) so its `Broadcast()` call `ctx.SetState(ContextState.Running)` is unaffected. `ZohRuntime.Run()` operates on `Context` (concrete) so its fatal-termination `ctx.SetState(ContextState.Terminated)` is unaffected. `TestExecutionContext.SetState()` remains as a regular (non-interface) method — callers via concrete type still compile.

---

### Step 4: Add `Context.Block()` to `c#/src/Zoh.Runtime/Execution/Context.cs`

`using Zoh.Runtime.Verbs;` is already present. Add after `Terminate()`:

```csharp
/// <summary>
/// Applies a blocking continuation returned by a verb driver.
/// Sets context state and handles all registrations (signal subscribe, etc.).
/// This is the only place context state is set for blocking — drivers must
/// return a VerbContinuation instead of calling SetState() directly.
/// </summary>
public void Block(VerbContinuation continuation)
{
    switch (continuation)
    {
        case SleepContinuation s:
            WaitCondition = DateTimeOffset.UtcNow.AddMilliseconds(s.DurationMs);
            SetState(ContextState.Sleeping);
            break;

        case MessageContinuation m:
            SignalManager.Subscribe(m.MessageName, this);
            WaitCondition = m.MessageName;
            SetState(ContextState.WaitingMessage);
            break;

        case ContextContinuation c:
            WaitCondition = c.ChildContext;
            SetState(ContextState.WaitingContext);
            break;

        default:
            throw new InvalidOperationException($"Unhandled continuation type: {continuation.GetType().Name}");
    }
}
```

`SetState()` is called on `this` (concrete `Context`) from within `Context.Block()` — this is legitimate internal state management, not driver code.

---

### Step 5: Update `c#/src/Zoh.Runtime/Execution/ZohRuntime.cs` Run() loop

After driver execution and fatal check, add continuation handling before the IP-advance block:

```csharp
// After ctx.LastDiagnostics = result.Diagnostics; add:
if (result.Continuation != null)
{
    ctx.Block(result.Continuation);
    break;
}
```

The existing auto-IP-advance block (`if (ctx.State == ContextState.Running && ...)`) is untouched — the `break` above skips it correctly for blocking verbs.

---

### Step 6: Update `c#/src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs`

```csharp
// Remove:
ctx.SetState(ContextState.Sleeping);
ctx.WaitCondition = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);
return VerbResult.Ok();

// Replace with:
return VerbResult.Yield(new SleepContinuation(durationSeconds * 1000));
```

Remove the `ctx` cast and the `ContextState` using if they become unused.

---

### Step 7: Update `c#/src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs`

```csharp
// Remove:
ctx.SignalManager.Subscribe(signalName, ctx);
ctx.SetState(ContextState.WaitingMessage);
ctx.WaitCondition = signalName;
return VerbResult.Ok();

// Replace with:
return VerbResult.Yield(new MessageContinuation(signalName));
```

`Context.Block(MessageContinuation)` handles `SignalManager.Subscribe`. Remove the `ctx` cast and `ContextState` using if they become unused.

---

### Step 8: Update `c#/src/Zoh.Runtime/Verbs/Flow/CallDriver.cs`

```csharp
// Remove:
ctx.SetState(ContextState.WaitingContext);
ctx.WaitCondition = newCtx;
return VerbResult.Ok();

// Replace with:
return VerbResult.Yield(new ContextContinuation(newCtx));
```

`ctx.ContextScheduler(newCtx)` stays — child must be scheduled before the parent blocks. `Context.Block(ContextContinuation)` sets `WaitCondition` and state.

---

### Step 9: Update `c#/tests/Zoh.Tests/Verbs/Flow/SleepTests.cs`

The test currently asserts `ctx.State == Sleeping` and `ctx.WaitCondition is DateTimeOffset`, which tested the old side-effect. Update `Sleep_SetsSleepingState_AndWaitCondition` to assert on the continuation instead:

```csharp
// Replace the three assertions after driver.Execute with:
Assert.True(result.IsSuccess);
var cont = Assert.IsType<SleepContinuation>(result.Continuation);
Assert.True(cont.DurationMs >= 1000);
Assert.Equal(ContextState.Running, ctx.State); // driver no longer mutates state
```

Rename the test to `Sleep_ReturnsSleepContinuation`.

---

### Step 10: Update `c#/tests/Zoh.Tests/Verbs/Flow/ConcurrencyTests.cs`

The `Call_BlocksParent_AndSchedulesChild` test checks `ctx.State == WaitingContext` and `ctx.WaitCondition == scheduled[0]`, which tested the old side-effect. Update to assert on the continuation:

```csharp
// Replace lines 104-107:
Assert.True(result.IsSuccess);
var cont = Assert.IsType<ContextContinuation>(result.Continuation);
Assert.Single(scheduled);
Assert.Same(scheduled[0], cont.ChildContext);
Assert.Equal(ContextState.Running, ctx.State); // driver no longer mutates state
```

---

## Verification Plan

### Build Check
- [ ] `dotnet build c#/src/Zoh.Runtime/Zoh.Runtime.csproj` passes with no errors

### Correctness Check
- [ ] Grep `c#/src/Zoh.Runtime/Verbs/` for `SetState` — zero results
- [ ] `SleepDriver`, `WaitDriver`, `CallDriver` contain no `SetState` or `WaitCondition =` assignments
- [ ] `Context.Block()` exists with 3 cases: Sleep, Message, Context

### Test Run
- [ ] `dotnet test c#/tests/Zoh.Tests/` passes with no failures

---

## Rollback Plan
Git revert the commit(s). All changes are in one bounded area.

---

## Notes

### Assumptions
- `WaitCondition` on `Context` remains `object?` — `Context.Block()` sets it to the appropriate typed value (DateTimeOffset, string, IExecutionContext) as before
- `ContextState` enum and the external scheduler (host code) are unchanged — they still read `ctx.State` and `ctx.WaitCondition` to drive resumption
- `TestExecutionContext.SetState()` remains as a regular method (it is not called through `IExecutionContext` anywhere in tests — the concrete type is always used)

### Risks
- **Deconstruction breaks**: If any test does positional record deconstruction of `VerbResult(value, diags)`, the `init` property approach keeps it safe. Verify no 3-positional deconstruct exists before execution.
