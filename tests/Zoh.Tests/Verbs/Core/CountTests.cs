using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Tests.Execution;
using Zoh.Runtime.Verbs.Core;
using System.Collections.Immutable;
using Xunit;

namespace Zoh.Tests.Verbs.Core;

public class CountTests
{
    private readonly TestExecutionContext _context = new();
    private readonly CountDriver _driver = new();

    private VerbCallAst MakeCall(params ValueAst[] args)
    {
        return new VerbCallAst(
           "core", "count", false, [],
           ImmutableDictionary<string, ValueAst>.Empty,
           [.. args],
           new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Fact]
    public void Count_List()
    {
        _context.Variables.Set("l", new ZohList([new ZohInt(1), new ZohInt(2)]));
        var call = MakeCall(new ValueAst.Reference("l"));
        var result = _driver.Execute(_context, call);
        Assert.Equal(new ZohInt(2), result.ValueOrNothing);
    }

    [Fact]
    public void Count_String()
    {
        _context.Variables.Set("s", new ZohStr("abc"));
        var call = MakeCall(new ValueAst.Reference("s"));
        var result = _driver.Execute(_context, call);
        Assert.Equal(new ZohInt(3), result.ValueOrNothing);
    }

    [Fact]
    public void Count_Nothing()
    {
        _context.Variables.Set("n", ZohValue.Nothing);
        var call = MakeCall(new ValueAst.Reference("n"));
        var result = _driver.Execute(_context, call);
        Assert.Equal(new ZohInt(0), result.ValueOrNothing);
    }

    [Fact]
    public void Count_Indexedlist()
    {
        // /set *l = ["a", "bb"]
        // /count *l 1 -> count "bb" -> 2
        var inner = new ZohList([new ZohStr("a"), new ZohStr("bb")]);
        _context.Variables.Set("l", inner);

        var call = MakeCall(new ValueAst.Reference("l"), new ValueAst.Integer(1));
        var result = _driver.Execute(_context, call);
        Assert.Equal(new ZohInt(2), result.ValueOrNothing);
    }
}
