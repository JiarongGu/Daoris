namespace Daoris.Driver;

/// <summary>
/// The rounds a red <c>check</c> goes back to the work: each failed verdict on a run's record is a round, a <c>back</c> wire from the check
/// carries the reading's words to the work's open session for the first rounds, and the round that reaches <see cref="Bound"/> is not sent
/// back: the risk policy decides it (<see cref="SpentChecks"/>), kept for its holder or at the owner's final exit as one row, never one per red.
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

    /// <summary>The newest attempt failing <paramref name="node"/> at <paramref name="tip"/>: asked again there before the work answers (<see cref="Answered"/>), it spends a round the work never had.</summary>
    public static NodeAttempt? RedAt(NodeRecord? record, string node, string? tip) =>
        string.IsNullOrEmpty(tip) ? null : record?.Attempts.LastOrDefault(attempt =>
            attempt is { Plugin: NodeKinds.Check, State: AttemptStates.Settled, Outcome: WorkStates.Failed }
            && attempt.Node == node && attempt.Call.SubjectRevision == tip);

    /// <summary>
    /// Whether the work answered a red asked at <paramref name="asked"/> with no commit: a session waits on the run's next step (<see cref="RunWaiters"/>)
    /// since after it, and none of the run's <paramref name="sessions"/> still works. The check then asks the same commit again as a round the bound counts.
    /// </summary>
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

    /// <summary>
    /// The reds a check's <c>back</c> wire to the work did not take because its rounds are spent (<see cref="SendsBack"/>), not told yet
    /// (<see cref="NodeRecord.ToldOf"/>) or told by an older build that kept no decision: each check's current failed attempt, settled failed, whose way back on <c>failed</c> goes to a
    /// <c>claimed</c> step, with its count of reds.
    /// </summary>
    public static IReadOnlyList<(string Node, int Attempt, int Reds)> SpentOwed(
        NodeRecord record, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, NodeDeclaration> declared)
    {
        var reds = Reds(record);
        var owed = new List<(string Node, int Attempt, int Reds)>();
        foreach (var wire in wires.Where(wire => wire is { Kind: WireKinds.Back, On: WireKinds.Failed, From.Node: not null }))
        {
            var source = wire.From!.Node;
            if (declared.GetValueOrDefault(source) is not { Kind: NodeKinds.Check } check || declared.GetValueOrDefault(wire.To.Node)?.Kind != NodeKinds.Claimed) continue;
            if (SendsBack(check, reds) || record.Statuses.GetValueOrDefault(source) is not { State: NodeStates.Failed } status) continue;
            if (record.Attempts.LastOrDefault(attempt => attempt.Node == source && attempt.Attempt == status.Attempt)
                    is not { State: AttemptStates.Settled, Outcome: WorkStates.Failed }
                || record.ToldOf(source, status.Attempt) is { } told && (!told.Spent || told.Decided is not null) || owed.Any(each => each.Node == source))
            {
                continue;
            }

            owed.Add((source, status.Attempt, reds.GetValueOrDefault(source)));
        }

        return owed;
    }

}
