# Walkthrough: Macro Runtime Implementation

> **Execution Date:** 2026-02-06
> **Completed By:** Antigravity
> **Source Plan:** [20260206-proposal-core-002-macro-pipe-syntax-plan-impl.md](../20260206-proposal-core-002-macro-pipe-syntax-plan-impl.md)
> **Result:** Success

---

## Summary

Implemented the pipe-delimited macro syntax using strict validation rules. The implementation enforces `|%NAME%|` for definitions and `|%NAME|...|%|` for expansions (including `|%NAME|%|` for no-arguments). Support for positional arguments (`|%0|`), auto-increment (`|%|`), escaping (`\|`), and missing-argument handling (resolving to empty string) was verified with a comprehensive test suite.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Implement MacroPreprocessor | Complete | Handles definition collection and expansion |
| Implement Test Suite | Complete | Covers all edge cases including escaping and recursion behavior (flat expansion) |
| Strict Syntax Enforcement | Complete | Removed ambiguity by removing `|%NAME%|` support for expansion |

---

## Execution Detail

### Step 1: MacroPreprocessor Implementation

**Planned:** Implement parsing logic for pipe syntax.
**Actual:** Implemented `CollectMacros` (regex-based definition extraction) and `ExpandMacros` (strict scanner for expansions).
**Deviations:**
- **Strict Syntax:** Originally plan allowed `|%NAME%|` for no-arg expansion. This was removed to resolve visual ambiguity with definition lines. Now requires `|%NAME|%|`.
- **Missing Args:** Originally might have returned `?`. User requested "0 string" (empty string) behavior. Implemented `""` for out-of-bounds access.

**Files Changed (ACTUAL):**
| File | Change Type | Details |
|------|-------------|---------|
| `src/Zoh.Runtime/Preprocessing/MacroPreprocessor.cs` | Created | Logic for `CollectMacros`, `ExpandMacros`, `ParseArgs`, `ExpandBody` |

### Step 2: Verification Tests

**Planned:** Validating no-args, positional args, etc.
**Actual:** Added `PreprocessorTests.cs` with 8 test cases.
**Deviations:** None.

**Files Changed (ACTUAL):**
| File | Change Type | Details |
|------|-------------|---------|
| `tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs` | Modified | Added 8 tests asserting correct preprocessor behavior |

---

## Complete Change Log

### Files Created
| File | Purpose |
|------|---------|
| `src/Zoh.Runtime/Preprocessing/MacroPreprocessor.cs` | Implementation of macro logic |

### Files Modified
| File | Changes |
|------|---------|
| `tests/Zoh.Tests/Preprocessing/PreprocessorTests.cs` | Added macro test suite |

---

## Success Criteria Verification

### Criterion 1: Pipe Syntax Support
**Verification Method:** `dotnet test`
**Result:** PASS

### Criterion 2: Missing Argument Handling
**Verification Method:** User verification + Test `Macro_HandleMissingArg_AsEmptyString`
**Evidence:** Test passes asserting `/val ;` output for missing arg `|%0|`.
**Result:** PASS

---

## Key Insights

### Lessons Learned
1. **Ambiguity Resolution:** Overloaded syntax (where definition and call look identical) is dangerous. Enforcing visual distinction (`|%|` terminator vs definition line) simplification parsing and reduces user confusion.
2. **Strictness vs Flexibility:** Being strict about syntax (`PRE005` errors for unterminated expansions) is better than heuristic guessing, especially in a transpiler/preprocessor context.

---

## Recommendations

### Future Considerations
- The user has proposed a **Macro Redo** (`proposal-macro-redo.md`) involving indentation preservation and relative parameter mirroring (`|%+2|`). The current implementation is compliant with the *previous* spec but will need significant updates to support the new Redo proposal. This walkthrough closes the *original* scope.
