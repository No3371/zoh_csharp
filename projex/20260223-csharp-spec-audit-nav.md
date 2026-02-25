# C# Runtime Compliance Audit Roadmap

> **Created:** 2026-02-23 | **Last Revised:** 2026-02-25
> **Author:** Antigravity
> **Scope:** Full spec compliance audit of the ZOH C# Reference Implementation
> **Parent Navigation:** [20260207-csharp-runtime-nav.md](20260207-csharp-runtime-nav.md)
> **Related Projex:**

---

## Vision

To systematically and rigorously verify that the ZOH C# Reference Implementation strictly adheres to every detail of the language specifications (`spec/`). This audit ensures the runtime is a completely accurate reference for how ZOH should behave, validating edge cases, type constraints, concurrency models, and standard feature parity.

---

## Current Position

**As of 2026-02-23:**

The comprehensive audit of Phases 1 through 9 against the ZOH specification has been completed. Numerous compliance gaps have been identified and documented within each phase below.

### Active Work
- None. Audit is complete.

### Known Blockers
- None.

---

## Roadmap

### Phase 1: Anatomy, Parsing & Preprocessing — [Status: Next]

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

### Phase 2: Type System, Variables & Expressions — [Status: Future]

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
- [x] **2.5 Expressions:** Order of precedence, unary/binary operator math, list/map concatenations, and type coercions.
  - Execution: Verified in `ExpressionEvaluator` and `ExpressionTests`. Math precedence is correct. Unary minus and power operators (`**`) bind correctly. Ints silently promote to Floats on overflow or precision division.
  - **GAP:** List concatenation via the `+` operator is missing (Map concatenation is undefined/N/A per spec). `ExpressionEvaluator.EvaluateBinary` throws an `InvalidOperationException` instead of producing a combined list.
- [x] **2.6 Interpolation (`Std.Interpolate`):** C#-style formatting (`${*var,8:N1}`), collection unrolling (`${*list...", "}`), picking (`${1|2|3}[*i]`), and evaluation specials (`$#`, `$?`).
  - Execution: Verified in `ExpressionEvaluator`. Nested interpolations (`$#`, `$?`) and collection unrolling (`...`) are supported and tested. Option picking `$(...)[idx]` works per spec.
  - **GAP:** C#-style formatting (e.g., `,8:N1`) is not implemented. `EvaluateInterpolationMatch` evaluates the target and calls `.ToString()` directly without parsing format specifiers.

---

### Phase 3: Core Verbs (Variables, Math, & Collections) — [Status: Future]

**Goal:** Verify basic procedural actions.

**Milestones:**
- [x] **3.1 Variable Verbs:** `/set` (with `[resolve]`), `/get`, `/drop`, `/capture`, `/type`, `/count`.
  - Execution: Verified `SetDriver`, `GetDriver`, `DropDriver`, `CaptureDriver`, `TypeDriver`, `CountDriver`. Implementation is complete and correctly handles deep paths, attributes (`[scope]`, `[typed]`, `[required]`, `[OneOf]`, `[resolve]`), and spec behaviors.
- [x] **3.2 Collection Verbs:** `/append`, `/remove`, `/insert`, `/clear`, `/has`, `/any`, `/first`.
  - Execution: Verified `AppendDriver`, `InsertDriver`, `RemoveDriver`, `ClearDriver`, `HasDriver`, `AnyDriver`, `FirstDriver`. Most operations handle map/list access and index logic correctly.
  - **GAP:** `FirstDriver.cs` does not dynamically evaluate expression arguments or recursively execute verb literal arguments per the `Core.First` spec ("In case of `/verb`, it takes the return value of the verb"). It currently yields the unevaluated `ZohVerb` or `ZohExpr` object.
- [x] **3.3 Mathematics Verbs:** `/increase`, `/decrease`.
  - Execution: Verified `IncreaseDriver`, `DecreaseDriver`.
  - **GAP 1:** The driver silently ignores the `amount` parameter if it resolves to a non-numeric type (e.g. string) instead of throwing an `invalid_type` diagnostic, defaulting to `1`.
  - **GAP 2:** Like `FirstDriver`, it does not recursively execute `ZohVerb` parameter inputs when a `/verb` literal is provided for the amount, leading to the same silent fallback to `1`.
