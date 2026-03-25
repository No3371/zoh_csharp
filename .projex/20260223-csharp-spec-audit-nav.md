# C# Runtime Compliance Audit Roadmap

> **Created:** 2026-02-23 | **Last Revised:** 2026-03-26
> **Author:** Antigravity
> **Scope:** Full spec compliance audit of the ZOH C# Reference Implementation
> **Parent Navigation:** [20260207-csharp-runtime-nav.md](20260207-csharp-runtime-nav.md)
> **Related Projex:** [20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md), [20260228-rng-parse-gaps-fix-plan.md](closed/20260228-rng-parse-gaps-fix-plan.md), [20260228-rng-parse-gaps-fix-log.md](closed/20260228-rng-parse-gaps-fix-log.md), [20260225-string-interpolation-formatting-plan.md](20260225-string-interpolation-formatting-plan.md), [20260226-interpolation-format-regex-free-eval.md](20260226-interpolation-format-regex-free-eval.md), [20260228-interpolate-feature-interleaving-eval.md](20260228-interpolate-feature-interleaving-eval.md)

---

## Vision

To systematically and rigorously verify that the ZOH C# Reference Implementation strictly adheres to every detail of the language specifications (`spec/`). This audit ensures the runtime is a completely accurate reference for how ZOH should behave, validating edge cases, type constraints, concurrency models, and standard feature parity.

---

## Current Position

**As of 2026-03-26:**

The audit baseline still spans Phases 1–9. **Phase 4 control-flow gaps** called out in this roadmap (`/if`, `/switch`, `/foreach` iterator reference, `breakif`/`continueif` verb conditions with suspend/fatal alignment, `/do` returned-verb hop) are **closed** under the umbrella split in [20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md), with walkthroughs and patches in `csharp/projex/closed/` (latest: condition propagation walkthrough dated 2026-03-26; full suite **719** passed per that record). **Phase 2.5 list `+` and Phase 2.6 interpolation format suffixes** are implemented in `ExpressionEvaluator` (see milestones below). **Phase 5 (partial refresh via `csharp/` git, 2026-03-26):** `/flag`, `/wait timeout:`, `/call` trailing `*var` + `[inline]` are **implemented** (e.g. commits `451d387`, `2c74be7`, `cf4f5bd`; see parent [20260207-csharp-runtime-nav.md](20260207-csharp-runtime-nav.md) Git section). **Still open:** `/jump` and `/fork` variadic transfer, Phase **6** channel pull semantics/timeouts, plus Phase 3/7/8/9 items below.

### Recent Progress
- Phase 4 (audit-listed gaps): `/if` subject/`else`/`is`, `/switch` verb cases, `/foreach` `*ref` iterator, `FlowUtils` condition evaluation + suspend/fatal propagation (loops, `/while`, `/sequence` test), `/do` second hop — see Phase 4 milestone links below.
- Phase 3.4 RNG/parse fixes and verification unchanged from prior checkpoint (20260228-rng-parse-gaps-fix-log.md).

### Active Work
- None tied to Phase 2.5/2.6 runtime gaps (those behaviors exist in code). Optional: add focused expression/interpolation tests if coverage review finds gaps. Historical design notes: [20260225-string-interpolation-formatting-plan.md](20260225-string-interpolation-formatting-plan.md), [20260226-interpolation-format-regex-free-eval.md](20260226-interpolation-format-regex-free-eval.md).

### Known Blockers
- No hard external blockers. Remaining work is prioritization and implementation effort across non–Phase-4 gaps.

---

## Roadmap

### Phase 1: Anatomy, Parsing & Preprocessing - [Status: Done]

**Goal:** Ensure ZOH syntax and physical script structures are parsed perfectly.

**Milestones:**
- [x] **1.1 Script Anatomy:** Standard vs. Block verb forms, whitespace/newline tolerance, and comments.
  - Execution: Verified Lexer and Parser tests (`LexerSpecComplianceTests.cs`, `ParserSpecComplianceTests.cs`). Full coverage exists for inline/block comments, whitespace skipping (newlines ignored outside strings/checkpoints), and block vs. standard verb form parsing. No gaps found.
