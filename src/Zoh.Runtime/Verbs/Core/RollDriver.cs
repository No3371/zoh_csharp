using System.Collections.Immutable;
using Zoh.Runtime.Execution;
using Zoh.Runtime.Parsing.Ast;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Verbs.Core;

public class RollDriver : IVerbDriver
{
    // Register for multiple names: roll, wroll, rand?
    // VerbRegistry maps Name->Driver. 
    // This driver will check name to dispatch logic.
    // Or simpler: One driver per verb. 
    // Spec has separate logic blocks but they share "Random" logic.
    // I'll make this class implement multiple namespaces? No, implementation must return ONE Namespace/Name.
    // Actually, I can create separate classes: RollDriver, WRollDriver, RandDriver.
    // OR create one class `RandomVerbs` and register strictly.
    // I'll use separate logic for clarity, but put them in this file or separate files.
    // I will write one file with multiple classes.

    public string Namespace => "core";
    public string Name => "roll"; // Default

    public VerbResult Execute(IExecutionContext context, VerbCallAst verb)
    {
        if (verb.Name.Equals("roll", StringComparison.OrdinalIgnoreCase))
            return ExecuteRoll(context, verb);
        if (verb.Name.Equals("wroll", StringComparison.OrdinalIgnoreCase))
            return ExecuteWRoll(context, verb);
        if (verb.Name.Equals("rand", StringComparison.OrdinalIgnoreCase))
            return ExecuteRand(context, verb);

        return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "invalid_verb", $"RollDriver cannot handle {verb.Name}", verb.Start));
    }

    private VerbResult ExecuteRoll(IExecutionContext context, VerbCallAst verb)
    {
        var options = verb.UnnamedParams;
        if (options.Length == 0) return VerbResult.Ok(ZohValue.Nothing);

        var index = System.Random.Shared.Next(options.Length);
        var chosen = ValueResolver.Resolve(options[index], context);
        return VerbResult.Ok(chosen);
    }

    private VerbResult ExecuteWRoll(IExecutionContext context, VerbCallAst verb)
    {
        // wroll val1, weight1, val2, weight2
        var args = verb.UnnamedParams;
        if (args.Length % 2 != 0)
            return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "invalid_args", "Weighted roll requires pairs of (value, weight)", verb.Start));

        var pairs = new List<(ZohValue Val, int Weight)>();
        int totalWeight = 0;

        for (int i = 0; i < args.Length; i += 2)
        {
            var val = ValueResolver.Resolve(args[i], context);
            var wVal = ValueResolver.Resolve(args[i + 1], context);
            int w = (int)wVal.AsInt().Value; // Throw if not int? AsInt handles it.
            if (w < 0) w = 0;

            pairs.Add((val, w));
            totalWeight += w;
        }

        if (totalWeight == 0) return VerbResult.Ok(pairs.FirstOrDefault().Val ?? ZohValue.Nothing);

        var roll = System.Random.Shared.Next(totalWeight);
        int current = 0;
        foreach (var p in pairs)
        {
            current += p.Weight;
            if (roll < current) return VerbResult.Ok(p.Val);
        }

        return VerbResult.Ok(pairs.Last().Val);
    }

    private VerbResult ExecuteRand(IExecutionContext context, VerbCallAst verb)
    {
        var args = verb.UnnamedParams;
        if (args.Length < 2)
            return VerbResult.Fatal(new Diagnostics.Diagnostic(Diagnostics.DiagnosticSeverity.Error, "missing_param", "Usage: /rand min, max;", verb.Start));

        var minVal = ValueResolver.Resolve(args[0], context);
        var maxVal = ValueResolver.Resolve(args[1], context);

        bool includeMax = false;
        // Check named param 'inclmax'
        // NamedParams is ImmutableDictionary<string, ValueAst>
        if (verb.NamedParams.TryGetValue("inclmax", out var inclVal))
        {
            // Resolve it
            var r = ValueResolver.Resolve(inclVal, context);
            includeMax = r.IsTruthy();
        }

        if (minVal is ZohFloat || maxVal is ZohFloat)
        {
            double min = minVal.AsFloat().Value;
            double max = maxVal.AsFloat().Value;
            // Random.NextDouble() is 0..1
            double r = System.Random.Shared.NextDouble() * (max - min) + min;
            return VerbResult.Ok(new ZohFloat(r));
        }
        else
        {
            long min = minVal.AsInt().Value;
            long max = maxVal.AsInt().Value;

            if (includeMax) max++;
            if (max <= min) return VerbResult.Ok(new ZohInt(min));

            // Random.Shared.NextInt64(min, max)
            long r = System.Random.Shared.NextInt64(min, max);
            return VerbResult.Ok(new ZohInt(r));
        }
    }
}
