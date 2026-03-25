using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Tests.Execution;
using Zoh.Runtime.Verbs.Collection;
using System.Collections.Immutable;
using Xunit;

namespace Zoh.Tests.Verbs.Core;

public class CollectionVerbsTests
{
    private readonly TestExecutionContext _context = new();
    private readonly InsertDriver _insert = new();
    private readonly RemoveDriver _remove = new();
    private readonly ClearDriver _clear = new();

    private VerbCallAst MakeCall(string name, params ValueAst[] args)
    {
        return new VerbCallAst(
           "core.collection", name, false, [],
           ImmutableDictionary<string, ValueAst>.Empty,
           [.. args],
           new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Fact]
    public void Insert_List_UpdatesVariable()
    {
        _context.Variables.Set("l", new ZohList([new ZohInt(1)]));

        // /insert *l 0 99
        var call = MakeCall("insert", new ValueAst.Reference("l"), new ValueAst.Integer(0), new ValueAst.Integer(99));
        var result = _insert.Execute(_context, call);

        Assert.True(result.IsSuccess);
        var l = (ZohList)_context.Variables.Get("l");
        Assert.Equal(2, l.Items.Length);
        Assert.Equal(new ZohInt(99), l.Items[0]);
        Assert.Equal(new ZohInt(1), l.Items[1]);
    }

    [Fact]
    public void Remove_List_UpdatesVariable()
    {
        _context.Variables.Set("l", new ZohList([new ZohInt(10), new ZohInt(20)]));

        // /remove *l 0
        var call = MakeCall("remove", new ValueAst.Reference("l"), new ValueAst.Integer(0));
        var result = _remove.Execute(_context, call);

        Assert.True(result.IsSuccess);
        var l = (ZohList)_context.Variables.Get("l");
        Assert.Single(l.Items);
        Assert.Equal(new ZohInt(20), l.Items[0]);
    }

    [Fact]
    public void Clear_Map_Empties()
    {
        var map = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("k", new ZohInt(1)));
        _context.Variables.Set("m", map);

        // /clear *m
        var call = MakeCall("clear", new ValueAst.Reference("m"));
        var result = _clear.Execute(_context, call);

        Assert.True(result.IsSuccess);
        var m = (ZohMap)_context.Variables.Get("m");
        Assert.Empty(m.Items);
    }
}
