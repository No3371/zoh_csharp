using System.Collections.Immutable;
using System.Text;

namespace Zoh.Runtime.Lexing;

/// <summary>
/// Tokenizes ZOH source code into a stream of tokens.
/// </summary>
public sealed class Lexer
{
    private readonly string _source;
    private int _current;
    private TextPosition _position = TextPosition.Start;
    private readonly List<Token> _tokens = [];
    private readonly List<LexError> _errors = [];
    private int _start;
    // Context tracking for virtual tokens
    private bool _inCheckpointDef;
    private bool _isStartOfLine = true;

    private readonly int _initialOffset;

    public Lexer(string source) : this(source, new TextPosition(1, 1, 0))
    {
    }

    public Lexer(string source, TextPosition startPosition)
    {
        _source = source;
        _position = startPosition;
        _initialOffset = startPosition.Offset;
        _start = 0;
        _current = 0;
    }

    public LexResult Tokenize()
    {
        while (!IsAtEnd)
        {
            ScanToken();
        }

        if (_inCheckpointDef)
        {
            AddToken(TokenType.CheckpointEnd, _position);
            _inCheckpointDef = false;
        }

        _tokens.Add(Token.Eof(_position));
        return new LexResult([.. _tokens], [.. _errors]);
    }

    private bool IsAtEnd => _current >= _source.Length;
    private char Current => IsAtEnd ? '\0' : _source[_current];

    private char Peek(int offset = 1) =>
        _current + offset < _source.Length ? _source[_current + offset] : '\0';

    private char Advance()
    {
        var c = Current;
        _current++;
        _position = c == '\n' ? _position.NextLine() : _position.NextColumn();
        return c;
    }

    private bool Match(char expected)
    {
        if (Current != expected) return false;
        Advance();
        return true;
    }

    private bool Match(string expected)
    {
        if (_current + expected.Length > _source.Length) return false;
        if (!_source.AsSpan(_current, expected.Length).SequenceEqual(expected)) return false;
        for (int i = 0; i < expected.Length; i++) Advance();
        return true;
    }

    private bool Check(string expected)
    {
        if (_current + expected.Length > _source.Length) return false;
        return _source.AsSpan(_current, expected.Length).SequenceEqual(expected);
    }

