# List Concatenation via `+` Operator Plan

> **Status:** Ready
> **Created:** 2026-02-25
> **Author:** Antigravity
> **Source:** Direct request from 20260223-csharp-spec-audit-nav.md (GAP 2.5)
> **Related Projex:** [20260223-csharp-spec-audit-nav.md](20260223-csharp-spec-audit-nav.md)

---

## Summary

This plan implements list concatenation using the `+` binary operator. Currently, `ExpressionEvaluator.EvaluateBinary` throws an `InvalidOperationException` when attempting to add two `ZohList` instances together. This plan updates the evaluator to produce a combined `ZohList` with the elements of both lists appended, and adds corresponding unit tests to verify the behavior.

**Scope:** `Zoh.Runtime.Expressions.ExpressionEvaluator` and its related tests.
**Estimated Changes:** 2 files modified.

---

## Objective

### Problem / Gap / Need
As identified in Phase 2.5 of the C# Runtime Compliance Audit Roadmap, list concatenation via the `+` operator is missing. A script attempting to execute `` `[1] + [2]` `` currently crashes the runtime with an `InvalidOperationException` instead of evaluating to `[1, 2]`.

### Success Criteria
- [ ] Evaluating expression `` `[1] + [2]` `` yields a `ZohList` containing `1` and `2`.
- [ ] Attempting to add a `ZohList` to a non-list (e.g., `ZohInt`) raises an `InvalidOperationException` (unless explicitly coerced to a string where one operand is a string).
- [ ] The `+` operator does not mutate the original lists but correctly returns a new, combined `ZohList`.

### Out of Scope
- Map concatenation (undefined per spec).
- Broad changes to other arithmetic operators.

---

## Context

### Current State
`ExpressionEvaluator.EvaluateBinary` restricts the `+` operator strictly to `ZohStr`, `ZohInt`, and `ZohFloat`. Falling through these checks triggers an `InvalidOperationException`.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs` | Evaluates AST nodes into `ZohValue`s. | Update `EvaluateBinary`'s `+` operator case to support `ZohList` addition. |
| `tests/Zoh.Tests/Expressions/ExpressionTests.cs` | Tests expression parsing and evaluation. | Add a new test method `Eval_ListConcatenation` to verify concatenating lists works properly. |

### Dependencies
- **Requires:** None.
- **Blocks:** None.

### Constraints
- The `+` operator must produce a new `ZohList` instance containing the concatenated items.
- Operation must use `ImmutableArray`'s non-destructive methods (like `AddRange`).

---

## Implementation

### Overview
1.  Update the switch case for `TokenType.Plus` in `ExpressionEvaluator.EvaluateBinary` to check for `ZohList` operands and return their combined values.
2.  Add a comprehensive unit test to `ExpressionTests.cs` to ensure concatenation works correctly.

### Step 1: Update ExpressionEvaluator

**Objective:** Add logic to support list concatenation via `+`.

**Files:**
- `src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs`

**Changes:**

```csharp
// Before:
            case TokenType.Plus:
                if (left is ZohStr || right is ZohStr)
                    return new ZohStr(left.ToString() + right.ToString());
                if (left is ZohInt li && right is ZohInt ri)
                    return new ZohInt(li.Value + ri.Value);
                if (left is ZohFloat || right is ZohFloat)

// After:
            case TokenType.Plus:
                if (left is ZohStr || right is ZohStr)
                    return new ZohStr(left.ToString() + right.ToString());
                if (left is ZohList ll && right is ZohList rl)
                    return new ZohList(ll.Items.AddRange(rl.Items));
                if (left is ZohInt li && right is ZohInt ri)
                    return new ZohInt(li.Value + ri.Value);
                if (left is ZohFloat || right is ZohFloat)
```

**Rationale:** `ImmutableArray<ZohValue>.AddRange` correctly returns a new `ImmutableArray` of the combined items, matching the immutable semantics of `ZohList`. String coercion rightfully retains highest priority.

**Verification:** Build should succeed. The logic directly maps the left and right items.

---

### Step 2: Add Unit Tests

**Objective:** Verify that list concatenation yields a correct list and prevents invalid concatenations.

**Files:**
- `tests/Zoh.Tests/Expressions/ExpressionTests.cs`

**Changes:**
Add the following method to the `ExpressionTests` class:

```csharp
// Before:
    [Fact]
    public void Eval_StringConcat()
    {
        Assert.Equal(new ZohStr("Hello World"), Eval("\"Hello \" + \"World\""));
        Assert.Equal(new ZohStr("Value: 10"), Eval("\"Value: \" + 10"));
    }

// After:
    [Fact]
    public void Eval_StringConcat()
    {
        Assert.Equal(new ZohStr("Hello World"), Eval("\"Hello \" + \"World\""));
        Assert.Equal(new ZohStr("Value: 10"), Eval("\"Value: \" + 10"));
    }

    [Fact]
    public void Eval_ListConcat()
    {
        _variables.Set("list1", new ZohList([new ZohInt(1), new ZohInt(2)]));
        _variables.Set("list2", new ZohList([new ZohInt(3), new ZohInt(4)]));
        
        var result = Eval("*list1 + *list2");
        Assert.IsType<ZohList>(result);
        
        var listResult = (ZohList)result;
        Assert.Equal(4, listResult.Items.Length);
        Assert.Equal(new ZohInt(1), listResult.Items[0]);
        Assert.Equal(new ZohInt(2), listResult.Items[1]);
        Assert.Equal(new ZohInt(3), listResult.Items[2]);
        Assert.Equal(new ZohInt(4), listResult.Items[3]);

        // List + Non-List throws error
        Assert.Throws<InvalidOperationException>(() => Eval("*list1 + 5"));
        
        // However, String + List results in a string
        var strConcat = Eval("\"Items: \" + *list1");
        Assert.IsType<ZohStr>(strConcat);
        Assert.Equal("Items: [1, 2]", ((ZohStr)strConcat).Value);
    }
```

**Rationale:** It proves that two list expressions combine into one, prevents invalid interactions, and confirms that implicit string conversion of a list still works.

**Verification:** Run `dotnet test` filtered on `Eval_ListConcat`. It should pass.

---

## Verification Plan

### Automated Checks
- [ ] Run `dotnet test` on `Zoh.Tests` specifically looking for expression evaluations to pass.

### Manual Verification
- [ ] No manual UI verification requires checking. The unit tests fully validate AST processing.

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| `[1] + [2]` is `[1, 2]` | Run `Eval_ListConcat` | Test passes checking array length and items. |
| Type safety | Run `Eval_ListConcat` | Attempting `[1] + 5` throws `InvalidOperationException`. |

---

## Rollback Plan

If implementation fails or causes issues:

1. Identify the failing compilation or test step.
2. Remove the typecheck for `ZohList` and delete the `Eval_ListConcat` test to revert to baseline.
3. Fix issues or discuss ambiguity.

---

## Notes

### Assumptions
- String concatenation correctly prioritizes string conversion of Lists. (Since `ZohList.ToString()` is implemented, `left is ZohStr || right is ZohStr` captures this implicitly).

### Risks
- None.

### Open Questions
- [ ] None.
