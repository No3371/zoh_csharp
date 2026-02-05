using System.Collections.Immutable;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Parsing;

/// <summary>
/// Result of parsing a ZOH source file.
/// </summary>
public readonly record struct ParseResult(
    StoryAst? Story,
    ImmutableArray<ParseError> Errors
)
{
    public bool HasErrors => Errors.Length > 0;
    public bool Success => Story is not null && !HasErrors;
}

/// <summary>
/// A parsing error with position and message.
/// </summary>
public readonly record struct ParseError(
    Lexing.TextPosition Position,
    string Message
);
