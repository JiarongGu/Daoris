namespace Daoris.Driver;

/// <summary>
/// What a quest closed done still leaves, by its own record: the requirements its done departed from. **Read, never stored**: a
/// departure stays on the quest as it was answered, whether or not the person accepted it (their yes lets what follows go on, and
/// finishes nothing), so a done carrying one is unfinished work, never plain done. A done answering every requirement met, a quest
/// that is not done, and one that names no requirement leave nothing.
/// </summary>
public static class QuestLeft
{
    /// <summary>The answers of a done quest that departed, in the requirements' order; empty for any other quest.</summary>
    public static IReadOnlyList<QuestAnswerView> Departed(QuestView? quest) =>
        quest is { Status: "Done" }
            ? [.. quest.Answers.Where(answer => string.IsNullOrWhiteSpace(answer.Met) && !string.IsNullOrWhiteSpace(answer.Departed)).OrderBy(answer => answer.Requirement)]
            : [];

    /// <summary>Whether the quest's own record says part of the work is left.</summary>
    public static bool Has(QuestView? quest) => Departed(quest).Count > 0;
}
