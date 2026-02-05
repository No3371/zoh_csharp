using System.Collections.Concurrent;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics; // For diagnostics if needed? 
// Actually VariableStore throws or returns result?
// ZOH runtime handles errors. 
// For now, simple C# implementation.

namespace Zoh.Runtime.Variables;

public class VariableStore(IDictionary<string, Variable> contextVariables)
{
    // Story variables are local to the current story execution and shadow context variables
    private readonly Dictionary<string, Variable> _storyVariables = new();

    // Context variables are shared across the context (and potentially parent contexts if passed down)
    private readonly IDictionary<string, Variable> _contextVariables = contextVariables;

    public ZohValue Get(string name)
    {
        if (TryGet(name, out var value)) return value;
        return ZohValue.Nothing;
    }

    public bool TryGet(string name, out ZohValue value)
    {
        name = name.ToLowerInvariant();
        if (_storyVariables.TryGetValue(name, out var v))
        {
            value = v.Value;
            return true;
        }

        if (_contextVariables.TryGetValue(name, out var cv))
        {
            value = cv.Value;
            return true;
        }

        value = ZohValue.Nothing;
        return false;
    }

    public void Set(string name, ZohValue value, Scope? specificScope = null)
    {
        if (!IsValidVariableName(name))
        {
            throw new ArgumentException($"Invalid variable name: '{name}'. Variable names can NOT start with digits and can NOT contain whitespaces or reserved characters.");
        }

        name = name.ToLowerInvariant();
        // Default to Story scope if not specified.
        // This ensures shadowing behavior: setting a variable defaults to the local (Story) scope,
        // even if it exists in the outer (Context) scope, unless Context scope is explicitly requested.
        var targetScope = (specificScope == Scope.Context) ? _contextVariables : _storyVariables;

        UpdateOrAdd(targetScope, name, value);
    }

    public void Drop(string name, Scope? scope = null)
    {
        name = name.ToLowerInvariant();
        if (scope == Scope.Context)
        {
            _contextVariables.Remove(name);
        }
        else if (scope == Scope.Story)
        {
            _storyVariables.Remove(name);
        }
        else
        {
            // Default behavior: Remove from Story by default? 
            // Spec says: "runtime should default to story too if the attribute not specified"
            // Wait, previous logic was remove from BOTH.
            // If user says /drop "x", they might expect it gone entirely?
            // "The scope to drop the variable from... defaults to story".
            // If it defaults to Story, then it only removes from Story scope.
            // If "x" exists in Context, it becomes visible (unshadowed).
            // This is a semantic change from "obliterate x". But adhering to spec.
            _storyVariables.Remove(name);
        }
    }

    private void UpdateOrAdd(IDictionary<string, Variable> scope, string name, ZohValue value)
    {
        if (scope.TryGetValue(name, out var existing))
        {
            scope[name] = existing.WithValue(value);
        }
        else
        {
            scope[name] = new Variable(value);
        }
    }

    // Helper to get type if typed
    // Used by SetDriver logic (not yet fully implemented there but useful)
    public ZohValueType? GetTypeConstraint(string name)
    {
        name = name.ToLowerInvariant();
        if (_storyVariables.TryGetValue(name, out var sv) && sv.TypeConstraint.HasValue) return sv.TypeConstraint;
        if (_contextVariables.TryGetValue(name, out var cv) && cv.TypeConstraint.HasValue) return cv.TypeConstraint;
        return null;
    }

    // Helper for explicit set typed
    public void SetTyped(string name, ZohValue value, ZohValueType type, Scope? scope = null)
    {
        name = name.ToLowerInvariant();
        var targetScope = scope == Scope.Context ? _contextVariables : _storyVariables;
        targetScope[name] = new Variable(value, type);
    }

    private static bool IsValidVariableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        // Cannot start with digit
        if (char.IsDigit(name[0])) return false;

        foreach (var c in name)
        {
            // Only letters, digits, and underscores are allowed.
            // Dots are NOT allowed for variables (unlike verbs/attributes).
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
