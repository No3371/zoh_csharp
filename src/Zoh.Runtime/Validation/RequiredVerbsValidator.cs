using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Validation;

/// <summary>
/// Validates that all verbs required by the story (via metadata) are registered in the runtime.
/// </summary>
public class RequiredVerbsValidator(VerbRegistry registry) : IStoryValidator
{
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();

        if (story.Metadata.TryGetValue("required_verbs", out var required) && required is ZohList list)
        {
            foreach (var element in list.Items)
            {
                string? verbName = null;
                if (element is ZohStr s) verbName = s.Value;
                // Maybe handle other types if needed, but strings are standard

                if (!string.IsNullOrEmpty(verbName))
                {
                    // Handle full path vs simple name? Spec says required_verbs lists verb names.
                    // Registry lookup handles namespace/name splitting if needed?
                    // VerbRegistry.IsRegistered might need checking.
                    // Assuming GetDriver return null if not found.

                    var parts = verbName.Split('.');
                    string ns = parts.Length > 1 ? string.Join(".", parts.Take(parts.Length - 1)) : "";
                    string name = parts.Last();

                    if (registry.GetDriver(ns, name) == null)
                    {
                        // No position info for metadata usually, unless we track it differently.
                        // We use story start or 0,0.
                        diagnostics.Add(new Diagnostic(
                            DiagnosticSeverity.Fatal,
                            "missing_required_verb",
                            $"Required verb not available: /{verbName}",
                            new Lexing.TextPosition(0, 0, 0),
                            story.Name
                        ));
                    }
                }
            }
        }

        return diagnostics;
    }
}
