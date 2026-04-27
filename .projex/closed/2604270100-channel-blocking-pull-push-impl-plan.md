# Plan: Blocking `/pull` + Rendezvous `/push` (m6.2)

> **Status:** Complete
> **Completed:** 2026-04-28
> **Walkthrough:** 2604281500-channel-blocking-pull-push-impl-walkthrough.md
> **Created:** 2026-04-27
> **Author:** Antigravity (orchestrate-projex subagent)
> **Source:** Direct request — orchestrate-projex against m6.2 of `20260223-csharp-spec-audit-nav.md`
> **Nav:** 20260223-csharp-spec-audit-nav.md
> **Related Projex:** 20260207-csharp-runtime-nav.md | 2603261730-csharp-closed-archive.md (Phase 5 precedents: two-phase continuation, `WaitDriver` timeout wiring, `SignalManager.Broadcast` resume) | 20260315-channel-semantics-try-suspension-plan.md (closed spec plan defining rendezvous + Suspend wrapping) | 20260211-channel-inbox-outbox-spec-plan.md (closed; hub redesign — out of scope here) | 2604270124-channel-blocking-pull-push-redteam.md (adversarial review; conditions below folded into Steps 7a–7b + Notes)
> **Worktree:** Yes

---

## Summary

Make `/pull` block until value is available; make `/push` (default `wait: true`) block until consumed (rendezvous). Spec at `2_verbs.md` §1174–1228 mandates both. Drivers currently parse `timeout` but discard it for `> 0`; pull returns `Ok(Nothing)` on empty; push fires-and-forgets unconditionally.

Wire blocking through the existing two-phase continuation machinery (`DriverResult.Suspend` → `Continuation` → `WaitOutcome`) — the same pattern Phase 5 used for `/wait` and `/sleep`. Activate the already-present-but-inert `ContextState.WaitingChannel` + `ChannelWaitCondition` + `ResolveWait` channel branch. Extend `ChannelManager` with rendezvous primitives modeled on `SignalManager.Broadcast` (token-guarded `Resume` of waiter contexts).

**Scope:** csharp/ runtime — `WaitRequest`, `ChannelManager`, `ChannelVerbs.cs` (Push/Pull only), `Context.BlockOnRequest`, `Context.Terminate`, `ZohRuntime.ResolveWait`, channel tests.
**Estimated Changes:** 6 source files | 2 test files | 4 driver/manager methods rewritten | 2 new `WaitRequest` records.

---

## Objective

### Problem / Gap / Need

Per nav m6.2 + `spec/2_verbs.md` Core.Channel.Push/Pull:

- **GAP-1 (pull non-blocking):** `PullVerbDriver` calls `TryPull` once; on `Empty` returns `Ok(Nothing)`. Inline comment: "Blocking requires async redesign." Spec: pull "takes the first variable from a channel, **or wait until a variable is available**".
- **GAP-2 (push not rendezvous):** `PushVerbDriver` calls `TryPush` and returns `Ok` immediately. Spec: default `wait: true` "blocks until the value is consumed by a puller"; only `wait: false` opts out.
- **GAP-3 (timeout unused for `> 0`):** Both drivers parse `timeoutMs` but never consume it; `<= 0` short-circuits to `info "timeout"`. Once blocking exists, `timeoutMs` must travel through `Suspend → ChannelWaitCondition` so `ZohRuntime.ResolveWait` can fire `WaitTimedOut`.

### Success Criteria

- `/pull` on empty open channel suspends (`State == WaitingChannel`); a subsequent `/push` from another context resumes the puller with the pushed value.
- `/push` (default `wait: true`) on a channel with no waiting puller suspends until a `/pull` consumes the value; on consumption pusher resumes `Ok(Nothing)`.
- `/push wait: false` retains fire-and-forget — value buffered (or delivered to a waiting puller fast-path), pusher returns `Ok` immediately.
- `/push` fast-path: if a puller is already waiting, value delivered directly to that puller; pusher returns `Ok` without suspending.
- `/pull` fast-path: if a value is already buffered, pull dequeues and returns immediately — even with `timeout: 0`.
- `timeout: <= 0` → `info "timeout"` only when fast-path fails (no buffered value / no waiting puller / no wait-false-bypass).
- `timeout: > 0` → suspends; `Tick` advancing past `StartTimeMs + TimeoutMs` resolves with `info "timeout"` and waiter is removed from manager state.
- `/close` on a channel with blocked pullers and/or blocked pushers wakes them with `error "closed"`.
- Context that terminates while blocked on a channel (e.g. fatal during another statement is impossible while blocked, but Terminate via `/exit`-of-host or test harness) leaves no waiter entry behind.
- FIFO across mixed `wait: false` (buffered) and `wait: true` (rendezvous) pushes preserved: pulls receive values in push order.
- Existing `ChannelManagerTests` (9) and `ChannelVerbTimeoutTests` (6) updated where rendezvous changes their fixture — full csharp test suite green.

### Out of Scope

- **m6.3 broader timeout audit** — cross-verb timeout consistency, type-coercion of `timeout` parameter beyond `ZohInt`/`ZohFloat`/`ZohNothing`, additional spec edge-cases on other verbs. m6.2 only wires `timeoutMs` into the new channel `Suspend` path because the blocking path mandates it; m6.3 owns wider coverage.
- **Hub / outbox / inbox architecture** from `impl/08_concurrency.md` (and closed `20260211-channel-inbox-outbox-spec-plan.md`). Single-buffer-per-channel preserved. Hub redesign is a separate proposal/plan.
- **Multi-threaded host `Tick` safety.** `Tick` is single-threaded today; per-channel `lock` inside `ChannelManager` covers in-runtime contention only.
- `**wait` parameter strict-typing diagnostic.** Spec says "Accept boolean". Plan: silently default to `true` for non-bool. A strict `invalid_type` for non-bool `wait:` is deferred — flag in Open Questions.
- `**/close` "already closed" diagnostic upgrade.** Spec lists `error "closed"` for already-closed `/close`. Current `CloseVerbDriver` emits `error "not_found"` instead. Out of scope for m6.2 (Channel IO); belongs in m6.1 cleanup.

---

## Context

### Current State

**ChannelVerbs.cs** (`csharp/src/Zoh.Runtime/Verbs/Channel/`):

- `PushVerbDriver`: parses `channel`, `value`, `timeout` (sec → ms). `timeout <= 0` → `info "timeout"` short-circuit. `timeout > 0` → `timeoutMs` parsed but never used. Calls `ChannelManager.TryPush(name, value)`. No `wait` named-param at all.
- `PullVerbDriver`: same `timeout` parse pattern. Calls `TryPull(name, gen)`; `PullStatus.Empty → Ok(Nothing)`. Comment: "For now: non-blocking pull. Blocking requires async redesign."

**ChannelManager.cs** (`csharp/src/Zoh.Runtime/Execution/`):

- One channel = `ConcurrentQueue<ZohValue>` + `Generation` + `IsClosed`.
- API surface: `Open` | `Exists` | `GetGeneration` | `TryPush` | `TryPull(name, gen) → PullResult{Status, Value}` | `Count` | `TryClose`.
- No waiter tracking. No rendezvous primitives. `TryClose` simply flips `IsClosed = true` — does not wake anything.

