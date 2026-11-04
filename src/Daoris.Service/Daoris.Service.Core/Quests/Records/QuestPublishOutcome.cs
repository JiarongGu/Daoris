namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="QuestPublishRefusal.None"/> when the quest was published.</param>
/// <param name="Message">The full answer, phrased once here so no two hosts can drift apart on it.</param>
/// <param name="Quest">The published quest, when there is one.</param>
/// <param name="Addressable">Who can be asked — the actionable half of a refusal.</param>
public sealed record QuestPublishOutcome(
    QuestPublishRefusal Refusal,
    string Message,
    Quest? Quest,
    IReadOnlyList<string> Addressable);
