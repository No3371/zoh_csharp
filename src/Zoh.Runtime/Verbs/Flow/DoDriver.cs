using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Verbs.Flow;

/// <summary>
/// <c>/do *verb</c> — runs the resolved verb; if the first result value is another <see cref="ZohVerb"/>,
/// runs that once more (single follow-up hop per spec).
/// </summary>
public class DoDriver : IVerbDriver
{
    public string Namespace => "core.flow";
    public string Name => "do";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        if (verb.UnnamedParams.Length == 0)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Error, "MissingArguments", "Usage: /do *verb", verb.Start));

        var val = ValueResolver.Resolve(verb.UnnamedParams[0], context);

        if (val is ZohVerb v)
        {
            var first = context.ExecuteVerb(v.VerbValue, context);
            if (first is DriverResult.Suspend)
                return first;
            if (first.IsFatal)
                return first;
            if (first.ValueOrNothing is ZohVerb returnedVerb)
                return context.ExecuteVerb(returnedVerb.VerbValue, context);
            return first;
        }

        return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Error, "invalid_type", $"Expected verb, got {val.Type}", verb.Start));
    }
}
