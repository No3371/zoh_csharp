# C# Runtime \r\n (CRLF) Handling Exploration

> **Created:** 2026-02-11 | **Author:** Antigravity
> **Type:** Question Exploration
> **Related Projex:**
> - `20260208-newline-handling-explore.md` (repo-level)
> - `20260209-story-header-parsing-plan.md` (this folder)

---

## Summary

The C# Lexer uses `'\n'` (LF) as the sentinel for line-ending logic, but **never strips or normalizes `'\r'` (CR)**. On Windows, C# verbatim strings (`@"..."`) and `File.ReadAllText` produce `\r\n` (CRLF) line endings. Since `\r` is not `\n`, line-sensitive methods like `ScanStoryName` include the `\r` in their output — hence the `"y Story\r"` test failure.

**Root cause:** The Lexer has no `\r\n` normalization strategy. It relies on `char.IsWhiteSpace()` to skip `\r` incidentally in general whitespace loops, but in newline-sensitive scanning methods, `\r` is not handled.

**Guiding Questions:**
1. Where exactly does the Lexer check for `'\n'` and what happens when input has `'\r\n'` instead?
2. What is the proper fix — normalize at input or handle `\r` at each check site?

**Scope:** `Lexer.cs` newline handling only (Parser is downstream and not the source of the bug).

---

## Context

**Why Now:** User is implementing `20260209-story-header-parsing-plan.md` and hit a test failure where `ScanStoryName` captures `\r` as part of the story name.

**Current State:** The Lexer was originally designed around `'\n'`-only inputs (Linux-style). It has worked so far because most test inputs are either single-line or use `char.IsWhiteSpace()` to skip whitespace (which handles `\r`). The new `ScanStoryName` method exposed the gap.

---

## Investigation Targets

### Target: `Lexer.Advance()` (Line 59-65)
**Rationale:** Core character-advance method — tracks position/line numbers
**Status:** Done
**Findings:**
```csharp
private char Advance()
{
    var c = Current;
    _current++;
    _position = c == '\n' ? _position.NextLine() : _position.NextColumn();
    return c;
}
```
- Only `'\n'` triggers `NextLine()`. A `'\r'` just advances the column counter.
- This means on `\r\n` input, `\r` increments the column, then `\n` resets to next line. The column count is off by one for CRLF lines, but functionally harmless since positions are used for error reporting, not parsing logic.

---

### Target: `Lexer.ScanStoryName()` (Line 88-103) — **ROOT CAUSE**
**Rationale:** The method that directly causes the test failure
**Status:** Done
**Findings:**
```csharp
private void ScanStoryName()
{
    var start = _position;
    var startOffset = _current;
    _isStartOfLine = true;

    while (Current != '\n')  // ← Only checks for \n
    {
        Advance();           // ← \r gets advanced through and included in lexeme
    }

    var lexeme = _source[startOffset.._current];  // ← Includes \r
    AddToken(TokenType.Identifier, start, lexeme);
    AddToken(TokenType.StoryNameEnd, _position);
    Advance(); // Consume newline
}
```
On input `"My Story\r\n"`:
1. Loop runs: `M`, `y`, ` `, `S`, `t`, `o`, `r`, `y`, **`\r`** — all are `!= '\n'`, so they're consumed
2. Loop stops at `\n`
3. `lexeme` = `"My Story\r"` — **the `\r` is included**

---

### Target: `Lexer.SkipWhitespaceAndComments()` (Line 308-334)
**Rationale:** Main whitespace-skipping loop — does it accidentally protect other paths?
**Status:** Done
**Findings:**
```csharp
if (char.IsWhiteSpace(c))  // ← char.IsWhiteSpace('\r') == true
{
    if (c == '\n' && _inCheckpointDef)
        return;
    Advance();
    if (c == '\n') _isStartOfLine = true;
}
```
- `char.IsWhiteSpace('\r')` returns `true`, so `\r` IS consumed as whitespace here.
- This is why most of the Lexer works fine with `\r\n` — the `\r` is silently eaten by `SkipWhitespaceAndComments` before `ScanToken` runs.
- **However:** `ScanStoryName` is called from `ScanToken`'s `default` branch (line 296-298) AFTER `SkipWhitespaceAndComments` has already run. The `\r` at EOL is inside the name content, not leading whitespace before a token — so `SkipWhitespaceAndComments` never sees it.

