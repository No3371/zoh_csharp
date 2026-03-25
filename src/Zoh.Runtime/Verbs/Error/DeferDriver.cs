using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using System.Linq;

namespace Zoh.Runtime.Verbs.Error;

public class DeferDriver : IVerbDriver
{
    public string Namespace => "core.error";
    public string Name => "defer";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        if (verb.UnnamedParams.Length == 0)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_param", "Missing verb argument", verb.Start));
        }

        var verbParam = verb.UnnamedParams[0];
        string scope = "story";

        // Check scope attribute
        // Check scope attribute
        var scopeAttr = verb.Attributes.FirstOrDefault(a => a.Name == "scope");
        if (scopeAttr != null)
        {
            // Resolve scope string? Or raw? Spec says: scope:story|context.
            // Attributes are AttributeAst. Value is ValueAst.
            // We generally resolve attribute values if they are dynamic.
            // For simplicity, let's assume string literal for now or resolve it.
            // ValueResolver CAN resolve primitives.
            var val = ValueResolver.Resolve(scopeAttr.Value, context);
            scope = val.ToString().ToLowerInvariant();
        }

        if (scope == "story")
        {
            context.AddStoryDefer(verbParam);
        }
        else if (scope == "context")
        {
            context.AddContextDefer(verbParam);
        }
        else
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_scope", $"Invalid scope: {scope}", verb.Start));
        }

        return DriverResult.Complete.Ok(ZohValue.Nothing);
    }
}
