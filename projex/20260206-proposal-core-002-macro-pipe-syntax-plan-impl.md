# Macro Runtime Implementation Plan

## Goal
Implement the pipe-delimited macro syntax (`|%...|%|`) in the ZOH C# runtime.

## User Review Required
- [ ] Confirmed strict syntax: `|%NAME%|` (def) and `|%NAME|%|` (expansion).
- [ ] Confirmed missing arg behavior: `|%0|` -> `""` (empty string) if out of bounds.

## Proposed Changes
### Zoh.Runtime
#### [MODIFY] [MacroPreprocessor.cs](file:///s:/repos/zoh/c%23/src/Zoh.Runtime/Preprocessing/MacroPreprocessor.cs)
- Implemented `CollectMacros` with regex `^\s*\|%(\w+)%\|\s*$`.
- Implemented `ExpandMacros` with strict `|%NAME|...|%|` parsing.
- Diagnostics for unterminated definitions (`PRE002`) and expansions (`PRE005`).

### Zoh.Tests
#### [MODIFY] [PreprocessorTests.cs](file:///s:/repos/zoh/c%23/tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs)
- Added tests for:
    - No-args expansion (`|%NAME|%|`)
    - Positional args (`|%0|`, `|%1|`)
    - Auto-increment args (`|%|`)
    - Missing args (empty string replacement)
    - Escaped pipes (`\|`)
    - Multiline args

## Verification Plan
### Automated Tests
- Command: `dotnet test --filter "PreprocessorTests"`
- Status: **Passed (8/8)**

## Status
- [x] **Complete**
