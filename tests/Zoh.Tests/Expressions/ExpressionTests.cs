using Zoh.Runtime.Expressions;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;

namespace Zoh.Tests.Expressions;

public class ExpressionTests
{
    private readonly VariableStore _variables;
    private readonly ExpressionEvaluator _evaluator;

    public ExpressionTests()
    {
        _variables = new VariableStore(new Dictionary<string, Variable>());
        _evaluator = new ExpressionEvaluator(_variables);
    }

    private ZohValue Eval(string source)
    {
        var lexer = new ExpressionLexer(source, new TextPosition(1, 1, 0));
        var tokens = lexer.Tokenize().Tokens;
        var parser = new ExpressionParser(tokens);
        var ast = parser.Parse();
        return _evaluator.Evaluate(ast);
    }

    [Fact]
    public void Eval_Literals()
    {
        Assert.Equal(new ZohInt(1), Eval("1"));
        Assert.Equal(new ZohFloat(1.5), Eval("1.5"));
        Assert.Equal(ZohValue.True, Eval("true"));
        Assert.Equal(new ZohStr("foo"), Eval("\"foo\""));
    }

    [Fact]
    public void Eval_Arithmetic()
    {
        Assert.Equal(new ZohInt(3), Eval("1 + 2"));
        Assert.Equal(new ZohInt(-1), Eval("1 - 2"));
        Assert.Equal(new ZohInt(6), Eval("2 * 3"));
        Assert.Equal(new ZohInt(2), Eval("6 / 3"));
        Assert.Equal(new ZohInt(1), Eval("5 % 2"));

        // Precedence
        Assert.Equal(new ZohInt(7), Eval("1 + 2 * 3"));
        Assert.Equal(new ZohInt(9), Eval("(1 + 2) * 3"));
    }

    [Fact]
    public void Eval_Comparison()
    {
        Assert.Equal(ZohValue.True, Eval("1 < 2"));
        Assert.Equal(ZohValue.False, Eval("2 < 1"));
        Assert.Equal(ZohValue.True, Eval("2 == 2"));
        Assert.Equal(ZohValue.True, Eval("1 != 2"));
        Assert.Equal(ZohValue.True, Eval("2 >= 2"));
        Assert.Equal(ZohValue.True, Eval("2 >= 2"));
        Assert.Equal(ZohValue.True, Eval("3 >= 2"));

        // Case sensitivity
        Assert.Equal(ZohValue.False, Eval("\"a\" == \"A\""));
    }

    [Fact]
    public void Eval_Logical()
    {
        Assert.Equal(ZohValue.True, Eval("true && true"));
        Assert.Equal(ZohValue.False, Eval("true && false"));
        Assert.Equal(ZohValue.True, Eval("false || true"));

        // Short-circuit check?
        // Hard to check without side-effects in expression...
    }

    [Fact]
    public void Eval_Variables()
    {
        _variables.Set("x", new ZohInt(10));
        Assert.Equal(new ZohInt(11), Eval("*x + 1"));
        Assert.Equal(new ZohInt(20), Eval("*x * 2")); // With * prefix
    }

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
    [Fact]
    public void Eval_Count()
    {
        _variables.Set("l", new ZohList([new ZohInt(1), new ZohInt(2)]));
        _variables.Set("s", new ZohStr("hello"));

        Assert.Equal(new ZohInt(2), Eval("$#(*l)"));
        Assert.Equal(new ZohInt(5), Eval("$#(*s)"));
        Assert.Throws<InvalidOperationException>(() => Eval("$#(*missingVal)"));
    }

    [Fact]
    public void Eval_Conditional()
    {
        _variables.Set("score", new ZohInt(10));
        Assert.Equal(new ZohStr("Win"), Eval("$?(*score >= 10 ? \"Win\" : \"Lose\")"));

        _variables.Set("score", new ZohInt(5));
        Assert.Equal(new ZohStr("Lose"), Eval("$?(*score >= 10 ? \"Win\" : \"Lose\")"));
    }

    [Fact]
    public void Eval_Any()
    {
        // $?("A" | "B")
        var result = Eval("$?(\"A\" | \"B\")");
        // Result should be "A" (first non-nothing)
        Assert.Equal(new ZohStr("A"), result);

        // Test with Nothing
        Assert.Equal(new ZohStr("B"), Eval("$?(nothing | \"B\")"));

        // Test with Undefined variable -> Should throw
        Assert.Throws<InvalidOperationException>(() => Eval("$?(*missingVar | \"B\")"));

        // Empty Any? $?() -> Nothing
        // Note: Parser requires at least one option for LogicalOr.
    }

