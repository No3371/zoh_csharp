using System;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow
{
    public class WhileDriver : IVerbDriver
    {
        public string Namespace => "core.flow";
        public string Name => "while";

        public DriverResult Execute(IExecutionContext context, VerbCallAst call)
        {
            if (call.UnnamedParams.Length < 2)
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "parameter_not_found", "Use: /while condition_expr, verb", call.Start));
            }

            var conditionParam = call.UnnamedParams[0];
            var verbVal = ValueResolver.Resolve(call.UnnamedParams[1], context);

            if (!(verbVal is ZohVerb verbToRun))
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Second argument must be a verb.", call.Start));
            }

            // Spec compliance: /while does NOT support breakif/continueif.
            // Loop runs as long as condition matches.

            while (true)
            {
                // 1. Resolve subject
                var subjectVal = ValueResolver.Resolve(conditionParam, context);

                // If subject is a verb, execute it to get the value
                if (subjectVal is ZohVerb vSubject)
                {
                    subjectVal = context.ExecuteVerb(vSubject.VerbValue, context).ValueOrNothing;
                }

                // 2. Resolve comparison value ('is' param), default true
                ZohValue compareVal;
                if (call.NamedParams.TryGetValue("is", out var isParamAst))
                {
                    compareVal = ValueResolver.Resolve(isParamAst, context);
                }
                else
                {
                    compareVal = ZohBool.True;
                    
                    // Spec type validation when comparing against default true
                    if (!(subjectVal is ZohBool) && !(subjectVal is ZohNothing))
                    {
                        return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", $"Condition must be a boolean or nothing when no 'is' parameter is provided. Got: {subjectVal.GetTypeString()}", call.Start));
                    }
                }

                // 3. Compare
                if (compareVal is ZohStr s && ValueExtensions.IsTypeKeyword(s.Value))
                {
                    // Type checking
                    if (subjectVal.GetTypeString() != s.Value)
                    {
                        break;
                    }
                }
                else
                {
                    // Value checking
                    if (!subjectVal.Equals(compareVal))
                    {
                        break;
                    }
                }

                // 4. Execute body
                var result = context.ExecuteVerb(verbToRun.VerbValue, context);
                if (result.IsFatal) return result;
            }

            return DriverResult.Complete.Ok();
        }
    }
}
