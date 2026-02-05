using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Zoh.Runtime.Types;

public interface IZohMap
{
    int Count { get; }
    bool IsEmpty { get; }
    bool TryGet(string key, [MaybeNullWhen(false)] out ZohValue value);
    IEnumerable<KeyValuePair<string, ZohValue>> Entries { get; }
}

public sealed record ZohMap(ImmutableDictionary<string, ZohValue> Items) : ZohValue, IZohMap
{
    public override ZohValueType Type => ZohValueType.Map;

    public int Count => Items.Count;
    public bool IsEmpty => Items.IsEmpty;
    public bool TryGet(string key, [MaybeNullWhen(false)] out ZohValue value) => Items.TryGetValue(key, out value);
    public IEnumerable<KeyValuePair<string, ZohValue>> Entries => Items;

    public override ZohValue DeepClone()
    {
        var newItems = Items.ToImmutableDictionary(k => k.Key, v => v.Value.DeepClone());
        return new ZohMap(newItems);
    }

    public bool Equals(ZohMap? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Items.Count != other.Items.Count) return false;

        foreach (var kvp in Items)
        {
            if (!other.Items.TryGetValue(kvp.Key, out var otherValue)) return false;
            if (!kvp.Value.Equals(otherValue)) return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        // Order independent hash code
        int hash = 0;
        foreach (var kvp in Items)
        {
            // XOR is order independent
            hash ^= HashCode.Combine(kvp.Key, kvp.Value);
        }
        return hash;
    }

    public override string ToString()
    {
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var kvp in Items)
        {
            if (!first) sb.Append(", ");
            sb.Append('"').Append(kvp.Key).Append("\": ");

            if (kvp.Value is ZohStr s)
            {
                sb.Append('"').Append(s.Value).Append('"');
            }
            else
            {
                sb.Append(kvp.Value);
            }
            first = false;
        }
        sb.Append("}");
        return sb.ToString();
    }
}

public sealed record ZohKvPair(string Key, ZohValue Value) : ZohValue, IZohMap
{
    public override ZohValueType Type => ZohValueType.Map;

    public int Count => 1;
    public bool IsEmpty => false;
    public IEnumerable<KeyValuePair<string, ZohValue>> Entries
    {
        get
        {
            yield return new KeyValuePair<string, ZohValue>(Key, Value);
        }
    }

    public bool TryGet(string key, out ZohValue value)
    {
        if (string.Equals(Key, key, StringComparison.Ordinal)) // ZohMap uses ordinal or invariant?
        // ZohMap backing is Dictionary<string, ...>. Default is Ordinal in C#? 
        // VariableStore uses ToLowerInvariant. ZohMap usually case-sensitive?
        // Spec: Map keys are strings. Usually case sensitive.
        // I will use Ordinal to match dictionary default.
        {
            value = Value;
            return true;
        }
        value = ZohValue.Nothing;
        return false;
    }

    public override ZohValue DeepClone() => new ZohKvPair(Key, Value.DeepClone());

    public override string ToString()
    {
        var valStr = Value is ZohStr s ? $"\"{s.Value}\"" : Value.ToString();
        return $"{{\"{(Key is null ? "" : Key)}\": {valStr}}}"; // Key shouldn't be null but just in case
    }
}
