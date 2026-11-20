namespace Daoris.Driver;

/// <summary>
/// A done checked against nothing: the quest an ask published carries no requirement, so nothing compared its done with its ask. Its
/// <c>quest.holds</c> waits on it only where a role the step names, or the place's cast of the <c>ask-reader</c>, names who reads it, until
/// that holder passes it with their words (never the run's own working session); a version with its <c>yes</c> off lets it go. With no
/// reader named it goes on by itself (<see cref="GoesOn"/>).
/// </summary>
/// <remarks>
/// A quest one repository asks of another is asked on no ask, so it can carry no requirement, and its publisher reads its done at its own
/// <c>work.requests</c>; a set-up quest is Daoris's own, whose press reads the repository it set up: only an ask's quest is checked by nobody.
/// </remarks>
internal static class UncheckedDones
{
    private const string Lead = "its done is unchecked";

    /// <summary>The words an unchecked done no one is set to read goes on with, kept on its step's reach.</summary>
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

    /// <summary>
    /// Whether nothing names who reads <paramref name="node"/>'s unchecked done, so it goes on: the step names no role of its own, and the place's
    /// cast of its role, read this pass (<paramref name="cast"/>), is the default (no cast, a place not at high risk). Where the cast is not read,
    /// someone may be named, so it waits.
    /// </summary>
    public static bool Unread(NodeDeclaration node, Func<string, CastNow>? cast) =>
        cast is not null && StepRoles.Named(node) is null or StepRoles.AskReader && cast(StepRoles.Of(node)).Why == CastWhy.Default;

    public static string? CodeOf(NodeDeclaration node, NodeStatus status) =>
        node.Kind == NodeKinds.QuestHolds && status.State == NodeStates.Reached && status.Words == GoesOn ? NoReader : null;
}
