using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using System.Collections.Generic;

namespace Zoh.Runtime.Validation.Standard;

public class ChooseFromValidator : IVerbValidator
{
    public string VerbName => "chooseFrom";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();

        if (call.UnnamedParams.Length < 1)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Fatal,
                "missing_parameter",
                $"/chooseFrom requires at least 1 parameter (the list of maps).",
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
                    $"The 'timeout' parameter for /chooseFrom must be numeric if provided as a literal.",
                    call.Start,
                    story.Name
                ));
            }
        }

        return diagnostics;
    }
}
