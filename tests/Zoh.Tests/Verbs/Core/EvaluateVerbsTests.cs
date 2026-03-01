using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Tests.Execution;
using Zoh.Runtime.Verbs.Core;
using System.Collections.Immutable;
using Xunit;

namespace Zoh.Tests.Verbs.Core;

public class EvaluateVerbsTests
{
    private readonly TestExecutionContext _context = new();
    private readonly EvaluateDriver _driver = new();

    private VerbCallAst MakeEvalCall(ValueAst expr)
    {
        return new VerbCallAst(
           "core", "evaluate", false, [],
           ImmutableDictionary<string, ValueAst>.Empty,
           [expr],
           new Zoh.Runtime.Lexing.TextPosition(1, 1, 0));
    }

    [Fact]
    public void Eval_Arithmetic()
    {
        // /eval 1 + 2 (Simulated by ExpressionAst)
        // Creating specific AST nodes manually is verbose, but we can verify integration if we had parser.
        // Or we can mock the Resolve to return specific things? 
        // ValueResolver calls Evaluator.
        // Let's manually construct a simple AST expression for 1 + 2

        var left = new ValueAst.Integer(1);
        var right = new ValueAst.Integer(2);
        // Note: ExpressionAst structure needed.
        // ExpressionEvaluator takes ExpressionAst. ValueAst.Expression wraps a string source.
        // ValueResolver parses the string source.

        var exprSource = "1 + 2";
        var call = MakeEvalCall(new ValueAst.Expression(exprSource, new Zoh.Runtime.Lexing.TextPosition(1, 1, 0)));

        var result = _driver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(3), result.ValueOrNothing);
    }

    [Fact]
    public void Eval_VariableRefs()
    {
        _context.Variables.Set("x", new ZohInt(10));
        var exprSource = "*x * 2";
        var call = MakeEvalCall(new ValueAst.Expression(exprSource, new Zoh.Runtime.Lexing.TextPosition(1, 1, 0)));

        var result = _driver.Execute(_context, call);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ZohInt(20), result.ValueOrNothing);
    }

    [Fact]
    public void Eval_UndefinedVar_ReturnsFatal()
    {
        var exprSource = "*missing + 1";
        var call = MakeEvalCall(new ValueAst.Expression(exprSource, new Zoh.Runtime.Lexing.TextPosition(1, 1, 0)));

        // This will throw inside ValueResolver/Evaluator
        // Driver should catch logic exceptions? 
        // Or if ValueResolver throws, we want to know what it throws.

        // Currently Evaluator logic might throw KeyNotFound or InvalidOperation.
        // We added try-catch in driver.

        var result = _driver.Execute(_context, call);

        Assert.False(result.IsSuccess);
        Assert.Equal(Zoh.Runtime.Diagnostics.DiagnosticSeverity.Error, result.DiagnosticsOrEmpty[0].Severity);
        // VerbResult.Fatal creates a list of 1 diagnostic.
        Assert.Contains("Undefined variable", result.DiagnosticsOrEmpty[0].Message, StringComparison.OrdinalIgnoreCase);
        // Note: Assuming Evaluator throws specific message. If not, this might fail until we fix Evaluator.
    }
}
