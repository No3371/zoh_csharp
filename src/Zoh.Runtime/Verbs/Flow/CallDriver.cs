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

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Call requires a valid Context.", call.Start));

        if (ctx.ContextScheduler == null)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_scheduler", "Context has no ContextScheduler to call new context.", call.Start));
        }

        string targetLabel = "";
        string? targetStoryName = null;

        if (call.UnnamedParams.Length == 1)
        {
            var val = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
            if (val is ZohStr s) targetLabel = s.Value;
            else return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Call label must be a string.", call.Start));
        }
        else if (call.UnnamedParams.Length == 2)
        {
            var val0 = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
            if (val0 is ZohStr s0) targetStoryName = s0.Value;
            else if (val0 is ZohNothing) targetStoryName = null;
            else return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Call story must be a string or nothing.", call.Start));

            var val1 = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
            if (val1 is ZohStr s1) targetLabel = s1.Value;
            else return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Call label must be a string.", call.Start));
        }
        else
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "arg_count", "Call requires 1 or 2 arguments.", call.Start));
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
                VerbExecutor = ctx.VerbExecutor,
                StoryLoader = ctx.StoryLoader,
                ContextScheduler = ctx.ContextScheduler,
                CurrentStory = ctx.CurrentStory
            };
        }

        CompiledStory? story = newCtx.CurrentStory;

        if (targetStoryName != null && !string.Equals(targetStoryName, story?.Name, StringComparison.Ordinal))
        {
            if (newCtx.StoryLoader == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_loader", "Missing StoryLoader.", call.Start));
            story = newCtx.StoryLoader(targetStoryName);
            if (story == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", $"Story '{targetStoryName}' not found.", call.Start));
            newCtx.CurrentStory = story;
            newCtx.ExitStory();
            newCtx.CurrentStory = story;
        }

        if (story == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", "No target story.", call.Start));

        if (!story.Labels.TryGetValue(targetLabel, out int ip))
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_checkpoint", $"Label '{targetLabel}' not found.", call.Start));
        }

        newCtx.InstructionPointer = ip;

        // Schedule child
        ctx.ContextScheduler(newCtx);

        // Suspend parent
        ctx.SetState(ContextState.WaitingContext);
        ctx.WaitCondition = newCtx; // Wait for child context

        return VerbResult.Ok();
    }
}
