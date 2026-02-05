using System.Collections.Immutable;

namespace Zoh.Runtime.Lexing;

/// <summary>
/// Result of lexing a ZOH source file.
/// </summary>
public readonly record struct LexResult(
    ImmutableArray<Token> Tokens,
    ImmutableArray<LexError> Errors
)
{
    public bool HasErrors => Errors.Length > 0;
}

/// <summary>
/// A lexing error with position and message.
/// </summary>
public readonly record struct LexError(
    TextPosition Position,
    string Message
);
