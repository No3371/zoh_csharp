using System.Collections.Immutable;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Preprocessing;

/// <summary>
/// Result of a preprocessing step.
/// </summary>
public record PreprocessorResult(
    string ProcessedText,
    SourceMap? SourceMap,
    ImmutableArray<Diagnostic> Diagnostics
)
{
    public bool Success => Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error && d.Severity != DiagnosticSeverity.Fatal);
}
