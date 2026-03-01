# Follow-up: Align C# Expression Special Forms with Expr Spec (`$(...)` and `$?(...?...:...)`)

> **Status:** In Progress
> **Created:** 2026-02-28
> **Author:** Codex
> **Source:** Direct request - follow-up from `20260224-expr-spec-fixes-impl-walkthrough.md`
> **Related Projex:** `20260223-csharp-spec-audit-nav.md`, `20260224-expr-spec-fixes-impl-walkthrough.md`, `20260224-expr-spec-fixes-impl-plan.md`

> **Partial Execution:** Implemented via `20260228-expression-special-forms-spec-parity-followup-patch.md` (code changes complete, automated tests pending)

---

## Summary

The C# expression parser still diverges from the current expression spec in two places: it accepts bare `$(...)` as `AnyExpressionAst` instead of requiring `[index]` or `[%]`, and it parses ternary `$?(cond ? then | else)` with `|` instead of the spec-defined `:` separator. This plan closes those parser-level gaps and updates tests so behavior is locked to `spec/expr.md`.

**Scope:** Expression parser and expression parser/evaluator tests in `csharp/`.
**Estimated Changes:** 3 files (`ExpressionParser.cs`, `ExpressionParserComplianceTests.cs`, `ExpressionTests.cs`).

---

## Objective

### Problem / Gap / Need

`spec/expr.md` now defines:
- `conditional_form := '$?(' expression '?' expression ':' expression ')'`
- `$(options)` without `[index]` or `[%]` is a parse error and should direct authors to `$?(...)`.

Current C# behavior in `ExpressionParser` does not match:
- `ParseSpecialDollarParen()` returns `AnyExpressionAst` when no suffix is present.
- `ParseConditionalOrAny()` consumes `TokenType.Pipe` for ternary separator.

### Success Criteria
- [ ] Parsing bare `$(1|2|3)` throws a parse error that explains `[index]`/`[%]` is required and suggests `$?(`.
- [ ] Parsing `$?(*score > 10 ? "Win" : "Lose")` succeeds as `ConditionalExpressionAst`.
- [ ] Parsing `$?(*score > 10 ? "Win" | "Lose")` fails with a clear separator error.
- [ ] Existing valid any-form syntax `$?(a|b|c)` remains supported.
- [ ] Updated expression parser tests and expression eval tests pass.

### Out of Scope
- Lexer/token model redesign.
- Interpolation scanner changes.
- Other expression compliance items not related to these two special-form rules.

---

## Context

### Current State

- `csharp/src/Zoh.Runtime/Expressions/ExpressionParser.cs`
  - `ParseSpecialDollarParen()` currently returns `AnyExpressionAst` if `$(...)` is not followed by `[...]`.
  - `ParseConditionalOrAny()` currently uses `Consume(TokenType.Pipe, "Expected '|' in ternary")`.
- `csharp/tests/Zoh.Tests/Expressions/ExpressionParserComplianceTests.cs`
  - Contains tests that assert `$(...)` produces `AnyExpressionAst`.
  - Conditional example/comments claim colon but test input still uses pipe.
- `csharp/tests/Zoh.Tests/Expressions/ExpressionTests.cs`
  - Conditional eval tests currently use pipe separator.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `csharp/src/Zoh.Runtime/Expressions/ExpressionParser.cs` | Parses special forms | Reject bare `$(...)`; require `:` for ternary in `$?()` conditional path |
| `csharp/tests/Zoh.Tests/Expressions/ExpressionParserComplianceTests.cs` | AST-level parser compliance tests | Replace/adjust legacy assertions and add failure-path coverage |
| `csharp/tests/Zoh.Tests/Expressions/ExpressionTests.cs` | Runtime expression evaluation tests | Update ternary inputs to colon and add bare-`$()` parse-failure regression |

### Dependencies
- **Requires:** None.
- **Blocks:** Future closure of expression compliance findings in `20260223-csharp-spec-audit-nav.md`.

### Constraints
- Preserve `$?(a|b|c)` any-form behavior.
- Keep diagnostic style consistent with current parser exceptions.
- Stay fully within `csharp/` projex scope.

---

## Implementation

### Overview

Apply a targeted parser correction and test realignment: make `$(...)` strict (index/roll only), keep any-selection under `$?(...)`, and enforce spec ternary separator `:`.

---

### Step 1: Enforce Suffix Requirement for `$(...)`

**Objective:** Remove fallback acceptance of bare `$(...)`.

**Files:**
- `csharp/src/Zoh.Runtime/Expressions/ExpressionParser.cs`

**Changes:**

