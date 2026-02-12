using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Tests.Execution;
using Zoh.Runtime.Verbs.Core;
using System.Collections.Immutable;
using Xunit;

namespace Zoh.Tests.Verbs.Core;

public class ParseTests
{
    private readonly TestExecutionContext _context = new();
    private readonly ParseDriver _driver = new();

    private VerbCallAst MakeParseCall(string value, string? targetType = null)
    {
        var unnamedParams = new List<ValueAst> { new ValueAst.String(value) };
        if (targetType != null)
        {
            unnamedParams.Add(new ValueAst.String(targetType));
        }

        return new VerbCallAst(
           "core", "parse", false, [],
           ImmutableDictionary<string, ValueAst>.Empty,
           unnamedParams.ToImmutableArray(),
           new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Theory]
    [InlineData("42", "42")]
    [InlineData("  42  ", "42")]
    [InlineData("\n42\r\n", "42")]
    public void Parse_Integer_WithWhitespace(string input, string expected)
    {
        var call = MakeParseCall(input, "integer");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess, result.Diagnostics.FirstOrDefault()?.Message);
        Assert.Equal(new ZohInt(long.Parse(expected)), result.Value);
    }

    [Theory]
    [InlineData("3.14", "3.14")]
    [InlineData("  3.14  ", "3.14")]
    public void Parse_Double_WithWhitespace(string input, string expected)
    {
        var call = MakeParseCall(input, "double");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohFloat(double.Parse(expected, System.Globalization.CultureInfo.InvariantCulture)), result.Value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("  false  ", false)]
    [InlineData("True", true)]
    public void Parse_Boolean_WithWhitespace(string input, bool expected)
    {
        var call = MakeParseCall(input, "boolean");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohBool(expected), result.Value);
    }

    [Theory]
    [InlineData("  123  ", "integer")]
    [InlineData("  3.14  ", "double")]
    [InlineData("  true  ", "boolean")]
    [InlineData("  [1, 2]  ", "list")]
    [InlineData("  {a:1}  ", "map")]
    [InlineData("  hello  ", "string")]
    public void Parse_Inference_WithWhitespace(string input, string expectedType)
    {
        var call = MakeParseCall(input);

        var result = _driver.Execute(_context, call);

        if (expectedType == "list" || expectedType == "map")
        {
            Assert.False(result.IsSuccess);
            Assert.Equal("not_implemented", result.Diagnostics[0].Code);
        }
        else
        {
            Assert.True(result.IsSuccess);
            string actualType = result.Value switch
            {
                ZohInt _ => "integer",
                ZohFloat _ => "double",
                ZohBool _ => "boolean",
                ZohStr _ => "string",
                _ => "unknown"
            };
            Assert.Equal(expectedType, actualType);
        }
    }
}
