using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Helpers;

namespace Zoh.Runtime.Verbs.Core;

// /insert collection, index, value
public class InsertDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "insert";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        if (verb.UnnamedParams.Length < 3)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /insert collection, index, value", verb.Start));

        if (verb.UnnamedParams[0] is not ValueAst.Reference refAst)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "First argument must be a reference", verb.Start));

        try
        {
            var collection = CollectionHelpers.GetAtPath(context, refAst.Name, refAst.Path);

            var indexVal = ValueResolver.Resolve(verb.UnnamedParams[1], context);
            var value = ValueResolver.Resolve(verb.UnnamedParams[2], context);

            if (collection is ZohList list)
            {
                if (indexVal is not ZohInt idx) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index", "Index must be integer", verb.Start));

                int i = (int)idx.Value;
                if (i < 0) i += list.Items.Length + 1;

                if (i < 0 || i > list.Items.Length) return VerbResult.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "invalid_index", $"Index {idx.Value} out of bounds", verb.Start));

                var newItems = list.Items.Insert(i, value);
                var newList = new ZohList(newItems);

                return CollectionHelpers.SetAtPath(context, refAst.Name, refAst.Path, newList);
            }

            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Expected list", verb.Start));
        }
        catch (ZohDiagnosticException ex)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, ex.DiagnosticCode, ex.Message, verb.Start));
        }
    }
}

// /remove collection, index
public class RemoveDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "remove";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        if (verb.UnnamedParams.Length < 2)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /remove collection, index", verb.Start));

        if (verb.UnnamedParams[0] is not ValueAst.Reference refAst)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "First argument must be a reference", verb.Start));

        try
        {
            var collection = CollectionHelpers.GetAtPath(context, refAst.Name, refAst.Path);
            var indexVal = ValueResolver.Resolve(verb.UnnamedParams[1], context);

            if (collection is ZohList list)
            {
                if (indexVal is not ZohInt idx) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index", "Index must be integer", verb.Start));
                int i = (int)idx.Value;
                if (i < 0) i += list.Items.Length;

                if (i >= 0 && i < list.Items.Length)
                {
                    var newItems = list.Items.RemoveAt(i);
                    var newList = new ZohList(newItems);
                    var setResult = CollectionHelpers.SetAtPath(context, refAst.Name, refAst.Path, newList);
                    if (!setResult.IsSuccess) return setResult;
                    return VerbResult.Ok(new ZohInt(newList.Items.Length));
                }
                return VerbResult.Ok(new ZohInt(list.Items.Length));
            }
            else if (collection is IZohMap map)
            {
                if (indexVal is not ZohStr keyStr)
                {
                    return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index_type", $"Map key must be string, got: {indexVal.Type}", verb.Start));
                }
                string key = keyStr.Value;

                if (map.TryGet(key, out _))
                {
                    // If it's a full map, remove item
                    if (map is ZohMap m)
                    {
                        var newItems = m.Items.Remove(key);
                        var newMap = new ZohMap(newItems);
                        var setResult = CollectionHelpers.SetAtPath(context, refAst.Name, refAst.Path, newMap);
                        if (!setResult.IsSuccess) return setResult;
                        return VerbResult.Ok(new ZohInt(newMap.Count));
                    }
                    // If it's a KvPair, since we found the key, removing it results in empty map
                    if (map is ZohKvPair)
                    {
                        var newMap = new ZohMap(ImmutableDictionary<string, ZohValue>.Empty);
                        var setResult = CollectionHelpers.SetAtPath(context, refAst.Name, refAst.Path, newMap);
                        if (!setResult.IsSuccess) return setResult;
                        return VerbResult.Ok(new ZohInt(0));
                    }
                }
                return VerbResult.Ok(new ZohInt(map.Count));
            }

            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Expected list or map", verb.Start));
        }
        catch (ZohDiagnosticException ex)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, ex.DiagnosticCode, ex.Message, verb.Start));
        }
    }
}

// /clear collection
public class ClearDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "clear";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        if (verb.UnnamedParams.Length < 1)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /clear collection", verb.Start));

        if (verb.UnnamedParams[0] is not ValueAst.Reference refAst)
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "First argument must be a reference", verb.Start));

        try
        {
            var collection = CollectionHelpers.GetAtPath(context, refAst.Name, refAst.Path);

            if (collection is ZohList)
            {
                return CollectionHelpers.SetAtPath(context, refAst.Name, refAst.Path, new ZohList(ImmutableArray<ZohValue>.Empty));
            }
            else if (collection is IZohMap)
            {
                return CollectionHelpers.SetAtPath(context, refAst.Name, refAst.Path, new ZohMap(ImmutableDictionary<string, ZohValue>.Empty));
            }

            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Expected list or map", verb.Start));
        }
        catch (ZohDiagnosticException ex)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Error, ex.DiagnosticCode, ex.Message, verb.Start));
        }
    }
}
