# C# Runtime Compliance Audit Roadmap

> **Created:** 2026-02-23 | **Last Revised:** 2026-04-28
> **Author:** Antigravity
> **Scope:** Full spec compliance audit of the ZOH C# Reference Implementation
> **Parent Navigation:** [20260207-csharp-runtime-nav.md](20260207-csharp-runtime-nav.md)
> **Evidence index:** Many `closed/*.md` citations below are **filenames only** (projex convention). In this checkout, bodies often live in the summarized archive — see [2603261730-csharp-closed-archive.md](closed/2603261730-csharp-closed-archive.md) — not as separate files next to this nav.
> **Related Projex:** [2603261730-csharp-closed-archive.md](closed/2603261730-csharp-closed-archive.md) (indexes **closed** Phase 5 plan `20260227-phase5-concurrency-context-signal-gaps-fix-plan.md`, log `2603261500-phase5-concurrency-context-signal-gaps-fix-log.md`, walkthrough `2603261500-phase5-concurrency-context-signal-gaps-fix-walkthrough.md`), [20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md), [20260228-rng-parse-gaps-fix-plan.md](closed/20260228-rng-parse-gaps-fix-plan.md), [20260228-rng-parse-gaps-fix-log.md](closed/20260228-rng-parse-gaps-fix-log.md), [20260225-string-interpolation-formatting-plan.md](20260225-string-interpolation-formatting-plan.md), [20260226-interpolation-format-regex-free-eval.md](20260226-interpolation-format-regex-free-eval.md), [20260228-interpolate-feature-interleaving-eval.md](20260228-interpolate-feature-interleaving-eval.md), [2603261500-20260227-phase5-concurrency-context-signal-gaps-fix-plan-review.md](2603261500-20260227-phase5-concurrency-context-signal-gaps-fix-plan-review.md) (pre-landing plan review; historical)

---

## Vision

To systematically and rigorously verify that the ZOH C# Reference Implementation strictly adheres to every detail of the language specifications (`spec/`). This audit ensures the runtime is a completely accurate reference for how ZOH should behave, validating edge cases, type constraints, concurrency models, and standard feature parity.

---

## Current Position

**As of 2026-03-31:**

The audit baseline still spans Phases 1–9. **Phase 4 control-flow gaps** remain **closed** as before (umbrella [20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md)). **Phase 2.5 / 2.6** list `+` and interpolation format suffixes remain implemented in `ExpressionEvaluator`. **Phase 5:** `/flag`, `/wait timeout:`, `/call` trailing `*var` + `[inline]` were already in place; `**/jump` and `/fork` trailing `*var` transfer** shipped (`csharp/` git `0fbf43c`) with execution record in the **closed** Phase 5 log/walkthrough filenames indexed under [2603261730-csharp-closed-archive.md](closed/2603261730-csharp-closed-archive.md). Original plan `20260227-phase5-concurrency-context-signal-gaps-fix-plan.md` is **closed** (archived — not a loose file here). **Primary audit pressure now:** Phase **6** — blocking `/pull`, real rendezvous `**timeout` on pull and push** (`ChannelVerbs.cs`: both parse `timeout` but `**timeoutMs` is unused** for positive waits; empty pull returns `nothing` immediately). Then Phase **7** (`EraseDriver` multi-var abort), **8** (`/error` vs `/fatal`), **9** (`/focus` / `/unfocus`). **Phase 3.2 `/first` and 3.3 `/increase`/`/decrease`** previously listed gaps are **implemented** in current `FirstDriver` / `IncreaseDriver` (verb execution + `invalid_type` on bad amount); optional: confirm test coverage depth.

### Recent Progress

- Phase 4 (audit-listed gaps): `/if` subject/`else`/`is`, `/switch` verb cases, `/foreach` `*ref` iterator, `FlowUtils` condition evaluation + suspend/fatal propagation (loops, `/while`, `/sequence` test), `/do` second hop — see Phase 4 milestone links below.
- Phase 3.4 RNG/parse fixes and verification unchanged from prior checkpoint (20260228-rng-parse-gaps-fix-log.md).

### Active Work

