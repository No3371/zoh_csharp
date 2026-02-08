using System.Collections.Immutable;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing.Ast;
using System.Text;

namespace Zoh.Runtime.Parsing;

/// <summary>
/// Parses ZOH tokens into an AST.
/// </summary>
public sealed class Parser
{
    private readonly ImmutableArray<Token> _tokens;
    private int _current;
    private readonly List<ParseError> _errors = [];

    public Parser(ImmutableArray<Token> tokens) => _tokens = tokens;

    public ParseResult Parse()
    {
        try
        {
            var story = ParseStory();
            return new ParseResult(story, [.. _errors]);
        }
        catch (ParseException ex)
        {
            _errors.Add(new ParseError(ex.Position, ex.Message));
            return new ParseResult(null, [.. _errors]);
        }
    }

    #region Token Navigation

    private bool IsAtEnd => Current.Type == TokenType.Eof;
    private Token Current => _tokens[_current];
    private Token Previous => _tokens[_current - 1];

    private Token Peek(int offset = 1) =>
        _current + offset < _tokens.Length ? _tokens[_current + offset] : _tokens[^1];

    private Token Advance()
    {
        if (!IsAtEnd) _current++;
        return Previous;
    }

    private bool Check(TokenType type) => Current.Type == type;

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }
        return false;
    }

    private string ParseNamespacedIdentifier(string errorMessage)
    {
        var sb = new StringBuilder();
        sb.Append(Consume(TokenType.Identifier, errorMessage).Lexeme);

        while (Match(TokenType.Dot))
        {
            sb.Append('.');
            sb.Append(Consume(TokenType.Identifier, "Expected identifier after '.'").Lexeme);
        }
        return sb.ToString();
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw Error(Current.Start, message);
    }

    private ParseException Error(TextPosition pos, string message)
    {
        _errors.Add(new ParseError(pos, message));
        return new ParseException(pos, message);
    }

    #endregion

    #region Story Parsing

    private StoryAst ParseStory()
    {
        // Parse optional story name and metadata before ===
        var name = "";
        var metadata = ImmutableDictionary<string, ValueAst>.Empty;

        // Check if there's a story header (name before ===)
        if (!Check(TokenType.StorySeparator) && !Check(TokenType.Slash) &&
            !Check(TokenType.At) && !Check(TokenType.Star) &&
            !Check(TokenType.ArrowLeft) && !Check(TokenType.ArrowRight) &&
            !Check(TokenType.Jump) && !Check(TokenType.Fork) && !Check(TokenType.Call))
        {
            // Try to parse story name
            if (Check(TokenType.Identifier) || Check(TokenType.String))
            {
                var nameToken = Advance();
                name = nameToken.Value?.ToString() ?? nameToken.Lexeme;
            }

            // Parse metadata until we hit === or a statement
            while (!IsAtEnd && !Check(TokenType.StorySeparator) &&
                   !Check(TokenType.Slash) && !Check(TokenType.At) &&
                   !Check(TokenType.Star) && !Check(TokenType.Jump) &&
                   !Check(TokenType.Fork) && !Check(TokenType.Call) &&
                   !Check(TokenType.ArrowLeft) && !Check(TokenType.ArrowRight) &&
                   !Check(TokenType.SlashBacktick) && !Check(TokenType.SlashQuote) &&
                   !Check(TokenType.Hash))
            {
                if (Check(TokenType.Identifier))
                {
                    var key = Advance().Lexeme;
                    if (Match(TokenType.Colon))
                    {
                        var value = ParseValue();
                        Match(TokenType.Semicolon); // optional
                        metadata = metadata.SetItem(key, value);
                    }
                }
                else
                {
                    Advance(); // Skip unknown token in header
                }
            }
        }

        // Consume story separator if present
        Match(TokenType.StorySeparator);

        // Parse statements
        var statements = ImmutableArray.CreateBuilder<StatementAst>();
        var labels = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.OrdinalIgnoreCase);

        while (!IsAtEnd)
        {
            var stmt = ParseStatement();
            if (stmt is StatementAst.Label label)
            {
                if (labels.ContainsKey(label.Name))
                    _errors.Add(new ParseError(label.Position, $"Duplicate label: @{label.Name}"));
                else
                    labels[label.Name] = statements.Count;
            }
            statements.Add(stmt);
        }

        return new StoryAst(name, metadata, statements.ToImmutable(), labels.ToImmutable());
    }

    #endregion

    #region Statement Parsing

    private StatementAst ParseStatement()
    {
        var pos = Current.Start;

        return Current.Type switch
        {
            TokenType.At => ParseLabel(),
            TokenType.Slash => ParseVerbCall(),
            TokenType.Star => ParseSetSugar(),
            TokenType.ArrowLeft => ParseGetSugar(),
            TokenType.ArrowRight => ParseCaptureSugar(),
            TokenType.Jump => ParseJumpSugar(),
            TokenType.Fork => ParseForkSugar(),
            TokenType.Call => ParseCallSugar(),
            TokenType.SlashBacktick => ParseEvalSugar(),
            TokenType.Hash => ParsePreprocessorOrFlag(),
            TokenType.SlashQuote => ParseInterpolateSugar(),
            TokenType.Semicolon => ParseEmptyStatement(),
            _ => throw Error(pos, $"Unexpected token: {Current.Type}")
        };
    }

    private StatementAst.Label ParseLabel()
    {
        var pos = Current.Start;
        Consume(TokenType.At, "Expected '@'");
        var name = Consume(TokenType.Identifier, "Expected label name").Lexeme;

        var paramsBuilder = ImmutableArray.CreateBuilder<StatementAst.ContractParam>();
        while (Match(TokenType.Star))
        {
            var paramPos = Previous.Start;
            var paramName = Consume(TokenType.Identifier, "Expected parameter name after '*'").Lexeme;
            string? paramType = null;
            if (Match(TokenType.Colon))
            {
                paramType = Consume(TokenType.Identifier, "Expected type name after ':'").Lexeme;
            }
            paramsBuilder.Add(new StatementAst.ContractParam(paramName, paramType, paramPos));
        }

        return new StatementAst.Label(name, paramsBuilder.ToImmutable(), pos);
    }

    private StatementAst.VerbCall ParseVerbCall()
    {
        var pos = Current.Start;
        Consume(TokenType.Slash, "Expected '/'");

        // Parse verb name (possibly with namespace)
        // Parse verb name (namespaced)
        var fullId = ParseNamespacedIdentifier("Expected verb name");
        string? ns = null;
        string name = fullId;

        var lastDot = fullId.LastIndexOf('.');
        if (lastDot != -1)
        {
            ns = fullId[..lastDot];
            name = fullId[(lastDot + 1)..];
        }

        // Check if block form: "slash" must be IMMEDIATELY after "name" (no whitespace).
        var isBlock = false;
        if (Current.Type == TokenType.Slash)
        {
            // Adjacency check: name end pos == slash start pos
            var prevEnd = Previous.Start.Offset + Previous.Lexeme.Length;
            if (Current.Start.Offset == prevEnd)
            {
                Advance(); // consume the second /
                isBlock = true;
            }
        }

        // Parse attributes
        var attrs = ParseAttributes();

        // Parse parameters - block form uses space/newline delimiters, standard uses commas
        var (named, unnamed) = ParseParameters(isBlock);

        // Consume terminator
        if (isBlock)
        {
            Consume(TokenType.SlashSemicolon, "Expected '/;' to close block verb");
        }
        else
        {
            Consume(TokenType.Semicolon, "Expected ';'");
        }



        // Block form content is already in params, no separate block body
        var call = new VerbCallAst(ns, name, isBlock, attrs, named, unnamed, pos);

        // If there was a capture, we need to return a sequence or handle it
        // For now, we'll just return the verb call (capture handling can be added later)
        return new StatementAst.VerbCall(call);
    }

    private ImmutableArray<StatementAst> ParseBlockBody()
    {
        var stmts = ImmutableArray.CreateBuilder<StatementAst>();
        while (!Check(TokenType.SlashSemicolon) && !IsAtEnd)
        {
            stmts.Add(ParseStatement());
        }
        return stmts.ToImmutable();
    }

    private ImmutableArray<AttributeAst> ParseAttributes()
    {
        var attrs = ImmutableArray.CreateBuilder<AttributeAst>();

        while (Match(TokenType.LeftBracket))
        {
            var pos = Previous.Start;
            var name = ParseNamespacedIdentifier("Expected attribute name");

            ValueAst? value = null;
            if (Match(TokenType.Colon))
            {
                value = ParseValue();
            }

            Consume(TokenType.RightBracket, "Expected ']'");
            attrs.Add(new AttributeAst(name, value, pos));
        }

        return attrs.ToImmutable();
    }

    private (ImmutableDictionary<string, ValueAst> Named, ImmutableArray<ValueAst> Unnamed) ParseParameters(bool isBlock = false)
    {
        var named = ImmutableDictionary.CreateBuilder<string, ValueAst>();
        var unnamed = ImmutableArray.CreateBuilder<ValueAst>();

        while (!IsAtEnd && !Check(TokenType.Semicolon) && !Check(TokenType.SlashSemicolon))
        {
            // Check if this is a named parameter (identifier followed by colon)
            if (Check(TokenType.Identifier) && Peek().Type == TokenType.Colon)
            {
                var paramName = Advance().Lexeme;
                Advance(); // consume colon
                var value = ParseValue();
                named[paramName] = value;
            }
            else
            {
                var value = ParseValue();
                unnamed.Add(value);
            }

            // In block form, spaces/newlines are delimiters (comma optional)
            // In standard form, commas are required between params
            if (!isBlock)
            {
                // Only consume comma if not at terminator
                if (!Check(TokenType.Semicolon) && !Check(TokenType.SlashSemicolon))
                {
                    Consume(TokenType.Comma, "Expected comma between parameters");
                }
            }
            else
            {
                // Block form: comma is optional, space/newline suffices
                Match(TokenType.Comma);
            }
        }

        return (named.ToImmutable(), unnamed.ToImmutable());
    }

    #endregion

    #region Value Parsing

    private ValueAst ParseValue()
    {
        var pos = Current.Start;

        if (Match(TokenType.Nothing)) return new ValueAst.Nothing();
        if (Match(TokenType.True)) return new ValueAst.Boolean(true);
        if (Match(TokenType.False)) return new ValueAst.Boolean(false);

        if (Match(TokenType.Integer))
            return new ValueAst.Integer((long)Previous.Value!);

        if (Match(TokenType.Double))
            return new ValueAst.Double((double)Previous.Value!);

        if (Match(TokenType.String, TokenType.MultilineString))
            return new ValueAst.String((string)Previous.Value!);

        if (Match(TokenType.Expression))
            return new ValueAst.Expression((string)Previous.Value!, Previous.Start);

        if (Match(TokenType.Channel))
            return new ValueAst.Channel((string)Previous.Value!);

        if (Match(TokenType.Star))
            return ParseReference();

        if (Match(TokenType.LeftBracket))
            return ParseList();

        if (Match(TokenType.LeftBrace))
            return ParseMap();

        if (Match(TokenType.Slash))
            return ParseVerbValue();

        // Handle capture sugar (-> *var;) as a verb value
        if (Match(TokenType.ArrowRight))
            return ParseCaptureSugarAsValue();

        // Bare identifiers become string values (e.g., [scope:story])
        if (Match(TokenType.Identifier))
            return new ValueAst.String(Previous.Lexeme);

        throw Error(pos, $"Expected value, got {Current.Type}");
    }

    private ValueAst.Reference ParseReference()
    {
        var name = Consume(TokenType.Identifier, "Expected variable name after '*'").Lexeme;

        var path = ImmutableArray.CreateBuilder<ValueAst>();

        while (Check(TokenType.LeftBracket))
        {
            Consume(TokenType.LeftBracket, "Expected '['");
            var index = ParseValue();
            Consume(TokenType.RightBracket, "Expected ']' after index");
            path.Add(index);
        }

        return new ValueAst.Reference(name, path.ToImmutable());
    }

    private ValueAst.List ParseList()
    {
        var elements = ImmutableArray.CreateBuilder<ValueAst>();

        while (!Check(TokenType.RightBracket) && !IsAtEnd)
        {
            elements.Add(ParseValue());
            Match(TokenType.Comma);
        }

        Consume(TokenType.RightBracket, "Expected ']' to close list");
        return new ValueAst.List(elements.ToImmutable());
    }

    private ValueAst.Map ParseMap()
    {
        var entries = ImmutableArray.CreateBuilder<(ValueAst Key, ValueAst Value)>();

        while (!Check(TokenType.RightBrace) && !IsAtEnd)
        {
            var key = ParseValue();
            Consume(TokenType.Colon, "Expected ':' after map key");
            var value = ParseValue();
            entries.Add((key, value));
            Match(TokenType.Comma);
        }

        Consume(TokenType.RightBrace, "Expected '}' to close map");
        return new ValueAst.Map(entries.ToImmutable());
    }

    private ValueAst.Verb ParseVerbValue()
    {
        // We already consumed the '/'
        var pos = Previous.Start;

        var fullId = ParseNamespacedIdentifier("Expected verb name");

        string? ns = null;
        string name = fullId;

        var lastDot = fullId.LastIndexOf('.');
        if (lastDot != -1)
        {
            ns = fullId[..lastDot];
            name = fullId[(lastDot + 1)..];
        }

        // Check if block form: "slash" must be IMMEDIATELY after "name" (no whitespace).
        var isBlock = false;
        if (Current.Type == TokenType.Slash)
        {
            var prevEnd = Previous.Start.Offset + Previous.Lexeme.Length;
            if (Current.Start.Offset == prevEnd)
            {
                Advance(); // consume the second /
                isBlock = true;
            }
        }

        var attrs = ParseAttributes();
        var (named, unnamed) = ParseParameters(isBlock);



        if (isBlock)
        {
            Consume(TokenType.SlashSemicolon, "Expected '/;'");
        }
        else
        {
            Consume(TokenType.Semicolon, "Expected ';' after verb value");
        }

        return new ValueAst.Verb(new VerbCallAst(ns, name, isBlock, attrs, named, unnamed, pos));
    }

    #endregion

    #region Sugar Transformations

    private StatementAst ParseSetSugar()
    {
        // *var <- value; becomes /set "var", value;
        var pos = Current.Start;
        Consume(TokenType.Star, "Expected '*'");

        // Use ParseReference to handle simple or nested references
        var refNode = ParseReference();

        var attrs = ParseAttributes();

        ValueAst? value = null;
        if (Match(TokenType.ArrowLeft))
        {
            value = ParseValue();
        }

        Consume(TokenType.Semicolon, "Expected ';'");

        var unnamed = ImmutableArray.CreateBuilder<ValueAst>();
        unnamed.Add(refNode);

        if (value is not null) unnamed.Add(value);

        return new StatementAst.VerbCall(new VerbCallAst(
            "core", "set", false, attrs,
            ImmutableDictionary<string, ValueAst>.Empty,
            unnamed.ToImmutable(), pos));
    }

    private StatementAst ParseGetSugar()
    {
        // <- *var; becomes /get "var"; (Note: typically goes to last return value)
        var pos = Current.Start;
        Consume(TokenType.ArrowLeft, "Expected '<-'");
        Consume(TokenType.Star, "Expected '*'");

        var refNode = ParseReference();

        Consume(TokenType.Semicolon, "Expected ';'");

        return new StatementAst.VerbCall(new VerbCallAst(
            "core", "get", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [refNode], pos));
    }

    private StatementAst ParseCaptureSugar()
    {
        // -> *var; becomes /capture "var";
        var pos = Current.Start;
        Consume(TokenType.ArrowRight, "Expected '->'");
        return ParseCaptureSugarInline();
    }

    private StatementAst ParseCaptureSugarInline()
    {
        var pos = Previous.Start; // Start at ->
        if (Current.Type != TokenType.Star)
            throw new Exception($"Expected '*' at {Current.Start}");

        Consume(TokenType.Star, "Expected '*'");

        var val = ParseReference();

        var stmt = new StatementAst.VerbCall(new VerbCallAst(
            "core", "capture", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [val],
            pos));

        // Check chaining
        if (Check(TokenType.ArrowRight))
        {
            // If chained, return it? No, ParseBlockBody loops.
            // We do NOT consume next token.
            // We do NOT consume semicolon.
            // Console.WriteLine("DEBUG PARSER: Capture chaining dectected, not consuming semicolon");
        }
        else
        {
            // Console.WriteLine("DEBUG PARSER: Capture end diff, consuming semicolon");
            Consume(TokenType.Semicolon, "Expected ';'");
        }
        return stmt;
    }

    /// <summary>
    /// Parses capture sugar (-> *var;) as a verb VALUE for use in block parameters.
    /// ArrowRight token is already consumed.
    /// </summary>
    private ValueAst.Verb ParseCaptureSugarAsValue()
    {
        var pos = Previous.Start; // ArrowRight was already consumed
        Consume(TokenType.Star, "Expected '*' after '->'");
        var name = Consume(TokenType.Identifier, "Expected variable name").Lexeme;

        ValueAst? index = null;
        if (Match(TokenType.LeftBracket))
        {
            index = ParseValue();
            Consume(TokenType.RightBracket, "Expected ']'");
        }

        var refVal = new ValueAst.Reference(name, index != null ? ImmutableArray.Create(index) : ImmutableArray<ValueAst>.Empty);

        // Consume the semicolon that terminates this capture value
        Consume(TokenType.Semicolon, "Expected ';'");

        return new ValueAst.Verb(new VerbCallAst(
            "core", "capture", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [refVal],
            pos));
    }

    private StatementAst ParseJumpSugar()
    {
        // ====> @label; becomes /jump ?, "label";
        // ====> @story:label; becomes /jump "story", "label";
        var pos = Current.Start;
        Consume(TokenType.Jump, "Expected '====>'");
        Consume(TokenType.At, "Expected '@'");

        var firstId = Consume(TokenType.Identifier, "Expected label or story name").Lexeme;
        ValueAst storyVal;
        ValueAst labelVal;

        if (Match(TokenType.Colon))
        {
            // @story:label
            var labelName = Consume(TokenType.Identifier, "Expected label name after ':'").Lexeme;
            storyVal = new ValueAst.String(firstId);
            labelVal = new ValueAst.String(labelName);
        }
        else
        {
            // @label (story is nothing)
            storyVal = new ValueAst.Nothing();
            labelVal = new ValueAst.String(firstId);
        }

        var unnamed = ImmutableArray.CreateBuilder<ValueAst>();
        unnamed.Add(storyVal);
        unnamed.Add(labelVal);

        // Parse trailing args (vars)
        while (!Check(TokenType.Semicolon) && !IsAtEnd)
        {
            unnamed.Add(ParseValue());
            Match(TokenType.Comma); // Optional comma
        }

        Consume(TokenType.Semicolon, "Expected ';'");

        return new StatementAst.VerbCall(new VerbCallAst(
            "core", "jump", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            unnamed.ToImmutable(), pos));
    }

    private StatementAst ParseForkSugar()
    {
        // ====+ @label args; becomes /fork ?, "label", args;
        // ====+ @story:label args; becomes /fork "story", "label", args;
        var pos = Current.Start;
        Consume(TokenType.Fork, "Expected '====+'");
        var attrs = ParseAttributes();
        Consume(TokenType.At, "Expected '@'");

        var firstId = Consume(TokenType.Identifier, "Expected label or story name").Lexeme;
        ValueAst storyVal;
        ValueAst labelVal;

        if (Match(TokenType.Colon))
        {
            var labelName = Consume(TokenType.Identifier, "Expected label name after ':'").Lexeme;
            storyVal = new ValueAst.String(firstId);
            labelVal = new ValueAst.String(labelName);
        }
        else
        {
            storyVal = new ValueAst.Nothing();
            labelVal = new ValueAst.String(firstId);
        }

        var unnamed = ImmutableArray.CreateBuilder<ValueAst>();
        unnamed.Add(storyVal);
        unnamed.Add(labelVal);

        // Parse optional arguments
        while (!Check(TokenType.Semicolon) && !IsAtEnd)
        {
            unnamed.Add(ParseValue());
            Match(TokenType.Comma);
        }

        Consume(TokenType.Semicolon, "Expected ';'");

        return new StatementAst.VerbCall(new VerbCallAst(
            "core", "fork", false, attrs,
            ImmutableDictionary<string, ValueAst>.Empty,
            unnamed.ToImmutable(), pos));
    }

    private StatementAst ParseCallSugar()
    {
        // <===+ @label args; becomes /call ?, "label", args;
        // <===+ @story:label args; becomes /call "story", "label", args;
        var pos = Current.Start;
        Consume(TokenType.Call, "Expected '<===+'");
        var attrs = ParseAttributes();
        Consume(TokenType.At, "Expected '@'");

        var firstId = Consume(TokenType.Identifier, "Expected label or story name").Lexeme;
        ValueAst storyVal;
        ValueAst labelVal;

        if (Match(TokenType.Colon))
        {
            var labelName = Consume(TokenType.Identifier, "Expected label name after ':'").Lexeme;
            storyVal = new ValueAst.String(firstId);
            labelVal = new ValueAst.String(labelName);
        }
        else
        {
            storyVal = new ValueAst.Nothing();
            labelVal = new ValueAst.String(firstId);
        }

        var unnamed = ImmutableArray.CreateBuilder<ValueAst>();
        unnamed.Add(storyVal);
        unnamed.Add(labelVal);

        while (!Check(TokenType.Semicolon) && !Check(TokenType.ArrowRight) && !IsAtEnd)
        {
            unnamed.Add(ParseValue());
            Match(TokenType.Comma);
        }

        // Optional capture
        StatementAst? capture = null;
        if (Match(TokenType.ArrowRight))
        {
            capture = ParseCaptureSugarInline();
        }
        else
        {
            Consume(TokenType.Semicolon, "Expected ';'");
        }

        return new StatementAst.VerbCall(new VerbCallAst(
            "core", "call", false, attrs,
            ImmutableDictionary<string, ValueAst>.Empty,
            unnamed.ToImmutable(), pos));
    }

    private StatementAst ParseEvalSugar()
    {
        // /`expr`; becomes /evaluate `expr`;
        var pos = Current.Start;
        Consume(TokenType.SlashBacktick, "Expected '/`'");
        var exprToken = Consume(TokenType.Expression, "Expected expression");

        // Optional capture
        if (Match(TokenType.ArrowRight))
        {
            // TODO: Handle capture after eval
        }

        Consume(TokenType.Semicolon, "Expected ';'");

        return new StatementAst.VerbCall(new VerbCallAst(
            "core", "evaluate", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [new ValueAst.Expression((string)exprToken.Value!, exprToken.Start)], pos));
    }

    private StatementAst ParseInterpolateSugar()
    {
        // /"string"; becomes /interpolate "string";
        var pos = Current.Start;
        Consume(TokenType.SlashQuote, "Expected '/' before string"); // Should match /
                                                                     // Lexer emits DollarQuote then String.
                                                                     // Wait, Lexer emits DollarQuote ($) and String token.
                                                                     // My Lexer change emits DollarQuote then String token.
                                                                     // So here we consume DollarQuote. Next is String.
        var strToken = Consume(TokenType.String, "Expected string after '$'");

        Consume(TokenType.Semicolon, "Expected ';'");

        return new StatementAst.VerbCall(new VerbCallAst(
            "core", "interpolate", false, [],
            ImmutableDictionary<string, ValueAst>.Empty,
            [new ValueAst.String((string)strToken.Value!)], pos));
    }

    private StatementAst ParseEmptyStatement()
    {
        Advance(); // consume ;
                   // Return a No-Op or handle gracefully.
                   // We can't return null because return type is StatementAst.
                   // We'll return a Sequence with empty list? Or a special mismatch?
                   // Better: ParseStatement loop should handle allowed empty statements?
                   // But ParseStatement returns StatementAst.
                   // Let's return a "Sequence" with 0 items. (Block)
        return new StatementAst.Sequence([], Current.Start);
    }

    private StatementAst ParsePreprocessorOrFlag()
    {
        var pos = Current.Start;
        Consume(TokenType.Hash, "Expected '#'");
        var directive = Consume(TokenType.Identifier, "Expected directive name").Lexeme;

        // All preprocessor directives should be handled in preprocessing phase
        throw Error(pos, $"Unknown directive: #{directive}");
    }

    #endregion
}

/// <summary>
/// Exception thrown during parsing.
/// </summary>
public class ParseException(TextPosition position, string message) : Exception(message)
{
    public TextPosition Position { get; } = position;
}
