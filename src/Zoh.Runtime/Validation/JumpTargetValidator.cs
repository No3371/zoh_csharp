using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Validation;

/// <summary>
/// Validates jump targets within the story.
/// Checks that local jumps (story argument is ? or nothing) target existing labels.
/// </summary>
public class JumpTargetValidator : IStoryValidator
{
    public int Priority => 500; // Run after label validation

    public IReadOnlyList<Diagnostic> Validate(CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();

        foreach (var stmt in story.Statements)
        {
            if (stmt is StatementAst.VerbCall callStmt)
            {
                var verbName = callStmt.Call.Name.ToLowerInvariant();
                if (verbName == "jump" || verbName == "fork" || verbName == "call")
                {
                    ValidateJump(callStmt.Call, story, diagnostics);
                }
            }
        }

        return diagnostics;
    }

    private void ValidateJump(VerbCallAst call, CompiledStory story, List<Diagnostic> diagnostics)
    {
        // Spec: /jump [story], [label]
        // If story is ? or nothing, it's a local jump.

        // Resolve params order?
        // Named params vs positional.
        // Assuming standard core verbs use positional or specific names.
        // Jump(story, label)

        // We need to resolve values if they are literals.
        // If they are references/expressions, we can't validate statically.

        ValueAst? storyParam = null;
        ValueAst? labelParam = null;

        if (call.UnnamedParams.Length >= 2)
        {
            storyParam = call.UnnamedParams[0];
            labelParam = call.UnnamedParams[1];
        }
        else
        {
            // Handle named params - "story", "label"?
            if (call.NamedParams.TryGetValue("story", out var s)) storyParam = s;
            if (call.NamedParams.TryGetValue("label", out var l)) labelParam = l;

            // Fallback: If 1 pos + 1 named? Complicated. 
            // Let's assume standard usage for now. If mixed, we skip static validation.
        }

        if (storyParam != null && labelParam != null)
        {
            // Check if story is local
            bool isLocal = false;

            if (storyParam is ValueAst.Nothing) isLocal = true;
            // Also null string? ZOH doesn't really have null strings, just String("").
            // But if user passes `?` it becomes Nothing.

            // Also explicit "?" token in AST might be represented as Nothing?

            if (isLocal)
            {
                // Check label
                if (labelParam is ValueAst.String s)
                {
                    var labelName = s.Value;
                    // Remove @ if present? Spec says label param is string "name".
                    // The label definition is @name.
                    // The jump is /jump ?, "name".

                    if (!story.Labels.ContainsKey(labelName))
                    {
                        diagnostics.Add(new Diagnostic(
                           DiagnosticSeverity.Warning, // Warning, not fatal (maybe runtime dynamic label creation? Unlikely in ZOH)
                           "unknown_jump_target",
                           $"Jump to unknown label: @{labelName}",
                           call.Start,
                           story.Name
                       ));
                    }
                }
            }
        }
    }
}