- [x] **3.4 RNG & Parsing Verbs:** `/rand` (inclusive/exclusive), `/roll`, `/wroll`, `/parse` (robust string conversion).
  - Execution: Verified `RollDriver` (handles rand/roll/wroll) and `ParseDriver`.
  - **GAP 1:** `/wroll` silently clamps negative weights to `0` instead of raising a fatal `invalid_value` diagnostic.
  - **GAP 2:** `/parse` does not implement `list` or `map` parsing ("not yet supported" error), missing JSON-like collection parsing capability.

---

### Phase 4: Control Flow — [Status: Future]

**Goal:** Ensure branching and looping structures are unbreakable.

**Milestones:**
- [x] **4.1 Conditional Branching:** `/if` (with `is` and `else` parameters), `/switch` (cases and `default` fallback).
  - Execution: Verified `IfDriver` and `SwitchDriver`.
  - **GAP 1:** `IfDriver` improperly implements `else` as a 3rd positional argument rather than a named parameter, and ignores the `is` named parameter entirely.
  - **GAP 2:** `SwitchDriver` does not recursively execute `ZohVerb` literals for `case` evaluations (spec: "In case of `/verb`, it takes the return value"), resulting in reference/verb matching failures.
- [x] **4.2 Loops:** `/loop` (fixed count, `-1` infinite), `/while` (conditional), `/foreach` (list/map iteration and scoping), `/sequence`.
  - Execution: Verified `LoopDriver`, `WhileDriver`, `ForeachDriver`, `SequenceDriver`.
  - **GAP 1:** `ForeachDriver` critically fails when passed a reference for the iteration variable (e.g., `*item`) because it uses `ValueResolver.Resolve` which extracts the target's current *value* instead of its name, then throws "Variable name must be a string".
  - **GAP 2:** `FlowUtils.ShouldBreak` evaluates `breakif` using standard `Resolve()`, preventing dynamic execution if a `/verb` is used as the breaking condition (a `ZohVerb` is inherently truthy, causing instant breaks).
- [x] **4.3 Execution Resolution:** `/do` for executing verb literals.
  - Execution: Verified `DoDriver`.
  - **GAP:** `DoDriver` executes the evaluated `ZohVerb` but directly returns its result, failing to satisfy the "verb returned by the parameter" spec. If a verb call returns another verb, `DoDriver` should execute the returned verb, but currently does not.

---

### Phase 5: Concurrency, Contexts & Signals — [Status: Complete]

**Goal:** Verify context boundaries and event architectures.

**Milestones:**
- [x] **5.1 Checkpoint Contracts:** Verification of `*var:type` typing constraints at checkpoint boundaries.
  - Execution: Verified `JumpDriver`, `ForkDriver`, and `CallDriver` successfully call `ValidateContract()` before resuming/jumping. No gaps found.
- [x] **5.2 Navigation Verbs:** `/jump` (intra/inter-story, arg passing), `/exit`.
  - Execution: Verified `ExitDriver` correctly terminates context.
  - **GAP:** `JumpDriver` hardcodes acceptance of 1 or 2 arguments (`story` and `checkpoint`) and completely ignores the variadic `var` repeating parameter, making it impossible to transfer variables between stories.
- [x] **5.3 Parallel Contexts:** `/fork` (`[clone]` attribute, var initialization), `/call` (`[inline]`, `[clone]`, blocking behavior).
  - Execution: Verified `ForkDriver` and `CallDriver` implement `[clone]` correctly using `ctx.Clone()`.
  - **GAP 1:** Like `JumpDriver`, both drivers completely ignore the trailing `var` arguments, severely limiting data-passing.
  - **GAP 2:** `CallDriver` completely lacks implementation for the `[inline]` attribute, blocking the return of variables from the forked context.
