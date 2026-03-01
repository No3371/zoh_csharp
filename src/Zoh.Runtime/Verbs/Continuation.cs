namespace Zoh.Runtime.Verbs;

public sealed record Continuation(
    WaitRequest Request,
    Func<WaitOutcome, DriverResult> OnFulfilled
);
