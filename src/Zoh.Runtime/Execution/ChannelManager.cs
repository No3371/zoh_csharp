using System.Collections.Concurrent;
using Zoh.Runtime.Types;
using Zoh.Runtime.Verbs;

namespace Zoh.Runtime.Execution;

public class ChannelManager
{
    private sealed class BufferEntry
    {
        public required ZohValue Value { get; init; }
        public Context? Pusher { get; init; }
        public int PusherToken { get; init; }
    }

    private sealed class WaitingPuller
    {
        public required Context Ctx { get; init; }
        public int Token { get; init; }
    }

    private sealed class Channel
    {
        public int Generation { get; init; }
        public bool IsClosed { get; set; }
        public LinkedList<BufferEntry> Buffer { get; } = new();
        public LinkedList<WaitingPuller> Pullers { get; } = new();
        public object Lock { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Channel> _channels = new();
    private int _generationCounter;

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

    public bool Exists(string name)
    {
        name = name.ToLowerInvariant();
        return _channels.TryGetValue(name, out var ch) && !ch.IsClosed;
    }

    public int GetGeneration(string name)
    {
        name = name.ToLowerInvariant();
        if (_channels.TryGetValue(name, out var ch) && !ch.IsClosed)
            return ch.Generation;
        return 0;
    }

    public int Count(string name)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch) || ch.IsClosed) return 0;
        lock (ch.Lock) return ch.Buffer.Count;
    }

    public OfferPushResult OfferPush(string name, int expectedGeneration, ZohValue value, bool wait, Context pusher)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return OfferPushResult.NotFound;
        lock (ch.Lock)
        {
            if (ch.IsClosed) return OfferPushResult.Closed;
            if (ch.Generation != expectedGeneration) return OfferPushResult.Closed;

            if (ch.Pullers.First is { } pullerNode)
            {
                var waiter = pullerNode.Value;
                ch.Pullers.RemoveFirst();
                waiter.Ctx.Resume(new WaitCompleted(value), waiter.Token);
                return OfferPushResult.Delivered;
            }

            if (!wait)
            {
                ch.Buffer.AddLast(new BufferEntry { Value = value });
                return OfferPushResult.Buffered;
            }

            return OfferPushResult.MustSuspend;
        }
    }

    public AttemptPullResult AttemptPull(string name, int expectedGeneration)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return AttemptPullResult.NotFound();
        lock (ch.Lock)
        {
            if (ch.IsClosed) return AttemptPullResult.Closed();
            if (ch.Generation != expectedGeneration) return AttemptPullResult.Stale();

            if (ch.Buffer.First is { } node)
            {
                var entry = node.Value;
                ch.Buffer.RemoveFirst();
                if (entry.Pusher is not null)
                    entry.Pusher.Resume(new WaitCompleted(ZohNothing.Instance), entry.PusherToken);
                return AttemptPullResult.Delivered(entry.Value);
            }

            return AttemptPullResult.Empty();
        }
    }

    /// <summary>Called from Context.BlockOnRequest after ResumeToken bump for blocking push.</summary>
    public void RegisterWaitingPusher(string name, ZohValue value, Context pusher, int pusherToken)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return;
        lock (ch.Lock)
        {
            if (ch.IsClosed)
            {
                pusher.Resume(new WaitCancelled("closed", $"Channel closed: {name}"), pusherToken);
                return;
            }

            ch.Buffer.AddLast(new BufferEntry { Value = value, Pusher = pusher, PusherToken = pusherToken });
        }
    }

    /// <summary>Called from Context.BlockOnRequest after ResumeToken bump for blocking pull.</summary>
    public void RegisterWaitingPuller(string name, Context puller, int pullerToken)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return;
        lock (ch.Lock)
        {
            if (ch.IsClosed)
            {
                puller.Resume(new WaitCancelled("closed", $"Channel closed: {name}"), pullerToken);
                return;
            }

            ch.Pullers.AddLast(new WaitingPuller { Ctx = puller, Token = pullerToken });
        }
    }

    public void CancelPuller(string name, Context puller)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return;
        lock (ch.Lock)
        {
            for (var node = ch.Pullers.First; node != null; node = node.Next)
            {
                if (ReferenceEquals(node.Value.Ctx, puller))
                {
                    ch.Pullers.Remove(node);
                    return;
                }
            }
        }
    }

    public void CancelPusher(string name, Context pusher)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return;
        lock (ch.Lock)
        {
            for (var node = ch.Buffer.First; node != null; node = node.Next)
            {
                if (ReferenceEquals(node.Value.Pusher, pusher))
                {
                    ch.Buffer.Remove(node);
                    return;
                }
            }
        }
    }

    public bool TryClose(string name)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return false;
        lock (ch.Lock)
        {
            if (ch.IsClosed) return false;
            ch.IsClosed = true;

            foreach (var w in ch.Pullers)
                w.Ctx.Resume(new WaitCancelled("closed", $"Channel closed: {name}"), w.Token);
            ch.Pullers.Clear();

            foreach (var e in ch.Buffer)
            {
                if (e.Pusher is not null)
                    e.Pusher.Resume(new WaitCancelled("closed", $"Channel closed: {name}"), e.PusherToken);
            }

            ch.Buffer.Clear();
        }

        return true;
    }

    public bool TryPush(string name, ZohValue value)
    {
        name = name.ToLowerInvariant();
        if (!_channels.TryGetValue(name, out var ch)) return false;
        lock (ch.Lock)
        {
            if (ch.IsClosed) return false;
            if (ch.Pullers.First is { } pullerNode)
            {
                var waiter = pullerNode.Value;
                ch.Pullers.RemoveFirst();
                waiter.Ctx.Resume(new WaitCompleted(value), waiter.Token);
                return true;
            }

            ch.Buffer.AddLast(new BufferEntry { Value = value });
            return true;
        }
    }

    public PullResult TryPull(string name, int expectedGeneration)
    {
        var r = AttemptPull(name, expectedGeneration);
        return r.Status switch
        {
            AttemptPullStatus.NotFound => PullResult.NotFound,
            AttemptPullStatus.Closed => PullResult.Closed,
            AttemptPullStatus.Stale => PullResult.GenerationMismatch,
            AttemptPullStatus.Delivered => PullResult.Success(r.Value!),
            AttemptPullStatus.Empty => PullResult.Empty,
            _ => PullResult.NotFound
        };
    }
}

public enum OfferPushResult
{
    NotFound,
    Closed,
    Delivered,
    Buffered,
    MustSuspend
}

public enum AttemptPullStatus
{
    NotFound,
    Closed,
    Stale,
    Delivered,
    Empty
}

public readonly struct AttemptPullResult
{
    public AttemptPullStatus Status { get; init; }
    public ZohValue? Value { get; init; }

    public static AttemptPullResult NotFound() => new() { Status = AttemptPullStatus.NotFound };
    public static AttemptPullResult Closed() => new() { Status = AttemptPullStatus.Closed };
    public static AttemptPullResult Stale() => new() { Status = AttemptPullStatus.Stale };
    public static AttemptPullResult Delivered(ZohValue v) => new() { Status = AttemptPullStatus.Delivered, Value = v };
    public static AttemptPullResult Empty() => new() { Status = AttemptPullStatus.Empty };
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
