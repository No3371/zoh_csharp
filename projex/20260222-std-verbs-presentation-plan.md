# Phase 4.4 Standard Verbs (Presentation) Plan

> **Status:** Ready
> **Created:** 2026-02-22
> **Author:** Antigravity (Agent)
> **Source:** Direct request & roadmap navigation `Phase 4.4`
> **Related Projex:** [Phase 4 Navigation](20260207-csharp-runtime-nav.md)

---

## Summary

This plan outlines the implementation of Phase 4.4 of the C# Runtime: Standard Verbs (Presentation). It introduces the `IPresentationHandler` abstraction for host integration and implements the `/converse`, `/choose`, `/chooseFrom`, and `/prompt` verbs, specifically utilizing `ContextState` to manage asynchronous host presentation blocking seamlessly as requested by the user.

**Scope:** `src/Zoh.Runtime/Presentation` (abstraction), `src/Zoh.Runtime/Verbs/Std` (drivers), `src/Zoh.Runtime/Execution` (context state), and corresponding tests.
**Estimated Changes:** ~10 new files, modifications to `ContextState` and `ZohRuntime`.

---

## Objective

### Problem / Gap / Need
Currently, the ZOH runtime can execute logic, but lacks the standard verbs (`/converse`, `/choose`, etc.) necessary to interact with users. These verbs require a non-blocking asynchronous interaction model with the host application (like a game engine or UI). Following the existing pattern for channels and sleeps, we need to implement these verbs such that they suspend the context (`ContextState.WaitingPresentation`) until the host fulfills the presentation request.

### Success Criteria
- [ ] `IPresentationHandler` interface and related data models (`PresentationRequest`) defined.
- [ ] `ContextState` updated to include `WaitingPresentation`.
- [ ] Runtime provides a mechanism for the host to resolve waiting presentations.
- [ ] `/converse` verb driver implemented with `[Wait]`, `[By]`, `[Style]` attributes and timeout handling.
- [ ] `/choose` verb driver implemented with visibility logic and correct returns.
- [ ] `/chooseFrom` verb driver implemented.
- [ ] `/prompt` verb driver implemented.
- [ ] Comprehensive unit tests for all drivers and blocking/resuming mechanics.

### Out of Scope
- Media Verbs (`/show`, `/hide`, `/play`, etc.) which belong to Phase 4.5.
- Complex GUI host implementations (only the runtime abstractions and drivers are in scope).

---

## Context