```csharp
// Before:
// $(expr) without suffix is now just a single-option Any/List, no longer Interpolate.
// It simply groups or selects first non-nothing (Any behavior).
return new AnyExpressionAst(options.ToImmutableArray());

// After:
throw new Exception(
    "'$(' option list requires '[index]' or '[%]' suffix; did you mean '$?(' for first-non-nothing selection?");
```

**Rationale:** Align parser behavior with `spec/expr.md` and impl doc correction; avoid silently accepting invalid syntax.

**Verification:** New parser/eval tests assert bare `$(...)` throws and message references `$?(`.

---

### Step 2: Align Ternary Separator to `:`

**Objective:** Parse conditional form using colon, not pipe.

**Files:**
- `csharp/src/Zoh.Runtime/Expressions/ExpressionParser.cs`

**Changes:**

```csharp
// Before:
Consume(TokenType.Pipe, "Expected '|' in ternary");

// After:
Consume(TokenType.Colon, "Expected ':' in ternary");
```

**Rationale:** `spec/expr.md` defines conditional form as `...? ... : ...` and parser should match.

**Verification:** Parser test with `:` succeeds; legacy `|` variant fails.

---

### Step 3: Update Parser Compliance Tests

**Objective:** Replace obsolete expectations and add explicit negative cases.

**Files:**
- `csharp/tests/Zoh.Tests/Expressions/ExpressionParserComplianceTests.cs`

**Changes:**
- Replace `Parse_Selection_SingleItem` and `Parse_Selection_MultipleItems` assumptions that bare `$(...)` returns `AnyExpressionAst`.
- Add tests:
  - `Parse_Selection_WithoutSuffix_Throws`
  - `Parse_Conditional_Ternary_UsesColon`
  - `Parse_Conditional_Ternary_WithPipe_Throws`
- Keep `$?(...)` any-form tests unchanged to confirm no regression.

**Rationale:** Current tests encode legacy behavior and prevent spec-aligned fixes.

**Verification:** Filtered parser compliance test run passes.

---

### Step 4: Update Expression Evaluation Tests for Ternary Syntax and Bare `$(...)` Error

**Objective:** Ensure evaluation-level coverage reflects parser rules.

**Files:**
- `csharp/tests/Zoh.Tests/Expressions/ExpressionTests.cs`

**Changes:**
- Update conditional expression inputs from `|` to `:` in existing tests.
- Replace `Eval_OptionList_NoInterpolation` (legacy bare `$()` acceptance) with a failure assertion for bare `$()`.

**Rationale:** Evaluation tests currently rely on syntax that should be rejected under current spec.

**Verification:** Expression tests pass with updated syntax; bare `$()` fails as expected.

---

## Verification Plan

### Automated Checks
- [ ] `dotnet test --filter "FullyQualifiedName~ExpressionParserComplianceTests|FullyQualifiedName~ExpressionTests"`
- [ ] `dotnet test`

### Manual Verification
- [ ] Confirm parser exception for `$(1|2)` mentions `[index]`/`[%]` and `$?(` guidance.
- [ ] Confirm `$?(*x ? 1 : 0)` parses/evaluates successfully.
- [ ] Confirm `$?(*x ? 1 | 0)` is rejected.
- [ ] Confirm `$?(1|2|3)` still parses as any-form.

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Bare `$()` is rejected | New parser + eval failure tests | Exception raised with guidance message |
| Ternary uses `:` | Updated parser/eval tests with `:` | Parses and evaluates correctly |
| Legacy ternary `|` rejected | New parser failure test | Clear separator error |
| Any-form unaffected | Existing `$?(...)` tests | Pass unchanged |
| No regressions | Targeted + full test runs | 0 failures |

---

## Rollback Plan

1. Revert parser edits in `ExpressionParser.cs`.
2. Revert test updates in `ExpressionParserComplianceTests.cs` and `ExpressionTests.cs`.
3. Re-run targeted expression tests to confirm baseline behavior restored.

---

## Notes

### Assumptions
- Expression parser exception-based syntax errors are acceptable for these failure paths.
- Existing consumers should migrate from legacy `|` ternary syntax to `:` without requiring compatibility mode.
- Weighted roll (`$(a:1|b:2)[%]`) remains unaffected because this change only touches ternary separator and bare-`$()` fallthrough.

### Risks
- **Compatibility risk:** Existing scripts/tests using `|` ternary or bare `$(...)` will break.
  - **Mitigation:** Include precise guidance in error text and update tests/examples in the same change.
- **Coverage risk:** Parser-only changes might miss evaluator entry paths.
  - **Mitigation:** Validate both parser compliance and evaluator tests.

### Open Questions
- [ ] None.
