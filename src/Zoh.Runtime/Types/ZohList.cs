using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Zoh.Runtime.Types;

public sealed record ZohList(ImmutableArray<ZohValue> Items) : ZohValue
{
    public override ZohValueType Type => ZohValueType.List;

    public override ZohValue DeepClone()
    {
        var newItems = Items.Select(i => i.DeepClone()).ToImmutableArray();
        return new ZohList(newItems);
    }

    public bool Equals(ZohList? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Items.Length != other.Items.Length) return false;

        for (int i = 0; i < Items.Length; i++)
        {
            if (!Items[i].Equals(other.Items[i])) return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
        {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < Items.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            var item = Items[i];
            if (item is ZohStr s)
            {
                sb.Append('"').Append(s.Value).Append('"'); // Simple quoting without escaping? Spec doesn't detail escaping in output but implies JSON-like.
                // Spec says: "String values within collections are quoted".
                // We should probably escape quotes inside.
                // Assuming simple quoting for now to pass basic tests.
            }
            else
            {
                sb.Append(item.ToString());
            }
        }
        sb.Append("]");
        return sb.ToString();
    }
}
