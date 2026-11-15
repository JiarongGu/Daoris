namespace Daoris.Driver;

/// <summary>
/// The rounds a red <c>check</c> goes back to the work before the person hears: each failed verdict on a run's record is a round, a
/// <c>back</c> wire from the check carries the reading's words to the work's open session for the first rounds, and the round that reaches
/// <see cref="Bound"/> is not sent back, so its failure is kept for the person as one row, never one per red.
/// </summary>
public static class CheckRounds
{
    /// <summary>The red rounds a check takes before the person hears of it.</summary>
    public const int Bound = 3;

    /// <summary>The failed verdicts each check node of <paramref name="record"/> has settled, by node id.</summary>
    public static IReadOnlyDictionary<string, int> Reds(NodeRecord record) =>
        record.Attempts
            .Where(attempt => attempt is { Plugin: NodeKinds.Check, State: AttemptStates.Settled, Outcome: WorkStates.Failed })
            .GroupBy(attempt => attempt.Node, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    /// <summary>
    /// The newest attempt with which <paramref name="node"/> already failed at <paramref name="tip"/>, or null: asking again there before the work
    /// answers it spends a round the work never had, so the check waits for a commit, or for the work's answer (<see cref="Answered"/>).
    /// </summary>
    public static NodeAttempt? RedAt(NodeRecord? record, string node, string? tip) =>
        string.IsNullOrEmpty(tip) ? null : record?.Attempts.LastOrDefault(attempt =>
            attempt is { Plugin: NodeKinds.Check, State: AttemptStates.Settled, Outcome: WorkStates.Failed }
            && attempt.Node == node && attempt.Call.SubjectRevision == tip);

    /// <summary>
    /// Whether the run's work answered a red asked at <paramref name="asked"/> without moving its commit: a session of this machine waits on
    /// the run's next step (<see cref="RunWaiters"/>) and began that wait after the red was asked, and none of the run's other sessions still
    /// works. Its turn ended with the red told to it as its next turn, so no commit is coming, and the check is asked again at the same commit
    /// as a new round, which the bound counts; waiting on would leave the check and the session each waiting on the other for good.
    /// </summary>
    /// <param name="sessions">The run's sessions (<see cref="RunScope.Sessions"/>), the executors of its quests.</param>
    public static bool Answered(IReadOnlyList<SessionRecord> records, string run, IReadOnlyCollection<string> sessions, DateTimeOffset asked)
    {
        var waiting = RunWaiters.Of(run, records).ToHashSet(StringComparer.Ordinal);
        var working = records.Any(record => record is { Teammate: false, Live: true } && !waiting.Contains(record.Id)
            && sessions.Contains(record.Id, StringComparer.Ordinal));
        return !working && records.Any(record => waiting.Contains(record.Id) && record.Updated > asked);
    }

    /// <summary>
    /// Whether a <c>back</c> wire from <paramref name="source"/> is taken on its failure: always but from a check, and from a check only while
    /// its reds stay under <see cref="Bound"/>.
    /// </summary>
    public static bool SendsBack(NodeDeclaration? source, IReadOnlyDictionary<string, int> reds) =>
        source?.Kind != NodeKinds.Check || reds.GetValueOrDefault(source.Id) < Bound;
}
