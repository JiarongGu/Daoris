namespace Daoris.Driver;

/// <summary>
/// A closure graph's end the milestone tells the person of (FABRIC-CLOSE1j; the fabric events design §E5): ended by their own done before
/// its end (<see cref="QuestClosing.Overridden"/>), naming the nodes it stopped; or closed only in part (<see cref="QuestClosing.Partial"/>),
/// naming the quest lost with its take and the close node, whose stop is offered while that node is live.
/// </summary>
/// <param name="Unit">The unit (<see cref="WorkUnit.Id"/>).</param>
/// <param name="Kind"><see cref="QuestClosing.Overridden"/> or <see cref="QuestClosing.Partial"/>.</param>
/// <param name="At">When it was found.</param>
public sealed record GraphEnd(string Unit, string Kind, DateTimeOffset At)
{
    /// <summary>Overridden: the nodes the person's done stopped.</summary>
    public IReadOnlyList<string> Nodes { get; init; } = [];

    /// <summary>Partially closed: the quest no longer done on the submission its close judged.</summary>
    public string? Quest { get; init; }

    /// <summary>Partially closed: the close node, and the attempt the person's stop is said on (<c>WORK_HELD</c>'s door, PERFORM1g).</summary>
    public string? Node { get; init; }

    public int? Attempt { get; init; }

    /// <summary>Partially closed: where the close node stands now (<see cref="NodeStates"/>).</summary>
    public string? NodeState { get; init; }

    /// <summary>The person's stop, given and not applied yet, or null.</summary>
    public string? Resolution { get; init; }

    /// <summary>The words the person may say of it: <c>stop</c> while the close node is live again (<see cref="QuestClosing.Partly"/>), else none.</summary>
    public IReadOnlyList<string> Words { get; init; } = [];
}

/// <summary>
/// The closure graphs' ends the person is told of, read from the units' node records' history (FABRIC-CLOSE1j): what <c>WORK_STATE</c>
/// lists beside the held attempts. Read-only: no record is written, no lease taken. Derived, stored nowhere but the history it reads (§D0).
/// </summary>
public static class GraphEnds
{
    /// <summary>Every graph's end on the records that read, oldest first; with <paramref name="unit"/>, that unit's alone.</summary>
    public static IReadOnlyList<GraphEnd> Read(NodeRecordFiles records, string? unit = null) =>
        [.. records.All().Records
            .Where(record => unit is null || string.Equals(record.Unit, unit, StringComparison.Ordinal))
            .SelectMany(Of)
            .OrderBy(end => end.At)];

    /// <summary>One record's ends: each override by the moment it stopped its nodes, and each quest found lost since its close.</summary>
    public static IEnumerable<GraphEnd> Of(NodeRecord record)
    {
        foreach (var stop in record.History.Where(entry => entry.Why == QuestClosing.Overridden).GroupBy(entry => entry.At))
        {
            yield return new GraphEnd(record.Unit, QuestClosing.Overridden, stop.Key) { Nodes = [.. stop.Select(entry => entry.Was.Node).Distinct(StringComparer.Ordinal)] };
        }

        var prefix = QuestClosing.Partial + " #";
        foreach (var entry in record.History.Where(entry => entry.Why.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var node = entry.Was.Node;
            var newest = record.Attempts.LastOrDefault(attempt => attempt.Node == node);
            var offered = newest is not null && QuestClosing.Partly(record, newest);
            yield return new GraphEnd(record.Unit, QuestClosing.Partial, entry.At)
            {
                Quest = entry.Why[prefix.Length..], Node = node, Attempt = newest?.Attempt, NodeState = WorkPerformer.StatusOf(record, node).State,
                Resolution = offered ? newest!.Resolution : null, Words = offered && newest!.Resolution is null ? [HeldResolutions.Stop] : [],
            };
        }
    }
}