    [Fact]
    public void Eval_Indexed()
    {
        // Direct indexing from options
        Assert.Equal(new ZohInt(10), Eval("$(10|20|30)[0]"));
        Assert.Equal(new ZohInt(30), Eval("$(10|20|30)[2]"));
        Assert.Equal(new ZohInt(30), Eval("$(10|20|30)[-1]")); // Last item

        // Wrap indexing
        Assert.Equal(new ZohInt(30), Eval("$(10|20|30)[!-1]")); // Last item
        Assert.Equal(new ZohInt(10), Eval("$(10|20|30)[!3]"));  // Wrap to 0
    }

    [Fact]
    public void Eval_Roll()
    {
        // Non-deterministic, check if result is one of options
        var result = Eval("$(10|20|30)[%]");
        var val = ((ZohInt)result).Value;
        Assert.Contains(val, new long[] { 10, 20, 30 });
    }

    [Fact]
    public void Eval_NewInterpolationSyntax()
    {
        // New Syntax: $"string"
        _variables.Set("name", new ZohStr("World"));
        Assert.Equal(new ZohStr("Hello World!"), Eval("$\"Hello ${*name}!\""));

        // Nested interpolation
        _variables.Set("inner", new ZohStr("Inner"));
        Assert.Equal(new ZohStr("Check Inner value"), Eval("$\"Check ${*inner} value\""));

        // Escaping
        // To output literal "${*name}", we must prevent interpolation.
        // Lexer consumes backslashes. `\$` -> `$`. `\\` -> `\`.
        // To pass `\${` to Interpolator, we need `\\${` in ZOH source.
        // In C# string, that is "\\\\${".
        // Interpolator should treat `\${` as escaped and output `${`.
        // Assert.Equal(new ZohStr("Literal ${*name}"), Eval("$\"Literal \\\\${*name}\""));
        // Note: Exact behavior depends on ZohInterpolator. 
        // If logic is undefined, assuming we just want to verify syntax parses.
        // Let's verify simple Literal pass-through first.
        Assert.Equal(new ZohStr("Literal Value"), Eval("$\"Literal Value\""));
        // Skip complex escaping assertion unless we verify Interpolator behavior.
    }

    [Fact]
    public void Eval_DynamicInterpolation()
    {
        // New Syntax: $*var
        // Semantics: Value of *var is treated as template and interpolated.

        _variables.Set("child", new ZohStr("ChildValue"));
        _variables.Set("parent", new ZohStr("Hello ${*child}"));

        // $*parent should resolve to "Hello ${*child}" then interpolate to "Hello ChildValue"
        Assert.Equal(new ZohStr("Hello ChildValue"), Eval("$*parent"));

        // Verify recursion depth or limits? 
        // For now just one level.

        // Verify it works with non-string values (stringified then interpolated?)
        // If *var is Int(10), result is "10"
        _variables.Set("num", new ZohInt(10));
        Assert.Equal(new ZohStr("10"), Eval("$*num"));

        // Verify Nothing
        _variables.Set("emptyVal", ZohValue.Nothing);
        // ZohNothing.ToString() returns "?"
        var nothingRes = Eval("$*emptyVal");
        Assert.Equal(new ZohStr("?"), nothingRes);

        // Verify *var (raw)
        var rawRes = Eval("*emptyVal");
        Assert.Equal(ZohValue.Nothing, rawRes);

        // Verify ${*var}
        Assert.Equal(new ZohStr("?"), Eval("$\"${*emptyVal}\""));
    }

