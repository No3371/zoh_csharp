using Zoh.Runtime.Execution;
using System.Collections.Generic;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public record ConverseRequest(
    string? Speaker,
    string? Portrait,
    bool Append,
    string Style,
    double? TimeoutMs,
    IReadOnlyList<string> Contents);

public interface IConverseHandler
{
    void OnConverse(IExecutionContext context, ConverseRequest request);
}
