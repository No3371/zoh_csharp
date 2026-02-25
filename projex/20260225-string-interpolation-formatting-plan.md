---
description: Plan for implementing C#-style formatting in ZOH string interpolation.
---

# String Interpolation Formatting Implementation

> **Status:** Ready
> **Created:** 2026-02-25
> **Author:** Antigravity
> **Source:** Direct request from spec audit compliance
> **Related Projex:** [20260223-csharp-spec-audit-nav.md](../20260223-csharp-spec-audit-nav.md)

---

## Summary

This plan implements C#-style formatting for string interpolation (`${*var,width:format}`) in the ZOH C# reference runtime. It addresses a known gap identified during the Phase 2 specification audit.

**Scope:** Modifying `ExpressionEvaluator` and `ExpressionParser` to correctly extract and apply format specifiers without breaking complex expressions.
**Estimated Changes:** 2 files modified, 1 test file updated.

---

## Objective

### Problem / Gap / Need
The ZOH specification for the `/interpolate` verb states that interpolation should support C# composite format parity, i.e., `${var[,width][:formatString]}` (e.g., `${*balance,8:N1}`). Currently, `ExpressionEvaluator.EvaluateInterpolationMatch` treats the entire bracketed content as an expression, which fails if a comma or colon is present because those are not valid in standard expressions.

### Success Criteria
- [ ] Interpolation matches correctly parse standard C#-style `,width` and `:format` specifiers.
- [ ] Values are correctly formatted according to `InvariantCulture`.
- [ ] Complex expressions containing internal colons (like `$(1:10)[%]`) do not cause parser confusion.
- [ ] `ExpressionTests.cs` includes tests for interpolation formatting.

### Out of Scope
- Implementing other interpolation spec features like unrolling or count if they are already working.
- Modifying string formatting logic inside core variables beyond interpolation matching.

---

## Context

### Current State
`EvaluateInterpolationMatch` directly uses `match.Content` and passes it to `EvaluateExprString`, which relies on `ExpressionLexer` and `ExpressionParser`. The specifier suffix causes syntax errors. 

### Key Files
| File | Purpose | Changes Needed |
|------|---------|----------------|
| `Zoh.Runtime/Expressions/ExpressionParser.cs` | Parses expression AST | Add a property to expose the number of consumed tokens. |
| `Zoh.Runtime/Expressions/ExpressionEvaluator.cs` | Evaluates expressions and interpolation | Update `EvaluateInterpolationMatch` to parse the AST first, detect trailing tokens, extract the format suffix by string offset, and use `string.Format`. |
| `Zoh.Tests/Expressions/ExpressionTests.cs` | Expression unit tests | Add `Eval_Interpolation_Formatting` test. |

### Dependencies
- **Requires:** None.
- **Blocks:** Full Phase 2 spec compliance.

---

## Implementation

### Overview
We will leverage `ExpressionParser`'s robust parsing to safely isolate the true expression from any suffix. By exposing how many tokens the parser consumes, the evaluator can reliably find the start of the format string in `match.Content` (using the first trailing token's string offset), parse the format suffix using Regex, and apply the formatting via standard C# `string.Format`.

### Step 1: Expose Parser State

**Objective:** Allow `ExpressionEvaluator` to determine where the expression ends and the format suffix begins.

**Files:**
- `s:/repos/zoh/c#/src/Zoh.Runtime/Expressions/ExpressionParser.cs`

**Changes:**
Add the `ConsumedTokensCount` property:

```csharp
// Before:
public class ExpressionParser(ImmutableArray<Token> tokens)
{
    private int _current = 0;

// After:
public class ExpressionParser(ImmutableArray<Token> tokens)
{
    private int _current = 0;

    public int ConsumedTokensCount => _current;
```

**Rationale:** Exposing `_current` is the safest way to find the boundary without duplicating grammar logic.

### Step 2: Implement Formatting Logic

**Objective:** Extract the format specifier and apply it using `string.Format`.

**Files:**
- `s:/repos/zoh/c#/src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs`

**Changes:**
In `EvaluateInterpolationMatch(MatchResult match)`:

