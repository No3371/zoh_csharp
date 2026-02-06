# Remove #flag Syntactic Sugar (C# Implementation)

> **Status:** Ready
> **Created:** 2026-02-07
> **Author:** Agent
> **Source:** Direct request
> **Related Projex:** [Spec Changes](../../projex/20260207-remove-flag-sugar-plan.md)

---

## Summary

Remove `#flag` handling from C# parser. After this change, `#flag debug true;` will produce a parse error.

**Scope:** C# parser and tests  
**Estimated Changes:** 2 files

---

## Objective

### Success Criteria

- [ ] `#flag` syntax produces parse error
- [ ] `/flag "name", value;` still works
- [ ] Build and tests pass

### Out of Scope

- Spec/impl docs (separate projex)

---

## Implementation

### Step 1: Update Parser.cs

**Files:** `src/Zoh.Runtime/Parsing/Parser.cs`

**Changes:** Simplify `ParsePreprocessorOrFlag()` (L781-802):

```csharp
// Before: L787-798 has special #flag handling
// After: All directives treated as unknown

private StatementAst ParsePreprocessorOrFlag()
{
    var pos = Current.Start;
    Consume(TokenType.Hash, "Expected '#'");
    var directive = Consume(TokenType.Identifier, "Expected directive name").Lexeme;

    // All preprocessor directives should be handled in preprocessing phase
    throw Error(pos, $"Unknown directive: #{directive}");
}
```

---

### Step 2: Remove Test

**Files:** `tests/Zoh.Tests/Parsing/ParserSpecComplianceTests.cs`

**Changes:** Delete `Spec_FlagSugar_Hash` test (L213-227)

---

## Verification

```bash
cd c# && dotnet build && dotnet test
```

| Check | Expected |
|-------|----------|
| `#flag debug true;` | Parse error |
| `/flag "debug", true;` | Success |
| All tests | Pass |

---

## Rollback

`git checkout -- src/Zoh.Runtime/Parsing/Parser.cs tests/`
