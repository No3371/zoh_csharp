using Zoh.Runtime.Execution;
using System.Collections.Generic;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public record PromptRequest(
    string Style,
    string? PromptText,
    double? TimeoutMs,
    string? Tag = null);

public interface IPromptHandler
{
    void OnPrompt(ContextHandle handle, PromptRequest request);
}
