using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;
using System.Linq;

namespace Zoh.Runtime.Verbs.Flow;

public class ForkDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "fork";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Fork requires a valid Context.", call.Start));

        if (ctx.ContextScheduler == null)
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_scheduler", "Context has no ContextScheduler to fork new context.", call.Start));
        }

        // Arguments: [StoryName?], LabelName
        // Similar to Jump
        string targetLabel = "";
        string? targetStoryName = null;

        if (call.UnnamedParams.Length == 1)
        {
            var val = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
            if (val is ZohStr s) targetLabel = s.Value;
            else return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Fork label must be a string.", call.Start));
        }
        else if (call.UnnamedParams.Length == 2)
        {
            var val0 = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
            if (val0 is ZohStr s0) targetStoryName = s0.Value;
            else if (val0 is ZohNothing) targetStoryName = null;
            else return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Fork story must be a string or nothing.", call.Start));

            var val1 = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
            if (val1 is ZohStr s1) targetLabel = s1.Value;
            else return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Fork label must be a string.", call.Start));
        }
        else
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Fork requires 1 or 2 arguments.", call.Start));
        }

        // Determine cloning
        bool isClone = call.Attributes.Any(a => a.Name.Equals("clone", StringComparison.OrdinalIgnoreCase));

        Context newCtx;
        if (isClone)
        {
            newCtx = ctx.Clone();
        }
        else
        {
            // Fresh context
            // We need to re-use Runtime services (Storage, Channels)
            // But we don't have direct access to Runtime instance to call CreateContext.
            // We have ContextScheduler which takes a Context.
            // So we must manually construct Context.

            // Fresh variable store
            var store = new VariableStore(new Dictionary<string, Variable>());
            // Reuse Storage and Channels
            newCtx = new Context(store, ctx.Storage, ctx.ChannelManager, ctx.SignalManager)
            {
                Runtime = ctx.Runtime,
                VerbExecutor = ctx.VerbExecutor,
                StoryLoader = ctx.StoryLoader,
                ContextScheduler = ctx.ContextScheduler,
                CurrentStory = ctx.CurrentStory // Inherit story initially? Or Null?
            };
        }

        ctx.CopyContextFlagsTo(newCtx);

        // Resolve Target
        CompiledStory? story = newCtx.CurrentStory;

        if (targetStoryName != null && !string.Equals(targetStoryName, story?.Name, StringComparison.Ordinal))
        {
            // Switch story logic
            if (newCtx.StoryLoader == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_loader", "Missing StoryLoader.", call.Start));
            story = newCtx.StoryLoader(targetStoryName);
            if (story == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", $"Story '{targetStoryName}' not found.", call.Start));
            newCtx.CurrentStory = story;
            // If we cloned, do we need to clear variables?
            // If we cloned, variables are copied.
            // If we switch story immediately, we should clear story variables?
            // Yes, because we enter a new story.
            // But WAIT: Fork [clone] copies the stack to run a parallel task. 
            // If that task starts in a NEW story, it should probably NOT have access to old story vars.
            // If it starts in SAME story, it keeps vars.
            newCtx.ExitStory(); // Clear story vars if switching story
            newCtx.CurrentStory = story; // Re-set story after ExitStory cleared it
        }

        if (story == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", "No target story.", call.Start));

        if (!story.Labels.TryGetValue(targetLabel, out int ip))
        {
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "label_not_found", $"Label '{targetLabel}' not found.", call.Start));
        }

        var validation = newCtx.ValidateContract(targetLabel);
        if (!validation.IsSuccess) return validation;

        newCtx.InstructionPointer = ip;
        // State is Running by default.

        // Schedule
        ctx.ContextScheduler(newCtx);

        return DriverResult.Complete.Ok();
    }
}
