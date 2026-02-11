using Zoh.Runtime.Lexing;

namespace Zoh.Tests.Lexing;

/// <summary>
/// Comprehensive spec-compliant tests for the ZOH lexer.
/// Tests are derived directly from the ZOH specification.
/// </summary>
public class LexerSpecComplianceTests
{
    private static LexResult Lex(string source) => new Lexer(source, false).Tokenize();
    private static Token First(string source) => Lex(source).Tokens[0];

    #region String Literals (spec lines 293-308)

    [Fact]
    public void String_SingleQuotes()
    {
        var token = First("'hello'");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello", token.Value);
    }

    [Fact]
    public void String_DoubleQuotes()
    {
        var token = First("\"hello\"");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello", token.Value);
    }

    [Fact]
    public void String_SingleQuotesCanContainDoubleQuotes()
    {
        // Spec: ' can be used to enclose strings that contain "
        var token = First("'He said \"hello\"'");
        Assert.Equal("He said \"hello\"", token.Value);
    }

    [Fact]
    public void String_DoubleQuotesCanContainSingleQuotes()
    {
        // Spec: " can be used to enclose strings that contain '
        var token = First("\"It's fine\"");
        Assert.Equal("It's fine", token.Value);
    }

    [Theory]
    [InlineData("\"hello\\nworld\"", "hello\nworld")]  // \n -> newline
    [InlineData("\"tab\\there\"", "tab\there")]       // \t -> tab
    [InlineData("\"back\\\\slash\"", "back\\slash")]  // \\ -> single backslash
    [InlineData("\"quote\\\"here\"", "quote\"here")]  // \" -> double quote
    [InlineData("'apos\\'here'", "apos'here")]        // \' -> single quote
    public void String_EscapeSequences(string source, string expected)
    {
        // Spec lines 297-299: escape sequences
        var token = First(source);
        Assert.Equal(expected, token.Value);
    }

    [Fact]
    public void String_Multiline_TripleDoubleQuotes()
    {
        // Spec lines 300-308: multiline strings
        var source = "\"\"\"hello\nworld\"\"\"";
        var token = First(source);
        Assert.Equal(TokenType.MultilineString, token.Type);
        Assert.Equal("hello\nworld", token.Value);
    }

    [Fact]
    public void String_Multiline_TripleSingleQuotes()
    {
        var source = "'''hello\nworld'''";
        var token = First(source);
        Assert.Equal(TokenType.MultilineString, token.Type);
        Assert.Equal("hello\nworld", token.Value);
    }

    #endregion

