# Story Header Parsing Fix Plan

> **Status:** Complete
> **Completed:** 2026-02-11
> **Walkthrough:** `20260209-story-header-parsing-walkthrough.md`
> **Created:** 2026-02-09
> **Author:** Antigravity
> **Source:** Direct request from user (referencing `20260208-newline-handling-explore.md`)
> **Related Projex:** 
> - `s:\repos\zoh\projex\20260208-newline-handling-explore.md`
> - `s:\repos\zoh\impl\projex\20260209-story-header-virtual-token-plan.md` (Implementation Spec)

---

## Summary

This plan addresses a discrepancy where story names are parsed as only the first identifier/word. We will implement the `STORY_NAME_END` virtual token defined in the **Implementation Guide** plan.

**Scope:** C# Runtime (`Lexer`, `Parser`) and Exploration Doc update.
**Estimated Changes:** 3 files.

---

## Objective

### Problem / Gap / Need
Current `Parser.ParseStory` captures only the first identifier as the story name.
The **Implementation Spec** now defines a `STORY_NAME_END` virtual token to handle this.
We need to implement this in the C# runtime.

### Success Criteria
- [ ] `Lexer` emits `STORY_NAME_END` at end of story header line.
- [ ] `Parser` consumes tokens until `STORY_NAME_END` for story name.
- [ ] `The Last Coffee Shop` is parsed as a single story name string.

### Out of Scope
- Documentation updates for `impl/` (handled by `20260209-story-header-virtual-token-plan.md`).

---

## Context

### Current State
- `Lexer` treats newlines as whitespace and skips them.
- `Parser` expects single identifier.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Lexing/TokenType.cs` | Enum | Add `StoryNameEnd` |
| `src/Zoh.Runtime/Lexing/Lexer.cs` | Logic | Emit `StoryNameEnd` on newline in header |
| `src/Zoh.Runtime/Parsing/Parser.cs` | Logic | Consume until `StoryNameEnd` |

### Dependencies
- `s:\repos\zoh\impl\projex\20260209-story-header-virtual-token-plan.md` (Defines the token behavior)

---

## Implementation

### Step 1: Add StoryNameEnd Token

**Objective:** Define the new token type.

**Files:**
- `src/Zoh.Runtime/Lexing/TokenType.cs`

**Changes:**
```csharp
public enum TokenType {
    // ...
    CheckpointEnd,
    StoryNameEnd, // [NEW] virtual token for end of story name line
    // ...
}
```

### Step 2: Implement Lexer Logic

**Objective:** Emit `StoryNameEnd` at the end of the first line.

**Files:**
- `src/Zoh.Runtime/Lexing/Lexer.cs`

**Changes:**
1.  Add `private bool _inStoryHeader = true;` field.
2.  In `SkipWhitespaceAndComments`:
    ```csharp
    if (c == '\n' && (_inCheckpointDef || _inStoryHeader)) // [MODIFY]
    {
        return;
    }
    ```
3.  In `ScanToken` (before `IsAtEnd` check):
    ```csharp
    // Story header injection
    if (_inStoryHeader && Current == '\n')
    {
        var pos = _position;
        Advance(); // Consume newline
        AddToken(TokenType.StoryNameEnd, pos);
        _inStoryHeader = false;
        _isStartOfLine = true;
        return;
    }
    ```

### Step 3: Update Parser Logic

**Objective:** Consume full story name.

**Files:**
- `src/Zoh.Runtime/Parsing/Parser.cs`

**Changes:**
In `ParseStory`:
```csharp
// [MODIFY] Logic to capture name
if (!Check(TokenType.StorySeparator) && ...) {
    // Consume tokens until StoryNameEnd, StorySeparator, or Eof
    var nameParts = new List<string>();
    while (!Check(TokenType.StoryNameEnd) && !Check(TokenType.StorySeparator) && !IsAtEnd && !Check(TokenType.Eof)) 
    {
         var token = Advance();
         nameParts.Add(token.Value?.ToString() ?? token.Lexeme);
    }
    name = string.Join(" ", nameParts); 
    
    if (Check(TokenType.StoryNameEnd)) Advance(); // Consume the virtual token
    
    // Parse metadata (loop)
    // ...
}
```

### Step 4: Update Exploration Doc

**Objective:** Mark the exploration as resolved.

**Files:**
- `s:\repos\zoh\projex\20260208-newline-handling-explore.md`

**Changes:**
- Update `E. Story Header (Discrepancy)` section to mark it as resolved.

---

## Verification Plan

### Automated Checks
- [ ] Run existing tests.
- [ ] Create test case: `Parse_MultiWord_StoryName`.

### Manual Verification
- [ ] Verify `20260208-newline-handling-explore.md` discrepancy is resolved.
