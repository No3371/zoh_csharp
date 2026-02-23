using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs.Standard.Media;

public record PlayRequest(
    string Resource,
    string Id,
    double Volume,
    int Loops,
    string Easing);

public interface IPlayHandler
{
    void OnPlay(IExecutionContext context, PlayRequest request);
}
