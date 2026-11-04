namespace Daoris.Knowledge;

/// <summary>
/// Who holds a quest's step and how each take that lost on it stands or ended, read from its log (SYNCMERGE1b0, D178 point 7): the take the
/// quest stands taken by, null unless it is taken; the conflicts still on it; and those settled, in the order they were.
/// </summary>
public sealed record QuestTakeReading(
    QuestOperation? Take, IReadOnlyList<QuestConflictReading> Conflicts, IReadOnlyList<QuestConflictReading> Settled)
{
    /// <summary>Nothing taken and nothing lost: a quest with no history, or none of either.</summary>
    public static readonly QuestTakeReading None = new(null, [], []);

    /// <summary>The machine whose take holds the step, an id no person gave; null unless the quest is taken.</summary>
    public string? Holder => Take?.Machine;

    /// <summary>
    /// The bring-forward the step is held by, where <paramref name="machine"/>'s take holds it and a bring-forward made that take: the
    /// one take a stood-down session of that machine's is carried on over (SessionLedger), and the claim's answer names. Null otherwise.
    /// </summary>
    public QuestForwardTake? BroughtForwardBy(string machine) =>
        Take is { } held && held.Machine == machine
            ? Settled.Where(conflict => conflict.Ended == QuestConflictEnd.BroughtForward)
                .Select(conflict => conflict.Forward)
                .FirstOrDefault(forward => forward is not null && forward.Take == new QuestOperationRef(held.Machine, held.Sequence))
            : null;
}
