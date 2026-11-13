using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The runs of work abandoned before an abandon stopped its runs' steps: a run whose quest is declined, or whose ask the person closed,
/// still offered its review and its landing. Each live step of such a run not ended is stopped as the person's through the run's node door,
/// as an abandon stops it now (<see cref="AbandonRuns"/>), so the first pass applies the stop and says it on the run. A step that already
/// holds a stop is left, so the step run again changes nothing; a home with no store or no runs is left as it is.
/// </summary>
public sealed class AbandonedRunsStep : IHomeMigrationStep
{
    public const string DeclinedWords = "Its quest was declined, so nothing of it is reviewed or landed.";

    /// <summary>The words kept with the stop of a closed ask's run.</summary>
    public const string ClosedWords = "Its ask was closed, so nothing of it is reviewed or landed.";

    public string Name => "abandoned-runs";

    public void Run(HomeMigrationRun run)
    {
        if (!Directory.Exists(WorkflowRunBindings.FolderOf(run.Home))) return;
        var (declined, closed) = Closed(DaorisHome.Entry(run.Home, HomeEntry.Knowledge));
        if (declined.Count == 0 && closed.Count == 0) return;

        // The migration holds the home's lease while its steps run.
        var records = new NodeRecordFiles(run.Home, () => true);
        foreach (var bound in new GraphRuns(records).Read().Where(each => each.Ended is null && each.Line is null))
        {
            var words = declined.Contains(bound.Run) ? DeclinedWords : bound.Binding.Ask is { } ask && closed.Contains(ask) ? ClosedWords : null;
            if (words is null || bound.Version.Graph is not { } graph || records.Read(bound.Unit) is not { } record) continue;
            var open = graph.Nodes
                .Where(node => NodeStates.Live.Contains(WorkPerformer.StatusOf(record, node.Id).State)
                               && !record.Pending.Any(word => word.Node == node.Id && word.Act == NodeWordActs.Stop))
                .ToList();
            if (open.Count == 0) continue;

            var path = NodeRecordFiles.PathOf(run.Home, bound.Unit);
            run.Keep(path);
            var stopped = open.Count(node => RunNodeWords.Word(records, bound.Run, node.Id, NodeWordActs.Stop, words).Done);
            if (stopped > 0) run.Said(path);
        }
    }

    /// <summary>The declined quests and the closed asks the store holds; none where it or its table is not there.</summary>
    private static (HashSet<string> Declined, HashSet<string> Closed) Closed(string path)
    {
        var declined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var closed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return (declined, closed);
        using var connection = StoreEngineSource.Connect(path, DesktopOpen.ReadWrite);
        Each(connection, "quests", "SELECT id FROM quests WHERE status = 'Declined'", declined);
        Each(connection, "asks", "SELECT id FROM asks WHERE state = 'Closed'", closed);
        return (declined, closed);
    }

    private static void Each(DbConnection connection, string table, string sql, HashSet<string> into)
    {
        using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            probe.Bind("$name", table);
            if (Convert.ToInt32(probe.ExecuteScalar(), CultureInfo.InvariantCulture) == 0) return;
        }

        using var select = connection.CreateCommand();
        select.CommandText = sql;
        using var reader = select.ExecuteReader();
        while (reader.Read()) into.Add(reader.GetString(0));
    }
}
