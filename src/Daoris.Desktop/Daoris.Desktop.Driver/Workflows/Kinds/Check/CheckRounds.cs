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

    /// <summary>Whether <paramref name="node"/> already failed at <paramref name="tip"/>: asking again there spends a round the work never had, so the check waits for a commit.</summary>
    public static bool RedAt(NodeRecord? record, string node, string? tip) =>
        !string.IsNullOrEmpty(tip) && record?.Attempts.Any(attempt =>
            attempt is { Plugin: NodeKinds.Check, State: AttemptStates.Settled, Outcome: WorkStates.Failed }
            && attempt.Node == node && attempt.Call.SubjectRevision == tip) == true;

    /// <summary>
    /// Whether a <c>back</c> wire from <paramref name="source"/> is taken on its failure: always but from a check, and from a check only while
    /// its reds stay under <see cref="Bound"/>.
    /// </summary>
    public static bool SendsBack(NodeDeclaration? source, IReadOnlyDictionary<string, int> reds) =>
        source?.Kind != NodeKinds.Check || reds.GetValueOrDefault(source.Id) < Bound;
}
