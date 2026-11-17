namespace Daoris.Driver;

/// <summary>
/// A holder's <i>Done outside Daoris</i> link waiting on its plugin's check (<see cref="WorkLinkChecks"/>): held, never applied on the word
/// alone, until the check answers and marks the attempt checked (<see cref="NodeAttempt.LinkChecked"/>). A check its plugin cannot give (down,
/// the pass's sends spent, an error) keeps it waiting; past the step's timeout from the word the wait climbs the reporting line and keeps
/// holding, so a skip or a stop by its line still ends it.
/// </summary>
public static class LinkHolds
{
    /// <summary>What a pass says of a link whose check did not answer by its deadline: its wait climbed the line.</summary>
    public const string Climbed = "link-climbed";

    /// <summary>Where a link no check answered waits: a wait's help, on the first level that holds it.</summary>
    public static LineWait Line() => ReportingLine.Of([], SupervisorKinds.Wait, new RolePlace()).Climb(ownerClass: false);

    /// <summary>Whether the word on <paramref name="attempt"/> still waits on its plugin's check.</summary>
    internal static bool Waits(NodeRecord record, NodeAttempt attempt, NodeDeclaration node) => WorkLinkChecks.Asks(record, attempt, node);

    /// <summary>Whether its wait reached its deadline, and its progress does not say so already.</summary>
    internal static bool Due(NodeAttempt attempt, NodeDeclaration node, DateTimeOffset now) =>
        attempt.ResolvedAt is { } worded && now >= worded + WorkPerformer.Timeout(node)
        && attempt.Progress?.Words?.StartsWith(Line().Said(), StringComparison.Ordinal) != true;

    /// <summary>The attempt with its wait said at the level it climbed to, the link its page.</summary>
    internal static NodeAttempt Held(NodeAttempt attempt, DateTimeOffset at) => attempt with
    {
        Progress = new StepProgress(null, $"{Line().Said()}: its plugin has not checked the Done outside Daoris link since {Moments.Write(attempt.ResolvedAt!.Value)}",
            attempt.Link, null, at),
    };
}