- None tied to Phase 2.5/2.6 or Phase 3.2/3.3 verb-literal gaps (runtime behavior updated). **Channels:** design/implement blocking pull + wired timeouts (`ChannelVerbs.cs` / `ChannelManager`). Optional: extra tests for **multiple** trailing `*var` on `/jump`/`/fork` (NavigationTests/ConcurrencyTests cover single-ref happy paths + fatals). Historical design notes: [20260225-string-interpolation-formatting-plan.md](20260225-string-interpolation-formatting-plan.md), [20260226-interpolation-format-regex-free-eval.md](20260226-interpolation-format-regex-free-eval.md).

### Known Blockers

- No hard external blockers. Remaining work is prioritization and implementation effort across non–Phase-4 gaps.

---

## Roadmap

### Phase 1: Anatomy, Parsing & Preprocessing - [Status: Done]

**Goal:** Ensure ZOH syntax and physical script structures are parsed perfectly.

**Milestones:**

- **1.1 Script Anatomy:** Standard vs. Block verb forms, whitespace/newline tolerance, and comments.
  - Execution: Verified Lexer and Parser tests (`LexerSpecComplianceTests.cs`, `ParserSpecComplianceTests.cs`). Full coverage exists for inline/block comments, whitespace skipping (newlines ignored outside strings/checkpoints), and block vs. standard verb form parsing. No gaps found.
- **1.2 Story Structure:** Story headers and Metadata entries validation.
  - Execution: Story boundaries and header logic are well-tested in `StoryHeaderParserTests` and `StoryNameLexerTests`. Metadata type validation gaps fixed via [20260224-metadata-type-validation-plan.md](closed/20260224-metadata-type-validation-plan.md). The AST-to-CompiledStory pipeline now strictly enforces allowed types and correctly reports `invalid_metadata_type` compilation diagnostics.
- **1.3 Namespaces:** Namespace resolution and forbidden ambiguity tests (`namespace_ambiguity` fatal).
  - Execution: Verified `VerbRegistry` suffix indexing and `VerbResolutionValidator`. The parser and validator correctly issue a `namespace_ambiguity` fatal diagnostic if an un-namespaced verb call matches multiple registered verb drivers. Full test coverage in `NamespaceTests.cs`. No gaps found.
- **1.4 Preprocessor - Embed:** Recursive resolution and single-embed-per-file limits.
  - Execution: Verified `EmbedPreprocessor`. Correctly uses `HashSet` to prevent circular dependencies (PRE001), throwing a fatal diagnostic if a file is embedded more than once in the compilation path. Recursive embedding and relative path resolution are fully supported and tested. No gaps found.
- **1.5 Preprocessor - Macros:** Definition, expansion, spacing/trimming, escaping (`\|`, `\%`), and positional parameters (`|%1|`, `|%+2|`, etc.).
  - Execution: Verified `MacroPreprocessor`. Correctly implements symmetric trimming, `\|` and `\%` escaping, multiline argument support, indentation preservation from the usage line, and all positional parameters (including `|%0|`, `|%|` auto-increment, and relative `|%+1|`/`|%-1|`). Comprehensive test coverage in `PreprocessorTests.cs`. No gaps found.

---

### Phase 2: Type System, Variables & Expressions - [Status: Future]

**Goal:** Validate fundamental primitives, memory, and math evaluation.

**Milestones:**

- **2.1 Variable Scopes:** Context vs. Story scope, dropping logic, and shadowing rules.
  - Execution: Verified `VariableStore` and `CoreVerbTests`. Context and Story scopes are isolated. `/set` defaults to Story scope and correctly shadows Context variables. `/drop` defaults to Story scope per spec. Missing variables return `nothing`. Complete test coverage exists in `VariableStoreTests.cs` and `CoreVerbTests.cs`. No gaps found.
- **2.2 Type Constraints:** `[typed]`, `[required]`, and `[OneOf]` attributes on variables.
  - Execution: Verified `SetDriver` correctly parses and applies these attributes. `[typed]` checks the runtime type against the target and stores the constraint; subsequent changes via `Variable.WithValue` enforce it. `[required]` successfully verifies presence when no default value is assigned. `[OneOf]` validates the provided value against a resolved list. Complete test coverage exists in `CoreVerbTests.cs`. No gaps found.
