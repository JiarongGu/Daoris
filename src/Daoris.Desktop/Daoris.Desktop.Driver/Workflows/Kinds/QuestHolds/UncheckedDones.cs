namespace Daoris.Driver;

/// <summary>
/// A done checked against nothing: the quest an ask published carries no requirement, so nothing compared its done with its ask. It is the act
/// <c>work.done</c>: the judge at its run's place passes it, so with nothing configured its <c>quest.holds</c> reaches by the policy with its
/// rule on its words (<see cref="RiskStepGates.Passed"/>), and where the place classes it <c>role</c> or <c>owner</c> the step waits on its role
/// until that holder passes it with their words (never the run's own working session); a version with its <c>yes</c> off lets it go.
/// </summary>
/// <remarks>
/// A quest one repository asks of another is asked on no ask, so it can carry no requirement, and its publisher reads its done at its own
/// <c>work.requests</c>; a set-up quest is Daoris's own, whose press reads the repository it set up: only an ask's quest is checked by nobody.
/// </remarks>
internal static class UncheckedDones
{
    private const string Lead = "its done is unchecked";

    /// <summary>
    /// The words an earlier build kept on an unchecked done's step when no cast named a reader, read as written; they end when no run record
    /// holds them (history's 30-day clear), as <c>QuestHoldsReading.YesLifts</c>' older words do.
    /// </summary>
    public const string GoesOn = "no one is set to read this done against its ask, so it goes on";

    public const string NoReader = "no-reader";

    /// <summary>Whether a hold's reason kept on its node (<see cref="NodeStatus.Holds"/>) is this one.</summary>
    public static bool Says(string? holds) => holds?.Contains(Lead, StringComparison.Ordinal) == true;

    /// <summary>Why <paramref name="quest"/>'s done waits unchecked, or null where it carries a requirement or no ask published it.</summary>
    public static string? Of(QuestView quest) =>
        quest.Requirements.Count == 0 && !SetupQuests.IsSetup(quest.Title) && AskSender.AskOf(quest.From) is { } ask
            ? $"{Lead}: ask #{ask} gave its quest no requirement, so nothing compared the done with what was asked; whoever holds the "
              + $"step's role (`{StepRoles.AskReader}` where it names none) reads its handback (quest #{quest.Id}'s done note) and passes this step "
              + "with their words where it does what was asked"
            : null;

    public static string? CodeOf(NodeDeclaration node, NodeStatus status) =>
        node.Kind == NodeKinds.QuestHolds && status.State == NodeStates.Reached && status.Words == GoesOn ? NoReader : null;
}
