namespace Daoris.Driver;

/// <summary>
/// <i>Done and failed</i>'s steps passed on the person's through words (<see cref="ThroughAccepts"/>): each unchecked done passed and each
/// review accepted on them, one notice by run, node and attempt (<c>through:&lt;run&gt;/&lt;node&gt;/&lt;attempt&gt;</c>), read from the run's own
/// record at every look, so the person hears of every step that moved on their words though they pressed nothing. Its fields:
/// <see cref="Notice.Quest"/> the run, <see cref="Notice.Act"/> the node, <see cref="Notice.Route"/> the node's kind, <see cref="Notice.State"/>
/// its state, <see cref="Notice.Code"/> the ask the words are kept on, <see cref="Notice.Count"/> the attempt.
/// </summary>
public static class ThroughNotices
{
    /// <summary>The notice's kind.</summary>
    public const string Kind = "through";

    /// <summary>The news teller whose occurrences these are.</summary>
    public const string Teller = "through-words";

    /// <summary>The occurrences the bound runs' steps passed on words make, those passed within the news' horizon.</summary>
    public static IReadOnlyList<NewsOccurrence> Occurrences(string home, DateTimeOffset now)
    {
        var occurrences = new List<NewsOccurrence>();
        var records = new NodeRecordFiles(home, () => false);
        foreach (var run in new GraphRuns(records).Read().Where(run => run.Line is null && run.Version.Graph is not null))
        {
            if (records.Read(run.Unit) is not { } record) continue;
            foreach (var node in run.Version.Graph!.Nodes)
            {
                if (record.Statuses.GetValueOrDefault(node.Id) is not { } status || ThroughAccepts.On(status) is not { } through) continue;
                var at = status.Since ?? now;
                if (now - at >= News.Horizon) continue;
                var id = $"{Kind}:{run.Run}/{node.Id}/{status.Attempt}";
                occurrences.Add(new NewsOccurrence(id, at, new Notice(id, Kind, at, Workspace: run.Binding.Workspace, Quest: run.Run,
                    Repository: run.Binding.Repository, State: status.State, Route: node.Kind, Act: node.Id, Code: through.Ask, Count: status.Attempt)));
            }
        }

        return occurrences;
    }
}
