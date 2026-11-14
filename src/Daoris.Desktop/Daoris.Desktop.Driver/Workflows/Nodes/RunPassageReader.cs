namespace Daoris.Driver;

/// <summary>One step of a run's bound graph version as its session's row draws it: passed, current, to come, or failed.</summary>
/// <param name="Mark">One of <see cref="RunPassageReader"/>' marks.</param>
public sealed record PassageStep(string Node, string Kind, string Mark);

/// <summary>A run's passage through its bound workflow version (D293 point 6): its steps in the graph's order, each with its mark.</summary>
public sealed record RunPassage(string Run, string Workflow, int? Version, IReadOnlyList<PassageStep> Steps);

/// <summary>
/// Where each run bound here stands in its workflow, by the graph's authored steps and the node record read now. **Read, never stored**,
/// as <see cref="PersonWaitReader"/> reads the person's waits.
/// </summary>
public static class RunPassageReader
{
    public const string Passed = "passed";

    public const string Current = "current";

    public const string Next = "next";

    public const string Failed = "failed";

    /// <summary>Every run not ended with a readable graph and a node record, by run.</summary>
    public static IReadOnlyList<RunPassage> Of(IEnumerable<GraphRun> runs, Func<string, NodeRecord?> record)
    {
        var passages = new List<RunPassage>();
        foreach (var run in runs.Where(run => run.Line is null && run.Ended is null).OrderBy(run => run.Run, StringComparer.Ordinal))
        {
            if (run.Version.Graph is not { } graph || record(run.Unit) is not { } kept) continue;
            List<PassageStep> steps = [.. graph.Nodes.Where(node => !node.Synthetic)
                .Select(node => new PassageStep(node.Id, node.Kind, MarkOf(WorkPerformer.StatusOf(kept, node.Id).State)))];
            passages.Add(new RunPassage(run.Run, run.Binding.Workflow, run.Binding.Version, steps));
        }

        return passages;
    }

    /// <summary>The runs bound in <paramref name="home"/>, read from its files now.</summary>
    public static IReadOnlyList<RunPassage> Read(string home)
    {
        var files = new NodeRecordFiles(home);
        return Of(new GraphRuns(files).Read(), files.Read);
    }

    /// <summary>A step reached or skipped has passed; one ready, active or held stands now; one failed or rejected failed; else it is to come.</summary>
    internal static string MarkOf(string state) => state switch
    {
        NodeStates.Reached or NodeStates.Skipped => Passed,
        NodeStates.Ready or NodeStates.Active or NodeStates.Unknown => Current,
        NodeStates.Failed or NodeStates.Rejected => Failed,
        _ => Next,
    };
}
