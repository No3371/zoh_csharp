using System.Collections.Concurrent;
using Zoh.Runtime.Types;
using Zoh.Runtime.Diagnostics;

namespace Zoh.Runtime.Execution;

public class ChannelManager
{
    private class Channel
    {
        public ConcurrentQueue<ZohValue> Queue { get; } = new();
        public int Generation { get; init; }
        public bool IsClosed { get; private set; }

        public void Close() => IsClosed = true;
    }

    private readonly ConcurrentDictionary<string, Channel> _channels = new();
    private int _generationCounter = 0;

    /// <summary>
    /// Opens/creates a channel. If closed channel exists with same name, 
    /// creates new channel with incremented generation.
    /// </summary>
    /// <returns>The generation of the (possibly new) channel</returns>
    public int Open(string name)
    {
        name = name.ToLowerInvariant();

        var channel = _channels.AddOrUpdate(
            name,
            _ => new Channel { Generation = Interlocked.Increment(ref _generationCounter) },
            (_, existing) =>
            {
                if (existing.IsClosed)
                    return new Channel { Generation = Interlocked.Increment(ref _generationCounter) };
                return existing;
            });

        return channel.Generation;
    }

    /// <summary>
    /// Checks if a channel exists and is open.
    /// </summary>
    public bool Exists(string name)
    {
        name = name.ToLowerInvariant();
        return _channels.TryGetValue(name, out var ch) && !ch.IsClosed;
    }

    /// <summary>
    /// Gets the generation of a channel. Returns 0 if not found or closed.
    /// </summary>
    public int GetGeneration(string name)
    {
        name = name.ToLowerInvariant();
        if (_channels.TryGetValue(name, out var ch) && !ch.IsClosed)
            return ch.Generation;
        return 0;
    }

    /// <summary>
    /// Pushes a value to an open channel.
    /// Returns false if channel doesn't exist or is closed.
    /// </summary>
    public bool TryPush(string name, ZohValue value)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch) || ch.IsClosed)
            return false;

        ch.Queue.Enqueue(value);
        return true;
    }

    /// <summary>
    /// Tries to pull a value from a channel (non-blocking).
    /// Also verifies generation hasn't changed.
    /// </summary>
    public PullResult TryPull(string name, int expectedGeneration)
    {
        name = name.ToLowerInvariant();

        if (!_channels.TryGetValue(name, out var ch))
            return PullResult.NotFound;

        if (ch.IsClosed)
            return PullResult.Closed;

        if (ch.Generation != expectedGeneration)
            return PullResult.GenerationMismatch;

        if (ch.Queue.TryDequeue(out var value))
            return PullResult.Success(value);

        return PullResult.Empty;
    }

    /// <summary>
    /// Gets the count of items in a channel.
    /// </summary>
    public int Count(string name)
    {
        name = name.ToLowerInvariant();
        if (_channels.TryGetValue(name, out var ch) && !ch.IsClosed)
            return ch.Queue.Count;
        return 0;
    }

    /// <summary>
    /// Closes a channel. Returns false if channel doesn't exist or already closed.
    /// </summary>
    public bool TryClose(string name)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch) || ch.IsClosed)
            return false;

        ch.Close();
        return true;
    }
}

public readonly struct PullResult
{
    public PullStatus Status { get; init; }
    public ZohValue? Value { get; init; }

    public static PullResult NotFound => new() { Status = PullStatus.NotFound };
    public static PullResult Closed => new() { Status = PullStatus.Closed };
    public static PullResult GenerationMismatch => new() { Status = PullStatus.GenerationMismatch };
    public static PullResult Empty => new() { Status = PullStatus.Empty };
    public static PullResult Success(ZohValue value) => new() { Status = PullStatus.Success, Value = value };
}

public enum PullStatus
{
    Success,
    NotFound,
    Closed,
    Empty,
    GenerationMismatch
}