- **2.3 Type-to-String Coercion:** String formatting semantics (e.g., doubles always have `.`, `?` prints as `?`, collection stringification).
  - Execution: Verified overrides in `ZohValue` derived types. `ZohFloat` explicitly uses `InvariantCulture` and appends `.0` if no decimal exists. `ZohNothing` coercion yields `?`. `ZohBool` explicitly yields lowercase `true`/`false`. `ZohList` and `ZohMap` properly produce JSON-like representation with nested strings correctly quoted. No gaps found.
- **2.4 Nested References:** Deep indexing into lists (`*list[0]`) and maps (`*map["key"]`), implicit evaluation of indices, and undefined/missing element fallbacks (`?`).
  - Execution: Verified in `CollectionHelpers`, `SetDriver`, and `NestedAccessTests.cs`. Indices in references are correctly evaluated through `ValueResolver.Resolve`. Getting missing keys or out-of-bounds indices returns `nothing`. Type constraints enforce `integer` indices for lists and `string` keys for maps. Intermediate missing path elements correctly throw fail diagnostics during assignment without creating orphaned dictionaries. No gaps found.
- **2.5 Expressions:** Order of precedence, unary/binary operator math, list concatenation via `+` (map concatenation N/A per spec), and type coercions.
  - Execution: Verified in `ExpressionEvaluator` (and verb-level eval coverage). Math precedence is correct. Unary minus and power operators (`*`*) bind correctly. Ints silently promote to Floats on overflow or precision division. **List + list:** `EvaluateBinary` (`TokenType.Plus`) combines two `ZohList` values with `AddRange` (no longer throws for list+list).
- **2.6 Interpolation (`Std.Interpolate`):** C#-style formatting (`${*var,8:N1}`), collection unrolling (`${*list...", "}`), picking (`${1|2|3}[*i]`), and evaluation specials (`$#`, `$?`).
  - Execution: Verified in `ExpressionEvaluator`. Nested interpolations (`$#`, `$?`) and collection unrolling (`...`) are supported and tested. Option picking `$(...)[idx]` works per spec. **Format suffix:** after the inner expression, `,width` / `:format` is parsed and applied via `string.Format(InvariantCulture, …)` in `EvaluateInterpolationMatch`. Design history: [20260225-string-interpolation-formatting-plan.md](20260225-string-interpolation-formatting-plan.md), [20260226-interpolation-format-regex-free-eval.md](20260226-interpolation-format-regex-free-eval.md).

---

### Phase 3: Core Verbs (Variables, Math, & Collections) - [Status: Future]

**Goal:** Verify basic procedural actions.

**Milestones:**

- **3.1 Variable Verbs:** `/set` (with `[resolve]`), `/get`, `/drop`, `/capture`, `/type`, `/count`.
  - Execution: Verified `SetDriver`, `GetDriver`, `DropDriver`, `CaptureDriver`, `TypeDriver`, `CountDriver`. Implementation is complete and correctly handles deep paths, attributes (`[scope]`, `[typed]`, `[required]`, `[OneOf]`, `[resolve]`), and spec behaviors.
- **3.2 Collection Verbs:** `/append`, `/remove`, `/insert`, `/clear`, `/has`, `/any`, `/first`.
  - Execution: Verified `AppendDriver`, `InsertDriver`, `RemoveDriver`, `ClearDriver`, `HasDriver`, `AnyDriver`, `FirstDriver`. Most operations handle map/list access and index logic correctly.
  - **Resolved:** `FirstDriver` executes `ZohVerb` via `ExecuteVerb` and re-resolves `ZohExpr` after `ValueResolver.Resolve` (`FirstDriver.cs`).
- Phase 3.3: Mathematics Verbs (Increase/Decrease) (Completed: 2026-02-26)
  - Execution: Verified `IncreaseDriver`, `DecreaseDriver`.
  - **Resolved:** `IncreaseDriver.ModifyVariable` executes verb-literal amounts then requires `ZohInt`/`ZohFloat` or fatal `invalid_type` (`IncreaseDriver.cs`); `DecreaseDriver` delegates to the same helper with sign `-1`.
