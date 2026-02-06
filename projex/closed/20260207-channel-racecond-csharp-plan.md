# Implement Channel Generation IDs and /open Verb in C# (Finding 1 - Part B)

> **Status:** Complete
> **Completed:** 2026-02-07
> **Walkthrough:** [Link](./20260207-channel-racecond-walkthrough.md)
> **Author:** Agent
> **Source:** [20260207-spec-impl-redteam.md](./20260207-spec-impl-redteam.md) - Finding 1
> **Related Projex:** [20260207-channel-racecond-impl-plan.md](../../../projex/closed/20260207-channel-racecond-impl-plan.md) (Part A - Docs)

---

## Summary

Implement the channel race condition fixes in the C# runtime: add generation tracking to channels, implement `/open` verb driver, update `/push` to error on missing/closed channels.

**Scope:** C# runtime channel implementation
**Estimated Changes:** ~5 files, 2 new files

---

## Objective

### Problem / Gap / Need

Current C# `ChannelManager.cs` uses simple `ConcurrentQueue` without:
1. Generation tracking to detect stale references
2. Closed state tracking
3. Proper `/open` verb (missing entirely)
4. `/push` errors on missing/closed channels (currently auto-creates)

### Success Criteria
- [ ] `ChannelManager` tracks full `Channel` objects with generation and closed state
- [ ] `/open` verb driver exists and creates/recreates channels
- [ ] `/push` verb driver errors on missing/closed channels
- [ ] `/pull` verb driver checks generation ID
- [ ] All existing tests pass
- [ ] New unit tests for channel generation behavior

### Out of Scope
- Documentation changes (separate plan)
- Other concurrency features
- Blocking pull (complex async - future work)

---

## Context

### Current State

`c#/src/Zoh.Runtime/Execution/ChannelManager.cs` (44 lines):
```csharp
public class ChannelManager
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ZohValue>> _channels = new();

    public void Push(string name, ZohValue value)
    {
        var ch = _channels.GetOrAdd(name, _ => new ConcurrentQueue<ZohValue>());  // Auto-creates!
        ch.Enqueue(value);
    }
    // ... Close just removes from dictionary
}
```

`c#/src/Zoh.Runtime/Types/ZohChannel.cs` (8 lines):
- Just a simple record `ZohChannel(string Name)` for the value type

No channel verb drivers exist in `Verbs/` - likely channel operations are in a general verb driver or not implemented yet.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Execution/ChannelManager.cs` | Channel storage | Add Channel class with generation, rewrite methods |
| `c#/src/Zoh.Runtime/Types/ZohChannel.cs` | Channel value type | Add generation field |
| `c#/src/Zoh.Runtime/Verbs/ChannelVerbDriver.cs` | [NEW] Channel verbs | Implement open/push/pull/close |
| `c#/tests/Zoh.Tests/Execution/ChannelManagerTests.cs` | [NEW] Channel tests | Test generation behavior |

### Dependencies
- **Requires:** Part A (impl docs) should be done first for clarity
- **Blocks:** Full concurrency feature completion

### Constraints
- Must maintain thread-safety
- Must pass existing tests
- Follow existing code patterns

---

## Implementation

### Overview

1. Create internal `Channel` class with queue, closed, generation
2. Refactor `ChannelManager` to use `Channel` objects
3. Update `ZohChannel` value type to track generation
4. Create channel verb drivers
5. Add unit tests

### Step 1: Create Internal Channel Class

**Objective:** Define a proper Channel class with all required state

**Files:**
- `c#/src/Zoh.Runtime/Execution/ChannelManager.cs`

**Changes:**

Add internal Channel class within ChannelManager:

```csharp
using System.Collections.Concurrent;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Execution;

public class ChannelManager
{
    // Internal channel representation
    private class Channel
    {
        public ConcurrentQueue<ZohValue> Queue { get; } = new();
        public int Generation { get; set; } = 1;
        public bool IsClosed { get; private set; }

        public void Close()
        {
            IsClosed = true;
            // Note: waiters notification would go here for async pull
        }
    }

    private readonly ConcurrentDictionary<string, Channel> _channels = new();
    private int _nextGeneration = 1;
    
    // ... methods to be updated in subsequent steps
}
```

**Rationale:** Encapsulates channel state properly. Generation starts at 1 so 0 can indicate "no generation captured".

**Verification:** Code compiles.

---

### Step 2: Refactor ChannelManager Methods

**Objective:** Update all manager methods to work with Channel objects

**Files:**
- `c#/src/Zoh.Runtime/Execution/ChannelManager.cs`

**Changes:**

Replace entire file with:

