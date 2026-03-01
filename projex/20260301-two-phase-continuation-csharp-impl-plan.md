# Two-Phase Continuation Model — C# Implementation

> **Status:** Ready
> **Created:** 2026-03-01
> **Author:** agent
> **Source:** `20260301-two-phase-continuation-model-proposal.md`
> **Related Projex:** `20260301-continuation-resume-ip-gap-eval.md`, `20260301-two-phase-continuation-spec-update-plan.md`

---

## Summary

Implement the two-phase continuation model in the C# runtime. Replace `VerbResult`/`VerbContinuation` with `DriverResult`/`Continuation`/`WaitRequest`/`WaitOutcome`. Update `Context` to use `ApplyResult()`/`Resume()` with token-guarded resumption. Update all blocking verb drivers to provide `onFulfilled` closures.

**Scope:** `csharp/src/Zoh.Runtime/` and `csharp/tests/Zoh.Tests/`
**Estimated Changes:** ~16 source files, ~4 test files

---

## Objective

### Problem / Gap / Need
The C# runtime has the same IP advancement bug as the spec (latent — no tick loop yet). `Context.Resume()` sets state to `Running` without invoking any driver post-resume logic. `SignalManager.Broadcast()` bypasses `Resume()` entirely. The `VerbResult`/`VerbContinuation` types don't support driver-owned resume callbacks.

### Success Criteria
- [ ] New types: `DriverResult` (discriminated union), `Continuation` (with `Func<WaitOutcome, DriverResult>`), `WaitRequest`, `WaitOutcome`
- [ ] `Context.Run()` uses `ApplyResult()` — IP advances only on `Complete`
- [ ] `Context.Resume(WaitOutcome, int token)` invokes `onFulfilled`, guards with `resumeToken`
- [ ] `Context.BlockOnRequest(WaitRequest)` replaces `Block(VerbContinuation)`
- [ ] All 7 blocking drivers return `DriverResult.Suspend` with `onFulfilled` closures
- [ ] `SignalManager.Broadcast()` calls `context.Resume()` instead of direct field mutation
- [ ] All existing tests pass (may need signature updates)
- [ ] `dotnet build` succeeds, `dotnet test` passes

### Out of Scope
- Spec file changes (separate plan)
- Implementing blocking `/pull` (currently non-blocking by design — separate work)
- Adding a tick loop / scheduler to `ZohRuntime` (deferred)
- New verbs or features

---

## Context

### Current State
- `VerbResult` is a flat record with optional `Continuation` property
- `VerbContinuation` is an abstract record with 4 concrete subtypes (Sleep, Message, Context, Host)
- `Context.Block()` switches on continuation type to set state
- `Context.Resume()` sets `LastResult` and state to `Running` — no callback
- `SignalManager.Broadcast()` directly mutates `ctx.SetState()`, `ctx.LastResult`, `ctx.WaitCondition`
- 7 drivers yield: Sleep, Wait, Call, Converse, Choose, ChooseFrom, Prompt

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Verbs/VerbResult.cs` | Current result type | Replace with `DriverResult` |
| `src/Zoh.Runtime/Verbs/VerbContinuation.cs` | Current continuation types | Replace with `Continuation`, `WaitRequest`, `WaitOutcome` |
| `src/Zoh.Runtime/Verbs/IVerbDriver.cs` | Driver interface | Return type `VerbResult` → `DriverResult` |
| `src/Zoh.Runtime/Execution/Context.cs` | Execution engine | `Run()`, `Block()` → `BlockOnRequest()`, `Resume()`, add `ApplyResult()` |
| `src/Zoh.Runtime/Execution/ContextState.cs` | State enum | No change needed (already has `WaitingHost`) |
| `src/Zoh.Runtime/Execution/IExecutionContext.cs` | Driver-facing interface | Add `Resume()` if needed for host access |
| `src/Zoh.Runtime/Execution/SignalManager.cs` | Signal dispatch | Call `Resume()` instead of direct field mutation |
| `src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs` | `/sleep` | Return `DriverResult.Suspend` |
| `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs` | `/wait` | Return `DriverResult.Suspend` |
| `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs` | `/call` | Return `DriverResult.Suspend` with inline var closure |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs` | `/converse` | Return `DriverResult.Suspend` |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs` | `/choose` | Return `DriverResult.Suspend` |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs` | `/choosefrom` | Return `DriverResult.Suspend` |
| `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs` | `/prompt` | Return `DriverResult.Suspend` |
| `src/Zoh.Runtime/Verbs/ChannelVerbs.cs` | `/push` | Update return type (push is non-blocking currently) |

