using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs.Flow;
using Zoh.Runtime.Verbs.Nav;
using Zoh.Runtime.Verbs; // Added
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Storage;
using System.Collections.Generic;
using System.Linq;

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
            VerbExecutor = (v, c) => DriverResult.Complete.Ok() // Mock executor
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

    private CompiledStory CreateStoryWithContract(string name, string checkpointName, params string[] requiredVars)
    {
        var labelMap = new Dictionary<string, int>();
        var stmts = new List<StatementAst>();
        var contractParams = requiredVars
            .Select(v => new StatementAst.ContractParam(v, null, new TextPosition(1, 1, 0)))
            .ToImmutableArray();
        var contracts = ImmutableDictionary<string, ImmutableArray<StatementAst.ContractParam>>.Empty
            .Add(checkpointName, contractParams);

        labelMap[checkpointName] = 0;
        stmts.Add(new StatementAst.Label(checkpointName, contractParams, new TextPosition(1, 1, 0)));
        stmts.Add(new StatementAst.VerbCall(new VerbCallAst(null, "noop", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray<ValueAst>.Empty, new TextPosition(1, 1, 0))));

        return new CompiledStory(name, ImmutableDictionary<string, ZohValue>.Empty, stmts.ToImmutableArray(), labelMap.ToImmutableDictionary(), contracts);
    }

    [Fact]
    public void Fork_PropagatesToScheduler()
    {
        var story = CreateStory("main", "thread");
        var ctx = CreateContext(story);

        var scheduled = new List<Context>();
        ctx.ContextScheduler = (c) => scheduled.Add(c);

        var driver = new ForkDriver();
        var call = new VerbCallAst("core.nav", "fork", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("thread")), new TextPosition(1, 1, 0));

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
        var call = new VerbCallAst("core.nav", "fork", false, attrs, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("thread")), new TextPosition(1, 1, 0));

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
        var call = new VerbCallAst("core.nav", "call", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("subroutine")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        var suspend = Assert.IsType<DriverResult.Suspend>(result);
        var req = Assert.IsType<JoinContextRequest>(suspend.Continuation.Request);
        Assert.Single(scheduled);
        Assert.Single(scheduled);
        Assert.Equal(scheduled[0].Id, req.Handle.Id);
        Assert.Equal(ContextState.Running, ctx.State); // driver no longer mutates state
    }

    [Fact]
    public void Call_Inline_CopiesVariablesBack()
    {
        var story = CreateStory("main", "sub");
        var ctx = CreateContext(story);
        ctx.Variables.Set("in_out", new ZohInt(1));

        var scheduled = new List<Context>();
        ctx.ContextScheduler = (c) => scheduled.Add(c);

        var driver = new CallDriver();
        var attrs = ImmutableArray.Create(new AttributeAst("inline", null, new TextPosition(1, 1, 0)));
        // /call [inline] "sub", *in_out;
        var call = new VerbCallAst("core.nav", "call", false, attrs,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(new ValueAst.String("sub"), new ValueAst.Reference("in_out")),
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        var suspend = Assert.IsType<DriverResult.Suspend>(result);

        var child = scheduled[0];
        // Mutate in child
        child.Variables.Set("in_out", new ZohInt(42));

        // Complete the wait
        var resumeResult = suspend.Continuation.OnFulfilled(new WaitCompleted(ZohValue.Nothing));

        Assert.True(resumeResult.IsSuccess);

        // Assert value copied back to parent
        Assert.Equal(new ZohInt(42), ctx.Variables.Get("in_out"));
    }

    [Fact]
    public void Exit_TerminatesContext()
    {
        var story = CreateStory("main");
        var ctx = CreateContext(story);

        var driver = new ExitDriver();
        var call = new VerbCallAst("core.flow", "exit", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray<ValueAst>.Empty, new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(ContextState.Terminated, ctx.State);
    }

    // --- Invalid params (missing arg) tests ---

    [Fact]
    public void Jump_MissingArgument_ReturnsFatalWithInvalidParams()
    {
        var story = CreateStory("main", "somewhere");
        var ctx = CreateContext(story);
        var driver = new JumpDriver();
        var call = new VerbCallAst("core.nav", "jump", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray<ValueAst>.Empty,
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsFatal);
        Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_params");
    }

    [Fact]
    public void Fork_MissingArgument_ReturnsFatalWithInvalidParams()
    {
        var story = CreateStory("main", "somewhere");
        var ctx = CreateContext(story);
        ctx.ContextScheduler = (_) => { };
        var driver = new ForkDriver();
        var call = new VerbCallAst("core.nav", "fork", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray<ValueAst>.Empty,
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsFatal);
        Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_params");
    }

    [Fact]
    public void Call_MissingArgument_ReturnsFatalWithInvalidParams()
    {
        var story = CreateStory("main", "sub");
        var ctx = CreateContext(story);
        ctx.ContextScheduler = (_) => { };
        var driver = new CallDriver();
        var call = new VerbCallAst("core.nav", "call", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray<ValueAst>.Empty,
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsFatal);
        Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_params");
    }

    [Fact]
    public void Fork_TransfersSpecifiedVariablesToChild()
    {
        // Checkpoint "worker" requires variable "payload" in its contract
        var story = CreateStoryWithContract("main", "worker", "payload");
        var ctx = CreateContext(story);
        ctx.Variables.Set("payload", new ZohStr("hello"));

        var scheduled = new List<Context>();
        ctx.ContextScheduler = (c) => scheduled.Add(c);

        var driver = new ForkDriver();
        // /fork "worker", *payload; — label first, then trailing reference
        var call = new VerbCallAst("core.nav", "fork", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(new ValueAst.String("worker"), new ValueAst.Reference("payload")),
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.Length > 0 ? result.DiagnosticsOrEmpty[0].Message : "");
        Assert.Single(scheduled);
        var child = scheduled[0];
        Assert.Equal(new ZohStr("hello"), child.Variables.Get("payload"));
        Assert.Equal(0, child.InstructionPointer);
    }

    [Fact]
    public void Fork_WithoutTransfer_ContractViolationFails()
    {
        var story = CreateStoryWithContract("main", "worker", "payload");
        var ctx = CreateContext(story);
        // payload is NOT set in parent

        ctx.ContextScheduler = (_) => { };
        var driver = new ForkDriver();
        var call = new VerbCallAst("core.nav", "fork", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(new ValueAst.String("worker")),
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "checkpoint_violation");
    }

    [Fact]
    public void Fork_NonReferenceTransferParam_ReturnsFatal()
    {
        var story = CreateStory("main", "worker");
        var ctx = CreateContext(story);
        ctx.ContextScheduler = (_) => { };
        var driver = new ForkDriver();
        // /fork "worker", *someref, "literal"; — "literal" is not a reference
        var call = new VerbCallAst("core.nav", "fork", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(
                new ValueAst.String("worker"),
                new ValueAst.Reference("someref"),
                new ValueAst.String("not_a_ref")),
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsFatal);
        Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
    }
}

