### User Feedback & Tasks
- [x] Fix `MacroPreprocessor` to correctly implement `Diagnostic` constructor (Severity, Code, Message, Position, FilePath).
- [x] Resolve unused variable warnings.
- [x] Fix invalid test case syntax `|%CHECK|%|` in `Macro_HandleMissingArg_AsNothing`.
- [x] Verify `|%GREET%|` vs `|%GREET|%|` expansion syntax.
    - [x] Confirmed spec mandates `|%NAME|%|` for expansion.
    - [x] Refactored `ExpandMacros` to enforce strict syntax and removed ambiguity resolution.
    - [x] Updated tests to use `|%GREET|%|`.
- [x] Fix "Unterminated expansion for '0'" error.
    - [x] Root cause: Definition block looked like expansion. Fixed by ensuring strict definition syntax handling or definition removal.
    - [x] Improved test source with explicit newlines to avoid CRLF regex issues.
- [x] Address User Feedback: "No it should just be 0 string, not `nothing`".
    - [x] Updated `ExpandBody` to return empty string `""` for missing/out-of-bounds args.
    - [x] Updated test `Macro_HandleMissingArg_AsEmptyString` to expect `/val ;`.

### Implementation Details
- **MacroPreprocessor.cs**:
    - Implemented strict pipe-delimited syntax: `|%NAME%|` (def), `|%NAME|...|%|` (expansion).
    - `ParseArgs`: Returns empty list `[]` for empty input (0 args).
    - `ExpandBody`: Returns `""` for missing args.
- **PreprocessorTests.cs**:
    - Comprehensive tests for no-args, positional args, auto-inc, missing args, escaping, multiline.
    - verbose failure reporting.

### Final Status
- Build: Success
- Tests: 8 passed, 0 failed.
