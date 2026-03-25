using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace Zoh.Runtime.Verbs.Nav;

public class ForkDriver : IVerbDriver
{
    public string Namespace => "core.nav";
    public string Name => "fork";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Fork requires a valid Context.", call.Start));

        if (ctx.ContextScheduler == null)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_scheduler", "Context has no ContextScheduler to fork new context.", call.Start));

        if (call.UnnamedParams.Length == 0)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Fork requires at least 1 argument.", call.Start));

        string targetLabel = "";
        string? targetStoryName = null;
        var transferRefs = new List<ValueAst.Reference>();

        var val0 = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        int paramIndex = 1;

        if (val0 is ZohNothing)
        {
            if (call.UnnamedParams.Length < 2)
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Fork requires a label after a null story.", call.Start));
            var val1 = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
            if (val1 is ZohStr s1) targetLabel = s1.Value;
            else return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Fork label must be a string.", call.Start));
            paramIndex = 2;
        }
        else if (val0 is ZohStr s0)
        {
            if (call.UnnamedParams.Length > 1)
            {
                if (call.UnnamedParams[1] is ValueAst.Reference)
                {
                    // Second arg is a reference → first str is the label, refs start at index 1
                    targetLabel = s0.Value;
                }
                else
                {
                    var val1 = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
                    if (val1 is ZohStr s1)
                    {
                        // Two strings → story + label, refs start at index 2
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
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Fork target must be a string or nothing.", call.Start));
        }

        for (int i = paramIndex; i < call.UnnamedParams.Length; i++)
        {
            if (call.UnnamedParams[i] is ValueAst.Reference refAst)
                transferRefs.Add(refAst);
            else
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Fork transfer parameters must be references.", call.Start));
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
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "label_not_found", $"Label '{targetLabel}' not found.", call.Start));

        // Transfer variables from parent into child before contract validation
        foreach (var r in transferRefs)
        {
            if (ctx.Variables.TryGetWithScope(r.Name, out var val, out var scope))
                newCtx.Variables.Set(r.Name, val, scope);
        }

        var validation = newCtx.ValidateContract(targetLabel);
        if (!validation.IsSuccess) return validation;

        newCtx.InstructionPointer = ip;

        ctx.ContextScheduler(newCtx);

        return DriverResult.Complete.Ok();
    }
}
