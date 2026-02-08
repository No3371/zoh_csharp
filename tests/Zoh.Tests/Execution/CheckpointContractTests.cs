using Xunit;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Storage;
using Zoh.Runtime.Verbs.Flow;
using System.Collections.Immutable;
using System.Collections.Generic;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Verbs;

namespace Zoh.Tests.Execution;

public class CheckpointContractTests
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

    private CompiledStory CreateStory(string name, string labelName, ImmutableArray<StatementAst.ContractParam> contractParams)
    {
        var stmts = new List<StatementAst>
        {
            new StatementAst.Label(labelName, contractParams, new TextPosition(1, 1, 0)),
            new StatementAst.VerbCall(new VerbCallAst(null, "noop", false, ImmutableArray<AttributeAst>.Empty, ImmutableDictionary<string, ValueAst>.Empty, ImmutableArray<ValueAst>.Empty, new TextPosition(2, 1, 0)))
        };

        var labelMap = new Dictionary<string, int> { { labelName, 0 } };
        var contracts = new Dictionary<string, ImmutableArray<StatementAst.ContractParam>>();
        if (!contractParams.IsEmpty)
        {
            contracts[labelName] = contractParams;
        }

        return new CompiledStory(name, ImmutableDictionary<string, ZohValue>.Empty, stmts.ToImmutableArray(), labelMap.ToImmutableDictionary(), contracts.ToImmutableDictionary());
    }

    [Fact]
    public void ValidateContract_Passes_WhenVariableExistsAndTypeMatches()
    {
        var paramsList = ImmutableArray.Create(new StatementAst.ContractParam("age", "integer", new TextPosition(1, 1, 0)));
        var story = CreateStory("test", "check", paramsList);
        var ctx = CreateContext(story);

        ctx.Variables.Set("age", new ZohInt(25));

        var result = ctx.ValidateContract("check");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateContract_Fails_WhenVariableIsNothing()
    {
        var paramsList = ImmutableArray.Create(new StatementAst.ContractParam("age", "integer", new TextPosition(1, 1, 0)));
        var story = CreateStory("test", "check", paramsList);
        var ctx = CreateContext(story);

        // age is implicitly Nothing

        var result = ctx.ValidateContract("check");

        Assert.False(result.IsSuccess);
        Assert.Equal("checkpoint_violation", result.Diagnostics[0].Code);
    }

    [Fact]
    public void ValidateContract_Fails_WhenTypeMismatch()
    {
        var paramsList = ImmutableArray.Create(new StatementAst.ContractParam("age", "integer", new TextPosition(1, 1, 0)));
        var story = CreateStory("test", "check", paramsList);
        var ctx = CreateContext(story);

        ctx.Variables.Set("age", new ZohStr("twenty-five"));

        var result = ctx.ValidateContract("check");

        Assert.False(result.IsSuccess);
        Assert.Equal("checkpoint_violation", result.Diagnostics[0].Code);
        Assert.Contains("integer", result.Diagnostics[0].Message);
        Assert.Contains("ZohStr", result.Diagnostics[0].Message);
    }

    [Theory]
    [InlineData("string", "test")]
    [InlineData("integer", 123L)]
    [InlineData("boolean", true)]
    [InlineData("double", 1.5)]
    public void ValidateContract_Passes_ForVariousTypes(string typeName, object value)
    {
        var paramsList = ImmutableArray.Create(new StatementAst.ContractParam("val", typeName, new TextPosition(1, 1, 0)));
        var story = CreateStory("test", "check", paramsList);
        var ctx = CreateContext(story);

        ZohValue zVal = value switch
        {
            string s => new ZohStr(s),
            long l => new ZohInt(l),
            bool b => b ? ZohBool.True : ZohBool.False,
            double d => new ZohFloat(d),
            _ => ZohValue.Nothing
        };

        ctx.Variables.Set("val", zVal);

        var result = ctx.ValidateContract("check");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateContract_Passes_WhenNoTypeSpecified()
    {
        var paramsList = ImmutableArray.Create(new StatementAst.ContractParam("anyvar", null, new TextPosition(1, 1, 0)));
        var story = CreateStory("test", "check", paramsList);
        var ctx = CreateContext(story);

        ctx.Variables.Set("anyvar", new ZohStr("anything"));

        var result = ctx.ValidateContract("check");

        Assert.True(result.IsSuccess);
    }
}
