namespace Zoh.Runtime.Execution;

public enum ContextState
{
    Running,
    WaitingChannel,
    WaitingMessage,
    WaitingContext,
    Sleeping,
    WaitingHost,
    Terminated
}
