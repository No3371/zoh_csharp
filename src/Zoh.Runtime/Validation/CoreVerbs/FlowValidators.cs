using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Validation.CoreVerbs;

public class JumpValidator : IVerbValidator
{
    public string VerbName => "jump";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        return FlowValidationHelper.ValidateStructure(call, story, "jump");
    }
}

public class ForkValidator : IVerbValidator
{
    public string VerbName => "fork";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        return FlowValidationHelper.ValidateStructure(call, story, "fork");
    }
}

public class CallValidator : IVerbValidator
{
    public string VerbName => "call";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        return FlowValidationHelper.ValidateStructure(call, story, "call");
    }
}

internal static class FlowValidationHelper
{
    public static IReadOnlyList<Diagnostic> ValidateStructure(VerbCallAst call, CompiledStory story, string verb)
    {
        var diagnostics = new List<Diagnostic>();

        // Expect at least 2 args: story, label
        // Or named: "story", "label"

        int count = call.UnnamedParams.Length + call.NamedParams.Count;

        if (count < 2)
        {
            diagnostics.Add(new Diagnostic(
               DiagnosticSeverity.Fatal,
               "missing_parameter",
               $"/{verb} requires at least a story name (or ?) and a label name",
               call.Start,
               story.Name
           ));
        }

        return diagnostics;
    }
}
