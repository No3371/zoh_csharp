using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs.Standard.Media;

public record PauseRequest(string Id);

public interface IPauseHandler
{
    void OnPause(IExecutionContext context, PauseRequest request);
}
