using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs.Standard.Media;

public record HideRequest(
    string Id,
    double FadeDuration,
    string Easing);

public interface IHideHandler
{
    void OnHide(IExecutionContext context, HideRequest request);
}
