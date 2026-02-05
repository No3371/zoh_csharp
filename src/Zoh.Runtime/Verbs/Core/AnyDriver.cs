using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class AnyDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "any";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        var paramsList = verb.UnnamedParams;
        if (paramsList.Length == 0)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, "missing_param", "Missing variable parameter", verb.Start));
        }

        var value = ValueResolver.Resolve(paramsList[0], context);

        if (paramsList.Length > 1)
        {
            var index = ValueResolver.Resolve(paramsList[1], context);

            if (index is ZohNothing)
            {
                return VerbResult.Ok(new ZohBool(!value.IsNothing()));
            }

            if (value is ZohList list)
            {
                if (index is not ZohInt i)
                    return VerbResult.Ok(ZohBool.False);

                var idx = i.Value;
                if (idx < 0) idx = list.Items.Length + idx;

                if (idx < 0 || idx >= list.Items.Length)
                    return VerbResult.Ok(ZohBool.False);

                return VerbResult.Ok(new ZohBool(!list.Items[(int)idx].IsNothing()));
            }

            if (value is ZohMap map)
            {
                if (index is not ZohStr keyStr)
                {
                    return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index_type", $"Map key must be string, got: {index.Type}", verb.Start));
                }
                var key = keyStr.Value;
                if (!map.Items.TryGetValue(key, out var item))
                    return VerbResult.Ok(ZohBool.False);

                return VerbResult.Ok(new ZohBool(!item.IsNothing()));
            }

            // Cannot index other types for 'any' check? Or always false?
            // Spec implies it handles collections. For scalar, maybe index is invalid?
            // Returning false for invalid index on scalar seems safe for 'Any' check.
            return VerbResult.Ok(ZohBool.False);
        }

        return VerbResult.Ok(new ZohBool(!value.IsNothing()));
    }
}