    [Fact]
    public void Eval_OptionList_WithoutSuffix_Throws()
    {
        var ex = Assert.Throws<Exception>(() => Eval("$(\"Hello ${*name}\")"));
        Assert.Contains("requires '[index]' or '[%]' suffix", ex.Message, StringComparison.Ordinal);
        Assert.Contains("$?(", ex.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void Eval_Interpolation_Advanced()
    {
        // $#{...} Count
        _variables.Set("list", new ZohList([new ZohInt(1), new ZohInt(2)]));
        Assert.Equal(new ZohStr("Count: 2"), Eval("$\"Count: $#{*list}\""));

        // $?{...} Conditional
        _variables.Set("score", new ZohInt(10));
        Assert.Equal(new ZohStr("Result: Win"), Eval("$\"Result: $?{*score >= 10 ? 'Win' : 'Lose'}\""));

        // {...} Unroll
        _variables.Set("items", new ZohList([new ZohStr("A"), new ZohStr("B")]));
        // ${*items ... ", "}
        Assert.Equal(new ZohStr("Items: A, B"), Eval("$\"Items: ${*items ... ', '}\""));
    }

    [Fact]
    public void Eval_Interpolation_Escaping()
    {
        // New Syntax: $"string"
        _variables.Set("name", new ZohStr("World"));

        // Assert.Equal(new ZohStr("Literal ${*name}"), Eval("$\"Literal \\\\${*name}\""));
        // Note: The lexer unescapes string literals. 
        // Source: "Literal \\${*name}" -> Lexer sees "Literal \${*name}" -> Parser sees value "Literal ${*name}" (backslash consumed?)
        // If we want the *interpolator* to see a backslash, the string value must contain it.
        // If parsing `$\" ... \"` creates a `InterpolateExpressionAst` containing a `LiteralExpressionAst` with the string content.

        // Let's test what ZOH string literal parsing does to backslashes.
        // Eval("\"\\${*name}\"") -> ZohStr("${*name}") because `\$` escapes `$`.
        // Wait, `\$` is valid escape in ZOH string? Spec says: "\ can be escaped with \\".
        // Does spec say \$ is escape? " `"` or `'` can be escaped... `\` can be escaped... "
        // It doesn't explicitly mention `\$`.
        // C# Lexer typically allows escaping any char? Or specific ones?
        // If `\$` isn't a valid escape, `\` remains `\`?

        // Assuming strict ZOH Spec:
        // If I write `$\" \${ ` : The `\` escapes `$`.
        // So the string content is ` ${ `. 
        // Scanner sees ` ${ `. It should scan it.

        // To prevent scanning, we need the Scanner to see `\${`.
        // So the string content must be ` \${ `.
        // To get `\` in string content, we need `\\`.
        // So ZOH Source: `$\" \\${ `
        // C# String: `"$\" \\\\${ "`

        Assert.Equal(new ZohStr("Literal ${*name}"), Eval("$\"Literal $\\\\{*name}\""));
    }

    [Fact]
    public void Eval_MalformedInterpolation()
    {
        // Unclosed interpolation in $"..."
        // Should throw exception (Diagnostic)
        var ex = Assert.Throws<Exception>(() => Eval("$\"Start ${unclosed\""));
        Assert.Contains("Malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
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
        Assert.Equal(new ZohStr("Int: 0010"), Eval("$\"Int: ${*score:D4}\""));
        Assert.Equal(new ZohStr("Hex: A"), Eval("$\"Hex: ${*score:X}\""));
        Assert.Equal(new ZohStr("HexLower: 0a"), Eval("$\"HexLower: ${*score:x2}\""));
        Assert.Equal(new ZohStr("Percent: 1,000.0 %"), Eval("$\"Percent: ${*score:P1}\""));
        Assert.Equal(new ZohStr("Cust: 010.0"), Eval("$\"Cust: ${*score:000.0}\""));

        // Width + Format
        Assert.Equal(new ZohStr("Bal:   100.0"), Eval("$\"Bal: ${*balance,7:F1}\""));
        Assert.Equal(new ZohStr("HexW: 0A   "), Eval("$\"HexW: ${*score,-5:X2}\""));

        // Literal inside string shouldn't break parser
        _variables.Set("dict", new ZohStr("Value: 1"));
        Assert.Equal(new ZohStr("Value: 1  "), Eval("$\"${*dict,-10}\""));

        // Nested special forms inside formatted interpolation
        Assert.Equal(new ZohStr("R: Win "), Eval("$\"R: $?{*score >= 10 ? 'Win' : 'Lose',-4}\""));
        Assert.Equal(new ZohStr("C:  3"), Eval("$\"C: $#{*list,2}\""));
        Assert.Contains(Eval("$\"Pick: ${$(10|20|30)[%],2}\"").ToString(), new[] { "Pick: 10", "Pick: 20", "Pick: 30" });

        // Deterministic failure: formatting + scanner suffix [..] is unsupported
        var ex1 = Assert.Throws<Exception>(() => Eval("$\"${*name,7}[0]\""));
        Assert.Contains("cannot be combined", ex1.Message, StringComparison.Ordinal);

        // Deterministic failure: malformed width
        var ex2 = Assert.Throws<FormatException>(() => Eval("$\"${*name,abc}\""));
        Assert.Contains("format", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eval_InterpolationSpecialForms_FormatEdgeCases()
    {
        _variables.Set("score", new ZohInt(10));
        _variables.Set("list", new ZohList([new ZohInt(1), new ZohInt(2), new ZohInt(3)]));
        _variables.Set("flag", ZohValue.True);

        // $?{} with :format only (no width) — ':' after else-branch must not be swallowed by the ternary parser
        Assert.Equal(new ZohStr("Val: 010"), Eval("$\"Val: $?{*score >= 10 ? *score : 0:D3}\""));

        // $?{} with ,width:format combined — D3(10) = "010" (3 chars), width 7 = 4 spaces + "010"
        Assert.Equal(new ZohStr("Val:     010"), Eval("$\"Val: $?{*score >= 10 ? *score : 0,7:D3}\""));

        // $#{} with :format only
        Assert.Equal(new ZohStr("Cnt: 003"), Eval("$\"Cnt: $#{*list:D3}\""));

        // $#{} with ,width:format combined — D3(3) = "003" (3 chars), width 7 = 4 spaces + "003"
        Assert.Equal(new ZohStr("Cnt:     003"), Eval("$\"Cnt: $#{*list,7:D3}\""));

        // $?{} Any form ($?{A | B}) with ,width format
        _variables.Set("nothing_var", ZohValue.Nothing);
        Assert.Equal(new ZohStr("Fb: Win   "), Eval("$\"Fb: $?{*nothing_var | 'Win',-6}\""));

        // String branches containing ',' and ':' must NOT confuse the trailing-token detector —
        // the lexer treats them as part of the string literal, so only the outer-level ','/:' matters.
        Assert.Equal(new ZohStr("R: yes, really"), Eval("$\"R: $?{*flag ? 'yes, really' : 'no'}\""));
        Assert.Equal(new ZohStr("R: yes: confirmed"), Eval("$\"R: $?{*flag ? 'yes: confirmed' : 'no'}\""));

        // Combined: branch with comma string + width format — 'yes, really' is 11 chars, -15 = 4 trailing spaces
        Assert.Equal(new ZohStr("R: yes, really    "), Eval("$\"R: $?{*flag ? 'yes, really' : 'no',-15}\""));
    }

    [Fact]
    public void Eval_Power()
    {
        Assert.Equal(new ZohInt(8), Eval("2 ** 3"));
        Assert.Equal(new ZohFloat(0.5), Eval("2 ** -1"));
        Assert.Equal(new ZohFloat(8.0), Eval("2.0 ** 3"));

        // Precedence: ** higher than *
        Assert.Equal(new ZohInt(18), Eval("2 * 3 ** 2")); // 2 * 9 = 18
        Assert.Equal(new ZohInt(36), Eval("(2 * 3) ** 2")); // 6 ^ 2 = 36

        // Precedence: ** higher than unary - (binds tighter to right? No, standard is -2**2 = -4)
        // In grammar: unary := - unary | power.
        // So - 2**2 parses as Unary(-, Power(2, 2)). Correct. -> -4
        // NOTE: "-2" tokenizes as a single Integer literal, so "-2 ** 2" is "(-2) ** 2" = 4.
        // We use space to ensure Minus token is generated.
        Assert.Equal(new ZohInt(-4), Eval("- 2 ** 2"));
        Assert.Equal(new ZohInt(4), Eval("-2 ** 2")); // Literal binding

        // 2**-2 parses as Power(2, Unary(-, 2)). Correct. -> 0.25 (or Power(2, Literal(-2)))
        Assert.Equal(new ZohFloat(0.25), Eval("2 ** -2"));

        // Associativity: Right
        // 2 ** 3 ** 2 = 2 ** (3 ** 2) = 2 ** 9 = 512
        Assert.Equal(new ZohInt(512), Eval("2 ** 3 ** 2"));
        // (2 ** 3) ** 2 = 8 ** 2 = 64
        Assert.Equal(new ZohInt(64), Eval("(2 ** 3) ** 2"));

        // Large integer
        Assert.Equal(new ZohInt(1024), Eval("2 ** 10"));

        // === Edge Cases & Coverage ===

        // 0 ** 0 -> 1 (Standard C# Math.Pow behavior)
        Assert.Equal(new ZohInt(1), Eval("0 ** 0"));

        // 0 ** -1 -> Infinity (Double)
        var inf = Eval("0 ** -1");
        Assert.True(((ZohFloat)inf).Value == double.PositiveInfinity);

        // Mixed Types
        // Int ** Float -> Float
        Assert.Equal(new ZohFloat(Math.Pow(2, 0.5)), Eval("2 ** 0.5"));
        // Float ** Int -> Float
        Assert.Equal(new ZohFloat(4.0), Eval("2.0 ** 2"));

        // Integer Overflow
        // 2 ** 62 fits in long. 2 ** 63 overflows to loose precision or negative?
        // ZohInt is long (64-bit signed). Max is 2^63 - 1.
        // 2^62 = 4611686018427387904. Fits.
        Assert.Equal(new ZohInt(4611686018427387904), Eval("2 ** 62"));

        // 2^63 = 9223372036854775808 > long.MaxValue. Should promote to Float.
        var overflow = Eval("2 ** 63");
        Assert.IsType<ZohFloat>(overflow);
        Assert.Equal(Math.Pow(2, 63), ((ZohFloat)overflow).Value);

        // Error cases? String ** Int
        Assert.Throws<InvalidOperationException>(() => Eval("\"foo\" ** 2"));
    }
}
