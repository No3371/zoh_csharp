# Virtual Checkpoint Token - C# Implementation Plan

> **Status:** Complete
> **Completed:** 2026-02-09
> **Walkthrough:** [20260208-virtual-checkpoint-token-csharp-walkthrough.md](20260208-virtual-checkpoint-token-csharp-walkthrough.md)

> **Created:** 2026-02-08
> **Author:** Antigravity
> **Source:** Direct request / [Analysis](../../../checkpoint-parsing-analysis.md)
> **Related Projex:** 
> - Requires: [Impl Doc Plan](../../../impl/projex/20260208-virtual-checkpoint-token-impl-doc-plan.md)

---

## Summary

This plan implements the "Virtual Checkpoint-Ending Token" mechanism in the C# Runtime. This involves modifying the Lexer to be context-aware of checkpoint definitions and emitting a `CheckpointEnd` token upon encountering a newline, which simplifies the Parser's checkpoint consumption logic.

**Scope:** `c#/src/Zoh.Runtime`, `c#/tests/Zoh.Tests`
**Estimated Changes:** 4 files

---

## Objective

### Problem / Gap / Need
The current `Parser.cs` uses complex lookahead logic to determine where a checkpoint definition ends because the Lexer discards newlines. This makes the parser fragile and hard to maintain.

### Success Criteria
- [ ] `TokenType` includes `CheckpointEnd`.
- [ ] `Lexer` emits `CheckpointEnd` when encountering a newline inside a checkpoint definition.
- [ ] `Parser` uses `CheckpointEnd` to terminate `ParseLabel` instead of lookahead.
- [ ] All existing tests pass.
- [ ] New tests verify correct token emission and parsing.

### Out of Scope
- Changes to other newline-sensitive constructs (only Checkpoints are affected).

---

## Context

### Current State
- `Lexer.cs`: Discards all whitespace/newlines in `SkipWhitespaceAndComments`.
- `Parser.cs`: `ParseLabel` peeks ahead to find start of next statement to know when to stop parsing params.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Lexing/TokenType.cs` | Token Definitions | Add `CheckpointEnd` |
| `c#/src/Zoh.Runtime/Lexing/Lexer.cs` | Tokenizer | Implement state tracking for `@id` and emit `CheckpointEnd` on newline. |
| `c#/src/Zoh.Runtime/Parsing/Parser.cs` | AST Builder | Update `ParseLabel` to consume until `CheckpointEnd`. |
| `c#/tests/Zoh.Tests/Lexing/LexerTests.cs` | Verification | Add tests for `CheckpointEnd` output. |

### Dependencies
- **Requires:** Impl Doc Plan (Conceptually)

---

## Implementation

### Step 1: Add Token Type

**Objective:** Define the new token.

**Files:**
- `c#/src/Zoh.Runtime/Lexing/TokenType.cs`

**Changes:**
```csharp
public enum TokenType {
    // ...
    CheckpointEnd,
    // ...
}
```

---

### Step 2: Implement Context-Aware Lexer

**Objective:** Emit virtual token on newline during checkpoint definition.

**Files:**
- `c#/src/Zoh.Runtime/Lexing/Lexer.cs`

