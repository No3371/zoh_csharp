using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Storage;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Verbs.Flow;
using Zoh.Runtime.Verbs.Nav;

namespace Zoh.Tests.Verbs.Flow;

public class ForkDriverFlagTests
{
    [Fact]
    public void Fork_CopiesContextFlags_ToChildContext()
    {
        var store = new VariableStore(new Dictionary<string, Variable>());
        var ctx = new Context(store, new InMemoryStorage(), new ChannelManager(), new SignalManager());

        var pos = new TextPosition(1, 1, 0);
        var label = new StatementAst.Label("start", ImmutableArray<StatementAst.ContractParam>.Empty, pos);
        var story = new CompiledStory(
            "test",
            ImmutableDictionary<string, ZohValue>.Empty,
            ImmutableArray.Create<StatementAst>(label),
            ImmutableDictionary<string, int>.Empty.Add("start", 0),
            ImmutableDictionary<string, ImmutableArray<StatementAst.ContractParam>>.Empty);

        ctx.CurrentStory = story;

        ctx.SetContextFlag("locale", new ZohStr("en"));

        Context? scheduled = null;
        ctx.ContextScheduler = c => scheduled = c;

        var driver = new ForkDriver();
        var call = new VerbCallAst(
            "core.nav",
            "fork",
            false,
            [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [new ValueAst.String("start")],
            pos);

        var result = driver.Execute(ctx, call);
        Assert.True(result.IsSuccess, string.Join(", ", result.DiagnosticsOrEmpty));

        Assert.NotNull(scheduled);
        Assert.Equal(new ZohStr("en"), scheduled!.ResolveFlag("locale"));

        scheduled.SetContextFlag("locale", new ZohStr("fr"));
        Assert.Equal(new ZohStr("en"), ctx.ResolveFlag("locale"));
        Assert.Equal(new ZohStr("fr"), scheduled.ResolveFlag("locale"));
    }
}

