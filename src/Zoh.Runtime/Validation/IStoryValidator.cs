using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Validation;

/// <summary>
/// Validates a compiled story before execution.
/// </summary>
public interface IStoryValidator
{
    int Priority { get; }
    IReadOnlyList<Diagnostic> Validate(CompiledStory story);
}
