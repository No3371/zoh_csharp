using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Tests.Execution;
using Zoh.Runtime.Verbs.Core;
using System.Collections.Immutable;
using Xunit;

namespace Zoh.Tests.Verbs.Core;

public class ControlFlowVerbsTests
{
    private readonly TestExecutionContext _context = new();
    private readonly DoDriver _do = new();

    private VerbCallAst MakeCall(params ValueAst[] args)
    {
        return new VerbCallAst(
           "core", "do", false, [],
           ImmutableDictionary<string, ValueAst>.Empty,
           [.. args],
           new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Fact]
    public void Do_ExecutesVerb()
    {
        // Mock verb execution
        bool executed = false;
        _context.VerbExecutor = (verb, ctx) =>
        {
            executed = true;
            return Zoh.Runtime.Verbs.VerbResult.Ok(new ZohInt(42));
        };

        // Value to invoke must be a ZohVerb wrapping a AST.
        // We'll manually inject one into variables.
        var targetVerbAst = new VerbCallAst("core", "dummy", false, [], ImmutableDictionary<string, ValueAst>.Empty, [], new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
        var zohVerb = ZohVerb.FromAst(targetVerbAst);

        _context.Variables.Set("myVerb", zohVerb);

        // /do *myVerb
        var call = MakeCall(new ValueAst.Reference("myVerb"));
        var result = _do.Execute(_context, call);

        Assert.True(executed);
        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(42), result.Value);
    }
}
