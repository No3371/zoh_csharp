using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs.Var;
using Zoh.Tests.Execution;

namespace Zoh.Tests.Verbs.Core;

public class FlagDriverTests
{
    private static VerbCallAst MakeCall(ImmutableArray<AttributeAst> attrs, params ValueAst[] args)
    {
        return new VerbCallAst(
            "core.var",
            "flag",
            false,
            attrs,
            ImmutableDictionary<string, ValueAst>.Empty,
            [.. args],
            new TextPosition(1, 1, 0));
    }

    private static VerbCallAst MakeCall(params ValueAst[] args) => MakeCall([], args);

    [Fact]
    public void Flag_DefaultScope_SetsContextFlag()
    {
        var ctx = new TestExecutionContext();
        var driver = new FlagDriver();

        var call = MakeCall(new ValueAst.String("locale"), new ValueAst.String("en"));
        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));
        Assert.Equal(new ZohStr("en"), ctx.ResolveFlag("locale"));
        Assert.Null(ctx.Runtime.GetFlag("locale"));
    }

    [Fact]
    public void Flag_RuntimeScope_SetsRuntimeFlag()
    {
        var ctx = new TestExecutionContext();
        var driver = new FlagDriver();

        var call = MakeCall(
            ImmutableArray.Create(new AttributeAst("scope", new ValueAst.String("runtime"), new TextPosition(1, 1, 0))),
            new ValueAst.String("locale"),
            new ValueAst.String("en"));
        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));
        Assert.Equal(new ZohStr("en"), ctx.Runtime.GetFlag("locale"));
        Assert.Equal(new ZohStr("en"), ctx.ResolveFlag("locale"));
    }

    [Fact]
    public void Flag_ContextShadowsRuntimeFlag()
    {
        var ctx = new TestExecutionContext();
        ctx.Runtime.SetFlag("locale", new ZohStr("en"));
        ctx.SetContextFlag("locale", new ZohStr("fr"));

        Assert.Equal(new ZohStr("fr"), ctx.ResolveFlag("locale"));
    }
}

