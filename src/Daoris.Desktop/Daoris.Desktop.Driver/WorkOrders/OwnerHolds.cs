namespace Daoris.Driver;

/// <summary>
/// The person's own holds on a quest's line, which only the person releases: a stop on its session, and its quest's pause or stop as
/// the planner reads them. A steward's stop is the person's to every reader but the steward's release of its own stop of this run. The
/// steward's hand-over and a supervisor's course and limit releases read them here, since a go-on, a move or a hand-over would release
/// what only the person releases.
/// </summary>
public static class OwnerHolds
{
    /// <summary>
    /// Whether <paramref name="record"/>'s line is held by the person: its session stopped, or, given <paramref name="plan"/>, its quest
    /// paused or stopped there. <paramref name="stewardOwn"/> names the session whose stop is the steward's own of this run, passed.
    /// </summary>
    public static bool Holds(PriorSession? record, IReadOnlyList<Consideration>? plan, string? stewardOwn = null)
    {
        if (record is null) return false;
        if (record.PersonStopped && !(record.StewardStopped && string.Equals(stewardOwn, record.Session, StringComparison.Ordinal))) return true;
        if (record.Quest is not { } quest || plan?.FirstOrDefault(each => string.Equals(each.Quest.Id, quest, StringComparison.OrdinalIgnoreCase)) is not { } verdict) return false;
        return verdict switch
        {
            { Verdict: StartVerdict.Paused } => true,
            { Verdict: StartVerdict.Stopped, HeldBy: { } stop } => !(stop.StewardStopped && string.Equals(stewardOwn, stop.Session, StringComparison.Ordinal)),
            { Verdict: StartVerdict.Stopped } => true,
            _ => false,
        };
    }
}