---

### Target: `Lexer.ScanToken()` checkpoint injection (Line 109-118)
**Rationale:** Another `'\n'`-sensitive check
**Status:** Done
**Findings:**
```csharp
if (_inCheckpointDef && Current == '\n')
```
- Same pattern: only checks `'\n'`. On `\r\n` input, `Current` would be `'\r'` first.
- In practice, this works because `SkipWhitespaceAndComments` runs first and eats the `\r`, leaving `\n` as `Current`.
- **Wait — no.** If `_inCheckpointDef` is true, `SkipWhitespaceAndComments` returns early when it sees `'\n'`. But `'\r'` comes before `'\n'`. `'\r'` is `char.IsWhiteSpace`, so it gets consumed in the whitespace loop. Then the loop iteration sees `'\n'` with `_inCheckpointDef`, and returns. So `Current` is `'\n'` when checkpoint injection checks. **This works correctly by accident.**

---

### Target: `Lexer.SkipComment()` line comment (Line 356-363)
**Rationale:** Another `'\n'` check for line comments
**Status:** Done
**Findings:**
```csharp
while (!IsAtEnd && Current != '\n')
{
    Advance();
}
```
- Same pattern as `ScanStoryName`. On `\r\n` input, the `\r` gets consumed as part of the comment.
- **Harmless** — comment contents are discarded, so including `\r` doesn't matter.

---

### Target: `Lexer.ScanString()` newline check (Line 372-376)
**Rationale:** Checks for raw `'\n'` in strings
**Status:** Done
**Findings:**
```csharp
if (Current == '\n')
{
    ReportError(_position, "Unterminated string");
    return;
}
```
- On `\r\n` input, `\r` is **not** `'\n'`, so it gets appended to the string content.
- Then `'\n'` triggers the error.
- **Subtle bug:** If a string literally contains `\r` before a newline, the `\r` gets into the string value before the error fires. Not a practical issue since the error aborts anyway.

---

### Target: `Lexer.ScanMultilineString()` leading newline trim (Line 436-441)
**Rationale:** Has explicit `\r\n` handling — the only place!
**Status:** Done
**Findings:**
```csharp
if (content.StartsWith('\n'))
    content = content[1..];
else if (content.StartsWith("\r\n"))
    content = content[2..];
```
- **This is the only place in the Lexer that handles `\r\n` explicitly.**
- Note the order is wrong: it checks `'\n'` first, so on `\r\n` input, it strips only `\n` and leaves `\r`. Should check `\r\n` first.

---

### Target: Test input patterns
**Rationale:** Understanding why most tests pass despite the `\r\n` issue
**Status:** Done
**Findings:**
- Most tests use C# regular string literals (`"..."`) with explicit `\n` where needed: `Parse("@foo\n@foo")` — these produce LF-only, no problem.
- Tests using `@"..."` verbatim strings on Windows produce `\r\n`. Examples:
  - `ParserTests.Parse_StoryHeader_ParsesNameAndMetadata` (line 28) — uses `@"My Story\r\n..."` but **asserts `"My"` (old single-identifier behavior)**, so the `\r` doesn't matter because `ScanStoryName` didn't exist in the old code.
  - `ParserSpecComplianceTests.Spec_StoryStructure_HeaderAndSeparator` (line 49) — user changed this to use `@"My Story\r\n..."` and now asserts `"My Story"` — **this is the failing test**.
  - `ParserComplexTests` — user added `@"test\r\n===\r\n..."` headers — the `\r` gets into the "test" name, but no test asserts on the name value, so it passes.

---

## Patterns & History

