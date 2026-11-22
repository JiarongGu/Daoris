namespace Daoris.Driver;

/// <summary>
/// Whose ask a git is, told by the flow that makes it: the look's stage, the opinion gate, a door's read, a machine-facts topic, a
/// check, the run-step consumer, and the flows that ask outside a look. The service counts each ask under it and its read kind with the
/// time it waited for a place (<see cref="GitAskTally"/>), so what holds git's places is measured by cause, not guessed.
/// </summary>
public static class GitAskers
{
    public const string Look = "look", Gate = "opinion-gate", Door = "door", Facts = "facts", Check = "check", Steps = "run-step", Other = "other";

    /// <summary>The flows that ask git outside a look or a step: the tree pool, a tidy or keep, the line's sync, a pull request's moment, a landing plugin.</summary>
    public const string Pool = "pool", Tidy = "tidy", Sync = "sync", Moments = "pull-request", Plugin = "plugin";

    private static readonly AsyncLocal<string?> s_caller = new();
    private static readonly AsyncLocal<string?> s_site = new();

    /// <summary>The caller this flow's asks are counted under; <see cref="Other"/> where none is named.</summary>
    internal static string Current => s_caller.Value ?? Other;

    /// <summary>Until disposed, the asks this flow makes are <paramref name="caller"/>'s, the innermost name standing.</summary>
    public static IDisposable Is(string caller)
    {
        var before = s_caller.Value;
        s_caller.Value = caller;
        return new Restore(() => s_caller.Value = before);
    }

    /// <summary>Like <see cref="Is"/>, unless a caller within this flow already named itself, which is the more exact name.</summary>
    internal static IDisposable Unless(string caller) => s_caller.Value is null ? Is(caller) : new Restore(() => { });

    /// <summary>Where a flow no caller named asks from, noted at the door's entry while the asker's frames stand, for an ask counted as <see cref="Other"/>.</summary>
    internal static void NoteSite()
    {
        if (s_caller.Value is null) s_site.Value = GitSites.Here(skip: 2);
    }

    internal static string? TakeSite()
    {
        var site = s_site.Value;
        s_site.Value = null;
        return site;
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

/// <summary>
/// The git asks since the last take, by caller and read kind: how many and the milliseconds they waited for a place. Told at the
/// scheduler as each is placed, taken by the look's stage line, which tells the bus's measures once for each caller and kind.
/// </summary>
public sealed class GitAskTally
{
    private const int MostSites = 40;

    /// <summary>The one tally of the process, as the scheduler is one.</summary>
    public static GitAskTally Shared { get; } = new();

    private readonly object _gate = new();
    private readonly Dictionary<(string Caller, string Kind), (long Count, long Waited)> _rows = [];
    private readonly Dictionary<string, long> _sites = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Caller, string Kind), (long Hits, long Misses)> _memo = [];

    public void Record(string caller, string kind, long waitedMilliseconds)
    {
        lock (_gate)
        {
            var (count, waited) = _rows.GetValueOrDefault((caller, kind));
            _rows[(caller, kind)] = (count + 1, waited + Math.Max(0, waitedMilliseconds));
        }
    }

    /// <summary>One read put to the memo of object-id answers (<see cref="GitObjectMemo"/>): a hit starts nothing, a miss goes on to the library or git.</summary>
    public void RecordMemo(string caller, string kind, bool hit)
    {
        lock (_gate)
        {
            var (hits, misses) = _memo.GetValueOrDefault((caller, kind));
            _memo[(caller, kind)] = hit ? (hits + 1, misses) : (hits, misses + 1);
        }
    }

    /// <summary>Every caller's and kind's memo hits and misses since the last take, the most reads first, and let go.</summary>
    public IReadOnlyList<(string Caller, string Kind, long Hits, long Misses)> TakeMemo()
    {
        lock (_gate)
        {
            var taken = _memo.Select(pair => (pair.Key.Caller, pair.Key.Kind, pair.Value.Hits, pair.Value.Misses))
                .OrderByDescending(row => row.Hits + row.Misses).ThenBy(row => row.Caller, StringComparer.Ordinal).ThenBy(row => row.Kind, StringComparer.Ordinal).ToList();
            _memo.Clear();
            return taken;
        }
    }

    /// <summary>One ask no caller named, from <paramref name="site"/>; at most <see cref="MostSites"/> sites are kept.</summary>
    internal void RecordSite(string site)
    {
        lock (_gate)
        {
            if (_sites.TryGetValue(site, out var count)) _sites[site] = count + 1;
            else if (_sites.Count < MostSites) _sites[site] = 1;
        }
    }

    /// <summary>Every caller's and kind's count and waited milliseconds since the last take, the most asks first, and let go.</summary>
    public IReadOnlyList<(string Caller, string Kind, long Count, long Waited)> Take()
    {
        lock (_gate)
        {
            var taken = _rows.Select(pair => (pair.Key.Caller, pair.Key.Kind, pair.Value.Count, pair.Value.Waited))
                .OrderByDescending(row => row.Count).ThenBy(row => row.Caller, StringComparer.Ordinal).ThenBy(row => row.Kind, StringComparer.Ordinal).ToList();
            _rows.Clear();
            return taken;
        }
    }

    /// <summary>The sites the <see cref="GitAskers.Other"/> asks since the last take were made from, the most asks first, and let go.</summary>
    internal IReadOnlyList<(string Site, long Count)> TakeSites()
    {
        lock (_gate)
        {
            var taken = _sites.Select(pair => (pair.Key, pair.Value)).OrderByDescending(row => row.Value).ThenBy(row => row.Key, StringComparer.Ordinal).ToList();
            _sites.Clear();
            return taken;
        }
    }
}