**Continuation/wait scaffolding (already complete from Phase 5):**

- `WaitRequest.cs`: `SleepRequest | SignalRequest | JoinContextRequest | HostRequest`. **No channel requests yet.**
- `WaitOutcome.cs`: `WaitCompleted(ZohValue) | WaitTimedOut | WaitCancelled(Code, Message)`.
- `Continuation(WaitRequest, Func<WaitOutcome, DriverResult>)`.
- `DriverResult.Complete | DriverResult.Suspend(Continuation, Diagnostics)`.
- `Context.BlockOnRequest` switch: `Sleep | Signal | Join | Host` — no channel cases (default throws).
- `Context.Resume(WaitOutcome, token)` token-guarded; idempotent on stale tokens.
- `ContextState.WaitingChannel` enum value — defined, never set.
- `ChannelWaitCondition(ChannelName, StartTimeMs, TimeoutMs?)` — defined with `IsTimedOut(elapsedMs)`, never constructed.
- `ZohRuntime.ResolveWait` `case WaitingChannel:` already returns `WaitTimedOut` when `chan.IsTimedOut(_elapsedMs)`. Comment: "Value delivery handled by PushDriver fast path." — but no PushDriver fast path exists.
- `Context.Terminate` already calls `SignalManager.UnsubscribeContext(this)` — analogous channel cleanup hook is the precedent for what this plan adds.

**Precedent (Phase 5 — closed):** `WaitDriver` is the canonical shape for blocking-with-timeout. `SignalManager.Broadcast(name, payload)` is the canonical shape for fast-path waker delivery: locks, iterates waiters, calls `ctx.Resume(WaitCompleted(payload), ctx.ResumeToken)`, removes them. `ChannelManager` mirrors both patterns.

**Tests:**

