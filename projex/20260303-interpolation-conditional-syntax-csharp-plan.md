# Interpolation Conditional Syntax Update - C# Implementation Plan

> **Status:** In Progress
> **Created:** 2026-03-03
> **Author:** Antigravity
> **Source:** [20260303-interpolation-conditional-syntax-outdated-memo.md](../../projex/20260303-interpolation-conditional-syntax-outdated-memo.md)
> **Related Projex:** [../../projex/20260303-interpolation-conditional-syntax-spec-plan.md](../../projex/20260303-interpolation-conditional-syntax-spec-plan.md)
> **Reviewed:** 2026-03-03 - [20260303-interpolation-conditional-syntax-csharp-plan-review.md](20260303-interpolation-conditional-syntax-csharp-plan-review.md)
> **Review Outcome:** Needs Modification (Modifications have been applied directly to this file to refine the steps).

---

## Summary

This plan updates the C# implementation to correctly handle the `$?{cond ? A : B}` syntax when combined with a string format suffix. Currently, if someone uses `$?{cond ? A : B, -4}`, the interpolator naively evaluates `$?(cond ? A : B, -4)`. The `ExpressionParser` attempts to parse this as a ternary expression, but the comma causes a parse exception because it expects the expression to end with `)` after the `else` branch.

**Scope:** `csharp/` folder ONLY.
**Estimated Changes:** 2 files modified.

---

## Objective

### Problem / Gap / Need
In commit `fa3090d`, formatting suffix extraction was added, which successfully strips `,width:format` off the end of interpolation features. However, for `$?{...}`, the `ExpressionEvaluator` naively translates this string into `$?(...)` before parsing. This means the parser is forced to evaluate `cond ? A : B, width:format` inside those parentheses as if it were a pure expression. When the parser reaches the `,` in the conditional parsing flow, it crashes expectedly.

### Success Criteria
- [ ] `$?{cond ? A : B, width:format}` formatting handles correctly without throwing parser errors.
- [ ] `ExpressionTests.cs` includes tests validating format suffixes with the interpolation specific syntactic blocks `$?{}` and `$#{}` directly, removing the nested `$?(...)` workaround tests.

### Out of Scope
- Specification updates (handled in the parent spec plan).
- Total rewrite of the `EvaluateInterpolationMatch` formatting logic.

---

## Context

### Current State
`ZohInterpolator.EvaluateInterpolationMatch` replaces the text content immediately, preventing a clean AST evaluation of `$?{}` when the remainder of the token stream contains formatting. The test currently resorts to using the nested `${ $?(...) , -4 }` workaround.

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `Zoh.Runtime/Expressions/ExpressionParser.cs` | Parses expressions | We need to parse interpolation features without strict parentheses. |
| `Zoh.Runtime/Expressions/ExpressionEvaluator.cs` | Evaluates interpolation matches | Detect the format suffix *before* wrapping the core `cond? A : B` into `$?(...)`. |

### Dependencies
- **Requires:** `projex/20260303-interpolation-conditional-syntax-spec-plan.md`

---

## Implementation

### Overview
Instead of wrapping the entire match content of `$?{...}` and `$#{...}` inside `()` before tokenizing, we shouldn't insert these parentheses at all. We will expose a helper in `ExpressionParser` to parse the inner conditional logic, and then evaluate the cleanly parsed `AstNode` directly.

### Step 1: Update Parser to expose Interpolation Conditional Logic
**Objective:** Allow parsing conditional syntax outside of a `$?(...)` literal.
**Files:**
- `s:/Repos/zoh/csharp/src/Zoh.Runtime/Expressions/ExpressionParser.cs`

**Changes:**
Add the following helper to cleanly parse the ternary conditional or the Any `|` without expecting a closing parenthesis:
```csharp
    public ExpressionAst ParseInterpolationConditionalOrAny()
    {
        var first = ParseLogicalOr();
        if (Match(TokenType.Nothing)) // The ? token
        {
            var thenExpr = ParseLogicalOr();
            Consume(TokenType.Colon, "Expected ':' in ternary");
            var elseExpr = ParseLogicalOr();
            return new ConditionalExpressionAst(first, thenExpr, elseExpr);
        }

        var options = new List<ExpressionAst> { first };
        while (Match(TokenType.Pipe))
        {
            options.Add(ParseLogicalOr());
        }
        return new AnyExpressionAst(options.ToImmutableArray());
    }
```