- **3.4 RNG & Parsing Verbs:** `/rand` (inclusive/exclusive), `/roll`, `/wroll`, `/parse` (robust string conversion).
  - Execution: Verified `RollDriver` and `ParseDriver`. `/wroll` now emits fatal `invalid_value` for negative weights and fatal `invalid_type` for non-integer weights. `/parse` now implements `list` and `map` parsing via JSON conversion with nested structure support and `invalid_format` diagnostics for malformed input. Coverage added in `RollTests.cs` and `ParseTests.cs` and validated by targeted/full test runs.
  - Execution evidence: [20260228-rng-parse-gaps-fix-plan.md](closed/20260228-rng-parse-gaps-fix-plan.md), [20260228-rng-parse-gaps-fix-log.md](closed/20260228-rng-parse-gaps-fix-log.md).

---

### Phase 4: Control Flow - [Status: Done]

**Goal:** Ensure branching and looping structures are unbreakable.

**Milestones:**

- **4.1 Conditional Branching:** `/if` (with `is` and `else` parameters), `/switch` (cases and `default` fallback).
  - Execution: Verified `IfDriver` and `SwitchDriver`.
  - **Resolved:** Named `else`, `is`, and verb subject semantics for `/if` — [2603251600-phase4-if-verb-subject-else-walkthrough.md](closed/2603251600-phase4-if-verb-subject-else-walkthrough.md).
  - **Resolved:** Verb-valued `/switch` case operands — [2603251601-phase4-switch-verb-case-walkthrough.md](closed/2603251601-phase4-switch-verb-case-walkthrough.md).
- **4.2 Loops:** `/loop` (fixed count, `-1` infinite), `/while` (conditional), `/foreach` (list/map iteration and scoping), `/sequence`.
  - Execution: Verified `LoopDriver`, `WhileDriver`, `ForeachDriver`, `SequenceDriver`.
  - **Resolved:** `/foreach` iterator as reference (`*it`) — [2603251602-phase4-foreach-iterator-ref-walkthrough.md](closed/2603251602-phase4-foreach-iterator-ref-walkthrough.md).
  - **Resolved:** `breakif` / `continueif` verb conditions, suspend/fatal propagation (incl. `/while`), `/sequence` regression test — [2603251825-phase4-flowutils-breakif-verb-patch.md](closed/2603251825-phase4-flowutils-breakif-verb-patch.md), [2603252130-phase4-condition-suspend-fatal-impl-walkthrough.md](closed/2603252130-phase4-condition-suspend-fatal-impl-walkthrough.md), [2603252220-phase4-sequence-breakif-verb-test-patch.md](closed/2603252220-phase4-sequence-breakif-verb-test-patch.md).
- **4.3 Execution Resolution:** `/do` for executing verb literals.
  - Execution: Verified `DoDriver`.
  - **Resolved:** Execute returned `ZohVerb` when the first hop yields a verb — [2603251810-phase4-do-returned-verb-patch.md](closed/2603251810-phase4-do-returned-verb-patch.md).
- Umbrella split / tracking (closed): [20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md).

---

### Phase 5: Concurrency, Contexts & Signals - [Status: Complete]

**Goal:** Verify context boundaries and event architectures.

**Milestones:**

- **5.1 Checkpoint Contracts:** Verification of `*var:type` typing constraints at checkpoint boundaries.
  - Execution: Verified `JumpDriver`, `ForkDriver`, and `CallDriver` successfully call `ValidateContract()` before resuming/jumping. No gaps found.
- **5.2 Navigation Verbs:** `/jump` (intra/inter-story, arg passing), `/exit`.
  - Execution: Verified `ExitDriver` correctly terminates context.
  - **Resolved:** `JumpDriver` parses story/label vs. label+refs, collects trailing `ValueAst.Reference` args, captures values with `TryGetWithScope` before cross-story `ExitStory`, applies `Set` at destination, then `ValidateContract` — `csharp/` git `0fbf43c`.
