namespace Zoh.Runtime.Lexing;

/// <summary>
/// A single token from the lexer. Immutable.
/// </summary>
public readonly record struct Token(
    TokenType Type,
    TextPosition Start,
    TextPosition End,
    string Lexeme,
    object? Value = null  // Parsed literal value (long, double, string content, etc.)
)
{
    public int Length => End.Offset - Start.Offset;

    public static Token Eof(TextPosition pos) => new(TokenType.Eof, pos, pos, "");

    public static Token Error(TextPosition start, TextPosition end, string message)
        => new(TokenType.Error, start, end, message);

    public override string ToString() => $"{Type}({Lexeme}) at {Start}";
}
