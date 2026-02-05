using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class HasDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "has";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        var paramsList = verb.UnnamedParams;
        if (paramsList.Length < 2)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, "missing_param", "Usage: /has collection, item;", verb.Start));
        }

        var collection = ValueResolver.Resolve(paramsList[0], context);
        var subject = ValueResolver.Resolve(paramsList[1], context);

        if (collection is ZohList list)
        {
            // Check if subject exists in list
            foreach (var item in list.Items)
            {
                // Equality check? ZohValue.Equals?
                // Record equality for simple types.
                if (item.Equals(subject)) return VerbResult.Ok(ZohBool.True);
            }
            return VerbResult.Ok(ZohBool.False);
        }

        if (collection is ZohMap map)
        {
            // Check if MAP HAS KEY (subject as string)
            // Strict: subject must be string
            if (subject is not ZohStr s)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index_type", $"Map key must be string, got: {subject.Type}", verb.Start));
            }
            string key = s.Value;
            return VerbResult.Ok(new ZohBool(map.Items.ContainsKey(key)));
        }

        return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, "invalid_type", $"Expected list or map, got: {collection.Type}", verb.Start));
    }
}
