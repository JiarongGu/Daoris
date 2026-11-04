using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The tool calls this client refused in one session, by id, and the last one refused in the turn running. The permission stage
/// writes them; the render stage records a refused call's later <c>failed</c> as <c>refused</c>, and the turn's end names the last
/// refusal where the agent cut its own turn after it.
/// </summary>
internal sealed class RefusedCalls
{
    private readonly ConcurrentDictionary<string, byte> _ids = new(StringComparer.Ordinal);
    private volatile string? _last;

    /// <summary>The last call refused in the turn running, by the name a reader knows it; the turn's end reads it and clears it.</summary>
    public string? Last
    {
        get => _last;
        set => _last = value;
    }

    /// <summary>A call refused: kept by its id where the wire gave one, and the turn's last by <paramref name="name"/>.</summary>
    public void Add(string? id, string name)
    {
        if (id is not null) _ids[id] = 0;
        _last = name;
    }

    /// <summary>Whether this client refused the call <paramref name="id"/>.</summary>
    public bool Contains(string id) => _ids.ContainsKey(id);
}
