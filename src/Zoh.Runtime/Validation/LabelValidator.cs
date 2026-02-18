using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Lexing;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Validation;

/// <summary>
/// Validates that story labels are unique (case-insensitive).
/// </summary>
public class LabelValidator : IStoryValidator
{
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();
        var seenLabels = new Dictionary<string, StatementAst.Label>(StringComparer.OrdinalIgnoreCase);

        // Scan AST for labels
        foreach (var statement in story.Statements)
        {
            if (statement is StatementAst.Label labelStmt)
            {
                if (seenLabels.TryGetValue(labelStmt.Name, out var firstOccurrence))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticSeverity.Fatal,
                        "duplicate_label",
                        $"Duplicate label found: @{labelStmt.Name}. First defined at {firstOccurrence.Position}.",
                        labelStmt.Position,
                        story.Name
                    ));
                }
                else
                {
                    seenLabels[labelStmt.Name] = labelStmt;
                }
            }
        }

        return diagnostics;
    }
}
