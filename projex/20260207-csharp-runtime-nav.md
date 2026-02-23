# C# Runtime Implementation Roadmap

> **Created:** 2026-02-07 | **Last Revised:** 2026-02-22
> **Scope:** Full ZOH language runtime implementation in C#
> **Parent Navigation:** None (Root for C# implementation)
> **Related Projex:** [Review](20260207-csharp-runtime-nav-review.md), [Red Team](../../projex/20260207-spec-impl-redteam.md)

---

## Vision

To build a robust, spec-compliant, and high-performance ZOH runtime in C# that serves as a reference implementation. The runtime should support the full language specification, including advanced features like concurrency, channels, and custom verb extensions, while maintaining a clean and testable architecture.

---

## Current Position

**As of 2026-02-23:**

Phase 4 (Runtime Architecture) is **complete**. The runtime now features a formalized handler-registry architecture, comprehensive storage drivers, a robust compilation pipeline, a complete validation layer, and a decoupled host-continuation model for interactive verbs, including full support for Standard Presentation and Media verbs. **Phase 5 (Integration & Polish)** is the immediate next objective, focusing on end-to-end testing, storage backends, red-team remediation, and documentation.

### Recent Progress
- **Phase 4.5 Complete**: Standard Verbs (Media) — Implemented `ShowDriver`, `HideDriver`, `PlayDriver`, `PlayOneDriver`, `StopDriver`, `PauseDriver`, `ResumeDriver`, `SetVolumeDriver` with their respective validators and decoupled media handler interfaces (2026-02-23).
  - Execution: [Plan](closed/20260222-std-verbs-media-csharp-plan.md) | [Walkthrough](closed/20260222-std-verbs-media-csharp-walkthrough.md)
- **Phase 4.4 Complete**: Standard Verbs (Presentation) — Implemented `ConverseDriver`, `ChooseDriver`, `ChooseFromDriver`, `PromptDriver` using fully decoupled host handler interfaces, accompanied by their respective `*Validator`s (2026-02-22)
  - Execution: [Plan](closed/20260222-std-verbs-presentation-csharp-plan.md) | [Walkthrough](closed/20260222-std-verbs-presentation-csharp-walkthrough.md) | [Audit](20260222-std-verbs-presentation-csharp-audit.md)
- **Architecture Refactor**: Verb Driver Continuation — Decoupled blocking verbs from the tick-loop scheduler via a `VerbContinuation` discriminated union (`HostContinuation`, `SleepContinuation`, `ContextContinuation`, `MessageContinuation`). Deprecated `Context.SetState()` in favor of functional yields (2026-02-22)
  - Execution: [Walkthrough](closed/20260222-verb-driver-continuation-csharp-walkthrough.md)
- **Phase 4.3 Complete**: Validation Pipeline — Implemented Story and Verb validators, diagnostic aggregation, and VerbResolutionValidator (2026-02-16)

### Active Work
- **Phase 5**: Integration & Polish — Preparing for end-to-end scenario testing and addressing remaining technical debt.

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

### Phase 4: Runtime Architecture — [Status: Done]

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

- [x] **4.4 Standard Verbs (Presentation)** — `impl/10_std_verbs.md`
  - Implement `ConverseDriver`, `ChooseDriver`, `ChooseFromDriver`, `PromptDriver` using the standard Per-Driver continuation model
  - Expose driver-specific interfaces (e.g., `IConverseHandler`, `IChooseHandler`) for host applications to hook actual presentation logic
  - Include timeout support and `[Wait]`/`[Style]`/`[By]` attribute handling
  - Execution: [Walkthrough](closed/20260222-std-verbs-presentation-csharp-walkthrough.md)

- [x] **4.5 Standard Verbs (Media)** — `impl/10_std_verbs.md`
  - Implement `ShowDriver`, `HideDriver`, `PlayDriver`, `PlayOneDriver`, `StopDriver`, `PauseDriver`, `ResumeDriver`, `SetVolumeDriver` using the independent driver model
  - Expose driver-specific media handler interfaces for platform integration
  - Execution: [Plan](closed/20260222-std-verbs-media-csharp-plan.md) | [Walkthrough](closed/20260222-std-verbs-media-csharp-walkthrough.md)

---

### Phase 5: Integration & Polish — [Status: In Progress]

**Goal:** Ensure correctness, stability, usability, and production readiness.

**Milestones:**
- [ ] **Integration Testing** — End-to-end scenarios from `13_testing.md`; multi-story, multi-context flows
- [ ] **Storage Backends** — File-based backend (`.zohstore` files), optional SQLite backend
- [ ] **Red-Team Remediation** — Address remaining findings from `20260207-spec-impl-redteam.md`: resource limit enforcement, defer error handling semantics, expression injection safe patterns
- [ ] **Documentation** — Public API docs, usage examples, verb reference
- [ ] **Performance** — Profiling, optimization, benchmark suite

---

## Priorities

**Current focus:** Phase 5 — Integration & Polish. Evaluating the next immediate step, such as Integration Testing (end-to-end scenarios) or Storage Backends (File/SQLite).

**Next up:** Resolving red-team findings and public API documentation.

**Deferred:** Performance optimizations.

---

## Open Questions

## Open Questions

- [x] Does the current `pull` implementation return a result object (old spec) or value/error (new spec)? **(Answer: Value/Error via VerbResult)**
- [x] Are there other discrepancies from the recent "Spec/Impl Inconsistencies" finding that need to be addressed in C#? **(Answer: Addressed via multiple patches)**
- [x] Should Phase 4 begin with Runtime Core or Standard Verbs? **(Answer: Runtime Core — it defines the handler registries that Standard Verbs plug into)**
- [x] What persistence mechanism should be used for Save/Load? **(Answer: InMemoryStorage for Phase 4 development/testing; file/SQLite backends deferred to Phase 5)**
- [x] Should verb validators be opt-in (registered per verb) or mandatory (auto-generated from signatures)? **(Answer: Opt-in, explicitly implemented per verb as specific classes, e.g. `SetValidator`)**
- [x] How should interactive drivers handle async host responses (e.g., user choosing from a menu)? **(Answer: Return a Continuation discriminated union so the runtime can decide how to fulfill it without a monolithic handler, while providing driver-specific interfaces for the presentation logic itself)**

---

## Revision Log

| Date | Summary of Changes |
|------|--------------------|
| 2026-02-07 | Initial roadmap created by converting `c#/task.md`. |
| 2026-02-13 | Phase 3 marked complete. Signal System, Story Header Parsing, Virtual Tokens, CRLF handling, and multiple quality improvements completed. Ready for Phase 4. |
| 2026-02-14 | Phase 4 restructured into five sequenced milestones (4.1–4.5) based on dependency analysis. Resolved ordering and persistence open questions. Added new questions for presentation handler async model and validator strategy. Red-team remediation items and storage backends moved to Phase 5. Updated test count to 515. |
| 2026-02-15 | Phase 4.1 (Runtime Core) and 4.2 (Storage Completion) marked complete. Updated Active Work to Phase 4.3 (Validation Pipeline). |
| 2026-02-22 | Phase 4.3 marked complete based on `20260216-validation-pipeline-walkthrough.md`. Updated Active Work to Phase 4.4 (Standard Verbs - Presentation). Resolved open questions regarding verb validators and async host responses. |
| 2026-02-22 | Adopted Per-Driver continuation model for Phase 4.4 and 4.5, replacing monolithic `IPresentationHandler` and `IMediaHandler` with driver-specific interfaces to maximize logic reuse. |
| 2026-02-22 | Phase 4.4 (Standard Verbs - Presentation) marked complete based on `20260222-std-verbs-presentation-csharp-walkthrough.md`. Updated Active Work to Phase 4.5 (Standard Verbs - Media). Logged architectural refactor of `VerbContinuation` discriminated union. |
| 2026-02-23 | Phase 4.5 (Standard Verbs - Media) marked complete based on `20260222-std-verbs-media-csharp-walkthrough.md`. Phase 4 is now fully complete. Updated Active Work to Phase 5 (Integration & Polish). |

