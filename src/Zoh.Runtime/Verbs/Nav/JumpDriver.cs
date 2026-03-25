using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;
using Zoh.Runtime.Diagnostics;
using System.Collections.Generic;

namespace Zoh.Runtime.Verbs.Nav;

public class JumpDriver : IVerbDriver
{
    public string Namespace => "core.nav";
    public string Name => "jump";

    public DriverResult Execute(IExecutionContext context, VerbCallAst call)
    {
        var ctx = context as Context;
        if (ctx == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_context", "Jump requires a valid Context.", call.Start));

        if (call.UnnamedParams.Length == 0)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Jump requires at least 1 argument.", call.Start));

        string targetLabel = "";
        string? targetStoryName = null;
        var transferRefs = new List<ValueAst.Reference>();

        var val0 = ValueResolver.Resolve(call.UnnamedParams[0], ctx);
        int paramIndex = 1;

        if (val0 is ZohNothing)
        {
            if (call.UnnamedParams.Length < 2)
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_params", "Jump requires a label after a null story.", call.Start));
            var val1 = ValueResolver.Resolve(call.UnnamedParams[1], ctx);
            if (val1 is ZohStr s1) targetLabel = s1.Value;
            else return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Jump label must be a string.", call.Start));
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
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Jump target must be a string or nothing.", call.Start));
        }

        for (int i = paramIndex; i < call.UnnamedParams.Length; i++)
        {
            if (call.UnnamedParams[i] is ValueAst.Reference refAst)
                transferRefs.Add(refAst);
            else
                return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_type", "Jump transfer parameters must be references.", call.Start));
        }

        CompiledStory? story = ctx.CurrentStory;
        bool switchStory = false;

        if (targetStoryName != null && !string.Equals(targetStoryName, story?.Name, StringComparison.Ordinal))
        {
            if (ctx.StoryLoader == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "missing_loader", "Missing StoryLoader.", call.Start));
            story = ctx.StoryLoader(targetStoryName);
            if (story == null) return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", $"Story '{targetStoryName}' not found.", call.Start));
            switchStory = true;
        }

        if (story == null)
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_story", "No target story.", call.Start));

        if (!story.Labels.TryGetValue(targetLabel, out int ip))
            return DriverResult.Complete.Fatal(new Diagnostic(DiagnosticSeverity.Fatal, "invalid_checkpoint", $"Label '{targetLabel}' not found in story '{story.Name}'.", call.Start));

        // Capture transfer values before ExitStory clears story-scoped vars on cross-story jumps
        var captured = new List<(string Name, ZohValue Val, Scope Scope)>();
        foreach (var r in transferRefs)
        {
            if (ctx.Variables.TryGetWithScope(r.Name, out var val, out var scope))
                captured.Add((r.Name, val, scope));
        }

        if (switchStory)
        {
            ctx.ExitStory();
            ctx.CurrentStory = story;
        }

        // Apply transfers to destination context before contract validation
        foreach (var (name, val, scope) in captured)
            ctx.Variables.Set(name, val, scope);

        var validation = ctx.ValidateContract(targetLabel);
        if (!validation.IsSuccess) return validation;

        ctx.InstructionPointer = ip;

        return DriverResult.Complete.Ok();
    }
}
