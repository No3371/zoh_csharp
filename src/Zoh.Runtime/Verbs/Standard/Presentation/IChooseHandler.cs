using Zoh.Runtime.Types;
using Zoh.Runtime.Execution;
using System.Collections.Generic;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public record ChoiceItem(string Text, ZohValue Value);

public record ChooseRequest(
    string? Speaker,
    string? Portrait,
    string Style,
    string? Prompt,
    double? TimeoutMs,
    IReadOnlyList<ChoiceItem> Choices,
    string? Tag = null);

public interface IChooseHandler
{
    void OnChoose(ContextHandle handle, ChooseRequest request);
}
