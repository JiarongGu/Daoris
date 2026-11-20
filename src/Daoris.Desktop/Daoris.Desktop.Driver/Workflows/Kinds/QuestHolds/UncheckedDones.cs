namespace Daoris.Driver;

/// <summary>
/// A done checked against nothing: the quest an ask published carries no requirement, so nothing compared its done with its ask. Its
/// <c>quest.holds</c> waits on it as unchecked, with its handback to read, never reached as checked; whoever holds the step's role (the
/// <c>asker</c> where the step names none, cast like any role: the person, the steward or a supervisor's agent) passes it with their words,
/// and a version with its <c>yes</c> off lets it go.
/// </summary>
/// <remarks>
/// A quest one repository asks of another is asked on no ask, so it can carry no requirement, and its publisher reads its done at its own
/// <c>work.requests</c>; a set-up quest is Daoris's own, whose press reads the repository it set up: only an ask's quest is checked by nobody.
/// </remarks>
internal static class UncheckedDones
{
    private const string Lead = "its done is unchecked";

    /// <summary>Whether a hold's reason kept on its node (<see cref="NodeStatus.Holds"/>) is this one.</summary>
    public static bool Says(string? holds) => holds?.Contains(Lead, StringComparison.Ordinal) == true;

    /// <summary>Why <paramref name="quest"/>'s done waits unchecked, or null where it carries a requirement or no ask published it.</summary>
    public static string? Of(QuestView quest) =>
        quest.Requirements.Count == 0 && !SetupQuests.IsSetup(quest.Title) && AskSender.AskOf(quest.From) is { } ask
            ? $"{Lead}: ask #{ask} gave its quest no requirement, so nothing compared the done with what was asked; whoever holds the "
              + $"step's role (`{StepRoles.Asker}` where it names none) reads its handback (quest #{quest.Id}'s done note) and passes this step "
              + "with their words where it does what was asked"
            : null;
}