### Step 2: Refactor `EvaluateInterpolationMatch`
**Objective:** Parse the AST without string wrapping and evaluate it directly.
**Files:**
- `s:/Repos/zoh/csharp/src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs`

**Changes:**
Refactor `EvaluateInterpolationMatch` to:
1. Scan `match.Content` directly via Lexer.
2. Switch on `match.OpenToken` to parse the respective AST node (`parser.Parse()`, `ParseInterpolationConditionalOrAny()`, or wrapping the base parse in `CountExpressionAst`).
3. Evaluate the `ast` to `coreVal`.
4. If `ConsumedTokensCount` < tokens max, extract the formatting suffix exactly as before from `match.Content`.
5. Eliminate the redundant `EvaluateExprString(exprCoreSource)` call, instead using our `coreVal` directly.

*Implementation Guide:*
```csharp
    private ZohValue EvaluateInterpolationMatch(MatchResult match)
    {
        if (match.OpenToken == "${")
        {
            // Keep the exact `...` unroll parsing logic here
            var parts = match.Content.Split("...", 2, StringSplitOptions.None);
            if (parts.Length == 2)
            { ... }
        }

        var lexer = new Lexer(match.Content, false);
        var result = lexer.Tokenize();
        if (result.Errors.Length > 0)
            throw new Exception("Lexer error: " + result.Errors[0].Message);

        var parser = new ExpressionParser(result.Tokens);
        ExpressionAst ast;

        if (match.OpenToken == "${") ast = parser.Parse();
        else if (match.OpenToken == "$#{") 
        {
            ast = new CountExpressionAst(parser.Parse());
        }
        else if (match.OpenToken == "$?{") 
        {
            ast = parser.ParseInterpolationConditionalOrAny();
        }
        else throw new Exception("Unknown token " + match.OpenToken);

        ZohValue val = Evaluate(ast);
        ZohValue coreVal = val;

        bool hasFormatting = false;
        if (parser.ConsumedTokensCount < result.Tokens.Length - 1)
        {
            var firstTrailingToken = result.Tokens[parser.ConsumedTokensCount];
            if (firstTrailingToken.Type == TokenType.Comma || firstTrailingToken.Type == TokenType.Colon)
            {
                hasFormatting = true;
                var suffixOffset = firstTrailingToken.Start.Offset;
                var formatSuffix = match.Content.Substring(suffixOffset);

                // ... Keep exact formatting suffix logic `string csFormat = "{0"` ...
                // But use `coreVal` instead of re-evaluating `exprCoreSource`
```

### Step 2: Implement Regression Tests
**Objective:** Confirm successful resolution.
**Files:**
- `s:/repos/zoh/csharp/tests/Zoh.Tests/Expressions/ExpressionTests.cs`

**Changes:**
Navigate to `Eval_Interpolation_Formatting` and replace the workaround tests:
```csharp
Assert.Equal(new ZohStr("R: Win "), Eval("$\"R: ${$?(*score >= 10 ? 'Win' : 'Lose'),-4}\""));
Assert.Equal(new ZohStr("C:  3"), Eval("$\"C: ${$#(*list),2}\""));
```
with the native interpolation syntax tests:
```csharp
Assert.Equal(new ZohStr("R: Win "), Eval("$\"R: $?{*score >= 10 ? 'Win' : 'Lose',-4}\""));
Assert.Equal(new ZohStr("C:  3"), Eval("$\"C: $#{*list,2}\""));
```

---

## Verification Plan
- [ ] `dotnet test --filter "FullyQualifiedName~ExpressionTests"` (Should pass all tests, meaning parser cleanly separated the format suffix without parens errors)

---

## Rollback Plan
- Revert modifications to `ExpressionEvaluator.cs` and `ExpressionTests.cs`.

