# Walkthrough: Story Header Parsing Fix

> **Execution Date:** 2026-02-11
> **Completed By:** User + Antigravity
> **Source Plan:** `20260209-story-header-parsing-plan.md`
> **Result:** Success (with evolved design)

---

## Summary

Implemented `StoryNameEnd` virtual token for multi-word story name parsing. The final design diverged significantly from the plan: instead of a single mega-Identifier containing the full name, individual Identifier tokens are emitted per word, with `StoryNameEnd` as a line terminator — mirroring the existing `CheckpointEnd` pattern. Also discovered and fixed CRLF (`\r\n`) handling, added a `header` constructor flag, and wrote 34 new tests.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Lexer emits `StoryNameEnd` at end of story header line | Complete | Emits at newline and EOF, mirrors `CheckpointEnd` pattern |
| Parser consumes tokens until `StoryNameEnd` for story name | Complete | Joins individual Identifier tokens with `string.Join(" ", nameParts)` |
| Multi-word names parsed correctly (e.g. "The Last Coffee Shop") | Complete | Verified via `StoryHeaderParserTests.Parse_ThreeWordName` |
| Exploration doc updated | Not Done | `20260208-newline-handling-explore.md` not updated; superseded by `20260211-csharp-crlf-handling-explore.md` |

---

## Execution Detail

### Step 1: Add StoryNameEnd Token

**Planned:** Add `StoryNameEnd` to `TokenType.cs`.

**Actual:** `StoryNameEnd` was already added in a prior session. No change needed.

**Deviation:** None.

---

### Step 2: Implement Lexer Logic

**Planned:** Add `_inStoryHeader = true` flag, emit `StoryNameEnd` at newline, add `ScanStoryName` method.

**Actual:** Design evolved through several iterations:

1. **First attempt** — `ScanStoryName` packed the entire name (with spaces) into a single `Identifier` token. This broke snippet parsing because `_tokens.Count == 0` assumed every source starts with a story name.

2. **CRLF bug discovered** — `ScanStoryName` used `while (Current != '\n')` which included `\r` from Windows `\r\n` line endings in the lexeme, producing `"My Story\r"` instead of `"My Story"`. This led to creating `20260211-csharp-crlf-handling-explore.md`.

3. **Off-by-one bug** — `ScanStoryName` captured from `_current` after the first character was already consumed by `Advance()` in `ScanToken`, producing `"y Story"` instead of `"My Story"`. Fixed by passing `start`/`startOffset` from `ScanToken`.

4. **Final design** — Removed `ScanStoryName` entirely. Added `_inCheckingStoryName` flag controlled by a new constructor parameter `bool header`. The Lexer now emits individual `Identifier` tokens per word. `SkipWhitespaceAndComments` returns early on newlines when `_inCheckingStoryName` is true (matching `_inCheckpointDef` pattern). `ScanToken` emits `StoryNameEnd` when it sees a `\n` with the flag set. `Tokenize()` emits `StoryNameEnd` at EOF if the flag is still set.

5. **CRLF normalization** — Added `_source = source.Replace("\r\n", "\n")` in constructor.

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Lexing/Lexer.cs` | Modified | Yes | Constructor takes `bool header`, CRLF normalization, `_inCheckingStoryName` flag, newline-aware `SkipWhitespaceAndComments`, `StoryNameEnd` injection in `ScanToken` and `Tokenize`, removed `ScanStoryName` call from `default` branch, removed buggy `\r\n` check in `ScanMultilineString` |

**Deviation:** Significant. Plan described a `_inStoryHeader` flag always starting `true`. Final uses `_inCheckingStoryName` controlled by constructor `header` parameter, and the Lexer emits individual tokens instead of one mega-Identifier.

---

### Step 3: Update Parser Logic

**Planned:** Consume tokens until `StoryNameEnd` and join.

**Actual:** `ParseStory` now collects Identifiers in a `List<string>` until `StoryNameEnd`:
```csharp
var nameParts = new List<string>();
while (Check(TokenType.Identifier))
{
    var nameToken = Consume(TokenType.Identifier, "Story Name");
    nameParts.Add(nameToken.Value?.ToString() ?? nameToken.Lexeme);
}
if (nameParts.Count > 0)
{
    name = string.Join(" ", nameParts);
    Consume(TokenType.StoryNameEnd, "Story Name End");
}
```

**Files Changed:**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Parsing/Parser.cs` | Modified | Yes | `ParseStory` collects Identifiers into `nameParts` list, joins with space, consumes `StoryNameEnd` |

