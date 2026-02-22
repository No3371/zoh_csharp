# C# Runtime Implementation Roadmap

> **Created:** 2026-02-07 | **Last Revised:** 2026-02-14
> **Scope:** Full ZOH language runtime implementation in C#
> **Parent Navigation:** None (Root for C# implementation)
> **Related Projex:** [Review](20260207-csharp-runtime-nav-review.md), [Red Team](../../projex/20260207-spec-impl-redteam.md)

---

## Vision

To build a robust, spec-compliant, and high-performance ZOH runtime in C# that serves as a reference implementation. The runtime should support the full language specification, including advanced features like concurrency, channels, and custom verb extensions, while maintaining a clean and testable architecture.

---

## Current Position

**As of 2026-02-22:**

Phase 4 (Runtime Architecture) is well underway. **Phase 4.1 (Runtime Core)**, **Phase 4.2 (Storage Completion)**, and **Phase 4.3 (Validation Pipeline)** are **complete**. The runtime now features a formalized handler-registry architecture, comprehensive storage drivers, a robust compilation pipeline, and a complete validation layer. **Phase 4.4 (Standard Verbs - Presentation)** is the immediate next objective, focusing on host application abstractions for interactive verbs.

### Recent Progress
- **Phase 4.3 Complete**: Validation Pipeline — Implemented Story and Verb validators, diagnostic aggregation, and VerbResolutionValidator (2026-02-16)
- **Phase 4.2 Complete**: Storage Completion — Implemented `EraseDriver`, `PurgeDriver`, and fixed `WriteDriver` type safety (2026-02-15)
- **Phase 4.1 Complete**: Runtime Core Formalization — Implemented `HandlerRegistry`, `RuntimeConfig`, `CompilationException`, and refactored `ZohRuntime` (2026-02-15)

### Active Work
- **Phase 4 Milestone 4**: Standard Verbs (Presentation) — `IPresentationHandler`, `ConverseDriver`, `ChooseDriver`, `PromptDriver`

### Known Blockers
- None currently identified

---

## Roadmap

### Phase 1: Foundation (Lexer, Parser, Preprocessor) — [Status: Done]

**Goal:** Establish the ability to read and understand ZOH source code.

**Milestones:**
- [x] Project Setup & Lexer Implementation (Tokenization, Position tracking)
- [x] Parser Implementation (AST generation, Sugar handling, Error recovery)
- [x] Preprocessor Implementation (#embed, `|%` macros, #expand)
- [x] Rigorous Codebase Review (Verified against `impl/*.md` guides)

---

### Phase 2: Core Execution (Types & Logic) — [Status: Done]

**Goal:** Execute synchronous logic and basic data manipulation.

**Milestones:**
- [x] Type System (Value hierarchy, Coercion, Truthiness)
- [x] Variable Storage (Scopes, Shadowing)
- [x] Expression Evaluation (Operators, Interpolation, Special forms)
- [x] Core Verbs (Flow control, Collection manipulation, Debugging)
- [x] Core Runtime Features (AST nodes, Value parsing, Attribute parsing)

---

### Phase 3: Control Flow & Concurrency — [Status: Done]

**Goal:** Enable complex story flows, parallel execution, and inter-context communication.

**Milestones:**
- [x] **Control Flow Verbs** — `If`, `Switch`, `Loop`, `While`, `Foreach`, `Sequence`.
- [x] **Context & Navigation** — Implemented `Jump`, `Fork`, `Call`, `Exit`, `Sleep`.
  - Execution: [Walkthrough](closed/20260207-context-navigation-walkthrough.md)
- [x] **Channel System** — Implemented `ChannelManager`, `Push`, `Pull` (with value/error result), `Close`.
  - Execution: [Walkthrough](closed/20260207-channel-racecond-walkthrough.md)
- [x] **Signal System** — Implemented `SignalManager`, `Wait`, `Signal`.
  - Plan: [20260207-signal-system-plan.md](closed/20260207-signal-system-plan.md)
  - Execution: [Walkthrough](closed/20260207-signal-system-walkthrough.md)

---

### Phase 4: Runtime Architecture — [Status: In Progress]

**Goal:** Formalize the runtime into a cohesive, extensible architecture with handler registries, a compilation pipeline, persistence, validation, and the standard verb interface — following `impl/09_runtime.md` through `impl/12_validation.md`.

**Milestone sequencing rationale:** Runtime Core establishes the handler-registry pattern that Storage, Validation, and Standard Verbs all plug into. Storage is nearly complete (Read/Write drivers + InMemoryStorage exist). Validation provides developer guardrails before we surface the host-facing Standard Verbs.

**Milestones:**

- [x] **4.1 Runtime Core Formalization** — `impl/09_runtime.md`
  - Formalize `RuntimeConfig` (maxContexts, maxChannelDepth, resource limits per red-team findings)
  - Implement ordered handler registries: preprocessor chain, compiler chain, story validators, verb validators, verb drivers
  - Define the compilation pipeline: preprocess → parse → compile → validate → execute
  - Existing code: `ZohRuntime.cs`, `Context.cs`, `CompiledStory.cs`, `VerbRegistry.cs`, `ValueResolver.cs`
  - Execution: [Walkthrough](closed/20260215-runtime-core-formalization-walkthrough.md)

- [x] **4.2 Storage Completion** — `impl/11_storage.md`
  - Implement `EraseDriver` and `PurgeDriver`
  - Define serialization format for persistent values (JSON-based, covering nothing/bool/int/double/string/list/map; reject non-serializable verb/channel/expression)
  - Type-safe reads: respect `[typed]` and `[required]` attributes, scope handling
  - Existing code: `IPersistentStorage.cs`, `InMemoryStorage.cs`, `ReadDriver.cs`, `WriteDriver.cs`
  - Execution: [Walkthrough](closed/20260214-storage-completion-walkthrough.md)

- [x] **4.3 Validation Pipeline** — `impl/12_validation.md`
  - Story validators: duplicate labels, required verbs, jump target validation
  - Verb validators: parameter counts and types for Set, Jump/Fork/Call, and other core verbs
  - Diagnostic formatting and aggregation pipeline (`DiagnosticCollector`)
  - Migrate hardcoded `NamespaceValidator` to `HandlerRegistry` using `VerbResolutionValidator`
  - Existing code: `LabelValidator.cs`, `SetValidator.cs`, `VerbResolutionValidator.cs`
  - Execution: [Walkthrough](closed/20260216-validation-pipeline-walkthrough.md)

- [ ] **4.4 Standard Verbs (Presentation)** — `impl/10_std_verbs.md`
  - Define `IPresentationHandler` interface: the abstraction that lets host applications handle `/converse`, `/choose`, `/prompt` output
  - Implement `ConverseDriver`, `ChooseDriver`, `ChooseFromDriver`, `PromptDriver`
  - Include timeout support and `[Wait]`/`[Style]`/`[By]` attribute handling

- [ ] **4.5 Standard Verbs (Media)** — `impl/10_std_verbs.md`
  - Define `IMediaHandler` interface: host-facing abstraction for visual/audio media
  - Implement `ShowDriver`, `HideDriver`, `PlayDriver`, `PlayOneDriver`, `StopDriver`, `PauseDriver`, `ResumeDriver`, `SetVolumeDriver`
  - These are stubs that delegate to the handler — no actual media rendering

---

### Phase 5: Integration & Polish — [Status: Future]

**Goal:** Ensure correctness, stability, usability, and production readiness.

**Milestones:**
- [ ] **Integration Testing** — End-to-end scenarios from `13_testing.md`; multi-story, multi-context flows
- [ ] **Storage Backends** — File-based backend (`.zohstore` files), optional SQLite backend
- [ ] **Red-Team Remediation** — Address remaining findings from `20260207-spec-impl-redteam.md`: resource limit enforcement, defer error handling semantics, expression injection safe patterns
- [ ] **Documentation** — Public API docs, usage examples, verb reference
- [ ] **Performance** — Profiling, optimization, benchmark suite

---

## Priorities

**Current focus:** Phase 4.4 — Standard Verbs (Presentation). Focus is on defining the `IPresentationHandler` and implementing drivers for `/converse`, `/choose`, `/prompt`.

**Next up:** Phase 4.5 — Standard Verbs (Media).

**Deferred:** File/SQLite storage backends, performance optimizations, and red-team remediation items deferred to Phase 5. In-memory storage is sufficient for Phase 4.

---

## Open Questions

## Open Questions

- [x] Does the current `pull` implementation return a result object (old spec) or value/error (new spec)? **(Answer: Value/Error via VerbResult)**
- [x] Are there other discrepancies from the recent "Spec/Impl Inconsistencies" finding that need to be addressed in C#? **(Answer: Addressed via multiple patches)**
- [x] Should Phase 4 begin with Runtime Core or Standard Verbs? **(Answer: Runtime Core — it defines the handler registries that Standard Verbs plug into)**
- [x] What persistence mechanism should be used for Save/Load? **(Answer: InMemoryStorage for Phase 4 development/testing; file/SQLite backends deferred to Phase 5)**
- [x] Should verb validators be opt-in (registered per verb) or mandatory (auto-generated from signatures)? **(Answer: Opt-in, explicitly implemented per verb as specific classes, e.g. `SetValidator`)**
- [x] How should `IPresentationHandler` handle async host responses (e.g., user choosing from a menu)? **(Answer: Suspend the context using Contextstate and rely on the scheduler to unblock them, similar to channel wait behavior)**

---

## Revision Log

| Date | Summary of Changes |
|------|--------------------|
| 2026-02-07 | Initial roadmap created by converting `c#/task.md`. |
| 2026-02-13 | Phase 3 marked complete. Signal System, Story Header Parsing, Virtual Tokens, CRLF handling, and multiple quality improvements completed. Ready for Phase 4. |
| 2026-02-14 | Phase 4 restructured into five sequenced milestones (4.1–4.5) based on dependency analysis. Resolved ordering and persistence open questions. Added new questions for presentation handler async model and validator strategy. Red-team remediation items and storage backends moved to Phase 5. Updated test count to 515. |
| 2026-02-15 | Phase 4.1 (Runtime Core) and 4.2 (Storage Completion) marked complete. Updated Active Work to Phase 4.3 (Validation Pipeline). |
| 2026-02-22 | Phase 4.3 marked complete based on `20260216-validation-pipeline-walkthrough.md`. Updated Active Work to Phase 4.4 (Standard Verbs - Presentation). Resolved open questions regarding verb validators and async host responses. |
