# C# Runtime API Surface Alignment

> **Status:** In Progress
> **Created:** 2026-03-05
> **Author:** Agent
> **Source:** 20260304-runtime-api-surface-spec-plan-walkthrough.md
> **Related Projex:** 20260304-runtime-api-surface-spec-plan.md (spec-side, Complete)

---

## Summary

Align the C# runtime (`Zoh.Runtime`) with the revised `impl/09_runtime.md` spec. Add `ContextHandle`, `ExecutionResult`, `VariableAccessor` types. Implement `Tick(deltaTimeMs)` as the multi-context scheduler and `Resume(handle, value)` as the public host resume path. Replace the system-clock dependency in `BlockOnRequest` with runtime-supplied elapsed time.

**Scope:** `csharp/src/Zoh.Runtime/` and `csharp/tests/Zoh.Tests/`
**Estimated Changes:** ~14 files modified, 5 files created

---

## Objective

### Problem / Gap / Need

The spec revision internalized `Context` and introduced `ContextHandle`, `ExecutionResult`, `tick(deltaTimeMs)`, and `resume(handle, value)` as the public API surface. The C# implementation still:

- Exposes raw `Context` to callers (all fields publicly accessible)
- Has no tick-loop scheduler — each `Context.Run()` is blocking and single-context
- Requires hosts to call `ctx.Resume()` directly on the internal context
- Uses `DateTimeOffset.UtcNow` in `BlockOnRequest` (system-clock dependency, violating the spec's host-supplied time model)
- Stores untyped `object?` in `WaitCondition` (no structured resolution possible)
- Has no `MaxStatementsPerTick` config

### Success Criteria

- [ ] `ContextHandle` type with `Id` and `State` (read-only, live-tracking)
- [ ] `ExecutionResult` type with `Value`, `Diagnostics`, `Variables` (`VariableAccessor`)
- [ ] `VariableAccessor` type with `Get`, `Has`, `Keys`
- [ ] `ZohRuntime.StartContext(story)` returns `ContextHandle`
- [ ] `ZohRuntime.Tick(deltaTimeMs)` drives all contexts (resolve waits + run)
- [ ] `ZohRuntime.Resume(handle, value)` is the public host resume path
- [ ] `ZohRuntime.GetResult(handle)` returns `ExecutionResult` for terminated contexts
- [ ] `RuntimeConfig.MaxStatementsPerTick` property exists (enforcement deferred)
- [ ] `Context.WaitCondition` uses typed `WaitConditionState` hierarchy
- [ ] `BlockOnRequest` uses `ElapsedMsProvider` instead of `DateTimeOffset.UtcNow`
- [ ] Presentation handlers (`IConverseHandler`, `IChooseHandler`, `IChooseFromHandler`, `IPromptHandler`) receive `ContextHandle`
- [ ] Old API (`CreateContext`, `Run`, `RunToCompletion`) marked `[Obsolete]`
- [ ] All existing tests pass
- [ ] New tests cover `ContextHandle`, `ExecutionResult`, `Tick`, `Resume`

### Out of Scope

- Removing old API methods (deferred to migration plan)
- Migrating existing tests from old to new API (deferred)
- Media handler interface changes (`IShowHandler`, `IPlayHandler`, etc. — non-blocking, no handle needed for resume)
- `WaitingChannelPush` state addition (separate channel semantics concern)
- Async execution model implementation
- `MaxStatementsPerTick` enforcement in `Context.Run()` (config added, enforcement deferred)

---

## Context

### Current State

`ZohRuntime` exposes raw `Context` objects. No tick-loop scheduler exists. Presentation handlers receive `IExecutionContext` and callers resume via `ctx.Resume()` directly. `Context.WaitCondition` is `object?` with ad-hoc casts. `BlockOnRequest` uses `DateTimeOffset.UtcNow` for sleep wake times.

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `csharp/src/Zoh.Runtime/Execution/ZohRuntime.cs` | Runtime class | Add `StartContext`, `Tick`, `Resume`, `GetResult`, `_elapsedMs`, handle registry, `[Obsolete]` on old methods |
| `csharp/src/Zoh.Runtime/Execution/Context.cs` | Context class | Add `Handle`, `ElapsedMsProvider`, typed `WaitCondition`, update `BlockOnRequest` |
| `csharp/src/Zoh.Runtime/Execution/RuntimeConfig.cs` | Config | Add `MaxStatementsPerTick` |
| `csharp/src/Zoh.Runtime/Variables/VariableStore.cs` | Variable storage | Add `GetAllKeys()` |
| `csharp/src/.../Presentation/IConverseHandler.cs` | Handler interface | `ContextHandle` parameter |
| `csharp/src/.../Presentation/IChooseHandler.cs` | Handler interface | `ContextHandle` parameter |
| `csharp/src/.../Presentation/IChooseFromHandler.cs` | Handler interface | `ContextHandle` parameter |
| `csharp/src/.../Presentation/IPromptHandler.cs` | Handler interface | `ContextHandle` parameter |
| `csharp/src/.../Presentation/ConverseDriver.cs` | Driver | Pass `ctx.Handle!` to handler |
| `csharp/src/.../Presentation/ChooseDriver.cs` | Driver | Pass `ctx.Handle!` to handler |
| `csharp/src/.../Presentation/ChooseFromDriver.cs` | Driver | Pass `ctx.Handle!` to handler |
| `csharp/src/.../Presentation/PromptDriver.cs` | Driver | Pass `ctx.Handle!` to handler |

### Dependencies

- **Requires:** Spec revision complete (20260304-runtime-api-surface-spec-plan-walkthrough.md — Complete)
- **Blocks:** Future test migration plan, async model plan

### Constraints

- Old API must remain functional (marked `[Obsolete]`) — 95 usages across 17 test files
- `SignalManager.Broadcast` continues to directly resume contexts (fast path); scheduler handles timeout only
- Channel value delivery continues via `PushDriver` fast path; scheduler handles timeout only

---

## Implementation

### Step 1: Add New Types

**Objective:** Create `ContextHandle`, `ExecutionResult`, `VariableAccessor`, and typed `WaitConditionState` hierarchy.

**Files (all new):**
- `csharp/src/Zoh.Runtime/Execution/ContextHandle.cs`
- `csharp/src/Zoh.Runtime/Execution/ExecutionResult.cs`
- `csharp/src/Zoh.Runtime/Execution/VariableAccessor.cs`
- `csharp/src/Zoh.Runtime/Execution/WaitConditionState.cs`

**Changes:**

```csharp
// ContextHandle.cs
namespace Zoh.Runtime.Execution;

/// <summary>
/// Opaque handle to a context. The only representation visible to callers.
/// Exposes read-only state for host code to identify and track contexts.
/// Internal fields (IP, continuations, defers) are not accessible.
/// </summary>
public class ContextHandle
{
    private readonly Context _context;

    internal ContextHandle(Context context) => _context = context;

    public string Id => _context.Id;
    public ContextState State => _context.State;

    internal Context InternalContext => _context;
}
```

```csharp
// ExecutionResult.cs
namespace Zoh.Runtime.Execution;

using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

/// <summary>
/// Result of a terminated context. Provides the final return value,
/// collected diagnostics, and lazy variable access.
/// </summary>
public class ExecutionResult
{
    private readonly Context _context;

    internal ExecutionResult(Context context)
    {
        if (context.State != ContextState.Terminated)
            throw new InvalidOperationException(
                "ExecutionResult is only valid for terminated contexts.");
        _context = context;
    }

    public ZohValue Value => _context.LastResult;
    public IReadOnlyList<Diagnostic> Diagnostics => (IReadOnlyList<Diagnostic>)_context.LastDiagnostics;
    public VariableAccessor Variables => new(_context.Variables);
}
```

```csharp
// VariableAccessor.cs
namespace Zoh.Runtime.Execution;

using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;

/// <summary>
/// Lazy accessor into a context's variable state.
/// Reads from the internal store on demand — no data copied until accessed.
/// </summary>
public class VariableAccessor
{
    private readonly VariableStore _store;

    internal VariableAccessor(VariableStore store) => _store = store;

    public ZohValue Get(string name) => _store.Get(name);
    public bool Has(string name) => _store.TryGet(name, out _);
    public IReadOnlyList<string> Keys() => _store.GetAllKeys();
}
```

```csharp
// WaitConditionState.cs
namespace Zoh.Runtime.Execution;

/// <summary>
/// Typed wait condition stored on a blocked context.
/// Used by the tick-loop scheduler (ResolveWait) to determine
/// when a blocked context should resume.
/// </summary>
public abstract record WaitConditionState;

public sealed record SleepCondition(double WakeTimeMs) : WaitConditionState;

public sealed record HostWaitCondition(double StartTimeMs, double? TimeoutMs) : WaitConditionState
{
    public bool IsTimedOut(double elapsedMs) =>
        TimeoutMs.HasValue && elapsedMs >= StartTimeMs + TimeoutMs.Value;
}

public sealed record SignalWaitCondition(
    string MessageName, double StartTimeMs, double? TimeoutMs) : WaitConditionState
{
    public bool IsTimedOut(double elapsedMs) =>
        TimeoutMs.HasValue && elapsedMs >= StartTimeMs + TimeoutMs.Value;
}

public sealed record ContextJoinCondition(string TargetContextId) : WaitConditionState;

public sealed record ChannelWaitCondition(
    string ChannelName, double StartTimeMs, double? TimeoutMs) : WaitConditionState
{
    public bool IsTimedOut(double elapsedMs) =>
        TimeoutMs.HasValue && elapsedMs >= StartTimeMs + TimeoutMs.Value;
}
```

**Verification:** Files compile. All constructors are `internal` — callers cannot instantiate from outside the assembly.

---

### Step 2: Update Supporting Types

**Objective:** Add `MaxStatementsPerTick` to config, add `GetAllKeys()` to `VariableStore`, update `Context` to use typed `WaitCondition` and add `Handle`/`ElapsedMsProvider` properties, rewrite `BlockOnRequest` to use runtime-supplied time.

**Files:**
- `csharp/src/Zoh.Runtime/Execution/RuntimeConfig.cs`
- `csharp/src/Zoh.Runtime/Variables/VariableStore.cs`
- `csharp/src/Zoh.Runtime/Execution/Context.cs`

**Changes:**

**RuntimeConfig** — add property:

```csharp
// After existing properties, before Default:
/// <summary>Statement budget per context.Run() invocation. 0 = unlimited.</summary>
public int MaxStatementsPerTick { get; set; } = 0;
```

**VariableStore** — add method (after `ClearStory`):

```csharp
/// <summary>Returns all variable names across both story and context scopes.</summary>
public IReadOnlyList<string> GetAllKeys()
{
    var keys = new HashSet<string>(_storyVariables.Keys);
    keys.UnionWith(_contextVariables.Keys);
    return keys.ToList().AsReadOnly();
}
```

**Context** — change `WaitCondition` type (L235):

```csharp
// Before:
public object? WaitCondition { get; set; }

// After:
public WaitConditionState? WaitCondition { get; set; }
```

**Context** — add `Handle` and `ElapsedMsProvider` (after existing delegate properties, ~L32):

```csharp
public ContextHandle? Handle { get; internal set; }
public Func<double>? ElapsedMsProvider { get; set; }
```

**Context.BlockOnRequest** — replace system clock with runtime time (L112–140):

```csharp
// Before:
private void BlockOnRequest(WaitRequest request)
{
    switch (request)
    {
        case SleepRequest s:
            WaitCondition = DateTimeOffset.UtcNow.AddMilliseconds(s.DurationMs);
            SetState(ContextState.Sleeping);
            break;

        case SignalRequest m:
            SignalManager.Subscribe(m.MessageName, this);
            WaitCondition = m.MessageName;
            SetState(ContextState.WaitingMessage);
            break;

        case JoinContextRequest c:
            WaitCondition = c.ContextId;
            SetState(ContextState.WaitingContext);
            break;

        case HostRequest h:
            WaitCondition = h.TimeoutMs;
            SetState(ContextState.WaitingHost);
            break;

        default:
            throw new InvalidOperationException($"Unhandled request type: {request.GetType().Name}");
    }
}

// After:
private void BlockOnRequest(WaitRequest request)
{
    var elapsedMs = ElapsedMsProvider?.Invoke() ?? 0;
    switch (request)
    {
        case SleepRequest s:
            WaitCondition = new SleepCondition(elapsedMs + s.DurationMs);
            SetState(ContextState.Sleeping);
            break;

        case SignalRequest m:
            SignalManager.Subscribe(m.MessageName, this);
            WaitCondition = new SignalWaitCondition(m.MessageName, elapsedMs, m.TimeoutMs);
            SetState(ContextState.WaitingMessage);
            break;

        case JoinContextRequest c:
            WaitCondition = new ContextJoinCondition(c.ContextId);
            SetState(ContextState.WaitingContext);
            break;

        case HostRequest h:
            WaitCondition = new HostWaitCondition(elapsedMs, h.TimeoutMs);
            SetState(ContextState.WaitingHost);
            break;

        default:
            throw new InvalidOperationException($"Unhandled request type: {request.GetType().Name}");
    }
}
```

**Rationale:** Eliminates `DateTimeOffset.UtcNow` — all time is now host-supplied via the runtime's `_elapsedMs` field, consistent with the spec's design principle. Typed wait conditions enable the scheduler's `ResolveWait` to inspect conditions without ad-hoc casts.

**Verification:** `dotnet build` succeeds. Existing tests pass (no test references `WaitCondition`). Sleep-based tests may need a `Tick()` call to advance time — verified in Step 5.

---

### Step 3: Update ZohRuntime — New API Surface

**Objective:** Add `StartContext`, `Tick` (with `ResolveWait`), `Resume(handle, value)`, `GetResult`. Extract shared context creation. Mark old methods `[Obsolete]`.

**Files:**
- `csharp/src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**

**Add fields** (after existing private fields):

```csharp
private double _elapsedMs;
private readonly Dictionary<string, ContextHandle> _handles = new();
```

**Add internal accessor:**

```csharp
internal double ElapsedMs => _elapsedMs;
```

**Extract shared context wiring** (new private method):

```csharp
private Context CreateContextInternal(CompiledStory story)
{
    var store = new VariableStore(new Dictionary<string, Variable>());
    var ctx = new Context(store, Storage, Channels, SignalManager);

    ctx.VerbExecutor = ExecuteVerb;
    ctx.StatementExecutor = ExecuteStatement;
    ctx.StoryLoader = GetCompiledStory;
    ctx.ContextScheduler = AddContext;
    ctx.ElapsedMsProvider = () => _elapsedMs;
    ctx.CurrentStory = story;

    var handle = new ContextHandle(ctx);
    ctx.Handle = handle;
    _handles[ctx.Id] = handle;
    _contexts.Add(ctx);
    return ctx;
}
```

**Add `StartContext`:**

```csharp
/// <summary>
/// Creates a new context for the given story and returns an opaque handle.
/// The context begins in Running state and will execute on the next Tick().
/// </summary>
public ContextHandle StartContext(CompiledStory story)
{
    var ctx = CreateContextInternal(story);
    return ctx.Handle!;
}
```

**Add `Tick`:**

```csharp
/// <summary>
/// Advances the runtime by deltaTimeMs. Accumulates elapsed time, resolves
/// blocked contexts whose wait conditions are met, then runs all RUNNING contexts.
/// </summary>
public void Tick(double deltaTimeMs)
{
    _elapsedMs += deltaTimeMs;

    for (int i = 0; i < _contexts.Count; i++)
    {
        var ctx = _contexts[i];

        if (ctx.State != ContextState.Running && ctx.State != ContextState.Terminated)
        {
            var token = ctx.ResumeToken;
            var outcome = ResolveWait(ctx);
            if (outcome != null)
            {
                ctx.Resume(outcome, token);
            }
        }

        if (ctx.State == ContextState.Running)
        {
            ctx.Run();
        }
    }
}
```

**Add `ResolveWait`:**

```csharp
/// <summary>
/// Checks if a blocked context's wait condition is met.
/// Returns WaitOutcome if ready to resume, null if still waiting.
/// </summary>
private WaitOutcome? ResolveWait(Context ctx)
{
    switch (ctx.State)
    {
        case ContextState.Sleeping:
            if (ctx.WaitCondition is SleepCondition sleep && _elapsedMs >= sleep.WakeTimeMs)
                return new WaitCompleted(ZohValue.Nothing);
            return null;

        case ContextState.WaitingHost:
            // Host-driven: scheduler only handles timeout.
            // Host calls runtime.Resume(handle, value) to fulfill.
            if (ctx.WaitCondition is HostWaitCondition host && host.IsTimedOut(_elapsedMs))
                return new WaitTimedOut();
            return null;

        case ContextState.WaitingMessage:
            // Fulfillment handled by SignalManager.Broadcast (fast path).
            // Scheduler only handles timeout.
            if (ctx.WaitCondition is SignalWaitCondition sig && sig.IsTimedOut(_elapsedMs))
            {
                SignalManager.Unsubscribe(sig.MessageName, ctx);
                return new WaitTimedOut();
            }
            return null;

        case ContextState.WaitingContext:
            if (ctx.WaitCondition is ContextJoinCondition join)
            {
                var target = _contexts.FirstOrDefault(c => c.Id == join.TargetContextId);
                if (target == null || target.State == ContextState.Terminated)
                    return new WaitCompleted(target?.LastResult ?? ZohValue.Nothing);
            }
            return null;

        case ContextState.WaitingChannel:
            // Value delivery handled by PushDriver fast path.
            // Scheduler only handles timeout.
            if (ctx.WaitCondition is ChannelWaitCondition chan && chan.IsTimedOut(_elapsedMs))
                return new WaitTimedOut();
            return null;

        default:
            return null;
    }
}
```

**Add `Resume(ContextHandle, ZohValue)`:**

```csharp
/// <summary>
/// Resumes a WAITING_HOST context with the given value.
/// The only public path for host code to unblock a suspended context.
/// </summary>
public void Resume(ContextHandle handle, ZohValue value)
{
    var ctx = handle.InternalContext;
    var token = ctx.ResumeToken;
    ctx.Resume(new WaitCompleted(value), token);
}
```

**Add `GetResult`:**

```csharp
/// <summary>
/// Returns the execution result for a terminated context.
/// Throws if the context has not terminated.
/// </summary>
public ExecutionResult GetResult(ContextHandle handle)
{
    return new ExecutionResult(handle.InternalContext);
}
```

**Refactor `CreateContext` to delegate + mark obsolete:**

```csharp
// Before:
public Context CreateContext(CompiledStory story)
{
    var store = new VariableStore(new Dictionary<string, Variable>());
    var ctx = new Context(store, Storage, Channels, SignalManager);

    ctx.VerbExecutor = ExecuteVerb;
    ctx.StatementExecutor = ExecuteStatement;
    ctx.StoryLoader = GetCompiledStory;
    ctx.ContextScheduler = AddContext;
    ctx.CurrentStory = story;

    _contexts.Add(ctx);
    return ctx;
}

// After:
[Obsolete("Use StartContext() which returns a ContextHandle.")]
public Context CreateContext(CompiledStory story)
{
    return CreateContextInternal(story);
}
```

**Mark other old methods obsolete:**

```csharp
[Obsolete("Use Tick() to drive execution.")]
public void Run(Context ctx)
{
    ctx.Run();
}

[Obsolete("Use StartContext() + Tick().")]
public ZohValue RunToCompletion(Context ctx)
{
    Run(ctx);
    return ctx.LastResult ?? ZohNothing.Instance;
}

[Obsolete("Use StartContext() + Tick().")]
public Context RunToCompletion(string source)
{
    var story = LoadStory(source);
    var ctx = CreateContext(story);
    Run(ctx);
    return ctx;
}
```

**Expose handles read-only** (alongside existing `Contexts`):

```csharp
public IReadOnlyCollection<ContextHandle> Handles => _handles.Values;
```

**Verification:** Build succeeds. Old paths (`CreateContext`/`Run`/`RunToCompletion`) still compile and work — they now wire `ElapsedMsProvider` and create handles internally. New `StartContext`/`Tick`/`Resume` path compiles.

---

### Step 4: Update Presentation Handler Interfaces and Drivers

**Objective:** Presentation handlers receive `ContextHandle` instead of `IExecutionContext`. Drivers pass `ctx.Handle!` to handlers.

**Files:**
- `csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/IConverseHandler.cs`
- `csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/IChooseHandler.cs`
- `csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/IChooseFromHandler.cs`
- `csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/IPromptHandler.cs`
- `csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs`
- `csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs`
- `csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs`
- `csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs`

**Changes:**

**Handler interfaces** — change first parameter (all four):

```csharp
// Before:
void OnConverse(IExecutionContext context, ConverseRequest request);
void OnChoose(IExecutionContext context, ChooseRequest request);
void OnChooseFrom(IExecutionContext context, ChooseRequest request);
void OnPrompt(IExecutionContext context, PromptRequest request);

// After:
void OnConverse(ContextHandle handle, ConverseRequest request);
void OnChoose(ContextHandle handle, ChooseRequest request);
void OnChooseFrom(ContextHandle handle, ChooseRequest request);
void OnPrompt(ContextHandle handle, PromptRequest request);
```

Each interface file also needs `using Zoh.Runtime.Execution;` added (already present for `IExecutionContext` — verify the `ContextHandle` type is in the same namespace).

**Driver call sites** — pass handle instead of context (in each driver's `Execute` method):

```csharp
// Before (ConverseDriver):
_handler.OnConverse(ctx, request);

// After:
_handler.OnConverse(ctx.Handle!, request);
```

Same pattern for `ChooseDriver` (`_handler.OnChoose`), `ChooseFromDriver` (`_handler.OnChooseFrom`), `PromptDriver` (`_handler.OnPrompt`).

Remove the `using Zoh.Runtime.Execution;` import from handler interface files if `IExecutionContext` is no longer referenced (verify each file — the Request record types don't use it).

**Rationale:** Per spec: "The verb driver passes a ContextHandle to the host handler before suspending. The host feeds the response back via `runtime.resume(handle, value)`." This decouples the host from internal Context state.

**Verification:** Build succeeds after updating test handler implementations in Step 5.

---

### Step 5: Update and Add Tests

**Objective:** Update existing presentation driver tests to match new handler signatures. Add new tests for the public API surface.

**Files (modified):**
- `csharp/tests/Zoh.Tests/Verbs/Standard/Presentation/ConverseDriverTests.cs`
- `csharp/tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseDriverTests.cs`
- `csharp/tests/Zoh.Tests/Verbs/Standard/Presentation/ChooseFromDriverTests.cs`
- `csharp/tests/Zoh.Tests/Verbs/Standard/Presentation/PromptDriverTests.cs`

**Files (new):**
- `csharp/tests/Zoh.Tests/Execution/ApiSurfaceTests.cs`

**Changes:**

**Presentation test handler stubs** — update parameter type in each test file's inner handler class:

```csharp
// Before (example from ConverseDriverTests):
public void OnConverse(IExecutionContext context, ConverseRequest request)
{
    LastContext = context;
    LastRequest = request;
}

// After:
public void OnConverse(ContextHandle handle, ConverseRequest request)
{
    LastHandle = handle;
    LastRequest = request;
}
```

Each test file needs:
- Handler stub field changed from `IExecutionContext? LastContext` to `ContextHandle? LastHandle`
- Test assertions updated: `handler.LastContext` → `handler.LastHandle`
- Where tests call `ctx.Resume(value)` after handler callback, they continue to use `ctx.Resume()` (the old `Context.Resume` overload still works; these are existing tests using the old API)

**New test file — `ApiSurfaceTests.cs`:**

```csharp
namespace Zoh.Tests.Execution;

using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Verbs.Standard.Presentation;

public class ApiSurfaceTests
{
    // --- ContextHandle ---

    [Fact]
    public void StartContext_ReturnsHandle_WithIdAndRunningState()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *x, 1;");
        var handle = runtime.StartContext(story);

        Assert.NotNull(handle);
        Assert.NotEmpty(handle.Id);
        Assert.Equal(ContextState.Running, handle.State);
    }

    [Fact]
    public void ContextHandle_State_TracksContextLifecycle()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *x, 1;");
        var handle = runtime.StartContext(story);

        Assert.Equal(ContextState.Running, handle.State);
        runtime.Tick(0);
        Assert.Equal(ContextState.Terminated, handle.State);
    }

    // --- Tick ---

    [Fact]
    public void Tick_DrivesContextToCompletion()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *x, 42;");
        var handle = runtime.StartContext(story);

        runtime.Tick(0);

        Assert.Equal(ContextState.Terminated, handle.State);
    }

    [Fact]
    public void Tick_ResolvesSleepingContext()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/sleep 100;\n/set *x, 1;");
        var handle = runtime.StartContext(story);

        runtime.Tick(0);  // Runs until sleep, blocks
        Assert.Equal(ContextState.Sleeping, handle.State);

        runtime.Tick(50);  // Not enough time
        Assert.Equal(ContextState.Sleeping, handle.State);

        runtime.Tick(60);  // Enough time (total 110ms >= 100ms wake)
        Assert.Equal(ContextState.Terminated, handle.State);
    }

    [Fact]
    public void Tick_MultipleContexts_AllDriven()
    {
        var runtime = new ZohRuntime();
        var story1 = runtime.LoadStory("S1\n===\n/set *a, 1;");
        var story2 = runtime.LoadStory("S2\n===\n/set *b, 2;");
        var h1 = runtime.StartContext(story1);
        var h2 = runtime.StartContext(story2);

        runtime.Tick(0);

        Assert.Equal(ContextState.Terminated, h1.State);
        Assert.Equal(ContextState.Terminated, h2.State);
    }

    // --- Resume ---

    [Fact]
    public void Resume_UnblocksWaitingHostContext()
    {
        var runtime = new ZohRuntime();
        ContextHandle? capturedHandle = null;

        runtime.VerbRegistry.Register(new ConverseDriver(
            new TestConverseHandler(h => capturedHandle = h)));

        var story = runtime.LoadStory("Test\n===\n/converse \"Hello\";");
        var handle = runtime.StartContext(story);

        runtime.Tick(0);  // Runs until converse blocks
        Assert.Equal(ContextState.WaitingHost, handle.State);
        Assert.NotNull(capturedHandle);
        Assert.Equal(handle.Id, capturedHandle!.Id);

        runtime.Resume(handle, ZohValue.Nothing);
        runtime.Tick(0);  // Continue after resume
        Assert.Equal(ContextState.Terminated, handle.State);
    }

    // --- ExecutionResult ---

    [Fact]
    public void GetResult_ReturnsValueAndVariables()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *x, 42;");
        var handle = runtime.StartContext(story);
        runtime.Tick(0);

        var result = runtime.GetResult(handle);

        Assert.NotNull(result);
        Assert.True(result.Variables.Has("x"));
        Assert.Equal(new ZohInt(42), result.Variables.Get("x"));
    }

    [Fact]
    public void GetResult_ThrowsForNonTerminated()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/sleep 1000;");
        var handle = runtime.StartContext(story);
        runtime.Tick(0);

        Assert.Throws<InvalidOperationException>(() => runtime.GetResult(handle));
    }

    [Fact]
    public void VariableAccessor_Keys_ReturnsAllNames()
    {
        var runtime = new ZohRuntime();
        var story = runtime.LoadStory("Test\n===\n/set *a, 1;\n/set *b, 2;");
        var handle = runtime.StartContext(story);
        runtime.Tick(0);

        var result = runtime.GetResult(handle);
        var keys = result.Variables.Keys();

        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    // --- Test helper ---

    private class TestConverseHandler : IConverseHandler
    {
        private readonly Action<ContextHandle> _onConverse;

        public TestConverseHandler(Action<ContextHandle> onConverse)
            => _onConverse = onConverse;

        public void OnConverse(ContextHandle handle, ConverseRequest request)
            => _onConverse(handle);
    }
}
```

**Verification:** `dotnet test` — all existing and new tests pass.

---

## Verification Plan

### Automated Checks

- [ ] `dotnet build` succeeds (warnings OK for `[Obsolete]` usage in old tests)
- [ ] `dotnet test` — all existing tests pass
- [ ] `dotnet test --filter "ApiSurface"` — new tests pass

### Manual Verification

- [ ] `ContextHandle` cannot be constructed outside assembly (internal ctor)
- [ ] `ExecutionResult` throws `InvalidOperationException` for non-terminated context
- [ ] Old API paths (`CreateContext`/`Run`/`RunToCompletion`) still produce correct results
- [ ] No remaining `DateTimeOffset.UtcNow` in `Context.cs`

### Acceptance Criteria Validation

| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| ContextHandle type | Read file | Class with `Id`, `State`, internal ctor |
| ExecutionResult type | Read file | `Value`, `Diagnostics`, `Variables` properties |
| VariableAccessor | Read file | `Get`, `Has`, `Keys` methods |
| StartContext returns handle | `StartContext_ReturnsHandle` test | Pass |
| Tick drives contexts | `Tick_DrivesContextToCompletion` test | Pass |
| Tick resolves sleep | `Tick_ResolvesSleepingContext` test | Pass |
| Resume unblocks host | `Resume_UnblocksWaitingHostContext` test | Pass |
| GetResult works | `GetResult_ReturnsValueAndVariables` test | Pass |
| MaxStatementsPerTick | Read RuntimeConfig | Property exists, defaults to 0 |
| Handler interfaces | Read interfaces | `ContextHandle` parameter |
| Typed WaitCondition | Read Context.cs | `WaitConditionState?` not `object?` |
| No system clock in BlockOnRequest | Grep for `DateTimeOffset`/`UtcNow` in Context.cs | Zero hits |
| Old API obsolete | Build warnings | CS0618 on old method usage |

---

## Rollback Plan

All new files can be deleted and all edits reverted:

1. Delete new files: `ContextHandle.cs`, `ExecutionResult.cs`, `VariableAccessor.cs`, `WaitConditionState.cs`, `ApiSurfaceTests.cs`
2. `git checkout -- csharp/src/Zoh.Runtime/Execution/ZohRuntime.cs`
3. `git checkout -- csharp/src/Zoh.Runtime/Execution/Context.cs`
4. `git checkout -- csharp/src/Zoh.Runtime/Execution/RuntimeConfig.cs`
5. `git checkout -- csharp/src/Zoh.Runtime/Variables/VariableStore.cs`
6. `git checkout -- csharp/src/Zoh.Runtime/Verbs/Standard/Presentation/`
7. `git checkout -- csharp/tests/Zoh.Tests/Verbs/Standard/Presentation/`

---

## Notes

### Assumptions

- `VariableStore._storyVariables` and `_contextVariables` are accessible within the same project (internal visibility not needed — `GetAllKeys()` is a public method on the store)
- `Context.Handle` is set by the runtime during context creation — drivers can safely access `ctx.Handle!` for any runtime-created context
- `SignalManager.Broadcast` continues to directly resume contexts (fast path) — the scheduler's `ResolveWait` for `WAITING_MESSAGE` only handles timeout
- Presentation driver tests are the only code implementing `IConverseHandler`, `IChooseHandler`, `IChooseFromHandler`, `IPromptHandler` — the signature change impacts only test stubs

### Risks

- **Handler signature break**: All `IConverseHandler`/`IChooseHandler`/`IChooseFromHandler`/`IPromptHandler` implementors must update. **Mitigated:** only test files implement these interfaces in-repo.
- **Sleep behavior change**: Sleep wake time changes from system clock (`DateTimeOffset.UtcNow + duration`) to runtime-supplied time (`elapsedMs + duration`). **Verified non-issue:** the old `DateTimeOffset` value was stored but never polled — no scheduler existed. The only sleep test (`SleepTests.cs`) tests the driver's return value, not `BlockOnRequest` or wake resolution. The old `Run()` path exits on sleep and never checks wake time. Only `Tick()` will resolve sleeps via `ResolveWait`.

### Open Questions

(none)