- [x] **1.2 Story Structure:** Story headers and Metadata entries validation.
  - Execution: Story boundaries and header logic are well-tested in `StoryHeaderParserTests` and `StoryNameLexerTests`. Metadata type validation gaps fixed via [20260224-metadata-type-validation-plan.md](closed/20260224-metadata-type-validation-plan.md). The AST-to-CompiledStory pipeline now strictly enforces allowed types and correctly reports `invalid_metadata_type` compilation diagnostics.
- [x] **1.3 Namespaces:** Namespace resolution and forbidden ambiguity tests (`namespace_ambiguity` fatal).
  - Execution: Verified `VerbRegistry` suffix indexing and `VerbResolutionValidator`. The parser and validator correctly issue a `namespace_ambiguity` fatal diagnostic if an un-namespaced verb call matches multiple registered verb drivers. Full test coverage in `NamespaceTests.cs`. No gaps found.
- [x] **1.4 Preprocessor - Embed:** Recursive resolution and single-embed-per-file limits.
  - Execution: Verified `EmbedPreprocessor`. Correctly uses `HashSet` to prevent circular dependencies (PRE001), throwing a fatal diagnostic if a file is embedded more than once in the compilation path. Recursive embedding and relative path resolution are fully supported and tested. No gaps found.
- [x] **1.5 Preprocessor - Macros:** Definition, expansion, spacing/trimming, escaping (`\|`, `\%`), and positional parameters (`|%1|`, `|%+2|`, etc.).
  - Execution: Verified `MacroPreprocessor`. Correctly implements symmetric trimming, `\|` and `\%` escaping, multiline argument support, indentation preservation from the usage line, and all positional parameters (including `|%0|`, `|%|` auto-increment, and relative `|%+1|`/`|%-1|`). Comprehensive test coverage in `PreprocessorTests.cs`. No gaps found.

---

### Phase 2: Type System, Variables & Expressions - [Status: Future]

**Goal:** Validate fundamental primitives, memory, and math evaluation.

**Milestones:**
- [x] **2.1 Variable Scopes:** Context vs. Story scope, dropping logic, and shadowing rules.
  - Execution: Verified `VariableStore` and `CoreVerbTests`. Context and Story scopes are isolated. `/set` defaults to Story scope and correctly shadows Context variables. `/drop` defaults to Story scope per spec. Missing variables return `nothing`. Complete test coverage exists in `VariableStoreTests.cs` and `CoreVerbTests.cs`. No gaps found.
- [x] **2.2 Type Constraints:** `[typed]`, `[required]`, and `[OneOf]` attributes on variables.
  - Execution: Verified `SetDriver` correctly parses and applies these attributes. `[typed]` checks the runtime type against the target and stores the constraint; subsequent changes via `Variable.WithValue` enforce it. `[required]` successfully verifies presence when no default value is assigned. `[OneOf]` validates the provided value against a resolved list. Complete test coverage exists in `CoreVerbTests.cs`. No gaps found.
- [x] **2.3 Type-to-String Coercion:** String formatting semantics (e.g., doubles always have `.`, `?` prints as `?`, collection stringification).
  - Execution: Verified overrides in `ZohValue` derived types. `ZohFloat` explicitly uses `InvariantCulture` and appends `.0` if no decimal exists. `ZohNothing` coercion yields `?`. `ZohBool` explicitly yields lowercase `true`/`false`. `ZohList` and `ZohMap` properly produce JSON-like representation with nested strings correctly quoted. No gaps found.
- [x] **2.4 Nested References:** Deep indexing into lists (`*list[0]`) and maps (`*map["key"]`), implicit evaluation of indices, and undefined/missing element fallbacks (`?`).
  - Execution: Verified in `CollectionHelpers`, `SetDriver`, and `NestedAccessTests.cs`. Indices in references are correctly evaluated through `ValueResolver.Resolve`. Getting missing keys or out-of-bounds indices returns `nothing`. Type constraints enforce `integer` indices for lists and `string` keys for maps. Intermediate missing path elements correctly throw fail diagnostics during assignment without creating orphaned dictionaries. No gaps found.
