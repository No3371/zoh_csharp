using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Verbs;

public interface IVerbDriver
{
    string Namespace { get; }
    string Name { get; }

    VerbResult Execute(IExecutionContext context, VerbCallAst verbCall);
}