```csharp
using System.Collections.Concurrent;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Execution;

public class ChannelManager
{
    private class Channel
    {
        public ConcurrentQueue<ZohValue> Queue { get; } = new();
        public int Generation { get; init; }
        public bool IsClosed { get; private set; }

        public void Close() => IsClosed = true;
    }

    private readonly ConcurrentDictionary<string, Channel> _channels = new();
    private int _generationCounter = 0;

    /// <summary>
    /// Opens/creates a channel. If closed channel exists with same name, 
    /// creates new channel with incremented generation.
    /// </summary>
    /// <returns>The generation of the (possibly new) channel</returns>
    public int Open(string name)
    {
        name = name.ToLowerInvariant();
        
        var channel = _channels.AddOrUpdate(
            name,
            _ => new Channel { Generation = Interlocked.Increment(ref _generationCounter) },
            (_, existing) =>
            {
                if (existing.IsClosed)
                    return new Channel { Generation = Interlocked.Increment(ref _generationCounter) };
                return existing;
            });
        
        return channel.Generation;
    }

    /// <summary>
    /// Checks if a channel exists and is open.
    /// </summary>
    public bool Exists(string name)
    {
        name = name.ToLowerInvariant();
        return _channels.TryGetValue(name, out var ch) && !ch.IsClosed;
    }

    /// <summary>
    /// Gets the generation of a channel. Returns 0 if not found or closed.
    /// </summary>
    public int GetGeneration(string name)
    {
        name = name.ToLowerInvariant();
        if (_channels.TryGetValue(name, out var ch) && !ch.IsClosed)
            return ch.Generation;
        return 0;
    }

    /// <summary>
    /// Pushes a value to an open channel.
    /// Returns false if channel doesn't exist or is closed.
    /// </summary>
    public bool TryPush(string name, ZohValue value)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch) || ch.IsClosed)
            return false;
        
        ch.Queue.Enqueue(value);
        return true;
    }

    /// <summary>
    /// Tries to pull a value from a channel (non-blocking).
    /// Also verifies generation hasn't changed.
    /// </summary>
    public PullResult TryPull(string name, int expectedGeneration)
    {
        name = name.ToLowerInvariant();
        
        if (!_channels.TryGetValue(name, out var ch))
            return PullResult.NotFound;
        
        if (ch.IsClosed)
            return PullResult.Closed;
        
        if (ch.Generation != expectedGeneration)
            return PullResult.GenerationMismatch;
        
        if (ch.Queue.TryDequeue(out var value))
            return PullResult.Success(value);
        
        return PullResult.Empty;
    }

    /// <summary>
    /// Gets the count of items in a channel.
    /// </summary>
    public int Count(string name)
    {
        name = name.ToLowerInvariant();
        if (_channels.TryGetValue(name, out var ch) && !ch.IsClosed)
            return ch.Queue.Count;
        return 0;
    }

    /// <summary>
    /// Closes a channel. Returns false if channel doesn't exist or already closed.
    /// </summary>
    public bool TryClose(string name)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch) || ch.IsClosed)
            return false;
        
        ch.Close();
        return true;
    }
}

public readonly struct PullResult
{
    public PullStatus Status { get; init; }
    public ZohValue? Value { get; init; }

    public static PullResult NotFound => new() { Status = PullStatus.NotFound };
    public static PullResult Closed => new() { Status = PullStatus.Closed };
    public static PullResult GenerationMismatch => new() { Status = PullStatus.GenerationMismatch };
    public static PullResult Empty => new() { Status = PullStatus.Empty };
    public static PullResult Success(ZohValue value) => new() { Status = PullStatus.Success, Value = value };
}

public enum PullStatus
{
    Success,
    NotFound,
    Closed,
    Empty,
    GenerationMismatch
}
```

**Rationale:** 
- `Open()` returns generation for callers to capture
- `TryPush()` returns bool for error handling
- `TryPull()` takes expectedGeneration to detect stale references
- Case-insensitive names per spec

**Verification:** Run `dotnet build` in c# folder.

---

### Step 3: Update ZohChannel to Track Generation

**Objective:** Allow ZohChannel values to optionally track their captured generation

**Files:**
- `c#/src/Zoh.Runtime/Types/ZohChannel.cs`

**Changes:**

```csharp
namespace Zoh.Runtime.Types;

/// <summary>
/// Represents a channel reference value.
/// Generation tracks the generation at time of capture (0 = not captured yet).
/// </summary>
public sealed record ZohChannel(string Name, int Generation = 0) : ZohValue
{
    public override ZohValueType Type => ZohValueType.Channel;
    public override string ToString() => $"<{Name}>";
    
    /// <summary>
    /// Creates a new ZohChannel with the given generation.
    /// </summary>
    public ZohChannel WithGeneration(int generation) => this with { Generation = generation };
}
```

**Rationale:** Generation=0 means "just a name reference, not captured from a specific channel instance".

