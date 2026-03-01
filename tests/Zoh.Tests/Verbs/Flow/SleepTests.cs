using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs.Flow;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Storage;
using System.Collections.Generic;
using System;

namespace Zoh.Tests.Verbs.Flow;

public class SleepTests
{
    private Context CreateContext()
    {
        var store = new VariableStore(new Dictionary<string, Variable>());
        var storage = new InMemoryStorage();
        var channels = new ChannelManager();
        var ctx = new Context(store, storage, channels, new SignalManager());
        return ctx;
    }

    [Fact]
    public void Sleep_ReturnsSleepContinuation()
    {
        var ctx = CreateContext();
        var driver = new SleepDriver();

        // /sleep 1
        var call = new VerbCallAst(null, "sleep", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.Integer(1)), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        var suspend = Assert.IsType<DriverResult.Suspend>(result);
        var req = Assert.IsType<SleepRequest>(suspend.Continuation.Request);
        Assert.True(req.DurationMs >= 1000);
        Assert.Equal(ContextState.Running, ctx.State); // driver no longer mutates state
    }
}
