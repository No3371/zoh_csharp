# Plan: Implement Signal System

> **Status:** Draft
> **Created:** 2026-02-07
> **Author:** Antigravity
> **Source:** Direct request (Phase 3 of C# Runtime Roadmap)
> **Related Projex:** [20260207-csharp-runtime-nav.md](../projex/20260207-csharp-runtime-nav.md)

---

## Summary

This plan implements the Signal System, a broadcasting mechanism for cross-context synchronization. It introduces `SignalManager` to handle subscriptions and broadcasting, and implements the `/wait` and `/signal` verbs.

**Scope:** `Zoh.Runtime` project, `Execution` and `Verbs` namespaces.
**Estimated Changes:** ~4 files (`SignalManager`, verbs).

---

## Objective

### Problem / Gap / Need
ZOH requires a mechanism for one-to-many communication, allowing a context to wake up multiple waiting contexts simultaneously (e.g., "stop all NPCs"). This is distinct from Channels (one-to-one).

### Success Criteria
- [ ] `SignalManager` exists and can track subscribers.
- [ ] `/wait` blocks a context until a specific named signal is received.
- [ ] `/signal` broadcasts a message to all contexts waiting for that name, waking them up.
- [ ] `/wait` supports timeout.

### Out of Scope
- Channel implementation.
- Context navigation.

---

## Context

### Current State
No signal infrastructure exists.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `Execution/SignalManager.cs` | Manages subscriptions | New file |
| `Verbs/Signals/WaitDriver.cs` | `/wait` implementation | New file |
| `Verbs/Signals/SignalDriver.cs` | `/signal` implementation | New file |
| `Execution/ContextState.cs` | Enum for context status | Add `WaitingMessage` |

### Dependencies
- **Requires:** `Context` class (to subscribe contexts).

---

## Implementation

### Step 1: Implement SignalManager

**Objective:** Manage signal subscriptions.

**Files:** `Execution/SignalManager.cs`

**Logic:**
- `Subscribe(string signalName, Context context)`
- `Broadcast(string signalName, ZohValue payload)`: checks all subscribers, updates their state to `Running`, sets their `LastResult` to the payload, and removes them from subscription list.

### Step 2: Implement Wait Verb

**Objective:** Block until signal.

**Files:** `Verbs/Signals/WaitDriver.cs`

**Logic:**
1. Resolve signal name.
2. `SignalManager.Subscribe(name, context)`.
3. Set context state `WaitingMessage`.
4. Set timeout condition if applicable.

### Step 3: Implement Signal Verb

**Objective:** Broadcast message.

**Files:** `Verbs/Signals/SignalDriver.cs`

**Logic:**
1. Resolve signal name and payload.
2. `SignalManager.Broadcast(name, payload)`.

---

## Verification Plan

### Automated Checks
- [ ] Test `Subscribe` and `Broadcast` interaction.
- [ ] Test multiple contexts waiting for same signal.
- [ ] Test timeout behavior.

---

## Notes

### Assumptions
- `Context` has a `SetResult` or similar method to receive the signal payload when woken.

