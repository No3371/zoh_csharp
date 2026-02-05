using System.Collections.Immutable;

namespace Zoh.Runtime.Parsing.Ast;

/// <summary>
/// Complete story AST - the root node of a parsed ZOH file.
/// </summary>
public sealed record StoryAst(
    string Name,
    ImmutableDictionary<string, ValueAst> Metadata,
    ImmutableArray<StatementAst> Statements,
    ImmutableDictionary<string, int> Labels  // Label name -> statement index
);