**Patterns Found:**
- **`'\n'`-only sentinel:** Every line-sensitive check in the Lexer uses `Current == '\n'`. Zero checks for `'\r'`.
- **Accidental `\r` consumption:** `SkipWhitespaceAndComments` uses `char.IsWhiteSpace()`, which handles `\r`. This accidentally protects most code paths from CRLF issues.
- **Single exception:** `ScanMultilineString` has one explicit `\r\n` check, but it's in the wrong order.

**Evolution:** The Lexer was designed for LF-only input. This worked because test inputs were either single-line strings or used `\n` explicitly. The introduction of `ScanStoryName` — which scans content until the next newline — is the first place where `\r` can leak into a token value.

---

## Findings

### Discoveries
1. **`ScanStoryName` is the root cause:** It loops `while (Current != '\n')`, capturing `\r` into the lexeme.
2. **The `\r` is in the lexeme, not the parser:** By the time `ParseStory` sees the Identifier token, the `\r` is already baked in. No parser-level fix is appropriate.
3. **Most other code paths are accidentally immune:** `SkipWhitespaceAndComments` eats `\r` as whitespace before sensitive `'\n'` checks in `ScanToken`.
4. **`ScanMultilineString` has a related bug:** Checks `'\n'` before `"\r\n"`, so on CRLF it strips only `\n`, leaving `\r`.

### Mental Model
```
Source input: "My Story\r\n===\r\n/set *x, 1;\r\n"
                       ↑↑
                       These two chars

ScanStoryName reads: M, y, ' ', S, t, o, r, y, '\r'  ← stops at '\n'
Lexeme created: "My Story\r"   ← BUG
```

### Recommended Fix Options

**Option A: Normalize input (Recommended)**
Strip `\r` from input at the Lexer constructor:
```csharp
public Lexer(string source) : this(source.Replace("\r", ""), new TextPosition(1, 1, 0)) { }
```
- **Pros:** Single fix point, all methods automatically handle CRLF, matches common compiler convention
- **Cons:** Modifies source (offsets won't match original for error reporting if `\r` existed)
- **Note:** Most compilers (Roslyn, Go, Rust) normalize line endings at the input stage.

**Option B: Handle `\r` in each method**
Add `\r` checks in `ScanStoryName`, `SkipComment`, `ScanString`, etc.
- **Pros:** Preserves original offsets
- **Cons:** Easy to miss a spot, ongoing maintenance burden

---

## Answers

**How has the C# been handling `\r\n` and `\n`?**

The Lexer only checks for `'\n'` as a line ending sentinel. `'\r'` is handled incidentally by `char.IsWhiteSpace()` in the general whitespace-skipping loop, which protects most code paths. However, any method that scans content until `'\n'` (like `ScanStoryName`) will include `'\r'` in its output. There is no intentional `\r\n` handling anywhere except a (buggy) trim in `ScanMultilineString`.

**Why does the test fail?**

The test uses a C# verbatim string `@"My Story\n..."` which on Windows produces `"My Story\r\n..."`. `ScanStoryName` captures everything before `'\n'`, including the `'\r'`, producing the lexeme `"My Story\r"` instead of `"My Story"`.

---

## Open Questions

- [ ] Should the Lexer normalize all `\r` at input (Option A) or handle per-method (Option B)?
- [ ] Should `TextPosition` offsets refer to the original or normalized source?
- [ ] Should `ParserTests.Parse_StoryHeader_ParsesNameAndMetadata` be updated to match `ScanStoryName` behavior (assert full name instead of `"My"`)?

---

## Appendix

**Sources:**
- `src/Zoh.Runtime/Lexing/Lexer.cs` (all scanning methods)
- `src/Zoh.Runtime/Parsing/Parser.cs` (`ParseStory`)
- `tests/Zoh.Tests/Parsing/ParserTests.cs`
- `tests/Zoh.Tests/Parsing/ParserComplexTests.cs`
- `tests/Zoh.Tests/Parsing/ParserSpecComplianceTests.cs`
- `20260208-newline-handling-explore.md`

**Limitations:** Did not investigate how `.zoh` files are loaded in production (file encoding/newline conventions).
