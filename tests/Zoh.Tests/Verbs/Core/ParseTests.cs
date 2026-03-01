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
        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.FirstOrDefault()?.Message);
        Assert.Equal(new ZohInt(long.Parse(expected)), result.ValueOrNothing);
    }

    [Theory]
    [InlineData("3.14", "3.14")]
    [InlineData("  3.14  ", "3.14")]
    public void Parse_Double_WithWhitespace(string input, string expected)
    {
        var call = MakeParseCall(input, "double");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohFloat(double.Parse(expected, System.Globalization.CultureInfo.InvariantCulture)), result.ValueOrNothing);
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
        Assert.Equal(new ZohBool(expected), result.ValueOrNothing);
    }

    [Theory]
    [InlineData("  123  ", "integer")]
    [InlineData("  3.14  ", "double")]
    [InlineData("  true  ", "boolean")]
    [InlineData("  [1, 2]  ", "list")]
    [InlineData("  {\"a\":1}  ", "map")]
    [InlineData("  hello  ", "string")]
    public void Parse_Inference_WithWhitespace(string input, string expectedType)
    {
        var call = MakeParseCall(input);

        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.FirstOrDefault()?.Message);
        string actualType = result.ValueOrNothing switch
        {
            ZohInt _ => "integer",
            ZohFloat _ => "double",
            ZohBool _ => "boolean",
            ZohStr _ => "string",
            ZohList _ => "list",
            ZohMap _ => "map",
            _ => "unknown"
        };
        Assert.Equal(expectedType, actualType);
    }

    [Fact]
    public void Parse_List_FromJson()
    {
        var call = MakeParseCall("[1, \"hello\", true]", "list");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.FirstOrDefault()?.Message);

        var list = Assert.IsType<ZohList>(result.ValueOrNothing);
        Assert.Equal(3, list.Items.Length);
        Assert.Equal(new ZohInt(1), list.Items[0]);
        Assert.Equal(new ZohStr("hello"), list.Items[1]);
        Assert.Equal(new ZohBool(true), list.Items[2]);
    }

    [Fact]
    public void Parse_Map_FromJson()
    {
        var call = MakeParseCall("{\"score\": 42, \"name\": \"hero\"}", "map");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.FirstOrDefault()?.Message);

        var map = Assert.IsType<ZohMap>(result.ValueOrNothing);
        Assert.Equal(new ZohInt(42), map.Items["score"]);
        Assert.Equal(new ZohStr("hero"), map.Items["name"]);
    }

    [Fact]
    public void Parse_List_Inferred()
    {
        var call = MakeParseCall("[10, 20]");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.FirstOrDefault()?.Message);
        Assert.IsType<ZohList>(result.ValueOrNothing);
    }

    [Fact]
    public void Parse_Map_Inferred()
    {
        var call = MakeParseCall("{\"k\": 1}");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.FirstOrDefault()?.Message);
        Assert.IsType<ZohMap>(result.ValueOrNothing);
    }

    [Fact]
    public void Parse_MalformedList_ReturnsFatal()
    {
        var call = MakeParseCall("[1, 2", "list");
        var result = _driver.Execute(_context, call);
        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_format", result.DiagnosticsOrEmpty[0].Code);
    }

    [Fact]
    public void Parse_NestedStructure()
    {
        var call = MakeParseCall("{\"items\": [1, 2, 3]}", "map");
        var result = _driver.Execute(_context, call);
        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.FirstOrDefault()?.Message);

        var map = Assert.IsType<ZohMap>(result.ValueOrNothing);
        var inner = Assert.IsType<ZohList>(map.Items["items"]);
        Assert.Equal(3, inner.Items.Length);
    }
}