- **5.3 Parallel Contexts:** `/fork` (`[clone]` attribute, var initialization), `/call` (`[inline]`, `[clone]`, blocking behavior).
  - Execution: Verified `ForkDriver` and `CallDriver` implement `[clone]` correctly using `ctx.Clone()`.
  - **Resolved:** `ForkDriver` mirrors `JumpDriver` trailing-ref parsing and copies into the child context before contract validation — `csharp/` git `0fbf43c`.
  - **Resolved:** `CallDriver` collects trailing `ValueAst.Reference` parameters, copies values into the child before contract validation, and `**[inline]`** merges those names back from the child on join (implementation in `CallDriver.cs`; inline join fix `cf4f5bd` 2026-03-07 in `csharp/` git).
- **5.4 Time & Flags:** `/sleep`, `/flag`.
  - Execution: Verified `SleepDriver` correctly yields `SleepContinuation`.
  - **Resolved:** `FlagDriver` registered (`VerbRegistry`) — runtime-scoped flags (`451d387` 2026-03-16, `csharp/` git).
- **5.5 Signal System:** `/wait` (timeout handling), `/signal` (cross-context broadcasts).
  - Execution: Verified `SignalDriver` correctly uses `Broadcast` and returns the number of woken contexts.
  - **Resolved:** `WaitDriver` reads named `timeout`, maps to `MessageContinuation` / `SignalRequest` (`2c74be7` timeout-consistency sweep + current `WaitDriver.cs`, `csharp/` git).
- **Closed:** Plan `20260227-phase5-concurrency-context-signal-gaps-fix-plan.md` + execution `2603261500-phase5-concurrency-context-signal-gaps-fix-log.md` / `2603261500-phase5-concurrency-context-signal-gaps-fix-walkthrough.md` — summarized in [2603261730-csharp-closed-archive.md](closed/2603261730-csharp-closed-archive.md) (not separate files in this checkout).

---

### Phase 6: Channels Architecture - [Status: Future]

**Goal:** Guarantee concurrent-safe FIFO pipe behaviors.

**Milestones:**

- **6.1 Channel Lifecycle:** `/open`, `/close` (instant wake-up of sleeping pullers, generation IDs).
  - Execution: Verified `OpenVerbDriver` and `CloseVerbDriver`. They correctly interface with `ChannelManager`. No gaps found.
- **6.2 Channel IO:** `/push` (blocking vs. `wait: false` fire-and-forget), `/pull`.
  - Execution: `PushVerbDriver` and `PullVerbDriver` in `ChannelVerbs.cs`; `ChannelManager` rendezvous + `WaitRequest` channel records + `Context`/`ZohRuntime` wiring — landed `projex/2604270100-channel-blocking-pull-push-impl` squash on `main` (2026-04-28).
  - **Resolved (m6.2):** blocking `/pull`; default `/push` rendezvous; `timeout` on suspend path; `/close` wakes blocked parties; `Terminate` clears channel waiters.
  - **Evidence:** `2604281500-channel-blocking-pull-push-impl-walkthrough.md` | `2604281500-channel-blocking-pull-push-impl-audit.md` (audit: Partial — Step 7b matrix not fully implemented) — in `closed/`.
  - **Plans / review:** `2604270100-channel-blocking-pull-push-impl-plan.md` (in `closed/`) | `2604270124-channel-blocking-pull-push-redteam.md` | `2604270022-channel-blocking-pull-push-impl-plan.md` (superseded duplicate).
- **6.3 Channel Timeouts:** Rendezvous timeouts on both push and pull sides, returning appropriate diagnostics.
  - Execution: Named `timeout` on push/pull now flows into `ChannelWaitCondition` + `ResolveWait` cleanup (m6.2).
  - **GAP (m6.3 / broader):** timeout semantics audit on **other** verbs / cross-verb consistency / edge-case matrix beyond channel IO — channel path covered by m6.2 close set.

---

### Phase 7: Storage & State Management - [Status: Complete]

**Goal:** Test persistence implementations according to spec.

**Milestones:**

- **7.1 Deferred Execution:** `/defer` (LIFO execution, `story` vs `context` scope teardown).
  - Execution: Verified `DeferDriver`. Correctly parses the `[scope]` attribute and registers the payload with the Context defer stack. No gaps found.