**Deviation:** Parser only collects `Identifier` tokens (per the plan's spirit) but uses `Check/Consume` loop instead of consuming arbitrary tokens until `StoryNameEnd`.

---

### Step 4: Update Exploration Doc

**Planned:** Update `20260208-newline-handling-explore.md` to mark story header discrepancy as resolved.

**Actual:** Not updated. Instead, a new deeper exploration `20260211-csharp-crlf-handling-explore.md` was created during the session, documenting all CRLF handling findings across the entire Lexer.

**Deviation:** Superseded by new, more comprehensive document.

---

### Additional: Propagate Constructor Changes

**Not in plan.** The new `Lexer(string source, bool header)` constructor signature required updates across all call sites:

| File | Details |
|------|---------|
| `src/Zoh.Runtime/Execution/ZohRuntime.cs` | `new Lexer(source, true)` — runtime always has story headers |
| `src/Zoh.Runtime/Interpolation/ZohInterpolator.cs` | `new Lexer(source, false)` — interpolation has no headers |
| `src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs` | `new Lexer(source, false)` |
| `src/Zoh.Runtime/Expressions/ExpressionLexer.cs` | `new Lexer(source, startPosition, false)` |

---

### Additional: Test Suite Expansion

**Not in plan (beyond one test case).** Created 34 new tests across two new files:

| File | Change Type | Tests | Coverage |
|------|-------------|-------|----------|
| `tests/Zoh.Tests/Lexing/StoryNameLexerTests.cs` | Created | 14 | StoryNameEnd emission (newline, EOF, multi-word), header=false, CRLF normalization, edge cases (empty, comments, numbers in name) |
| `tests/Zoh.Tests/Parsing/StoryHeaderParserTests.cs` | Created | 20 | Single/multi/three-word names, CRLF, metadata, header=false, full stories, edge cases, known limitation test |

Updated existing test files to pass `header` parameter:

| File | Change Type | Details |
|------|-------------|---------|
| `tests/Zoh.Tests/Lexing/LexerTests.cs` | Modified | All calls use `new Lexer(source, false)`, added 1 `StoryNameEnd` test |
| `tests/Zoh.Tests/Lexing/LexerSpecComplianceTests.cs` | Modified | `Lex()` helper passes `false` |
| `tests/Zoh.Tests/Parsing/ParserTests.cs` | Modified | `Parse()` takes `bool header`, all calls updated |
| `tests/Zoh.Tests/Parsing/ParserComplexTests.cs` | Modified | `Parse()` takes `bool header`, story-header tests pass `true` |
| `tests/Zoh.Tests/Parsing/ParserSpecComplianceTests.cs` | Modified | `Parse()` takes `bool header`, updated per test |
| `tests/Zoh.Tests/Parser/ReferenceParsingTests.cs` | Modified | `new Lexer(input, false)` |
| `tests/Zoh.Tests/Runtime/NestedAccessTests.cs` | Modified | `new Lexer(script, false)` |
| `tests/Zoh.Tests/Execution/RuntimeTests.cs` | Modified | Added story headers to test sources |

---

## Success Criteria Verification

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| Lexer emits `StoryNameEnd` at EOL | `StoryNameLexerTests` (14 tests) | Pass | `Lex_MultiWordName_EmitsIdentifiersAndStoryNameEnd`, `Lex_StoryNameAtEof_EmitsStoryNameEnd` |
| Parser consumes until `StoryNameEnd` | `StoryHeaderParserTests` (20 tests) | Pass | `Parse_MultiWordName`, `Parse_ThreeWordName` |
| Multi-word names work | `Parse_ThreeWordName` | Pass | `"The Last Coffee Shop"` parsed correctly |
| CRLF handled | `Parse_NameWithCrLf`, `Lex_CrLfNormalized_StoryName` | Pass | `"My Story\r\n..."` normalized correctly |
| All existing tests still pass | `dotnet test` | Pass | 501/501 passed |

**Overall:** 4/4 core criteria passed. Step 4 (exploration doc update) technically incomplete but superseded.

---

## Deviations from Plan

### Deviation 1: Individual tokens instead of mega-Identifier
- **Planned:** `ScanStoryName` produces `Identifier("My Story")`
- **Actual:** Normal `ScanIdentifier` produces `Identifier("My")`, `Identifier("Story")`, then `StoryNameEnd`
- **Reason:** Mega-Identifier approach broke snippet parsing and was architecturally unsound
- **Impact:** Cleaner design, matches `CheckpointEnd` pattern

### Deviation 2: Constructor flag instead of always-on
- **Planned:** `_inStoryHeader = true` always
- **Actual:** `_inCheckingStoryName` controlled by `bool header` constructor parameter
- **Reason:** Without a flag, snippets like `/set *x, 10;` would be misinterpreted
- **Impact:** All Lexer call sites needed updating to pass `header` parameter

### Deviation 3: CRLF normalization added
- **Not planned.** Discovered during execution that Windows `\r\n` caused `\r` to leak into token values
- **Fix:** `_source = source.Replace("\r\n", "\n")` in constructor
- **Impact:** Fixes a class of bugs, removes buggy `\r\n` handling in `ScanMultilineString`

---

## Key Insights

### Lessons Learned
1. **Don't pack delimited content into a single token** — Lexers should emit fine-grained tokens. The Parser joins them.
2. **The `CheckpointEnd` pattern is reusable** — Newline-significant contexts can use the same flag-and-inject pattern for virtual tokens.
3. **CRLF normalization should be done at input** — Matches industry standard (Roslyn, Go, Rust). Eliminates per-method `\r` handling.

### Known Limitation
- `header=true` with `===` but no story name (empty first line) doesn't parse cleanly. The `_inCheckingStoryName` flag isn't cleared before `===` is consumed. Callers should use `header=false` when there's no story name.

---

## Related Projex

| Document | Status |
|----------|--------|
| `20260209-story-header-parsing-plan.md` | → Closed |
| `20260208-newline-handling-explore.md` | Story header section superseded by `20260211-csharp-crlf-handling-explore.md` |
| `20260211-csharp-crlf-handling-explore.md` | Created during execution |

---

## Appendix

### Test Output
```
已通過! - 失敗: 0，通過: 501，略過: 0，總計: 501
```
