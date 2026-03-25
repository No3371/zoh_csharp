using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs.Collection;
using Zoh.Runtime.Verbs.Var;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Runtime;

public class MapStringKeyTests
{
    private readonly TestExecutionContext _context;

    public MapStringKeyTests()
    {
        _context = new TestExecutionContext();
    }

    private ValueAst.Reference Ref(string name, params ValueAst[] indices)
    {
        return new ValueAst.Reference(name, [.. indices]);
    }

    [Fact]
    public void Map_IntegerIndex_Fails()
    {
        // Setup map: *map = {"key": "value"}
        var map = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("key", new ZohStr("value")));
        _context.Variables.Set("map", map);

        // Access: *map[0] -> Should Fail with invalid_type or Nothing (depending on impl, but strict says Error/Nothing?)
        // Spec implies strict type check should error? Or just return Nothing?
        // Project doc says: "invalid_type" Fatal error.

        // Using CollectionHelpers.GetAtPath via value resolution or just calling it directly?
        // Let's test via CollectionHelpers since that's what we modify.

        var path = ImmutableArray.Create<ValueAst>(new ValueAst.Integer(0));

        // This is tricky because GetAtPath returns ZohValue, it doesn't return VerbResult (so it can't be Fatal).
        // If GetIndex fails type check, it currently returns Nothing.
        // But SetIndex (SetAtPath) returns VerbResult.
        // Let's check Set first as it is stricter.

        var result = Zoh.Runtime.Helpers.CollectionHelpers.SetAtPath(
            _context,
            "map",
            path,
            new ZohStr("newVal")
        );

        Assert.True(result.IsFatal);
        Assert.Equal("invalid_index_type", result.DiagnosticsOrEmpty[0].Code);
    }

    [Fact]
    public void Map_StringIndex_Works()
    {
        // Setup map: *map = {"0": "value"}
        var map = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("0", new ZohStr("value")));
        _context.Variables.Set("map", map);

        // Set *map["0"] = "updated"
        var path = ImmutableArray.Create<ValueAst>(new ValueAst.String("0"));
        var result = Zoh.Runtime.Helpers.CollectionHelpers.SetAtPath(
            _context,
            "map",
            path,
            new ZohStr("updated")
        );

        Assert.False(result.IsFatal);
        var updated = _context.Variables.Get("map") as ZohMap;
        Assert.NotNull(updated);
        Assert.Equal(new ZohStr("updated"), updated.Items["0"]);
    }

    [Fact]
    public void Has_MapWithIntegerSubject_Fails()
    {
        var map = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("key", new ZohInt(1)));
        _context.Variables.Set("map", map);

        // /has *map 42
        var call = new VerbCallAst(
            "core.collection", "has", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [new ValueAst.Reference("map"), new ValueAst.Integer(42)],
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0)
        );

        var result = new HasDriver().Execute(_context, call);
        Assert.True(result.IsFatal);
        Assert.Equal("invalid_index_type", result.DiagnosticsOrEmpty[0].Code);
    }

    [Fact]
    public void Remove_MapWithIntegerKey_Fails()
    {
        var map = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty.Add("key", new ZohInt(1)));
        _context.Variables.Set("map", map);

        // /remove *map 42
        var call = new VerbCallAst(
            "core.collection", "remove", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [new ValueAst.Reference("map"), new ValueAst.Integer(42)],
            new Zoh.Runtime.Lexing.TextPosition(1, 1, 0)
        );

        var result = new RemoveDriver().Execute(_context, call);
        Assert.True(result.IsFatal);
        Assert.Equal("invalid_index_type", result.DiagnosticsOrEmpty[0].Code);
    }
}
