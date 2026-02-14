using System.Collections;
using System.Collections.Immutable;
using Zoh.Runtime.Lexing;

namespace Zoh.Runtime.Diagnostics;

public class DiagnosticBag : IEnumerable<Diagnostic>
{
    private readonly List<Diagnostic> _diagnostics = new();

    public void Report(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public void ReportInfo(string code, string message, TextPosition pos, string? file = null)
        => Report(new Diagnostic(DiagnosticSeverity.Info, code, message, pos, file));

    public void ReportWarning(string code, string message, TextPosition pos, string? file = null)
        => Report(new Diagnostic(DiagnosticSeverity.Warning, code, message, pos, file));

    public void ReportError(string code, string message, TextPosition pos, string? file = null)
        => Report(new Diagnostic(DiagnosticSeverity.Error, code, message, pos, file));

    public void ReportFatal(string code, string message, TextPosition pos, string? file = null)
        => Report(new Diagnostic(DiagnosticSeverity.Fatal, code, message, pos, file));

    public void AddRange(IEnumerable<Diagnostic> diagnostics) => _diagnostics.AddRange(diagnostics);

    public ImmutableArray<Diagnostic> ToImmutable() => _diagnostics.ToImmutableArray();

    public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool HasErrors => _diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Error);
    public bool HasFatalErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal);
    public int Count => _diagnostics.Count;
}
