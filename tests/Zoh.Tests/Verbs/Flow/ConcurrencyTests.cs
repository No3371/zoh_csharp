using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs.Flow;
using Zoh.Runtime.Verbs; // Added
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Storage;
using System.Collections.Generic;

namespace Zoh.Tests.Verbs.Flow;

public class ConcurrencyTests
{
    private Context CreateContext(CompiledStory story)
    {
        var store = new VariableStore(new Dictionary<string, Variable>());
        var storage = new InMemoryStorage();
        var channels = new ChannelManager();
        var ctx = new Context(store, storage, channels, new SignalManager())
        {
            CurrentStory = story,
            InstructionPointer = 0,
            VerbExecutor = (v, c) => VerbResult.Ok() // Mock executor
        };
        return ctx;
    }

    private CompiledStory CreateStory(string name, params string[] labels)
    {
        var labelMap = new Dictionary<string, int>();
        var stmts = new List<StatementAst>();

        int i = 0;
        foreach (var lbl in labels)
        {
            labelMap[lbl] = i;
            stmts.Add(new StatementAst.Label(lbl, ImmutableArray<StatementAst.ContractParam>.Empty, new TextPosition(1, 1, 0)));
            i++;
        }
        stmts.Add(new StatementAst.VerbCall(new VerbCallAst(null, "noop", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray<ValueAst>.Empty, new TextPosition(1, 1, 0))));

        return new CompiledStory(name, ImmutableDictionary<string, ZohValue>.Empty, stmts.ToImmutableArray(), labelMap.ToImmutableDictionary(), ImmutableDictionary<string, ImmutableArray<StatementAst.ContractParam>>.Empty);
    }

    [Fact]
    public void Fork_PropagatesToScheduler()
    {
        var story = CreateStory("main", "thread");
        var ctx = CreateContext(story);

        var scheduled = new List<Context>();
        ctx.ContextScheduler = (c) => scheduled.Add(c);

        var driver = new ForkDriver();
        var call = new VerbCallAst("core", "fork", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("thread")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        Assert.Single(scheduled);
        Assert.Equal(0, scheduled[0].InstructionPointer); // At "thread" (index 0)
        Assert.Same(story, scheduled[0].CurrentStory);
    }

    [Fact]
    public void Fork_Clone_CopiesVariables()
    {
        var story = CreateStory("main", "thread");
        var ctx = CreateContext(story);
        ctx.Variables.Set("shared", new ZohInt(42));

        var scheduled = new List<Context>();
        ctx.ContextScheduler = (c) => scheduled.Add(c);

        var driver = new ForkDriver();
        // /fork [clone] "thread"
        var attrs = ImmutableArray.Create(new AttributeAst("clone", null, new TextPosition(1, 1, 0)));
        var call = new VerbCallAst("core", "fork", false, attrs, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("thread")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        var child = scheduled[0];
        Assert.Equal(new ZohInt(42), child.Variables.Get("shared"));
    }

    [Fact]
    public void Call_BlocksParent_AndSchedulesChild()
    {
        var story = CreateStory("main", "subroutine");
        var ctx = CreateContext(story);

        var scheduled = new List<Context>();
        ctx.ContextScheduler = (c) => scheduled.Add(c);

        var driver = new CallDriver();
        var call = new VerbCallAst("core", "call", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("subroutine")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ContextState.WaitingContext, ctx.State);
        Assert.Single(scheduled);
        Assert.Same(scheduled[0], ctx.WaitCondition);
    }

    [Fact]
    public void Exit_TerminatesContext()
    {
        var story = CreateStory("main");
        var ctx = CreateContext(story);

        var driver = new ExitDriver();
        var call = new VerbCallAst("core", "exit", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray<ValueAst>.Empty, new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ContextState.Terminated, ctx.State);
    }
}