- `ChannelManagerTests.cs` (9 tests): manager unit tests. All operate on the legacy `TryPush`/`TryPull` surface.
- `ChannelVerbTimeoutTests.cs` (6 tests): driver-level. Two — `Pull_TimeoutQuestion_ProceedsNormally` and `Push_TimeoutQuestion_ProceedsNormally` — use a single-context `runtime.Run(ctx)` with sequential `/push` then `/pull`. Under rendezvous default this single-context shape would deadlock on `/push`. Both need `wait: false` added to keep their original intent (timeout-parse-doesn't-trigger-anything) testable in one context.

### Key Files


| File                                                       | Role                           | Change Summary                                                                                                                                                                                                                                                                                                    |
| ---------------------------------------------------------- | ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/Zoh.Runtime/Verbs/WaitRequest.cs`                     | Wait-request record family     | Add `ChannelPullRequest(Name, Generation, TimeoutMs?)` + `ChannelPushRequest(Name, Generation, TimeoutMs?, Value)`                                                                                                                                                                                                |
| `src/Zoh.Runtime/Execution/ChannelManager.cs`              | Channel storage + dispatch     | Replace `ConcurrentQueue<ZohValue>` with FIFO of `BufferEntry(Value, Pusher?, Token)`; add `WaitingPullers` linked list; add `OfferPush` / `AttemptPull` / `RegisterWaitingPuller` / `RegisterWaitingPusher` / `CancelPuller` / `CancelPusher`; rewrite `TryClose` to drain waiters with `WaitCancelled "closed"` |
| `src/Zoh.Runtime/Verbs/Channel/ChannelVerbs.cs`            | Push/Pull drivers              | `PushVerbDriver`: parse `wait` (default true); fast-path via `OfferPush`; on `MustSuspend` → `Suspend(ChannelPushRequest)`. `PullVerbDriver`: `AttemptPull` fast-path; on `Empty` → `Suspend(ChannelPullRequest)`                                                                                                 |
| `src/Zoh.Runtime/Execution/Context.cs`                     | `BlockOnRequest` + `Terminate` | Add `case ChannelPullRequest` / `case ChannelPushRequest`. In `Terminate`: if `WaitCondition is ChannelWaitCondition`, call `ChannelManager.CancelPuller`/`CancelPusher` (mirror existing `SignalManager.UnsubscribeContext` pattern)                                                                             |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs`                  | `ResolveWait` cleanup          | On `WaitingChannel` timeout, call `Channels.CancelPuller(name, ctx)` and `Channels.CancelPusher(name, ctx)` before returning `WaitTimedOut` so dropped waiters don't linger                                                                                                                                       |
| `tests/Zoh.Tests/Execution/ChannelManagerTests.cs`         | Manager unit tests             | Add tests for `OfferPush` / `AttemptPull` / `RegisterWaiting`* / `Cancel`* / close-drains-waiters / FIFO mixed                                                                                                                                                                                                    |
| `tests/Zoh.Tests/Verbs/Channel/ChannelVerbTimeoutTests.cs` | Driver tests                   | Patch the two `_ProceedsNormally` tests to use `wait: false`; add tests for blocking pull, blocking push, fast-paths, positive-timeout firing, close-wakes-waiters, FIFO                                                                                                                                          |


### Dependencies

- **Requires:**
  - Two-phase continuation infra (`DriverResult` / `Continuation` / token-guarded `Resume`) — landed Phase 5, archived in `2603261730-csharp-closed-archive.md`.
  - `ChannelWaitCondition` + `WaitingChannel` enum + `ResolveWait` channel branch — present and inert.
  - `Tick`-based runtime API (`ZohRuntime.Tick(deltaMs)` + `StartContext` returning `ContextHandle`) — present.
- **Blocks:**
  - m6.3 broader channel-timeout audit (consumes the wiring this plan adds).
  - Phase 6.x hub/outbox/inbox redesign (would supersede manager-level rendezvous; out of scope).

### Constraints

- Single-threaded `ZohRuntime.Tick` assumption preserved. `ChannelManager` uses per-channel `lock` to guard buffer + waiting-puller list mutations. No claim of multi-threaded host safety.
- `IExecutionContext` (driver-facing interface) does not expose `ResumeToken`. Drivers cast to `Context` only when needed (matches `WaitDriver` / `SignalDriver` pattern). New manager methods that need a `Context` accept it directly.
- Diagnostic codes match spec verbatim: `not_found` | `closed` | `timeout` | `stale`. No new codes introduced.
- Public manager API preserved: `TryPush(name, value)` and `TryPull(name, gen)` retained as thin wrappers so existing `ChannelManagerTests` and any external callers continue to compile and behave (fire-and-forget push, non-blocking pull).
- Channel-name case-insensitivity preserved (existing `ToLowerInvariant` discipline).

### Assumptions

- `ZohRuntime.Tick` advances `_elapsedMs` once per call and visits each context once. Verified in `ZohRuntime.Tick`.
- `Context.Resume(outcome, token)` is the single resume entry point and is idempotent on stale tokens (`token != ResumeToken` early-returns). Verified in `Context.Resume`.
- A pusher's value travels in the `ChannelPushRequest` record (not enqueued during `OfferPush` — only enqueued in `RegisterWaitingPusher` after `ResumeToken` bump). Race-free under single-threaded Tick.
- `_channels.AddOrUpdate` in `Open` replaces a closed channel with a fresh `Channel` instance at a new generation. A waiter registering after an `Open`-while-closed → `Open`-fresh sequence lands on the new instance.
- Existing `Pull_TimeoutZero_ReturnsInfoDiagnosticImmediately` (empty channel, `timeout: 0`) and `Pull_TimeoutNegative_`* keep passing unchanged — empty buffer + `timeout <= 0` = immediate `info "timeout"` under the new flow.
- Existing `Push_TimeoutZero_`* and `Push_TimeoutNegative_`* keep passing — no waiting puller + `timeout <= 0` + default `wait: true` = immediate `info "timeout"`, value not buffered.

### Impact Analysis

- **Direct:** `WaitRequest.cs`, `ChannelManager.cs`, `ChannelVerbs.cs`, `Context.cs` (`BlockOnRequest` + `Terminate`), `ZohRuntime.cs` (`ResolveWait`), 2 channel test files.
- **Adjacent:**
  - `IExecutionContext` — no surface change; drivers cast to `Context` for new manager methods that need it.
  - `ContextState.WaitingChannel` — newly active state; any state-machine consumers will now observe it.
  - `ChannelWaitCondition` — newly constructed; debug/inspection code reading `WaitCondition` may surface this type.
- **Downstream:**
  - m6.3 nav entry — wiring done here directly satisfies the m6.3 GAP for `/push` and `/pull` `timeout > 0`. Nav update during close-projex.
  - `OpenVerbDriver` / `CloseVerbDriver` — unchanged in shape. `TryClose` semantics expand (now wakes waiters) but its bool return contract is preserved.
  - Other test suites (Concurrency, Navigation, etc.) do not exercise channels — no expected regressions; verify via full-suite run.

---

## Implementation

### Overview

1. Add two `WaitRequest` records.
2. Rebuild `ChannelManager` storage: per-channel `LinkedList<BufferEntry>` (where `BufferEntry.Pusher` is non-null for blocking pushes) + `LinkedList<WaitingPuller>`; per-channel `lock`. Add atomic `OfferPush` / `AttemptPull` / `Register`* / `Cancel`* methods. Rewrite `TryClose` to drain waiters via `Resume(WaitCancelled("closed", ...), token)`. Keep `TryPush` / `TryPull` wrappers for backward compat.
3. Wire `ZohRuntime.ResolveWait` `WaitingChannel` timeout cleanup.
4. Rewrite `PushVerbDriver` and `PullVerbDriver` against the new manager API + `Suspend` for blocking paths.
5. Add `ChannelPullRequest` / `ChannelPushRequest` cases to `Context.BlockOnRequest` (sets `WaitCondition` + state, then `RegisterWaitingPuller` / `RegisterWaitingPusher`).
6. Add channel-cleanup hook to `Context.Terminate` mirroring `SignalManager.UnsubscribeContext`.
7. Update existing tests + add new coverage (mandatory **7a before 7b** — see Steps 7a–7b).

Implementation order: Step 1 → Step 2 (Steps 1+2 compile standalone) → Steps 3–6 together (drivers/context block; cross-references) → Step 7a (patch-only + test checkpoint) → Step 7b (new tests + full suite).

---

### Step 1: Add `ChannelPullRequest` + `ChannelPushRequest`

**Objective:** Type-level expression of channel-blocking suspends, mirroring `SignalRequest`.
**Confidence:** High
**Depends on:** None.

**Files:** `src/Zoh.Runtime/Verbs/WaitRequest.cs`

**Changes:**

```csharp
// Before:
using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs;

public abstract record WaitRequest;
public sealed record SleepRequest(double DurationMs) : WaitRequest;
public sealed record SignalRequest(string MessageName, double? TimeoutMs = null) : WaitRequest;
public sealed record JoinContextRequest(ContextHandle Handle) : WaitRequest;
public sealed record HostRequest(double? TimeoutMs = null) : WaitRequest;

// After:
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs;

public abstract record WaitRequest;
public sealed record SleepRequest(double DurationMs) : WaitRequest;
public sealed record SignalRequest(string MessageName, double? TimeoutMs = null) : WaitRequest;
public sealed record JoinContextRequest(ContextHandle Handle) : WaitRequest;
public sealed record HostRequest(double? TimeoutMs = null) : WaitRequest;
public sealed record ChannelPullRequest(string ChannelName, int Generation, double? TimeoutMs = null) : WaitRequest;
public sealed record ChannelPushRequest(string ChannelName, int Generation, double? TimeoutMs, ZohValue Value) : WaitRequest;
```

**Rationale:** Captures everything `BlockOnRequest` needs to register the waiter — channel name (manager lookup), generation (stale-channel detection), timeout (for `ChannelWaitCondition`), pushed value (push only). Mirrors `SignalRequest`'s arity and shape.

**Verification:** Compile-only check after this step. Pattern-match exhaustiveness in `BlockOnRequest` will fall into `default → throw` until Step 5 wires the new cases — that's expected interim state.

**If this fails:** Revert single file. Nothing depends on it yet.

---

### Step 2: Rebuild `ChannelManager` for rendezvous + waiter tracking

**Objective:** Move from `ConcurrentQueue<ZohValue>` to a unified buffer that holds both fire-and-forget values and blocking-pusher entries; add a separate waiting-puller list; add atomic `OfferPush` / `AttemptPull` / `Register`* / `Cancel`* operations; rewrite `TryClose` to wake waiters.

**Confidence:** Medium — most invasive single change; redesign of internal storage with new locking discipline.
**Depends on:** None (compiles independent of Step 1).

**Files:** `src/Zoh.Runtime/Execution/ChannelManager.cs`

**Changes (full new shape; legacy `PullResult`/`PullStatus` retained):**

```csharp
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Execution;

public class ChannelManager
{
    private sealed class BufferEntry
    {
        public ZohValue Value { get; init; } = ZohNothing.Instance;
        public Context? Pusher { get; init; }   // null = fire-and-forget
        public int PusherToken { get; init; }
    }

    private sealed class WaitingPuller
    {
        public Context Ctx { get; init; } = null!;
        public int Token { get; init; }
    }

    private sealed class Channel
    {
        public int Generation { get; init; }
        public bool IsClosed { get; set; }
        public LinkedList<BufferEntry> Buffer { get; } = new();
        public LinkedList<WaitingPuller> Pullers { get; } = new();
        public object Lock { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Channel> _channels = new();
    private int _generationCounter;

    public int Open(string name) { /* unchanged shape: AddOrUpdate; recreate on closed; bumps generation */ }
    public bool Exists(string name) { /* unchanged */ }
    public int GetGeneration(string name) { /* unchanged */ }

    public int Count(string name)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch) || ch.IsClosed) return 0;
        lock (ch.Lock) return ch.Buffer.Count;
    }

    // -- New atomic API --

    public OfferPushResult OfferPush(string name, int expectedGeneration, ZohValue value, bool wait, Context pusher)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return OfferPushResult.NotFound;
        lock (ch.Lock)
        {
            if (ch.IsClosed) return OfferPushResult.Closed;
            if (ch.Generation != expectedGeneration) return OfferPushResult.Closed; // post-recreate

            if (ch.Pullers.First is { } pullerNode)
            {
                var waiter = pullerNode.Value;
                ch.Pullers.RemoveFirst();
                // Resume under lock is safe: it mutates only the puller's Context, not channel state,
                // and Context.Resume is idempotent on stale tokens.
                waiter.Ctx.Resume(new WaitCompleted(value), waiter.Token);
                return OfferPushResult.Delivered;
            }

            if (!wait)
            {
                ch.Buffer.AddLast(new BufferEntry { Value = value });
                return OfferPushResult.Buffered;
            }

            return OfferPushResult.MustSuspend;
        }
    }

    public AttemptPullResult AttemptPull(string name, int expectedGeneration)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return AttemptPullResult.NotFound();
        lock (ch.Lock)
        {
            if (ch.IsClosed) return AttemptPullResult.Closed();
            if (ch.Generation != expectedGeneration) return AttemptPullResult.Stale();

            if (ch.Buffer.First is { } node)
            {
                var entry = node.Value;
                ch.Buffer.RemoveFirst();
                if (entry.Pusher is not null)
                    entry.Pusher.Resume(new WaitCompleted(ZohNothing.Instance), entry.PusherToken);
                return AttemptPullResult.Delivered(entry.Value);
            }

            return AttemptPullResult.Empty();
        }
    }

    /// <summary>Called from Context.BlockOnRequest after ResumeToken bump for blocking push.</summary>
    public void RegisterWaitingPusher(string name, ZohValue value, Context pusher, int pusherToken)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return;
        lock (ch.Lock)
        {
            if (ch.IsClosed)
            {
                pusher.Resume(new WaitCancelled("closed", $"Channel closed: {name}"), pusherToken);
                return;
            }
            ch.Buffer.AddLast(new BufferEntry { Value = value, Pusher = pusher, PusherToken = pusherToken });
        }
    }

    /// <summary>Called from Context.BlockOnRequest after ResumeToken bump for blocking pull.</summary>
    public void RegisterWaitingPuller(string name, Context puller, int pullerToken)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return;
        lock (ch.Lock)
        {
            if (ch.IsClosed)
            {
                puller.Resume(new WaitCancelled("closed", $"Channel closed: {name}"), pullerToken);
                return;
            }
            ch.Pullers.AddLast(new WaitingPuller { Ctx = puller, Token = pullerToken });
        }
    }

    /// <summary>Remove this context from the waiting-puller list (timeout / terminate cleanup).</summary>
    public void CancelPuller(string name, Context puller)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return;
        lock (ch.Lock)
        {
            for (var node = ch.Pullers.First; node != null; node = node.Next)
                if (ReferenceEquals(node.Value.Ctx, puller)) { ch.Pullers.Remove(node); return; }
        }
    }

    /// <summary>Remove this context's pending push entry (timeout / terminate cleanup). Drops the value.</summary>
    public void CancelPusher(string name, Context pusher)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return;
        lock (ch.Lock)
        {
            for (var node = ch.Buffer.First; node != null; node = node.Next)
                if (ReferenceEquals(node.Value.Pusher, pusher)) { ch.Buffer.Remove(node); return; }
        }
    }

    public bool TryClose(string name)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return false;
        lock (ch.Lock)
        {
            if (ch.IsClosed) return false;
            ch.IsClosed = true;

            foreach (var w in ch.Pullers)
                w.Ctx.Resume(new WaitCancelled("closed", $"Channel closed: {name}"), w.Token);
            ch.Pullers.Clear();

            foreach (var e in ch.Buffer)
                if (e.Pusher is not null)
                    e.Pusher.Resume(new WaitCancelled("closed", $"Channel closed: {name}"), e.PusherToken);
            ch.Buffer.Clear();
        }
        return true;
    }

    // -- Backward-compat thin wrappers (kept for ChannelManagerTests + any external embedders) --

    public bool TryPush(string name, ZohValue value)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return false;
        lock (ch.Lock)
        {
            if (ch.IsClosed) return false;
            if (ch.Pullers.First is { } pullerNode)
            {
                var waiter = pullerNode.Value;
                ch.Pullers.RemoveFirst();
                waiter.Ctx.Resume(new WaitCompleted(value), waiter.Token);
                return true;
            }
            ch.Buffer.AddLast(new BufferEntry { Value = value });
            return true;
        }
    }

    public PullResult TryPull(string name, int expectedGeneration)
    {
        var r = AttemptPull(name, expectedGeneration);
        return r.Status switch
        {
            AttemptPullStatus.NotFound => PullResult.NotFound,
            AttemptPullStatus.Closed => PullResult.Closed,
            AttemptPullStatus.Stale => PullResult.GenerationMismatch,
            AttemptPullStatus.Delivered => PullResult.Success(r.Value!),
            AttemptPullStatus.Empty => PullResult.Empty,
            _ => PullResult.NotFound
        };
    }
}

public enum OfferPushResult { NotFound, Closed, Delivered, Buffered, MustSuspend }

public enum AttemptPullStatus { NotFound, Closed, Stale, Delivered, Empty }
public readonly struct AttemptPullResult
{
    public AttemptPullStatus Status { get; init; }
    public ZohValue? Value { get; init; }
    public static AttemptPullResult NotFound() => new() { Status = AttemptPullStatus.NotFound };
    public static AttemptPullResult Closed() => new() { Status = AttemptPullStatus.Closed };
    public static AttemptPullResult Stale() => new() { Status = AttemptPullStatus.Stale };
    public static AttemptPullResult Delivered(ZohValue v) => new() { Status = AttemptPullStatus.Delivered, Value = v };
    public static AttemptPullResult Empty() => new() { Status = AttemptPullStatus.Empty };
}

// Existing PullResult / PullStatus retained as-is.
```

**Rationale:**

- Single FIFO `Buffer` preserves push order across `wait: false` and `wait: true` pushes — pulls always dequeue head, naturally FIFO.
- Wakers (push fast-path on a waiting puller, pull fast-path on a buffered entry, close on all waiters) are responsible for calling `Resume` with the stored token. Token check inside `Context.Resume` makes this idempotent against stale waiters (e.g., a puller that already timed out before the matching push arrives).
- Per-channel `lock` keeps `Buffer + Pullers + IsClosed` consistent. Cross-channel ops remain parallel via `ConcurrentDictionary`.
- `Register`* is split from the corresponding `Offer/Attempt` fast-path because it must run AFTER `Context.ResumeToken` bumps in `ApplyResult` (Step 5 invariant). Race against close: `Register`* takes the same per-channel lock — if `IsClosed` between Offer and Register, immediate `Resume(WaitCancelled "closed", token)` rather than enqueuing a stuck waiter.
- Backward-compat `TryPush(name, value)` keeps `ChannelManagerTests.TryPush_`* invariants. It opportunistically fast-paths to a waiting puller so legacy callers don't strand them either.

**Verification:**

- `dotnet build csharp/Zoh.sln` (or equivalent) — compile clean.
- `ChannelManagerTests` original tests still pass under the new `TryPush` / `TryPull` wrappers — their assertions don't touch `BufferEntry`/`WaitingPuller` shape.
- Step 7b adds dedicated tests for the new methods.

**If this fails:** Revert `ChannelManager.cs`. Steps 4–6 reference the new API; if Step 2 reverts, Steps 4–6 must also revert.

---

### Step 3: Wire `ZohRuntime.ResolveWait` cleanup for channel timeout

**Objective:** When a context blocked on a channel times out, remove its puller/pusher entry from `ChannelManager` before resuming with `WaitTimedOut`. Otherwise a future push could deliver to a now-stale puller (no-op via token check, but value is lost from buffer), or a pusher's value sits forever in the buffer with no one resuming the pusher.

**Confidence:** High
**Depends on:** Step 2 (`CancelPuller` / `CancelPusher`).

**Files:** `src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes (`ResolveWait`, `WaitingChannel` branch):**

```csharp
// Before:
case ContextState.WaitingChannel:
    // Value delivery handled by PushDriver fast path.
    // Scheduler only handles timeout.
    if (ctx.WaitCondition is ChannelWaitCondition chan && chan.IsTimedOut(_elapsedMs))
        return new WaitTimedOut();
    return null;

// After:
case ContextState.WaitingChannel:
    if (ctx.WaitCondition is ChannelWaitCondition chan && chan.IsTimedOut(_elapsedMs))
    {
        // ChannelWaitCondition does not encode whether ctx is on the puller or pusher list.
        // Both Cancel methods are no-ops when ctx isn't on the respective list, so calling
        // both is safe and avoids adding a role enum to the condition record.
        Channels.CancelPuller(chan.ChannelName, ctx);
        Channels.CancelPusher(chan.ChannelName, ctx);
        return new WaitTimedOut();
    }
    return null;
```

**Rationale:** `ChannelWaitCondition` doesn't carry a "which side" flag. Calling both cancels keeps the condition record minimal and the cleanup idempotent. Alternative — extend `ChannelWaitCondition` with a `ChannelWaiterRole` enum — adds a field and switch to avoid one O(n) walk over a typically-short list. Defer until a profiler complains.

**Verification:** Driver test `Pull_TimeoutPositive_FiresOnTickAdvance` (Step 7b): pull with `timeout: 0.05` on empty channel; `Tick(60)`; assert `info "timeout"` AND a subsequent `wait: false` push to that same channel correctly buffers (proves the timed-out puller was evicted from `Pullers`).

**If this fails:** Revert this hunk; tests for timeout cleanup will fail loudly.

---

### Step 4: Rewrite `PushVerbDriver` and `PullVerbDriver`

**Objective:** Drivers parse → fast-path → `Suspend` for blocking. Pattern matches `WaitDriver` exactly.
**Confidence:** High
**Depends on:** Step 1 (request types), Step 2 (manager API).

**Files:** `src/Zoh.Runtime/Verbs/Channel/ChannelVerbs.cs`

**Changes — `PushVerbDriver`:**

```csharp
public class PushVerbDriver : IVerbDriver
{
    public string Namespace => "core.channel";
    public string Name => "push";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (context is not Context ctx)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Push requires a Context.", call.Start));

        if (call.UnnamedParams.Length < 2)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Expected channel and value parameters", call.Start));

        var channelValue = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        if (channelValue is not ZohChannel channel)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected channel, got: {channelValue.Type}", call.Start));

        var value = ValueResolver.Resolve(call.UnnamedParams[1], ctx);

        bool wait = true;
        double? timeoutMs = null;
        bool immediateTimeout = false;
        foreach (var p in call.NamedParams)
        {
            if (p.Key.Equals("wait", StringComparison.OrdinalIgnoreCase))
            {
                var v = ValueResolver.Resolve(p.Value, ctx);
                if (v is ZohBool b) wait = b.Value;
                // anything else: keep default true (lenient — see Out of Scope)
            }
            else if (p.Key.Equals("timeout", StringComparison.OrdinalIgnoreCase))
            {
                var v = ValueResolver.Resolve(p.Value, ctx);
                if (v is ZohFloat f) { if (f.Value <= 0) immediateTimeout = true; else timeoutMs = f.Value * 1000.0; }
                else if (v is ZohInt i) { if (i.Value <= 0) immediateTimeout = true; else timeoutMs = i.Value * 1000.0; }
                // ZohNothing (`?`) → no timeout (blocks indefinitely)
            }
        }

        // No `gen == 0` pre-check: GetGeneration returns 0 for both NotFound and Closed,
        // so a pre-check would conflate them. Let OfferPush distinguish via its enum below.
        var gen = ctx.ChannelManager.GetGeneration(channel.Name);
        var offer = ctx.ChannelManager.OfferPush(channel.Name, gen, value, wait, ctx);
        switch (offer)
        {
            case OfferPushResult.NotFound:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Channel does not exist: {channel.Name}", call.Start));
            case OfferPushResult.Closed:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "closed", $"Cannot push to closed channel: {channel.Name}", call.Start));
            case OfferPushResult.Delivered:
            case OfferPushResult.Buffered:
                return DriverResult.Complete.Ok(ZohValue.Nothing);
            case OfferPushResult.MustSuspend:
                if (immediateTimeout)
                    return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                        new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                return new DriverResult.Suspend(new Continuation(
                    new ChannelPushRequest(channel.Name, gen, timeoutMs, value),
                    outcome => outcome switch
                    {
                        WaitCompleted => DriverResult.Complete.Ok(ZohValue.Nothing),
                        WaitTimedOut => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start))),
                        WaitCancelled x => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start))),
                        _ => DriverResult.Complete.Ok(ZohValue.Nothing)
                    }));
            default:
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "internal", "Unknown OfferPushResult", call.Start));
        }
    }
}
```

**Changes — `PullVerbDriver`:**

```csharp
public class PullVerbDriver : IVerbDriver
{
    public string Namespace => "core.channel";
    public string Name => "pull";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (context is not Context ctx)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Pull requires a Context.", call.Start));