- [x] **2.5 Expressions:** Order of precedence, unary/binary operator math, list concatenation via `+` (map concatenation N/A per spec), and type coercions.
  - Execution: Verified in `ExpressionEvaluator` (and verb-level eval coverage). Math precedence is correct. Unary minus and power operators (`**`) bind correctly. Ints silently promote to Floats on overflow or precision division. **List + list:** `EvaluateBinary` (`TokenType.Plus`) combines two `ZohList` values with `AddRange` (no longer throws for list+list).
- [x] **2.6 Interpolation (`Std.Interpolate`):** C#-style formatting (`${*var,8:N1}`), collection unrolling (`${*list...", "}`), picking (`${1|2|3}[*i]`), and evaluation specials (`$#`, `$?`).
  - Execution: Verified in `ExpressionEvaluator`. Nested interpolations (`$#`, `$?`) and collection unrolling (`...`) are supported and tested. Option picking `$(...)[idx]` works per spec. **Format suffix:** after the inner expression, `,width` / `:format` is parsed and applied via `string.Format(InvariantCulture, …)` in `EvaluateInterpolationMatch`. Design history: [20260225-string-interpolation-formatting-plan.md](20260225-string-interpolation-formatting-plan.md), [20260226-interpolation-format-regex-free-eval.md](20260226-interpolation-format-regex-free-eval.md).

---

### Phase 3: Core Verbs (Variables, Math, & Collections) - [Status: Future]

**Goal:** Verify basic procedural actions.

**Milestones:**
- [x] **3.1 Variable Verbs:** `/set` (with `[resolve]`), `/get`, `/drop`, `/capture`, `/type`, `/count`.
  - Execution: Verified `SetDriver`, `GetDriver`, `DropDriver`, `CaptureDriver`, `TypeDriver`, `CountDriver`. Implementation is complete and correctly handles deep paths, attributes (`[scope]`, `[typed]`, `[required]`, `[OneOf]`, `[resolve]`), and spec behaviors.
- [x] **3.2 Collection Verbs:** `/append`, `/remove`, `/insert`, `/clear`, `/has`, `/any`, `/first`.
  - Execution: Verified `AppendDriver`, `InsertDriver`, `RemoveDriver`, `ClearDriver`, `HasDriver`, `AnyDriver`, `FirstDriver`. Most operations handle map/list access and index logic correctly.
  - **GAP:** `FirstDriver.cs` does not dynamically evaluate expression arguments or recursively execute verb literal arguments per the `Core.First` spec ("In case of `/verb`, it takes the return value of the verb"). It currently yields the unevaluated `ZohVerb` or `ZohExpr` object.
- [x] Phase 3.3: Mathematics Verbs (Increase/Decrease) (Completed: 2026-02-26)
  - Execution: Verified `IncreaseDriver`, `DecreaseDriver`.
  - **GAP 1:** The driver silently ignores the `amount` parameter if it resolves to a non-numeric type (e.g. string) instead of throwing an `invalid_type` diagnostic, defaulting to `1`.
  - **GAP 2:** Like `FirstDriver`, it does not recursively execute `ZohVerb` parameter inputs when a `/verb` literal is provided for the amount, leading to the same silent fallback to `1`.
- [x] **3.4 RNG & Parsing Verbs:** `/rand` (inclusive/exclusive), `/roll`, `/wroll`, `/parse` (robust string conversion).
  - Execution: Verified `RollDriver` and `ParseDriver`. `/wroll` now emits fatal `invalid_value` for negative weights and fatal `invalid_type` for non-integer weights. `/parse` now implements `list` and `map` parsing via JSON conversion with nested structure support and `invalid_format` diagnostics for malformed input. Coverage added in `RollTests.cs` and `ParseTests.cs` and validated by targeted/full test runs.
  - Execution evidence: [20260228-rng-parse-gaps-fix-plan.md](closed/20260228-rng-parse-gaps-fix-plan.md), [20260228-rng-parse-gaps-fix-log.md](closed/20260228-rng-parse-gaps-fix-log.md).

---

### Phase 4: Control Flow - [Status: Done]

**Goal:** Ensure branching and looping structures are unbreakable.

