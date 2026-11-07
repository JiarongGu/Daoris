namespace Daoris.Knowledge;

/// <param name="Quest">The quest as it now stands — null when no such id exists.</param>
/// <param name="Moved">Whether THIS call moved it. False with a non-null quest is a refused move.</param>
/// <param name="FollowUp">The chain's next step, published by this close — null when there is none.</param>
public sealed record QuestMove(Quest? Quest, bool Moved, Quest? FollowUp = null)
{
    /// <summary>
    /// Refused because this machine's take on the quest lost, and the take that beat it stands: the move or
    /// wait would be made on a claim this machine never held. Said apart from the table's refusals, so the answer can say why.
    /// </summary>
    public bool ClaimLost { get; init; }

    /// <summary>
    /// With <see cref="ClaimLost"/>: the take lost to the person's reopen of the work its step grew from
    /// (<see cref="QuestLog.LostToTheReopen"/>), read inside the write. On an open step a take that lost otherwise lost with the work it
    /// rests on, the step standing on another submission since, and on a taken quest to another machine's take;
    /// the refusal's words say which.
    /// </summary>
    public bool LostToTheReopen { get; init; }

    /// <summary>
    /// Refused because the quest's graph closes it: an agent's done on a quest bound to a closure graph,
    /// which says <c>ready</c> instead, so no done slips past the graph.
    /// </summary>
    public bool ClosedByGraph { get; init; }

    /// <summary>
    /// The successor a sealed submission published, naming why a later <c>ready</c> is refused: it
    /// stands on the submission it grew from. Null on every other move.
    /// </summary>
    public string? SealedBy { get; init; }

    /// <summary>
    /// Refused because the work the quest grew from is reopened by the person: an agent's take, done or
    /// <c>ready</c> on a step withdrawn or held for the person's choice.
    /// </summary>
    public bool SourceReopened { get; init; }
}
