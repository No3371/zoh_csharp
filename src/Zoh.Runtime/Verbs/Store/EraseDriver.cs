using System.Linq;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Store;

public class EraseDriver : IVerbDriver
{
    public string Namespace => "store";
    public string Name => "erase";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /erase store:"name"?, *var;
        string? storeName = null;
        if (verb.NamedParams.TryGetValue("store", out var storeValAst))
        {
            var storeVal = ValueResolver.Resolve(storeValAst, context);
            if (storeVal is ZohStr s) storeName = s.Value;
        }

        var refs = verb.UnnamedParams.OfType<ValueAst.Reference>().ToList();
        if (refs.Count == 0)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(
                DiagnosticSeverity.Fatal, "parameter_not_found",
                "Erase requires at least one reference parameter", verb.Start));
        }

        foreach (var varRef in refs)
        {
            if (!context.Storage.Exists(storeName, varRef.Name))
            {
                // Spec: return ok with info diagnostic for not-found
                return DriverResult.Complete.WithDiagnostics(ZohValue.Nothing, new[]
                {
                    new Diagnostic(DiagnosticSeverity.Info, "not_found",
                        $"Variable not in storage: {varRef.Name}", verb.Start)
                });
            }
            context.Storage.Erase(storeName, varRef.Name);
        }

        return DriverResult.Complete.Ok();
    }
}
