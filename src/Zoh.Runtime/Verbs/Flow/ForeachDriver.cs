using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow
{
    public class ForeachDriver : IVerbDriver
    {
        public string Namespace => "core.flow";
        public string Name => "foreach";

        public DriverResult Execute(IExecutionContext context, VerbCallAst call)
        {
            if (call.UnnamedParams.Length < 3)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /foreach collection, item_var_name, verb", call.Start));
            }

            var collectionVal = ValueResolver.Resolve(call.UnnamedParams[0], context);

            if (call.UnnamedParams[1] is not ValueAst.Reference iteratorRef)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Iterator must be a reference (*name).", call.Start));
            }

            var varName = iteratorRef.Name;
            context.Variables.Drop(varName);

            var verbVal = ValueResolver.Resolve(call.UnnamedParams[2], context);
            if (!(verbVal is ZohVerb verbToRun))
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Third argument must be a verb.", call.Start));
            }

            IEnumerable<ZohValue> items = Enumerable.Empty<ZohValue>();

            if (collectionVal is ZohList list)
            {
                items = list.Items;
            }
            else if (collectionVal is ZohMap map)
            {
                // Map iteration yields entries implicitly or keys? Spec says "for (key, value) in collection.entries"
                // And "Iterator receives single-entry map"
                // Iterator receives single-entry map (optimized as KvPair)
                items = map.Items.Select(kvp =>
                    new ZohKvPair(kvp.Key, kvp.Value)
                ).Cast<ZohValue>();
            }

            else
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Cannot iterate over type {collectionVal.Type}", call.Start));
            }

            foreach (var item in items)
            {
                context.Variables.Set(varName, item);

                var breakResult = FlowUtils.EvaluateBreakIf(call, context);
                if (breakResult is DriverResult.Suspend) return breakResult;
                if (breakResult is { IsFatal: true }) return breakResult;
                if (breakResult != null) break;

                var continueResult = FlowUtils.EvaluateContinueIf(call, context);
                if (continueResult is DriverResult.Suspend) return continueResult;
                if (continueResult is { IsFatal: true }) return continueResult;
                if (continueResult != null) continue;

                var result = context.ExecuteVerb(verbToRun.VerbValue, context);
                if (result.IsFatal) return result;
            }

            return DriverResult.Complete.Ok();
        }
    }
}
