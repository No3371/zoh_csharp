# C# Runtime Implementation Roadmap

> **Created:** 2026-02-07 | **Last Revised:** 2026-03-26
> **Git:** C# history lives in the **nested repo** `csharp/` (`git -C csharp log …`). The parent `zoh` checkout may show `csharp/` as untracked — use the nested repo for blame and archaeology.
> **Scope:** Full ZOH language runtime implementation in C# (structure, features, integration, polish)
> **Parent Navigation:** None (root for this implementation area)
> **Child navigation:** [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md) — spec-by-spec compliance and remaining behavioral gaps
> **Related Projex:** [20260227-phase5-concurrency-context-signal-gaps-fix-plan.md](20260227-phase5-concurrency-context-signal-gaps-fix-plan.md) (plan text partially superseded — see Git history; **remaining:** `/jump`/`/fork` var transfer), [closed/20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md) (closed umbrella — control-flow compliance). Historical red-team / spec–impl follow-ups: `spec/.projex/closed/` (e.g. channel race condition, inconsistency patches).

---

## Vision

To build a robust, spec-aligned, and high-performance ZOH runtime in C# that serves as a reference implementation. The runtime should support the full language specification, including concurrency, channels, and standard verbs, with a clean and testable architecture. **Implementation milestones** (this document) track major capability layers; **compliance** (child nav) tracks line-by-line spec parity and schedules focused fix plans.

---

## Current Position

**As of 2026-03-26:**

**Structural phases 1–4 (this roadmap)** remain **done** — lexer through standard presentation/media verbs, validation pipeline, storage drivers, continuation model, and runtime core formalization as scoped here.

**Since the last revision (2026-02-23):** a large amount of **spec-hardening** landed: control-flow semantics (`/if`, `/switch`, `/foreach`, `breakif`/`continueif`, `/do`), suspend/fatal propagation for verb conditions, RNG/parse fixes, diagnostic cleanup, core-verb namespace alignment, and related tests. Full suite: **719** passing (`dotnet test` on `Zoh.Tests`).

**What is *not* done at spec level** (see child nav): Phase **5–6** audit items — **still open:** variadic `var` transfer on **`/jump`** and **`/fork`** (per `JumpDriver` / `ForkDriver`); Phase **6** blocking `/pull`, pull `timeout`, and further items in the child nav. **Already landed** (git-backed; child nav updated): `/flag` (`451d387`), `/wait` + `timeout:` on `MessageContinuation` (`2c74be7` + `WaitDriver`), `/call` trailing `*var` + `[inline]` merge (`CallDriver`, inline join `cf4f5bd`). Narrow follow-up plan: [20260227-phase5-concurrency-context-signal-gaps-fix-plan.md](20260227-phase5-concurrency-context-signal-gaps-fix-plan.md) (scope trimmed to remaining gaps).

### Recent Progress
- Control-flow / expression compliance wave (2026-02-28–03-26): closed umbrella [closed/20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md); condition suspend/fatal propagation (`bbc3ef2`); supporting patches/walkthroughs under `csharp/projex/closed/`.
- March 2026 runtime hardening (see **Git history** below): try/suspension wrapping (`cf349b2`), presentation wait diagnostics, diagnostic code normalization, **timeout consistency** across verb drivers (`2c74be7`), **`/flag`** (`451d387`), embed/preprocessor flags, flow-verb test coverage (`d5bfe11`), **inline `/call` join** (`cf4f5bd`), core **namespace alignment** (`ca35285`), Phase 4 control-flow closure cluster (`6b3de81`…`bbc3ef2`).
- Audit nav updated for Phase 4 closure and Phase 2.5/2.6 implementation reality: [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md).
- Earlier Phase 4.x deliveries unchanged in substance — presentation/media verbs, continuation refactor, validation, storage completion (see Roadmap below and existing closed walkthroughs).

### Active Work
- **Primary:** Close remaining **navigation** gaps (`/jump` / `/fork` variadic transfer) per trimmed [20260227-phase5-concurrency-context-signal-gaps-fix-plan.md](20260227-phase5-concurrency-context-signal-gaps-fix-plan.md); follow child nav for Phase **6** (channels) and other audit lines.
- **Parallel (Phase 5 of *this* roadmap):** integration scenarios, storage backends, docs, performance — still largely ahead (see milestones).

