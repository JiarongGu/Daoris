using System.Diagnostics.CodeAnalysis;

namespace Daoris.Driver;

/// <summary>
/// One kind's bound: values kept by key in least-recently-used order, each weighed as it is kept, the oldest dropped once the kind's
/// weight passes its budget. Each kind has its own, so one never evicts another's; a value heavier than the kind's largest is refused.
/// </summary>
internal sealed class DeclarationBound(string kind, long budget, long largest)
{
    // What a kept value costs beside itself: the order's node and the dictionary's entry.
    private const long Node = 96;

    private readonly Lock _gate = new();
    private readonly Dictionary<object, LinkedListNode<(object Key, object Value, long Weight)>> _kept = [];
    private readonly LinkedList<(object Key, object Value, long Weight)> _order = new();
    private long _weight;

    /// <summary>Declarations' bytes, their parses and their views.</summary>
    internal static readonly DeclarationBound Contents = new("contents", 16L << 20, 16L << 20);

    /// <summary>Tracked lists by commit, none kept past a quarter of the budget.</summary>
    internal static readonly DeclarationBound Lists = new("tracked lists", 24L << 20, 6L << 20);

    /// <summary>The listings' commits and trees, and each proven blob's content key.</summary>
    internal static readonly DeclarationBound Indexes = new("indexes", 4L << 20, 4L << 20);

    internal string Kind { get; } = kind;

    internal long Budget { get; } = budget;

    internal long Largest { get; } = largest;

    internal long Weight
    {
        get
        {
            lock (_gate) return _weight;
        }
    }

    /// <summary>The value kept under <paramref name="key"/>, made the most recent.</summary>
    internal bool TryGet<T>(object key, [NotNullWhen(true)] out T? value) where T : class
    {
        lock (_gate)
        {
            if (_kept.TryGetValue(key, out var node) && node.Value.Value is T kept)
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = kept;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Keeps <paramref name="value"/> at <paramref name="weight"/>, dropping the least recent past the budget; false where it is past the largest.</summary>
    internal bool Keep(object key, object value, long weight)
    {
        if (weight > Largest) return false;
        lock (_gate)
        {
            Remove(key);
            _kept[key] = _order.AddFirst((key, value, weight));
            _weight += weight;
            while (_weight > Budget && _order.Last is { } oldest) Remove(oldest.Value.Key);
            return true;
        }
    }

    /// <summary>What a string holds in memory, kept as a value or a part of one.</summary>
    internal static long Text(string? text) => text is null ? 0 : 24 + 2L * text.Length;

    /// <summary>What a list of strings holds: its array and each string.</summary>
    internal static long Texts(IEnumerable<string>? texts) => texts is null ? 0 : 32 + texts.Sum(text => 8 + Text(text));

    /// <summary>What a kept entry costs beside its value.</summary>
    internal static long Entry => Node;

    private void Remove(object key)
    {
        if (!_kept.Remove(key, out var node)) return;
        _order.Remove(node);
        _weight -= node.Value.Weight;
    }
}
