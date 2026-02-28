using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Expressions;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Tests.Expressions;

public class ExpressionParserComplianceTests
{
    private ExpressionAst Parse(string source)
    {
        var lexer = new ExpressionLexer(source, new TextPosition(1, 1, 0));
        var tokens = lexer.Tokenize().Tokens;
        var parser = new ExpressionParser(tokens);
        return parser.Parse();
    }

    [Fact]
    public void Parse_Interpolation_String()
    {
        // New syntax: $"string"
        var ast = Parse("$\"Hello\"");
        Assert.IsType<InterpolateExpressionAst>(ast);
        var interp = (InterpolateExpressionAst)ast;
        Assert.IsType<LiteralExpressionAst>(interp.Expression);
    }

    [Fact]
    public void Parse_Interpolation_Ref()
    {
        // New syntax: $*var
        var ast = Parse("$*var");
        Assert.IsType<InterpolateExpressionAst>(ast);
        var interp = (InterpolateExpressionAst)ast;
        Assert.IsType<VariableExpressionAst>(interp.Expression);
    }

    [Fact]
    public void Parse_Selection_WithoutSuffix_Throws()
    {
        var ex = Assert.Throws<Exception>(() => Parse("$(1 | 2 | 3)"));
        Assert.Contains("requires '[index]' or '[%]' suffix", ex.Message, StringComparison.Ordinal);
        Assert.Contains("$?(", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Count()
    {
        // Spec: $#(*reference)
        var ast = Parse("$#(*foo)");
        Assert.IsType<CountExpressionAst>(ast);
        var count = (CountExpressionAst)ast;
        Assert.IsType<VariableExpressionAst>(count.Reference);
        Assert.Equal("foo", ((VariableExpressionAst)count.Reference).Name);
    }

    [Fact]
    public void Parse_Conditional_Ternary_UsesColon()
    {
        // Spec: $?(*cond ? *then : *else)
        // Example: $?(*score > 10 ? "Win" : "Lose")
        var ast = Parse("$?(*score > 10 ? \"Win\" : \"Lose\")");
        Assert.IsType<ConditionalExpressionAst>(ast);
        var cond = (ConditionalExpressionAst)ast;

        Assert.IsType<BinaryExpressionAst>(cond.Condition);
        Assert.IsType<LiteralExpressionAst>(cond.Then);
        Assert.IsType<LiteralExpressionAst>(cond.Else);
    }

    [Fact]
    public void Parse_Conditional_Ternary_WithPipe_Throws()
    {
        var ex = Assert.Throws<Exception>(() => Parse("$?(*score > 10 ? \"Win\" | \"Lose\")"));
        Assert.Contains("Expected ':' in ternary", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Any()
    {
        // Spec: $?(opt1 | opt2 | ...)
        // Example: $?("Hi" | "Hello" | "Hey")
        var ast = Parse("$?(\"Hi\" | \"Hello\" | \"Hey\")");
        Assert.IsType<AnyExpressionAst>(ast);
        var any = (AnyExpressionAst)ast;
        Assert.Equal(3, any.Options.Length);
        Assert.IsType<LiteralExpressionAst>(any.Options[0]);
    }

    [Fact]
    public void Parse_Any_With_Logical_Or()
    {
        // $?(1||2) - verify double pipe is treated as logical OR, NOT separate option
        var ast = Parse("$?(1||2)");
        Assert.IsType<AnyExpressionAst>(ast);
        var any = (AnyExpressionAst)ast;
        Assert.Single(any.Options);
        Assert.IsType<BinaryExpressionAst>(any.Options[0]);
    }

    [Fact]
    public void Parse_Indexed()
    {
        // Spec: $(options)[index]
        // Example: $("A"|"B")[0]
        var ast = Parse("$(\"A\"|\"B\")[0]");
        Assert.IsType<IndexedExpressionAst>(ast);
        var idx = (IndexedExpressionAst)ast;
        Assert.Equal(2, idx.Options.Length);
        Assert.IsType<LiteralExpressionAst>(idx.Index);
    }

    [Fact]
    public void Parse_Roll()
    {
        // Spec: $(options)[%]
        // Example: $("A"|"B")[%]
        var ast = Parse("$(\"A\"|\"B\")[%]");
        Assert.IsType<RollExpressionAst>(ast);
        var roll = (RollExpressionAst)ast;
        Assert.Equal(2, roll.Options.Length);
    }

    [Fact]
    public void Parse_Nested_Special_Forms()
    {
        // Same as before
        // $($?(true?1:0))[0]
        var ast = Parse("$($?(true?1:0))[0]");
        Assert.IsType<IndexedExpressionAst>(ast);
        var idx = (IndexedExpressionAst)ast;
        Assert.Single(idx.Options);
        Assert.IsType<ConditionalExpressionAst>(idx.Options[0]);
    }
}
