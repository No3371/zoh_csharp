using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs.Standard.Media;

public record PlayOneRequest(
    string Resource,
    double Volume,
    int Loops);

public interface IPlayOneHandler
{
    void OnPlayOne(IExecutionContext context, PlayOneRequest request);
}
