using System.Collections.Concurrent;
using Zoh.Runtime.Types;

namespace Zoh.Runtime.Execution;

public class SignalManager
{
    private readonly ConcurrentDictionary<string, HashSet<Context>> _subscriptions = new();
    private readonly ConcurrentDictionary<Context, HashSet<string>> _contextSubscriptions = new();
    private readonly object _lock = new();

    public void Subscribe(string signalName, Context context)
    {
        lock (_lock)
        {
            // Add to signal subscriptions
            if (!_subscriptions.TryGetValue(signalName, out var contexts))
            {
                contexts = new HashSet<Context>();
                _subscriptions[signalName] = contexts;
            }
            contexts.Add(context);

            // Add to context tracking (for cleanup)
            if (!_contextSubscriptions.TryGetValue(context, out var signals))
            {
                signals = new HashSet<string>();
                _contextSubscriptions[context] = signals;
            }
            signals.Add(signalName);
        }
    }

    public void Unsubscribe(string signalName, Context context)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(signalName, out var contexts))
            {
                contexts.Remove(context);
                if (contexts.Count == 0)
                {
                    _subscriptions.TryRemove(signalName, out _);
                }
            }

            if (_contextSubscriptions.TryGetValue(context, out var signals))
            {
                signals.Remove(signalName);
                if (signals.Count == 0)
                {
                    _contextSubscriptions.TryRemove(context, out _);
                }
            }
        }
    }

    public void UnsubscribeContext(Context context)
    {
        lock (_lock)
        {
            if (_contextSubscriptions.TryGetValue(context, out var signals))
            {
                foreach (var signal in signals)
                {
                    if (_subscriptions.TryGetValue(signal, out var contexts))
                    {
                        contexts.Remove(context);
                        if (contexts.Count == 0)
                        {
                            _subscriptions.TryRemove(signal, out _);
                        }
                    }
                }
                _contextSubscriptions.TryRemove(context, out _);
            }
        }
    }

    public int Broadcast(string signalName, ZohValue payload)
    {
        int count = 0;
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(signalName, out var contexts))
            {
                // Create a copy to iterate because we will modify the collections
                // Actually, logic says: "Find all subscribers... Update state... Remove from list"
                // So we are "consuming" the signal for these waiters.

                var waiters = contexts.ToList(); // Copy

                foreach (var ctx in waiters)
                {
                    // Wake up the context via Resume (invokes onFulfilled callback if present)
                    if (ctx.State == ContextState.WaitingMessage)
                    {
                        ctx.Resume(new Verbs.WaitCompleted(payload), ctx.ResumeToken);
                        count++;
                    }

                    // Remove subscription/tracking
                    if (_contextSubscriptions.TryGetValue(ctx, out var signals))
                    {
                        signals.Remove(signalName);
                    }
                }

                // Clear the signal specific list as everyone was woken up? 
                // Or just the ones we woke up?
                // Plan says: "Removes them from the subscription list (auto-unsubscribe on signal)"
                // So YES, remove them.

                contexts.Clear();
                _subscriptions.TryRemove(signalName, out _);
            }
        }
        return count;
    }
}
