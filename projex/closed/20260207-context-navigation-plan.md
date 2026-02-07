# Plan: Implement Context Navigation & Concurrency

> **Status:** Completed
> **Created:** 2026-02-07
> **Author:** Antigravity
> **Source:** Direct request (Phase 3 of C# Runtime Roadmap)
> **Related Projex:** [20260207-csharp-runtime-nav.md](../../20260207-csharp-runtime-nav.md)
> **Walkthrough:** [20260207-context-navigation-walkthrough.md](./20260207-context-navigation-walkthrough.md)

---

## Summary

This plan implements the core navigation and concurrency primitives for the ZOH runtime. It expands the `Context` class to support execution state (instruction pointer, current story) and implements the verbs required for flow control: `Jump` (move IP), `Fork` (parallel execution), `Call` (subroutine execution), `Exit` (termination), and `Sleep` (pausing).

**Scope:** `Zoh.Runtime` project, specifically `Execution` and `Verbs` namespaces.
**Estimated Changes:** ~7 files (Context, drivers).

---

## Objective

### Problem / Gap / Need
The current `Context` implementation is a skeleton. It lacks the ability to track execution position (`InstructionPointer`), manage story transitions, or handle parallel execution. Without these, the runtime cannot execute actual stories, only isolated verb calls.

### Success Criteria
- [ ] `Context` can track its current Story and Instruction Pointer.
- [ ] `/jump` allows moving execution to a different label (same or different story).
- [ ] `/fork` creates a new independent `Context` running in parallel.
- [ ] `/call` creates a child context and blocks the parent until completion.
- [ ] `/exit` correctly terminates a context and triggering defer execution.
- [ ] `/sleep` pauses context execution for a specified duration.

### Out of Scope
- Channel implementation (already done).
- Signal system (separate plan).
- Runtime scheduler loop (Phase 4).

---

## Context

### Current State
`Context.cs` exists but only contains variable storage, channel manager reference, and basic state enum. It misses:
- `InstructionPointer`
- `CurrentStory`
- `WaitCondition`
- `Clone()` logic

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `Execution/Context.cs` | Execution state container | Add IP, StoryRef, Clone(), Wait state data |
| `Execution/ContextState.cs` | Enum for context status | Add `WaitingContext`, `Sleeping` |
| `Verbs/Flow/JumpDriver.cs` | `/jump` implementation | New file |
| `Verbs/Flow/ForkDriver.cs` | `/fork` implementation | New file |
| `Verbs/Flow/CallDriver.cs` | `/call` implementation | New file |
| `Verbs/Flow/ExitDriver.cs` | `/exit` implementation | New file |
| `Verbs/Flow/SleepDriver.cs` | `/sleep` implementation | New file |

### Dependencies
- **Requires:** `CompiledStory` structure (to resolve labels).
- **Requires:** `ZohRuntime` (to register new contexts).

---

## Implementation

### Step 1: Expand Context Class

**Objective:** specificy state requirements for navigation.

**Files:** `Execution/Context.cs`, `Execution/ContextState.cs`

**Changes:**
```csharp
public class Context : IExecutionContext
{
    public Guid Id { get; } = Guid.NewGuid();
    public CompiledStory CurrentStory { get; set; }
    public int InstructionPointer { get; set; }
    
    // For blocking operations
    public object WaitCondition { get; set; } 
    
    public Context Clone() { ... }
}
```

### Step 2: Implement Jump Verb

**Objective:** Allow moving execution to a specific label.

**Files:** `Verbs/Flow/JumpDriver.cs`

**Logic:**
1. Resolve story (optional) and label.
2. If story changes, executing story defers, clear story scope, load new story.
3. Resolve label index in target story.
4. Set `InstructionPointer`.

### Step 3: Implement Fork Verb

**Objective:** Start parallel execution.

**Files:** `Verbs/Flow/ForkDriver.cs`

**Logic:**
1. Create new Context (via `new` or `Clone` if `[clone]` attribute present).
2. Initialize variables in new context.
3. Set IP and Story in new context.
4. Add new context to `Runtime`.

### Step 4: Implement Call Verb

**Objective:** Subroutine execution.

**Files:** `Verbs/Flow/CallDriver.cs`

**Logic:**
1. Fork new context.
2. Set parent state to `WaitingContext`.
3. Set `WaitCondition` to child Context ID.

### Step 5: Implement Exit & Sleep

**Files:** `Verbs/Flow/ExitDriver.cs`, `Verbs/Flow/SleepDriver.cs`

**Logic:**
- **Exit**: Call `Context.Terminate()`.
- **Sleep**: Set state `Sleeping`, set `WaitCondition` to wake time.

---

## Verification Plan

### Automated Checks
- [ ] Unit tests for `Jump`: valid label, invalid label, story switch.
- [ ] Unit tests for `Fork`: context creation, variable isolation (or copying).
- [ ] Unit tests for `Call`: parent blocking behavior.

### Manual Verification
- [ ] N/A (Unit tests cover logic).

---

## Notes

### Assumptions
- `ZohRuntime` has methods to `AddContext` and `LoadStory`. If not, we will need to mock or stub them.

### Open Questions
- [ ] Does `WaitCondition` need a specific type hierarchy? (Implementation detail: can uses `object` or specific struct).