### Dependencies
- **Requires:** Spec update plan executed first (defines canonical pseudocode)
- **Blocks:** Nothing immediately

### Constraints
- Must maintain backward compatibility for host code calling `Resume()` — the old `Resume(ZohValue?)` signature is used by test infrastructure and host handlers
- All existing tests must pass after migration

---

## Implementation

### Overview
Bottom-up: new types first, then `Context` internals, then drivers, then `SignalManager`, then fix tests.

### Step 1: New Type Files

**Objective:** Define `DriverResult`, `Continuation`, `WaitRequest`, `WaitOutcome`.

**Files:** Create or replace in `src/Zoh.Runtime/Verbs/`

**1a. Replace `VerbContinuation.cs` with three types:**

```csharp
// WaitRequest.cs
namespace Zoh.Runtime.Verbs;

public abstract record WaitRequest;
public sealed record SleepRequest(double DurationMs) : WaitRequest;
public sealed record SignalRequest(string MessageName, double? TimeoutMs = null) : WaitRequest;
public sealed record JoinContextRequest(string ContextId) : WaitRequest;
public sealed record HostRequest(double? TimeoutMs = null) : WaitRequest;
// ChannelPullRequest and ChannelPushRequest deferred (pull is non-blocking currently)

// WaitOutcome.cs
namespace Zoh.Runtime.Verbs;

public abstract record WaitOutcome;
public sealed record WaitCompleted(ZohValue Value) : WaitOutcome;
public sealed record WaitTimedOut : WaitOutcome;
public sealed record WaitCancelled(string Code, string Message) : WaitOutcome;

// Continuation.cs
namespace Zoh.Runtime.Verbs;

public sealed record Continuation(
    WaitRequest Request,
    Func<WaitOutcome, DriverResult> OnFulfilled
);
```

**1b. Replace `VerbResult.cs` with `DriverResult.cs`:**

```csharp
// DriverResult.cs
namespace Zoh.Runtime.Verbs;

public abstract record DriverResult
{
    public sealed record Complete(ZohValue Value, ImmutableArray<Diagnostic> Diagnostics) : DriverResult
    {
        public static Complete Ok(ZohValue? value = null)
            => new(value ?? ZohNothing.Instance, ImmutableArray<Diagnostic>.Empty);

        public static Complete Fatal(Diagnostic diagnostic)
            => new(ZohNothing.Instance, ImmutableArray.Create(diagnostic));

        public static Complete WithDiagnostics(ZohValue value, params Diagnostic[] diagnostics)
            => new(value, diagnostics.ToImmutableArray());
    }

    public sealed record Suspend(Continuation Continuation, ImmutableArray<Diagnostic> Diagnostics) : DriverResult
    {
        public Suspend(Continuation continuation)
            : this(continuation, ImmutableArray<Diagnostic>.Empty) { }
    }
}
```

**Rationale:** Types must exist before anything can reference them.

**Verification:** `dotnet build` compiles the new types (existing code won't compile yet — that's expected).

---

### Step 2: Update IVerbDriver Interface

**Objective:** Change return type from `VerbResult` to `DriverResult`.

**File:** `src/Zoh.Runtime/Verbs/IVerbDriver.cs`

```csharp
// Before:
VerbResult Execute(IExecutionContext context, VerbCallAst verbCall);

// After:
DriverResult Execute(IExecutionContext context, VerbCallAst verbCall);
```

**Rationale:** All drivers must return the new type. This will cause compile errors in every driver — that's the migration path.

---

### Step 3: Update Context — Core Execution

**Objective:** Replace `Run()`, `Block()`, `Resume()` with the two-phase model.

**File:** `src/Zoh.Runtime/Execution/Context.cs`

**3a. Add new fields:**

```csharp
public Continuation? PendingContinuation { get; private set; }
public int ResumeToken { get; private set; }
```

**3b. Replace `Run()` method (L37–98):**

The new `Run()` calls `ApplyResult()` after driver execution. The IP advancement guard (L90–96) is replaced by `ApplyResult()` logic — `Complete` advances IP, `Suspend` breaks the loop.

