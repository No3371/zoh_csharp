---
description: Plan for implementing C#-style formatting in ZOH string interpolation.
---

# String Interpolation Formatting Implementation

> **Status:** Complete (2026-03-03)
> **Walkthrough:** [20260225-string-interpolation-formatting-plan-walkthrough.md](../../projex/closed/20260225-string-interpolation-formatting-plan-walkthrough.md)
> **Created:** 2026-02-25
> **Author:** Antigravity
> **Source:** Direct request from spec audit compliance
> **Related Projex:** [20260223-csharp-spec-audit-nav.md](../20260223-csharp-spec-audit-nav.md)
> **Reviewed:** 2026-02-28 - [20260228-string-interpolation-formatting-plan-review.md](20260228-string-interpolation-formatting-plan-review.md)
> **Review Outcome:** Needs Modification (Step 2 ordering + mixed-syntax coverage gaps)

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
- [ ] Nested interpolation special forms (`$?{...}`, `$#{...}`, `$(...)[%]`) still work when used inside formatted interpolation expressions.
- [ ] If formatting is combined with scanner-level suffix (`}[...]`), behavior is deterministic and tested (supported with defined precedence or explicit `invalid_syntax`).
- [ ] `ExpressionTests.cs` includes formatting and mixed-syntax regression tests.

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
We will leverage `ExpressionParser`'s robust parsing to safely isolate the true expression from any format suffix. By exposing how many tokens the parser consumes, the evaluator can reliably find the start of `,width` / `:format` in the interpolation body, evaluate the core expression, then apply formatting. Scanner-level interpolation suffix (`match.Suffix`, i.e., trailing `[...]`) must be handled only after this split (or explicitly rejected when combined with formatting), to avoid parse-order conflicts.

### Step 1: Expose Parser State

**Objective:** Allow `ExpressionEvaluator` to determine where the expression ends and the format suffix begins.

**Files:**
- `s:/repos/zoh/csharp/src/Zoh.Runtime/Expressions/ExpressionParser.cs`

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

**Objective:** Extract `,width` / `:format` from interpolation body *before* applying scanner suffix handling, then apply formatting with `string.Format`.

**Files:**
- `s:/repos/zoh/csharp/src/Zoh.Runtime/Expressions/ExpressionEvaluator.cs`

**Known Break Examples (old ordering):**
- `${*name,7}[0]` -> old flow rewrites to `$(*name,7)[0]` and parser fails before format split.
- `${*balance:F2}[0]` -> old flow rewrites to `$(*balance:F2)[0]` and parser fails before format split.
- `${$(10|20|30)[%],2}[0]` -> old flow rewrites to `$($(10|20|30)[%],2)[0]` and fails early on trailing comma.

**Changes:**
In `EvaluateInterpolationMatch(MatchResult match)`:

```csharp
// Before:
        if (!string.IsNullOrEmpty(match.Suffix))
        {
            exprSource = "$(" + exprSource + ")" + match.Suffix;
        }

        return EvaluateExprString(exprSource);
```

