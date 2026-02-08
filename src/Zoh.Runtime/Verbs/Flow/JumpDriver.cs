using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables; // For ValueResolver
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Verbs.Flow;

public class JumpDriver : IVerbDriver
{
    public string Namespace => "core";
    public string Name => "jump";

    public VerbResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Jump requires a valid Context.", call.Start));

        // Resolve arguments
        // /jump [story], label;

        string targetLabel = "";
        string? targetStoryName = null;

        if (call.UnnamedParams.Length == 1)
        {
            var val = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
            if (val is ZohStr s)
            {
                targetLabel = s.Value;
            }
            else
            {
                return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Jump label must be a string.", call.Start));
            }
        }
        else if (call.UnnamedParams.Length == 2)
        {
            var val0 = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
            if (val0 is ZohStr s0) targetStoryName = s0.Value;
            else if (val0 is ZohNothing) targetStoryName = null;
            else return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Jump story must be a string or nothing.", call.Start));

            var val1 = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
            if (val1 is ZohStr s1) targetLabel = s1.Value;
            else return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_arg", "Jump label must be a string.", call.Start));
        }
        else
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "arg_count", "Jump requires 1 or 2 arguments.", call.Start));
        }

        // Check if we need to switch story
        CompiledStory? story = ctx.CurrentStory;
        bool switchStory = false;

        if (targetStoryName != null && !string.Equals(targetStoryName, story?.Name, StringComparison.Ordinal)) // Ordinal or Invariant?
        {
            // Switch story
            if (ctx.StoryLoader == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_loader", "Missing StoryLoader.", call.Start));
            story = ctx.StoryLoader(targetStoryName);
            if (story == null) return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", $"Story '{targetStoryName}' not found.", call.Start));
            switchStory = true;
        }

        if (story == null)
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", "No target story.", call.Start));
        }

        // Resolve label in story
        if (!story.Labels.TryGetValue(targetLabel, out int ip))
        {
            return VerbResult.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_checkpoint", $"Label '{targetLabel}' not found in story '{story.Name}'.", call.Start));
        }

        // Perform Jump
        if (switchStory)
        {
            ctx.ExitStory();
            ctx.CurrentStory = story;
        }

        var validation = ctx.ValidateContract(targetLabel);
        if (!validation.IsSuccess) return validation;

        ctx.InstructionPointer = ip;

        return VerbResult.Ok();
    }
}
