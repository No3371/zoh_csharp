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

    public StoreVerbTests()
    {
        _ctx = new TestExecutionContext();
        _write = new WriteDriver();
        _read = new ReadDriver();
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
}
