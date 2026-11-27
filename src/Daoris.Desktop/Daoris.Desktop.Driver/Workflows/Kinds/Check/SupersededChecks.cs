namespace Daoris.Driver;

/// <summary>
/// The checks a new commit superseded, ended at the pass: an attempt still waiting or running for a node that no longer holds it
/// (invalidated, or ready again) at a commit other than the run's branch tip. Such an attempt keeps its node's gate until it answers, so the
/// check at the tip would wait out gates whose verdict nothing reads; its job ends as at a stop, its words say why, and its answer settles it
/// as a stale attempt's. A check on the landing's commit, or at the tip, runs on. Each attempt is ended once a process.
/// </summary>
internal static class SupersededChecks
{
    private const int Most = 4096;

    private static readonly HashSet<string> Ended = new(StringComparer.Ordinal);

    public static void End(WorkPerformer performer, string unit, string? tip, IReadOnlySet<string> afterHanded)
    {
        if (tip is null || performer.RecordOf(unit) is not { } record) return;
        foreach (var (attempt, why) in Of(record, tip, afterHanded))
        {
            if (performer.PortOf(attempt.Plugin) is not { } check) continue;
            lock (Ended)
            {
                if (Ended.Count >= Most) Ended.Clear();
                if (!Ended.Add(attempt.Call.Invocation)) continue;
            }

            check.Supersede(attempt.Call.Invocation, why);
        }
    }

    /// <summary>The checks of <paramref name="record"/> that <paramref name="tip"/> superseded, each with the words its result says.</summary>
    public static IReadOnlyList<(NodeAttempt Attempt, string Why)> Of(NodeRecord record, string tip, IReadOnlySet<string> afterHanded)
    {
        if (!WorkingTree.IsCommitId(tip)) return [];
        var found = new List<(NodeAttempt, string)>();
        foreach (var attempt in record.Attempts)
        {
            if (attempt.Plugin != NodeKinds.Check || attempt.State is not (AttemptStates.Sent or AttemptStates.Recorded or AttemptStates.Unknown)
                || afterHanded.Contains(attempt.Node) || !WorkingTree.IsCommitId(attempt.Call.SubjectRevision)
                || string.Equals(attempt.Call.SubjectRevision, tip, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var status = WorkPerformer.StatusOf(record, attempt.Node);
            if (status.Unreconciled != attempt.Attempt || status.State is NodeStates.Active or NodeStates.Unknown) continue;
            found.Add((attempt,
                $"the work moved on to {TextBounds.ShortCommit(tip, 8)} after it was sent at {TextBounds.ShortCommit(attempt.Call.SubjectRevision, 8)}"));
        }

        return found;
    }
}