        if (call.UnnamedParams.Length < 1)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Expected channel parameter", call.Start));

        var channelValue = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        if (channelValue is not ZohChannel channel)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected channel, got: {channelValue.Type}", call.Start));

        double? timeoutMs = null;
        bool immediateTimeout = false;
        foreach (var p in call.NamedParams)
        {
            if (p.Key.Equals("timeout", StringComparison.OrdinalIgnoreCase))
            {
                var v = ValueResolver.Resolve(p.Value, ctx);
                if (v is ZohFloat f) { if (f.Value <= 0) immediateTimeout = true; else timeoutMs = f.Value * 1000.0; }
                else if (v is ZohInt i) { if (i.Value <= 0) immediateTimeout = true; else timeoutMs = i.Value * 1000.0; }
            }
        }

        // No `gen == 0` pre-check (see PushVerbDriver). AttemptPull's status enum
        // distinguishes NotFound vs Closed vs Stale. Side benefit: pull on a closed
        // channel now correctly emits `error "closed"` instead of the legacy `error "not_found"`.
        var gen = ctx.ChannelManager.GetGeneration(channel.Name);
        var attempt = ctx.ChannelManager.AttemptPull(channel.Name, gen);
        switch (attempt.Status)
        {
            case AttemptPullStatus.NotFound:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Channel does not exist: {channel.Name}", call.Start));
            case AttemptPullStatus.Closed:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "closed", $"Channel is closed: {channel.Name}", call.Start));
            case AttemptPullStatus.Stale:
                return DriverResult.Complete.Error(ZohValue.Nothing,
                    new Diagnostic(DiagnosticSeverity.Error, "stale", $"Channel was recreated: {channel.Name}", call.Start));
            case AttemptPullStatus.Delivered:
                return DriverResult.Complete.Ok(attempt.Value!);
            case AttemptPullStatus.Empty:
                if (immediateTimeout)
                    return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                        new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start)));
                return new DriverResult.Suspend(new Continuation(
                    new ChannelPullRequest(channel.Name, gen, timeoutMs),
                    outcome => outcome switch
                    {
                        WaitCompleted c => DriverResult.Complete.Ok(c.Value),
                        WaitTimedOut => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Info, "timeout", "The timeout was reached.", call.Start))),
                        WaitCancelled x => new DriverResult.Complete(ZohValue.Nothing, ImmutableArray.Create(
                            new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start))),
                        _ => DriverResult.Complete.Ok()
                    }));
            default:
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "internal", "Unknown AttemptPullStatus", call.Start));
        }
    }
}
```

**Rationale:**

- `immediateTimeout` is checked AFTER fast-path so `pull timeout: 0` on a non-empty channel returns the buffered value (spec compliance: pull blocks "until value or close or timeout"; `timeout: 0` "triggers immediate timeout" — a value already present is not "waiting", it returns directly).
- `wait: false` push with `timeout: 0`: per spec "Ignored when `wait` is `false`". Code path: `OfferPush(wait=false)` returns `Delivered` (waiting puller) or `Buffered` — both → `Ok` without consulting `immediateTimeout`. Spec-compliant.
- Push's `WaitCompleted` carries no value (push returns `Nothing`). Pull's `WaitCompleted` carries the delivered value.
- Removed the legacy `gen == 0` pre-check. `GetGeneration` collapses NotFound and Closed into the same return value (0); the pre-check therefore reported `not_found` for closed channels, contradicting spec which lists `error "closed"` for closed-channel push/pull. Letting `OfferPush`/`AttemptPull` return their typed status (NotFound | Closed | …) restores spec-correct diagnostics. Push behavior incidentally improves; pull's pre-existing closed→not_found regression is fixed as a byproduct (no test relies on the wrong code).

**Verification:** Build clean. Step 7b tests cover both drivers.

**If this fails:** Revert `ChannelVerbs.cs`. If Step 5 already landed, revert it too.

---

### Step 5: Wire `Context.BlockOnRequest` for channel requests

**Objective:** Register the suspended context with `ChannelManager` (as a waiting puller, or as a waiting-pusher buffer entry) AFTER `ResumeToken` is bumped, and set the appropriate `WaitCondition` + `WaitingChannel` state.

**Confidence:** High
**Depends on:** Step 1, Step 2.

**Files:** `src/Zoh.Runtime/Execution/Context.cs`

**Changes (insert two new cases before `default → throw`):**

```csharp
case ChannelPullRequest pullReq:
    WaitCondition = new ChannelWaitCondition(pullReq.ChannelName, elapsedMs, pullReq.TimeoutMs);
    SetState(ContextState.WaitingChannel);
    ChannelManager.RegisterWaitingPuller(pullReq.ChannelName, this, ResumeToken);
    break;

