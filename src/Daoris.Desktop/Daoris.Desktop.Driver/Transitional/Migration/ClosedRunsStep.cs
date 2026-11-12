
namespace Daoris.Driver;

/// <summary>
/// The runs a closed quest left standing before the runtime ended them: a run whose chain's quests here are all done, declined or gone from
/// the store, and whose standing steps none can move (<see cref="ClosedRunEnds"/>), ends with its steps as they stand. It is judged as a look
/// judges it, from what the home holds before any look: the line asked at the repository's checkout, and its branch standing where a session
/// serving its quests still has its tree here or its record is bound to a branch not recorded gone. Each end is counted by why; a home with no
/// store or no runs is left as it is, and the step run again changes nothing.
/// </summary>
public sealed class ClosedRunsStep : IHomeMigrationStep
{
    public string Name => "closed-runs";

    public void Run(HomeMigrationRun run)
    {
        if (!Directory.Exists(WorkflowRunBindings.FolderOf(run.Home))) return;
        if (Read(DaorisHome.Entry(run.Home, HomeEntry.Knowledge)) is not { } read) return;
        var (quests, trees) = read;
        var chains = new QuestChains(quests);
        var roots = WorkflowsStep.Registry(run.Home).Where(repo => repo.Root is { Length: > 0 }).ToList();
        var standing = new SessionBranches(run.Home).Entries().Where(each => each.GoneAt is null && each.Unit is not null)
            .Select(each => each.Unit!).ToHashSet(StringComparer.Ordinal);
        var context = new SessionTrees(run.Home).Context;

        // The migration holds the home's lease while its steps run.
        var records = new NodeRecordFiles(run.Home, () => true);
        foreach (var bound in new GraphRuns(records).Read().Where(each => each.Ended is null && each.Line is null && !ChatRuns.IsChatRun(each.Run)))
        {
            if (bound.Version.Graph is not { } graph || records.Read(bound.Unit) is not { } record || NodeRunKeeping.Waits(graph, record)) continue;
            var repository = bound.Binding.Repository;
            var work = chains.Of(bound.Run).Where(quest => string.Equals(quest.To, repository, StringComparison.OrdinalIgnoreCase) && quest.SetUpIn is null).ToList();
            if (work.Any(quest => !ClosedRunEnds.Closed(quest.Status))) continue;

            var stands = work.Any(quest => trees[quest.Id].Any(Directory.Exists))
                         || record.Statuses.Values.Any(status => status.Binding?.Subject is { } subject && standing.Contains(subject));
            var root = roots.FirstOrDefault(repo => string.Equals(repo.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(repo.Workspace, bound.Binding.Workspace, StringComparison.OrdinalIgnoreCase))?.Root;
            var held = root is null || !Directory.Exists(root) ? new HashSet<string>()
                : ClosedRunEnds.OnLineAsync(ClosedRunEnds.Bound(graph, record), async (tip, ct) =>
                        (await LineHolding.ReadInAsync(context, root, repository, bound.Binding.Workspace, tip, ct).ConfigureAwait(false))?.Whole,
                    CancellationToken.None).GetAwaiter().GetResult();
            if (ClosedRunEnds.Why(graph, record, false, stands, held) is not { } why) continue;

            var path = NodeRecordFiles.PathOf(run.Home, bound.Unit);
            run.Keep(path);
            var quest = work.LastOrDefault()?.Id ?? bound.Run;
            var at = DateTimeOffset.UtcNow;
            var (refused, kept) = records.Edit<bool>(bound.Unit, current =>
                current.Ended is null ? (current with { Ended = RunOutcomes.Of(graph, current, quest, at) with { Why = why } }, true) : (null, false));
            if (refused is not null || kept != true) continue;
            run.Said(path);
            run.Count(Name, why);
        }
    }

    /// <summary>The store's quests in the order it filed them, and each quest's sessions' trees; null where it or its tables are not there.</summary>
    private static (IReadOnlyList<QuestView> Quests, ILookup<string, string> Trees)? Read(string path)
    {
        if (!File.Exists(path)) return null;
        using var connection = StoreEngineSource.Connect(path, DesktopOpen.ReadOnly);
        using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('quests', 'sessions')";
            if (Convert.ToInt32(probe.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) < 2) return null;
        }

        var quests = new List<QuestView>();
        using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT id, status, receiver, parent, set_up_in FROM quests ORDER BY rowid";
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                quests.Add(new QuestView(reader.GetString(0), "", reader.IsDBNull(2) ? "" : reader.GetString(2), "", "", reader.GetString(1))
                {
                    Parent = reader.IsDBNull(3) ? null : reader.GetString(3), SetUpIn = reader.IsDBNull(4) ? null : reader.GetString(4),
                });
            }
        }

        var trees = new List<(string Quest, string Tree)>();
        using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT quest, tree FROM sessions WHERE quest IS NOT NULL AND tree IS NOT NULL AND tree <> ''";
            using var reader = select.ExecuteReader();
            while (reader.Read()) trees.Add((reader.GetString(0).TrimStart('#'), reader.GetString(1)));
        }

        return (quests, trees.ToLookup(each => each.Quest, each => each.Tree, StringComparer.OrdinalIgnoreCase));
    }
}
