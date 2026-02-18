using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Validation.CoreVerbs;

public class SetValidator : IVerbValidator
{
    public string VerbName => "set";
    public int Priority => 100;

    public IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story)
    {
        var diagnostics = new List<Diagnostic>();

        // Check parameter count
        if (call.UnnamedParams.Length + call.NamedParams.Count < 1)
        {
            diagnostics.Add(new Diagnostic(
               DiagnosticSeverity.Fatal,
               "missing_parameter",
               "/set requires at least a variable name",
               call.Start,
               story.Name
           ));
            return diagnostics;
        }

        // Validate first parameter is Reference or String (variable name)
        // /set *var, val;  -> *var is Reference
        // /set "var", val; -> "var" is String

        var firstParam = call.UnnamedParams.Length > 0 ? call.UnnamedParams[0] : call.NamedParams.Values.FirstOrDefault();

        if (firstParam != null && !(firstParam is ValueAst.Reference || firstParam is ValueAst.String))
        {
            diagnostics.Add(new Diagnostic(
               DiagnosticSeverity.Fatal,
               "invalid_type",
               "/set first parameter must be a reference (*var) or string identifier",
               call.Start,
               story.Name
           ));
        }

        // Validate [typed] attribute
        // call.Attributes is ImmutableArray<AttributeAst>
        foreach (var attr in call.Attributes)
        {
            if (attr.Name.Equals("typed", StringComparison.OrdinalIgnoreCase))
            {
                if (attr.Value is ValueAst.String s)
                {
                    var type = s.Value.ToLowerInvariant();
                    // Valid types per spec
                    var validTypes = new HashSet<string> { "nothing", "boolean", "integer", "double", "string", "list", "map", "verb", "channel" };
                    if (!validTypes.Contains(type))
                    {
                        diagnostics.Add(new Diagnostic(
                           DiagnosticSeverity.Fatal,
                           "invalid_attribute_value",
                           $"Invalid type for [typed]: {type}. Valid types: {string.Join(", ", validTypes)}",
                           attr.Position,
                           story.Name
                       ));
                    }
                }
                else
                {
                    diagnostics.Add(new Diagnostic(
                       DiagnosticSeverity.Fatal,
                       "invalid_attribute_value",
                       "[typed] attribute must be a string",
                       attr.Position,
                       story.Name
                   ));
                }
            }
        }

        return diagnostics;
    }
}
