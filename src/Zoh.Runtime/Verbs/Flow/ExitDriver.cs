using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow;

public class ExitDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "exit";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Exit requires a valid Context.", call.Start));

        // Terminate context
        // This will run defers and set state to Terminated
        ctx.Terminate();

        return DriverResult.Complete.Ok();
    }
}
