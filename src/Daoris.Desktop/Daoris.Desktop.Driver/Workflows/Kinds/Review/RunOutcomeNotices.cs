namespace Daoris.Driver;

/// <summary>
/// <i>Done and failed</i>'s run outcomes: each watched step after a run's review (a landing, a push, a pull request, a release, a return, an
/// apply) once it is reached or failed, one notice by run, node and attempt (<c>outcome:&lt;run&gt;/&lt;node&gt;/&lt;attempt&gt;</c>), read from
/// the run's own record at every look, so one a lost write left is claimed the next time; the news claims each key once, so a dismissed one
/// stays dismissed. Its fields: <see cref="Notice.Quest"/> the run, <see cref="Notice.Act"/> the node, <see cref="Notice.Route"/> its kind,
/// <see cref="Notice.State"/> its state, <see cref="Notice.Code"/> its outcome or failure, <see cref="Notice.Count"/> the attempt.</summary>
public static class RunOutcomeNotices
{
    /// <summary>The notice's kind.</summary>
    public const string Kind = "outcome";

    /// <summary>The news teller whose occurrences these are.</summary>
    public const string Teller = "run-outcomes";

    public static RunOutcomesRead Read(string home, DateTimeOffset now)
    {
        var failing = new List<NewsOccurrence>();
        var records = new NodeRecordFiles(home, () => false);
        foreach (var run in new GraphRuns(records).Read().Where(run => run.Line is null))
        {
            if (run.Version.Graph is not { } graph || records.Read(run.Unit) is not { } record) continue;
            foreach (var step in RunReviews.Follows(graph, record).Where(step => step.State == NodeStates.Failed))
            {
                var at = step.Since ?? now;
                var id = $"outcome:{run.Run}/{step.Node}/{step.Attempt}";
                failing.Add(new NewsOccurrence(id, at, new Notice(id, Kind, at, Workspace: run.Binding.Workspace, Quest: run.Run,
                    Repository: run.Binding.Repository, State: step.State, Route: step.Kind, Act: step.Node, Code: step.Outcome ?? step.Code, Count: step.Attempt)));
            }
        }

        return new(failing, now);
    }
}

public sealed class RunOutcomesRead(IReadOnlyList<NewsOccurrence> failing, DateTimeOffset now)
{
    public IReadOnlyList<NewsOccurrence> Occurrences { get; } = [.. failing.Where(each => now - each.At < News.Horizon)];

    public IReadOnlyList<string> Moved(IEnumerable<Notice> held)
    {
        var standing = failing.Select(each => each.Key).ToHashSet(StringComparer.Ordinal);
        return [.. held.Where(notice => notice.Kind == RunOutcomeNotices.Kind && !standing.Contains(notice.Id)).Select(notice => notice.Id)];
    }
}
