using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Var;

public class FlagDriver : IVerbDriver
{
    public string Namespace => "core.var";
    public string Name => "flag";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /flag "name", value;
        // /flag [scope: "runtime"] "name", value;

        if (verb.UnnamedParams.Length < 2)
        {
            return DriverResult.Complete.Fatal(
                new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /flag \"name\", value;", verb.Start));
        }

        var nameVal = ValueResolver.Resolve(verb.UnnamedParams[0], context);
        if (nameVal is not ZohStr nameStr)
        {
            return DriverResult.Complete.Fatal(
                new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Flag name must be a string", verb.Start));
        }

        var value = ValueResolver.Resolve(verb.UnnamedParams[1], context);

        bool isRuntime = false;
        foreach (var attr in verb.Attributes)
        {
            if (!attr.Name.Equals("scope", StringComparison.OrdinalIgnoreCase)) continue;
            if (attr.Value == null) continue;

            var scopeVal = ValueResolver.Resolve(attr.Value, context);
            isRuntime = scopeVal is ZohStr s && s.Value.Equals("runtime", StringComparison.OrdinalIgnoreCase);
            break;
        }

        if (isRuntime) context.Runtime.SetFlag(nameStr.Value, value);
        else context.SetContextFlag(nameStr.Value, value);

        return DriverResult.Complete.Ok(value);
    }
}

