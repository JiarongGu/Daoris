namespace Daoris.Driver;

/// <summary>
/// A check whose executor could not say what became of it (`unrun`, or unknown with no cause of its own) is no question for the person: its
/// attempt settles with no verdict and its node is asked again as a new attempt, a few times on one binding (<see cref="Bound"/>), with the
/// attempt's own reason in the retry. Past them it is given up as said (<see cref="GaveUp"/>) and decided as a spent check
/// (<see cref="SpentChecks"/>), never held for the person.
/// </summary>
public static class UnrunChecks
{
    /// <summary>The asks a check takes on one binding before Daoris gives it up.</summary>
    public const int Bound = 3;

    /// <summary>The failure code of a check given up as said.</summary>
    public const string GaveUp = "unsaid";

    /// <summary>Whether <paramref name="code"/> says the executor could not say.</summary>
    public static bool Is(string? code) => code is VerifyResult.Unrun or WorkStates.Unknown;

    /// <summary>The unknown attempt settled and its node made ready again, its <paramref name="why"/> in the retry; null where its asks are spent.</summary>
    internal static (NodeRecord? Next, WorkPerformed Result)? Again(
        NodeRecord record, NodeAttempt attempt, NodeDeclaration node, int generation, DateTimeOffset now, string? code, string? why)
    {
        var settled = WorkPerformer.With(record, attempt, attempt with { State = AttemptStates.Settled, Outcome = WorkStates.Unknown, Code = code, Generation = generation });
        var ready = NodeLifecycle.Apply(node, WorkPerformer.StatusOf(settled, node.Id), new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        return ready.Moved
            ? (WorkPerformer.Kept(settled, ready, "unrun-again", now), new(record.Unit, node.Id, PerformedWhat.Retry, code) { Detail = why })
            : null;
    }
}
