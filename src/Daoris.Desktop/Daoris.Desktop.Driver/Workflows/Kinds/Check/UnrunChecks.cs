namespace Daoris.Driver;

/// <summary>
/// A check that came back <c>unrun</c>, which Daoris could not run (never a failure of the work): its attempt settles with no verdict and its
/// node is asked again as a new attempt, so it counts as neither a red round (<see cref="CheckRounds.Reds"/>) nor a pass. Only a few such
/// attempts are made on one binding (<see cref="Bound"/>); past them the step is held for the person, as any unknown is.
/// </summary>
public static class UnrunChecks
{
    /// <summary>The asks a check takes on one binding where Daoris keeps being unable to run it, before the person hears.</summary>
    public const int Bound = 3;

    /// <summary>Whether <paramref name="code"/> is the reason a check was not run.</summary>
    public static bool Is(string? code) => code == VerifyResult.Unrun;

    /// <summary>
    /// <paramref name="attempt"/>, the unknown answer of the current attempt of <paramref name="node"/>, settled without a verdict and its node
    /// made ready for a new attempt; held for the person where the table refuses it, its asks being spent.
    /// </summary>
    internal static (NodeRecord? Next, WorkPerformed Result) Again(
        NodeRecord record, NodeAttempt attempt, NodeDeclaration node, int generation, DateTimeOffset now)
    {
        var settled = WorkPerformer.With(record, attempt, attempt with { State = AttemptStates.Settled, Outcome = WorkStates.Unknown, Code = VerifyResult.Unrun, Generation = generation });
        var ready = NodeLifecycle.Apply(node, WorkPerformer.StatusOf(settled, node.Id), new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        return ready.Moved
            ? (WorkPerformer.Kept(settled, ready, "unrun-again", now), new(record.Unit, node.Id, PerformedWhat.Retry, VerifyResult.Unrun))
            : WorkPerformer.Hold(record, attempt with { Generation = generation }, VerifyResult.Unrun);
    }
}
