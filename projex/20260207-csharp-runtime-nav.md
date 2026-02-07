# C# Runtime Implementation Roadmap

> **Created:** 2026-02-07 | **Last Revised:** 2026-02-07
> **Scope:** Full ZOH language runtime implementation in C#
> **Parent Navigation:** None (Root for C# implementation)
> **Related Projex:** [Review](20260207-csharp-runtime-nav-review.md)

---

## Vision

To build a robust, spec-compliant, and high-performance ZOH runtime in C# that serves as a reference implementation. The runtime should support the full language specification, including advanced features like concurrency, channels, and custom verb extensions, while maintaining a clean and testable architecture.

---

## Current Position

**As of 2026-02-07:**

The project has established a strong foundation with a complete Lexer, Parser, and Preprocessor (Phase 1). The Core Execution layer, including Type System, Expressions, and most Core Verbs, is also implemented and verified (Phase 2).

Work was previously halted to conduct a comprehensive review and validation against the official spec, followed by a series of language spec updates. We are now in a position to resume development, starting with verifying alignment with recent spec changes and then proceeding to concurrency features.

### Recent Progress
- **Foundation Complete**: Lexer, Parser, and Preprocessor are fully implemented and tested.
- **Core Execution**: Types, Variables, Expressions, and basic Verbs (Set, Get, If, Loop, etc.) are working.
- **Spec Review**: A rigorous review and validation procedure was conducted, identifying and fixing several inconsistencies.

### Active Work
- **Resumption**: Re-aligning with latest spec changes (e.g., `pull` behavior, macro syntax).
- **Concurrency**: Groundwork for Contexts and Channels.

### Known Blockers
- **Spec Drift**: The C# implementation needs verification against the very latest `spec.md` changes that occurred during the halt.

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

### Phase 3: Control Flow & Concurrency — [Status: Next]

**Goal:** Enable complex story flows, parallel execution, and inter-context communication.

**Milestones:**
- [x] **Control Flow Verbs** — `If`, `Switch`, `Loop`, `While`, `Foreach`, `Sequence`.
- [x] **Context & Navigation** — Implement `Jump`, `Fork`, `Call`, `Exit`, `Sleep`. (Context class structure exists). [Plan](closed/20260207-context-navigation-plan.md)
- [x] **Channel System** — Implemented `ChannelManager`, `Push`, `Pull` (with value/error result), `Close`.
- [ ] **Signal System** — Implement `SignalManager`, `Broadcast`, `Wait`. [Plan](20260207-signal-system-plan.md)

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

**Current focus:** verifying that the C# runtime is up-to-date with the latest spec changes (specifically `pull` error handling and macro syntax).

**Next up:** Begin implementation of Phase 3 Concurrency features (`Context`, `Jump`, `Fork`).

**Deferred:** Performance optimizations and advanced Standard Verbs until core runtime architecture is solidified.

---

## Open Questions

- [x] Does the current `pull` implementation return a result object (old spec) or value/error (new spec)? **(Answer: Value/Error via VerbResult)**
- [ ] Are there other discrepancies from the recent "Spec/Impl Inconsistencies" finding that need to be addressed in C#?

---

## Revision Log

| Date | Summary of Changes |
|------|--------------------|
| 2026-02-07 | Initial roadmap created by converting `c#/task.md`. |