**Verification:** Run `dotnet build` in c# folder.

---

### Step 4: Create Channel Verb Drivers

**Objective:** Implement /open, /push, /pull, /close verb drivers

**Files:**
- `c#/src/Zoh.Runtime/Verbs/ChannelVerbDriver.cs` [NEW]

**Changes:**

Create new file:

```csharp
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs;

public class OpenVerbDriver : VerbDriver
{
    public override string Namespace => "channel";
    public override string Name => "open";
    public override int Priority => 0;

    public override ExecutionResult Execute(VerbCall call, IExecutionContext context)
    {
        if (call.UnnamedParams.Count < 1)
            return ExecutionResult.Fatal("parameter_not_found", "Expected channel parameter");

        var channelValue = context.Resolve(call.UnnamedParams[0]);
        if (channelValue is not ZohChannel channel)
            return ExecutionResult.Fatal("invalid_type", $"Expected channel, got: {channelValue.Type}");

        var generation = context.ChannelManager.Open(channel.Name);
        return ExecutionResult.Ok(ZohValue.Nothing);
    }
}

public class PushVerbDriver : VerbDriver
{
    public override string Namespace => "channel";
    public override string Name => "push";
    public override int Priority => 0;

    public override ExecutionResult Execute(VerbCall call, IExecutionContext context)
    {
        if (call.UnnamedParams.Count < 2)
            return ExecutionResult.Fatal("parameter_not_found", "Expected channel and value parameters");

        var channelValue = context.Resolve(call.UnnamedParams[0]);
        if (channelValue is not ZohChannel channel)
            return ExecutionResult.Fatal("invalid_type", $"Expected channel, got: {channelValue.Type}");

        var value = context.Resolve(call.UnnamedParams[1]);

        if (!context.ChannelManager.Exists(channel.Name))
            return ExecutionResult.Error("not_found", $"Channel does not exist: {channel.Name}");

        if (!context.ChannelManager.TryPush(channel.Name, value))
            return ExecutionResult.Error("closed", $"Cannot push to closed channel: {channel.Name}");

        return ExecutionResult.Ok(ZohValue.Nothing);
    }
}

public class PullVerbDriver : VerbDriver
{
    public override string Namespace => "channel";
    public override string Name => "pull";
    public override int Priority => 0;

    public override ExecutionResult Execute(VerbCall call, IExecutionContext context)
    {
        if (call.UnnamedParams.Count < 1)
            return ExecutionResult.Fatal("parameter_not_found", "Expected channel parameter");

        var channelValue = context.Resolve(call.UnnamedParams[0]);
        if (channelValue is not ZohChannel channel)
            return ExecutionResult.Fatal("invalid_type", $"Expected channel, got: {channelValue.Type}");

        // Get current generation for comparison
        var currentGen = context.ChannelManager.GetGeneration(channel.Name);
        if (currentGen == 0)
            return ExecutionResult.Error("not_found", $"Channel does not exist: {channel.Name}");

        // For now: non-blocking pull. Blocking requires async redesign.
        var result = context.ChannelManager.TryPull(channel.Name, currentGen);
        
        return result.Status switch
        {
            PullStatus.Success => ExecutionResult.Ok(result.Value!),
            PullStatus.NotFound => ExecutionResult.Error("not_found", $"Channel does not exist: {channel.Name}"),
            PullStatus.Closed => ExecutionResult.Error("closed", $"Channel is closed: {channel.Name}"),
            PullStatus.GenerationMismatch => ExecutionResult.Error("stale", $"Channel was recreated: {channel.Name}"),
            PullStatus.Empty => ExecutionResult.Ok(ZohValue.Nothing), // Non-blocking: return nothing if empty
            _ => ExecutionResult.Fatal("internal", "Unknown pull status")
        };
    }
}

public class CloseVerbDriver : VerbDriver
{
    public override string Namespace => "channel";
    public override string Name => "close";
    public override int Priority => 0;

    public override ExecutionResult Execute(VerbCall call, IExecutionContext context)
    {
        if (call.UnnamedParams.Count < 1)
            return ExecutionResult.Fatal("parameter_not_found", "Expected channel parameter");

        var channelValue = context.Resolve(call.UnnamedParams[0]);
        if (channelValue is not ZohChannel channel)
            return ExecutionResult.Fatal("invalid_type", $"Expected channel, got: {channelValue.Type}");

        if (!context.ChannelManager.TryClose(channel.Name))
            return ExecutionResult.Error("not_found", $"Channel does not exist or already closed: {channel.Name}");

        return ExecutionResult.Ok(ZohValue.Nothing);
    }
}
```

**Rationale:** Standard verb driver pattern. Non-blocking pull for now (blocking requires async context redesign).

