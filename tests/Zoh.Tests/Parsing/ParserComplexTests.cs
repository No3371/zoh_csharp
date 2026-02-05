using System.Collections.Immutable;
using Xunit;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Tests.Parsing;

public class ParserComplexTests
{
    private static ParseResult Parse(string source)
    {
        var lexResult = new Lexer(source).Tokenize();
        // If lexing fails, return failed parse result or handle?
        // Parser constructor takes tokens.
        return new Parser(lexResult.Tokens).Parse();
    }

    [Fact]
    public void Parse_StandardInsideBlock_RequiresCommas()
    {
        // Block verb /list/ 
        // Elements are standard verbs.
        var source = @"/list/ 
    /set *x, 1; 
    /set *y, 2;
/;";
        var result = Parse(source);
        if (!result.Success) Assert.Fail(string.Join("\n", result.Errors));
        Assert.True(result.Success, "Should parse standard verbs inside block");

        // Verify structure
        var outer = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.True(outer.Call.IsBlock);
        Assert.Equal(2, outer.Call.UnnamedParams.Length); // Two standard verbs as values
    }

    [Fact]
    public void Parse_StandardInsideBlock_MissingComma_Fails()
    {
        var source = @"/list/ /set *x 1; /;"; // Missing comma in inner verb
        var result = Parse(source);
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("Expected comma"));
    }

    [Fact]
    public void Parse_BlockInsideStandard_AllowsSpaces()
    {
        // Standard verb /defer taking a block verb as value
        // /defer /block/ arg1 arg2 /;;
        var source = @"/defer /list/ 1 2 3 /;;";
        var result = Parse(source);
        Assert.True(result.Success);

        var outer = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.False(outer.Call.IsBlock);

        var innerVal = Assert.IsType<ValueAst.Verb>(outer.Call.UnnamedParams[0]);
        Assert.True(innerVal.Call.IsBlock);
        Assert.Equal(3, innerVal.Call.UnnamedParams.Length);
    }

    [Fact]
    public void Parse_MixedNesting_ParsesCorrectly()
    {
        // /outer, /inner/ a b /;, /standard v1, v2;;
        var source = @"/outer /inner/ a b /;, /standard v1, v2;;";
        var result = Parse(source);
        Assert.True(result.Success);

        var outer = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal(2, outer.Call.UnnamedParams.Length);

        var p1 = Assert.IsType<ValueAst.Verb>(outer.Call.UnnamedParams[0]);
        Assert.True(p1.Call.IsBlock); // /inner/

        var p2 = Assert.IsType<ValueAst.Verb>(outer.Call.UnnamedParams[1]);
        Assert.False(p2.Call.IsBlock); // /standard
    }
}
