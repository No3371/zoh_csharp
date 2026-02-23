using Zoh.Runtime.Execution;

namespace Zoh.Runtime.Verbs.Standard.Media;

public record ShowRequest(
    string Resource,
    string Id,
    double? RelativeWidth,
    double? RelativeHeight,
    double? Width,
    double? Height,
    string Anchor,
    double PosX,
    double PosY,
    double PosZ,
    double FadeDuration,
    double Opacity,
    string Easing);

public interface IShowHandler
{
    void OnShow(IExecutionContext context, ShowRequest request);
}
