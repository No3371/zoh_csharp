using Zoh.Runtime.Types;
using Zoh.Runtime.Execution;
using System.Collections.Generic;

namespace Zoh.Runtime.Verbs.Standard.Presentation;

public interface IChooseFromHandler
{
    void OnChooseFrom(IExecutionContext context, ChooseRequest request);
}