- **7.2 Persistence Verification:** `/write` (type restrictions), `/read` (defaults), `/erase`, `/purge` across explicit `store:` containers.
  - Execution: Verified `WriteDriver`, `ReadDriver`, `PurgeDriver`, and `EraseDriver`. Writing actively rejects verbs and channels. Reading integrates `[required]` and `[scope]` rules.
  - **GAP:** `EraseDriver` correctly issues an `info` diagnostic if a variable to erase is not found, but it uses a hard `return` instead of `continue` in its iteration loop. This causes it to completely abort erasing any subsequent variables provided in the `/erase` call if an earlier variable fails to resolve.

---

### Phase 8: Diagnostics & Debugging - [Status: Complete]

**Goal:** Ensure errors are trapped, reported, or escalated properly.

**Milestones:**

- **8.1 Debug Logging:** `/info`, `/warning`, `/error`, `/fatal`.
  - Execution: Verified `DebugDriver`. All four verbs are implemented and registered via `VerbRegistry.RegisterCoreVerbs()`, sharing a single `DebugDriver` instance.
  - **GAP:** `/error` and `/fatal` collapse to the same code path (`DiagnosticSeverity.Error` -> `VerbResult.Fatal`), producing identical behavior. The spec likely intends `/error` to emit an error-severity diagnostic (non-fatal) while `/fatal` terminates execution - the implementation does not distinguish between them.
- **8.2 Diagnostic Trapping:** `/try` (downgrading fatals, `catch:` execution, `[suppress]`), `/diagnose`.
  - Execution: Verified `TryDriver` and `DiagnoseDriver`. `TryDriver` properly checks the `catch:` named parameter and the `[suppress]` attribute to intercept and downgrade fatals. `DiagnoseDriver` correctly extracts `context.LastDiagnostics` and bundles them into a `ZohMap` grouped by severity. No gaps found.
- **8.3 Assertions:** `/assert` (truthy vs falsy resolution, formatting messages).
  - Execution: Verified `AssertDriver` handles conditional matching, correctly interpolates the optional failure message using `ZohInterpolator`, and throws `assertion_failed` fatals upon failure. No gaps found.

---

### Phase 9: Standard Verbs & Attributes - [Status: Complete]

**Goal:** Test the decoupled standard vocabulary that most ZOH scripts rely on.

**Milestones:**

- **9.1 Presentation Layer:** `/converse`, `/choose`, `/chooseFrom`, `/prompt`, `/focus`, `/unfocus` (including timeouts and presentation attributes like `[Wait]`, `[Style]`, `[By]`).
  - Execution: Verified `ConverseDriver`, `ChooseDriver`, `ChooseFromDriver`, and `PromptDriver` correctly parse presentation attributes and issue host continuations.
  - **GAP:** `/focus` and `/unfocus` verbs are completely unimplemented. There are no drivers for them.
- **9.2 Media Layer:** `/show`, `/hide` (including transforms, anchor, fade values), `/play`, `/playOne`, `/stop`, `/pause`, `/resume`, `/setVolume`.
  - Execution: Verified `ShowDriver`, `HideDriver`, `PlayDriver`, `PlayOneDriver`, `StopDriver`, `PauseDriver`, `ResumeDriver`, and `SetVolumeDriver`. All media drivers interface correctly with their respective handlers and parse transformation attributes successfully. No gaps found.
- **9.3 Standard Attributes Mapping:** Verify global attribute behavior (e.g., `resolve`, `required`, `scope`).
  - Execution: Verified implementation across drivers. `[resolve]` operates correctly in `SetDriver`. `[required]` is enforced in `ReadDriver` and `GetDriver`. `[scope]` handles `story` and `context` boundaries appropriately in `SetDriver` and `DeferDriver`. No gaps found.

---

## Priorities

**Current focus:** Phase **6** — blocking `/pull`, end-to-end channel `**timeout` semantics** (push + pull) vs spec; likely needs continuation/async alignment with `ChannelManager`.

**Next up:**

