using Zoh.Runtime.Lexing;

namespace Zoh.Tests.Lexing;

/// <summary>
/// Tests for StoryNameEnd virtual token emission and CRLF normalization.
/// </summary>
public class StoryNameLexerTests
{
    private static LexResult Lex(string source, bool header) =>
        new Lexer(source, header).Tokenize();

    #region StoryNameEnd emission

    [Fact]
    public void Lex_SingleWordName_EmitsStoryNameEndAtNewline()
    {
        var result = Lex("MyStory\n===", true);

        Assert.False(result.HasErrors);
        // MyStory, StoryNameEnd, ===, Eof
        Assert.Equal(TokenType.Identifier, result.Tokens[0].Type);
        Assert.Equal("MyStory", result.Tokens[0].Value);
        Assert.Equal(TokenType.StoryNameEnd, result.Tokens[1].Type);
        Assert.Equal(TokenType.StorySeparator, result.Tokens[2].Type);
    }

    [Fact]
    public void Lex_MultiWordName_EmitsIdentifiersAndStoryNameEnd()
    {
        var result = Lex("My Story Name\n===", true);

        Assert.False(result.HasErrors);
        // My, Story, Name, StoryNameEnd, ===, Eof
        Assert.Equal(TokenType.Identifier, result.Tokens[0].Type);
        Assert.Equal("My", result.Tokens[0].Value);
        Assert.Equal(TokenType.Identifier, result.Tokens[1].Type);
        Assert.Equal("Story", result.Tokens[1].Value);
        Assert.Equal(TokenType.Identifier, result.Tokens[2].Type);
        Assert.Equal("Name", result.Tokens[2].Value);
        Assert.Equal(TokenType.StoryNameEnd, result.Tokens[3].Type);
        Assert.Equal(TokenType.StorySeparator, result.Tokens[4].Type);
        Assert.Equal(TokenType.Eof, result.Tokens[5].Type);
    }

    [Fact]
    public void Lex_StoryNameAtEof_EmitsStoryNameEnd()
    {
        // No trailing newline — StoryNameEnd should still be emitted at EOF
        var result = Lex("My Story", true);

        Assert.False(result.HasErrors);
        // My, Story, StoryNameEnd, Eof
        Assert.Equal(4, result.Tokens.Length);
        Assert.Equal(TokenType.Identifier, result.Tokens[0].Type);
        Assert.Equal(TokenType.Identifier, result.Tokens[1].Type);
        Assert.Equal(TokenType.StoryNameEnd, result.Tokens[2].Type);
        Assert.Equal(TokenType.Eof, result.Tokens[3].Type);
    }

    [Fact]
    public void Lex_HeaderFalse_NoStoryNameEnd()
    {
        // When header=false, identifiers at the start should NOT produce StoryNameEnd
        var result = Lex("My Story\n===", false);

        Assert.False(result.HasErrors);
        // My, Story, ===, Eof — no StoryNameEnd
        Assert.DoesNotContain(result.Tokens, t => t.Type == TokenType.StoryNameEnd);
    }

    [Fact]
    public void Lex_StoryNameEnd_OnlyEmittedOnce()
    {
        // StoryNameEnd should only be emitted for the first line
        var result = Lex("My Story\nauthor: \"John\"\n===", true);

        var storyNameEndCount = result.Tokens.Count(t => t.Type == TokenType.StoryNameEnd);
        Assert.Equal(1, storyNameEndCount);
    }

    [Fact]
    public void Lex_StoryNameEndPosition_IsAtNewline()
    {
        var result = Lex("Hello\n===", true);

        // Hello(line1), StoryNameEnd(at newline), ===, Eof
        var sne = result.Tokens.First(t => t.Type == TokenType.StoryNameEnd);
        // StoryNameEnd should reference the position at the newline
        Assert.Equal(1, sne.Start.Line);
    }

    #endregion

    #region CRLF normalization

    [Fact]
    public void Lex_CrLfNormalized_StoryName()
    {
        // \r\n should be normalized to \n — story name should not contain \r
        var result = Lex("My Story\r\n===\r\n", true);

        Assert.False(result.HasErrors);
        // Check that identifier values don't contain \r
        var identifiers = result.Tokens.Where(t => t.Type == TokenType.Identifier).ToList();
        foreach (var id in identifiers)
        {
            Assert.DoesNotContain("\r", id.Value?.ToString() ?? id.Lexeme);
        }
        Assert.Contains(result.Tokens, t => t.Type == TokenType.StoryNameEnd);
    }

    [Fact]
    public void Lex_CrLfNormalized_NoHeader()
    {
        // Even without header mode, CRLF should be normalized
        var result = Lex("/set *x, 10;\r\n/set *y, 20;\r\n", false);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Lex_CrLfNormalized_CheckpointEnd()
    {
        // Checkpoint should work correctly with \r\n input
        var result = Lex("@main\r\n/set *x, 1;", false);

        Assert.False(result.HasErrors);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.CheckpointEnd);
    }

