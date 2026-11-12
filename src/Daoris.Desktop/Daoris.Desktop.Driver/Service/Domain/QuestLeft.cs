namespace Daoris.Driver;

/// <summary>
/// What a quest closed done still leaves by its own record: the requirements its done departed from. Read, never stored; a departure
/// needs the person until they accept it, and reads done, part left after, never the plain done.
/// </summary>
public static class QuestLeft
{
    public static IReadOnlyList<QuestAnswerView> Departed(QuestView? quest) =>
        quest is { Status: "Done" }
            ? [.. quest.Answers.Where(answer => string.IsNullOrWhiteSpace(answer.Met) && !string.IsNullOrWhiteSpace(answer.Departed)).OrderBy(answer => answer.Requirement)]
            : [];

    public static bool Has(QuestView? quest) => Departed(quest).Count > 0;

    public static bool Waits(QuestView? quest) => quest is { Held: true } && Has(quest);

    public static bool Accepted(QuestView? quest) => quest is { Held: false } && Has(quest);
}