```csharp
public void Run()
{
    while (State == ContextState.Running)
    {
        if (CurrentStory == null || InstructionPointer >= CurrentStory.Statements.Length)
        {
            Terminate();
            break;
        }

        var stmt = CurrentStory.Statements[InstructionPointer];

        // Capture state before execution to detect jumps
        int entryIp = InstructionPointer;
        CompiledStory entryStory = CurrentStory;

        if (stmt is StatementAst.VerbCall callStmt)
        {
            var result = StatementExecutor!(this, callStmt.Call);
            ApplyResult(result, entryIp, entryStory);
        }
        else if (stmt is StatementAst.Label label)
        {
            var validation = ValidateContract(label.Name);
            if (validation is DriverResult.Complete c &&
                c.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
            {
                LastDiagnostics = c.Diagnostics;
                SetState(ContextState.Terminated);
                break;
            }
            // Labels: advance IP if no jump
            if (State == ContextState.Running &&
                InstructionPointer == entryIp &&
                CurrentStory == entryStory)
            {
                InstructionPointer++;
            }
        }
    }
}
```

**3c. Add `ApplyResult()` method:**

```csharp
private void ApplyResult(DriverResult result, int entryIp, CompiledStory entryStory)
{
    switch (result)
    {
        case DriverResult.Complete c:
            LastResult = c.Value;
            LastDiagnostics = c.Diagnostics;
            if (c.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal))
            {
                SetState(ContextState.Terminated);
            }
            else if (State == ContextState.Running &&
                     InstructionPointer == entryIp &&
                     CurrentStory == entryStory)
            {
                InstructionPointer++;
            }
            break;

        case DriverResult.Suspend s:
            LastDiagnostics = s.Diagnostics;
            ResumeToken++;
            PendingContinuation = s.Continuation;
            BlockOnRequest(s.Continuation.Request);
            // State is now non-Running — Run() loop exits
            break;
    }
}
```

**3d. Replace `Block()` with `BlockOnRequest()`:**

```csharp
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
```

**3e. Replace `Resume()`:**

```csharp
public void Resume(WaitOutcome outcome, int token)
{
    if (token != ResumeToken) return;       // Stale token
    if (PendingContinuation == null) return; // Already resumed

    var handler = PendingContinuation.OnFulfilled;
    PendingContinuation = null;
    WaitCondition = null;

    var result = handler(outcome);
    SetState(ContextState.Running);

    // Use current IP/story as entry point for ApplyResult
    int ip = InstructionPointer;
    var story = CurrentStory!;
    ApplyResult(result, ip, story);
}

// Keep backward-compatible overload for existing host code / tests
public void Resume(ZohValue? value = null)
{
    Resume(new WaitCompleted(value ?? ZohNothing.Instance), ResumeToken);
}
```

**3f. Update `Clone()` to copy new fields:**

Add `ResumeToken = 0` (fresh context), `PendingContinuation = null`.

**3g. Update `ValidateContract` return type:**

Change from `VerbResult` to `DriverResult`:

```csharp
public DriverResult ValidateContract(string checkpointName)
{
    // ... same logic but return DriverResult.Complete.Ok() / DriverResult.Complete.Fatal(...)
}
```

**Rationale:** Context is the core of the model change.

**Verification:** `Run()` compiles. `ApplyResult()` handles both `Complete` and `Suspend`. `Resume()` has token guard.

---

### Step 4: Update All Non-Blocking Drivers

**Objective:** Change return type from `VerbResult` to `DriverResult` for all drivers that never yield.

**Files:** All `IVerbDriver` implementations that return `VerbResult.Ok()` / `VerbResult.Fatal()` / `VerbResult.Error()`.

**Pattern:**
```csharp
// Before:
return VerbResult.Ok(value);
return VerbResult.Fatal(diagnostic);

// After:
return DriverResult.Complete.Ok(value);
return DriverResult.Complete.Fatal(diagnostic);
```

This is a mechanical find-and-replace across all driver files. The compile errors from Step 2 guide which files need updating.

**Verification:** `dotnet build` compiles all non-blocking drivers.

---

### Step 5: Update Blocking Drivers — Sleep, Wait

**Objective:** Convert `SleepDriver` and `WaitDriver` to return `DriverResult.Suspend` with closures.

