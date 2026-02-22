using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using System.Collections.Generic;

namespace Zoh.Runtime.Validation.Standard;

public class ChooseValidator : IVerbValidator
{
    public string VerbName => "choose";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();

        if (call.UnnamedParams.Length % 3 != 0)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Fatal,
                "invalid_parameter_count",
                $"/choose options must be provided in triples (visible, text, value).",
                call.Start,
                story.Name
            ));
        }

        if (call.NamedParams.TryGetValue("timeout", out var timeoutVal))
        {
            if (timeoutVal is ValueAst.String || timeoutVal is ValueAst.Boolean)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    "invalid_type",
                    $"The 'timeout' parameter for /choose must be numeric if provided as a literal.",
                    call.Start,
                    story.Name
                ));
            }
        }

        return diagnostics;
    }
}
