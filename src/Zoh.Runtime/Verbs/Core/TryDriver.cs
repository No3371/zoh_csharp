using System.Linq;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Zoh.Runtime.Verbs.Core;

public class TryDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "try";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /try verb, catch:handler?
        // [suppress]

        if (verb.UnnamedParams.Length == 0)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /try verb", verb.Start));

        var targetVerbVal = ValueResolver.Resolve(verb.UnnamedParams[0], context);
        if (targetVerbVal is null or not ZohVerb)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Expected verb, got " + targetVerbVal.Type, verb.Start));

        ZohVerb targetVerb;
        targetVerb = (ZohVerb)targetVerbVal;

        ZohVerb? catchVerb = null;
        if (verb.NamedParams.TryGetValue("catch", out var catchValAst))
        {
            var catchVal = ValueResolver.Resolve(catchValAst, context);
            catchVerb = catchVal as ZohVerb;
            if (catchVerb == null)
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Expected cache being a verb, got " + catchVal.Type, verb.Start));
        }

        var suppressing = verb.Attributes.Any(a => a.Name.Equals("suppress", System.StringComparison.OrdinalIgnoreCase));

        var result = context.ExecuteVerb(targetVerb.VerbValue, context);
        if (result.IsFatal)
        {
            var resultDiags = result is DriverResult.Complete rc ? rc.Diagnostics : ImmutableArray<Diagnostic>.Empty;

            if (catchVerb is not null)
            {
                var catchResult = context.ExecuteVerb(catchVerb.VerbValue, context);
                var catchValue = catchResult is DriverResult.Complete cc ? cc.Value : ZohNothing.Instance;
                var catchDiags = catchResult is DriverResult.Complete cd ? cd.Diagnostics : ImmutableArray<Diagnostic>.Empty;

                if (suppressing)
                {
                    return new DriverResult.Complete(catchValue, catchDiags);
                }
                else
                {
                    return new DriverResult.Complete(
                        catchValue,
                        resultDiags
                            .Select(d => d.Severity == DiagnosticSeverity.Fatal
                                ? new Diagnostic(DiagnosticSeverity.Error, d.Code, d.Message, d.Position)
                                : d).Concat(catchDiags)
                            .ToImmutableArray());
                }
            }

            if (suppressing)
            {
                return new DriverResult.Complete(ZohValue.Nothing, ImmutableArray<Diagnostic>.Empty);
            }
            else
            {
                return new DriverResult.Complete(
                    ZohValue.Nothing,
                    resultDiags
                        .Select(d => d.Severity == DiagnosticSeverity.Fatal
                                ? new Diagnostic(DiagnosticSeverity.Error, d.Code, d.Message, d.Position)
                                : d)
                        .ToImmutableArray()
                );
            }
        }

        return result;
    }
}