**Verification:** Run `dotnet build` in c# folder.

---

### Step 5: Add Unit Tests

**Objective:** Test generation tracking and error conditions

**Files:**
- `c#/tests/Zoh.Tests/Execution/ChannelManagerTests.cs` [NEW]

**Changes:**

Create new test file:

```csharp
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;

namespace Zoh.Tests.Execution;

public class ChannelManagerTests
{
    [Fact]
    public void Open_CreatesChannel()
    {
        var manager = new ChannelManager();
        var gen = manager.Open("test");
        
        Assert.True(gen > 0);
        Assert.True(manager.Exists("test"));
    }

    [Fact]
    public void Open_ExistingOpen_NoOp()
    {
        var manager = new ChannelManager();
        var gen1 = manager.Open("test");
        var gen2 = manager.Open("test");
        
        Assert.Equal(gen1, gen2);
    }

    [Fact]
    public void Open_ClosedChannel_IncrementsGeneration()
    {
        var manager = new ChannelManager();
        var gen1 = manager.Open("test");
        manager.TryClose("test");
        var gen2 = manager.Open("test");
        
        Assert.True(gen2 > gen1);
    }

    [Fact]
    public void TryPush_NonExistent_ReturnsFalse()
    {
        var manager = new ChannelManager();
        var result = manager.TryPush("test", ZohValue.From(42));
        
        Assert.False(result);
    }

    [Fact]
    public void TryPush_ClosedChannel_ReturnsFalse()
    {
        var manager = new ChannelManager();
        manager.Open("test");
        manager.TryClose("test");
        var result = manager.TryPush("test", ZohValue.From(42));
        
        Assert.False(result);
    }

    [Fact]
    public void TryPush_OpenChannel_ReturnsTrue()
    {
        var manager = new ChannelManager();
        manager.Open("test");
        var result = manager.TryPush("test", ZohValue.From(42));
        
        Assert.True(result);
        Assert.Equal(1, manager.Count("test"));
    }

    [Fact]
    public void TryPull_WrongGeneration_ReturnsGenerationMismatch()
    {
        var manager = new ChannelManager();
        var gen1 = manager.Open("test");
        manager.TryPush("test", ZohValue.From(42));
        manager.TryClose("test");
        manager.Open("test");  // New channel with new generation
        
        var result = manager.TryPull("test", gen1);  // Old generation
        
        Assert.Equal(PullStatus.GenerationMismatch, result.Status);
    }

    [Fact]
    public void TryPull_CorrectGeneration_ReturnsValue()
    {
        var manager = new ChannelManager();
        var gen = manager.Open("test");
        manager.TryPush("test", ZohValue.From(42));
        
        var result = manager.TryPull("test", gen);
        
        Assert.Equal(PullStatus.Success, result.Status);
        Assert.IsType<ZohInteger>(result.Value);
    }

    [Fact]
    public void ChannelName_CaseInsensitive()
    {
        var manager = new ChannelManager();
        manager.Open("TEST");
        
        Assert.True(manager.Exists("test"));
        Assert.True(manager.Exists("Test"));
        Assert.True(manager.Exists("TEST"));
    }
}
```

**Rationale:** Tests the key behaviors: generation increment on reopen, push/pull errors, case insensitivity.

**Verification:** Run `dotnet test` in c# folder.

---

## Verification Plan

### Automated Checks
- [ ] `cd c# && dotnet build` — All projects compile
- [ ] `cd c# && dotnet test` — All tests pass (existing + new)

### Manual Verification
- [ ] Review ChannelManager changes for thread-safety
- [ ] Verify verb drivers follow existing patterns

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Generation tracking | Run ChannelManagerTests | All tests pass |
| /open creates channel | Open test | Generation > 0 returned |
| /push errors on missing | TryPush_NonExistent test | Returns false |
| /pull generation check | TryPull_WrongGeneration test | Returns GenerationMismatch |

---

## Rollback Plan

If implementation causes issues:

1. `git checkout -- c#/src/Zoh.Runtime/Execution/ChannelManager.cs`
2. `git checkout -- c#/src/Zoh.Runtime/Types/ZohChannel.cs`
3. Delete new files if any

---

## Notes

### Assumptions
- Existing verb driver base classes exist (VerbDriver, ExecutionResult, etc.)
- IExecutionContext has ChannelManager property
- ZohValue.From() exists for creating values

### Risks
- Verb driver registration may need updates
- IExecutionContext interface may need ChannelManager property added
- Blocking pull not implemented (returns Nothing for empty queue)

### Open Questions
- [ ] Does IExecutionContext currently have ChannelManager access?
- [ ] How are verb drivers registered? Need to add new ones to registry.
- [ ] Is blocking pull needed immediately or can we defer?
