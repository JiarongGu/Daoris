namespace Daoris.Driver;

/// <summary>
/// The run's <c>again</c> on a check whose outcome is not known (<c>daoris workflow again &lt;run&gt; &lt;check&gt;</c>): kept as its current
/// attempt's <i>Run again</i> (<see cref="HeldResolutions.Again"/>), the one way a held check is asked again, which the next look applies: its
/// node stopped, the attempt settled as the person's to answer for, and the node made ready for a new attempt. An attempt Daoris still asks
/// after is held with that word: a check's effect is a verdict alone, so the person's new attempt is the way on, never a wait on the old one.
/// </summary>
public static class UnknownChecks
{
    /// <summary>Whether the run's <c>again</c> on <paramref name="node"/> standing at <paramref name="status"/> is a not-known check's.</summary>
    public static bool Takes(NodeDeclaration node, NodeStatus status) => node.Kind == NodeKinds.Check && status.State == NodeStates.Unknown;

    /// <summary>The record with the word kept on the node's current attempt, or why not: none stands unsettled, or a word is kept already.</summary>
    internal static (NodeRecord? Next, string? Refused) Again(NodeRecord record, NodeStatus status, DateTimeOffset now)
    {
        var attempt = record.Attempts.LastOrDefault(each => each.Node == status.Node && each.Attempt == status.Attempt);
        if (attempt is null || attempt.State == AttemptStates.Settled) return (null, WorkPerformer.NotAttempt);
        if (attempt.Resolution is not null) return (null, RunNodeWords.Worded);
        var held = attempt with { State = AttemptStates.Held, Code = attempt.Code ?? ReconcileStates.Unknown, Resolution = HeldResolutions.Again, ResolvedAt = now };
        return (WorkPerformer.With(record, attempt, held), null);
    }
}
