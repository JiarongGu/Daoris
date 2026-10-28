namespace Daoris.Driver;

/// <summary>
/// Whether a taken quest's take stands this machine's after a session of it ended: the hand-over's judge asks it before a quest's session goes
/// on with another agent (MIXAGENT1a-ii), and the planner before it carries a quest on (CARRY2b). Course's pure rule, over the service's records,
/// which the planner's <c>TakenAway</c>, <c>TakenAt</c> and <c>BroughtForward</c> answer through.
/// </summary>
internal static class QuestTakes
{
    /// <summary>
    /// Whether a taken quest's take is not this machine's, as <c>TakenElsewhere</c> reads it (CARRY2b) and the hand-over's judge
    /// asks it before a quest's session goes on with another agent (MIXAGENT1a-ii): another's by the claim, or made here after
    /// <paramref name="last"/> ended by a chat or work outside Daoris that no record marks.
    /// </summary>
    internal static bool TakenAway(QuestView quest, PriorSession last, QuestTake take) =>
        !take.Here
        || (!take.Took && TakenAt(quest, take) is { } taken && last.Updated is { } ended && taken > ended && !BroughtForward(last, take));

    /// <summary>
    /// When a taken quest's take was made (CARRY3): the claim's own time, where its host said one. A host before it says none, and the
    /// quest's <c>updated</c> stands in for it, as before, though a submission, an evidence read, a reopen and a go-on move that too.
    /// </summary>
    internal static DateTimeOffset? TakenAt(QuestView quest, QuestTake take) => take.At ?? quest.Updated;

    /// <summary>
    /// Whether <paramref name="lost"/>, a record of this machine's that stood down, is carried on by the person's bring-forward of its lost
    /// take (SYNCMERGE1b, D178 point 3), as the ledger opens it (SYNCMERGE1b0 point 1): <paramref name="take"/> stands this machine's by a
    /// press made after the record was created. Its creation, not its end, so a stand-down written after the press still goes on. Once:
    /// the session carried on is opened after the press, so its own stand-down is no carry-on.
    /// </summary>
    /// <remarks>A record with no creation said is none: nothing could tell the press after it.</remarks>
    internal static bool BroughtForward(PriorSession lost, QuestTake take) =>
        string.Equals(lost.State, "stood-down", StringComparison.OrdinalIgnoreCase)
        && !lost.Teammate
        && take is { Here: true, Forward: { Stands: true } forward }
        && lost.Created is { } created
        && forward.At > created;
}
