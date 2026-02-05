using System.Collections.Generic;
using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Helpers;

public static class CollectionHelpers
{
    private const int MAX_RECURSION_DEPTH = 20;

    public static ZohValue GetIndex(ZohValue collection, ZohValue index)
    {
        switch (collection, index)
        {
            case (ZohList list, ZohInt i):
                long idx = i.Value;
                if (idx < 0) idx += list.Items.Length;

                if (idx >= 0 && idx < list.Items.Length)
                {
                    return list.Items[(int)idx];
                }
                return ZohValue.Nothing;

            case (ZohMap map, ZohStr key):
                return map.Items.TryGetValue(key.Value, out var val) ? val : ZohValue.Nothing;

            default:
                return ZohValue.Nothing;
        }
    }

    public static ZohValue GetAtPath(IExecutionContext context, string varName, ImmutableArray<ValueAst> pathAst)
    {
        var value = context.Variables.Get(varName);

        foreach (var pathElement in pathAst)
        {
            var index = ValueResolver.Resolve(pathElement, context);
            // Implicitly resolve expressions in path
            int depth = 0;
            while (index is ZohExpr expr)
            {
                if (++depth > MAX_RECURSION_DEPTH) throw new ZohDiagnosticException("runtime_error", "Maximum recursion depth exceeded during index resolution.");
                index = ValueResolver.Resolve(expr.ast, context);
            }

            // Type Validation
            if (value is ZohList && index is not ZohInt)
                throw new ZohDiagnosticException("invalid_index_type", $"List index must be integer, got {index.Type}");
            if (value is ZohMap && index is not ZohStr)
                throw new ZohDiagnosticException("invalid_index_type", $"Map index must be string, got: {index.Type}");
            if (value is not ZohList && value is not ZohMap)
                throw new ZohDiagnosticException("invalid_index_type", $"Cannot index into type {value.Type}");

            value = GetIndex(value, index);
            if (value is ZohNothing) return ZohValue.Nothing;
        }

        return value;
    }

    public static VerbResult SetAtPath(IExecutionContext context, string varName, ImmutableArray<ValueAst> pathAst, ZohValue value, Scope? scope = null)
    {
        if (pathAst.IsEmpty)
        {
            try
            {
                context.Variables.Set(varName, value, scope);
                return VerbResult.Ok();
            }
            catch (System.InvalidOperationException ex)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "type_mismatch", ex.Message, new Lexing.TextPosition(0, 0, 0)));
            }
        }

        var root = context.Variables.Get(varName);
        if (root is ZohNothing)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index", $"Variable '{varName}' does not exist", new Lexing.TextPosition(0, 0, 0)));
        }

        var indices = new List<ZohValue>();
        foreach (var p in pathAst)
        {
            var idx = ValueResolver.Resolve(p, context);
            // Implicitly resolve expressions in path
            int depth = 0;
            while (idx is ZohExpr expr)
            {
                if (++depth > MAX_RECURSION_DEPTH) throw new ZohDiagnosticException("runtime_error", "Maximum recursion depth exceeded during index resolution.");
                idx = ValueResolver.Resolve(expr.ast, context);
            }
            indices.Add(idx);
        }

        var result = Reconstruct(root, indices, 0, value);

        if (result.IsFatal) return result;
        if (result.Value is null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "internal_error", "Reconstruction returned null", new Lexing.TextPosition(0, 0, 0)));

        try
        {
            context.Variables.Set(varName, result.Value, scope);
            return VerbResult.Ok();
        }
        catch (System.Exception ex)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "error", ex.Message, new Lexing.TextPosition(0, 0, 0)));
        }
    }

    private static VerbResult Reconstruct(ZohValue current, List<ZohValue> indices, int depth, ZohValue newValue)
    {
        if (depth == indices.Count)
        {
            return new VerbResult(newValue, ImmutableArray<Diagnostic>.Empty);
        }

        var index = indices[depth];

        if (current is ZohList list)
        {
            if (index is ZohInt i)
            {
                int idx = (int)i.Value;
                if (idx < 0 || idx >= list.Items.Length)
                {
                    return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index", $"Index out of bounds: {idx}", new Lexing.TextPosition(0, 0, 0)));
                }

                var element = list.Items[idx];
                var newElementResult = Reconstruct(element, indices, depth + 1, newValue);
                if (newElementResult.IsFatal) return newElementResult;

                var newItems = list.Items.SetItem(idx, newElementResult.Value!);
                return new VerbResult(new ZohList(newItems), ImmutableArray<Diagnostic>.Empty);
            }
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index_type", $"List index must be integer, got {index.Type}", new Lexing.TextPosition(0, 0, 0)));
        }
        else if (current is ZohMap map)
        {
            string keyStr;
            if (index is ZohStr s)
            {
                keyStr = s.Value;
            }
            else
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index_type", $"Map index must be string, got: {index.Type}", new Lexing.TextPosition(0, 0, 0)));
            }

            var childExists = map.Items.TryGetValue(keyStr, out var child);
            if (!childExists)
            {
                if (depth < indices.Count - 1)
                {
                    return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index", $"Path element '{keyStr}' does not exist", new Lexing.TextPosition(0, 0, 0)));
                }
                child = ZohValue.Nothing;
            }

            var newChildResult = Reconstruct(child!, indices, depth + 1, newValue);
            if (newChildResult.IsFatal) return newChildResult;

            var newItems = map.Items.SetItem(keyStr, newChildResult.Value!);
            return new VerbResult(new ZohMap(newItems), ImmutableArray<Diagnostic>.Empty);
        }

        return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_index_type", $"Cannot index into type {current.Type}", new Lexing.TextPosition(0, 0, 0)));
    }
}
