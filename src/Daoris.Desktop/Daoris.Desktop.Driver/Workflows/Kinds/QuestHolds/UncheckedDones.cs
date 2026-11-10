namespace Daoris.Driver;

/// <summary>
/// A done checked against nothing: the quest an ask published carries no requirement of the person's, so nothing compared its done
/// with what they asked. Its <c>quest.holds</c> waits on it as unchecked, with its handback to read, never reached as checked; the person's
/// pass of the step with their words is the way on, and a version with its <c>yes</c> off lets it go.
/// </summary>
/// <remarks>
/// A quest one repository asks of another is asked on no ask, so it can carry no requirement, and its publisher reads its done at its own
/// <c>work.requests</c>: only the person's quest is checked by nobody.
/// </remarks>
internal static class UncheckedDones
{
    /// <summary>Why <paramref name="quest"/>'s done waits unchecked, or null where it carries a requirement or no ask published it.</summary>
    public static string? Of(QuestView quest) =>
        quest.Requirements.Count == 0 && AskSender.AskOf(quest.From) is { } ask
            ? $"its done is unchecked: ask #{ask} gave its quest no requirement, so nothing compared the done with what you asked; read its "
              + $"handback (quest #{quest.Id}'s done note) and pass this step with your words where it does what you asked"
            : null;
}
