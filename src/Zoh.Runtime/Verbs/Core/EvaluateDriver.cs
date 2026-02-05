using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class EvaluateDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "evaluate";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /evaluate expr;

        if (verb.UnnamedParams.Length == 0)
        {
            // TODO: Review codebase for "improvised" diagnostic codes and standardise them to spec (e.g. invalid_type, parameter_not_found)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, "parameter_not_found", "Usage: /evaluate expr", verb.Start));
        }

        var param = verb.UnnamedParams[0];
        // Special case: Unlike normal Resolve, we want to know if it's an expression to catch evaluation errors nicely
        // But ValueResolver handles expression evaluation. 

        try
        {
            var result = ValueResolver.Resolve(param, context);
            return VerbResult.Ok(result);
        }
        catch (InvalidOperationException ex) // Assuming Evaluator throws these for undefined vars etc
        {
            // If ValueResolver throws specific exceptions for undefined vars, we catch them.
            // Currently generic InvalidOperationException or others?
            // Need to ensure Evaluator throws something we can identify or just treat as fatal.
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, "EvalError", ex.Message, verb.Start));
        }
        catch (IndexOutOfRangeException ex)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, "EvalError", ex.Message, verb.Start));
        }
    }
}
