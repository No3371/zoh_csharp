using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs.Standard.Media;

public record StopRequest(
    string? Id,
    double FadeDuration,
    string Easing);

public interface IStopHandler
{
    void OnStop(IExecutionContext context, StopRequest request);
}