**Milestones:**
- [x] **4.1 Conditional Branching:** `/if` (with `is` and `else` parameters), `/switch` (cases and `default` fallback).
  - Execution: Verified `IfDriver` and `SwitchDriver`.
  - **Resolved:** Named `else`, `is`, and verb subject semantics for `/if` — [2603251600-phase4-if-verb-subject-else-walkthrough.md](closed/2603251600-phase4-if-verb-subject-else-walkthrough.md).
  - **Resolved:** Verb-valued `/switch` case operands — [2603251601-phase4-switch-verb-case-walkthrough.md](closed/2603251601-phase4-switch-verb-case-walkthrough.md).
- [x] **4.2 Loops:** `/loop` (fixed count, `-1` infinite), `/while` (conditional), `/foreach` (list/map iteration and scoping), `/sequence`.
  - Execution: Verified `LoopDriver`, `WhileDriver`, `ForeachDriver`, `SequenceDriver`.
  - **Resolved:** `/foreach` iterator as reference (`*it`) — [2603251602-phase4-foreach-iterator-ref-walkthrough.md](closed/2603251602-phase4-foreach-iterator-ref-walkthrough.md).
  - **Resolved:** `breakif` / `continueif` verb conditions, suspend/fatal propagation (incl. `/while`), `/sequence` regression test — [2603251825-phase4-flowutils-breakif-verb-patch.md](closed/2603251825-phase4-flowutils-breakif-verb-patch.md), [2603252130-phase4-condition-suspend-fatal-impl-walkthrough.md](closed/2603252130-phase4-condition-suspend-fatal-impl-walkthrough.md), [2603252220-phase4-sequence-breakif-verb-test-patch.md](closed/2603252220-phase4-sequence-breakif-verb-test-patch.md).
- [x] **4.3 Execution Resolution:** `/do` for executing verb literals.
  - Execution: Verified `DoDriver`.
  - **Resolved:** Execute returned `ZohVerb` when the first hop yields a verb — [2603251810-phase4-do-returned-verb-patch.md](closed/2603251810-phase4-do-returned-verb-patch.md).

- Umbrella split / tracking (closed): [20260227-phase4-control-flow-gaps-fix-plan.md](closed/20260227-phase4-control-flow-gaps-fix-plan.md).

---

### Phase 5: Concurrency, Contexts & Signals - [Status: Complete]

**Goal:** Verify context boundaries and event architectures.

**Milestones:**
- [x] **5.1 Checkpoint Contracts:** Verification of `*var:type` typing constraints at checkpoint boundaries.
  - Execution: Verified `JumpDriver`, `ForkDriver`, and `CallDriver` successfully call `ValidateContract()` before resuming/jumping. No gaps found.
- [x] **5.2 Navigation Verbs:** `/jump` (intra/inter-story, arg passing), `/exit`.
  - Execution: Verified `ExitDriver` correctly terminates context.
  - **GAP:** `JumpDriver` still accepts only 1–2 arguments and does not apply trailing `*var` transfer into the target checkpoint context (unchanged as of 2026-03-26; see `csharp/` git).
- [x] **5.3 Parallel Contexts:** `/fork` (`[clone]` attribute, var initialization), `/call` (`[inline]`, `[clone]`, blocking behavior).
  - Execution: Verified `ForkDriver` and `CallDriver` implement `[clone]` correctly using `ctx.Clone()`.
  - **GAP:** `ForkDriver` still accepts only 1–2 args — no trailing `*var` transfer into the forked context (same class of gap as `/jump`).
  - **Resolved:** `CallDriver` collects trailing `ValueAst.Reference` parameters, copies values into the child before contract validation, and **`[inline]`** merges those names back from the child on join (implementation in `CallDriver.cs`; inline join fix `cf4f5bd` 2026-03-07 in `csharp/` git).
- [x] **5.4 Time & Flags:** `/sleep`, `/flag`.
  - Execution: Verified `SleepDriver` correctly yields `SleepContinuation`.
  - **Resolved:** `FlagDriver` registered (`VerbRegistry`) — runtime-scoped flags (`451d387` 2026-03-16, `csharp/` git).
- [x] **5.5 Signal System:** `/wait` (timeout handling), `/signal` (cross-context broadcasts).
  - Execution: Verified `SignalDriver` correctly uses `Broadcast` and returns the number of woken contexts.
  - **Resolved:** `WaitDriver` reads named `timeout`, maps to `MessageContinuation` / `SignalRequest` (`2c74be7` timeout-consistency sweep + current `WaitDriver.cs`, `csharp/` git).