### Current State
`Context.cs` handles blocking via `ContextState` enumeration (`WaitingChannel`, `Sleeping`, etc.) and `WaitCondition`. `ZohRuntime` handles ticking/unblocking contexts. We will reuse this machinery for presentation waiting.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Execution/ContextState.cs` | Context states | Add `WaitingPresentation` |
| `src/Zoh.Runtime/Presentation/IPresentationHandler.cs` | Host facade interface | [NEW] Define abstractions |
| `src/Zoh.Runtime/Presentation/Models.cs` | Request types | [NEW] Define `PresentationRequest`, `Choice`, etc. |
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | Runtime execution | Add presentation fulfillment methods |
| `src/Zoh.Runtime/Verbs/Std/ConverseDriver.cs` | /converse logic | [NEW] Implement driver |
| `src/Zoh.Runtime/Verbs/Std/ChooseDriver.cs` | /choose logic | [NEW] Implement driver |
| `src/Zoh.Runtime/Verbs/Std/ChooseFromDriver.cs` | /chooseFrom logic | [NEW] Implement driver |
| `src/Zoh.Runtime/Verbs/Std/PromptDriver.cs` | /prompt logic | [NEW] Implement driver |
| `tests/Zoh.Tests/Verbs/Std/PresentationTests.cs` | Tests | [NEW] Verify behavior |

### Dependencies
- **Requires:** Phase 4.3 (Validation Pipeline) — Completed.
- **Blocks:** Phase 4.5 (Standard Verbs - Media).

### Constraints
- Must remain strictly non-blocking inside `ZohRuntime.Run()`. Context must suspend and exit the `Run` loop while waiting for the host.
- Must accurately map specific attributes (`[By]`, `[Portrait]`, `[Wait]`, `[Style]`) per `impl/10_std_verbs.md`.

---

## Implementation

### Overview
1.  **Define Presentation Abstractions**: Create `IPresentationHandler`, `PresentationRequest`, and `PresentationWaitCondition`.
2.  **Update Core Execution**: Add `WaitingPresentation` to `ContextState` and allow `ZohRuntime` to dispatch to `IPresentationHandler` when a verb suspends playback.
3.  **Implement Drivers**: Build `ConverseDriver`, `ChooseDriver`, `ChooseFromDriver`, `PromptDriver` which dispatch the request and put the context to sleep.
4.  **Host Fulfillment**: Create methods on `ZohRuntime` (e.g. `ResumePresentation(contextId, value)`) to unblock the context and resume execution.

### Step 1: Presentation Abstractions

**Objective:** Define the interface between ZOH and the host application.

**Files:**
- `src/Zoh.Runtime/Presentation/IPresentationHandler.cs`
- `src/Zoh.Runtime/Presentation/PresentationRequest.cs`
- `src/Zoh.Runtime/Execution/WaitConditions.cs` (or inside `Context.cs`)

**Changes:**
- Create abstract models mirroring the spec definitions (e.g. `PresentationRequest` with `type`, `speaker`, `choices`, etc.).
- Create `IPresentationHandler` with `void HandlePresentation(PresentationRequest req, string contextId)`.
- Define `PresentationWaitCondition` to store the expected response type and an optional timeout.

### Step 2: Core State Updates

**Objective:** Wire up `ContextState` to support presentation blocking.

**Files:**
- `src/Zoh.Runtime/Execution/ContextState.cs`
- `src/Zoh.Runtime/Execution/ZohRuntime.cs`

**Changes:**
- Add `WaitingPresentation` to `ContextState`.
- Add `IPresentationHandler? PresentationHandler { get; set; }` to `ZohRuntime`.
- Add `ResumePresentation(string contextId, ZohValue result)` to `ZohRuntime`. This method sets the context's state back to `Running`, sets `LastResult`, and immediately clears `WaitCondition`. It should also handle timeouts if ticked.

### Step 3: Implement Drivers

**Objective:** Implement the actual Standard Verbs.

**Files:**
- `src/Zoh.Runtime/Verbs/Std/ConverseDriver.cs`
- `src/Zoh.Runtime/Verbs/Std/ChooseDriver.cs`
- `src/Zoh.Runtime/Verbs/Std/ChooseFromDriver.cs`
- `src/Zoh.Runtime/Verbs/Std/PromptDriver.cs`

**Changes:**
- Register these in `HandlerRegistry` with namespace `std` (or global if they act as core, per spec they are generally run without namespaces but belong to standard library).
- Implement `ConverseDriver`: Resolves string interpolation, creates `PresentationRequest`, calls `context.Runtime.PresentationHandler?.HandlePresentation()`, and sets `context.SetState(ContextState.WaitingPresentation)` if `[Wait]` is true or interactive is true.
- Implement `ChooseDriver`: Resolves visibility conditions of interlaced parameters, creates choice list, suspends context.
- Implement `PromptDriver`: Similar suspension.

### Step 4: Testing

**Objective:** Ensure full compliance to `impl/10_std_verbs.md`.

**Files:**
- `tests/Zoh.Tests/Verbs/Std/PresentationTests.cs`

**Changes:**
- Inject a mock `IPresentationHandler`.
- Test that execution stops after `/converse` until `ResumePresentation` is called.
- Test `/choose` conditionally hiding options based on expression evaluations.

---

## Verification Plan

### Automated Checks
- [ ] `dotnet build c#\Zoh.sln`
- [ ] `dotnet test c#\Zoh.sln --filter "PresentationTests"`

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `Context` suspends | Run a story with `/converse; /set *x, 1;`. Check context state. Variable `*x` should not be set yet. | State is `WaitingPresentation`. |
| Host resumes | Call `runtime.ResumePresentation(id)`. Call `runtime.Run()`. | Context finishes, `*x` is 1. |
| `/choose` filters | Use visible toggles `false, "option1", 1`. | Mock handler receives constraints where option1 is missing. |

---

## Rollback Plan
1. Delete `src/Zoh.Runtime/Presentation` namespace files.
2. Delete new drivers in `src/Zoh.Runtime/Verbs/Std`.
3. Undo `ContextState` and `ZohRuntime` modifications.

---

## Notes

### Assumptions
- Host response values map directly to `ZohValue` (e.g., StringValue for prompt, arbitrary selected value for choose).
- If `IPresentationHandler` is null, drivers should act as "no-op fire-and-forget" or auto-resume immediately depending on strictness (defaulting to auto-resume is safer to prevent soft locks in headless testing).

### Open Questions
- [ ] None.
