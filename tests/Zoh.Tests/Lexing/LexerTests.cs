using Zoh.Runtime.Lexing;

namespace Zoh.Tests.Lexing;

public class LexerTests
{
    [Fact]
    public void Lex_EmptySource_ReturnsEof()
    {
        var result = new Lexer("", false).Tokenize();
        Assert.Single(result.Tokens);
        Assert.Equal(TokenType.Eof, result.Tokens[0].Type);
    }

    [Theory]
    [InlineData(";", TokenType.Semicolon)]
    [InlineData(",", TokenType.Comma)]
    [InlineData(":", TokenType.Colon)]
    [InlineData("@", TokenType.At)]
    [InlineData("*", TokenType.Star)]
    [InlineData("[", TokenType.LeftBracket)]
    [InlineData("]", TokenType.RightBracket)]
    [InlineData("{", TokenType.LeftBrace)]
    [InlineData("}", TokenType.RightBrace)]
    [InlineData("(", TokenType.LeftParen)]
    [InlineData(")", TokenType.RightParen)]
    [InlineData("?", TokenType.Nothing)]
    [InlineData("#", TokenType.Hash)]
    [InlineData(">", TokenType.RightAngle)]
    public void Lex_SingleCharTokens(string source, TokenType expected)
    {
        Assert.Equal(expected, new Lexer(source, false).Tokenize().Tokens[0].Type);
    }

