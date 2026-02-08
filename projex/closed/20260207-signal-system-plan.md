# Plan: Implement Signal System

> **Status:** Complete
> **Created:** 2026-02-07
> **Completed:** 2026-02-08
> **Author:** Antigravity
> **Source:** Direct request (Phase 3 of C# Runtime Roadmap)
> **Related Projex:** [20260207-csharp-runtime-nav.md](../projex/20260207-csharp-runtime-nav.md)
> **Walkthrough:** [20260207-signal-system-walkthrough.md](20260207-signal-system-walkthrough.md)
> **Reviewed:** 2026-02-08 - [20260208-signal-system-plan-review.md](20260208-signal-system-plan-review.md)
> **Review Outcome:** Needs Modification (Cleanup logic, Runtime integration, ContextState correction)

---

## Summary

This plan implements the Signal System, a broadcasting mechanism for cross-context synchronization. It introduces `SignalManager` to handle subscriptions and broadcasting, and implements the `/wait` and `/signal` verbs.

**Scope:** `Zoh.Runtime` project, `Execution` and `Verbs` namespaces.
**Estimated Changes:** ~6 files (`SignalManager`, `ZohRuntime`, `Context`, `VerbRegistry`, verbs).

---

## Objective

### Problem / Gap / Need
ZOH requires a mechanism for one-to-many communication, allowing a context to wake up multiple waiting contexts simultaneously (e.g., "stop all NPCs"). This is distinct from Channels (one-to-one).

### Success Criteria
- [x] `SignalManager` exists and can track subscribers.
- [x] `SignalManager` is integrated into `ZohRuntime` and accessible via `Context`.
- [x] `/wait` blocks a context until a specific named signal is received.
- [x] `/signal` broadcasts a message to all contexts waiting for that name, waking them up.
- [x] `/wait` supports timeout, handling unsubscription correctly type.
- [x] Context termination cleans up signal subscriptions.

### Out of Scope
- Channel implementation.
- Context navigation.

---

## Context

### Current State
No signal infrastructure exists. `ContextState.WaitingMessage` exists but is unused. `ChannelManager` is currently injected into `Context`.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `Execution/SignalManager.cs` | Manages subscriptions | New file |
| `Execution/ZohRuntime.cs` | Runtime root | Add `SignalManager` property |
| `Execution/Context.cs` | Execution context | Inject `SignalManager`, update `Terminate` |
| `Verbs/Signals/WaitDriver.cs` | `/wait` implementation | New file |
| `Verbs/Signals/SignalDriver.cs` | `/signal` implementation | New file |

### Dependencies
- **Requires:** `Context` class modifications.

---

## Implementation

### Step 1: Implement SignalManager

**Objective:** Manage signal subscriptions and broadcasting.

**Files:** `Execution/SignalManager.cs`

**Logic:**
- `Subscribe(string signalName, Context context)`: Adds context to list for signal.
- `Unsubscribe(string signalName, Context context)`: Removes context.
- `UnsubscribeContext(Context context)`: Removes context from ALL signals (for termination).
- `Broadcast(string signalName, ZohValue payload)`: 
    - Finds all subscribers for `signalName`.
    - Updates their state to `Running`.
    - Sets their `LastResult` to the payload.
    - Removes them from the subscription list (auto-unsubscribe on signal).

### Step 2: Integrate SignalManager

**Objective:** Make SignalManager available to verbs and ensure cleanup.

**Files:** `Execution/ZohRuntime.cs`, `Execution/Context.cs`

**Changes:**
- `ZohRuntime`: Initialize `SignalManager` singleton.
- `Context`: 
    - Add `SignalManager` property.
    - Update constructor to accept `SignalManager`.
    - Update `Terminate()` (and `ExitStory` if needed?) to call `SignalManager.UnsubscribeContext(this)`.

### Step 3: Implement Wait Verb

**Objective:** Block until signal or timeout.

**Files:** `Verbs/Signals/WaitDriver.cs`

**Logic:**
1. Resolve signal name.
2. `SignalManager.Subscribe(name, context)`.
3. Set context state `WaitingMessage`.
4. Set `Context.WaitCondition` to signal name (for debugging).
5. If timeout provided:
    - Set `WaitCondition` to a composite object (e.g. `tuple(string Signal, DateTimeOffset Deadline)`).
    - Note: Runtime loop must handle timeout checks (out of scope for this plan, but data structure should support it).

### Step 4: Implement Signal Verb

**Objective:** Broadcast message.

**Files:** `Verbs/Signals/SignalDriver.cs`

**Logic:**
1. Resolve signal name and payload.
2. `SignalManager.Broadcast(name, payload)`.

---

## Verification Plan

### Automated Checks
- [x] Unit Test `SignalManager`: Subscribe, Broadcast, Unsubscribe.
- [x] Unit Test `Context`: Ensure `Terminate` calls unsubscribe.
- [x] Integration Test: One context `/wait`, another `/signal`. Verify wake-up.
- [x] Integration Test: Broadcast to multiple waiters.

---

## Notes

### Assumptions
- `WaitCondition` property on `Context` can hold the signal name implies debugging/inspection usage.
