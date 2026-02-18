using Zoh.Runtime.Preprocessing;
using Zoh.Runtime.Validation;
using Zoh.Runtime.Validation.CoreVerbs;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Execution;

/// <summary>
/// Central registry for all runtime handler chains.
/// Handlers are stored ordered by priority (ascending = runs first).
/// </summary>
public class HandlerRegistry
{
    private readonly List<IPreprocessor> _preprocessors = new();
    private readonly List<IStoryValidator> _storyValidators = new();
    private readonly Dictionary<string, List<IVerbValidator>> _verbValidators = new(StringComparer.OrdinalIgnoreCase);

    public VerbRegistry VerbDrivers { get; } = new();

    // --- Preprocessors ---

    public IReadOnlyList<IPreprocessor> Preprocessors => _preprocessors;

    public void RegisterPreprocessor(IPreprocessor preprocessor)
    {
        _preprocessors.Add(preprocessor);
        _preprocessors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    // --- Story Validators ---

    public IReadOnlyList<IStoryValidator> StoryValidators => _storyValidators;

    public void RegisterStoryValidator(IStoryValidator validator)
    {
        _storyValidators.Add(validator);
        _storyValidators.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    // --- Verb Validators ---

    public IReadOnlyDictionary<string, List<IVerbValidator>> VerbValidators => _verbValidators;

    public void RegisterVerbValidator(IVerbValidator validator)
    {
        var key = validator.VerbName.ToLowerInvariant();
        if (!_verbValidators.TryGetValue(key, out var list))
        {
            list = new List<IVerbValidator>();
            _verbValidators[key] = list;
        }
        list.Add(validator);
        list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>
    /// Registers all core verb drivers and wires preprocessors/validators.
    /// </summary>
    public void RegisterCoreHandlers()
    {
        VerbDrivers.RegisterCoreVerbs();

        // Story Validators
        RegisterStoryValidator(new LabelValidator());
        RegisterStoryValidator(new JumpTargetValidator());
        RegisterStoryValidator(new RequiredVerbsValidator(VerbDrivers));
        RegisterStoryValidator(new VerbResolutionValidator(this));

        // Verb Validators
        RegisterVerbValidator(new SetValidator());
        RegisterVerbValidator(new JumpValidator());
        RegisterVerbValidator(new ForkValidator());
        RegisterVerbValidator(new CallValidator());
    }
}
