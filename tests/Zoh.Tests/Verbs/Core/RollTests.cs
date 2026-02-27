using System.Collections.Immutable;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs.Core;
using Zoh.Tests.Execution;
using Xunit;

namespace Zoh.Tests.Verbs.Core;

public class RollTests
{
    private readonly TestExecutionContext _context = new();
    private readonly RollDriver _driver = new();

    private static VerbCallAst MakeWRollCall(params ValueAst[] args)
    {
        return new VerbCallAst(
            "core",
            "wroll",
            false,
            [],
            ImmutableDictionary<string, ValueAst>.Empty,
            args.ToImmutableArray(),
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Fact]
    public void WRoll_NegativeWeight_ReturnsFatal()
    {
        var call = MakeWRollCall(
            new ValueAst.String("a"),
            new ValueAst.Integer(1),
            new ValueAst.String("b"),
            new ValueAst.Integer(-1));

        var result = _driver.Execute(_context, call);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_value", result.Diagnostics[0].Code);
    }

    [Fact]
    public void WRoll_NonNumericWeight_ReturnsFatal()
    {
        var call = MakeWRollCall(
            new ValueAst.String("a"),
            new ValueAst.String("bad_weight"));

        var result = _driver.Execute(_context, call);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_type", result.Diagnostics[0].Code);
    }

    [Fact]
    public void WRoll_ValidWeights_ReturnsValue()
    {
        var call = MakeWRollCall(
            new ValueAst.String("x"),
            new ValueAst.Integer(1),
            new ValueAst.String("y"),
            new ValueAst.Integer(2));

        var result = _driver.Execute(_context, call);

        Assert.True(result.IsSuccess, result.Diagnostics.FirstOrDefault()?.Message);
        var value = Assert.IsType<ZohStr>(result.Value);
        Assert.Contains(value.Value, new[] { "x", "y" });
    }
}
