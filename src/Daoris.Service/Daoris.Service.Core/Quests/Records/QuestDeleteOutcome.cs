namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="QuestDeleteRefusal.None"/> when the quest is deleted.</param>
/// <param name="Message">The full answer, phrased once here for every door.</param>
/// <param name="Quest">The quest as it stood before the delete, when there is one.</param>
public sealed record QuestDeleteOutcome(QuestDeleteRefusal Refusal, string Message, Quest? Quest)
{
    /// <summary>Deleted here, and its remote has not taken the delete yet — the next pass carries it.</summary>
    public bool Unconfirmed { get; init; }
}
