namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="QuestRespondRefusal.None"/> when the status moved.</param>
/// <param name="Message">The full answer, phrased once here.</param>
/// <param name="Quest">The quest as it now stands, when the status moved.</param>
public sealed record QuestRespondOutcome(QuestRespondRefusal Refusal, string Message, Quest? Quest)
{
    /// <summary>What a take made of the closure graph named for it: none on every other answer.</summary>
    public QuestTakeBind TakeBind { get; init; }
}
