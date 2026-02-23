using System.Collections.Generic;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Validation.Standard.Media;

public class PlayOneValidator : IVerbValidator
{
    public string VerbName => "playOne";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();
        if (call.UnnamedParams.Length < 1)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Fatal,
                "missing_parameter",
                "/playOne requires at least 1 parameter (the resource).",
                call.Start, story.Name));
        }
        return diagnostics;
    }
}