### Known Blockers
- None technical; sequencing is prioritization between compliance fixes (child nav) and integration/polish milestones below.

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
- [x] Expression Evaluation (Operators, Interpolation, Special forms) — *ongoing spec tweaks tracked in child nav (e.g. list `+`, format suffixes implemented)*
- [x] Core Verbs (Flow control, Collection manipulation, Debugging)
- [x] Core Runtime Features (AST nodes, Value parsing, Attribute parsing)

---

### Phase 3: Control Flow & Concurrency — [Status: Done]

**Goal:** Enable complex story flows, parallel execution, and inter-context communication.

**Milestones:**
- [x] **Control Flow Verbs** — `If`, `Switch`, `Loop`, `While`, `Foreach`, `Sequence` — *initial feature set; March 2026 work closed major spec deviations (see closed Phase 4 umbrella).*
- [x] **Context & Navigation** — `Jump`, `Fork`, `Call`, `Exit`, `Sleep`.
  - Execution: [Walkthrough](closed/20260207-context-navigation-walkthrough.md)
  - *Spec note:* **`/jump`** still accepts only 1–2 args (no trailing `*var` transfer). **`/call`** supports trailing references + `[inline]` (see child nav / git `cf4f5bd`). **`/fork`** still 1–2 args only.
- [x] **Channel System** — `ChannelManager`, `Push`, `Pull`, `Close`.
  - Execution: [Walkthrough](closed/20260207-channel-racecond-walkthrough.md)
  - *Spec note:* blocking pull and timeouts — child nav Phase 6.
- [x] **Signal System** — `SignalManager`, `Wait`, `Signal`.
  - Plan: [20260207-signal-system-plan.md](closed/20260207-signal-system-plan.md)
  - Execution: [Walkthrough](closed/20260207-signal-system-walkthrough.md)
  - *Spec note:* `/wait` **`timeout:`** supported (`WaitDriver` + `2c74be7`); audit nav updated.

---

### Phase 4: Runtime Architecture — [Status: Done]

**Goal:** Formalize the runtime into a cohesive, extensible architecture with handler registries, a compilation pipeline, persistence, validation, and the standard verb interface — following `impl/09_runtime.md` through `impl/12_validation.md`.

**Milestone sequencing rationale:** Runtime Core establishes the handler-registry pattern that Storage, Validation, and Standard Verbs plug into. Storage is nearly complete (Read/Write drivers + InMemoryStorage exist). Validation provides developer guardrails before we surface the host-facing Standard Verbs.

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

### Phase 5: Integration, Spec Closure & Polish — [Status: In Progress]

**Goal:** Production readiness: correctness vs `spec/`, stability, usability, and non–InMemory storage.

**Milestones:**
- [ ] **Conformance backlog** — Line-item gaps vs `spec/` and `impl/`, prioritized in [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md). Next focused work: [20260227-phase5-concurrency-context-signal-gaps-fix-plan.md](20260227-phase5-concurrency-context-signal-gaps-fix-plan.md) (**remaining:** `/jump` + `/fork` variadic transfer; plan body superseded for `/flag`, `/wait`, `/call`).
- [ ] **Integration Testing** — End-to-end scenarios from `13_testing.md`; multi-story, multi-context flows
- [ ] **Storage Backends** — File-based backend (`.zohstore` files), optional SQLite backend
- [ ] **Hardening** — Resource limits, defer/error semantics, safe expression patterns — cross-check child nav and historical `spec/.projex/closed/` red-team / inconsistency work
- [ ] **Documentation** — Public API docs, usage examples, verb reference
- [ ] **Performance** — Profiling, optimization, benchmark suite

---

## Priorities

**Current focus:** Drive **spec compliance** for concurrency/navigation/signals (Phase 5 plan above + child nav Phases 5–6) while keeping **integration testing** and **storage backends** in view for reference-runtime usefulness.

**Next up:** Implement `/jump`/`/fork` var transfer (or close plan as obsolete with a patch-projex); then channel semantics (pull/blocking/timeout) per audit nav; continue hardening as surfaced by audits.

**Deferred:** Performance optimizations and broad doc polish until critical spec gaps shrink.

---

## Open Questions

