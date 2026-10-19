namespace Daoris.Driver;

/// <summary>What one sync pass came to (D47 §2, D48 §6): what went wrong, and what the remote deliberately did not take.</summary>
/// <param name="Problem">
/// Null on a clean pass. Records sync eventually (D47 §2), so a problem here is reported and retried
/// next tick rather than failing the tick — but it is REPORTED, because a feed dying quietly (an
/// expired key, an unjoined repository) looks exactly like a family with nothing to say. What a clean
/// pass moved is deliberately not carried: a healthy sync moves something almost every tick, so
/// per-tick counts would be noise on every surface that renders events — they return with a surface
/// that reads them.
/// </param>
/// <param name="Notes">
/// What the remote understood and deliberately did not take (D48 §6) — a feed from a stale checkout,
/// or from a line that is not the repository's canonical one. Reported, never treated as a failure:
/// those are the rules working, and a loop that logged them as problems would teach the person to
/// ignore its problems.
/// </param>
public sealed record SyncReport(string? Problem, IReadOnlyList<string> Notes)
{
    public SyncReport(string? problem) : this(problem, []) { }

    public static readonly SyncReport Clean = new((string?)null, []);

    /// <summary>
    /// How long the pass waited for the set's one gate before it ran (LOOKWAIT1, D181 point 6): the tick and *Sync now* share it,
    /// so a look's sync is said as its queue apart from its hold. Zero where nothing waited.
    /// </summary>
    public TimeSpan Queued { get; init; }
}
