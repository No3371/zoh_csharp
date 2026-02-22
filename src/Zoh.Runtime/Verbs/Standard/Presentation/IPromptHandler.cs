using Zoh.Runtime.Execution;
using System.Collections.Generic;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public record PromptRequest(
    string Style,
    string? PromptText,
    double? TimeoutMs);

public interface IPromptHandler
{
    void OnPrompt(IExecutionContext context, PromptRequest request);
}
