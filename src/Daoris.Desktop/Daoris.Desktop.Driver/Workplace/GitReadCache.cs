using System.Diagnostics.CodeAnalysis;

namespace Daoris.Driver;

/// <summary>
/// What the reads behind a page keep in memory (GIT1b, D147 §4.2): answers named only by whole commit ids, least recently
/// used first out, bounded by bytes. Which answers may be kept, and under what key, is <see cref="GitReads"/>'.
/// </summary>
/// <remarks>
/// <para><b>Memory only.</b> git's object store is already the cache on disk, and a copy would be a second record; nothing
/// here is written anywhere.</para>
///
/// <para><b>The weight is an estimate</b> the reads make of each answer (its strings' characters, two bytes each, and a
/// little for each object), so the bound holds the memory near where it is set rather than to the byte.</para>
/// </remarks>
public sealed class GitReadCache
{
    /// <summary>The bound a host starts with: room for some eighty commits whose changes each spent the review's whole patch budget.</summary>
    public const long DefaultBound = 64L * 1024 * 1024;

    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.Ordinal);

    // The most recently used first, so the last is the first out.
    private readonly LinkedList<Entry> _order = new();
    private long _spent;

    public GitReadCache(long bound = DefaultBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bound);
        Bound = bound;
    }

    /// <summary>How many bytes the kept answers may weigh together.</summary>
    public long Bound { get; }

    /// <summary>What the kept answers weigh now.</summary>
    public long Spent
    {
        get
        {
            lock (_gate) return _spent;
        }
    }

    /// <summary>How many answers are kept now.</summary>
    public int Count
    {
        get
        {
            lock (_gate) return _entries.Count;
        }
    }

    /// <summary>The answer kept under <paramref name="key"/>, as the type it was kept as; asking for it makes it the most recent.</summary>
    public bool TryGet<T>(string key, [MaybeNullWhen(false)] out T value) where T : class
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node) && node.Value.Value is T kept)
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

    /// <summary>
    /// Keep <paramref name="value"/> under <paramref name="key"/>, weighing <paramref name="size"/> bytes, replacing what the
    /// key held; the least recently used go until the rest fit. One answer heavier than the whole bound is not kept, and
    /// nothing goes for it.
    /// </summary>
    public void Keep(string key, object value, long size)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (size > Bound) return;

        lock (_gate)
        {
            if (_entries.Remove(key, out var old))
            {
                _order.Remove(old);
                _spent -= old.Value.Size;
            }

            while (_spent + size > Bound && _order.Last is { } last)
            {
                _order.RemoveLast();
                _entries.Remove(last.Value.Key);
                _spent -= last.Value.Size;
            }

            _entries[key] = _order.AddFirst(new Entry(key, value, size));
            _spent += size;
        }
    }

    private sealed record Entry(string Key, object Value, long Size);
}
