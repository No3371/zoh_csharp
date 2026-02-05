using System.Collections.Immutable;
using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Parsing.Ast;

/// <summary>
/// AST node representing a value in ZOH. Sealed hierarchy for exhaustive pattern matching.
/// </summary>
public abstract record ValueAst
{
    private ValueAst() { }

    /// <summary>Nothing/null value.</summary>
    public sealed record Nothing() : ValueAst;

    /// <summary>Boolean literal.</summary>
    public sealed record Boolean(bool Value) : ValueAst;

    /// <summary>Integer literal.</summary>
    public sealed record Integer(long Value) : ValueAst;

    /// <summary>Double literal.</summary>
    public sealed record Double(double Value) : ValueAst;

    /// <summary>String literal.</summary>
    public sealed record String(string Value) : ValueAst;

    /// <summary>Variable reference, optionally indexed.</summary>
    public sealed record Reference(string Name, ImmutableArray<ValueAst> Path) : ValueAst
    {
        public Reference(string Name) : this(Name, ImmutableArray<ValueAst>.Empty) { }
        public Reference(string Name, ValueAst? Index) : this(Name, Index != null ? ImmutableArray.Create(Index) : ImmutableArray<ValueAst>.Empty) { }
    }

    /// <summary>Channel reference.</summary>
    public sealed record Channel(string Name) : ValueAst;

    /// <summary>List literal.</summary>
    public sealed record List(ImmutableArray<ValueAst> Elements) : ValueAst;

    /// <summary>Map literal.</summary>
    public sealed record Map(ImmutableArray<(ValueAst Key, ValueAst Value)> Entries) : ValueAst;

    /// <summary>Unevaluated expression.</summary>
    public sealed record Expression(string Source, TextPosition Position) : ValueAst;

    /// <summary>Verb as a value (objectified verb call).</summary>
    public sealed record Verb(VerbCallAst Call) : ValueAst;
}

/// <summary>
/// Attribute on a verb call.
/// </summary>
public sealed record AttributeAst(string Name, ValueAst? Value, TextPosition Position);

/// <summary>
/// A verb call in the AST.
/// </summary>
public sealed record VerbCallAst(
    string? Namespace,
    string Name,
    bool IsBlock,
    ImmutableArray<AttributeAst> Attributes,
    ImmutableDictionary<string, ValueAst> NamedParams,
    ImmutableArray<ValueAst> UnnamedParams,
    TextPosition Start);
