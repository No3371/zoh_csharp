using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Storage;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Verbs.Store;
using Zoh.Tests.Execution;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;

namespace Zoh.Tests.Verbs.Store;

public class StoreVerbTests
{
    private readonly TestExecutionContext _ctx;
    private readonly WriteDriver _write;
    private readonly ReadDriver _read;
    private readonly EraseDriver _erase;
    private readonly PurgeDriver _purge;

    public StoreVerbTests()
    {
        _ctx = new TestExecutionContext();
        _write = new WriteDriver();
        _read = new ReadDriver();
        _erase = new EraseDriver();
        _purge = new PurgeDriver();
    }

    private ValueAst.Reference Ref(string name) => new(name);

    [Fact]
    public void Write_MultipleVariables_PersistsAll()
    {
        // Arrange
        _ctx.Variables.Set("a", new ZohInt(1));
        _ctx.Variables.Set("b", new ZohStr("text"));
        _ctx.Variables.Set("c", new ZohBool(true));

        var verb = new VerbCallAst(
            null,
            "write",
            false,
            ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(Ref("a"), Ref("b"), Ref("c")),
            new TextPosition(0, 0, 0)
        );

        // Act
        var result = _write.Execute(_ctx, verb);

        // Assert
        Assert.True(result.IsSuccess, "Result should be success");
        Assert.Equal(new ZohInt(1), _ctx.Storage.Read(null, "a"));
        Assert.Equal(new ZohStr("text"), _ctx.Storage.Read(null, "b"));
        Assert.Equal(new ZohBool(true), _ctx.Storage.Read(null, "c"));
    }

    [Fact]
    public void Write_NoRefs_ReturnsFatal()
    {
        var verb = new VerbCallAst(
            null,
            "write",
            false,
            ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray<ValueAst>.Empty,
            new TextPosition(0, 0, 0)
        );

        var result = _write.Execute(_ctx, verb);

        Assert.True(result.IsFatal);
        Assert.Contains(result.Diagnostics, d => d.Code == "parameter_not_found");
    }

