namespace Daoris.Knowledge;

/// <summary>How a walk of a line forward ended (FOUNDATION1 rows d-i and d-ii, D212; the foundation design §3.4 rule 2, §19.13).</summary>
public enum LineEnd
{
    /// <summary>At the line's head: a record nothing here continues.</summary>
    Head,

    /// <summary>On a record the walk had already passed, a cycle planted by hand: the line has no head.</summary>
    Cycle,

    /// <summary>Past <see cref="Lines.Bound"/> hops: the head is not read, and nothing is guessed from where the walk stopped.</summary>
    Bound,
}

/// <summary>A line walked forward from a record: its head where one was reached, and each record the walk visited, the named first.</summary>
/// <param name="Head">The head, or null where the walk ended on a cycle or at the bound.</param>
/// <param name="Records">Every record visited, the one named first and the head, where reached, last.</param>
/// <param name="End">How the walk ended.</param>
public sealed record LineWalk(Session? Head, IReadOnlyList<Session> Records, LineEnd End);

/// <summary>The head of a local line, read inside its caller's exclusive step.</summary>
public static class Lines
{
    /// <summary>
    /// The most hops a walk of a line follows, forward to its head (<see cref="WalkAsync"/>) and back along <c>from</c> for the settlement
    /// (<see cref="SaidKeys"/>), one bound for both (row d-ii). The design names none; row p's walk back stopped at 256 and the hand desks'
    /// walks at 16. A line of 256 parts is far past any one the install has made, and each hop is one indexed probe, so the bound costs a
    /// short line nothing. A walk that reaches it is refused with a sentence, never read as far as it got: a settlement read over a line cut
    /// short could keep a taken word a second time.
    /// </summary>
    public const int Bound = 256;

    /// <summary>
    /// Walks forward from <paramref name="named"/>: to the newest local record continuing it by <c>from</c>, across kinds (a driven part's
    /// successor conversation included), and for a driven part with no successor to its quest's last run here; one probe on
    /// <c>sessions_from</c> a hop, and a second on <c>sessions_quest</c> for a driven part with none. A record already walked ends it as a
    /// cycle, and a hop past <see cref="Bound"/> ends it at the bound, each with no head. A teammate's record is on no local line, so it is
    /// its own head with no probe: its quest's runs here are this machine's line, never the one it ran on (the say door refuses it
    /// <c>not-ours</c>, as before).
    /// </summary>
    public static async Task<LineWalk> WalkAsync(SessionStore sessions, Session named, CancellationToken ct = default)
    {
        List<Session> records = [named];
        if (named.Origin is not null) return new LineWalk(named, records, LineEnd.Head);
        var seen = new HashSet<string>(StringComparer.Ordinal) { named.Id };
        var at = named;
        for (var hops = 0; ; hops++)
        {
            var next = await sessions.ContinuedByAsync(at.Id, ct).ConfigureAwait(false);
            if (next is null && at.Kind == SessionKind.Driven && at.Quest is { } quest
                && await sessions.LastOwnIdForQuestAsync(quest, ct).ConfigureAwait(false) is { } last && last != at.Id)
            {
                // Most often the part is its quest's last run here, and the probe reads its id alone; a later run is read whole.
                next = await sessions.FindAsync(last, ct).ConfigureAwait(false);
            }

            if (next is null) return new LineWalk(at, records, LineEnd.Head);
            if (!seen.Add(next.Id)) return new LineWalk(null, records, LineEnd.Cycle);
            if (hops == Bound) return new LineWalk(null, records, LineEnd.Bound);
            records.Add(next);
            at = next;
        }
    }

    /// <summary>The head of <paramref name="named"/>'s line, as <see cref="WalkAsync"/> reaches it; null where the walk ended on a cycle or the bound.</summary>
    public static async Task<Session?> HeadOf(SessionStore sessions, Session named, CancellationToken ct = default) =>
        (await WalkAsync(sessions, named, ct).ConfigureAwait(false)).Head;
}
