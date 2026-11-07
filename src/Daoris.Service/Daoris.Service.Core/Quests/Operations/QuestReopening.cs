namespace Daoris.Knowledge;

/// <summary>Why the person's reopen, or their word to go on, moved nothing, or <see cref="None"/>.</summary>
public enum QuestReopenRefusal
{
    None,

    /// <summary>No quest under that id.</summary>
    NotFound,

    /// <summary>Not taken: open, or closed done or declined, so it has no submission to reopen.</summary>
    NotTaken,

    /// <summary>Taken on another machine, or on a take this machine lost: the submission and its seal are that machine's.</summary>
    NotHeld,

    /// <summary>The submission named is not the quest's current one, or it has none.</summary>
    NotCurrent,

    /// <summary>The submission published no step that stands on it, so nothing seals it: a later <c>ready</c> replaces it.</summary>
    NotSealed,

    /// <summary>The step it published is closed, done or declined, on the submission it grew from, and a reopen takes no close back.</summary>
    StepClosed,

    /// <summary>A word to go on with a step no reopen holds.</summary>
    NotWithdrawn,

    /// <summary>A word to go on while the source has no unheld submission to grow from, so the step waits for its work again.</summary>
    SourceNotReady,
}

/// <param name="Refusal"><see cref="QuestReopenRefusal.None"/> when it moved.</param>
/// <param name="Quest">The quest named, as it now stands; null for no such id.</param>
/// <param name="Step">The step it moved with it, as it now stands: the reopened submission's, or the source a word to go on read.</param>
public sealed record QuestReopening(QuestReopenRefusal Refusal, Quest? Quest, Quest? Step)
{
    /// <summary>
    /// For a word to go on: the step's own successor, which the submission it cleared had published, as the same write reopened it,
    /// withdrawn or held. Null where none stood on that submission.
    /// </summary>
    public Quest? Successor { get; init; }
}
