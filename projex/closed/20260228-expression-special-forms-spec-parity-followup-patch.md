# Patch: Expression Special Forms Spec-Parity Follow-up (`$(...)` and `$?(...?...:...)`)

> **Date:** 2026-02-28
> **Author:** Codex
> **Directive:** `patch-projex`
> **Source Plan:** `20260228-expression-special-forms-spec-parity-followup-plan.md`
> **Result:** Partial Success

---

## Summary

Implemented the parser/spec parity fixes identified in the C# follow-up plan: bare `$(...)` now errors unless followed by `[index]` or `[%]`, and ternary `$?()` now requires `:` as the separator. Parser and evaluator tests were updated to match the new syntax and failure behavior. Automated test execution was intentionally skipped per user request due runtime cost.

---

## Changes

### Parser Special-Form Semantics

**File:** `csharp/src/Zoh.Runtime/Expressions/ExpressionParser.cs`  
**Change Type:** Modified  
**What Changed:**
- `ParseSpecialDollarParen()` now throws on bare `$(...)` with guidance to use `$?(` (`line 283`).
- `ParseConditionalOrAny()` now requires `TokenType.Colon` with message `Expected ':' in ternary` (`line 302`).

**Why:**
To align runtime parser behavior with `spec/expr.md` and the implementation walkthrough correction in `20260224-expr-spec-fixes-impl-walkthrough.md`.

---

### Parser Compliance Tests

**File:** `csharp/tests/Zoh.Tests/Expressions/ExpressionParserComplianceTests.cs`  
**Change Type:** Modified  
**What Changed:**
- Replaced legacy bare-`$()` acceptance tests with `Parse_Selection_WithoutSuffix_Throws` (`lines 41-46`).
- Updated conditional positive test to use `:` (`lines 60-71`).
- Added negative test `Parse_Conditional_Ternary_WithPipe_Throws` (`lines 74-78`).
- Updated nested ternary sample from `true?1|0` to `true?1:0` (`lines 130-131`).

**Why:**
Previous tests encoded outdated syntax/behavior and blocked the spec-aligned parser fix.

---

### Expression Evaluation Tests

**File:** `csharp/tests/Zoh.Tests/Expressions/ExpressionTests.cs`  
**Change Type:** Modified  
**What Changed:**
- Updated `Eval_Conditional` ternary inputs from `|` to `:` (`lines 131, 134`).
- Replaced `Eval_OptionList_NoInterpolation` with `Eval_OptionList_WithoutSuffix_Throws` (`lines 237-242`).
- Updated interpolation conditional sample from `|` to `:` (`line 252`).

**Why:**
Evaluator-facing tests must reflect parser semantics and guard against reintroducing bare-`$()` acceptance.

---

## Verification

**Method:** Static inspection of changed files and line-level validation.

**Result:**
```text
- Parser now throws for bare $(...) and guides toward $?(
- Parser ternary separator now requires ':'
- Parser/evaluator tests updated to use ':' and assert legacy '|' failures
- No automated tests run (skipped per user request)
```

**Status:** PARTIAL

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| `20260228-expression-special-forms-spec-parity-followup-plan.md` | Source plan | Status set to `In Progress`; partial execution note added referencing this patch |
| `20260224-expr-spec-fixes-impl-walkthrough.md` | Upstream trigger | Patch implements its C# follow-up recommendation |

---

## Notes

- Automated verification remains pending (`dotnet test` filters for expression parser/evaluator suites).
- If desired, a follow-up quick patch can run only narrow tests for the modified methods.