case ChannelPushRequest pushReq:
    WaitCondition = new ChannelWaitCondition(pushReq.ChannelName, elapsedMs, pushReq.TimeoutMs);
    SetState(ContextState.WaitingChannel);
    ChannelManager.RegisterWaitingPusher(pushReq.ChannelName, pushReq.Value, this, ResumeToken);
    break;
```

**Rationale:**

- Order: set `WaitCondition` + `SetState(WaitingChannel)` BEFORE `Register`*. If `Register`* finds the channel closed (race window between driver's `OfferPush`/`AttemptPull` and this register), it immediately calls `Resume(WaitCancelled "closed", token)`. `Resume` will flip `State → Running` and apply the continuation's `OnFulfilled` — which requires `State` to already have transitioned out of `Running` so the resume can re-enter it cleanly. The order above satisfies that.
- `ResumeToken` is post-bump (`ApplyResult` increments `ResumeToken` BEFORE calling `BlockOnRequest`) — exactly the token the manager must store and use when waking this context.

**Verification:** Build. Step 7b tests for blocking pull / blocking push exercise this path.

**If this fails:** Revert this hunk + Step 4 (drivers reference these cases via `Suspend`).

---

### Step 6: Channel cleanup on `Context.Terminate`

**Objective:** A context terminating while in `WaitingChannel` (e.g. host calls handle.Terminate, or a defer-driven flow) must not leave a stale puller/pusher entry in `ChannelManager`. Mirrors existing `SignalManager.UnsubscribeContext(this)` call already in `Terminate`.

**Confidence:** High
**Depends on:** Step 2.

**Files:** `src/Zoh.Runtime/Execution/Context.cs`

**Changes (`Terminate` — add channel cleanup before existing `SignalManager.UnsubscribeContext`):**

```csharp
// Before:
public void Terminate()
{
    if (State == ContextState.Terminated) return;

    ExecuteDefers(_storyDefers);
    ExecuteDefers(_contextDefers);

    StatementState = null;
    SignalManager.UnsubscribeContext(this);

    State = ContextState.Terminated;
}