- Planning evidence: [20260227-phase5-concurrency-context-signal-gaps-fix-plan.md](20260227-phase5-concurrency-context-signal-gaps-fix-plan.md).


---

### Phase 6: Channels Architecture - [Status: Complete]

**Goal:** Guarantee concurrent-safe FIFO pipe behaviors.

**Milestones:**
- [x] **6.1 Channel Lifecycle:** `/open`, `/close` (instant wake-up of sleeping pullers, generation IDs).
  - Execution: Verified `OpenVerbDriver` and `CloseVerbDriver`. They correctly interface with `ChannelManager`. No gaps found.
- [x] **6.2 Channel IO:** `/push` (blocking vs. `wait: false` fire-and-forget), `/pull`.
  - Execution: Verified `PushVerbDriver` handles values correctly.
  - **GAP:** `PullVerbDriver` implements `/pull` as strictly non-blocking. If the channel is empty, it returns `nothing` instantly with a comment indicating "non-blocking pull. Blocking requires async redesign". This egregiously violates the spec that `/pull` should wait until a value is available.
- [x] **6.3 Channel Timeouts:** Rendezvous timeouts on both push and pull sides, returning appropriate diagnostics.
  - Execution: Verified `PushVerbDriver` timeout path.
  - **GAP:** `PullVerbDriver` completely ignores the `timeout` named parameter, failing to provide any timeout semantics.

---

### Phase 7: Storage & State Management - [Status: Complete]

**Goal:** Test persistence implementations according to spec.

**Milestones:**
- [x] **7.1 Deferred Execution:** `/defer` (LIFO execution, `story` vs `context` scope teardown).
  - Execution: Verified `DeferDriver`. Correctly parses the `[scope]` attribute and registers the payload with the Context defer stack. No gaps found.
- [x] **7.2 Persistence Verification:** `/write` (type restrictions), `/read` (defaults), `/erase`, `/purge` across explicit `store:` containers.
  - Execution: Verified `WriteDriver`, `ReadDriver`, `PurgeDriver`, and `EraseDriver`. Writing actively rejects verbs and channels. Reading integrates `[required]` and `[scope]` rules.
  - **GAP:** `EraseDriver` correctly issues an `info` diagnostic if a variable to erase is not found, but it uses a hard `return` instead of `continue` in its iteration loop. This causes it to completely abort erasing any subsequent variables provided in the `/erase` call if an earlier variable fails to resolve.

---

### Phase 8: Diagnostics & Debugging - [Status: Complete]

**Goal:** Ensure errors are trapped, reported, or escalated properly.

**Milestones:**
- [x] **8.1 Debug Logging:** `/info`, `/warning`, `/error`, `/fatal`.
  - Execution: Verified `DebugDriver`. All four verbs are implemented and registered via `VerbRegistry.RegisterCoreVerbs()`, sharing a single `DebugDriver` instance.
  - **GAP:** `/error` and `/fatal` collapse to the same code path (`DiagnosticSeverity.Error` -> `VerbResult.Fatal`), producing identical behavior. The spec likely intends `/error` to emit an error-severity diagnostic (non-fatal) while `/fatal` terminates execution - the implementation does not distinguish between them.
- [x] **8.2 Diagnostic Trapping:** `/try` (downgrading fatals, `catch:` execution, `[suppress]`), `/diagnose`.
  - Execution: Verified `TryDriver` and `DiagnoseDriver`. `TryDriver` properly checks the `catch:` named parameter and the `[suppress]` attribute to intercept and downgrade fatals. `DiagnoseDriver` correctly extracts `context.LastDiagnostics` and bundles them into a `ZohMap` grouped by severity. No gaps found.
- [x] **8.3 Assertions:** `/assert` (truthy vs falsy resolution, formatting messages).
  - Execution: Verified `AssertDriver` handles conditional matching, correctly interpolates the optional failure message using `ZohInterpolator`, and throws `assertion_failed` fatals upon failure. No gaps found.

---

### Phase 9: Standard Verbs & Attributes - [Status: Complete]

**Goal:** Test the decoupled standard vocabulary that most ZOH scripts rely on.