1. Plan/patch for Phase 6.2–6.3; regression tests against spec (incl. push timeout if spec requires wait).
2. Phase 7 `EraseDriver` loop abort; Phase 8 `/error` vs `/fatal`; Phase 9 `/focus`/`/unfocus`.
3. Optional: multi-ref `/jump`/`/fork` transfer tests; list `+` / interpolation format suffix coverage review.

**Deferred:**

- Broad Phase 9 presentation sweep beyond documented gaps until higher-severity runtime gaps shrink.

---

## Open Questions

- Prefer one umbrella plan per phase cluster (5, 6, …) with child plans, or single multi-phase plans with staged objectives?

---

## Revision Log


| Date       | Summary of Changes                                                                                                                                                                                                                                                                                                                                                                   |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2026-02-23 | Initial roadmap created based on spec audit.                                                                                                                                                                                                                                                                                                                                         |
| 2026-02-23 | Revised to provide greater granularity, mapping directly to specific features in the specification across 9 detailed phases.                                                                                                                                                                                                                                                         |
| 2026-02-25 | Corrected GAP 8.1 (DebugDriver exists and is registered). Restructured all milestones to separate gap bullets from execution prose for readability.                                                                                                                                                                                                                                  |
| 2026-02-28 | Updated status checkpoint after Phase 3.4 remediation: marked `/wroll` and `/parse` gaps resolved, added evidence links, refreshed current position/priorities, and replaced stale open questions.                                                                                                                                                                                   |
| 2026-03-26 | Navigate-projex revision: Phase 4 audit gaps marked resolved with closed walkthrough/patch links; Phase 1 status corrected to Done; current position, priorities, open questions, and related projex updated for post–Phase-4 focus (5–6, 2.5/2.6, remaining milestone gaps).                                                                                                        |
| 2026-03-26 | Corrected Phase 2.5/2.6: list `+` and interpolation `,width`/`format` suffixes are implemented in `ExpressionEvaluator`; removed stale GAP bullets; adjusted priorities and active work.                                                                                                                                                                                             |
| 2026-03-26 | Phase 4 umbrella plan moved to `closed/20260227-phase4-control-flow-gaps-fix-plan.md`; related links in this nav updated.                                                                                                                                                                                                                                                            |
| 2026-03-26 | Phase 5 milestones reconciled with `**csharp/` git** and code: resolved `/flag`, `/wait timeout`, `/call` transfer+`[inline]`; remaining gaps `/jump`/`/fork` transfer; priorities updated.                                                                                                                                                                                          |
| 2026-03-31 | Navigate-projex revision: `/jump` and `/fork` trailing `*var` transfer marked resolved (`0fbf43c`, `JumpDriver`/`ForkDriver`); current position and priorities repointed at Phase 6 and remaining gaps; related projex links added (Phase 5 plan + plan review).                                                                                                                     |
| 2026-03-31 | Parallel **explore-projex** sweep: added **evidence index** + `2603261730-csharp-closed-archive.md` link; Phase 3.2/3.3 gaps cleared (`FirstDriver`, `IncreaseDriver`/`DecreaseDriver`); Phase 6 status → **Future**; refined 6.2/6.3 (pull non-blocking, **push** timeout unused, positive `timeout` unused on both); priorities/active work updated; jump/fork test coverage note. |
| 2026-03-31 | Phase 5 plan `20260227-phase5-concurrency-context-signal-gaps-fix-plan.md` documented as **closed/archived** (index + log/walkthrough filenames); removed stale “reconcile plan” priority; Related Projex no longer links it as an active artifact.                                                                                                                                  |
| 2026-04-28 | Phase 6.2: linked active plan `2604270100-channel-blocking-pull-push-impl-plan.md`, redteam `2604270124-channel-blocking-pull-push-redteam.md`, superseded `2604270022-channel-blocking-pull-push-impl-plan.md`; canonical plan gains redteam Step 7a→7b test gate.                                                                                                                  |
| 2026-04-28 | Phase 6.2 runtime gaps cleared (squash `projex/2604270100-channel-blocking-pull-push-impl`); walkthrough `2604281500-channel-blocking-pull-push-impl-walkthrough.md`, audit `2604281500-channel-blocking-pull-push-impl-audit.md`; milestone bullets refreshed.                                                                                                                      |
