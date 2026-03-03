# Interpolation Conditional Syntax Update - C# Implementation Plan

> **Status:** Ready
> **Created:** 2026-03-03
> **Author:** Antigravity
> **Source:** [20260303-interpolation-conditional-syntax-outdated-memo.md](../../projex/20260303-interpolation-conditional-syntax-outdated-memo.md)
> **Related Projex:** [../../projex/20260303-interpolation-conditional-syntax-spec-plan.md](../../projex/20260303-interpolation-conditional-syntax-spec-plan.md)

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
Instead of wrapping the entire match content of `$?{...}` and `$#{...}` inside `()` before tokenizing, we shouldn't insert these parentheses until *after* peeling off the format string. Alternatively, we can just let `ExpressionEvaluator` tokenize the string without the `$?(` prefix, parse the AST naturally up to the format separator, and *then* execute the parsed node as a generic evaluation. However, the `$?` and `$#` operators are inherently unary operators inside expressions, requiring `ParseConditionalOrAny` and `ParseCount` logic.

### Step 1: Update Evaluator Translation Order
**Objective:** Apply the evaluation formatting correctly for `$?{}` features.
**Files:**
- `s:/repos/zoh/csharp/src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs`

**Changes:**
Currently, `EvaluateInterpolationMatch(MatchResult match)` does string concatenation right at the top for `$?{`. 
```csharp
// Before:
        else if (match.OpenToken == "$?{") exprSource = "$?(" + match.Content + ")";
```
Wait to append `$?(` and `)` until *after* the format suffix split. Or better yet: tokenize just `match.Content`, parse the AST (which handles the conditional branch without needing `$?`), and evaluate. 

Actually, `$?` in expressions means `Any/Conditional` operator (which uses `|` or `? :`). Wait, if `match.Content` is `cond ? A : B`, we can literally just tokenize `cond ? A : B`, use the standard `parser.Parse()` (because `? :` is just the ternary operator `ParseLogicalOr()`!). We don't need `$?(` wrapping for ternary conditionals! The symbol `$?` in `spec/2_verbs.md` is simply Zoh's way of marking the *start* of the interpolation conditional block `$?{ }`. 

Similarly, for `$#{ list }`, we are just parsing `list` and evaluating it, then returning its `.Length`. We do not strictly need the parser to evaluate it via `$#()`.

Let's modify `EvaluateInterpolationMatch`:
```csharp
    private ZohValue EvaluateInterpolationMatch(MatchResult match)
    {
        string exprSource;

        if (match.OpenToken == "${")
        {
            var parts = match.Content.Split("...", 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                // ... same unroll logic
            }
            exprSource = match.Content;
        }
        else if (match.OpenToken == "$#{") exprSource = match.Content; // Changed
        else if (match.OpenToken == "$?{") exprSource = match.Content; // Changed
        else throw new Exception("Unknown token " + match.OpenToken);

        var lexer = new Lexer(exprSource, false);
        var result = lexer.Tokenize();
        if (result.Errors.Length > 0)
            throw new Exception("Lexer error: " + result.Errors[0].Message);

        var parser = new ExpressionParser(result.Tokens);
        var ast = parser.Parse();
        var val = Evaluate(ast);
        
        // Execute special logic based on OpenToken
        if (match.OpenToken == "$#{")
        {
            val = val switch
            {
                ZohStr s => new ZohInt(s.Value.Length),
                ZohList l => new ZohInt(l.Items.Length),
                IZohMap m => new ZohInt(m.Count),
                ZohNothing => new ZohInt(0),
                _ => throw new Exception($"Cannot count type {val.Type}")
            };
        }
        else if (match.OpenToken == "$?{")
        {
            // The AST parsed cond ? A : B natively without `$?(` because `? :` is a standard operator at the root level!
            // Wait, does Parse() support standalone Any `A | B | C` without `$?(`? No, `Parse()` expects expressions. 
            // If Any is needed: `$?{ A | B }`, we will need to wrap it.
        }
        // ... format extraction logic ...
```

Wait, if we tokenize `A | B`, the standard `ExpressionParser.Parse()` will compile it as a `BinaryExpression(Pipe)`. But ZOH `|` means "Any" inside `$?()`.
Actually, looking at `ExpressionParser.cs`, `ParsePrimary()` handles `$?(`.
Since we're evaluating the content *inside* `$?{...}`, it's safer to just extract the format suffix textually using a Regex or string scan *before* wrapping it in `$?()`.

Let's simplify.
```csharp
        string innerContent = match.Content;
        string formatSuffix = "";
        
        // A naive but safe split: find the last unescaped comma or colon OUTSIDE of string literals.
        // Actually, just using Lexer on the RAW innerContent is perfect to find the format boundary.
        var lexer = new Lexer(innerContent, false);
        var result = lexer.Tokenize();
        if (result.Errors.Length > 0) throw new Exception("Lexer error: " + result.Errors[0].Message);

        // We can just use the standard ExpressionParser! It will stop parsing when it hits the `,` or `:` format specifier.
        var parser = new ExpressionParser(result.Tokens);
        AstNode innerAst;
        
        if (match.OpenToken == "${") innerAst = parser.Parse();
        else if (match.OpenToken == "$#{") 
        {
            innerAst = parser.Parse(); // Parse the inner list var
            innerAst = new CountExpressionAst(innerAst as ExpressionAst); 
        }
        else if (match.OpenToken == "$?{") 
        {
            // ParseConditionalOrAny expects to be called AFTER `$?(` is consumed. 
            // We can add a public `ParseInterpolationConditionalOrAny()` helper to ExpressionParser.
            innerAst = parser.ParseInterpolationConditionalOrAny();
        }

        int consumed = parser.ConsumedTokensCount;
        if (consumed < result.Tokens.Length - 1)
        {
             // extract formatSuffix same as before
        }
```

Wait, the existing `ExpressionParser` logic is perfectly fine! The problem is `exprSource = "$?(" + match.Content + ")"`. If `match.Content` is `cond?A:B,-4`, then we pass `$?(cond?A:B,-4)` to the Lexer! 
If we split `match.Content` into the actual expression and the format string *first*, we fix it. However, the only way to know where the expression ends and the format string begins is to *parse* it. 

**Changes:**
1. In `Zoh.Runtime/Expressions/ExpressionParser.cs`:
Add a helper for interpolation:
```csharp
    public ExpressionAst ParseInterpolationConditionalOrAny()
    {
        var first = ParseLogicalOr();
        if (Match(TokenType.Nothing)) // The ? token
        {
            var thenExpr = ParseLogicalOr();
            Match(TokenType.Colon); // Consume colon if present, or maybe it's optional? Wait, ternary expects `:`
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
2. In `Zoh.Runtime/Expressions/ExpressionEvaluator.cs`, in `EvaluateInterpolationMatch`:
Parse `match.Content` purely. If it's `${`, call `parser.Parse()`. If it's `$?{`, call `ParseInterpolationConditionalOrAny()`. If it's `$#{`, call `parser.Parse()` and wrap in a `CountExpressionAst`.
*Then* extract the format suffix from the remaining unconsumed tokens.
*Then* evaluate the AST. No need to reconstruct `$?(...)` strings and lex/parse them a second time!

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