- [x] **5.4 Time & Flags:** `/sleep`, `/flag`.
  - Execution: Verified `SleepDriver` correctly yields `SleepContinuation`.
  - **GAP:** `/flag` is entirely missing from the implementation. There is no `FlagDriver` despite being documented in the spec (`/flag "name", value;`).
- [x] **5.5 Signal System:** `/wait` (timeout handling), `/signal` (cross-context broadcasts).
  - Execution: Verified `SignalDriver` correctly uses `Broadcast` and returns the number of woken contexts.
  - **GAP:** `WaitDriver` completely ignores the `timeout` named parameter, causing `/wait` to blindly block forever even if a timeout is explicitly provided.

---

### Phase 6: Channels Architecture — [Status: Complete]

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

### Phase 7: Storage & State Management — [Status: Complete]

**Goal:** Test persistence implementations according to spec.

**Milestones:**
- [x] **7.1 Deferred Execution:** `/defer` (LIFO execution, `story` vs `context` scope teardown).
  - Execution: Verified `DeferDriver`. Correctly parses the `[scope]` attribute and registers the payload with the Context defer stack. No gaps found.
- [x] **7.2 Persistence Verification:** `/write` (type restrictions), `/read` (defaults), `/erase`, `/purge` across explicit `store:` containers.
  - Execution: Verified `WriteDriver`, `ReadDriver`, `PurgeDriver`, and `EraseDriver`. Writing actively rejects verbs and channels. Reading integrates `[required]` and `[scope]` rules.
  - **GAP:** `EraseDriver` correctly issues an `info` diagnostic if a variable to erase is not found, but it uses a hard `return` instead of `continue` in its iteration loop. This causes it to completely abort erasing any subsequent variables provided in the `/erase` call if an earlier variable fails to resolve.

---

### Phase 8: Diagnostics & Debugging — [Status: Complete]

**Goal:** Ensure errors are trapped, reported, or escalated properly.

**Milestones:**
- [x] **8.1 Debug Logging:** `/info`, `/warning`, `/error`, `/fatal`.
  - Execution: Verified `DebugDriver`. All four verbs are implemented and registered via `VerbRegistry.RegisterCoreVerbs()`, sharing a single `DebugDriver` instance.
  - **GAP:** `/error` and `/fatal` collapse to the same code path (`DiagnosticSeverity.Error` → `VerbResult.Fatal`), producing identical behavior. The spec likely intends `/error` to emit an error-severity diagnostic (non-fatal) while `/fatal` terminates execution — the implementation does not distinguish between them.
- [x] **8.2 Diagnostic Trapping:** `/try` (downgrading fatals, `catch:` execution, `[suppress]`), `/diagnose`.
  - Execution: Verified `TryDriver` and `DiagnoseDriver`. `TryDriver` properly checks the `catch:` named parameter and the `[suppress]` attribute to intercept and downgrade fatals. `DiagnoseDriver` correctly extracts `context.LastDiagnostics` and bundles them into a `ZohMap` grouped by severity. No gaps found.
- [x] **8.3 Assertions:** `/assert` (truthy vs falsy resolution, formatting messages).
  - Execution: Verified `AssertDriver` handles conditional matching, correctly interpolates the optional failure message using `ZohInterpolator`, and throws `assertion_failed` fatals upon failure. No gaps found.

---

### Phase 9: Standard Verbs & Attributes — [Status: Complete]

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

**Current focus:** Comprehensive audit finished. Detailed gap documentation is completed.
**Next up:** Triage identified GAPs, prioritize fixes, and create patch projex to align the runtime with the ZOH specification.

---

## Open Questions

- [ ] Will we write comprehensive `scenarios/*.md` ZOH test scripts for each of these milestones, or primarily use C# unit tests?
- [ ] Are we ready to begin Phase 1?

---

## Revision Log

| Date | Summary of Changes |
|------|--------------------|
| 2026-02-23 | Initial roadmap created based on spec audit. |
| 2026-02-23 | Revised to provide greater granularity, mapping directly to specific features in the specification across 9 detailed phases. |
| 2026-02-25 | Corrected GAP 8.1 (DebugDriver exists and is registered). Restructured all milestones to separate gap bullets from execution prose for readability. |
