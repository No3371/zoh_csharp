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

    [Fact]
    public void Jump_ToLocalLabel_UpdatesIP()
    {
        var story = CreateStory("test", "start", "target");
        // "start" is at index 0. "target" is at index 1.
        var ctx = CreateContext(story);

        var driver = new JumpDriver();
        var call = new VerbCallAst("core", "jump", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("target")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess, "Jump failed: " + (result.Diagnostics.Length > 0 ? result.Diagnostics[0].Message : ""));
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
        var call = new VerbCallAst("core", "jump", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("s2"), new ValueAst.String("entry")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.True(result.IsSuccess, "Jump failed: " + (result.Diagnostics.Length > 0 ? result.Diagnostics[0].Message : ""));
        Assert.Same(story2, ctx.CurrentStory);
        Assert.Equal(0, ctx.InstructionPointer); // "entry" is first label (index 0)
    }

    [Fact]
    public void Jump_InvalidLabel_ReturnsError()
    {
        var story = CreateStory("test");
        var ctx = CreateContext(story);
        var driver = new JumpDriver();

        var call = new VerbCallAst("core", "jump", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray.Create<ValueAst>(new ValueAst.String("missing")), new TextPosition(1, 1, 0));

        var result = driver.Execute(ctx, call);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_checkpoint", result.Diagnostics[0].Code); // JumpDriver uses "invalid_checkpoint"
    }
}