**Files:**
- `src/Zoh.Runtime/Verbs/Flow/SleepDriver.cs`
- `src/Zoh.Runtime/Verbs/Signals/WaitDriver.cs`

**SleepDriver:**
```csharp
// Before:
return VerbResult.Yield(new SleepContinuation(durationSeconds * 1000));

// After:
return new DriverResult.Suspend(new Continuation(
    new SleepRequest(durationSeconds * 1000),
    _ => DriverResult.Complete.Ok()
));
```

**WaitDriver:**
```csharp
// Before:
return VerbResult.Yield(new MessageContinuation(signalName));

// After:
return new DriverResult.Suspend(new Continuation(
    new SignalRequest(signalName),
    outcome => outcome switch
    {
        WaitCompleted c => DriverResult.Complete.Ok(c.Value),
        WaitTimedOut => DriverResult.Complete.Ok(), // TODO: add timeout param
        WaitCancelled x => new DriverResult.Complete(
            ZohNothing.Instance,
            ImmutableArray.Create(new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message))),
        _ => DriverResult.Complete.Ok()
    }
));
```

**Verification:** Both drivers compile. Sleep closure ignores outcome. Wait closure maps outcome to result.

---

### Step 6: Update Blocking Drivers — Call

**Objective:** Convert `CallDriver` to return `DriverResult.Suspend` with inline var copying in `onFulfilled`.

**File:** `src/Zoh.Runtime/Verbs/Flow/CallDriver.cs`

```csharp
// Before (L97):
return VerbResult.Yield(new ContextContinuation(newCtx));

// After:
var childCtx = newCtx;
return new DriverResult.Suspend(new Continuation(
    new JoinContextRequest(newCtx.Id),  // Note: Context needs an Id field
    outcome => outcome switch
    {
        WaitCompleted c =>
        {
            // Inline: copy vars back from child to parent
            if (shouldInline)
            {
                foreach (var varName in inlineVarNames)
                {
                    var val = childCtx.Variables.Get(varName);
                    ctx.Variables.Set(varName, val);
                }
            }
            return DriverResult.Complete.Ok(c.Value);
        },
        WaitCancelled x => new DriverResult.Complete(
            ZohNothing.Instance,
            ImmutableArray.Create(new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message))),
        _ => DriverResult.Complete.Ok()
    }
));
```

**Note:** `CallDriver` currently uses `ContextContinuation(IExecutionContext ChildContext)`. The new model uses `JoinContextRequest(string ContextId)`. The `Context` class may need an `Id` property (a GUID or incrementing int). Check if this already exists — if not, add `public string Id { get; } = Guid.NewGuid().ToString();` to Context.

**Verification:** Inline var copying is in the closure. No inline logic in scheduler.

---

### Step 7: Update Blocking Drivers — Presentation Verbs

**Objective:** Convert Converse, Choose, ChooseFrom, Prompt to `DriverResult.Suspend`.

**Files:**
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs`
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs`
- `src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs`
- `src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs`

**Pattern (all four):**
```csharp
// Before:
return VerbResult.Yield(new HostContinuation("converse"));

// After:
return new DriverResult.Suspend(new Continuation(
    new HostRequest(),
    outcome => outcome switch
    {
        WaitCompleted c => DriverResult.Complete.Ok(c.Value),
        WaitTimedOut => DriverResult.Complete.Ok(),
        _ => DriverResult.Complete.Ok()
    }
));
```

For `ConverseDriver`: the `onFulfilled` always returns `Ok()` (dialog acknowledgment has no meaningful value).

For `ChooseDriver`/`ChooseFromDriver`/`PromptDriver`: `onFulfilled` passes through `c.Value` — the host provides the selected/typed value.

Also update all non-yielding return paths in these drivers from `VerbResult.Ok()` to `DriverResult.Complete.Ok()`.

**Verification:** All four compile. Each yields `HostRequest`. Each maps `WaitCompleted.Value` through.

---

### Step 8: Update SignalManager

**Objective:** Replace direct field mutation with `Resume()` call.

**File:** `src/Zoh.Runtime/Execution/SignalManager.cs`

```csharp
// Before (L96-100):
ctx.SetState(ContextState.Running);
ctx.LastResult = payload;
ctx.WaitCondition = null;

// After:
ctx.Resume(new WaitCompleted(payload), ctx.ResumeToken);
```

