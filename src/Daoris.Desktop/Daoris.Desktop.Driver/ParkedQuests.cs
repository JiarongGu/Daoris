namespace Daoris.Driver;

/// <summary>A quest the loop's last tick parked by its failed sessions (DRV6): its id, and the repository it is addressed to.</summary>
/// <param name="Failures">
/// How many sessions have failed on it here in all, as the records count them (<see cref="Consideration.Failures"/>), which a
/// retry marks it at (RETRY1b); null where the verdict carried none.
/// </param>
public sealed record ParkedQuest(string Quest, string Repository, int? Failures = null);

/// <summary>
/// A quest the loop's last look held by the person's stop (SESSUX1b, D126 §3.3): its id, the repository it is addressed
/// to, and the session they stopped, which a release names.
/// </summary>
public sealed record HeldQuest(string Quest, string Repository, string Session)
{
    /// <summary>
    /// The quests these considerations hold by the person's stop, in their order; none for none. Read from the loop's last
    /// look (<see cref="LastLook"/>), which is replaced whole each look, so a quest released, done or gone is not offered.
    /// </summary>
    public static IReadOnlyList<HeldQuest> From(IEnumerable<Consideration>? considered) =>
        [.. (considered ?? [])
            .Where(consideration => consideration is { Verdict: StartVerdict.Stopped, HeldBy: not null })
            .Select(consideration => new HeldQuest(consideration.Quest.Id, consideration.Quest.To, consideration.HeldBy!.Session))];
}

/// <summary>
/// The quests the loop's last tick parked by their failed sessions (DRV6, <see cref="StartVerdict.Exhausted"/>): the
/// verdict the quest drawer shows its Retry by, and what Ask Daoris's <c>retry</c> is judged against (HELP10, D110).
/// </summary>
/// <remarks>
/// Kept as <see cref="TrustHolds"/> keeps the trust holds, and for the same reason: replaced whole each tick, so a
/// quest retried, done or gone is no longer offered. A helper can invent a quest id, and forgiving one that is not
/// parked lets it run past its strikes (D110), so nothing wider than the last tick's verdict is ever offered.
/// </remarks>
public sealed class ParkedQuests
{
    private volatile IReadOnlyList<ParkedQuest> _latest = [];

    /// <summary>What the last tick parked, in its order; empty before any tick.</summary>
    public IReadOnlyList<ParkedQuest> Latest => _latest;

    public void Record(IEnumerable<Consideration> considered) =>
        _latest = [.. considered
            .Where(consideration => consideration.Verdict == StartVerdict.Exhausted)
            .Select(consideration => new ParkedQuest(consideration.Quest.Id, consideration.Quest.To, consideration.Failures))];
}