    [Theory]
    [InlineData("/", TokenType.Slash)]
    [InlineData("/;", TokenType.SlashSemicolon)]
    [InlineData("<-", TokenType.ArrowLeft)]
    [InlineData("->", TokenType.ArrowRight)]
    [InlineData("====>", TokenType.Jump)]
    [InlineData("====+", TokenType.Fork)]
    [InlineData("<===+", TokenType.Call)]
    [InlineData("===", TokenType.StorySeparator)]
    public void Lex_MultiCharTokens(string source, TokenType expected)
    {
        Assert.Equal(expected, new Lexer(source, false).Tokenize().Tokens[0].Type);
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("42", 42L)]
    [InlineData("123456789", 123456789L)]
    [InlineData("-5", -5L)]
    public void Lex_Integers(string source, long expected)
    {
        var token = new Lexer(source, false).Tokenize().Tokens[0];
        Assert.Equal(TokenType.Integer, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("0.5", 0.5)]
    [InlineData("-2.5", -2.5)]
    public void Lex_Doubles(string source, double expected)
    {
        var token = new Lexer(source, false).Tokenize().Tokens[0];
        Assert.Equal(TokenType.Double, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("'world'", "world")]
    [InlineData("\"with\\nnewline\"", "with\nnewline")]
    [InlineData("\"with\\\"quote\"", "with\"quote")]
    public void Lex_Strings(string source, string expected)
    {
        var token = new Lexer(source, false).Tokenize().Tokens[0];
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("TRUE", true)]
    [InlineData("False", false)]
    public void Lex_Booleans(string source, bool expected)
    {
        var token = new Lexer(source, false).Tokenize().Tokens[0];
        Assert.Equal(expected ? TokenType.True : TokenType.False, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Theory]
    [InlineData("foo", "foo")]
    [InlineData("_bar", "_bar")]
    [InlineData("test123", "test123")]
    public void Lex_Identifiers(string source, string expected)
    {
        var token = new Lexer(source, false).Tokenize().Tokens[0];
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Theory]
    [InlineData("`*x + 1`", "*x + 1")]
    [InlineData("`(*a && *b)`", "(*a && *b)")]
    public void Lex_Expressions(string source, string expected)
    {
        var token = new Lexer(source, false).Tokenize().Tokens[0];
        Assert.Equal(TokenType.Expression, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Theory]
    [InlineData("<chan>", "chan")]
    [InlineData("<my_channel>", "my_channel")]
    public void Lex_Channels(string source, string expected)
    {
        var token = new Lexer(source, false).Tokenize().Tokens[0];
        Assert.Equal(TokenType.Channel, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Fact]
    public void Lex_SkipsLineComments()
    {
        var result = new Lexer("foo :: this is a comment\nbar", false).Tokenize();
        Assert.Equal(3, result.Tokens.Length); // foo, bar, Eof
        Assert.Equal("foo", result.Tokens[0].Value);
        Assert.Equal("bar", result.Tokens[1].Value);
    }

    [Fact]
    public void Lex_SkipsBlockComments()
    {
        var result = new Lexer("foo ::: block \n comment ::: bar", false).Tokenize();
        Assert.Equal(3, result.Tokens.Length);
        Assert.Equal("foo", result.Tokens[0].Value);
        Assert.Equal("bar", result.Tokens[1].Value);
    }

    [Fact]
    public void Lex_MultilineString()
    {
        var source = "\"\"\"hello\nworld\"\"\"";
        var token = new Lexer(source, false).Tokenize().Tokens[0];
        Assert.Equal(TokenType.MultilineString, token.Type);
        Assert.Equal("hello\nworld", token.Value);
    }

    [Fact]
    public void Lex_ComplexStatement()
    {
        var source = "/set [scope:story] \"name\", *value;";
        var result = new Lexer(source, false).Tokenize();

        var types = result.Tokens.Select(t => t.Type).ToArray();
        Assert.Equal(new[]
        {
            TokenType.Slash,
            TokenType.Identifier,  // set
            TokenType.LeftBracket,
            TokenType.Identifier,  // scope
            TokenType.Colon,
            TokenType.Identifier,  // story
            TokenType.RightBracket,
            TokenType.String,      // "name"
            TokenType.Comma,
            TokenType.Star,
            TokenType.Identifier,  // value
            TokenType.Semicolon,
            TokenType.Eof
        }, types);
    }

    [Fact]
    public void Lex_SugarStatements()
    {
        var source = "*var <- 42; -> *result; ====> @label;";
        var result = new Lexer(source, false).Tokenize();

        Assert.False(result.HasErrors);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.ArrowLeft);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.ArrowRight);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.Jump);
    }

    [Fact]
    public void Lex_PreservesPosition()
    {
        var source = "foo\nbar";
        var result = new Lexer(source, false).Tokenize();

        Assert.Equal(1, result.Tokens[0].Start.Line);
        Assert.Equal(2, result.Tokens[1].Start.Line);
    }

    [Fact]
    public void Scan_Checkpoint_EmitsCheckpointEnd()
    {
        var source = "@main\n";
        var result = new Lexer(source, false).Tokenize();

        Assert.Equal(4, result.Tokens.Length); // At, Identifier, CheckpointEnd, Eof
        Assert.Equal(TokenType.At, result.Tokens[0].Type);
        Assert.Equal(TokenType.Identifier, result.Tokens[1].Type);
        Assert.Equal("main", result.Tokens[1].Value);
        Assert.Equal(TokenType.CheckpointEnd, result.Tokens[2].Type);
        Assert.Equal(TokenType.Eof, result.Tokens[3].Type);
    }

    [Fact]
    public void Lex_MultipleCheckpoints_EmitsTokens()
    {
        var source = "@foo\n@bar";
        var result = new Lexer(source, false).Tokenize();

        Assert.Equal(7, result.Tokens.Length); // At, Id, End, At, Id, End, Eof
        Assert.Equal("foo", result.Tokens[1].Value);
        Assert.Equal(TokenType.CheckpointEnd, result.Tokens[2].Type);
        Assert.Equal("bar", result.Tokens[4].Value);
        Assert.Equal(TokenType.CheckpointEnd, result.Tokens[5].Type);
    }

    [Fact]
    public void Lex_CheckpointAtEof_EmitsCheckpointEnd()
    {
        var source = "@main"; // No newline
        var result = new Lexer(source, false).Tokenize();

        Assert.Equal(4, result.Tokens.Length); // At, Id, End, Eof
        Assert.Equal(TokenType.CheckpointEnd, result.Tokens[2].Type);
    }

    [Fact]
    public void Lex_CheckpointAtEof_EmitsStoryNameEnd()
    {
        var source = "My Story";
        var result = new Lexer(source, true).Tokenize();

        Assert.Equal(4, result.Tokens.Length); // Id, Id, SNE, Eof
        Assert.Equal(TokenType.StoryNameEnd, result.Tokens[2].Type);
    }


}