    private void ScanToken()
    {
        SkipWhitespaceAndComments();

        // CheckpointEnd injection
        if (_inCheckpointDef && Current == '\n')
        {
            var pos = _position;
            Advance(); // Consume newline
            AddToken(TokenType.CheckpointEnd, pos);
            _inCheckpointDef = false;
            _isStartOfLine = true;
            return;
        }

        if (IsAtEnd) return;

        var start = _position;
        var startOffset = _current;
        var c = Advance();

        switch (c)
        {
            // Single-char tokens
            case ';': AddToken(TokenType.Semicolon, start); break;
            case ',': AddToken(TokenType.Comma, start); break;
            case '@':
                if (_isStartOfLine) _inCheckpointDef = true;
                AddToken(TokenType.At, start);
                break;
            case '*':
                if (Match('*')) AddToken(TokenType.StarStar, start);
                else AddToken(TokenType.Star, start);
                break;
            case '+': AddToken(TokenType.Plus, start); break;
            case '%': AddToken(TokenType.Percent, start); break;
            case '[': AddToken(TokenType.LeftBracket, start); break;
            case ']': AddToken(TokenType.RightBracket, start); break;
            case '{': AddToken(TokenType.LeftBrace, start); break;
            case '}': AddToken(TokenType.RightBrace, start); break;
            case '(': AddToken(TokenType.LeftParen, start); break;
            case ')': AddToken(TokenType.RightParen, start); break;
            case '?': AddToken(TokenType.Nothing, start); break;
            case '>':
                if (Match('=')) AddToken(TokenType.GreaterEqual, start);
                else AddToken(TokenType.RightAngle, start); // Greater
                break;
            case '!':
                if (Match('=')) AddToken(TokenType.BangEqual, start);
                else AddToken(TokenType.Bang, start);
                break;
            case '&':
                if (Match('&')) AddToken(TokenType.AmpersandAmpersand, start);
                else ReportError(start, "Expected '&' after '&'");
                break;
            case '|':
                if (Match('|')) AddToken(TokenType.PipePipe, start);
                else AddToken(TokenType.Pipe, start);
                break;
            case '#': AddToken(TokenType.Hash, start); break;
            case '.': AddToken(TokenType.Dot, start); break;

            // Multi-char tokens
            case '/':
                if (Match(';'))
                {
                    AddToken(TokenType.SlashSemicolon, start);
                }
                else if (Current == '`')
                {
                    AddToken(TokenType.SlashBacktick, start);
                    // Do not consume the backtick; it will be scanned as an Expression in the next iteration
                }
                else if (Current == '"' || Current == '\'')
                {
                    var quote = Current;
                    AddToken(TokenType.SlashQuote, start);
                    Advance(); // consume the opening quote
                    ScanString(quote, start);
                }
                else
                {
                    AddToken(TokenType.Slash, start);
                }
                break;

            case ':':
                // Could be just colon, or start of comment ::
                if (Current == ':')
                {
                    // Comment - skip to end of line or block
                    SkipComment();
                }
                else
                {
                    AddToken(TokenType.Colon, start);
                }
                break;

            case '<':
                if (Match("===+"))
                    AddToken(TokenType.Call, start);
                else if (Match('-'))
                    AddToken(TokenType.ArrowLeft, start);
                else if (Match('='))
                    AddToken(TokenType.LessEqual, start);
                else if (IsChannelStart())
                    ScanChannel(start);
                else
                    AddToken(TokenType.LeftAngle, start);
                break;

            case '-':
                if (Match('>'))
                    AddToken(TokenType.ArrowRight, start);
                else if (char.IsDigit(Current))
                    ScanNumber(start, startOffset, isNegative: true);
                else
                    AddToken(TokenType.Minus, start);
                break;

            case '=':
                // Check longest first
                if (Match("===>"))
                    AddToken(TokenType.Jump, start);
                else if (Match("===+"))
                    AddToken(TokenType.Fork, start);
                else if (Match("=="))
                    AddToken(TokenType.StorySeparator, start);
                else if (Match('='))
                    AddToken(TokenType.EqualEqual, start);
                else
                    AddToken(TokenType.Equal, start);
                break;

            case '`':
                ScanExpression(start);
                break;

            case '"':
                if (Check("\"\""))
                    ScanMultilineString(start, '"');
                else
                    ScanString('"', start);
                break;

            case '\'':
                if (Check("''"))
                    ScanMultilineString(start, '\'');
                else
                    ScanString('\'', start);
                break;

            case '$':
                if (Current == '(')
                {
                    AddToken(TokenType.DollarParen, start);
                    Advance();
                }
                else if (Current == '#' && Peek() == '(')
                {
                    AddToken(TokenType.DollarHashParen, start);
                    Advance(); Advance();
                }
                else if (Current == '?' && Peek() == '(')
                {
                    AddToken(TokenType.DollarQuestionParen, start);
                    Advance(); Advance();
                }
                else if (Current == '"' || Current == '\'')
                {
                    var quote = Current;
                    AddToken(TokenType.DollarString, start);
                    Advance(); // consume quote
                    ScanString(quote, start);
                }
                else if (Current == '*') // $*
                {
                    AddToken(TokenType.DollarRef, start);
                    Advance(); // consume *
                }
                else
                    ReportError(start, "Expected ` or \" or ' or ( or #( or ?( or * after $");
                break;

            default:
                if (char.IsDigit(c))
                    ScanNumber(start, startOffset, isNegative: false);
                else if (IsIdentifierStart(c))
                    ScanIdentifier(start, startOffset);
                else
                    ReportError(start, $"Unexpected character: '{c}'");
                break;
        }
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd)
        {
            var c = Current;
            if (char.IsWhiteSpace(c))
            {
                if (c == '\n' && _inCheckpointDef)
                {
                    // Do not consume newline if inside checkpoint def,
                    // so ScanToken can emit CheckpointEnd.
                    return;
                }

                Advance();
                if (c == '\n') _isStartOfLine = true;
            }
            else if (c == ':' && Peek() == ':')
            {
                SkipComment();
            }
            else
            {
                break;
            }
        }
    }

    private void SkipComment()
    {
        // We're at first ':'
        Advance(); // consume first ':'
        Advance(); // consume second ':'

        if (Current == ':')
        {
            // Block comment :::
            Advance(); // consume third ':'
            while (!IsAtEnd)
            {
                if (Current == ':' && Peek() == ':' && Peek(2) == ':')
                {
                    Advance(); Advance(); Advance();
                    break;
                }
                Advance();
            }
        }
        else
        {
            // Line comment - skip to end of line
            while (!IsAtEnd && Current != '\n')
            {
                Advance();
            }
        }
    }

    private void ScanString(char quote, TextPosition start)
    {
        var sb = new StringBuilder();

        while (!IsAtEnd && Current != quote)
        {
            if (Current == '\n')
            {
                ReportError(_position, "Unterminated string");
                return;
            }

            if (Current == '\\')
            {
                Advance();
                if (IsAtEnd)
                {
                    ReportError(_position, "Unterminated escape sequence");
                    return;
                }
                sb.Append(Current switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    '{' => '{',
                    '}' => '}',
                    _ => Current
                });
                Advance();
            }
            else
            {
                sb.Append(Current);
                Advance();
            }
        }

        if (IsAtEnd)
        {
            ReportError(start, "Unterminated string");
            return;
        }

        Advance(); // closing quote
        AddToken(TokenType.String, start, sb.ToString());
    }

    private void ScanMultilineString(TextPosition start, char delimiter)
    {
        // We're at first quote after seeing the triple quote sequence
        Advance(); // second quote
        Advance(); // third quote

        var sb = new StringBuilder();

        while (!IsAtEnd)
        {
            if (Current == delimiter && Peek() == delimiter && Peek(2) == delimiter)
            {
                Advance(); Advance(); Advance();
                break;
            }
            sb.Append(Current);
            Advance();
        }

        // Trim leading newline if present
        var content = sb.ToString();
        if (content.StartsWith('\n'))
            content = content[1..];
        else if (content.StartsWith("\r\n"))
            content = content[2..];

        AddToken(TokenType.MultilineString, start, content);
    }

    private void ScanNumber(TextPosition start, int startOffset, bool isNegative)
    {
        while (char.IsDigit(Current)) Advance();

        var isDouble = false;
        if (Current == '.' && char.IsDigit(Peek()))
        {
            isDouble = true;
            Advance(); // consume '.'
            while (char.IsDigit(Current)) Advance();
        }

        var lexeme = _source[startOffset.._current];

        if (isDouble)
        {
            if (double.TryParse(lexeme, out var d))
                AddToken(TokenType.Double, start, d);
            else
                ReportError(start, $"Invalid double: {lexeme}");
        }
        else
        {
            if (long.TryParse(lexeme, out var l))
                AddToken(TokenType.Integer, start, l);
            else
                ReportError(start, $"Invalid integer: {lexeme}");
        }
    }

    private void ScanIdentifier(TextPosition start, int startOffset)
    {
        while (IsIdentifierContinue(Current)) Advance();

        var lexeme = _source[startOffset.._current];

        var type = lexeme.ToLowerInvariant() switch
        {
            "true" => TokenType.True,
            "false" => TokenType.False,
            "nothing" => TokenType.Nothing,
            _ => TokenType.Identifier
        };

        if (type == TokenType.True)
            AddToken(type, start, true);
        else if (type == TokenType.False)
            AddToken(type, start, false);
        else
            AddToken(type, start, lexeme);
    }

    private void ScanExpression(TextPosition start)
    {
        var sb = new StringBuilder();
        var depth = 1;

        while (!IsAtEnd && depth > 0)
        {
            if (Current == '`')
            {
                depth--;
                if (depth == 0) break;
            }
            else if (Current == '\\' && Peek() == '`')
            {
                sb.Append('`');
                Advance();
            }
            else
            {
                sb.Append(Current);
            }
            Advance();
        }

        if (IsAtEnd && depth > 0)
        {
            ReportError(start, "Unterminated expression");
            return;
        }

        Advance(); // closing `
        AddToken(TokenType.Expression, start, sb.ToString());
    }

    private void ScanChannel(TextPosition start)
    {
        // We already consumed '<'
        var sb = new StringBuilder();

        while (!IsAtEnd && Current != '>')
        {
            if (char.IsWhiteSpace(Current))
            {
                // Spec line 331: No white space is allowed between < and >
                // If we hit whitespace, this is NOT a valid channel.
                // It should be treated as LeftAngle (<) and then whatever follows.
                // But we already consumed <. We need to rollback?
                // Or just error?
                // Spec doesn't say "error on whitespace in channel", it says "No white space allowed".
                // This implies < chan > is simply < followed by identifier "chan" then >.
                // So ScanChannel should abort and let the main loop continue scan from here?
                // But we called ScanChannel because IsChannelStart returned true.

                // If IsChannelStart was stricter, we wouldn't be here.
                // Let's fix IsChannelStart first? No, IsChannelStart looks ahead.

                // If we abort here, we need to backtrack or re-emit tokens.
                // Easier: Report Error? "Invalid channel name: contains whitespace".

                // Wait, if it's NOT a channel, it should be parsed as discrete tokens if possible.
                // But if the user intended a channel and added space, an error is helpful.
                // If the user meant "Less Than", then `IsChannelStart` should haven't triggered.

                // Let's look at IsChannelStart.
                ReportError(_position, "Channel names cannot contain whitespace");
                return;
            }

            sb.Append(Current);
            Advance();
        }

        if (IsAtEnd)
        {
            ReportError(start, "Unterminated channel");
            return;
        }

        Advance(); // closing >
        AddToken(TokenType.Channel, start, sb.ToString());
    }

    private bool IsChannelStart()
    {
        // Look ahead to see if this is <identifier> without whitespace
        var i = _current;
        while (i < _source.Length)
        {
            var c = _source[i];
            if (c == '>') return true; // Found closing >
            if (c == '\n' || char.IsWhiteSpace(c)) return false; // Found whitespace/newline before >, not a channel
            // Also spec says channel name is identifier-like? Reference says <name>.
            // Spec 329 "uniquely identified by 'channel_name'".
            // If we hit a non-identifier char that isn't >, likely not a channel?
            // But let's stick to simple "no whitespace" for now as per spec line 331.
            i++;
        }
        return false;
    }

    private static bool IsIdentifierStart(char c) =>
        char.IsLetter(c) || c == '_';

    private static bool IsIdentifierContinue(char c) =>
        char.IsLetterOrDigit(c) || c == '_';

    private void AddToken(TokenType type, TextPosition start, object? value = null)
    {
        var localStart = start.Offset - _initialOffset;
        var lexeme = _source[localStart.._current];
        _tokens.Add(new Token(type, start, _position, lexeme, value));
        _isStartOfLine = false;
    }

    private void ReportError(TextPosition pos, string message)
    {
        _errors.Add(new LexError(pos, message));
    }
}
