# Standard Verbs (Presentation) Plan

> **Status:** Draft
> **Created:** 2026-02-22
> **Author:** Antigravity
> **Source:** Direct request from user / Phase 4.4 Roadmap
> **Related Projex:** [Navigation Roadmap](../../20260207-csharp-runtime-nav.md)

---

## Summary

Implement the Phase 4.4 Standard Presentation Verbs (`/converse`, `/choose`, `/chooseFrom`, `/prompt`) using a Per-Driver Continuation Model. Each driver will expose a specific interface (e.g., `IConverseHandler`) for the host application to hook into, maximizing reusability without enforcing a monolithic presentation architecture.

**Scope:** `c#/src/Zoh.Runtime` (Standard verbs, Context resume logic, validation)
**Estimated Changes:** ~15 files (4 drivers, 4 handlers, 4 validators, Context additions, extensions)

---

## Objective

### Problem / Gap / Need
ZOH scripts require interactive storytelling capabilities (dialogue, choices, text input). The runtime needs to provide drivers for these standard verbs (`10_std_verbs.md`) while remaining completely decoupled from actual UI/presentation rendering. The implementation must allow hosts to selectively support verbs via specific interfaces, and properly suspend script execution using the Continuation model until the host provides user input.

### Success Criteria
- [ ] `Context` and `IExecutionContext` expose a clean `Resume(ZohValue? value)` method.
- [ ] `ConverseDriver` correctly evaluates `[Wait]`, `[Style]`, `[By]` attrs, interpolates content, calls `IConverseHandler`, and yields if waiting.
- [ ] `ChooseDriver` properly evaluates visibility expressions, passing only visible choices to `IChooseHandler`, and yields for user selection.
- [ ] `ChooseFromDriver` processes a List of Maps and passes to `IChooseFromHandler`.
- [ ] `PromptDriver` passes timeout and style to `IPromptHandler` and yields for a string response.
- [ ] Verbs correctly handle timeouts (if > 0) per the ZOH spec.
- [ ] Validators ensure correct argument counts and types for all 4 verbs.

### Out of Scope
- Media Verbs (`/show`, `/hide`, `/play`, etc.) which belong to Phase 4.5.
- Actual UI implementations (e.g., Unity/Godot integrations).

---

## Context

### Current State
Phase 4.3 completed the validation pipeline. Context blocking uses `VerbContinuation` and `Context.Block()`, but unblocking currently requires manual state manipulation or `SignalManager`. We need a standard `Resume` method on `Context`. Drivers for presentation verbs do not yet exist. 

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `Zoh.Runtime/Execution/IExecutionContext.cs` | Context abstraction | Add `Resume(ZohValue? value = null)` |
| `Zoh.Runtime/Execution/Context.cs` | Context implementation | Implement `Resume` to unset `WaitCondition` and set `State = Running` |
| `Zoh.Runtime/Verbs/VerbContinuation.cs` | Continuation records | Add `PresentationContinuation(string Type)` |
| `Zoh.Runtime/Verbs/Standard/Presentation/*Driver.cs` | Verb logic | Create `ConverseDriver`, `ChooseDriver`, `ChooseFromDriver`, `PromptDriver` |
| `Zoh.Runtime/Verbs/Standard/Presentation/I*Handler.cs` | Host hooks | Create `IConverseHandler`, `IChooseHandler`, `IChooseFromHandler`, `IPromptHandler` |
| `Zoh.Runtime/Validation/Standard/*Validator.cs` | AST Validation | Create validators for new verbs |

### Dependencies
- **Requires:** Phase 4.3 Validation Pipeline, Phase 4.1 Runtime Core.
- **Blocks:** Phase 4.5 Media Verbs (requires continuation patterns established here).

---

## Implementation

### Overview
1. Enhance the Execution Engine to easily resume blocked contexts.
2. Define the exact DTOs (Data Transfer Objects) and Hook Interfaces for each presentation verb.
3. Implement the Verb Drivers to perform Zoh-specific logic (interpolation, expression evaluation, timeout parsing) and forward clean DTOs to the hooks.
4. Implement rigorous AST validators to shift errors left.

---

### Step 1: Context Resume Support

**Objective:** Give host applications a clean API to resume a context blocked by a presentation continuation.

**Files:**
- `c#/src/Zoh.Runtime/Execution/IExecutionContext.cs`
- `c#/src/Zoh.Runtime/Execution/Context.cs`
- `c#/src/Zoh.Runtime/Verbs/VerbContinuation.cs`

**Changes:**

```csharp
// In IExecutionContext.cs
public interface IExecutionContext
{
    // ... existing ...
    void Resume(ZohValue? value = null);
}

// In Context.cs
public void Resume(ZohValue? value = null)
{
    if (State == ContextState.Terminated) return;
    
    LastResult = value ?? ZohNothing.Instance;
    WaitCondition = null;
    SetState(ContextState.Running);
}

// In VerbContinuation.cs
public sealed record PresentationContinuation(string InteractionType) : VerbContinuation;
```

**Rationale:** Hosts implementing the handler interfaces will be passed the `IExecutionContext`. Once the user interacts with the UI (clicks a button, types text), the host simply calls `context.Resume(selectedValue)` and schedules the runtime to tick again.

---

### Step 2: Converse Driver & Handler

**Objective:** Implement `/converse`.

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Standard/Presentation/IConverseHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Presentation/ConverseDriver.cs`

**Changes:**

```csharp
// IConverseHandler.cs
public record ConverseRequest(string? Speaker, string? Portrait, bool Append, string Style, double? TimeoutMs, IReadOnlyList<string> Contents);

