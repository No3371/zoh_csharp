using System.Collections.Concurrent;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Execution;

public class ChannelManager
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ZohValue>> _channels = new();

    // Create channel
    public void Create(string name)
    {
        _channels.TryAdd(name, new ConcurrentQueue<ZohValue>());
    }

    public void Push(string name, ZohValue value)
    {
        var ch = _channels.GetOrAdd(name, _ => new ConcurrentQueue<ZohValue>());
        ch.Enqueue(value);
    }

    public bool TryPull(string name, out ZohValue value)
    {
        if (_channels.TryGetValue(name, out var queue))
        {
            if (queue.TryDequeue(out value)) return true;
        }
        value = ZohValue.Nothing;
        return false;
    }

    public int Count(string name)
    {
        if (_channels.TryGetValue(name, out var queue)) return queue.Count;
        return 0;
    }

    public void Close(string name)
    {
        _channels.TryRemove(name, out _);
    }
}
