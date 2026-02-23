using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Verbs.Standard.Media;

public class ResumeDriver : IVerbDriver
{
    private readonly IResumeHandler? _handler;
    public string? Namespace => "std";
    public string Name => "resume";

    public ResumeDriver(IResumeHandler? handler = null)
    {
        _handler = handler;
    }

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Fatal, "invalid_context", "Resume requires a valid Context.", call.Start));

        string id = "";
        if (call.UnnamedParams.Length > 0)
        {
            id = ValueResolver.Resolve(call.UnnamedParams[0], ctx).ToString();
        }

        var request = new ResumeRequest(id);

        _handler?.OnResume(ctx, request);

        return VerbResult.Ok();
    }
}
