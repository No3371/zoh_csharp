using System.Text.RegularExpressions;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Expressions;

/// <summary>
/// specialized lexer for expression strings (inside backticks).
/// Reuses core Token Types but operates on expression fragments.
/// </summary>
public class ExpressionLexer(string source, TextPosition startPosition)
{
    // Reuse main Lexer?
    // Main Lexer handles expression syntax logic (it emits tokens for +, -, identifiers etc).
    // If I use main Lexer on the expression string, it should work?
    // Yes, ZOH expression syntax is subset of ZOH syntax.
    // So I can reuse Lexer.

    // However, Lexer.ScanToken handles "Structure" tokens like ===.
    // Expression only needs values and operators.
    // But reusing Lexer ensures consistency.

    public LexResult Tokenize()
    {
        // Position offset needs to be handled?
        // Lexer takes source string.
        // If I pass source fragment, positions will be relative to 0.
        // We want positions relative to original file.
        // Lexer constructor doesn't take start offset?
        // Let's check Lexer.cs

        // Pass reference position to allow correct error reporting
        var lexer = new Lexer(source, startPosition);
        // Lexer assumes new file context?
        // I might need to extended Lexer to accept start position.

        return lexer.Tokenize();
    }
}
