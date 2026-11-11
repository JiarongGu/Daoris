namespace Daoris.Driver;

/// <summary>
/// <i>Done and failed</i>'s run outcomes: each watched step after a run's review (a landing, a push, a pull request, a release, a return, an
/// apply) once it is reached or failed, one notice by run, node and attempt (<c>outcome:&lt;run&gt;/&lt;node&gt;/&lt;attempt&gt;</c>), read from
/// the run's own record at every look, so one a lost write left is claimed the next time; the news claims each key once, so a dismissed one
/// stays dismissed. Its fields: <see cref="Notice.Quest"/> the run, <see cref="Notice.Act"/> the node, <see cref="Notice.Route"/> the node's
/// kind, <see cref="Notice.State"/> its state, <see cref="Notice.Code"/> its outcome or failure, <see cref="Notice.Count"/> the attempt.
/// </summary>
public static class RunOutcomeNotices
{
    /// <summary>The notice's kind.</summary>
    public const string Kind = "outcome";

    /// <summary>The news teller whose occurrences these are.</summary>
    public const string Teller = "run-outcomes";

    /// <summary>The occurrences the bound runs' settled steps after their reviews make, those settled within the news' horizon.</summary>
    public static IReadOnlyList<NewsOccurrence> Occurrences(string home, DateTimeOffset now)
    {
        var occurrences = new List<NewsOccurrence>();
        foreach (var run in new GraphRuns(new NodeRecordFiles(home, () => false)).Read().Where(run => run.Line is null))
        {
            if (RunReviews.Read(home, run.Run, null) is not { } review) continue;
            foreach (var step in review.Follows.Where(step => step.State is NodeStates.Reached or NodeStates.Failed))
            {
                var at = step.Since ?? now;
                if (now - at >= News.Horizon) continue;
                var id = $"outcome:{run.Run}/{step.Node}/{step.Attempt}";
                occurrences.Add(new NewsOccurrence(id, at, new Notice(id, Kind, at, Workspace: run.Binding.Workspace, Quest: run.Run,
                    Repository: run.Binding.Repository, State: step.State, Route: step.Kind, Act: step.Node, Code: step.Outcome ?? step.Code, Count: step.Attempt)));
            }
        }

        return occurrences;
    }
}