**Milestones:**
- [x] **9.1 Presentation Layer:** `/converse`, `/choose`, `/chooseFrom`, `/prompt`, `/focus`, `/unfocus` (including timeouts and presentation attributes like `[Wait]`, `[Style]`, `[By]`).
  - Execution: Verified `ConverseDriver`, `ChooseDriver`, `ChooseFromDriver`, and `PromptDriver` correctly parse presentation attributes and issue host continuations.
  - **GAP:** `/focus` and `/unfocus` verbs are completely unimplemented. There are no drivers for them.
- [x] **9.2 Media Layer:** `/show`, `/hide` (including transforms, anchor, fade values), `/play`, `/playOne`, `/stop`, `/pause`, `/resume`, `/setVolume`.
  - Execution: Verified `ShowDriver`, `HideDriver`, `PlayDriver`, `PlayOneDriver`, `StopDriver`, `PauseDriver`, `ResumeDriver`, and `SetVolumeDriver`. All media drivers interface correctly with their respective handlers and parse transformation attributes successfully. No gaps found.
- [x] **9.3 Standard Attributes Mapping:** Verify global attribute behavior (e.g., `resolve`, `required`, `scope`).
  - Execution: Verified implementation across drivers. `[resolve]` operates correctly in `SetDriver`. `[required]` is enforced in `ReadDriver` and `GetDriver`. `[scope]` handles `story` and `context` boundaries appropriately in `SetDriver` and `DeferDriver`. No gaps found.

---

## Priorities

**Current focus:** Close remaining **navigation** variadic transfer (`/jump`, `/fork`) and **channel** semantics (Phase 6); other audit lines unchanged.

**Next up:**
1. Plan/patch for Phase 5.2 `/jump` + Phase 5.3 `/fork` trailing `*var` transfer (align with `CallDriver`); refresh or close [20260227-phase5-concurrency-context-signal-gaps-fix-plan.md](20260227-phase5-concurrency-context-signal-gaps-fix-plan.md) after scope trim.
2. Phase 6 blocking `/pull` + pull `timeout`; Phase 3 `FirstDriver` / `IncreaseDriver`/`DecreaseDriver` verb-argument behavior; Phase 7 `EraseDriver` loop abort; Phase 8 `/error` vs `/fatal`; Phase 9 `/focus`/`/unfocus`.
3. Optional: targeted tests for list `+` and interpolation format suffixes if review shows weak coverage.

**Deferred:**
- Broad Phase 9 presentation sweep beyond documented gaps until higher-severity runtime gaps shrink.

---

## Open Questions

- [ ] Prefer one umbrella plan per phase cluster (5, 6, …) with child plans, or single multi-phase plans with staged objectives?

---

## Revision Log

| Date | Summary of Changes |
|------|--------------------|
| 2026-02-23 | Initial roadmap created based on spec audit. |
| 2026-02-23 | Revised to provide greater granularity, mapping directly to specific features in the specification across 9 detailed phases. |
| 2026-02-25 | Corrected GAP 8.1 (DebugDriver exists and is registered). Restructured all milestones to separate gap bullets from execution prose for readability. |
| 2026-02-28 | Updated status checkpoint after Phase 3.4 remediation: marked `/wroll` and `/parse` gaps resolved, added evidence links, refreshed current position/priorities, and replaced stale open questions. |
| 2026-03-26 | Navigate-projex revision: Phase 4 audit gaps marked resolved with closed walkthrough/patch links; Phase 1 status corrected to Done; current position, priorities, open questions, and related projex updated for post–Phase-4 focus (5–6, 2.5/2.6, remaining milestone gaps). |
| 2026-03-26 | Corrected Phase 2.5/2.6: list `+` and interpolation `,width`/`format` suffixes are implemented in `ExpressionEvaluator`; removed stale GAP bullets; adjusted priorities and active work. |
| 2026-03-26 | Phase 4 umbrella plan moved to `closed/20260227-phase4-control-flow-gaps-fix-plan.md`; related links in this nav updated. |
| 2026-03-26 | Phase 5 milestones reconciled with **`csharp/` git** and code: resolved `/flag`, `/wait timeout`, `/call` transfer+`[inline]`; remaining gaps `/jump`/`/fork` transfer; priorities updated. |