    [Fact]
    public void Read_MultipleVariables_LoadsAll()
    {
        // Arrange
        _ctx.Storage.Write(null, "a", new ZohInt(10));
        _ctx.Storage.Write(null, "b", new ZohStr("loaded"));

        var verb = new VerbCallAst(
             null,
             "read",
             false,
             ImmutableArray<AttributeAst>.Empty,
             ImmutableDictionary<string, ValueAst>.Empty,
             ImmutableArray.Create<ValueAst>(Ref("a"), Ref("b")),
             new TextPosition(0, 0, 0)
         );

        // Act
        var result = _read.Execute(_ctx, verb);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(10), _ctx.Variables.Get("a"));
        Assert.Equal(new ZohStr("loaded"), _ctx.Variables.Get("b"));
    }

    [Fact]
    public void Read_MissingVariable_UsesDefault()
    {
        // Arrange
        // "a" missing
        var verb = new VerbCallAst(
             null,
             "read",
             false,
             ImmutableArray<AttributeAst>.Empty,
             ImmutableDictionary<string, ValueAst>.Empty.Add("default", new ValueAst.Integer(99)),
             ImmutableArray.Create<ValueAst>(Ref("a")),
             new TextPosition(0, 0, 0)
         );

        // Act
        var result = _read.Execute(_ctx, verb);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(99), _ctx.Variables.Get("a"));
    }

    [Fact]
    public void Read_Required_MissingVariable_ReturnsError()
    {
        // Arrange
        var verb = new VerbCallAst(
             null,
             "read",
             false,
             ImmutableArray.Create(new AttributeAst("required", null, new TextPosition(0, 0, 0))),
             ImmutableDictionary<string, ValueAst>.Empty,
             ImmutableArray.Create<ValueAst>(Ref("a")),
             new TextPosition(0, 0, 0)
         );

        // Act
        var result = _read.Execute(_ctx, verb);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(result.IsFatal);
        Assert.Contains(result.Diagnostics, d => d.Code == "not_found");
    }
    [Fact]
    public void Write_Then_Drop_Then_Read_RoundTrip()
    {
        // Arrange
        _ctx.Variables.Set("var1", new ZohInt(100));
        _ctx.Variables.Set("var2", new ZohStr("persistent"));

        // 1. Write
        var writeVerb = new VerbCallAst(
            null,
            "write",
            false,
            ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(Ref("var1"), Ref("var2")),
            new TextPosition(0, 0, 0)
        );
        var writeResult = _write.Execute(_ctx, writeVerb);
        Assert.True(writeResult.IsSuccess, "Write failed");

        // 2. Drop variables from context to ensure we are actually reading from storage
        _ctx.Variables.Drop("var1");
        _ctx.Variables.Drop("var2");

        Assert.Equal(ZohValue.Nothing, _ctx.Variables.Get("var1"));
        Assert.Equal(ZohValue.Nothing, _ctx.Variables.Get("var2"));

        // 3. Read
        var readVerb = new VerbCallAst(
             null,
             "read",
             false,
             ImmutableArray<AttributeAst>.Empty,
             ImmutableDictionary<string, ValueAst>.Empty,
             ImmutableArray.Create<ValueAst>(Ref("var1"), Ref("var2")),
             new TextPosition(0, 0, 0)
         );
        var readResult = _read.Execute(_ctx, readVerb);

        // Assert
        Assert.True(readResult.IsSuccess, "Read failed");
        Assert.Equal(new ZohInt(100), _ctx.Variables.Get("var1"));
        Assert.Equal(new ZohStr("persistent"), _ctx.Variables.Get("var2"));
    }

    [Fact]
    public void Erase_ExistingVariable_RemovesFromStorage()
    {
        _ctx.Storage.Write(null, "temp", new ZohInt(123));
        Assert.True(_ctx.Storage.Exists(null, "temp"));

        var verb = new VerbCallAst(null, "erase", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(Ref("temp")), new TextPosition(0, 0, 0));
        var result = _erase.Execute(_ctx, verb);

        Assert.True(result.IsSuccess);
        Assert.False(_ctx.Storage.Exists(null, "temp"));
    }

    [Fact]
    public void Erase_NonExistentVariable_ReturnsInfoDiagnostic()
    {
        var verb = new VerbCallAst(null, "erase", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(Ref("missing")), new TextPosition(0, 0, 0));
        var result = _erase.Execute(_ctx, verb);

        Assert.True(result.IsSuccess); // Should be success per spec
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Code == "not_found");
    }

    [Fact]
    public void Purge_ClearsEntireStore()
    {
        _ctx.Storage.Write(null, "a", new ZohInt(1));
        _ctx.Storage.Write(null, "b", new ZohInt(2));

        var verb = new VerbCallAst(null, "purge", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray<ValueAst>.Empty, new TextPosition(0, 0, 0));
        var result = _purge.Execute(_ctx, verb);

        Assert.True(result.IsSuccess);
        Assert.False(_ctx.Storage.Exists(null, "a"));
        Assert.False(_ctx.Storage.Exists(null, "b"));
    }

    [Fact]
    public void Write_Read_NamedStore()
    {
        var storeAttr = new Dictionary<string, ValueAst> { { "store", new ValueAst.String("myStore") } }.ToImmutableDictionary();

        // Write
        _ctx.Variables.Set("x", new ZohInt(99));
        var writeVerb = new VerbCallAst(null, "write", false, ImmutableArray<AttributeAst>.Empty, storeAttr, ImmutableArray.Create<ValueAst>(Ref("x")), new TextPosition(0, 0, 0));
        Assert.True(_write.Execute(_ctx, writeVerb).IsSuccess);

        // Verify isolation
        Assert.False(_ctx.Storage.Exists(null, "x"));
        Assert.True(_ctx.Storage.Exists("myStore", "x"));

        // Read
        _ctx.Variables.Drop("x");
        var readVerb = new VerbCallAst(null, "read", false, ImmutableArray<AttributeAst>.Empty, storeAttr, ImmutableArray.Create<ValueAst>(Ref("x")), new TextPosition(0, 0, 0));
        Assert.True(_read.Execute(_ctx, readVerb).IsSuccess);
        Assert.Equal(new ZohInt(99), _ctx.Variables.Get("x"));
    }

    [Fact]
    public void Purge_NamedStore_OnlyAffectsTargetStore()
    {
        _ctx.Storage.Write(null, "defaultVar", new ZohInt(1));
        _ctx.Storage.Write("store1", "storeVar", new ZohInt(2));

        var storeAttr = new Dictionary<string, ValueAst> { { "store", new ValueAst.String("store1") } }.ToImmutableDictionary();
        var verb = new VerbCallAst(null, "purge", false, ImmutableArray<AttributeAst>.Empty, storeAttr, ImmutableArray<ValueAst>.Empty, new TextPosition(0, 0, 0));

        _purge.Execute(_ctx, verb);

        Assert.False(_ctx.Storage.Exists("store1", "storeVar"));
        Assert.True(_ctx.Storage.Exists(null, "defaultVar")); // Default store untouched
    }

    [Fact]
    public void Write_ExpressionType_ReturnsFatal()
    {
        _ctx.Variables.Set("expr", new ZohExpr(new ValueAst.Expression("1+1", new TextPosition(0, 0, 0))));
        var verb = new VerbCallAst(null, "write", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(Ref("expr")), new TextPosition(0, 0, 0));

        var result = _write.Execute(_ctx, verb);

        Assert.True(result.IsFatal);
        Assert.Equal("invalid_type", result.Diagnostics[0].Code);
    }

    [Fact]
    public void Write_AllSerializableTypes_Succeeds()
    {
        _ctx.Variables.Set("i", new ZohInt(1));
        _ctx.Variables.Set("d", new ZohFloat(1.5));
        _ctx.Variables.Set("s", new ZohStr("s"));
        _ctx.Variables.Set("b", new ZohBool(true));
        _ctx.Variables.Set("l", new ZohList(ImmutableArray.Create<ZohValue>(new ZohInt(1))));

        var mapDict = new Dictionary<string, ZohValue> { { "k", new ZohInt(1) } }.ToImmutableDictionary();
        _ctx.Variables.Set("m", new ZohMap(mapDict));

        _ctx.Variables.Set("n", ZohNothing.Instance); // ZohValue.Nothing is a property/field? ZohNothing is the type.

        var verb = new VerbCallAst(null, "write", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(Ref("i"), Ref("d"), Ref("s"), Ref("b"), Ref("l"), Ref("m"), Ref("n")), new TextPosition(0, 0, 0));

        Assert.True(_write.Execute(_ctx, verb).IsSuccess);
    }

    [Fact]
    public void Write_VerbType_ReturnsFatal()
    {
        _ctx.Variables.Set("v", new ZohVerb(new VerbCallAst(null, "noop", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray<ValueAst>.Empty, new TextPosition(0, 0, 0))));
        var verb = new VerbCallAst(null, "write", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(Ref("v")), new TextPosition(0, 0, 0));
        var result = _write.Execute(_ctx, verb);
        Assert.True(result.IsFatal);
        Assert.Equal("invalid_type", result.Diagnostics[0].Code);
    }

    [Fact]
    public void Write_ChannelType_ReturnsFatal()
    {
        _ctx.Variables.Set("c", new ZohChannel("chan"));
        var verb = new VerbCallAst(null, "write", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(Ref("c")), new TextPosition(0, 0, 0));
        var result = _write.Execute(_ctx, verb);
        Assert.True(result.IsFatal);
        Assert.Equal("invalid_type", result.Diagnostics[0].Code);
    }

    [Fact]
    public void Erase_NoRefs_ReturnsFatal()
    {
        var verb = new VerbCallAst(null, "erase", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray<ValueAst>.Empty, new TextPosition(0, 0, 0));
        var result = _erase.Execute(_ctx, verb);
        Assert.True(result.IsFatal);
        Assert.Equal("parameter_not_found", result.Diagnostics[0].Code);
    }

    [Fact]
    public void Read_RequiredWithScopedStore_ReturnsError()
    {
        // Spec says: if store is named, required check should still work.
        var storeAttr = new Dictionary<string, ValueAst> { { "store", new ValueAst.String("missingStore") } }.ToImmutableDictionary();
        var reqAttr = ImmutableArray.Create(new AttributeAst("required", null, new TextPosition(0, 0, 0)));

        var verb = new VerbCallAst(null, "read", false, reqAttr, storeAttr, ImmutableArray.Create<ValueAst>(Ref("missing")), new TextPosition(0, 0, 0));
        var result = _read.Execute(_ctx, verb);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "not_found");
    }
}
