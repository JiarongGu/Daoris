namespace Daoris.Knowledge;

/// <summary>
/// Each root's uncommitted status, asked once for the commit and signature it is taken at and shared: a background pass walks
/// the ones a refresh started, and a hit whose stored reading is another's starts its own.
/// </summary>
internal sealed class RootProbes(Func<string, bool?>? uncommitted)
{
    private readonly Dictionary<string, (string? Commit, RootSignature? Signature, Lazy<Task<bool?>> Dirty)> _probes =
        new(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

    private readonly object _gate = new();

    /// <summary>Whether a seam is given to ask a root's status at all.</summary>
    public bool Asks => uncommitted is not null;

    /// <summary>The root's status for this commit and signature: the one already asked, or one started now; null without a seam or a commit.</summary>
    public Lazy<Task<bool?>>? ProbeOf(string root, string? commit, RootSignature? signature)
    {
        if (commit is null || uncommitted is null) return null;
        lock (_gate)
        {
            if (_probes.TryGetValue(root, out var held) && held.Commit == commit && held.Signature == signature) return held.Dirty;
            var probe = Probe(root);
            _probes[root] = (commit, signature, probe);
            return probe;
        }
    }

    /// <summary>One status of a root, started when first asked for; a git that throws answers nothing.</summary>
    public Lazy<Task<bool?>> Probe(string root) => new(() => Task.Run<bool?>(() =>
    {
        try
        {
            return uncommitted!(root);
        }
        catch (Exception)
        {
            return null;
        }
    }));

    /// <summary>Keeps a status a refresh started for the reading it took.</summary>
    public void Hold(string root, string? commit, RootSignature? signature, Lazy<Task<bool?>> dirty)
    {
        lock (_gate) _probes[root] = (commit, signature, dirty);
    }
}
