namespace Zoh.Runtime.Lexing;

/// <summary>
/// Immutable position in source text. 1-indexed for human-readable errors.
/// </summary>
public readonly record struct TextPosition(int Line, int Column, int Offset)
{
    public static TextPosition Start => new(1, 1, 0);
    
    public TextPosition NextColumn() => this with { Column = Column + 1, Offset = Offset + 1 };
    public TextPosition NextLine() => new(Line + 1, 1, Offset + 1);
    
    public override string ToString() => $"({Line}:{Column})";
}
