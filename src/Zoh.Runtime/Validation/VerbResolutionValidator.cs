using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Validation;

/// <summary>
/// Core validator that iterates all verb calls in a story.
/// 1. Checks if the verb exists in the registry (replaces NamespaceValidator).
/// 2. Delegates to specific IVerbValidators if registered.
/// </summary>
public class VerbResolutionValidator(HandlerRegistry registry) : IStoryValidator
{
    public int Priority => 0; // Run early

    public IReadOnlyList<Diagnostic> Validate(CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();

        foreach (var stmt in story.Statements)
        {
            if (stmt is StatementAst.VerbCall callStmt)
            {
                ValidateVerbCall(callStmt.Call, story, diagnostics);
            }
        }

        return diagnostics;
    }

    private void ValidateVerbCall(VerbCallAst call, CompiledStory story, List<Diagnostic> diagnostics)
    {
        // 1. Check if verb exists
        IVerbDriver driver = null;
        var fullName = string.IsNullOrEmpty(call.Namespace) ? call.Name : $"{call.Namespace}.{call.Name}";

        try
        {
            driver = registry.VerbDrivers.GetDriver(call.Namespace, call.Name);
        }
        catch (AmbiguousVerbException ex)
        {
            diagnostics.Add(new Diagnostic(
               DiagnosticSeverity.Fatal,
               "namespace_ambiguity",
               ex.Message,
               call.Start,
               story.Name
           ));
            return;
        }

        if (driver == null)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Fatal,
                "unknown_verb",
                $"Unknown verb: /{fullName}",
                call.Start,
                story.Name
            ));

            // If verb is unknown, we can't really validate it further with specific validators
            // unless we have validators for unknown verbs? Unlikely.
            return;
        }

        // 2. Run specific validators
        if (registry.VerbValidators.TryGetValue(call.Name, out var validators)) // Note: matching by name only? Or namespaced?
        {
            // Registry key logic needs to be consistent. 
            // HandlerRegistry.RegisterVerbValidator uses validator.VerbName.ToLowerInvariant().
            // Does it support namespaces? "std.print"?
            // If the key is just name, we might have collisions or applied validators to wrong namespace verbs.
            // Assumption: VerbName in IVerbValidator should match how we look it up.
            // For now, simple name lookup. If we need namespace support, IVerbValidator should probably specify it.

            // Checking how RegisterVerbValidator works:
            // var key = validator.VerbName.ToLowerInvariant();

            // So if I have "std.print", key is "std.print".
            // call.Name is "print", call.Namespace is "std".
            // So lookup key should be $"{call.Namespace}.{call.Name}" (normalized).

            // Wait, standard verbs mostly don't have namespaces in ZOH core yet? 
            // Or they do? /std.verb?
            // "Namespace Resolution: /c.d.c;"

            // We should reconstruct the key efficiently.

            string key = fullName.ToLowerInvariant();

            // Check full name first
            if (registry.VerbValidators.TryGetValue(key, out var specificValidators))
            {
                foreach (var v in specificValidators)
                {
                    diagnostics.AddRange(v.Validate(call, story));
                }
            }

            // Check simple name if namespace is empty? 
            // Or maybe validators are registered without namespace usually?
            // ZOH spec says "Nested namespace... forbid ambiguity".
            // Core verbs like /set are just "set".

            // If simple name "set" is registered, we match "set".
            // If "my.verb" is registered, we match "my.verb".
        }
    }
}
