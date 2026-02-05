using System.Collections.Immutable;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs;

public sealed record VerbResult(ZohValue Value, ImmutableArray<Diagnostic> Diagnostics)
{
    public bool IsSuccess => !Diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Error);
    public bool IsFatal => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal);

    public static VerbResult Ok(ZohValue? value = null)
        => new(value ?? ZohNothing.Instance, ImmutableArray<Diagnostic>.Empty);

    public static VerbResult WithDiagnostics(ZohValue value, IEnumerable<Diagnostic> diagnostics)
        => new(value, diagnostics.ToImmutableArray());

    public static VerbResult Error(ZohValue value, params Diagnostic[] diagnostics)
         => new(value, diagnostics.ToImmutableArray());

    public static VerbResult Fatal(Diagnostic diagnostic)
        => new(ZohNothing.Instance, ImmutableArray.Create(diagnostic));
}
