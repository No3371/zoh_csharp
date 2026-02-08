using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs.Flow;
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
    public void Sleep_SetsSleepingState_AndWaitCondition()
    {
        var ctx = CreateContext();
        var driver = new SleepDriver();

        // /sleep 1
        var call = new VerbCallAst(null, "sleep", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.Integer(1)), new TextPosition(1, 1, 0));

        var before = DateTimeOffset.UtcNow;
        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ContextState.Sleeping, ctx.State);
        Assert.IsType<DateTimeOffset>(ctx.WaitCondition);

        var wakeTime = (DateTimeOffset)ctx.WaitCondition;
        Assert.True(wakeTime >= before.AddSeconds(1));
    }
}
