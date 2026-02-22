# Walkthrough: Implementing Standard Presentation Verbs (C#)

## Goal
To implement the standard presentation verbs (`/converse`, `/choose`, `/chooseFrom`, `/prompt`) for the C# ZOH runtime using a Per-Driver Continuation Model, replacing the deprecated `IPresentationHandler` system.

## Changes Made

1. **Context Resume Support:**
   - Modified `IExecutionContext` and `Context` to support `Resume` operations containing arbitrary objects provided by the host.
   - Replaced `PresentationContinuation` and `MediaContinuation` with a unified `HostContinuation` class in `VerbContinuation.cs` that carries the verb name needing host completion.
   - Refactored `Context.Block` to transition state to `ContextState.WaitingHost`.

2. **Per-Driver Interfaces & Requests:**
   - Created individual handler interfaces for maximum composability for Host implementation:
     - `IConverseHandler` -> `ConverseRequest`
     - `IChooseHandler` -> `ChooseRequest`
     - `IChooseFromHandler` -> `ChooseRequest`
     - `IPromptHandler` -> `PromptRequest`

3. **Driver Implementations:**
   - Authored the core logic for the standard verbs ensuring all logic aligns exactly with `spec.md` and `impl/10_std_verbs.md`.
   - Drivers process their attributes, evaluate parameters via `ValueResolver`, and delegate interaction payloads directly to the Handler implementations attached to Context.
   - Built to return `VerbResult.Yield(new HostContinuation(...))` when interactions demand blocking execution.

4. **Validation Logic & Registry updates:**
   - Built Validators enforcing correct attributes (`[By]`, `[Portrait]`, `[Style]`, `[Wait]`), positional argument count, and types (`ZohList` validation in `ChooseFrom`).
   - Registered all new Drivers (`VerbRegistry.cs`) and Validators (`HandlerRegistry.cs`) in the runtime's core loader pipeline.
   - Fixed `VerbRegistry.cs` suffix indexing to correctly override drivers when multiple plugins have the same `Namespace` and `Name`, allowing seamless mocking during tests.

## Verification Activity

1. **Unit Testing:**
   - Authored unit test suites for each verb (`ConverseDriverTests`, `ChooseDriverTests`, `ChooseFromDriverTests`, `PromptDriverTests`), mocking their relevant `IHandler` interfaces.
   - Handled compiler exceptions to surface real `Diagnostic` outputs, exposing that test drivers needed isolated Context environments to prevent ambiguity.
   - All tests properly check for `ContextState.WaitingHost`, payload correctness, timeout compilation, runtime expression evaluation in text nodes, and variable string interpolation logic.

2. **Results:**
   - All 13/13 unit tests passed flawlessly.
   - Core codebase updated successfully without regressions.

## Future Recommendations
- Review remaining Media Standard Verbs to leverage the same unified `HostContinuation` paradigm.
