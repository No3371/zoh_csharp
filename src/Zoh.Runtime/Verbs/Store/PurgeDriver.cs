using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Store;

public class PurgeDriver : IVerbDriver
{
    public string Namespace => "store";
    public string Name => "purge";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /purge store:"name"?;
        string? storeName = null;
        if (verb.NamedParams.TryGetValue("store", out var storeValAst))
        {
            var storeVal = ValueResolver.Resolve(storeValAst, context);
            if (storeVal is ZohStr s) storeName = s.Value;
        }

        context.Storage.Purge(storeName);
        return VerbResult.Ok();
    }
}
