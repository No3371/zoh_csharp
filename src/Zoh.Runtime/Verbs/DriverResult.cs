using System.Collections.Immutable;
using Zoh.Runtime.Diagnostics;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs;

public abstract record DriverResult
{
    public abstract bool IsSuccess { get; }
    public abstract bool IsFatal { get; }

    /// <summary>
    /// Returns the value if this is a Complete result; ZohNothing otherwise.
    /// </summary>
    public ZohValue ValueOrNothing => this is Complete c ? c.Value : ZohNothing.Instance;

    /// <summary>
    /// Returns diagnostics from the result, or empty if not applicable.
    /// Note: uses a non-conflicting name to avoid record-property shadowing recursion.
    /// </summary>
    public ImmutableArray<Diagnostic> DiagnosticsOrEmpty => this switch
    {
        Complete c => c.Diagnostics,
        Suspend s => s.Diagnostics,
        _ => ImmutableArray<Diagnostic>.Empty
    };

    public sealed record Complete(ZohValue Value, ImmutableArray<Diagnostic> Diagnostics) : DriverResult
    {
        public override bool IsSuccess => !Diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Error);
        public override bool IsFatal => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal);

        public static Complete Ok(ZohValue? value = null)
            => new(value ?? ZohNothing.Instance, ImmutableArray<Diagnostic>.Empty);

        public static Complete Fatal(Diagnostic diagnostic)
            => new(ZohNothing.Instance, ImmutableArray.Create(diagnostic));

        public static Complete WithDiagnostics(ZohValue value, IEnumerable<Diagnostic> diagnostics)
            => new(value, diagnostics.ToImmutableArray());

        public static Complete Error(ZohValue value, params Diagnostic[] diagnostics)
            => new(value, diagnostics.ToImmutableArray());
    }

    public sealed record Suspend(Continuation Continuation, ImmutableArray<Diagnostic> Diagnostics) : DriverResult
    {
        public override bool IsSuccess => !Diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Error);
        public override bool IsFatal => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Fatal);

        public Suspend(Continuation continuation)
            : this(continuation, ImmutableArray<Diagnostic>.Empty) { }
    }
}