**Changes:**
1.  Add `private bool _inCheckpointDef;` state.
2.  When scanning `@`: check if followed by identifier. If so, set `_inCheckpointDef = true`.
    *   *Note:* `ScanToken` handles `@`. We might need to lookahead in `ScanToken` or flag it when `At` token is emitted.
    *   Actually, `At` is emitted, then `Identifier` is emitted.
    *   Better approach: `Parser` usually drives meaning, but Lexer needs to be smart here.
    *   Logic: If we just emitted `At`, and next is identifier? No, `At` is used for other things? Only labels use `@` at start of line?
    *   Spec says `@identifier` is a label.
    *   Let's refine: When `ScanToken` sees `@`, it emits `At`.
    *   If `_previousToken` was `At` and `currentToken` is `Identifier`? Lexer doesn't track previous token easily.
    *   *Alternative:* In `ScanToken`, when we see `@`, we can peek next char. If it's identifier start, we know it's a label.
    *   But wait, `jump` sugar also uses `@label`. `====> @label`. Newlines there don't end the statement (semicolon does).
    *   *Crucial Distinction:* A **Checkpoint Definition** is at the start of a statement (or file). It is NOT inside a verb call or sugar.
    *   How does Lexer know if `@` is start of line vs inside `====>`?
    *   The `====>` consumes `@`? No, `ParseJumpSugar` consumes `Jump` then `At`.
    *   So `@` is a distinct token.
    *   *Revised Logic*: We need to know if `@` is the *start of a statement*.
    *   Lexer doesn't know about statements.
    *   *Correction*: Checkpoints start with `@` at the *beginning of a line* (ignoring whitespace).
    *   If `@` is the first non-whitespace token on a line, it's likely a checkpoint definition.
    *   Logic: `_isStartOfLine`. Reset to `true` after newline. Set to `false` after emitting any token (except whitespace/comment).
    *   If `_isStartOfLine` and we see `@`, set `_inCheckpointDef = true`.
    *   When `_inCheckpointDef` is true, if we hit newline, emit `CheckpointEnd` and set `_inCheckpointDef = false`.

**Rationale:** Using "start of line" heuristic accurately identifies checkpoint definitions distinguishing them from references to checkpoints in other commands (which don't start the line).

---

### Step 3: Update Parser Logic

**Objective:** Simplfy `ParseLabel`.

**Files:**
- `c#/src/Zoh.Runtime/Parsing/Parser.cs`

**Changes:**
```csharp
private StatementAst.Label ParseLabel()
{
    // ... consume At, Identifier ...
    while (!Check(TokenType.CheckpointEnd) && !IsAtEnd)
    {
         // Parse contract param...
    }
    Consume(TokenType.CheckpointEnd, "Expected newline...");
    return ...;
}
```

---

### Step 4: Verification Tests

**Objective:** Verify new behavior.

**Files:**
- `c#/tests/Zoh.Tests/Lexing/LexerTests.cs` (New tests)
- `c#/tests/Zoh.Tests/Parsing/ParserTests.cs` (Update tests)

**Changes:**
1.  Lexer Test: `Scan_Checkpoint_EmitsCheckpointEnd`. Input: `@main\n`. Expect: `At`, `Identifier`, `CheckpointEnd`, `Eof`.
2.  Parser Test: `Parse_Checkpoint_WithParams`. Input: `@main *p1 *p2\n`. Expect successful parsing.

---

## Verification Plan

### Automated Checks
- [ ] Run all tests: `dotnet test s:\repos\zoh\c#\tests\Zoh.Tests\Zoh.Tests.csproj`

### Manual Verification
- [ ] Create a small `.zoh` file with `@start\n*var <- 1;`. Run parser and inspect AST (via debugger or temporary print) to ensure `start` checkpoint is parsed correctly and `set` statement is separate.

---

## Rollback Plan
- Revert changes to `Lexer.cs`, `Parser.cs`, `TokenType.cs`.

---

## Notes
- **Risk**: Does `@` always mean checkpoint at start of line? Yes, per spec.
- **Risk**: What about `====>` at start of line? That's `Jump` token, not `At` token.
- **Risk**: What about comments? `SkipWhitespaceAndComments` needs to handle comments *before* checking for newline? If comment ends line, the newline after comment should trigger `CheckpointEnd`.
    - Logic: `SkipComment` consumes newline. We might need `SkipComment` to *not* consume newline if we are in `_inCheckpointDef`? Or just let `SkipWhitespaceAndComments` loop handle it.
    - If `SkipComment` consumes newline, we miss the trigger.
    - *Refinement*: `SkipComment` should NOT consume the newline if it's a line comment `::`. It should stop *at* the newline. Then `ScanToken` loop sees newline, triggers `CheckpointEnd` logic.
