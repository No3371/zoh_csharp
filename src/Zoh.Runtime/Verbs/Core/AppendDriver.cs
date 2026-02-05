using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Helpers;


namespace Zoh.Runtime.Verbs.Core;

public class AppendDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "append";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        var paramsList = verb.UnnamedParams;
        if (paramsList.Length < 2)
        {
            return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "missing_param", "Usage: /append collection, value;", verb.Start));
        }

        var collectionParam = paramsList[0];

        if (collectionParam is not ValueAst.Reference refAst)
        {
            return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "invalid_type", "First argument must be a variable reference (e.g. *list)", verb.Start));
        }

        try
        {
            // 1. Get Target Value (resolving path)
            var current = CollectionHelpers.GetAtPath(context, refAst.Name, refAst.Path);

            // 2. Perform Mutation
            var valueToAppend = ValueResolver.Resolve(paramsList[1], context);

            if (current is ZohList list)
            {
                var newList = list.Items.Add(valueToAppend);
                var newZohList = new ZohList(newList);

                // 3. Set Back
                // Use SetAtPath to update the Nested structure correctly
                // explicitly null scope to let VariableStore decide (defaults to Story/shadowing) or use what SetAtPath determines
                return CollectionHelpers.SetAtPath(context, refAst.Name, refAst.Path, newZohList);
            }

            if (current is IZohMap map)
            {
                // valueToAppend must be map {"key": val} (IZohMap with count 1)
                if (valueToAppend is not IZohMap appendMap || appendMap.Count != 1)
                {
                    return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "invalid_type", "Appending to a map requires a single-entry map value (e.g. {\"key\": value})", verb.Start));
                }

                var kvp = appendMap.Entries.First();

                ImmutableDictionary<string, ZohValue> newMapItems;
                if (map is ZohMap zm)
                {
                    newMapItems = zm.Items.SetItem(kvp.Key, kvp.Value);
                }
                else // ZohKvPair
                {
                    newMapItems = ImmutableDictionary<string, ZohValue>.Empty
                        .AddRange(map.Entries)
                        .SetItem(kvp.Key, kvp.Value);
                }

                var newZohMap = new ZohMap(newMapItems);

                // 3. Set Back
                var setResult = CollectionHelpers.SetAtPath(context, refAst.Name, refAst.Path, newZohMap);
                if (!setResult.IsSuccess) return setResult;

                return VerbResult.Ok(new ZohInt(newMapItems.Count));
            }

            return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "invalid_type", $"Cannot append to type {current.Type}", verb.Start));
        }
        catch (ZohDiagnosticException ex)
        {
            return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, ex.DiagnosticCode, ex.Message, verb.Start));
        }
    }
}

