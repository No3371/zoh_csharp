using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs.Flow;
using Zoh.Runtime.Verbs.Nav;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Storage;
using System.Collections.Generic;
using System.Linq;

namespace Zoh.Tests.Verbs.Flow;

public class NavigationTests
{
    private Context CreateContext(CompiledStory story)
    {
        var store = new VariableStore(new Dictionary<string, Variable>());
        var storage = new InMemoryStorage();
        var channels = new ChannelManager();
        var ctx = new Context(store, storage, channels, new SignalManager())
        {
            CurrentStory = story,
            InstructionPointer = 0
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
        // Add dummy statement at end
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
    public void Jump_ToLocalLabel_UpdatesIP()
    {
        var story = CreateStory("test", "start", "target");
        // "start" is at index 0. "target" is at index 1.
        var ctx = CreateContext(story);

        var driver = new JumpDriver();
        var call = new VerbCallAst("core.nav", "jump", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("target")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess, "Jump failed: " + (result.DiagnosticsOrEmpty.Length > 0 ? result.DiagnosticsOrEmpty[0].Message : ""));
        Assert.Equal(1, ctx.InstructionPointer);
        Assert.Same(story, ctx.CurrentStory);
    }

    [Fact]
    public void Jump_ToOtherStory_UpdatesStoryAndIP()
    {
        var story1 = CreateStory("s1");
        var story2 = CreateStory("s2", "entry");

        var ctx = CreateContext(story1);
        ctx.StoryLoader = (name) => name == "s2" ? story2 : null;
        ctx.ContextScheduler = (c) => { }; // Not needed for Jump but good practice

        var driver = new JumpDriver();
        // /jump "s2", "entry"
        var call = new VerbCallAst("core.nav", "jump", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("s2"), new ValueAst.String("entry")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess, "Jump failed: " + (result.DiagnosticsOrEmpty.Length > 0 ? result.DiagnosticsOrEmpty[0].Message : ""));
        Assert.Same(story2, ctx.CurrentStory);
        Assert.Equal(0, ctx.InstructionPointer); // "entry" is first label (index 0)
    }

    [Fact]
    public void Jump_InvalidLabel_ReturnsError()
    {
        var story = CreateStory("test");
        var ctx = CreateContext(story);
        var driver = new JumpDriver();

        var call = new VerbCallAst("core.nav", "jump", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("missing")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_checkpoint", result.DiagnosticsOrEmpty[0].Code); // JumpDriver uses "invalid_checkpoint"
    }

    [Fact]
    public void Jump_TransfersVariablesToTargetCheckpoint()
    {
        // Checkpoint "entry" requires variable "x" in its contract
        var story = CreateStoryWithContract("test", "entry", "x");
        var ctx = CreateContext(story);
        ctx.Variables.Set("x", new ZohInt(99));

        var driver = new JumpDriver();
        // /jump "entry", *x; — label is first string, *x is trailing reference
        var call = new VerbCallAst("core.nav", "jump", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(new ValueAst.String("entry"), new ValueAst.Reference("x")),
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess, result.DiagnosticsOrEmpty.Length > 0 ? result.DiagnosticsOrEmpty[0].Message : "");
        Assert.Equal(0, ctx.InstructionPointer);
        Assert.Equal(new ZohInt(99), ctx.Variables.Get("x"));
    }

    [Fact]
    public void Jump_WithoutTransfer_ContractViolationFails()
    {
        // Same story but x is never set — contract should fail without transfer
        var story = CreateStoryWithContract("test", "entry", "x");
        var ctx = CreateContext(story);

        var driver = new JumpDriver();
        var call = new VerbCallAst("core.nav", "jump", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(new ValueAst.String("entry")),
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "checkpoint_violation");
    }

    [Fact]
    public void Jump_NonReferenceTransferParam_ReturnsFatal()
    {
        var story = CreateStory("test", "target");
        var ctx = CreateContext(story);
        var driver = new JumpDriver();
        // /jump "target", *someref, "bad"; — "bad" is a string literal, not a reference
        var call = new VerbCallAst("core.nav", "jump", false, ImmutableArray<AttributeAst>.Empty,
            ImmutableDictionary<string, ValueAst>.Empty,
            ImmutableArray.Create<ValueAst>(
                new ValueAst.String("target"),
                new ValueAst.Reference("someref"),
                new ValueAst.String("bad")),
            new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsFatal);
        Assert.Contains(result.DiagnosticsOrEmpty, d => d.Code == "invalid_type");
    }
}
