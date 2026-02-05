using System.Collections.Concurrent;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Storage;

public class InMemoryStorage : IPersistentStorage
{
    // storeName -> varName -> Value
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ZohValue>> _stores = new();

    private ConcurrentDictionary<string, ZohValue> GetStore(string? store)
    {
        var key = store ?? "default";
        return _stores.GetOrAdd(key, _ => new ConcurrentDictionary<string, ZohValue>());
    }

    public void Write(string? store, string varName, ZohValue value)
    {
        var s = GetStore(store);
        s[varName] = value;
    }

    public ZohValue? Read(string? store, string varName)
    {
        var s = GetStore(store);
        if (s.TryGetValue(varName, out var value))
        {
            return value;
        }
        return null;
    }

    public void Erase(string? store, string varName)
    {
        var s = GetStore(store);
        s.TryRemove(varName, out _);
    }

    public void Purge(string? store)
    {
        var key = store ?? "default";
        _stores.TryRemove(key, out _);
    }

    public bool Exists(string? store, string varName)
    {
        var s = GetStore(store);
        return s.ContainsKey(varName);
    }
}
