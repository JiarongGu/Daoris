namespace Daoris.Driver;

/// <summary>
/// What a quest closed done still leaves by its own record: the requirements its done departed from. Read, never stored; the person's
/// yes lets what follows go on and finishes none of it, so a done with a departure on it is never the plain done; it needs the person
/// until they accept the departure, and reads done, part left after.
/// </summary>
public static class QuestLeft
{
    public static IReadOnlyList<QuestAnswerView> Departed(QuestView? quest) =>
        quest is { Status: "Done" }
            ? [.. quest.Answers.Where(answer => string.IsNullOrWhiteSpace(answer.Met) && !string.IsNullOrWhiteSpace(answer.Departed)).OrderBy(answer => answer.Requirement)]
            : [];

    public static bool Has(QuestView? quest) => Departed(quest).Count > 0;

    /// <summary>A departure still held for the person's yes: unfinished work that needs them.</summary>
    public static bool Waits(QuestView? quest) => quest is { Held: true } && Has(quest);

    /// <summary>A departure the person accepted: their word on it, so it reads done, part left, and asks nothing.</summary>
    public static bool Accepted(QuestView? quest) => quest is { Held: false } && Has(quest);
}
