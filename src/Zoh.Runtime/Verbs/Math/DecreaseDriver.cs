using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Verbs.Math;

public class DecreaseDriver : IVerbDriver
{
    public string Namespace => "core.math";
    public string Name => "decrease";

    public DriverResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        // /decrease *var [amount];
        return IncreaseDriver.ModifyVariable(context, verb, -1);
    }
}