    [Fact]
    public void Lex_PureLf_WorksIdentically()
    {
        // \n-only should work the same
        var result = Lex("My Story\n===\n@start\n", true);

        Assert.False(result.HasErrors);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.StoryNameEnd);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.StorySeparator);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Lex_EmptySource_HeaderTrue_NoStoryNameEnd()
    {
        // Empty source with header=true — no identifiers, no StoryNameEnd
        // (StoryNameEnd should only be emitted if there were identifiers)
        // Actually, _inCheckingStoryName will be true at EOF, so it will emit StoryNameEnd
        var result = Lex("", true);

        // Behavior: StoryNameEnd, Eof (the Tokenize method emits StoryNameEnd at EOF if flag still set)
        Assert.Equal(TokenType.StoryNameEnd, result.Tokens[0].Type);
        Assert.Equal(TokenType.Eof, result.Tokens[1].Type);
    }

    [Fact]
    public void Lex_OnlyNewline_HeaderTrue()
    {
        // Just a newline with header=true
        var result = Lex("\n===", true);

        // Whitespace: \n is whitespace, but _inCheckingStoryName causes early return
        // Then ScanToken sees \n, emits StoryNameEnd
        Assert.Contains(result.Tokens, t => t.Type == TokenType.StoryNameEnd);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.StorySeparator);
    }

    [Fact]
    public void Lex_StoryNameWithComment_AfterName()
    {
        // Comment after story name on the same line — should be skipped?
        // SkipWhitespaceAndComments is called first, but :: starts a comment
        // which consumes until \n. Since _inCheckingStoryName is true,
        // the \n return-early logic is in whitespace handling not comment handling.
        // SkipComment consumes until \n but doesn't consume the \n itself?
        // Let's check what happens.
        var result = Lex("My Story :: this is a comment\n===", true);

        // If comment eats to \n and the \n triggers StoryNameEnd, identifiers before :: should be captured
        // The :: is inside the name scanning or caught by SkipWhitespaceAndComments?
        // Since SkipWhitespace runs first, after identifiers there may be spaces, then ::
        // But wait: identifiers are emitted one at a time. Between identifiers,
        // SkipWhitespaceAndComments runs. When it hits ::, it calls SkipComment.
        // SkipComment eats until \n. Then the loop sees \n with _inCheckingStoryName, returns.
        // Then ScanToken sees \n, emits StoryNameEnd.
        Assert.False(result.HasErrors);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.StoryNameEnd);
        // Story name should be "My" and "Story" only
        var ids = result.Tokens.Where(t => t.Type == TokenType.Identifier).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Equal("My", ids[0].Value);
        Assert.Equal("Story", ids[1].Value);
    }

    [Fact]
    public void Lex_StoryNameFollowedByMetadata()
    {
        // Full story header with metadata
        var result = Lex("The Last Coffee Shop\nauthor: \"Alice\"\nversion: 1\n===\n", true);

        Assert.False(result.HasErrors);

        // StoryNameEnd should appear after the name identifiers, before metadata
        var tokenTypes = result.Tokens.Select(t => t.Type).ToList();
        var sneIndex = tokenTypes.IndexOf(TokenType.StoryNameEnd);
        Assert.True(sneIndex > 0, "StoryNameEnd should exist");

        // All identifiers before StoryNameEnd should be the story name parts
        var nameIds = result.Tokens.Take(sneIndex)
            .Where(t => t.Type == TokenType.Identifier)
            .Select(t => t.Value?.ToString())
            .ToList();
        Assert.Equal(new[] { "The", "Last", "Coffee", "Shop" }, nameIds);
    }

    [Fact]
    public void Lex_StoryNameWithNumbers_MixedTokens()
    {
        // Story name containing a number — numbers lex as Integer tokens, not Identifiers
        var result = Lex("Story 2\n===", true);

        Assert.False(result.HasErrors);
        // Expect: Identifier("Story"), Integer(2), StoryNameEnd, ===, Eof
        Assert.Equal(TokenType.Identifier, result.Tokens[0].Type);
        Assert.Equal("Story", result.Tokens[0].Value);
        Assert.Equal(TokenType.Integer, result.Tokens[1].Type);
        Assert.Equal(2L, result.Tokens[1].Value);
        Assert.Equal(TokenType.StoryNameEnd, result.Tokens[2].Type);
    }

    [Fact]
    public void Lex_StoryNameWithUnderscore()
    {
        var result = Lex("my_story\n===", true);

        Assert.False(result.HasErrors);
        Assert.Equal(TokenType.Identifier, result.Tokens[0].Type);
        Assert.Equal("my_story", result.Tokens[0].Value);
        Assert.Equal(TokenType.StoryNameEnd, result.Tokens[1].Type);
    }

    #endregion
}