- [ ] Implement **`/jump` + `/fork` var transfer** in one change set or split by verb for review?
- [x] Does the current `pull` implementation return a result object (old spec) or value/error (new spec)? **(Answer: Value/Error via VerbResult)**
- [x] Are there other discrepancies from the recent "Spec/Impl Inconsistencies" finding that need to be addressed in C#? **(Answer: Addressed via multiple patches; new gaps tracked in child audit nav)**
- [x] Should Phase 4 begin with Runtime Core or Standard Verbs? **(Answer: Runtime Core — it defines the handler registries that Standard Verbs plug into)**
- [x] What persistence mechanism should be used for Save/Load? **(Answer: InMemoryStorage for Phase 4 development/testing; file/SQLite backends deferred to Phase 5)**
- [x] Should verb validators be opt-in (registered per verb) or mandatory (auto-generated from signatures)? **(Answer: Opt-in, explicitly implemented per verb as specific classes, e.g. `SetValidator`)**
- [x] How should interactive drivers handle async host responses (e.g., user choosing from a menu)? **(Answer: Return a Continuation discriminated union so the runtime can decide how to fulfill it without a monolithic handler, while providing driver-specific interfaces for the presentation logic itself)**

---

## Git history (evidence)

Use the **nested** repository:

```bash
cd csharp
git log --oneline -40
git log --oneline --date=short --format="%h %ad %s" -- src/Zoh.Runtime/Verbs/
```

**Representative commits** (authoritative for “what landed when”; hashes from `main` as of 2026-03-26):

| When | Theme | Example `git` |
|------|--------|----------------|
| 2026-03-25–26 | Phase 4 control-flow compliance (`/if`, `/switch`, `/foreach`, `breakif`, `/do`, suspend/fatal) | `6b3de81` … `bbc3ef2` |
| 2026-03-25 | `core.nav` / `core.signal` namespace alignment | `ca35285` |
| 2026-03-20 | Try/suspension wrapping; presentation wait diagnostics; diagnostic `invalid_params` / `invalid_type`; **timeout consistency** (7 drivers) | `cf349b2`, `d733253`, `2dc45e2`, `2c74be7` |
| 2026-03-16 | **`/flag`** + runtime-scoped flags; embed path interpolation / preprocessor | `451d387`, `79f6ad8` |
| 2026-03-15 | Flow verb error-path tests | `d5bfe11` |
| 2026-03-07 | **`/call` `[inline]`** join handle fix | `cf4f5bd` |

Test count in commit messages climbed **665 → 719** across this window (`cf349b2` → `bbc3ef2`); run `dotnet test` on `Zoh.Tests` for the current number.

---

## Revision Log

| Date | Summary of Changes |
|------|---------------------|
| 2026-02-07 | Initial roadmap created by converting `csharp/task.md`. |
| 2026-02-13 | Phase 3 marked complete. Signal System, Story Header Parsing, Virtual Tokens, CRLF handling, and multiple quality improvements completed. Ready for Phase 4. |
| 2026-02-14 | Phase 4 restructured into five sequenced milestones (4.1–4.5) based on dependency analysis. Resolved ordering and persistence open questions. Added new questions for presentation handler async model and validator strategy. Red-team remediation items and storage backends moved to Phase 5. Updated test count to 515. |
| 2026-02-15 | Phase 4.1 (Runtime Core) and 4.2 (Storage Completion) marked complete. Updated Active Work to Phase 4.3 (Validation Pipeline). |
| 2026-02-22 | Phase 4.3 marked complete based on `20260216-validation-pipeline-walkthrough.md`. Updated Active Work to Phase 4.4 (Standard Verbs - Presentation). Resolved open questions regarding verb validators and async host responses. |
| 2026-02-22 | Adopted Per-Driver continuation model for Phase 4.4 and 4.5, replacing monolithic `IPresentationHandler` and `IMediaHandler` with driver-specific interfaces to maximize logic reuse. |
| 2026-02-22 | Phase 4.4 (Standard Verbs - Presentation) marked complete based on `20260222-std-verbs-presentation-csharp-walkthrough.md`. Updated Active Work to Phase 4.5 (Standard Verbs - Media). Logged architectural refactor of `VerbContinuation` discriminated union. |
| 2026-02-23 | Phase 4.5 (Standard Verbs - Media) marked complete based on `20260222-std-verbs-media-csharp-walkthrough.md`. Phase 4 is now fully complete. Updated Active Work to Phase 5 (Integration & Polish). |
| 2026-03-26 | Navigate-projex: child link to spec audit nav; Phase 5 reframed (conformance + integration/polish); Current Position through March 2026 (719 tests, control-flow umbrella closed); Phase 3 notes for remaining spec gaps; removed stale/broken Related Projex links; fixed duplicate Open Questions heading; Priorities aligned with Phase 5 gap plan. |
| 2026-03-26 | Cross-checked **`csharp/` nested git log**: added Git history (evidence) section, commit index, nested-repo note; corrected Phase 5 “remaining work” (`/flag`, `/wait`, `/call` already shipped; `/jump`/`/fork` transfer still open). |
