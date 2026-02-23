using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Verbs.Standard.Media;

public class PauseDriver : IVerbDriver
{
    private readonly IPauseHandler? _handler;
    public string? Namespace => "std";
    public string Name => "pause";

    public PauseDriver(IPauseHandler? handler = null)
    {
        _handler = handler;
    }

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Fatal, "invalid_context", "Pause requires a valid Context.", call.Start));

        string id = "";
        if (call.UnnamedParams.Length > 0)
        {
            id = ValueResolver.Resolve(call.UnnamedParams[0], ctx).ToString();
        }

        var request = new PauseRequest(id);

        _handler?.OnPause(ctx, request);

        return VerbResult.Ok();
    }
}