```csharp
// Before:
        if (!string.IsNullOrEmpty(match.Suffix))
        {
            exprSource = "$(" + exprSource + ")" + match.Suffix;
        }

        return EvaluateExprString(exprSource);

// After:
        if (!string.IsNullOrEmpty(match.Suffix))
        {
            exprSource = "$(" + exprSource + ")" + match.Suffix;
        }

        var lexer = new Lexer(exprSource, false);
        var result = lexer.Tokenize();
        if (result.Errors.Length > 0)
            throw new Exception("Lexer error: " + result.Errors[0].Message);

        var parser = new ExpressionParser(result.Tokens);
        var ast = parser.Parse();
        var val = Evaluate(ast);

        if (parser.ConsumedTokensCount < result.Tokens.Length - 1)
        {
            var firstTrailingToken = result.Tokens[parser.ConsumedTokensCount];
            if (firstTrailingToken.Type == TokenType.Comma || firstTrailingToken.Type == TokenType.Colon)
            {
                var suffixOffset = firstTrailingToken.Start.Offset;
                var formatSuffix = exprSource.Substring(suffixOffset);

                var fmtMatch = System.Text.RegularExpressions.Regex.Match(formatSuffix, @"^(?:\s*,\s*(-?\d+))?(?:\s*:(.*))?$");
                if (fmtMatch.Success)
                {
                    string widthStr = fmtMatch.Groups[1].Value;
                    string formatStr = fmtMatch.Groups[2].Value;

                    string csFormat = "{0";
                    if (!string.IsNullOrEmpty(widthStr)) csFormat += "," + widthStr;
                    if (!string.IsNullOrEmpty(formatStr)) csFormat += ":" + formatStr;
                    csFormat += "}";

                    object? clrValue = val switch
                    {
                        ZohInt i => i.Value,
                        ZohFloat f => f.Value,
                        ZohStr s => s.Value,
                        ZohBool b => b.Value,
                        ZohNothing => "?",
                        _ => val.ToString()
                    };

                    return new ZohStr(string.Format(System.Globalization.CultureInfo.InvariantCulture, csFormat, clrValue));
                }
            }
            throw new Exception("invalid_syntax: Unexpected tokens after interpolation expression");
        }

        return val;
```

Remove the `EvaluateExprString(exprSource);` call in this method since we evaluate it inline now.

*(Note: Ensure `System.Globalization` usage is correct)*

**Rationale:** Utilizing the parser strictly separates the expression from its formatting suffix, completely avoiding bugs with nested delimiters.

### Step 3: Add Unit Tests

**Objective:** Prevent regressions and verify correctness.

**Files:**
- `s:/repos/zoh/c#/tests/Zoh.Tests/Expressions/ExpressionTests.cs`

**Changes:**
Add the following test method:

```csharp
    [Fact]
    public void Eval_Interpolation_Formatting()
    {
        _variables.Set("balance", new ZohFloat(100.0));
        _variables.Set("name", new ZohStr("John"));

        // Width logic
        Assert.Equal(new ZohStr("|John   |"), Eval("$\"|${*name,-7}|\""));
        Assert.Equal(new ZohStr("|   John|"), Eval("$\"|${*name,7}|\""));

        // Format logic
        Assert.Equal(new ZohStr("Bal: 100.00"), Eval("$\"Bal: ${*balance:F2}\""));

        // Width + Format
        // $`...` parses an expression from the string. Let's just give it a Zoh.Runtime.Expressions.ExpressionLexer string.
        Assert.Equal(new ZohStr("Bal:   100.0"), Eval("$\"Bal: ${*balance,7:F1}\""));
        
        // Literal inside string shouldn't break parser
        _variables.Set("dict", new ZohStr("Value: 1"));
        Assert.Equal(new ZohStr("Value: 1  "), Eval("$\"${*dict,-10}\""));
    }
```

**Rationale:** Covers width alignment, formatting strings, and their combination.

---

## Verification Plan

### Automated Checks
- [ ] `dotnet test --filter "FullyQualifiedName~ExpressionTests"` (Should pass all)

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Parsing C# style specs | Run the new unit tests | Tests pass demonstrating `,width` and `:format` are parsed |
| Complex expressions | Existing tests will implicitly check | No regressions on complex interpolation tests |

---

## Rollback Plan

If implementation fails or causes issues:

1. Revert `ExpressionEvaluator.cs` to use `EvaluateExprString`.
2. Remove the `ConsumedTokensCount` property from `ExpressionParser`.

---

## Notes

### Assumptions
- `Lexer` preserves `Start.Offset` correctly relative to `exprSource` string.
- `ZohValue` maps elegantly to CLR `long` and `double` for `IFormattable`.

### Open Questions
- [ ] None.
