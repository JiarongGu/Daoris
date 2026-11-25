namespace Daoris.Driver;

/// <summary>
/// A check whose executor could not say what became of it (<c>unrun</c>, or unknown with no cause of its own, its reason carried on the
/// answer) is no question for the person: its attempt settles with no verdict and its node is asked again as a new attempt, a few times on
/// one binding (<see cref="Bound"/>), the attempt's own reason in the retry's line. Past them the attempt is held as given up
/// (<see cref="GaveUp"/>), never stopped (a stopped node ends the run, its work unlanded), and the run waits for the new attempt a person or
/// the work's session asks (<see cref="UnknownChecks"/>).
/// </summary>
public static class UnrunChecks
{
    /// <summary>The asks a check takes on one binding before Daoris gives it up.</summary>
    public const int Bound = 3;

    /// <summary>The code of a check given up as said.</summary>
    public const string GaveUp = "unsaid";

    /// <summary>Whether <paramref name="code"/> says the executor could not say.</summary>
    public static bool Is(string? code) => code is VerifyResult.Unrun or VerifyResult.Load or WorkStates.Unknown;

    /// <summary>The unknown attempt settled and its node made ready again with <paramref name="why"/>; asks spent, it is held as given up.</summary>
    internal static (NodeRecord? Next, WorkPerformed Result) Again(
        NodeRecord record, NodeAttempt attempt, NodeDeclaration node, int generation, DateTimeOffset now, string? code, string? why)
    {
        var settled = WorkPerformer.With(record, attempt, attempt with { State = AttemptStates.Settled, Outcome = WorkStates.Unknown, Code = code, Generation = generation });
        var status = WorkPerformer.StatusOf(settled, node.Id);
        var ready = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        if (ready.Moved) return (WorkPerformer.Kept(settled, ready, "unrun-again", now), new(record.Unit, node.Id, PerformedWhat.Retry, code) { Detail = why });

        var words = $"the check could not say whether the work passes after {Bound} asks, so Daoris gave it up and the run waits for a new attempt{(string.IsNullOrWhiteSpace(why) ? "" : $": {why.Trim().ReplaceLineEndings(" ")}")}";
        var reason = string.IsNullOrWhiteSpace(why) ? null : why.Trim().ReplaceLineEndings(" ");
        var held = WorkPerformer.With(record, attempt, attempt with
        {
            State = AttemptStates.Held, Outcome = WorkStates.Unknown, Code = GaveUp, Generation = generation, Tail = reason is { Length: > LostChecks.TailChars } ? reason[..LostChecks.TailChars] : reason,
        });
        return (held, new(record.Unit, node.Id, PerformedWhat.Held, GaveUp) { Detail = words });
    }
}
