using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class CountDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "count";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /count *var
        // /count *var[index]
        // /count [1,2,3]

        if (verb.UnnamedParams.Length == 0)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /count target", verb.Start));

        // ValueResolver handles Reference paths via GetAtPath automatically now.
        var target = ValueResolver.Resolve(verb.UnnamedParams[0], context);

        if (target is ZohNothing) return VerbResult.Ok(new ZohInt(0));
        if (target is ZohStr s) return VerbResult.Ok(new ZohInt(s.Value.Length));
        if (target is ZohList list) return VerbResult.Ok(new ZohInt(list.Items.Length));
        if (target is IZohMap map) return VerbResult.Ok(new ZohInt(map.Count));
        if (target is ZohChannel channel) return VerbResult.Ok(new ZohInt(context.GetChannelSize(channel.Name)));

        return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Cannot count type {target.Type}", verb.Start));
    }
}
