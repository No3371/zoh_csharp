using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs;
using Zoh.Runtime.Lexing;
using System.Collections.Immutable;

namespace Zoh.Runtime.Validation;

public class NamespaceValidator(VerbRegistry registry)
{
    private readonly List<ValidationError> _errors = new();

    public ValidationResult Validate(StoryAst story)
    {
        _errors.Clear();
        foreach (var statement in story.Statements)
        {
            ValidateStatement(statement);
        }
        return new ValidationResult([.. _errors]);
    }

    private void ValidateStatement(StatementAst statement)
    {
        switch (statement)
        {
            case StatementAst.VerbCall call:
                ValidateVerbCall(call.Call);
                break;
                // Add other statement types if they can contain verb calls (e.g., nested blocks?)
                // Currently VerbCallAst handles block nesting via Parameters?
                // Verify if Block body is separate.
                // Parser.cs `ParseBlockBody` returns ImmutableArray<StatementAst>. 
                // `VerbCallAst` doesn't seem to have a Body property in the snippet I saw? 
                // Wait, Parser `ParseVerbCall` calls `ParseParameters` which handles block parameter parsing.
                // But if it's a block verb, does it have statements inside?
                // "Block form: comma is optional, space/newline suffices". It parses *Values*. 
                // Control flow verbs like /if, /while take a VerbValue as a parameter (`/verb;;`).
                // So we need to validate VerbValues inside parameters.
        }
    }

    private void ValidateVerbCall(VerbCallAst call)
    {
        // 1. Resolve Verb
        // Use reconstructed full identifier
        var id = string.IsNullOrEmpty(call.Namespace) ? call.Name : $"{call.Namespace}.{call.Name}";
        var result = registry.Resolve(id);

        if (result.Status == ResolutionStatus.NotFound)
        {
            _errors.Add(new ValidationError(call.Start, $"Unknown verb: /{id}"));
        }
        else if (result.Status == ResolutionStatus.Ambiguous)
        {
            var candidates = string.Join(", ", result.Candidates.Select(c => $"{c.Namespace}.{c.Name}"));
            _errors.Add(new ValidationError(call.Start, $"Ambiguous verb: /{id}. Matches: {candidates}"));
        }

        // 2. Validate Attributes
        foreach (var attr in call.Attributes)
        {
            // Attributes are also identified by name (namespace support TBD in spec? "Attributes are optional metadata... [name]")
            // Can attributes be namespaced? Spec says "Identifiers (dot separated)".
            // But Attributes don't invoke Drivers directly via Registry in the same way? 
            // Or does Registry also store Attributes?
            // `VerbRegistry` only stores `IVerbDriver`.
            // There is no `IAttributeDriver`.
            // Attributes are processed by the Verbs themselves (e.g. `getAttribute(call, "scope")`).
            // So we generally cannot validate Attributes against a registry unless we have an AttributeRegistry.
            // Current codebase doesn't seem to have AttributeRegistry.
            // So we skip Attribute validation for now.
        }

        // 3. Recursive Validation (Parameters)
        foreach (var param in call.NamedParams.Values)
        {
            ValidateValue(param);
        }
        foreach (var param in call.UnnamedParams)
        {
            ValidateValue(param);
        }
    }

    private void ValidateValue(ValueAst value)
    {
        switch (value)
        {
            case ValueAst.Verb v:
                ValidateVerbCall(v.Call);
                break;
            case ValueAst.List l:
                foreach (var item in l.Elements) ValidateValue(item);
                break;
            case ValueAst.Map m:
                foreach (var (k, v) in m.Entries) { ValidateValue(k); ValidateValue(v); }
                break;
                // What about Expressions? 
                // Expressions are evaluated at runtime. 
                // We can't statically validate function calls inside expressions effectively without Expression analysis.
                // If they contain `$(...)` which calls a verb? 
                // ZOH expressions don't call verbs directly usually. 
                // Refer to spec: special forms like interpolate, count.
                // Interpolate `${*var}` is var reference. 
                // There is no "function call" syntax in expressions that maps to Verbs?
                // `/evaluate` runs expressions.
                // So we are safe ignoring expressions for Verb resolution updates.
        }
    }
}

public record ValidationResult(ImmutableArray<ValidationError> Errors)
{
    public bool IsSuccess => Errors.Length == 0;
}

public record ValidationError(TextPosition Position, string Message);
