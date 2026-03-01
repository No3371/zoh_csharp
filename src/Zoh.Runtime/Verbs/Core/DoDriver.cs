using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Core;

public class DoDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "do";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /do *verb_ref;

        if (verb.UnnamedParams.Length == 0)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Error, "MissingArguments", "Usage: /do *verb", verb.Start));

        var param = verb.UnnamedParams[0];
        // We want the VALUE to be a VerbValue (not yet implemented in ValueResolver?? I should check ZohValue types)

        // Actually DoDriver usually takes a reference to a verb, or a verb literal?
        // ZohValue has ZohVerb? Yes/No? Let's check ZohValueTypes.
        // Assuming there isn't one clearly defined yet, but `ValueAst.VerbCall` exists in AST.
        // If ValueResolver resolves a VerbCallAst, what does it return?
        // Checking ValueResolver... it doesn't handle ValueAst.VerbType? 
        // Wait, AST has `ValueAst.VerbCall`? No, AST has `VerbCallAst`.
        // A value *can* be a verb call in block form?

        // Implementation detail: "A verb call is an objectified verb invocation command... Denoted as /verb..."
        // If we have `ValueAst` that wraps a verb call, resolving it might execute it if not careful, OR allow storing it.

        // For now, let's assume `ValueResolver.Resolve` might return something that represents a verb, OR we check the AST directly.
        // But `param` is `ValueAst`. If it's `ValueAst.Reference`, we resolve it.

        var val = ValueResolver.Resolve(param, context);

        // We need a ZohValue that holds a verb.
        if (val is ZohVerb v)
        {
            // Execute the verb
            return context.ExecuteVerb(v.VerbValue, context);
        }

        return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Error, "invalid_type", $"Expected verb, got {val.Type}", verb.Start));
    }
}