// After:
public void Terminate()
{
    if (State == ContextState.Terminated) return;

    ExecuteDefers(_storyDefers);
    ExecuteDefers(_contextDefers);

    StatementState = null;

    if (WaitCondition is ChannelWaitCondition chan)
    {
        ChannelManager.CancelPuller(chan.ChannelName, this);
        ChannelManager.CancelPusher(chan.ChannelName, this);
    }
    SignalManager.UnsubscribeContext(this);

    State = ContextState.Terminated;
}
```

**Rationale:** Symmetric to `SignalManager.UnsubscribeContext`. Calling both `CancelPuller` and `CancelPusher` is idempotent — the role isn't tracked in `ChannelWaitCondition` but exactly one list (or neither) will hold this context.

**Verification:** Step 7b test `Terminate_RemovesChannelWaiter` — start a pulling context, terminate it before any push, then push: value buffers (because the now-removed puller is no longer there to grab it).

**If this fails:** Revert this hunk; cleanup is hygiene-only — no test besides the dedicated one fails without it. Lower-priority than Steps 1–5.

---

### Step 7a: Patch existing timeout tests (checkpoint — no hangs)

**Objective:** Apply **only** the two `_TimeoutQuestion_ProceedsNormally` patches (`wait:false` on `/push`) before any new blocking/rendezvous tests exist. Prevents CI/test-runner from hanging on unchanged single-context push→pull stories once Steps 2–6 land.

**Confidence:** High
**Depends on:** Steps 2, 4, 5, 6 (patches ship with implementation; run 7a gate before 7b additions).

**Files:**

- `tests/Zoh.Tests/Verbs/Channel/ChannelVerbTimeoutTests.cs` (patch section only — no new `[Fact]` yet)

**Changes — `ChannelVerbTimeoutTests.cs` patches:**

```csharp
// Before:
[Fact]
public void Pull_TimeoutQuestion_ProceedsNormally()
{
    var runtime = CreateRuntime();
    var story = runtime.LoadStory(@"
        @start
        /open <ch>;
        /push <ch>, 42;
        /pull <ch>, timeout:?;
    ");
    var ctx = runtime.CreateContext(story);
    runtime.Run(ctx);
    // asserts ...
}

// After (use wait:false so the single-context shape doesn't deadlock under rendezvous default):
[Fact]
public void Pull_TimeoutQuestion_ProceedsNormally()
{
    var runtime = CreateRuntime();
    var story = runtime.LoadStory(@"
        @start
        /open <ch>;
        /push <ch>, 42, wait:false;
        /pull <ch>, timeout:?;
    ");
    var ctx = runtime.CreateContext(story);
    runtime.Run(ctx);
    var internalCtx = (Context)ctx;
    Assert.Equal(ContextState.Terminated, ctx.State);
    Assert.DoesNotContain(internalCtx.LastDiagnostics, d => d.Code == "timeout");
    Assert.Equal(new ZohInt(42), internalCtx.LastResult);
}

// Push_TimeoutQuestion_ProceedsNormally — same patch: add wait:false to the /push.
```

**Verification (mandatory gate before Step 7b):**

- `dotnet test --filter FullyQualifiedName~ChannelVerbTimeoutTests` — must complete green. **Do not** add new tests from Step 7b until this passes; otherwise new tests may run in arbitrary order and mask a hang on the two unpatched tests (redteam: indefinite hang, not a failing assert).

**If this fails:** Fix patches first; do not proceed to 7b.

---

### Step 7b: New tests + manager coverage

**Objective:** Cover blocking pull, blocking push, fast-paths, positive timeout via `Tick`, close-wakes-waiters, FIFO mixed, terminate cleanup; extend `ChannelManagerTests`.

**Confidence:** High
**Depends on:** Step 7a complete and green.

**Files:**

- `tests/Zoh.Tests/Execution/ChannelManagerTests.cs` (extend)
- `tests/Zoh.Tests/Verbs/Channel/ChannelVerbTimeoutTests.cs` (additions only)

**Changes — `ChannelVerbTimeoutTests.cs` additions (each `[Fact]`):**


| Test                                           | Setup                                                                                                    | Assert                                                                                                                                                                                     |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Pull_OnEmpty_BlocksThenResumesViaPush`        | Story A: `@start /pull <ch>;`                                                                            | `StartContext(A)` + `Tick(0)`: `A.State == WaitingChannel`. `StartContext(B: /push <ch>, 7, wait:false)` + `Tick(0)`: `A.State == Terminated`, `A.LastResult == 7`.                        |
| `Push_BlockingDefault_BlocksUntilPullConsumes` | A: `/push <ch>, 9;` (default `wait:true`)                                                                | `StartContext(A)` + `Tick(0)`: `A.State == WaitingChannel`. `StartContext(B: /pull <ch>)` + `Tick(0)`: `A.State == Terminated`, `B.LastResult == 9`.                                       |
| `Push_FastPath_DeliversToWaitingPuller`        | A: `/pull <ch>;` first; B: `/push <ch>, 5;` second                                                       | After both `Tick`s: `A.State == Terminated`, `A.LastResult == 5`, `B.State == Terminated` and `B` was never `WaitingChannel` (assert via spying `B.State` after `B`'s `Tick`).             |
| `Pull_FastPath_OnBuffered`                     | `/push <ch>, 5, wait:false; /pull <ch>, timeout:0;`                                                      | `LastResult == 5`, no `timeout` diagnostic.                                                                                                                                                |
| `Pull_TimeoutPositive_FiresOnTickAdvance`      | `/pull <ch>, timeout:0.05;`                                                                              | `Tick(0)`: `WaitingChannel`. `Tick(60)`: `Terminated`, `info "timeout"` in `LastDiagnostics`. Then `wait:false` push: buffer count == 1 (proves puller list cleaned).                      |
| `Push_TimeoutPositive_FiresOnTickAdvance`      | `/push <ch>, 1, timeout:0.05;` (no puller)                                                               | `Tick(0)`: `WaitingChannel`. `Tick(60)`: `Terminated`, `info "timeout"`. Then `wait:false` push of `2`: buffer count == 1, pull returns `2` (proves the timed-out push entry was dropped). |
| `Close_WakesBlockedPuller_WithErrorClosed`     | A: `/pull <ch>;` (suspends). B: `/close <ch>;`                                                           | A resumes with `error "closed"` in `LastDiagnostics`.                                                                                                                                      |
| `Close_WakesBlockedPusher_WithErrorClosed`     | A: `/push <ch>, 1;` (no puller, suspends). B: `/close <ch>;`                                             | A resumes with `error "closed"`.                                                                                                                                                           |
| `FifoOrder_AcrossWaitFalseAndWaitTrue`         | A: `/push <ch>, 1, wait:false; /push <ch>, 2;` (second push blocks). B: `/pull <ch>;` then `/pull <ch>;` | First pull → 1. Second pull → 2. After second pull: A's blocking push resumed `Ok`, A `Terminated`.                                                                                        |


**Changes — `ChannelManagerTests.cs` additions (driver-free, fastest):**


| Test                                                         | Setup                                                                   | Assert                                                                                                                    |
| ------------------------------------------------------------ | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `OfferPush_NoPullerWaitTrue_ReturnsMustSuspend`              | `Open("c")`, `OfferPush("c", gen, v, wait:true, ctx)`                   | Returns `MustSuspend`; buffer count == 0 (not enqueued by Offer alone).                                                   |
| `OfferPush_NoPullerWaitFalse_ReturnsBuffered`                | same but `wait:false`                                                   | Returns `Buffered`; buffer count == 1.                                                                                    |
| `OfferPush_WaitingPuller_ReturnsDelivered`                   | Manually register a fake-context puller; offer                          | Returns `Delivered`; puller's `Resume` invoked with `WaitCompleted(value)`.                                               |
| `AttemptPull_Empty_ReturnsEmpty`                             | `Open("c"); AttemptPull`                                                | `Status == Empty`.                                                                                                        |
| `AttemptPull_Buffered_DeliversInPushOrder`                   | push 1, push 2, pull, pull                                              | Returns 1 then 2.                                                                                                         |
| `AttemptPull_BlockingPusher_ResumesPusher`                   | Register a waiting pusher; AttemptPull                                  | Pull returns the value; pusher's `Resume(WaitCompleted(Nothing), token)` invoked.                                         |
| `TryClose_DrainsWaitingPullers_WithErrorClosed`              | Register two pullers; `TryClose`                                        | Both resumed with `WaitCancelled("closed", ...)`. Channel `IsClosed`.                                                     |
| `TryClose_DrainsWaitingPushers_WithErrorClosed`              | Register two waiting pushers; `TryClose`                                | Both pushers resumed with `WaitCancelled("closed", ...)`.                                                                 |
| `CancelPuller_RemovesEntry_IdempotentOnAbsent`               | Register, Cancel, Cancel again                                          | Second cancel is no-op (no exception).                                                                                    |
| `Generation_OldGenAttemptPull_ReturnsStale`                  | open → push wait:false → close → reopen → AttemptPull(oldGen)           | `Status == Stale`.                                                                                                        |
| `OfferPush_StaleGeneration_ReturnsClosed`                    | open → close → reopen → `OfferPush` with **pre-reopen** generation      | Assert driver/manager behavior per chosen enum (`Closed` vs future `Stale`) — redteam flagged untested stale-gen on push. |
| `Terminate_StalePuller_FuturePushBuffersInsteadOfDelivering` | (in `ChannelVerbTimeoutTests` via `runtime` — could live there instead) | After cleanup, push to same channel buffers.                                                                              |


**Verification:** `dotnet test` on full `csharp/` test suite — green. Track final test-count delta in execution log.

**If this fails:** Iterate per test; the new manager methods are deterministic.

---

## Verification Plan

### Automated Checks

- `dotnet build csharp/Zoh.sln` — zero errors, zero new warnings.
- After Step 7a: `dotnet test --filter FullyQualifiedName~ChannelVerbTimeoutTests` — green before any Step 7b additions.
- After Step 7b: full `dotnet test` on csharp suite — all green.
- Targeted: `dotnet test --filter FullyQualifiedName~ChannelManagerTests`
- Targeted: `dotnet test --filter FullyQualifiedName~ChannelVerbTimeoutTests` (full file after 7b)
- Sanity: `dotnet test --filter FullyQualifiedName~ConcurrencyTests` (no channel use, but confirms continuation infra unbroken).

### Manual Verification

- Read final `ChannelVerbs.cs`: confirm fast-path / suspend / immediate-timeout branches map 1-to-1 with spec `core.channel.push` and `core.channel.pull`.
- Trace `Push_BlockingDefault_BlocksUntilPullConsumes` by hand: pusher Tick → driver `OfferPush → MustSuspend` → `BlockOnRequest` sets `WaitingChannel` + buffers entry with token; puller Tick → `AttemptPull` dequeues entry → resumes pusher with `WaitCompleted(Nothing)` → pusher's `OnFulfilled → Complete.Ok` → next Tick advances IP.
- Confirm no driver outside `ChannelVerbs.cs` references `OfferPush` / `AttemptPull` / `Register`* / `Cancel`* directly. Legacy `TryPush` / `TryPull` wrappers remain for `ChannelManagerTests` and external embedders.

### Acceptance Criteria Validation


| Criterion                                | How to Verify                                                | Expected                                                                        |
| ---------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------- |
| Pull blocks on empty                     | `Pull_OnEmpty_BlocksThenResumesViaPush`                      | Puller `WaitingChannel` then `Terminated` with pushed value                     |
| Push (default) blocks until consumed     | `Push_BlockingDefault_BlocksUntilPullConsumes`               | Pusher `WaitingChannel` then `Terminated` with `Nothing` after pull             |
| `wait:false` fire-and-forget             | `Pull_TimeoutQuestion_ProceedsNormally` (patched)            | No suspend; pull retrieves value in same context                                |
| Push fast-path                           | `Push_FastPath_DeliversToWaitingPuller`                      | Pusher never enters `WaitingChannel`                                            |
| Pull fast-path on buffered + `timeout:0` | `Pull_FastPath_OnBuffered`                                   | Returns value, no `info "timeout"`                                              |
| `timeout > 0` fires via Tick             | `Pull_TimeoutPositive_`*, `Push_TimeoutPositive_`*           | `info "timeout"` after Tick advance; cleanup proven by subsequent push behavior |
| `/close` wakes pullers                   | `Close_WakesBlockedPuller_WithErrorClosed`                   | `error "closed"`                                                                |
| `/close` wakes pushers                   | `Close_WakesBlockedPusher_WithErrorClosed`                   | `error "closed"`                                                                |
| FIFO mixed wait modes                    | `FifoOrder_AcrossWaitFalseAndWaitTrue`                       | Pulls in push order; blocking pusher resumes Ok after consume                   |
| Terminate cleanup                        | `Terminate_StalePuller_FuturePushBuffersInsteadOfDelivering` | Subsequent push buffers, not delivered to dead context                          |


---

## Rollback Plan

Per-step rollback documented above. For full rollback:

1. Squash-merged execution lands as a single SHA — `git revert <sha>` undoes everything.
2. Pre-merge: `projex-abandon` removes the worktree; main branch unaffected (worktree mode keeps base branch clean).
3. Public surface preserved (`TryPush(name, value)`, `TryPull(name, gen)`, `OpenVerbDriver`/`CloseVerbDriver` shapes unchanged) — no downstream consumer affected by rollback.

---

## Notes

### Risks

- **Single-context deadlock on misuse.** `/push` (default `wait: true`) without a separate puller hangs forever. Spec-compliant but a footgun. Mitigation: documentation + `wait: false` for single-context buffered scenarios. The two patched tests in Step 7a demonstrate the idiom.
- **Token-staleness leak.** If a manager method `Resume`s with a stored token AFTER the context resumed via another path (e.g., close + concurrent push race), token mismatch makes `Resume` a no-op — buffer entry was already removed by close, so no leak. Per-channel `lock` serializes all resume-issuing operations (`OfferPush` fast-path | `AttemptPull` | `TryClose` | `Register`* self-cancel), so the dangerous interleaving is impossible.
- **ConcurrentDictionary** + per-channel `lock` interaction. `_channels.TryGetValue` is lock-free; subsequent ops under `ch.Lock` are serialized. `_channels` is only mutated via `Open` (`AddOrUpdate`); channels are never removed. Safe.
- **Generation drift between OfferPush and RegisterWaitingPusher.** If a channel is closed-then-reopened in that window, `RegisterWaitingPusher` lookup-by-name lands on the new instance (which is open). The pusher then waits on the new channel — auto-rebind. See Open Questions; defer until concrete failure mode shows up.

### Post-redteam execution notes (`2604270124-channel-blocking-pull-push-redteam.md`)

- `**Resume` under `ch.Lock`:** `OfferPush` / `AttemptPull` / `TryClose` (and register self-cancel paths) call `ctx.Resume` while holding the per-channel lock. Continuation handlers for channel wake outcomes **must** return `Complete` only — if any path returned `Suspend` → `BlockOnRequest` → re-enter `Register`* → same lock → deadlock. Add brief comments at those call sites documenting the invariant; optional follow-up: collect wake list inside lock, `Resume` after release (redteam Should Fix).
- `**Count` semantics:** After Step 2, `Count` may include blocking-pusher buffer entries — document on `Count` XML summary + add `ChannelManagerTests` case if redteam matrix is adopted.
- **Terminate vs defers:** Redteam flagged ordering (`ExecuteDefers` before channel cancel). Validate during execution; if a repro exists, cancel channel waiters before defers or as first cleanup after guard.

### Open Questions

- **Should non-bool `wait:` emit a diagnostic?** Spec says "Accept boolean". Plan defaults to `true` silently. Redteam suggests non-fatal `info "invalid_type"` (or strict `invalid_type`) so hangs are easier to debug — decide at execution.
- **Auto-rebind on register-after-recreate.** When `OfferPush → MustSuspend`, then channel is `Close → Open`-recreated, then `RegisterWaitingPusher` runs: pusher lands on the new channel as a waiter on the new generation. Plan accepts this. Alternative: capture generation in the request and `WaitCancelled "stale"` if mismatched. Decide during redteam.
- **m6.3 boundary: should `ChannelWaiterRole` enum live on `ChannelWaitCondition`?** Avoids the dual `CancelPuller`/`CancelPusher` calls. Plan defers — adds a field/switch to save one short-list walk. Promote to m6.3 if profiling or diagnostics requirements emerge.

