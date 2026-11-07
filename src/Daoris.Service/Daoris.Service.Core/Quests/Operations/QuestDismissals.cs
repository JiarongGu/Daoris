namespace Daoris.Knowledge;

/// <summary>What a dismissal did: the quest as it now stands (null when there is no such quest), and how many conflicts went.</summary>
public sealed record QuestDismissal(Quest? Quest, int Dismissed);

/// <summary>Why the person's bring-forward of a lost take wrote nothing, or <see cref="None"/>.</summary>
public enum QuestForwardRefusal
{
    None,

    /// <summary>No quest under that id: never published here, or deleted.</summary>
    NotFound,

    /// <summary>
    /// The conflict named is not this machine's take that lost on the quest as it stands: none is so named, it is another machine's, it
    /// attempted another move than a take, or it went with an earlier publication of the quest. Only the machine whose take lost
    /// brings it forward.
    /// </summary>
    NotOurs,

    /// <summary>The conflict is dismissed already, plainly or brought forward, here or on another machine: a settled conflict authorizes no take.</summary>
    Settled,

    /// <summary>Another machine holds the step: the claim is exclusive, so the step is not this machine's while that take stands.</summary>
    TakenElsewhere,

    /// <summary>This machine holds the step already, by a take of its own since: nothing is left to bring forward, and the conflict is the person's to dismiss.</summary>
    TakenHere,

    /// <summary>The step is closed, done or declined: a closed step takes no take.</summary>
    Closed,

    /// <summary>The person's reopen holds the work the step grows from: withdrawn, it is taken by nobody until that work is ready again.</summary>
    SourceReopened,

    /// <summary>
    /// A session here still runs on the quest, the one whose take lost or a later one: the press waits until
    /// its process has ended, so a late done, <c>ready</c> or wait of the old session cannot stand on the fresh take. Judged last, once
    /// the press could otherwise stand.
    /// </summary>
    StillRunning,
}

/// <param name="Refusal"><see cref="QuestForwardRefusal.None"/> when it wrote the take and the dismissal.</param>
/// <param name="Quest">The quest as it now stands; null for no such id.</param>
/// <param name="Take">The fresh take it made, which the dismissal is marked with (<see cref="QuestOperation.Forward"/>); null on a refusal.</param>
public sealed record QuestBringingForward(QuestForwardRefusal Refusal, Quest? Quest, QuestOperationRef? Take = null);
