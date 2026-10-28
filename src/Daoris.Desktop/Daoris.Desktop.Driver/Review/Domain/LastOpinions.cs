namespace Daoris.Driver;

/// <summary>
/// One owed landing opinion's gate as the last look judged it (XAGENT1g3): the work, since when its opinion has been owed, whether
/// it lands by itself, and the gate as <see cref="SessionTreeGates.GateAsync"/> reads it for the route that answers *What needs you*.
/// </summary>
public sealed record OpinionJudged(
    string Session, string Quest, string Repository, string Workspace, DateTimeOffset Since, bool Auto, OpinionGateState Gate);

/// <summary>
/// The second-opinion gates the loop's last look judged (XAGENT1g3, D155): each owed landing opinion's gate, kept so the attention
/// band's route reads them instead of running a gate per owed session at every poll.
/// </summary>
/// <remarks>
/// Kept as <see cref="LastLook"/> is, and replaced whole each look: a judgment is as of the last look, at most one poll old, and a
/// work whose opinion closed is not offered. <c>steps</c> opinions are not kept; the route is for the landing's.
/// </remarks>
public sealed class LastOpinions
{
    private volatile IReadOnlyList<OpinionJudged>? _judged;

    /// <summary>Each owed landing opinion the last look judged, oldest first, one per session; null before any look.</summary>
    public IReadOnlyList<OpinionJudged>? Latest => _judged;

    public void Record(IEnumerable<OpinionJudged> judged) => _judged = [.. judged];
}
