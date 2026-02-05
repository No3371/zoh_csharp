using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Parsing.Ast;

/// <summary>
/// AST node representing a statement. Sealed hierarchy for exhaustive pattern matching.
/// </summary>
public abstract record StatementAst
{
    private StatementAst() { }

    /// <summary>A label definition.</summary>
    public sealed record Label(string Name, TextPosition Position) : StatementAst;

    /// <summary>A verb call statement.</summary>
    public sealed record VerbCall(VerbCallAst Call) : StatementAst;

    /// <summary>A sequence of statements (block or empty).</summary>
    public sealed record Sequence(System.Collections.Immutable.ImmutableArray<StatementAst> Statements, TextPosition Position) : StatementAst;
}
