using System.Linq;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Store;

public class ReadDriver : IVerbDriver
{
    public string Namespace => "store";
    public string Name => "read";

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /read [required] [scope] store:name? default:value? *var1 ...;

        string? storeName = null;
        ZohValue defaultValue = ZohValue.Nothing;
        Scope? targetScope = null; // Default to null (Context rules?) or Story? Spec says "Scope of the variables are preserved" or standard /set rules. Impl doc said getScope(call). SetDriver has scope logic.

        // Named Params
        if (verb.NamedParams.TryGetValue("store", out var storeValAst))
        {
            var val = ValueResolver.Resolve(storeValAst, context);
            if (val is ZohStr s) storeName = s.Value;
        }
        if (verb.NamedParams.TryGetValue("default", out var defaultValAst))
        {
            defaultValue = ValueResolver.Resolve(defaultValAst, context);
        }

        // Attributes
        bool required = false;
        foreach (var attr in verb.Attributes)
        {
            var name = attr.Name.ToLowerInvariant();
            if (name == "required") required = true;
            else if (name == "scope")
            {
                var val = attr.Value != null ? ValueResolver.Resolve(attr.Value, context) : ZohValue.True;
                if (val is ZohStr s)
                {
                    if (s.Value.Equals("story", System.StringComparison.OrdinalIgnoreCase)) targetScope = Scope.Story;
                    else if (s.Value.Equals("context", System.StringComparison.OrdinalIgnoreCase)) targetScope = Scope.Context;
                }
            }
        }

        var refs = verb.UnnamedParams.OfType<ValueAst.Reference>().ToList();

        if (refs.Count == 0)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Read requires at least one reference parameter", verb.Start));
        }

        foreach (var varRef in refs)
        {
            // Read from storage
            var value = context.Storage.Read(storeName, varRef.Name);

            if (value == null)
            {
                if (required)
                {
                    return VerbResult.Error(ZohValue.Nothing, new Diagnostic(DiagnosticSeverity.Error, "not_found", $"Required variable not in storage: {varRef.Name}", verb.Start));
                }
                value = defaultValue;
            }

            // Type Check against existing variable
            var existing = context.Variables.Get(varRef.Name);
            // Verify IsTyped. VariableStore.Get returns ZohValue/Variable?
            // context.Variables.Get returns Variable (wrapper) or Value?
            // IExecutionContext.Variables is VariableStore. VariableStore.Get usually returns Value directly?
            // Checking VariableStore implementation needed. Assuming implicit knowledge or based on SetDriver logic. 
            // SetDriver uses `context.Variables.Get(targetName)` returning `existing`.
            // Code: `var existing = context.Variables.Get(targetName);`
            // `if (existing.IsNothing())`
            // This implies `Get` returns `ZohValue` directly since `IsNothing()` is ext method on Value.
            // BUT strict type check requires knowing if variable is *Typed*. ZohValue holds Type. 
            // But strict typing usually is metadata on Variable slot.
            // If `VariableStore` abstracts that away, we might can't check if it was *strictly* typed vs just having a value of a type.
            // However, `SetDriver` Line 122 checked `typedAs` attribute.
            // `ReadDriver` spec says: "Type check against existing typed variable".
            // If `VariableStore` doesn't expose strict typing constraint, we might skip this unless `VariableStore` throws on Set?
            // `SetDriver`: `context.Variables.Set` throws `InvalidOperationException` (line 146).
            // So if we just try to Set types that mismatch, `VariableStore` might throw, and we catch it?

            try
            {
                context.Variables.Set(varRef.Name, value, targetScope);
            }
            catch (System.InvalidOperationException ex)
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "type_mismatch", ex.Message, verb.Start));
            }
        }

        return VerbResult.Ok();
    }
}
