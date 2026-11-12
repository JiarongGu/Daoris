namespace Daoris.Driver;

/// <summary>
/// What a quest closed done still leaves by its own record: the requirements its done departed from. Read, never stored; the person's
/// yes lets what follows go on and finishes none of it, so a done with a departure on it is unfinished, accepted or not.
/// </summary>
public static class QuestLeft
{
    public static IReadOnlyList<QuestAnswerView> Departed(QuestView? quest) =>
        quest is { Status: "Done" }
            ? [.. quest.Answers.Where(answer => string.IsNullOrWhiteSpace(answer.Met) && !string.IsNullOrWhiteSpace(answer.Departed)).OrderBy(answer => answer.Requirement)]
            : [];

    public static bool Has(QuestView? quest) => Departed(quest).Count > 0;
}
