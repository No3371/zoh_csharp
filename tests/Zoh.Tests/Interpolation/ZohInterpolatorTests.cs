using Xunit;
using Zoh.Runtime.Interpolation;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;

namespace Zoh.Tests.Interpolation;

public class ZohInterpolatorTests
{
    private readonly VariableStore _variables;
    private readonly ZohInterpolator _interpolator;

    public ZohInterpolatorTests()
    {
        _variables = new VariableStore(new Dictionary<string, Variable>());
        _interpolator = new ZohInterpolator(_variables);
    }

    private void AssertInterpolation(string input, string expected)
    {
        var result = _interpolator.Interpolate(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Interpolate_Literals_NoChange()
    {
        AssertInterpolation("Hello World", "Hello World");
        AssertInterpolation("", "");
        AssertInterpolation("123", "123");
    }

    [Fact]
    public void Interpolate_Variables()
    {
        _variables.Set("name", new ZohStr("Alice"));
        _variables.Set("age", new ZohInt(30));

        AssertInterpolation("Hello ${*name}", "Hello Alice");
        AssertInterpolation("Age: ${*age}", "Age: 30");
        AssertInterpolation("${*name} is ${*age}", "Alice is 30");
    }

    [Fact]
    public void Interpolate_Expressions()
    {
        _variables.Set("a", new ZohInt(5));
        _variables.Set("b", new ZohInt(10));

        AssertInterpolation("Sum: ${*a + *b}", "Sum: 15");
        AssertInterpolation("Check: ${*a < *b}", "Check: true");
    }

    [Fact]
    public void Interpolate_Escaping()
    {
        // \{ -> {
        // \} -> }
        AssertInterpolation(@"\{encoded\}", "{encoded}");

        // Literal ${...} requires preventing pattern match.
        // If we write \{val\}, interpolator output is {val}.
        // If we want literal `${val}`, we need `$\{val\}`? No, Zoh uses `${` token.
        // To escape `${`, current scanner might require `\${` ? 
        // Logic in ZohInterpolator Line 33: checks for `\` followed by `{` or `}`.
        // So `\{` becomes `{`.
        // To print `${*var}`, we can do `$\{var}`? -> `$`.Append(`{var}`).
        // Let's verify:
        // Input: "$\{*var}" -> `$` is literal. `\{` becomes `{`. `*var` is literal. `}` literal.
        // Result: "${*var}"

        AssertInterpolation(@"$\{not_var}", "${not_var}");
        AssertInterpolation(@"Val: \{123\}", "Val: {123}");
    }

    [Fact]
    public void Interpolate_Unroll_List()
    {
        _variables.Set("list", new ZohList([new ZohInt(1), new ZohInt(2), new ZohInt(3)]));

        // ${*list ... ", "}
        AssertInterpolation("Items: ${*list ... \", \"}", "Items: 1, 2, 3");
    }

    [Fact]
    public void Interpolate_Unroll_Single()
    {
        _variables.Set("val", new ZohStr("A"));

        // ${*val ... ","} -> Just prints val
        AssertInterpolation("${*val ... \", \"}", "A");
    }

    [Fact]
    public void Interpolate_Sugar_Count()
    {
        _variables.Set("list", new ZohList([new ZohInt(1), new ZohInt(2)]));
        // $#{*list}
        AssertInterpolation("Count: $#{*list}", "Count: 2");
    }

    [Fact]
    public void Interpolate_Sugar_Conditional()
    {
        _variables.Set("score", new ZohInt(10));
        // $?{*score >= 10 ? 'Win' : 'Lose'}
        AssertInterpolation("Result: $?{*score >= 10 ? \"Win\" | \"Lose\"}", "Result: Win");
    }

    [Fact]
    public void Interpolate_Mixed()
    {
        _variables.Set("x", new ZohInt(1));
        _variables.Set("y", new ZohInt(2));

        AssertInterpolation("Start ${*x} Middle ${*y} End", "Start 1 Middle 2 End");
    }

    [Fact]
    public void Interpolate_Errors_Wrapped()
    {
        // Invalid expression logic inside valid syntax
        var ex = Assert.Throws<Exception>(() => _interpolator.Interpolate("Bad: ${1 + }"));
        Assert.Contains("Interpolation error", ex.Message);
    }

    [Fact]
    public void Interpolate_Malformed_UnclosedInterpolation()
    {
        // Should throw InvalidSyntax (or generic Exception for now) instead of returning literal
        // Current impl returns "Start ${unclosed..." literal, which is WRONG per spec.
        var ex = Assert.Throws<Exception>(() => _interpolator.Interpolate("Start ${unclosed..."));
        Assert.Contains("Malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interpolate_Malformed_UnclosedUnroll()
    {
        // ${*list ...
        var ex = Assert.Throws<Exception>(() => _interpolator.Interpolate("Start ${*list ..."));
        Assert.Contains("Malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Interpolate_Legacy_Braces_Are_Literal()
    {
        // {*var} should be treated as literal since we don't support bare braces anymore
        _variables.Set("a", new ZohInt(1));
        AssertInterpolation("{*a}", "{*a}");
        AssertInterpolation("Pre {*a ... \",\"} Post", "Pre {*a ... \",\"} Post");
    }

    [Fact]
    public void Interpolate_Malformed_InvalidPrefix()
    {
        // $?{ without match?
        // If Scanner returns null, it's literal.
        // Spec says Malformed Syntax -> Fatal.
        var ex = Assert.Throws<Exception>(() => _interpolator.Interpolate("Start $?{ unclosed"));
        Assert.Contains("Malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
