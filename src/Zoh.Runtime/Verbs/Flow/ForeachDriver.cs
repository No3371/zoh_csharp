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
        public string Namespace => "core";
        public string Name => "foreach";

        public VerbResult Execute(IExecutionContext context, VerbCallAst call)
        {
            if (call.UnnamedParams.Length < 3)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /foreach collection, item_var_name, verb", call.Start));
            }

            var collectionVal = ValueResolver.Resolve(call.UnnamedParams[0], context);
            var varNameVal = ValueResolver.Resolve(call.UnnamedParams[1], context);

            if (varNameVal.Type != ZohValueType.String)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Variable name must be a string.", call.Start));
            }
            var varName = varNameVal.AsString().Value;
            context.Variables.Drop(varName);

            var verbVal = ValueResolver.Resolve(call.UnnamedParams[2], context);
            if (!(verbVal is ZohVerb verbToRun))
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Third argument must be a verb.", call.Start));
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
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Cannot iterate over type {collectionVal.Type}", call.Start));
            }

            foreach (var item in items)
            {
                context.Variables.Set(varName, item);

                if (FlowUtils.ShouldBreak(call, context))
                {
                    break;
                }

                if (FlowUtils.ShouldContinue(call, context))
                {
                    continue;
                }

                var result = context.ExecuteVerb(verbToRun.VerbValue, context);
                if (result.IsFatal) return result;
            }

            return VerbResult.Ok();
        }
    }
}
