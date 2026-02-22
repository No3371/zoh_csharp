using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using System.Collections.Generic;

namespace Zoh.Runtime.Validation.Standard;

public class PromptValidator : IVerbValidator
{
    public string VerbName => "prompt";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();

        // Optional arguments are allowed, no need to strictly enforce arg counts at AST level
        // as prompt can be /prompt; or /prompt "text";

        // Ensure timeout attribute is an int or float literal if provided as literal
        if (call.NamedParams.TryGetValue("timeout", out var timeoutVal))
        {
            if (timeoutVal is ValueAst.String || timeoutVal is ValueAst.Boolean)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    "invalid_type",
                    $"The 'timeout' parameter for /prompt must be numeric if provided as a literal.",
                    call.Start,
                    story.Name
                ));
            }
        }

        return diagnostics;
    }
}
