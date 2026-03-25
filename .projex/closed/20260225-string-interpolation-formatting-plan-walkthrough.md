# Walkthrough: String Interpolation Formatting Implementation

> **Execution Date:** 2026-03-03
> **Completed By:** Antigravity
> **Source Plan:** [20260225-string-interpolation-formatting-plan.md](20260225-string-interpolation-formatting-plan.md)
> **Duration:** 2 days (includes feedback cycle)
> **Result:** Success

---

## Summary

Successfully implemented string interpolation formatting functionality. This allows ZOH's string interpolation `${*var}` syntax to accept C#-style formatting specifiers for width and format strings, such as `${*var,-10:F2}`. The feature fully relies on C#'s underlying `string.Format(CultureInfo.InvariantCulture, ...)` matching the intended implementation plan.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Enable `width` specification in string interpolation | Complete | Supported formats like `${*var,10}` and `${*var,-5}` |
| Enable `formatMode` specification in string interpolation | Complete | Supported formats like `${*var:F2}`, `${*var:D4}`, `${*var:X}` |
| Ensure compatibility with C#'s `string.Format` behavior | Complete | Handled via invariant culture conversion |
| Enforce syntax constraints and diagnostics | Complete | Throws clear errors on malformed formatting syntax or combining with scanner suffix syntax |

---

## Execution Detail

> **NOTE:** This section documents what ACTUALLY happened, derived from git history and execution notes.

### Step 1: Expose Parser State

**Planned:** Expose the number of tokens consumed by the expression parser back to the evaluator so it knows exactly where the formatting suffix begins.

**Actual:** Added a simple `ConsumedTokensCount` property which returns the `_current` cursor position inside `ExpressionParser.cs`.

**Deviation:** None

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Expressions/ExpressionParser.cs` | Modified | Yes | Lines 10-14: Added `ConsumedTokensCount` property. |

**Verification:** Code reviewed against plan.

---

### Step 2: Implement Formatting Logic

**Planned:** Modify `ExpressionEvaluator.cs` to execute the lexer then the parser on the string inside the `${...}` block, extract the expression value, and then parse the remaining trailing string to extract `,width` and `:format` specifiers to build the `string.Format("{0,width:format}", value)` output.

**Actual:** Implemented the exact sequence planned. Encountered a variable shadowing error (`CS0136`) for `s` inside the switch expression mapping to strings which was renamed. Also implemented the logic to fallback to `?` for `ZohNothing` values and delegate other literals to `coreVal.ToString()`.

**Deviation:** Only minor implementation detail fixes such as variable renaming from shadows.

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs` | Modified | Yes | Lines 242-315: Evaluator extraction, token splitting based on `ConsumedTokensCount`, substring parsing for width formatting rules, and invalid combination checks. |

**Verification:** Successful build testing. Functionality verified via automated tests.

---

### Step 3: Add Unit Tests

**Planned:** Add the `Eval_Interpolation_Formatting` test suite in `ExpressionTests.cs`.

**Actual:** Added the tests. A feedback review requested even broader coverage for C# syntax formats. Additional tests were integrated (`D4`, `X`, `x2`, `P1` percentages). A `FormatException` expectation was fixed due to `string.Format` throwing it for malformed patterns. Finally, an old interpolation syntax bug `$?{*var? 1 | 2}` used in a nested test was identified and corrected to use the standard `$?(*var? 1 : 2)` expression shape.

**Deviation:** Added additional tests over what was originally planned to provide broader coverage. Also corrected an outdated spec syntax usage in the test.

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `tests/Zoh.Tests/Expressions/ExpressionTests.cs` | Modified | Yes | Lines 299-340: Added comprehensive width/format suite. |

**Verification:** All 606 ZOH tests, including the updated `Eval_Interpolation_Formatting`, passed successfully (Exit code 0).

---

## Complete Change Log

> **Derived from:** `git diff --stat main..HEAD` 

### Files Modified
| File | Changes | Lines Affected | In Plan? |
|------|---------|----------------|----------|
| `src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs` | Refactored trailing parser logic | +71, -1 | Yes |
| `src/Zoh.Runtime/Expressions/ExpressionParser.cs` | Added property | +2, -0 | Yes |
| `tests/Zoh.Tests/Expressions/ExpressionTests.cs` | Added formatting test suite | +42, -0 | Yes |

---

## Success Criteria Verification

### Criterion 1: Formats behave identically to `string.Format` in `CultureInfo.InvariantCulture`.

**Verification Method:** Unit Tests (e.g. `P1`, `D4`, `X2`) covering all general numeric format conversions.

**Evidence:**
```csharp
Assert.Equal(new ZohStr("Percent: 1,000.0 %"), Eval("$\"Percent: ${*score:P1}\""));
Assert.Equal(new ZohStr("Hex: A"), Eval("$\"Hex: ${*score:X}\""));
```

**Result:** PASS

### Criterion 2: Gracefully fail / throw diagnostic when formatting a value that cannot be formatted.

**Verification Method:** Evaluator validation & Unit Tests.

**Evidence:**
```csharp
var ex2 = Assert.Throws<FormatException>(() => Eval("$\"${*name,abc}\""));
Assert.Contains("format", ex2.Message, StringComparison.OrdinalIgnoreCase);
```

**Result:** PASS

### Criterion 3: Disallow combining formatting suffix with scanner suffix `[..]`.

**Verification Method:** Unit tests.

**Evidence:**
```csharp
var ex1 = Assert.Throws<Exception>(() => Eval("$\"${*name,7}[0]\""));
Assert.Contains("cannot be combined", ex1.Message, StringComparison.Ordinal);
```

**Result:** PASS

---

## Deviations from Plan

None of consequence. 

---

## Key Insights

### Gotchas / Pitfalls

1. **Incorrect Exception type expectation for `string.Format`**
   - Trap: `string.Format` throws `FormatException`, NOT a generic `Exception` for bad argument specifiers.
   - How encountered: The expected generic Exception assertion test failed.
   - Avoidance: Catch the right subclass for runtime exception propagation, particularly when delegating to standard C# BCL types.

2. **Outdated Markdown Spec Sync**
   - Trap: The specification document `06_core_verbs.md` showed an outdated syntax model for conditional expressions in string interpolation blocks, leading to malformed tests.
   - How encountered: A test failed due to lexer rejection.
   - Avoidance: Created a memo (`20260303-interpolation-conditional-syntax-outdated-memo.md`) to remind ourselves to backport parser updates into the markdown specifications.

---

## Related Projex Updates

### Documents to Update
| Document | Update Needed |
|----------|---------------|
| `20260225-string-interpolation-formatting-plan.md` | Mark as Complete |