```csharp
// After:
        var lexer = new Lexer(exprSource, false);
        var result = lexer.Tokenize();
        if (result.Errors.Length > 0)
            throw new Exception("Lexer error: " + result.Errors[0].Message);

        var parser = new ExpressionParser(result.Tokens);
        var ast = parser.Parse();
        var val = Evaluate(ast);

        bool hasFormatting = false;
        if (parser.ConsumedTokensCount < result.Tokens.Length - 1)
        {
            var firstTrailingToken = result.Tokens[parser.ConsumedTokensCount];
            if (firstTrailingToken.Type == TokenType.Comma || firstTrailingToken.Type == TokenType.Colon)
            {
                hasFormatting = true;
                var suffixOffset = firstTrailingToken.Start.Offset;
                var formatSuffix = exprSource.Substring(suffixOffset);
                var exprCoreSource = exprSource.Substring(0, suffixOffset).TrimEnd();
                var coreVal = EvaluateExprString(exprCoreSource);

                // Parse the format suffix: [,width][:formatString]
                // The format string is opaque — we extract it verbatim and delegate to string.Format.
                string? widthStr = null;
                string? formatStr = null;
                var s = formatSuffix.AsSpan().Trim();

                if (s.Length > 0 && s[0] == ',')
                {
                    s = s.Slice(1).TrimStart();
                    var colonIdx = s.IndexOf(':');
                    var widthSpan = colonIdx >= 0 ? s.Slice(0, colonIdx) : s;
                    widthStr = widthSpan.Trim().ToString();
                    if (colonIdx >= 0)
                        formatStr = s.Slice(colonIdx + 1).ToString();
                }
                else if (s.Length > 0 && s[0] == ':')
                {
                    formatStr = s.Slice(1).ToString();
                }

                string csFormat = "{0";
                if (!string.IsNullOrEmpty(widthStr)) csFormat += "," + widthStr;
                if (!string.IsNullOrEmpty(formatStr)) csFormat += ":" + formatStr;
                csFormat += "}";

                object? clrValue = coreVal switch
                {
                    ZohInt i => i.Value,
                    ZohFloat f => f.Value,
                    ZohStr s => s.Value,
                    ZohBool b => b.Value,
                    ZohNothing => "?",
                    _ => coreVal.ToString()
                };

                val = new ZohStr(string.Format(System.Globalization.CultureInfo.InvariantCulture, csFormat, clrValue));
            }
            else
            {
                throw new Exception("invalid_syntax: Unexpected tokens after interpolation expression");
            }
        }

        if (!string.IsNullOrEmpty(match.Suffix))
        {
            if (hasFormatting)
            {
                throw new Exception("invalid_syntax: formatting suffix (,width/:format) cannot be combined with interpolation suffix [..]");
            }
            exprSource = "$(" + exprSource + ")" + match.Suffix;
            return EvaluateExprString(exprSource);
        }

        return val;
```

Remove the old early `match.Suffix` wrapping and old `EvaluateExprString(exprSource);` return in this method since evaluation now needs explicit ordering.

**Rationale:** This ordering prevents the parse break where `,width` / `:format` is forced into `$(...)` parsing before split, e.g., `${*name,7}[0]` becoming `$(*name,7)[0]`.

### Step 3: Add Unit Tests

**Objective:** Prevent regressions and verify correctness.

**Files:**
- `s:/repos/zoh/csharp/tests/Zoh.Tests/Expressions/ExpressionTests.cs`

**Changes:**
Add the following test method:

```csharp
    [Fact]
    public void Eval_Interpolation_Formatting()
    {
        _variables.Set("balance", new ZohFloat(100.0));
        _variables.Set("name", new ZohStr("John"));
        _variables.Set("score", new ZohInt(10));
        _variables.Set("list", new ZohList([new ZohInt(1), new ZohInt(2), new ZohInt(3)]));

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

        // Nested special forms inside formatted interpolation
        Assert.Equal(new ZohStr("R: Win "), Eval("$\"R: ${$?{*score >= 10 ? 'Win' : 'Lose'},-4}\""));
        Assert.Equal(new ZohStr("C:  3"), Eval("$\"C: ${$#{*list},2}\""));
        Assert.Contains(Eval("$\"Pick: ${$(10|20|30)[%],2}\"").ToString(), new[] { "Pick: 10", "Pick: 20", "Pick: 30" });

        // Deterministic failure: formatting + scanner suffix [..] is unsupported
        var ex1 = Assert.Throws<Exception>(() => Eval("$\"${*name,7}[0]\""));
        Assert.Contains("cannot be combined", ex1.Message, StringComparison.Ordinal);

        // Deterministic failure: malformed width
        var ex2 = Assert.Throws<Exception>(() => Eval("$\"${*name,abc}\""));
        Assert.Contains("format", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }
```

**Rationale:** Covers formatting core behavior, nested-feature compatibility, and explicit unsupported/malformed cases.

---

## Verification Plan

### Automated Checks
- [ ] `dotnet test --filter "FullyQualifiedName~ExpressionTests"` (Should pass all)

### Acceptance Criteria Validation
| Criterion | How to Verify | Expected Result |
|-----------|---------------|-----------------|
| Parsing C# style specs | Run the new unit tests | Tests pass demonstrating `,width` and `:format` are parsed |
| Mixed interpolation syntax compatibility | Run explicit mixed tests in `Eval_Interpolation_Formatting` | Nested `$?{}`, `$#{}`, and `$(...)[%]` continue to work with formatting |
| Format + scanner suffix behavior | Run explicit negative test `${*name,7}[0]` | Deterministic `invalid_syntax` with clear message |

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
