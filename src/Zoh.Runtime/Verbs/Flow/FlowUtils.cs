using System;
using System.Linq;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Parsing.Ast;

namespace Zoh.Runtime.Verbs.Flow
{
    public static class FlowUtils
    {
        public static bool ShouldBreak(VerbCallAst call, IExecutionContext context)
        {
            if (call.NamedParams.TryGetValue("breakif", out var val))
            {
                var resolved = ValueResolver.Resolve(val, context);
                return resolved.IsTruthy();
            }
            return false;
        }

        public static bool ShouldContinue(VerbCallAst call, IExecutionContext context)
        {
            if (call.NamedParams.TryGetValue("continueif", out var val))
            {
                var resolved = ValueResolver.Resolve(val, context);
                return resolved.IsTruthy();
            }
            return false;
        }
    }
}
