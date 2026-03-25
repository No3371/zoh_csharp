using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Interpolation;

namespace Zoh.Runtime.Verbs.Debug;

public class AssertDriver : IVerbDriver
{
    public string Namespace => "core.debug";
    public string Name => "assert";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        if (call.UnnamedParams.Length < 1)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /assert condition, [message]", call.Start));
        }

        var subjectParam = call.UnnamedParams[0];
        var subjectValue = ValueResolver.Resolve(subjectParam, context);

        if (subjectValue is ZohVerb subjectVerb)
        {
            subjectValue = context.ExecuteVerb(subjectVerb.VerbValue, context).ValueOrNothing;
        }

        var isParam = call.NamedParams.GetValueOrDefault("is");
        ZohValue compareValue = isParam != null ? ValueResolver.Resolve(isParam, context) : ZohBool.True;

        if (isParam == null && subjectValue is not ZohBool && subjectValue is not ZohNothing)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Condition must be boolean or nothing, got: {subjectValue.Type}", call.Start));
        }

        if (!subjectValue.Equals(compareValue))
        {
            string message = "assertion failed";
            if (call.UnnamedParams.Length > 1)
            {
                var msgValue = ValueResolver.Resolve(call.UnnamedParams[1], context);

                if (msgValue is ZohStr s)
                {
                    try
                    {
                        var interpolator = new ZohInterpolator(context.Variables);
                        message = interpolator.Interpolate(s.Value);
                    }
                    catch (Exception ex)
                    {
                        message = $"[Interpolation Error: {ex.Message}] {s.Value}";
                    }
                }
                else
                {
                    message = msgValue.ToString();
                }
            }

            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "assertion_failed", message, call.Start));
        }

        return DriverResult.Complete.Ok();
    }
}