public interface IConverseHandler
{
    void OnConverse(IExecutionContext context, ConverseRequest request);
}

// ConverseDriver.cs
public class ConverseDriver : IVerbDriver
{
    private readonly IConverseHandler? _handler;
    public string? Namespace => "std";
    public string Name => "converse";

    public ConverseDriver(IConverseHandler? handler = null) => _handler = handler;

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        // Parse attributes (By, Portrait, Append, Style, Wait)
        // Check wait behavior (interactive flag vs Wait attribute)
        // Parse timeout named param
        // For each unnamed param:
        //   Resolve & Interpolate
        // Create ConverseRequest
        // _handler?.OnConverse(context, request);
        // if shouldWait: return VerbResult.Yield(new PresentationContinuation("converse"));
        
        // Note: For multiple contents that all wait, we yield on the FIRST one.
        // Wait, Zoh spec says process each as separate presentation.
        // Handling multiple yields requires the driver to store state or the script to not pass multiple if waiting.
        // ZohSpec: "Process each content parameter as separate presentation... Wait for user input if interactive"
        // Since we pass the entire List of content to the handler natively, the host handles stepping through the multiple dialogue lines, and the driver yields *once*.
    }
}
```

---

### Step 3: Choose & ChooseFrom Drivers

**Objective:** Implement choices.

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Standard/Presentation/IChooseHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseDriver.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Presentation/ChooseFromDriver.cs`

**Changes:**
Establish `ChoiceItem(string Text, ZohValue Value)` and `ChooseRequest(string? Speaker, string? Portrait, string Style, string? Prompt, double? TimeoutMs, IReadOnlyList<ChoiceItem> Choices)`.

**ChooseDriver**: Look at `call.UnnamedParams`. Step by 3 (visible, text, value). Evaluate `visible`. If true, evaluate/interpolate text and add to list. Call `_handler?.OnChoose(context, request)`. Yield `PresentationContinuation`.

**ChooseFromDriver**: Expects 1 list param of maps. Extract text/value pairs into `ChoiceItem` list. Call `_handler?.OnChoose(context, request)`. Yield.

---

### Step 4: Prompt Driver

**Objective:** Implement `/prompt`.

**Files:**
- `c#/src/Zoh.Runtime/Verbs/Standard/Presentation/IPromptHandler.cs`
- `c#/src/Zoh.Runtime/Verbs/Standard/Presentation/PromptDriver.cs`

**Changes:**
Establish `PromptRequest(string Style, string? PromptText, double? TimeoutMs)`.
Call `_handler?.OnPrompt(context, request)`. Yield `PresentationContinuation`.

---

### Step 5: Validation Layer & Registration

**Objective:** Ensure AST is validated for these verbs before execution, and register them.

**Files:**
- `c#/src/Zoh.Runtime/Validation/Standard/ConverseValidator.cs`
- `c#/src/Zoh.Runtime/Validation/Standard/ChooseValidator.cs`
- `c#/src/Zoh.Runtime/Validation/Standard/ChooseFromValidator.cs`
- `c#/src/Zoh.Runtime/Validation/Standard/PromptValidator.cs`
- `c#/src/Zoh.Runtime/HandlerRegistry.cs`

**Changes:**
- Add validators that enforce minimum arg counts and validate named param types (e.g. `timeout` must be a literal number if provided as literal).
- Update `HandlerRegistry.RegisterCoreHandlers()` to inject default `null` handlers (meaning the verb executes and resolves immediately, acting as a no-op if no host implements them, which prevents crashes but warns the user).

---

## Verification Plan

### Automated Checks
Because these drivers depend on external host logic to be tested properly, we will use mock handlers in Unit Tests:
- [ ] **ConverseTests.cs**: Mock `IConverseHandler`. Verify `OnConverse` is called with correctly interpolated strings. Verify context `State` is `WaitingMessage` or similar, call `Resume()` and verify it continues.
- [ ] **ChooseTests.cs**: Mock `IChooseHandler`. Provide choices with `true` and `false` visibility expressions. Verify only the `true` choices are passed to the handler. Provide a mock selection via `Resume(selectedValue)`. Verify the context receives the selected value.
- [ ] **ChooseFromTests.cs**: Similar to Choose, using a list of maps.
- [ ] **PromptTests.cs**: Verify `Resume(new ZohStr("user input"))` correctly populates the captured variable.

### Command for Verification
```powershell
cd c#; dotnet test --filter "FullyQualifiedName~Zoh.Runtime.Tests.Verbs.Presentation"
```

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Context unblocks cleanly | Run unit test calling `context.Resume(...)` | `InstructionPointer` advances correctly. |
| Per-Driver Handlers | Inspect Driver constructors | Drivers require specific `I*Handler`, no `IPresentationHandler`. |

---

## Rollback Plan
If implementation causes issues, revert the commit that adds `Verbs/Standard/Presentation` and remove the `Resume` method from `Context` and `IExecutionContext`.

---

## Notes

### Assumptions
- The compilation pipeline does not expand `/converse A, B;` into multiple AST nodes. Therefore, passing a List of contents to `IConverseHandler` is the functionally correct way to prevent dropping the execution pointer on a single yield. Hosts will present them sequentially.
- If no handler is provided to a Driver (i.e. Headless mode or unregistered), the Driver will execute as a no-op, printing to a debug log, and returning `VerbResult.Ok()` immediately without yielding.

### Open Questions
- [ ] None. The architectural decisions align with the user's Per-Driver continuation request.
