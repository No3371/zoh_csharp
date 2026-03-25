using System.Collections.Immutable;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Tests.Parsing;

public class ParserTests
{
    private static ParseResult Parse(string source, bool header)
    {
        var lexResult = new Lexer(source, header).Tokenize();
        return new Parser(lexResult.Tokens).Parse();
    }

    [Fact]
    public void Parse_EmptySource_ReturnsEmptyStory()
    {
        var result = Parse("", true);
        Assert.True(result.Success);
        Assert.NotNull(result.Story);
        Assert.Empty(result.Story.Statements);
    }

    [Fact]
    public void Parse_StoryHeader_ParsesNameAndMetadata()
    {
        var source = @"My Story
author: ""John""
version: 1
===
";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
    }

    [Fact]
    public void Parse_Label_CreatesLabelStatement()
    {
        var result = Parse("@main", false);
        Assert.True(result.Success);
        Assert.Single(result.Story!.Statements);

        var label = Assert.IsType<StatementAst.Label>(result.Story.Statements[0]);
        Assert.Equal("main", label.Name);
        Assert.Single(result.Story.Labels);
        Assert.Equal(0, result.Story.Labels["main"]);
    }

    [Fact]
    public void Parse_SimpleVerbCall_ParsesCorrectly()
    {
        var result = Parse("/set \"name\", \"value\";", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("set", stmt.Call.Name);
        Assert.Equal(2, stmt.Call.UnnamedParams.Length);
    }

    [Fact]
    public void Parse_VerbCallWithAttributes_ParsesAttributes()
    {
        var result = Parse("/set [scope:story] [required] \"name\", \"value\";", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal(2, stmt.Call.Attributes.Length);
        Assert.Equal("scope", stmt.Call.Attributes[0].Name);
        Assert.Equal("required", stmt.Call.Attributes[1].Name);
    }

    [Fact]
    public void Parse_VerbCallWithNamedParams_ParsesNamedParams()
    {
        var result = Parse("/converse by: \"narrator\", \"Hello world\";", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.True(stmt.Call.NamedParams.ContainsKey("by"));
        Assert.Single(stmt.Call.UnnamedParams);
    }

    [Fact]
    public void Parse_SetSugar_TransformsToSetVerb()
    {
        var result = Parse("*name <- \"Alice\";", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("set", stmt.Call.Name);
        Assert.Equal("core.var", stmt.Call.Namespace);
        Assert.Equal(2, stmt.Call.UnnamedParams.Length);
    }

    [Fact]
    public void Parse_GetSugar_TransformsToGetVerb()
    {
        var result = Parse("<- *name;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("get", stmt.Call.Name);
    }

    [Fact]
    public void Parse_CaptureSugar_TransformsToCaptureVerb()
    {
        var result = Parse("-> *result;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("capture", stmt.Call.Name);
    }

    [Fact]
    public void Parse_JumpSugar_TransformsToJumpVerb()
    {
        var result = Parse("====> @target;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("jump", stmt.Call.Name);

        // Expect: [Nothing, "target"]
        Assert.Equal(2, stmt.Call.UnnamedParams.Length);
        Assert.IsType<ValueAst.Nothing>(stmt.Call.UnnamedParams[0]);
        var target = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Equal("target", target.Value);
    }

    [Fact]
    public void Parse_ForkSugar_TransformsToForkVerb()
    {
        var result = Parse("====+ @worker *arg1;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("fork", stmt.Call.Name);

        // Expect: [Nothing, "worker", *arg1]
        Assert.Equal(3, stmt.Call.UnnamedParams.Length);
        Assert.IsType<ValueAst.Nothing>(stmt.Call.UnnamedParams[0]);
        var target = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Equal("worker", target.Value);
    }

    [Fact]
    public void Parse_CallSugar_TransformsToCallVerb()
    {
        var result = Parse("<===+ @subroutine;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("call", stmt.Call.Name);
    }

    [Fact]
    public void Parse_ListValue_ParsesElements()
    {
        var result = Parse("/set \"arr\", [1, 2, 3];", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var list = Assert.IsType<ValueAst.List>(stmt.Call.UnnamedParams[1]);
        Assert.Equal(3, list.Elements.Length);
    }

    [Fact]
    public void Parse_MapValue_ParsesEntries()
    {
        var result = Parse("/set \"obj\", {\"a\": 1, \"b\": 2};", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var map = Assert.IsType<ValueAst.Map>(stmt.Call.UnnamedParams[1]);
        Assert.Equal(2, map.Entries.Length);
    }

    [Fact]
    public void Parse_ReferenceWithIndex_ParsesIndex()
    {
        var result = Parse("/get *arr[0];", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var refVal = Assert.IsType<ValueAst.Reference>(stmt.Call.UnnamedParams[0]);
        Assert.Equal("arr", refVal.Name);
        Assert.Single(refVal.Path);
        var idx = Assert.IsType<ValueAst.Integer>(refVal.Path[0]);
        Assert.Equal(0, idx.Value);
    }

    [Fact]
    public void Parse_ChannelValue_ParsesChannel()
    {
        var result = Parse("/push <data>, 42;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var chan = Assert.IsType<ValueAst.Channel>(stmt.Call.UnnamedParams[0]);
        Assert.Equal("data", chan.Name);
    }

    [Fact]
    public void Parse_ExpressionValue_ParsesExpression()
    {
        var result = Parse("/evaluate `*x + 1`;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var expr = Assert.IsType<ValueAst.Expression>(stmt.Call.UnnamedParams[0]);
        Assert.Equal("*x + 1", expr.Source);
    }

    [Fact]
    public void Parse_MultipleStatements_ParsesAll()
    {
        var source = @"
@start
*count <- 0;
/converse ""Hello"";
====> @end;
@end
";
        var result = Parse(source, false);
        Assert.True(result.Success);
        Assert.Equal(5, result.Story!.Statements.Length);
        Assert.Equal(2, result.Story.Labels.Count);
    }

    [Fact]
    public void Parse_DuplicateLabel_ReportsError()
    {
        var result = Parse("@foo\n@foo", false);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate label"));
    }

    #region Literal Parsing Tests

    [Fact]
    public void Parse_NothingLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"x\", ?;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.Nothing>(stmt.Call.UnnamedParams[1]);
    }

    [Fact]
    public void Parse_TrueLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"flag\", true;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.Boolean>(stmt.Call.UnnamedParams[1]);
        Assert.True(value.Value);
    }

    [Fact]
    public void Parse_FalseLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"flag\", false;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.Boolean>(stmt.Call.UnnamedParams[1]);
        Assert.False(value.Value);
    }

    [Fact]
    public void Parse_IntegerLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"num\", 42;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.Integer>(stmt.Call.UnnamedParams[1]);
        Assert.Equal(42L, value.Value);
    }

    [Fact]
    public void Parse_NegativeIntegerLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"num\", -100;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.Integer>(stmt.Call.UnnamedParams[1]);
        Assert.Equal(-100L, value.Value);
    }

    [Fact]
    public void Parse_DoubleLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"pi\", 3.14159;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.Double>(stmt.Call.UnnamedParams[1]);
        Assert.Equal(3.14159, value.Value);
    }

    [Fact]
    public void Parse_NegativeDoubleLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"temp\", -273.15;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.Double>(stmt.Call.UnnamedParams[1]);
        Assert.Equal(-273.15, value.Value);
    }

    [Fact]
    public void Parse_StringLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"greeting\", \"Hello, World!\";", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Equal("Hello, World!", value.Value);
    }

    [Fact]
    public void Parse_EmptyStringLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"empty\", \"\";", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Equal("", value.Value);
    }

    [Fact]
    public void Parse_MultilineStringLiteral_ParsesCorrectly()
    {
        var result = Parse("/set \"text\", \"\"\"Line 1\nLine 2\nLine 3\"\"\";", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var value = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Contains("Line 1", value.Value);
        Assert.Contains("Line 2", value.Value);
        Assert.Contains("Line 3", value.Value);
    }

    [Fact]
    public void Parse_IdentifierAsString_ParsesCorrectly()
    {
        // Bare identifiers in value positions become strings (e.g., [scope:story])
        var result = Parse("/set [scope:story] \"x\", 1;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        var attrValue = Assert.IsType<ValueAst.String>(stmt.Call.Attributes[0].Value);
        Assert.Equal("story", attrValue.Value);
    }

    #endregion
    [Fact]
    public void Parse_StandardVerb_MissingComma_Fails()
    {
        var result = Parse("/set \"name\" \"value\";", false);
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("Expected comma"));
    }
    [Fact]
    public void Parse_JumpSugar_WithStoryAndLabel_ParsesBoth()
    {
        var result = Parse("====> @mystory:target;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("jump", stmt.Call.Name);

        // First param should be story name "mystory"
        var storyParam = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[0]);
        Assert.Equal("mystory", storyParam.Value);

        // Second param should be label name "target"
        var labelParam = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Equal("target", labelParam.Value);
    }

    [Fact]
    public void Parse_ForkSugar_WithStoryAndLabel_ParsesBoth()
    {
        var result = Parse("====+ @otherstory:worker;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("fork", stmt.Call.Name);

        var storyParam = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[0]);
        Assert.Equal("otherstory", storyParam.Value);
        var labelParam = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Equal("worker", labelParam.Value);
    }

    [Fact]
    public void Parse_CallSugar_WithStoryAndLabel_ParsesBoth()
    {
        var result = Parse("<===+ @lib:subroutine;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("call", stmt.Call.Name);

        var storyParam = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[0]);
        Assert.Equal("lib", storyParam.Value);
        var labelParam = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Equal("subroutine", labelParam.Value);
    }

    [Fact]
    public void Parse_JumpSugar_LabelOnly_UsesNothingForStory()
    {
        var result = Parse("====> @target;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal("jump", stmt.Call.Name);

        // Expect: [Nothing, "target"]
        Assert.Equal(2, stmt.Call.UnnamedParams.Length);
        Assert.IsType<ValueAst.Nothing>(stmt.Call.UnnamedParams[0]);
        var label = Assert.IsType<ValueAst.String>(stmt.Call.UnnamedParams[1]);
        Assert.Equal("target", label.Value);
    }

    [Fact]
    public void Parse_JumpSugar_WithVariables()
    {
        // ====> @story:label *var1 *var2;
        var result = Parse("====> @dest:main *arg1 *arg2;", false);
        Assert.True(result.Success);

        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story!.Statements[0]);
        Assert.Equal(4, stmt.Call.UnnamedParams.Length); // story, label, var1, var2
    }
}

