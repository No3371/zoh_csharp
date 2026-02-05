using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Interpolation;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class InterpolateDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "interpolate";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /interpolate "template"

        if (verb.UnnamedParams.Length == 0)
        {
            return VerbResult.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "MissingArguments", "Usage: /interpolate \"template\"", verb.Start));
        }

        var templateVal = ValueResolver.Resolve(verb.UnnamedParams[0], context);

        string template;
        if (templateVal is ZohStr s) template = s.Value;
        else template = templateVal.ToString();

        var interpolator = new ZohInterpolator(context.Variables);
        try
        {
            var result = interpolator.Interpolate(template);
            return VerbResult.Ok(new ZohStr(result));
        }
        catch (Exception ex)
        {
            return VerbResult.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "InterpolationError", ex.Message, verb.Start));
        }
    }
}