**Rationale:** This is the only place outside `Context` that directly mutates context state on unblock. It must go through `Resume()` so the `onFulfilled` callback is invoked.

**Verification:** No direct `SetState`/`LastResult`/`WaitCondition` mutation for unblocking in `SignalManager`.

---

### Step 9: Update StatementExecutor Delegate Type

**Objective:** The `StatementExecutor` delegate on `Context` returns `VerbResult` — update to `DriverResult`.

**File:** `src/Zoh.Runtime/Execution/Context.cs`

```csharp
// Before (L28):
public Func<IExecutionContext, VerbCallAst, VerbResult>? StatementExecutor { get; set; }

// After:
public Func<IExecutionContext, VerbCallAst, DriverResult>? StatementExecutor { get; set; }
```

Also update `VerbExecutor` delegate and `ExecuteVerb` method if they also return `VerbResult` → `DriverResult`:

```csharp
// Before (L27):
public Func<ValueAst, IExecutionContext, VerbResult>? VerbExecutor { get; set; }

// After:
public Func<ValueAst, IExecutionContext, DriverResult>? VerbExecutor { get; set; }
```

And `IExecutionContext.ExecuteVerb` return type.

**Verification:** All delegate types use `DriverResult`.

---

### Step 10: Delete Old Type Files

**Objective:** Remove `VerbContinuation.cs` and `VerbResult.cs` after all references are migrated.

**Files:**
- Delete `src/Zoh.Runtime/Verbs/VerbContinuation.cs`
- Delete `src/Zoh.Runtime/Verbs/VerbResult.cs`

**Verification:** `dotnet build` succeeds with no references to `VerbResult` or `VerbContinuation`.

---

### Step 11: Fix Tests

**Objective:** Update test files that reference `VerbResult`, `VerbContinuation`, `Resume()`.

**Files:** All test files under `csharp/tests/Zoh.Tests/` that:
- Create `VerbResult.Ok()` → `DriverResult.Complete.Ok()`
- Create `VerbResult.Yield(new HostContinuation(...))` → `new DriverResult.Suspend(new Continuation(new HostRequest(), ...))`
- Call `context.Resume(value)` → still works via backward-compatible overload
- Assert on `result.Continuation` → assert on `result is DriverResult.Suspend s` and `s.Continuation.Request`

**Verification:** `dotnet test` passes.

---

## Verification Plan

### Automated Checks
- [ ] `dotnet build` succeeds with zero warnings related to old types
- [ ] `dotnet test` passes all existing tests

### Manual Verification
- [ ] No references to `VerbResult`, `VerbContinuation`, `ExecutionResult` remain in source
- [ ] `SignalManager.Broadcast` calls `Resume()`, not direct field mutation
- [ ] `Context.Resume()` has token guard
- [ ] All 7 blocking drivers use `DriverResult.Suspend` with `onFulfilled` closures

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| IP advancement | `ApplyResult()` — `InstructionPointer++` only in `Complete` case | No IP change in `Suspend` case |
| Post-resume logic in drivers | Grep for `OnFulfilled` in driver files | All 7 blocking drivers have closures |
| Token guard | Read `Resume(WaitOutcome, int)` | Returns early if `token != ResumeToken` |
| Backward compat | `Resume(ZohValue?)` overload exists | Tests using old signature still compile |

---

## Rollback Plan

1. Git revert the implementation commit(s)
2. No external dependencies affected

---

## Notes

### Assumptions
- `Context` does not currently have an `Id` property — one will be added (GUID string)
- `PullVerbDriver` stays non-blocking (not converting to yield in this plan)
- Existing tests use the `Resume(ZohValue?)` overload which is preserved
- `ZohRuntime.Run()`/`RunToCompletion()` currently just calls `ctx.Run()` — no scheduler changes needed in this plan

### Risks
- **Large surface area** — 16 files changing simultaneously. Mitigation: compile after each step, fix errors incrementally.
- **Closure captures in drivers** — must ensure captured `ctx` reference stays valid. Low risk since contexts aren't disposed while blocked.
- **`CallDriver` needs Context.Id** — must verify no collision with existing fields.

### Open Questions
- [ ] Should `DriverResult` nested records (`Complete`, `Suspend`) be top-level or nested? Nested is cleaner for C# pattern matching but slightly more verbose to construct.
