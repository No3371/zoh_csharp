using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Zoh.Runtime.Verbs;

public class VerbRegistry
{
    // Primary index for exact lookups and debugging
    private readonly ConcurrentDictionary<(string ns, string name), IVerbDriver> _drivers = new();

    // Suffix index for resolution: "log" -> [std.log, my.log]
    // Key is case-insensitive suffix.
    private readonly ConcurrentDictionary<string, List<IVerbDriver>> _suffixIndex = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IVerbDriver driver)
    {
        var ns = driver.Namespace?.ToLowerInvariant() ?? "";
        var name = driver.Name.ToLowerInvariant();

        // 1. Exact storage
        _drivers[(ns, name)] = driver;

        // 2. Suffix indexing
        IndexDriver(driver, ns, name);
    }

    public void Register(string alias, IVerbDriver driver)
    {
        var ns = driver.Namespace?.ToLowerInvariant() ?? "";
        var name = alias.ToLowerInvariant();

        // 1. Exact storage
        _drivers[(ns, name)] = driver;

        // 2. Suffix indexing
        IndexDriver(driver, ns, name);
    }

    private void IndexDriver(IVerbDriver driver, string ns, string name)
    {
        var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        var parts = fullName.Split('.');

        var currentSuffix = "";
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            var part = parts[i];
            currentSuffix = string.IsNullOrEmpty(currentSuffix) ? part : $"{part}.{currentSuffix}";

            _suffixIndex.AddOrUpdate(currentSuffix,
                // Add
                _ => new List<IVerbDriver> { driver },
                // Update
                (_, list) =>
                {
                    lock (list)
                    {
                        // Remove any existing driver with the exact same Namespace and Name to support overriding
                        list.RemoveAll(d => string.Equals(d.Namespace ?? "", driver.Namespace ?? "", StringComparison.OrdinalIgnoreCase) &&
                                            string.Equals(d.Name, driver.Name, StringComparison.OrdinalIgnoreCase));

                        if (!list.Contains(driver)) list.Add(driver);
                    }
                    return list;
                });
        }
    }

    /// <summary>
    /// resolving a verb call using suffix matching.
    /// </summary>
    public ResolutionResult Resolve(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return ResolutionResult.NotFound();

        if (_suffixIndex.TryGetValue(identifier, out var candidates))
        {
            lock (candidates)
            {
                if (candidates.Count == 0) return ResolutionResult.NotFound();
                if (candidates.Count == 1) return ResolutionResult.Success(candidates[0]);

                // Ambiguity check
                // If we have "core.set" and "set", and user asks for "set".
                // Both map to "set" suffix?
                // Wait, "core.set" driver has "set" suffix.
                // "set" driver has "set" suffix.
                // If "core.set" IS "set" (same driver instance), it's fine.
                // List only contains unique drivers.
                // So if we have 2 DIFFERENT drivers, it's ambiguous.
                // Uniqueness is handled in AddOrUpdate.

                return ResolutionResult.Ambiguous(candidates.ToImmutableList());
            }
        }

        return ResolutionResult.NotFound();
    }

    public IVerbDriver? GetDriver(string? ns, string name)
    {
        // Backward compatibility / Runtime helper
        // If ns is null, we just look up by name (suffix "name")
        // If ns is present, we look up by "ns.name"

        var query = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        var result = Resolve(query);

        if (result.Status == ResolutionStatus.Success) return result.Driver;

        if (result.Status == ResolutionStatus.Ambiguous)
        {
            // For now, throw or log?
            // Runtime expects null if not found.
            // But ambiguity is fatal.
            throw new AmbiguousVerbException(query, result.Candidates.Select(d => $"{d.Namespace}.{d.Name}"));
        }

        return null;
    }

    public IEnumerable<IVerbDriver> GetAllDrivers() => _drivers.Values;

    public void RegisterCoreVerbs()
    {
        Register(new Core.SetDriver());
        Register(new Core.GetDriver());
        Register(new Core.DropDriver());
        Register(new Core.CaptureDriver());
        Register(new Core.TypeDriver());
        Register(new Core.AssertDriver());

        Register(new Core.IncreaseDriver());
        Register(new Core.DecreaseDriver());

        Register(new Core.InterpolateDriver());

        var debug = new Core.DebugDriver();
        Register("info", debug);
        Register("warning", debug);
        Register("error", debug);
        Register("fatal", debug);

        Register(new Core.HasDriver());
        Register(new Core.AnyDriver());
        Register(new Core.FirstDriver());
        Register(new Core.AppendDriver());

        var roll = new Core.RollDriver();
        Register(roll);
        Register("wroll", roll);
        Register("rand", roll);

        Register(new Core.ParseDriver());
        Register(new Core.DeferDriver());

        Register(new Store.WriteDriver());
        Register(new Store.ReadDriver());
        Register(new Store.EraseDriver());
        Register(new Store.PurgeDriver());

        Register(new OpenVerbDriver());
        Register(new PushVerbDriver());
        Register(new PullVerbDriver());
        Register(new CloseVerbDriver());

        // Flow Verbs
        Register(new Flow.IfDriver());
        Register(new Flow.LoopDriver());
        Register(new Flow.WhileDriver());
        Register(new Flow.ForeachDriver());
        Register(new Flow.SwitchDriver());
        Register(new Flow.SequenceDriver());

        Register(new Flow.JumpDriver());
        Register(new Flow.ForkDriver());
        Register(new Flow.CallDriver());
        Register(new Flow.ExitDriver());
        Register(new Flow.SleepDriver());

        // Signal Verbs
        Register(new Signals.WaitDriver());
        Register(new Signals.SignalDriver());

        // Presentation Verbs
        Register(new Standard.Presentation.ConverseDriver());
        Register(new Standard.Presentation.ChooseDriver());
        Register(new Standard.Presentation.ChooseFromDriver());
        Register(new Standard.Presentation.PromptDriver());

        // Media Verbs
        Register(new Standard.Media.ShowDriver());
        Register(new Standard.Media.HideDriver());
        Register(new Standard.Media.PlayDriver());
        Register(new Standard.Media.PlayOneDriver());
        Register(new Standard.Media.StopDriver());
        Register(new Standard.Media.PauseDriver());
        Register(new Standard.Media.ResumeDriver());
        Register(new Standard.Media.SetVolumeDriver());
    }
}

public enum ResolutionStatus { Success, NotFound, Ambiguous }

public readonly struct ResolutionResult
{
    public ResolutionStatus Status { get; }
    public IVerbDriver? Driver { get; }
    public IReadOnlyList<IVerbDriver> Candidates { get; }

    private ResolutionResult(ResolutionStatus status, IVerbDriver? driver, IReadOnlyList<IVerbDriver> candidates)
    {
        Status = status;
        Driver = driver;
        Candidates = candidates;
    }

    public static ResolutionResult Success(IVerbDriver driver) => new(ResolutionStatus.Success, driver, ImmutableList<IVerbDriver>.Empty);
    public static ResolutionResult NotFound() => new(ResolutionStatus.NotFound, null, ImmutableList<IVerbDriver>.Empty);
    public static ResolutionResult Ambiguous(IReadOnlyList<IVerbDriver> candidates) => new(ResolutionStatus.Ambiguous, null, candidates);
}

public class AmbiguousVerbException : Exception
{
    public AmbiguousVerbException(string query, IEnumerable<string> candidates)
        : base($"Ambiguous verb '{query}'. Matches: {string.Join(", ", candidates)}") { }
}
