namespace Daoris.Driver;

/// <summary>
/// The runs the person stopped, by the quests their nodes are bound on: a run's record holding a node the person stopped (its review
/// among them) ends what it asked of them, so *What needs you* keeps no *Ready for you* row for a session of that quest. A stop ends the
/// run, which the runtime then governs no more, so this reads the records themselves. Read-only.
/// </summary>
public static class StoppedRuns
{
    private static readonly string QuestPrefix = QuestUnits.IdOf("");

    /// <summary>The quests of every run the person stopped in <paramref name="home"/>; none where no record is kept.</summary>
    public static IReadOnlySet<string> Quests(string home) =>
        Directory.Exists(NodeRecordFiles.FolderOf(home)) ? Of(new NodeRecordFiles(home, () => false).All().Records) : new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The quests the records the person stopped a node of are bound on.</summary>
    public static IReadOnlySet<string> Of(IEnumerable<NodeRecord> records) =>
        records.Where(record => record.Statuses.Values.Any(status => status is { State: NodeStates.Stopped, By: NodeCause.Person }))
            .SelectMany(record => record.Statuses.Values.SelectMany(status => new[] { status.Binding?.Observes, status.Acceptance?.Observes }))
            .OfType<string>()
            .Where(unit => unit.StartsWith(QuestPrefix, StringComparison.Ordinal))
            .Select(unit => unit[QuestPrefix.Length..])
            .ToHashSet(StringComparer.Ordinal);
}
