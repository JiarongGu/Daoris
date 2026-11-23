namespace Daoris.Driver;

/// <summary>
/// A check whose executor could not say what became of it (<c>unrun</c>, or unknown with no cause of its own, its reason carried on the
/// answer) is no question for the person: its attempt settles with no verdict and its node is asked again as a new attempt, a few times on
/// one binding (<see cref="Bound"/>), the attempt's own reason in the retry's line. Past them Daoris gives it up as said: its node is stopped
/// by Daoris with that reason (<see cref="GaveUp"/>), which ends its run and holds no row for anyone.
/// </summary>
public static class UnrunChecks
{
    /// <summary>The asks a check takes on one binding before Daoris gives it up.</summary>
    public const int Bound = 3;

    /// <summary>The code of a check given up as said.</summary>
    public const string GaveUp = "unsaid";

    /// <summary>Whether <paramref name="code"/> says the executor could not say.</summary>
    public static bool Is(string? code) => code is VerifyResult.Unrun or WorkStates.Unknown;

    /// <summary>
    /// The unknown attempt settled without a verdict, its node made ready for a new attempt with its <paramref name="why"/> in the retry; its asks
    /// spent, the node is stopped by Daoris, saying why.
    /// </summary>
    internal static (NodeRecord? Next, WorkPerformed Result) Again(
        NodeRecord record, NodeAttempt attempt, NodeDeclaration node, int generation, DateTimeOffset now, string? code, string? why)
    {
        var settled = WorkPerformer.With(record, attempt, attempt with { State = AttemptStates.Settled, Outcome = WorkStates.Unknown, Code = code, Generation = generation });
        var status = WorkPerformer.StatusOf(settled, node.Id);
        var ready = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        if (ready.Moved) return (WorkPerformer.Kept(settled, ready, "unrun-again", now), new(record.Unit, node.Id, PerformedWhat.Retry, code) { Detail = why });

        var words = $"the check could not say whether the work passes after {Bound} asks, so Daoris gave it up{(string.IsNullOrWhiteSpace(why) ? "" : $": {why.Trim().ReplaceLineEndings(" ")}")}";
        var stopped = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Stopped, NodeCause.Daoris) { Words = words });
        return stopped.Moved
            ? (WorkPerformer.Kept(settled, stopped, "unsaid", now), new(record.Unit, node.Id, PerformedWhat.Stale, GaveUp) { Detail = words })
            : WorkPerformer.Hold(record, attempt with { Generation = generation }, code ?? WorkStates.Unknown);
    }
}
