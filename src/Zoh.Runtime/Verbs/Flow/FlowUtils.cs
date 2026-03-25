using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Verbs.Flow
{
    public static class FlowUtils
    {
        public static bool ShouldBreak(VerbCallAst call, IExecutionContext context)
        {
            if (call.NamedParams.TryGetValue("breakif", out var val))
                return ResolveConditionValue(val, context).IsTruthy();
            return false;
        }

        public static bool ShouldContinue(VerbCallAst call, IExecutionContext context)
        {
            if (call.NamedParams.TryGetValue("continueif", out var val))
                return ResolveConditionValue(val, context).IsTruthy();
            return false;
        }

        private static ZohValue ResolveConditionValue(ValueAst val, IExecutionContext context)
        {
            var resolved = ValueResolver.Resolve(val, context);
            if (resolved is ZohVerb condVerb)
                resolved = context.ExecuteVerb(condVerb.VerbValue, context).ValueOrNothing;
            return resolved;
        }
    }
}
