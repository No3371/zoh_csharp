# Walkthrough: Context Navigation & Concurrency

I have implemented the core navigation and concurrency verbs for the ZOH runtime, enabling non-linear story execution and parallel processing.

## Changes

### 1. Context & Execution State
- **Enhanced `Context.cs`**: Added `InstructionPointer`, `CurrentStory`, and `WaitCondition` to track execution state.
- **New States**: Added `WaitingContext` (for `Call`) and `Sleeping` (for `Sleep`) to `ContextState.cs`.
- **Delegates**: Introduced `StoryLoader` and `ContextScheduler` to decouple story loading and thread scheduling from the core context logic.
- **Deep Copy**: Implemented `VariableStore.Clone()` and `Context.Clone()` to support safe context forking.

### 2. Flow Verbs Implementation
- **`Core.Jump`**: Implemented `JumpDriver` to handle local jumps (updating IP) and cross-story jumps (loading new story, clearing story variables).
- **`Core.Fork`**: Implemented `ForkDriver` to spawn new parallel contexts. Supports `[clone]` attribute to copy variables.
- **`Core.Call`**: Implemented `CallDriver` to spawn a child context and pause the parent until the child terminates.
- **`Core.Exit`**: Implemented `ExitDriver` to terminate the current context.
- **`Sleep`**: Implemented `SleepDriver` to pause execution for a specified duration.

### 3. Verb Registry & Namespaces
- Registered all new verbs in `VerbRegistry`.
- **Spec Compliance**: ensured `Jump`, `Fork`, `Call`, `Exit` are registered in the **`core`** namespace (e.g., `/core.jump`), and `Sleep` is global/unnamespaced, matching the ZOH specification. The internal C# namespace `Zoh.Runtime.Verbs.Flow` is purely for code organization.

### 4. Verification
Created new test suites in `Zoh.Tests`:
- **`NavigationTests.cs`**: Verifies `/jump` behavior, ensuring IP updates correct labels and variables are cleared when switching stories. Verified error codes `invalid_story` and `invalid_checkpoint`.
- **`ConcurrencyTests.cs`**: Verifies `/fork` creates new scheduled contexts and `/call` correctly pauses the parent/waits for child. Verified `[clone]` attribute works as expected.
- **`SleepTests.cs`**: Verifies `/sleep` puts context into `Sleeping` state with correct timeout.

## Verification Results

### Automatic Tests
Ran `dotnet test` on `Zoh.Tests`.

| Test Suite | Status | Notes |
| :--- | :--- | :--- |
| `NavigationTests` | **PASS** | Correctly handles jumps and story switching. |
| `ConcurrencyTests` | **PASS** | Validates forking, calling, and variable isolation. |
| `SleepTests` | **PASS** | Confirms sleep state transitions. |
| `VerbSpecComplianceTests` | **PASS** | Confirms verbs are registered with correct names/namespaces. |
| **Total** | **450 Passed** | 0 Failed |

### Manual Checks
- Verified that `VariableStore` correctly isolates story variables vs context variables.
- Verified that `Jump` to a different story clears story variables but keeps context variables.
- Verified that `Fork` without `[clone]` starts with empty variables, and with `[clone]` copies them.
