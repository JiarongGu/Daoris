namespace Daoris.Driver;

/// <summary>
/// A check whose answer binds to no work (<see cref="Retried"/>) is no question for the person: its attempt settles with no verdict and its
/// node is asked again on the work as it stands, a few times on one binding (<see cref="Bound"/>), its reason in the retry's line. Past them
/// the attempt is held as given up (<see cref="GaveUp"/>), never stopped (a stopped node ends the run, its work unlanded), and the run waits
/// for the new attempt a person or the work's session asks (<see cref="UnknownChecks"/>). <c>changed</c>, <c>stopped</c> and <c>bound</c> stay the person's.
/// </summary>
public static class UnrunChecks
{
    /// <summary>The asks a check takes on one binding before Daoris gives it up.</summary>
    public const int Bound = 3;

    /// <summary>The code of a check given up as said.</summary>
    public const string GaveUp = "unsaid";

    /// <summary>Whether <paramref name="code"/>, an executor's unknown, is one only a new attempt answers.</summary>
    public static bool Is(string? code) => code is VerifyResult.Unrun or VerifyResult.Load or VerifyResult.Unprepared or VerifyResult.Busy or WorkStates.Unknown;

    /// <summary>Those, a process lost on its rerun too, and a check done with no verdict; a refused verdict passes its refusal's code.</summary>
    internal static bool Retried(string? code) => Is(code) || code is LostChecks.Code or WorkPerformer.NoOutcome;

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

    /// <summary>
    /// Every check an older build holds for the person under a <see cref="Retried"/> code, unworded and its node still on it, asked again.
    /// The reader of records already written: it ends when no install holds one, since this build holds none (its asks spent, it holds <see cref="GaveUp"/>).
    /// </summary>
    public static IReadOnlyList<WorkPerformed> Released(NodeRecordFiles records, Func<string, string, NodeDeclaration?> declared, DateTimeOffset now)
    {
        var done = new List<WorkPerformed>();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(Holds).ToList())
            {
                if (declared(record.Unit, attempt.Node) is not { } node) continue;
                var released = records.Edited(record.Unit, node.Id, current =>
                    current.Attempts.FirstOrDefault(each => each.Node == attempt.Node && each.Attempt == attempt.Attempt) is { } held && Holds(held)
                    && WorkPerformer.StatusOf(current, node.Id) is { State: NodeStates.Unknown } status && status.Attempt == held.Attempt
                        ? Again(current, held, node, held.Generation ?? 0, now, held.Code, $"held by an older build ({held.Code}), which only a new attempt answers")
                        : (null, new WorkPerformed(record.Unit, node.Id, PerformedWhat.NotTaken)));
                if (released.What is PerformedWhat.Retry or PerformedWhat.Held) done.Add(released);
            }
        }

        return done;
    }

    private static bool Holds(NodeAttempt attempt) =>
        attempt is { Plugin: NodeKinds.Check, State: AttemptStates.Held, Resolution: null } && Retried(attempt.Code);
}
