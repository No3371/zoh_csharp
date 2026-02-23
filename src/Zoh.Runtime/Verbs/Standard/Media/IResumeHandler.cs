using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs.Standard.Media;

public record ResumeRequest(string Id);

public interface IResumeHandler
{
    void OnResume(IExecutionContext context, ResumeRequest request);
}
