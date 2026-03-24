using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;
using System.Linq;

namespace Zoh.Runtime.Verbs.Flow;

public class CallDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "call";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Call requires a valid Context.", call.Start));

        if (ctx.ContextScheduler == null)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_scheduler", "Context has no ContextScheduler to call new context.", call.Start));
        }

        string targetLabel = "";
        string? targetStoryName = null;
        var transferRefs = new List<ValueAst.Reference>();

        if (call.UnnamedParams.Length == 0)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Call requires at least 1 argument.", call.Start));
        }

        // Try parsing first element as story or label
        var firstArg = call.UnnamedParams[0];
        var val0 = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        int paramIndex = 1;

        if (val0 is ZohNothing)
        {
            // Explicit null story: `/call ?, "label", ...`
            if (call.UnnamedParams.Length < 2)
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Call requires a label after a null story.", call.Start));

            var val1 = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
            if (val1 is ZohStr s1) targetLabel = s1.Value;
            else return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Call label must be a string.", call.Start));
            paramIndex = 2;
        }
        else if (val0 is ZohStr s0)
        {
            // Could be just "label" or "story", "label"
            if (call.UnnamedParams.Length > 1)
            {
                // Check if the second argument evaluates to a string (which means first was story)
                // But Wait, what if second arg is a variable reference evaluating to something else? 
                // Zoh spec typically prefers explicit `?, "label"` for local, or `"story", "label"` for remote. 
                // But `/call "label";` is also valid for local. 
                // Let's check if param 1 is a Reference. If so, it might be a transfer param. 
                // If it is NOT a Reference, or if we evaluate it and it's a string, it might be a label.
                var arg1 = call.UnnamedParams[1];
                if (arg1 is ValueAst.Reference r)
                {
                    // If it's a reference, it's ambiguous if resolving to string. 
                    // Let's assume: if length=2 and both resolve to string, it's story, label.
                    // But if it's a Reference, it's a transfer param of the form `/call "label", *var`. 
                    targetLabel = s0.Value;
                }
                else
                {
                    var val1 = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
                    if (val1 is ZohStr s1)
                    {
                        targetStoryName = s0.Value;
                        targetLabel = s1.Value;
                        paramIndex = 2;
                    }
                    else
                    {
                        targetLabel = s0.Value;
                    }
                }
            }
            else
            {
                targetLabel = s0.Value;
            }
        }
        else
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Call target must be a string or nothing.", call.Start));
        }

        // Collect transfer refs
        for (int i = paramIndex; i < call.UnnamedParams.Length; i++)
        {
            if (call.UnnamedParams[i] is ValueAst.Reference refAst)
            {
                transferRefs.Add(refAst);
            }
            else
            {
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Call transfer parameters must be references.", call.Start));
            }
        }

        bool isClone = call.Attributes.Any(a => a.Name.Equals("clone", StringComparison.OrdinalIgnoreCase));

        Context newCtx;
        if (isClone)
        {
            newCtx = ctx.Clone();
        }
        else
        {
            var store = new VariableStore(new Dictionary<string, Variable>());
            newCtx = new Context(store, ctx.Storage, ctx.ChannelManager, ctx.SignalManager)
            {
                Runtime = ctx.Runtime,
                VerbExecutor = ctx.VerbExecutor,
                StoryLoader = ctx.StoryLoader,
                ContextScheduler = ctx.ContextScheduler,
                CurrentStory = ctx.CurrentStory
            };
        }

        ctx.CopyContextFlagsTo(newCtx);

        CompiledStory? story = newCtx.CurrentStory;

        if (targetStoryName != null && !string.Equals(targetStoryName, story?.Name, StringComparison.Ordinal))
        {
            if (newCtx.StoryLoader == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_loader", "Missing StoryLoader.", call.Start));
            story = newCtx.StoryLoader(targetStoryName);
            if (story == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", $"Story '{targetStoryName}' not found.", call.Start));
            newCtx.CurrentStory = story;
            newCtx.ExitStory();
            newCtx.CurrentStory = story;
        }

        if (story == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", "No target story.", call.Start));

        if (!story.Labels.TryGetValue(targetLabel, out int ip))
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_checkpoint", $"Label '{targetLabel}' not found.", call.Start));
        }

        // Transfer params into child context before validating the contract
        foreach (var r in transferRefs)
        {
            if (ctx.Variables.TryGetWithScope(r.Name, out var val, out var scope))
            {
                newCtx.Variables.Set(r.Name, val, scope);
            }
        }

        var validation = newCtx.ValidateContract(targetLabel);
        if (validation.IsFatal) return validation;

        newCtx.InstructionPointer = ip;

        // Schedule child
        ctx.ContextScheduler(newCtx);
        var childHandle = newCtx.Handle ??= new ContextHandle(newCtx);

        bool shouldInline = call.Attributes.Any(a => a.Name.Equals("inline", StringComparison.OrdinalIgnoreCase));

        // Suspend parent until child terminates
        return new DriverResult.Suspend(new Continuation(
            new JoinContextRequest(childHandle),
            outcome =>
            {
                switch (outcome)
                {
                    case WaitCompleted c:
                        if (shouldInline)
                        {
                            var finalStore = childHandle.InternalContext.Variables;
                            foreach (var r in transferRefs)
                            {
                                var varName = r.Name;
                                if (finalStore.TryGetWithScope(varName, out var childVal, out var childScope))
                                {
                                    ctx.Variables.Set(varName, childVal, childScope);
                                }
                            }
                        }
                        return DriverResult.Complete.Ok(c.Value);

                    case WaitCancelled x:
                        return new DriverResult.Complete(
                            ZohNothing.Instance,
                            ImmutableArray.Create(new Diagnostic(DiagnosticSeverity.Error, x.Code, x.Message, call.Start)));

                    default:
                        return DriverResult.Complete.Ok();
                }
            }
        ));
    }
}
