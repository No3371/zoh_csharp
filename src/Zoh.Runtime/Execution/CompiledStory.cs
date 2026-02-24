using System.Collections.Immutable;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using System.Linq;

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
    public ImmutableDictionary<string, ImmutableArray<StatementAst.ContractParam>> Contracts { get; }

    public CompiledStory(string name, ImmutableDictionary<string, ZohValue> metadata, ImmutableArray<StatementAst> statements, ImmutableDictionary<string, int> labels, ImmutableDictionary<string, ImmutableArray<StatementAst.ContractParam>> contracts)
    {
        Name = name;
        Metadata = metadata;
        Statements = statements;
        Labels = labels;
        Contracts = contracts;
    }

    public static CompiledStory FromAst(StoryAst ast, DiagnosticBag diagnostics)
    {
        // Simple conversion for now. Real compilation would flatten blocks etc if needed.
        // We need to map labels to indices.
        var labels = new Dictionary<string, int>();
        var contracts = new Dictionary<string, ImmutableArray<StatementAst.ContractParam>>();

        for (int i = 0; i < ast.Statements.Length; i++)
        {
            if (ast.Statements[i] is StatementAst.Label label)
            {
                labels[label.Name] = i;
                if (!label.Params.IsDefaultOrEmpty && label.Params.Length > 0)
                {
                    contracts[label.Name] = label.Params;
                }
            }
        }

        // Convert metadata? AST metadata is Dictionary<string, ValueAst>. We need to resolve them?
        // Or store as is. The spec says Runtime Metadata is Map<string, Value>.
        // Let's assume empty metadata for the wrapper for now or basic conversion.
        var b = ImmutableDictionary.CreateBuilder<string, ZohValue>();
        foreach (var kvp in ast.Metadata)
        {
            if (IsValidMetadataAst(kvp.Value))
            {
                try
                {
                    b.Add(kvp.Key, ValueResolver.ResolveContextless(kvp.Value));
                }
                catch (Exception ex)
                {
                    diagnostics.ReportError("invalid_metadata_type", $"Failed to resolve metadata '{kvp.Key}': {ex.Message}", default);
                }
            }
            else
            {
                diagnostics.ReportError("invalid_metadata_type", $"Metadata value for '{kvp.Key}' has unsupported type. Allowed types are boolean, integer, double, string, list, and map.", default);
            }
        }
        var name = ast.Name;
        var meta = b.ToImmutableDictionary();
        if (meta.ContainsKey("id") && meta["id"].Type == ZohValueType.String) name = meta["id"].AsString().Value;
        return new CompiledStory(name, meta, ast.Statements, labels.ToImmutableDictionary(), contracts.ToImmutableDictionary());
    }

    private static bool IsValidMetadataAst(ValueAst ast)
    {
        return ast switch
        {
            ValueAst.Boolean or ValueAst.Integer or ValueAst.Double or ValueAst.String => true,
            ValueAst.List l => l.Elements.All(IsValidMetadataAst),
            ValueAst.Map m => m.Entries.All(e => IsValidMetadataAst(e.Key) && IsValidMetadataAst(e.Value)),
            _ => false
        };
    }
}
