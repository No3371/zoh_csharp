using System.Linq;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Store;

public class WriteDriver : IVerbDriver
{
    public string Namespace => "core.store";
    public string Name => "write";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /write [store: "name"] *var1 *var2 ...;

        string? storeName = null;
        if (verb.NamedParams.TryGetValue("store", out var storeValAst))
        {
            var storeVal = ValueResolver.Resolve(storeValAst, context);
            if (storeVal is ZohStr s) storeName = s.Value; // Should we strictly checking string? Spec says store:name?
        }

        var refs = verb.UnnamedParams.OfType<ValueAst.Reference>().ToList();

        if (refs.Count == 0)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Write requires at least one reference parameter", verb.Start));
        }

        foreach (var varRef in refs)
        {
            if (!varRef.Path.IsEmpty)
            {
                // Zoh Spec doesn't explicitly support Indexed Write yet (implied write variable).
                // We resolve value of *var.
            }

            var value = ValueResolver.Resolve(varRef, context);

            if (value is ZohVerb || value is ZohChannel || value is ZohExpr)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Cannot persist type: {value.Type}", verb.Start));
            }

            context.Storage.Write(storeName, varRef.Name, value);
        }

        return DriverResult.Complete.Ok();
    }
}
