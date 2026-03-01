using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs;

public abstract record WaitOutcome;
public sealed record WaitCompleted(ZohValue Value) : WaitOutcome;
public sealed record WaitTimedOut : WaitOutcome;
public sealed record WaitCancelled(string Code, string Message) : WaitOutcome;
