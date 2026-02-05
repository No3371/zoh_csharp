# Plan: Pipe-Delimited Macro System (Runtime Impl)

> **Status:** In Progress
> **Created:** 2026-02-06
> **Source:** [proposal-core-002-macro-pipe-syntax.md](./proposal-core-002-macro-pipe-syntax.md)
> **Dependencies:** [20260206-proposal-core-002-macro-pipe-syntax-plan-spec.md](./20260206-proposal-core-002-macro-pipe-syntax-plan-spec.md)

---

## Summary

Phase 2 of the Macro System Redo. Implements the new pipe-delimited macro syntax in the C# runtime, based on the updated specification.

**Scope:** `MacroPreprocessor.cs`, `PreprocessorTests.cs`
**Estimated Changes:** 2 files modified

---

## Objective

### Problem / Gap / Need

The runtime currently implements the legacy `#macro` syntax. It must be updated to match the new specification defined in the Spec Plan.

### Success Criteria

- [ ] `MacroPreprocessor.cs` implements definition parsing (`|%NAME%|`)
- [ ] `MacroPreprocessor.cs` implements expansion parsing (`|%NAME|...|%|`)
- [ ] `MacroPreprocessor.cs` implements new placeholder logic
- [ ] Legacy tests updated and passing
- [ ] New tests added for escaped pipes and multiline arguments

---

## Context

### Key Files

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `c#/src/Zoh.Runtime/Preprocessing/MacroPreprocessor.cs` | Logic | Complete rewrite of parsing logic |
| `c#/tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs` | Verification | Update existing tests, add new ones |

### Dependencies

- **Requires:** Completion of Spec/Docs Plan

---

## Implementation Steps

### Step 1: Update Macro Definition Parsing
Parse `|%NAME%|...body...|%NAME%|` blocks using regex pattern `^\s*\|%(\w+)%\|`.

### Step 2: Update Macro Expansion Parsing
Parse `|%NAME|arg0|arg1|...|%|` expansions, splitting on unescaped `|`.

### Step 3: Update Placeholder Replacement
Replace `|%0|`, `|%1|`, `|%|` (auto-increment), `|%+N|`, `|%-N|` (relative) placeholders.

### Step 4: Handle Escaping
Support `\|` escape for literal pipes in arguments.

### Step 5: Update Tests
Rewrite `Macro_DefinesAndExpands` and related tests. Add specific tests for pipe escaping and multiline args.

---

## Verification Plan

### Automated Checks
```bash
cd c#
dotnet test --filter "Macro_"
dotnet test --filter "PreprocessorTests"
```

---

## Rollback Plan

- Revert `MacroPreprocessor.cs` and tests to previous commit.
