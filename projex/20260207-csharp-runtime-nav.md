# C# Runtime Implementation Roadmap

> **Created:** 2026-02-07 | **Last Revised:** 2026-02-13
> **Scope:** Full ZOH language runtime implementation in C#
> **Parent Navigation:** None (Root for C# implementation)
> **Related Projex:** [Review](20260207-csharp-runtime-nav-review.md)

---

## Vision

To build a robust, spec-compliant, and high-performance ZOH runtime in C# that serves as a reference implementation. The runtime should support the full language specification, including advanced features like concurrency, channels, and custom verb extensions, while maintaining a clean and testable architecture.

---

## Current Position

**As of 2026-02-13:**

Phase 3 (Control Flow & Concurrency) is **complete**. All concurrency features including Contexts, Channels, Signals, and Navigation are fully implemented and tested. The implementation has been enhanced with virtual tokens (CheckpointEnd, StoryNameEnd), comprehensive macro support, robust CRLF handling, and parse verb improvements. The runtime now consists of 500+ passing tests and is ready for Phase 4 (Runtime Architecture).

### Recent Progress
- **Phase 3 Complete**: All concurrency features (Context, Navigation, Channels, Signals) implemented and verified (2026-02-11)
- **Story Parsing**: Multi-word story names and virtual tokens (StoryNameEnd, CheckpointEnd) (2026-02-09)
- **CRLF Handling**: Comprehensive analysis and normalization implemented (2026-02-11)
- **Macro System**: Indentation preservation, symmetric trimming, and escaping (2026-02-06)
- **Parse Improvements**: Whitespace trimming for `/parse` verb (2026-02-12)

### Active Work
- **Phase 4 Planning**: Preparing for Runtime Architecture phase
- **CRLF Investigation**: Ongoing exploration of line ending handling across the lexer

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

### Phase 4: Runtime Architecture — [Status: Future]

**Goal:** Create a cohesive runtime environment capable of hosting multiple stories and providing external hooks.

**Milestones:**
- [ ] **Runtime Core** — Top-level coordinator, Handler registry, Compilation pipeline.
- [ ] **Standard Verbs** — Input/Output (Converse, Choose, Prompt), Audio/Visual stubs.
- [ ] **Persistence** — Save/Load functionality (Write, Read, Erase).
- [ ] **Validation Pipeline** — Semantic analysis and diagnostics.

---

### Phase 5: Integration & Polish — [Status: Future]

**Goal:** Ensure correctness, stability, and usability.

**Milestones:**
- [ ] **Integration Testing** — End-to-end scenarios from `13_testing.md`.
- [ ] **Documentation** — Public API docs and usage examples.
- [ ] **Performance** — Profiling and optimization.

---

## Priorities

**Current focus:** Planning Phase 4 Runtime Architecture.

**Next up:** Runtime Core — Top-level coordinator, Handler registry, Compilation pipeline.

**Deferred:** Performance optimizations and advanced Standard Verbs until core runtime architecture is solidified.

---

## Open Questions

- [x] Does the current `pull` implementation return a result object (old spec) or value/error (new spec)? **(Answer: Value/Error via VerbResult)**
- [x] Are there other discrepancies from the recent "Spec/Impl Inconsistencies" finding that need to be addressed in C#? **(Answer: Addressed via multiple patches)**
- [ ] Should Phase 4 begin with Runtime Core or Standard Verbs?
- [ ] What persistence mechanism should be used for Save/Load (filesystem, database, custom)?

---

## Revision Log

| Date | Summary of Changes |
|------|--------------------|
| 2026-02-07 | Initial roadmap created by converting `c#/task.md`. |
| 2026-02-13 | Phase 3 marked complete. Signal System, Story Header Parsing, Virtual Tokens, CRLF handling, and multiple quality improvements completed. Ready for Phase 4. |
