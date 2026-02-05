using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Expressions;

namespace Zoh.Runtime.Verbs.Core;

public class SetDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "set";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /set *var value;
        // /set *var[0] value;
        // /set [scope: "story"] [typed: "integer"] [required] [resolve] [OneOf: [1,2,3]] *var value;

        Scope? targetScope = null;
        string? typedAs = null;
        bool required = false;
        bool resolve = false;
        ZohList? oneOfList = null;

        // 1. Process Attributes
        foreach (var attr in verb.Attributes)
        {
            var attrName = attr.Name.ToLowerInvariant();
            switch (attrName)
            {
                case "scope":
                    var scopeVal = attr.Value != null ? ValueResolver.Resolve(attr.Value, context) : ZohValue.True;
                    if (scopeVal is ZohStr s)
                    {
                        if (s.Value.Equals("story", StringComparison.OrdinalIgnoreCase)) targetScope = Scope.Story;
                        else if (s.Value.Equals("context", StringComparison.OrdinalIgnoreCase)) targetScope = Scope.Context;
                    }
                    break;
                case "typed":
                    var typeVal = attr.Value != null ? ValueResolver.Resolve(attr.Value, context) : null;
                    if (typeVal is ZohStr ts) typedAs = ts.Value.ToLowerInvariant();
                    break;
                case "required":
                    required = true;
                    break;
                case "resolve":
                    resolve = true;
                    break;
                case "oneof":
                    var oneOfVal = attr.Value != null ? ValueResolver.Resolve(attr.Value, context) : null;
                    if (oneOfVal is ZohList l) oneOfList = l;
                    break;
            }
        }

        string? targetName = null;
        ImmutableArray<ValueAst> targetPath = ImmutableArray<ValueAst>.Empty;
        ZohValue? targetValue = null;

        // 2. Process Unnamed Params (Positional)
        if (verb.UnnamedParams.Length > 0)
        {
            var p0 = verb.UnnamedParams[0];

            // First param: Target (Reference ONLY)
            if (p0 is ValueAst.Reference r)
            {
                targetName = r.Name;
                targetPath = r.Path;
            }
            else
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Set target must be a variable reference (*var)", verb.Start));
            }

            // Second param: Value (Optional, default Nothing)
            if (verb.UnnamedParams.Length > 1)
            {
                var valAst = verb.UnnamedParams[1];

                if (resolve)
                {
                    // [resolve] present: always resolve
                    targetValue = ValueResolver.Resolve(valAst, context);
                }
                else
                {
                    // [resolve] absent: check for Code-as-Data types
                    if (valAst is ValueAst.Expression exprAst)
                    {
                        // Store as separate ZohExpr entity wrapping the AST node
                        targetValue = new ZohExpr(exprAst);
                    }
                    else if (valAst is ValueAst.Verb verbAst)
                    {
                        // Store as ZohVerb
                        targetValue = new ZohVerb(verbAst.Call);
                    }
                    else
                    {
                        // Literal or Ref -> Resolve normally
                        targetValue = ValueResolver.Resolve(valAst, context);
                    }
                }
            }
            else
            {
                targetValue = ZohValue.Nothing;
            }
        }
        else
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Usage: /set *var [value]", verb.Start));
        }

        // 3. Handle [required] attribute logic:
        bool hasValueProvided = targetValue != null && !targetValue.IsNothing();

        if (required && !hasValueProvided)
        {
            // Check if variable exists
            var existing = Zoh.Runtime.Helpers.CollectionHelpers.GetAtPath(context, targetName, targetPath);
            if (existing.IsNothing())
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "required", "Variable is required but not set and no value provided", verb.Start));
            }
        }

        // 4. Handle [typed] attribute
        if (typedAs != null && hasValueProvided)
        {
            var actualType = targetValue!.Type.ToString().ToLowerInvariant();
            if (actualType != typedAs)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Expected {typedAs}, got {actualType}", verb.Start));
            }
        }

        // 5. Handle [OneOf] attribute
        if (oneOfList != null && hasValueProvided)
        {
            bool found = false;
            foreach (var item in oneOfList.Items)
            {
                if (item.Equals(targetValue)) { found = true; break; }
            }
            if (!found)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_value", "Value not in allowed list", verb.Start));
            }
        }

        // 6. Perform Set via Helpers
        return Zoh.Runtime.Helpers.CollectionHelpers.SetAtPath(context, targetName, targetPath, targetValue ?? ZohValue.Nothing, targetScope);
    }
}
