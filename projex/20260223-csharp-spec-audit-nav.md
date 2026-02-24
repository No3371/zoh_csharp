# C# Runtime Compliance Audit Roadmap

> **Created:** 2026-02-23 | **Last Revised:** 2026-02-23
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

The audit is conceptually framed. We are currently deciding the exact methodology (code review, integration test scenarios, or both) and parsing the specific features required for coverage.

### Active Work
- None. Audit strategy and roadmap formulation currently in progress.

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
  - Execution: Story boundaries and header logic are well-tested in `StoryHeaderParserTests` and `StoryNameLexerTests`. However, **metadata type validation has gaps**. The spec restricts metadata to `boolean`, `integer`, `double`, `string`, `list`, and `map`. The C# parser accepts any AST value. During AST-to-CompiledStory conversion, `ValueResolver.ResolveContextless` incorrectly permits `nothing` and `verb` types, and throws a hard `NotImplementedException` for expressions/references/channels instead of emitting a proper compiler diagnostic.
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
- [ ] **2.1 Variable Scopes:** Context vs. Story scope, dropping logic, and shadowing rules.
  - Execution: 
- [ ] **2.2 Type Constraints:** `[typed]`, `[required]`, and `[OneOf]` attributes on variables.
  - Execution: 
- [ ] **2.3 Type-to-String Coercion:** String formatting semantics (e.g., doubles always have `.`, `?` prints as `?`, collection stringification).
  - Execution: 
- [ ] **2.4 Nested References:** Deep indexing into lists (`*list[0]`) and maps (`*map["key"]`), implicit evaluation of indices, and undefined/missing element fallbacks (`?`).
  - Execution: 
- [ ] **2.5 Expressions:** Order of precedence, unary/binary operator math, list/map concatenations, and type coercions.
  - Execution: 
- [ ] **2.6 Interpolation (`Std.Interpolate`):** C#-style formatting (`${*var,8:N1}`), collection unrolling (`${*list...", "}`), picking (`${1|2|3}[*i]`), and evaluation specials (`$#`, `$?`).
  - Execution: 

---

### Phase 3: Core Verbs (Variables, Math, & Collections) — [Status: Future]

**Goal:** Verify basic procedural actions.

**Milestones:**
- [ ] **3.1 Variable Verbs:** `/set` (with `[resolve]`), `/get`, `/drop`, `/capture`, `/type`, `/count`.
  - Execution: 
- [ ] **3.2 Collection Verbs:** `/append`, `/remove`, `/insert`, `/clear`, `/has`, `/any`, `/first`.
  - Execution: 
- [ ] **3.3 Mathematics Verbs:** `/increase`, `/decrease`.
  - Execution: 
- [ ] **3.4 RNG & Parsing Verbs:** `/rand` (inclusive/exclusive), `/roll`, `/wroll`, `/parse` (robust string conversion).
  - Execution: 

---

### Phase 4: Control Flow — [Status: Future]

**Goal:** Ensure branching and looping structures are unbreakable.

**Milestones:**
- [ ] **4.1 Conditional Branching:** `/if` (with `is` and `else` parameters), `/switch` (cases and `default` fallback).
  - Execution: 
- [ ] **4.2 Loops:** `/loop` (fixed count, `-1` infinite), `/while` (conditional), `/foreach` (list/map iteration and scoping), `/sequence`.
  - Execution: 
- [ ] **4.3 Execution Resolution:** `/do` for executing verb literals.
  - Execution: 

---

### Phase 5: Concurrency, Contexts & Signals — [Status: Future]

**Goal:** Verify context boundaries and event architectures.

**Milestones:**
- [ ] **5.1 Checkpoint Contracts:** Verification of `*var:type` typing constraints at checkpoint boundaries.
  - Execution: 
- [ ] **5.2 Navigation Verbs:** `/jump` (intra/inter-story, arg passing), `/exit`.
  - Execution: 
- [ ] **5.3 Parallel Contexts:** `/fork` (`[clone]` attribute, var initialization), `/call` (`[inline]`, `[clone]`, blocking behavior).
  - Execution: 
- [ ] **5.4 Time & Flags:** `/sleep`, `/flag`.
  - Execution: 
- [ ] **5.5 Signal System:** `/wait` (timeout handling), `/signal` (cross-context broadcasts).
  - Execution: 

---

### Phase 6: Channels Architecture — [Status: Future]

**Goal:** Guarantee concurrent-safe FIFO pipe behaviors.

**Milestones:**
- [ ] **6.1 Channel Lifecycle:** `/open`, `/close` (instant wake-up of sleeping pullers, generation IDs).
  - Execution: 
- [ ] **6.2 Channel IO:** `/push` (blocking vs. `wait: false` fire-and-forget), `/pull`.
  - Execution: 
- [ ] **6.3 Channel Timeouts:** Rendezvous timeouts on both push and pull sides, returning appropriate diagnostics.
  - Execution: 

---

### Phase 7: Storage & State Management — [Status: Future]

**Goal:** Test persistence implementations according to spec.

**Milestones:**
- [ ] **7.1 Deferred Execution:** `/defer` (LIFO execution, `story` vs `context` scope teardown).
  - Execution: 
- [ ] **7.2 Persistence Verification:** `/write` (type restrictions), `/read` (defaults), `/erase`, `/purge` across explicit `store:` containers.
  - Execution: 

---

### Phase 8: Diagnostics & Debugging — [Status: Future]

**Goal:** Ensure errors are trapped, reported, or escalated properly.

**Milestones:**
- [ ] **8.1 Debug Logging:** `/info`, `/warning`, `/error`, `/fatal`.
  - Execution: 
- [ ] **8.2 Diagnostic Trapping:** `/try` (downgrading fatals, `catch:` execution, `[suppress]`), `/diagnose`.
  - Execution: 
- [ ] **8.3 Assertions:** `/assert` (truthy vs falsy resolution, formatting messages).
  - Execution: 

---

### Phase 9: Standard Verbs & Attributes — [Status: Future]

**Goal:** Test the decoupled standard vocabulary that most ZOH scripts rely on.

**Milestones:**
- [ ] **9.1 Presentation Layer:** `/converse`, `/choose`, `/chooseFrom`, `/prompt`, `/focus`, `/unfocus` (including timeouts and presentation attributes like `[Wait]`, `[Style]`, `[By]`).
  - Execution: 
- [ ] **9.2 Media Layer:** `/show`, `/hide` (including transforms, anchor, fade values), `/play`, `/playOne`, `/stop`, `/pause`, `/resume`, `/setVolume`.
  - Execution: 
- [ ] **9.3 Standard Attributes Mapping:** Verify global attribute behavior (e.g., `resolve`, `required`, `scope`).
  - Execution: 

---

## Priorities

**Current focus:** Establishing the granular roadmap and deciding the execution mechanism (e.g. creating pure ZOH integration test scripts for each milestone vs structural testing in C#).
**Next up:** Begin executing Phase 1 (Anatomy, Parsing & Preprocessing).

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
