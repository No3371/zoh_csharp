namespace Zoh.Runtime.Execution;

using Zoh.Runtime.Types;
using Zoh.Runtime.Variables;

/// <summary>
/// Lazy accessor into a context's variable state.
/// Reads from the internal store on demand — no data copied until accessed.
/// </summary>
public class VariableAccessor
{
    private readonly VariableStore _store;

    internal VariableAccessor(VariableStore store) => _store = store;

    public ZohValue Get(string name) => _store.Get(name);
    public bool Has(string name) => _store.TryGet(name, out _);
    public IReadOnlyList<string> Keys() => _store.GetAllKeys();
}
