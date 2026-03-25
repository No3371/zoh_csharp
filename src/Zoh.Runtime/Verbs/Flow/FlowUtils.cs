using Zoh.Runtime.Execution;
using Zoh.Runtime.Types;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Verbs.Flow
{
    public static class FlowUtils
    {
        /// <summary>
        /// Returns null (no breakif param or condition falsy), DriverResult.Complete.Ok() (break),
        /// DriverResult.Suspend (propagate), or fatal DriverResult (propagate).
        /// </summary>
        public static DriverResult? EvaluateBreakIf(VerbCallAst call, IExecutionContext context)
        {
            if (!call.NamedParams.TryGetValue("breakif", out var val))
                return null;
            return EvaluateCondition(val, context);
        }

        /// <summary>
        /// Same contract as EvaluateBreakIf but for continueif.
        /// </summary>
        public static DriverResult? EvaluateContinueIf(VerbCallAst call, IExecutionContext context)
        {
            if (!call.NamedParams.TryGetValue("continueif", out var val))
                return null;
            return EvaluateCondition(val, context);
        }

        private static DriverResult? EvaluateCondition(ValueAst val, IExecutionContext context)
        {
            var resolved = ValueResolver.Resolve(val, context);
            if (resolved is ZohVerb condVerb)
            {
                var result = context.ExecuteVerb(condVerb.VerbValue, context);
                if (result is DriverResult.Suspend)
                    return result;
                if (result.IsFatal)
                    return result;
                resolved = result.ValueOrNothing;
            }
            return resolved.IsTruthy() ? DriverResult.Complete.Ok() : null;
        }
    }
}
