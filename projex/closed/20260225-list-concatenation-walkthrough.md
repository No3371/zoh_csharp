# Walkthrough: List Concatenation via `+` Operator Plan

> **Execution Date:** 2026-02-25
> **Completed By:** Antigravity
> **Source Plan:** [20260225-list-concatenation-plan.md](20260225-list-concatenation-plan.md)
> **Duration:** 10 minutes
> **Result:** Success

---

## Summary

Successfully implemented list concatenation using the `+` binary operator. The expression evaluator now seamlessly combines two `ZohList` instances into a new `ZohList` instance while preserving the correct error handling semantics for invalid string combinations. Tests were added to ensure type safety and expected results.

---

## Objectives Completion

| Objective | Status | Notes |
|-----------|--------|-------|
| Evaluate `[1] + [2]` to `[1, 2]` | Complete | Added support in `ExpressionEvaluator.EvaluateBinary`. |
| Add `ZohList` to non-list raises error | Complete | Evaluator correctly falls through to default error unless coercion applies. |
| Original lists are not mutated | Complete | Utilized `ImmutableArray.AddRange` which returns a new instance. |

---

## Execution Detail

> **NOTE:** This section documents what ACTUALLY happened, derived from git history and execution notes. 
> Differences from the plan are explicitly called out.

### Step 1: Update ExpressionEvaluator

**Planned:** Update the switch case for `TokenType.Plus` in `ExpressionEvaluator.EvaluateBinary` to check for `ZohList` operands and return their combined values.

**Actual:** Modified `EvaluateBinary` in `src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs`. Checked if `left` and `right` were both `ZohList` and then returned a new `ZohList` created via `ll.Items.AddRange(rl.Items)`.

**Deviation:** None

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs` | Modified | Yes | Lines 306-307: Added list addition behavior under `TokenType.Plus`. |

**Verification:** Ran `dotnet build` successfully.

**Issues:** None

---

### Step 2: Add Unit Tests

**Planned:** Add a unit test to `ExpressionTests.cs` to ensure concatenation works correctly and throws errors appropriately for invalid operations.

**Actual:** Added the `Eval_ListConcat` test case to `tests/Zoh.Tests/Expressions/ExpressionTests.cs`. It tests list combination, throwing an `InvalidOperationException` for invalid list-to-non-list operations, and validating fallback string coercion.

**Deviation:** None

**Files Changed (ACTUAL):**
| File | Change Type | Planned? | Details |
|------|-------------|----------|---------|
| `tests/Zoh.Tests/Expressions/ExpressionTests.cs` | Modified | Yes | Lines 92-115: Added `Eval_ListConcat` test method with 3 scenarios. |

**Verification:** Ran `dotnet test --filter Eval_ListConcat` successfully. The full test suite of 589 tests was also verified and passed.

**Issues:** None

---

## Complete Change Log

> **Derived from:** `git diff --stat main..HEAD` 

### Files Created
| File | Purpose | Lines | In Plan? |
|------|---------|-------|----------|
| `projex/20260225-list-concatenation-log.md` | Execution logging | 35 | Yes |

### Files Modified
| File | Changes | Lines Affected | In Plan? |
|------|---------|----------------|----------|
| `src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs` | Added array concatenation logic for `Plus` operator | +2 | Yes |
| `tests/Zoh.Tests/Expressions/ExpressionTests.cs` | Added new unit test `Eval_ListConcat` | +25 | Yes |
| `projex/20260225-list-concatenation-plan.md` | Updated status to Complete | +1, -1 | Yes |

### Files Deleted
None

### Planned But Not Changed
None

---

## Success Criteria Verification

### Criterion 1: Evaluating expression `[1] + [2]` yields a `ZohList` containing `1` and `2`

**Verification Method:** Unit Test `Eval_ListConcat`

**Evidence:**
```csharp
var result = Eval("*list1 + *list2");
Assert.IsType<ZohList>(result);

var listResult = (ZohList)result;
Assert.Equal(4, listResult.Items.Length);
Assert.Equal(new ZohInt(1), listResult.Items[0]);
Assert.Equal(new ZohInt(2), listResult.Items[1]);
Assert.Equal(new ZohInt(3), listResult.Items[2]);
Assert.Equal(new ZohInt(4), listResult.Items[3]);
```

**Result:** PASS

---

### Criterion 2: Attempting to add a `ZohList` to a non-list raises an `InvalidOperationException`

**Verification Method:** Unit Test `Eval_ListConcat`

**Evidence:**
```csharp
Assert.Throws<InvalidOperationException>(() => Eval("*list1 + 5"));
```

**Result:** PASS

---

### Criterion 3: The `+` operator does not mutate the original lists but correctly returns a new, combined `ZohList`

**Verification Method:** Code inspection and immutable constraints

**Evidence:**
```csharp
return new ZohList(ll.Items.AddRange(rl.Items));
```
`ImmutableArray<T>.AddRange` does not mutate the source array; it returns a new array.

**Result:** PASS

---

### Acceptance Criteria Summary

| Criterion | Method | Result | Evidence |
|-----------|--------|--------|----------|
| `[1] + [2]` evaluates correctly | Test | Pass | `Eval_ListConcat` |
| Invalid concatenations blocked | Test | Pass | `Eval_ListConcat` |
| Non-mutating behavior preserved | Code | Pass | Line 307 |

**Overall:** 3/3 criteria passed

---

## Deviations from Plan

None.

---

## Issues Encountered

None.

---

## Key Insights

### Technical Insights

- Immutable structures in C# (`ImmutableArray`) map very cleanly to strict specification limits in ZOH execution design, allowing operators like `+` to safely be extended without accidental side-effects.
- String coercion behavior seamlessly acts as a strict priority layer above other binary operations, preventing conflicting overload issues between adding lists and converting lists to strings.

---

## Recommendations

### Immediate Follow-ups
- [ ] No immediate changes required.

---

## Related Projex Updates

### Documents to Update
| Document | Update Needed |
|----------|---------------|
| `20260225-list-concatenation-plan.md` | Mark as Complete |

---

## Appendix

### Test Output
```
總共有 1 個測試檔案與指定的模式相符。
已通過! - 失敗:     0，通過:   589，略過:     0，總計:   589，持續時間: 99 ms - Zoh.Tests.dll (net8.0)
```
