using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs.Standard.Media;

public record SetVolumeRequest(
    string Id,
    double Volume,
    double FadeDuration,
    string Easing);

public interface ISetVolumeHandler
{
    void OnSetVolume(IExecutionContext context, SetVolumeRequest request);
}
