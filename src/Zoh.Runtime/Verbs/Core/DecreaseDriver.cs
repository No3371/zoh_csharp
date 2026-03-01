using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Verbs.Core;

public class DecreaseDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "decrease";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /decrease *var [amount];
        return IncreaseDriver.ModifyVariable(context, verb, -1);
    }
}
