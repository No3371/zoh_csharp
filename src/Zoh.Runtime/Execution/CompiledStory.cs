using System.Collections.Immutable;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Execution;

/// <summary>
/// Represents a story ready for execution.
/// </summary>
public class CompiledStory
{
    public string Name { get; }
    public ImmutableDictionary<string, ZohValue> Metadata { get; }
    public ImmutableArray<StatementAst> Statements { get; }
    public ImmutableDictionary<string, int> Labels { get; }

    public CompiledStory(string name, ImmutableDictionary<string, ZohValue> metadata, ImmutableArray<StatementAst> statements, ImmutableDictionary<string, int> labels)
    {
        Name = name;
        Metadata = metadata;
        Statements = statements;
        Labels = labels;
    }

    public static CompiledStory FromAst(StoryAst ast)
    {
        // Simple conversion for now. Real compilation would flatten blocks etc if needed.
        // We need to map labels to indices.
        var labels = new Dictionary<string, int>();
        for (int i = 0; i < ast.Statements.Length; i++)
        {
            if (ast.Statements[i] is StatementAst.Label label)
            {
                labels[label.Name] = i;
            }
        }

        // Convert metadata? AST metadata is Dictionary<string, ValueAst>. We need to resolve them?
        // Or store as is. The spec says Runtime Metadata is Map<string, Value>.
        // Let's assume empty metadata for the wrapper for now or basic conversion.
        var meta = ImmutableDictionary<string, ZohValue>.Empty;

        return new CompiledStory(ast.Name, meta, ast.Statements, labels.ToImmutableDictionary());
    }
}
