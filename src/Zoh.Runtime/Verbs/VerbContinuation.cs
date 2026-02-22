using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs;

/// <summary>
/// Describes what a driver is waiting for. Returned via VerbResult.Continuation.
/// The runtime calls Context.Block(continuation) to apply blocking state.
/// Drivers must NOT call IExecutionContext.SetState() directly.
/// </summary>
public abstract record VerbContinuation;

/// <summary>/sleep — block for a fixed duration.</summary>
public sealed record SleepContinuation(double DurationMs) : VerbContinuation;

/// <summary>/wait — block until a named signal is received.</summary>
public sealed record MessageContinuation(string MessageName, double? TimeoutMs = null) : VerbContinuation;

/// <summary>/call — block until a child context terminates.</summary>
public sealed record ContextContinuation(IExecutionContext ChildContext) : VerbContinuation;

/// <summary>Presentation/interactive verbs — block until the host application provides input.</summary>
public sealed record HostContinuation(string InteractionType) : VerbContinuation;
