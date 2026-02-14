using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Validation;

/// <summary>
/// Validates compiled verb calls for a specific verb.
/// </summary>
public interface IVerbValidator
{
    string VerbName { get; }
    int Priority { get; }
    IReadOnlyList<Diagnostic> Validate(VerbCallAst call, CompiledStory story);
}
