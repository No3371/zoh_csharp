using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Tests.Parsing;

/// <summary>
/// Tests for story header parsing: story name (multi-word), metadata, and separator.
/// </summary>
public class StoryHeaderParserTests
{
    private static ParseResult Parse(string source, bool header)
    {
        var lexResult = new Lexer(source, header).Tokenize();
        return new Parser(lexResult.Tokens).Parse();
    }

    #region Story Name Parsing

    [Fact]
    public void Parse_SingleWordName()
    {
        var source = "MyStory\n===\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("MyStory", result.Story!.Name);
    }

    [Fact]
    public void Parse_MultiWordName()
    {
        var source = "My Story\n===\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
    }

    [Fact]
    public void Parse_ThreeWordName()
    {
        var source = "The Last Coffee Shop\n===\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("The Last Coffee Shop", result.Story!.Name);
    }

    [Fact]
    public void Parse_NameWithCrLf()
    {
        // \r\n should be normalized — name should not contain \r
        var source = "My Story\r\n===\r\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
        Assert.DoesNotContain("\r", result.Story.Name);
    }

    [Fact]
    public void Parse_NameWithUnderscore()
    {
        var source = "my_story\n===\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("my_story", result.Story!.Name);
    }

    #endregion

    #region Story Name + Metadata

    [Fact]
    public void Parse_NameAndMetadata()
    {
        var source = "My Story\nauthor: \"Alice\"\n===\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
        Assert.Contains(result.Story.Metadata,
            kvp => kvp.Key == "author" && ((ValueAst.String)kvp.Value).Value == "Alice");
    }

    [Fact]
    public void Parse_NameAndMultipleMetadata()
    {
        var source = "My Story\nauthor: \"Alice\"\nversion: 2\n===\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
        Assert.Equal(2, result.Story.Metadata.Count);
    }

    [Fact]
    public void Parse_NameAndMetadata_CrLf()
    {
        var source = "My Story\r\nauthor: \"Bob\"\r\nversion: 1\r\n===\r\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
        Assert.Contains(result.Story.Metadata,
            kvp => kvp.Key == "author" && ((ValueAst.String)kvp.Value).Value == "Bob");
    }

    #endregion

    #region No Story Header (header=false)

    [Fact]
    public void Parse_NoHeader_StartsWithVerb()
    {
        var source = "/set *x, 10;";
        var result = Parse(source, false);
        Assert.True(result.Success);
        Assert.Equal("", result.Story!.Name);
        Assert.Single(result.Story.Statements);
    }

    [Fact]
    public void Parse_NoHeader_StartsWithLabel()
    {
        var source = "@main\n/set *x, 1;";
        var result = Parse(source, false);
        Assert.True(result.Success);
        Assert.Equal("", result.Story!.Name);
    }

    [Fact]
    public void Parse_NoHeader_StartsWithSugar()
    {
        var source = "*x <- 10;";
        var result = Parse(source, false);
        Assert.True(result.Success);
        Assert.Equal("", result.Story!.Name);
    }

    #endregion

    #region Story Header + Body

    [Fact]
    public void Parse_FullStory_NameAndStatements()
    {
        var source = "My Story\n===\n/set *x, 10;\n";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
        Assert.Single(result.Story.Statements);
        var stmt = Assert.IsType<StatementAst.VerbCall>(result.Story.Statements[0]);
        Assert.Equal("set", stmt.Call.Name);
    }

    [Fact]
    public void Parse_FullStory_NameMetadataAndStatements()
    {
        var source = @"The Adventure
author: ""Alice""
===
@start
/set *hp, 100;
";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("The Adventure", result.Story!.Name);
        Assert.Contains(result.Story.Metadata,
            kvp => kvp.Key == "author");
        Assert.Equal(2, result.Story.Statements.Length); // @start label + /set
    }

    [Fact]
    public void Parse_FullStory_WithLabelsAndJumps()
    {
        var source = @"Test Story
===
@start
/set *x, 0;
====> @end;
@end
";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("Test Story", result.Story!.Name);
        Assert.Equal(2, result.Story.Labels.Count);
        Assert.True(result.Story.Labels.ContainsKey("start"));
        Assert.True(result.Story.Labels.ContainsKey("end"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_EmptyNameWithSeparator_HeaderFalse()
    {
        // === with no name — use header=false when there's no story name
        var source = "===\n/set *x, 1;";
        var result = Parse(source, false);
        Assert.True(result.Success);
        Assert.Equal("", result.Story!.Name);
    }

    [Fact]
    public void Parse_EmptyNameWithSeparator_HeaderTrue_Limitation()
    {
        // Known limitation: header=true with === but no name doesn't parse cleanly.
        // The _inCheckingStoryName flag stays true because no newline is hit before ===.
        // Callers should use header=false when there's no story name.
        var source = "===\n/set *x, 1;";
        var result = Parse(source, true);
        // This currently fails because StoryNameEnd is never emitted before ===
        Assert.False(result.Success);
    }

    [Fact]
    public void Parse_NameOnly_NoSeparator_NoStatements()
    {
        // Just a name with header=true, no === and no statements
        var source = "My Story";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
        Assert.Empty(result.Story.Statements);
    }

    [Fact]
    public void Parse_VerbatimStringSource_CrLf()
    {
        // Verbatim string in C# produces \r\n on Windows
        // This is the original scenario that triggered the bug
        var source = @"My Story
author: ""Author""
version: 1.0
===
@start
";
        var result = Parse(source, true);
        Assert.True(result.Success);
        Assert.Equal("My Story", result.Story!.Name);
        Assert.DoesNotContain("\r", result.Story.Name);
    }

    #endregion
}