    #region Numbers (spec lines 309-316)

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("42", 42L)]
    [InlineData("-123", -123L)]
    [InlineData("9223372036854775807", 9223372036854775807L)]  // Max int64
    public void Integer_64Bit(string source, long expected)
    {
        // Spec lines 309-312: integers are always 64-bit signed
        var token = First(source);
        Assert.Equal(TokenType.Integer, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("-2.5", -2.5)]
    [InlineData("0.0", 0.0)]
    public void Double_64Bit(string source, double expected)
    {
        // Spec lines 313-316: doubles are always 64-bit
        var token = First(source);
        Assert.Equal(TokenType.Double, token.Type);
        Assert.Equal(expected, token.Value);
    }

    #endregion

    #region Booleans (spec lines 317-319)

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("False", false)]
    public void Boolean_CaseInsensitive(string source, bool expected)
    {
        // Spec lines 317-319: booleans are case insensitive
        var token = First(source);
        Assert.Equal(expected, token.Value);
    }

    #endregion

    #region Channels (spec lines 326-331)

    [Theory]
    [InlineData("<chan>", "chan")]
    [InlineData("<my_channel>", "my_channel")]
    [InlineData("<stop_idle>", "stop_idle")]
    [InlineData("<ending>", "ending")]
    public void Channel_NoWhitespace(string source, string expected)
    {
        // Spec line 331: No white space is allowed between < and >
        var token = First(source);
        Assert.Equal(TokenType.Channel, token.Type);
        Assert.Equal(expected, token.Value);
    }

    [Fact]
    public void Channel_WithLeadingWhitespace_NotParsedAsChannel()
    {
        // "< chan>" - space after < should NOT produce Channel token
        var result = Lex("< chan>");
        Assert.DoesNotContain(result.Tokens, t => t.Type == TokenType.Channel);
        // Should produce: LeftAngle, Identifier, RightAngle
        Assert.Equal(TokenType.LeftAngle, result.Tokens[0].Type);
    }

    [Fact]
    public void Channel_WithTrailingWhitespace_NotParsedAsChannel()
    {
        // "<chan >" - space before > should NOT produce Channel token
        var result = Lex("<chan >");
        Assert.DoesNotContain(result.Tokens, t => t.Type == TokenType.Channel);
    }

    [Fact]
    public void Channel_WithInternalWhitespace_NotParsedAsChannel()
    {
        // "<my channel>" - space in name
        var result = Lex("<my channel>");
        // Should NOT be a single Channel token with "my channel"
        Assert.DoesNotContain(result.Tokens, t => t.Type == TokenType.Channel);
    }

    #endregion

    #region Expressions (spec lines 332-334)

    [Theory]
    [InlineData("`*x + 1`", "*x + 1")]
    [InlineData("`*trust > 0`", "*trust > 0")]
    [InlineData("`*trust + 2`", "*trust + 2")]
    [InlineData("`*final_trust < 0`", "*final_trust < 0")]
    [InlineData("`*final_trust >= 3`", "*final_trust >= 3")]
    [InlineData("`true`", "true")]
    public void Expression_Backticks(string source, string expected)
    {
        // Spec lines 332-334: expressions in backticks
        var token = First(source);
        Assert.Equal(TokenType.Expression, token.Type);
        Assert.Equal(expected, token.Value);
    }

    #endregion

    #region References (spec lines 335-338)

    [Fact]
    public void Reference_Asterisk()
    {
        // Spec lines 335-338: references denoted as *variable_name
        var result = Lex("*player_name");
        Assert.Equal(TokenType.Star, result.Tokens[0].Type);
        Assert.Equal(TokenType.Identifier, result.Tokens[1].Type);
        Assert.Equal("player_name", result.Tokens[1].Value);
    }

    #endregion

    #region Verb Syntax (spec lines 376-397)

    [Fact]
    public void Verb_StandardForm()
    {
        // /verb [attr] param1, param2;
        var result = Lex("/set [scope:story] \"name\", \"value\";");
        Assert.False(result.HasErrors);
        Assert.Equal(TokenType.Slash, result.Tokens[0].Type);
        Assert.Equal(TokenType.Identifier, result.Tokens[1].Type);
    }

    [Fact]
    public void Verb_BlockFormStart()
    {
        // Block form starts with additional /
        var result = Lex("/verb/");
        Assert.Equal(TokenType.Slash, result.Tokens[0].Type);
        Assert.Equal(TokenType.Identifier, result.Tokens[1].Type);
        Assert.Equal(TokenType.Slash, result.Tokens[2].Type);
    }

    [Fact]
    public void Verb_BlockFormEnd()
    {
        // Block form ends with /;
        var result = Lex("/;");
        Assert.Equal(TokenType.SlashSemicolon, result.Tokens[0].Type);
    }

    #endregion

    #region Comments (from lexer spec)

    [Fact]
    public void Comment_InlineDoesNotInterfere()
    {
        // :: comments should not produce tokens
        var result = Lex("foo :: this is a comment");
        Assert.Equal(2, result.Tokens.Length); // foo + Eof
        Assert.Equal("foo", result.Tokens[0].Value);
    }

    [Fact]
    public void Comment_BlockDoesNotInterfere()
    {
        // ::: block comments :::
        var result = Lex("before ::: block comment ::: after");
        Assert.Equal(3, result.Tokens.Length); // before, after, Eof
        Assert.Equal("before", result.Tokens[0].Value);
        Assert.Equal("after", result.Tokens[1].Value);
    }

    [Fact]
    public void Comment_MultilineBlock()
    {
        var result = Lex("start :::\nmulti\nline\ncomment\n::: end");
        Assert.Equal(3, result.Tokens.Length);
        Assert.Equal("start", result.Tokens[0].Value);
        Assert.Equal("end", result.Tokens[1].Value);
    }

    #region Slash Quote Sugar (spec line 676)

    [Fact]
    public void Sugar_SlashQuote_DoubleQuote()
    {
        var result = Lex("/\"Hello\"");
        Assert.Equal(TokenType.SlashQuote, result.Tokens[0].Type);
        Assert.Equal(TokenType.String, result.Tokens[1].Type);
        Assert.Equal("Hello", result.Tokens[1].Value);
    }

    [Fact]
    public void Sugar_SlashQuote_SingleQuote()
    {
        var result = Lex("/'Hello'");
        Assert.Equal(TokenType.SlashQuote, result.Tokens[0].Type);
        Assert.Equal(TokenType.String, result.Tokens[1].Type);
        Assert.Equal("Hello", result.Tokens[1].Value);
    }

    [Fact]
    public void Sugar_SlashQuote_WithInterpolation()
    {
        // Lexer just sees it as SlashQuote token followed by String token.
        // Interpolation parsing happens at Parser level or runtime evaluation level, not Lexer level (inside the string token).
        // Wait, Lexer.cs ScanString just reads characters.
        var result = Lex("/\"Value: ${*x}\"");
        Assert.False(result.HasErrors);
        Assert.Equal(TokenType.SlashQuote, result.Tokens[0].Type);
        Assert.Equal(TokenType.String, result.Tokens[1].Type);
        Assert.Equal("Value: ${*x}", result.Tokens[1].Value);
    }

    #endregion

    #region Dollar Special Forms (spec lines 651-656, 684-691)

    [Fact]
    public void Sugar_DollarParen()
    {
        var result = Lex("$(expr)");
        Assert.Equal(TokenType.DollarParen, result.Tokens[0].Type);
        Assert.Equal(TokenType.Identifier, result.Tokens[1].Type);
        Assert.Equal("expr", result.Tokens[1].Value);
    }

    [Fact]
    public void Sugar_DollarHashParen()
    {
        // $#(*var) - count
        var result = Lex("$#(*var)");
        Assert.Equal(TokenType.DollarHashParen, result.Tokens[0].Type);
        Assert.Equal(TokenType.Star, result.Tokens[1].Type);
    }

    [Fact]
    public void Sugar_DollarQuestionParen()
    {
        // $?(*cond) - any/conditional
        var result = Lex("$?(cond)");
        Assert.Equal(TokenType.DollarQuestionParen, result.Tokens[0].Type);
    }

    [Fact]
    public void Sugar_DollarQuestionParen_Ternary()
    {
        // $?(*a ? *b : *c)
        var result = Lex("$?(*a ? *b : *c)");
        Assert.False(result.HasErrors);
        Assert.Equal(TokenType.DollarQuestionParen, result.Tokens[0].Type);
    }

    #endregion

    #endregion

    #region Sugar Syntax (spec examples)

    [Fact]
    public void Sugar_SetVariable()
    {
        // *var <- value; (spec line 46)
        var result = Lex("*player_name <- \"stranger\";");
        Assert.Contains(result.Tokens, t => t.Type == TokenType.Star);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.ArrowLeft);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.String);
    }

    [Fact]
    public void Sugar_EvalExpression()
    {
        // /`expr`; (spec line 79)
        var result = Lex("/`*trust + 1`;");
        Assert.Equal(TokenType.SlashBacktick, result.Tokens[0].Type);
        Assert.Equal(TokenType.Expression, result.Tokens[1].Type);
    }

    [Fact]
    public void Sugar_Jump()
    {
        // ====> @label; (spec line 56)
        var result = Lex("====> @first_approach;");
        Assert.Equal(TokenType.Jump, result.Tokens[0].Type);
        Assert.Equal(TokenType.At, result.Tokens[1].Type);
    }

    [Fact]
    public void Sugar_Fork()
    {
        // ====+ @label; (spec line 50)
        var result = Lex("====+ @cafe_atmosphere;");
        Assert.Equal(TokenType.Fork, result.Tokens[0].Type);
        Assert.Equal(TokenType.At, result.Tokens[1].Type);
    }

    [Fact]
    public void Sugar_Call()
    {
        // <===+ @label; 
        var result = Lex("<===+ @subroutine;");
        Assert.Equal(TokenType.Call, result.Tokens[0].Type);
    }

    [Fact]
    public void Sugar_Capture()
    {
        // -> *var; (spec line 64, 92)
        var result = Lex("-> *choice;");
        Assert.Equal(TokenType.ArrowRight, result.Tokens[0].Type);
        Assert.Equal(TokenType.Star, result.Tokens[1].Type);
    }

    #endregion

    #region Story Header (spec lines 243-264)

    [Fact]
    public void StoryHeader_Separator()
    {
        // === marks end of header (spec line 245)
        var result = Lex("Story Name\n===\n/set \"x\", 1;");
        Assert.Contains(result.Tokens, t => t.Type == TokenType.StorySeparator);
    }

    [Fact]
    public void StoryHeader_WithMetadata()
    {
        // Metadata before === (spec lines 258-263)
        var source = "Story\nmeta_key: \"value\";\n===";
        var result = Lex(source);
        Assert.False(result.HasErrors);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.StorySeparator);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_EmptyString()
    {
        var token = First("\"\"");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("", token.Value);
    }

    [Fact]
    public void EdgeCase_ZeroInteger()
    {
        var token = First("0");
        Assert.Equal(TokenType.Integer, token.Type);
        Assert.Equal(0L, token.Value);
    }

    [Fact]
    public void EdgeCase_NothingLiteral()
    {
        // ? represents nothing (spec line 292)
        var token = First("?");
        Assert.Equal(TokenType.Nothing, token.Type);
    }

    [Fact]
    public void EdgeCase_ConsecutiveOperators()
    {
        // Should not confuse adjacent operators
        var result = Lex("*x<-*y;");
        Assert.Contains(result.Tokens, t => t.Type == TokenType.ArrowLeft);
    }

    [Fact]
    public void EdgeCase_NestedBrackets()
    {
        var result = Lex("[[1, 2], [3, 4]]");
        Assert.False(result.HasErrors);
        Assert.Equal(14, result.Tokens.Length); // [,[,1,,,2,],,,[ etc
    }

    [Fact]
    public void EdgeCase_MapLiteral()
    {
        // {key: value} (spec line 322-323)
        var result = Lex("{\"a\": 1, \"b\": 2}");
        Assert.False(result.HasErrors);
        Assert.Equal(TokenType.LeftBrace, result.Tokens[0].Type);
    }

    [Fact]
    public void EdgeCase_NamespacedVerb()
    {
        // core.set (spec lines 266-273)
        var result = Lex("/core.set \"var\";");
        // Note: lexer doesn't parse namespaces, it just tokenizes
        Assert.Equal(TokenType.Slash, result.Tokens[0].Type);
    }

    #endregion

    #region Real Examples from Spec

    [Fact]
    public void RealExample_ShowVerb()
    {
        // spec line 43
        var result = Lex("/show [id: \"bg\"] [fade: 1.5] \"cafe_night.png\";");
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void RealExample_PlayVerb()
    {
        // spec line 44
        var result = Lex("/play [id: \"ambience\"] [loops: -1] \"rain_and_jazz.ogg\";");
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void RealExample_ConverseWithAttribute()
    {
        // spec line 53
        var result = Lex("/converse [By: \"Narrator\"] \"11:47 PM. The cafe closes at midnight.\";");
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void RealExample_ChooseBlock()
    {
        // spec lines 59-64
        var source = @"/choose/
    prompt: ""The rain picks up outside.""
    true  ""Sit across from her""         ""sit""
/; -> *choice;";
        var result = Lex(source);
        Assert.False(result.HasErrors);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.SlashSemicolon);
    }

    [Fact]
    public void RealExample_PushChannel()
    {
        // spec line 83
        var result = Lex("/push <stop_idle>, true;");
        Assert.False(result.HasErrors);
        Assert.Contains(result.Tokens, t => t.Type == TokenType.Channel);
    }

    [Fact]
    public void RealExample_SleepWithRand()
    {
        // spec line 139
        var result = Lex("/sleep /rand 5.0, 8.0;;");
        Assert.False(result.HasErrors);
    }

    #endregion
}
