namespace Daoris.Driver;

/// <summary>
/// How a graph run came out, kept once with its end (<see cref="RunEnd.Outcome"/>), so a run's state is read from its end rather than from
/// attempts a later reopen moves.
/// </summary>
public static class RunOutcomes
{
    /// <summary>Its end node reached, or no step of it failed or was stopped.</summary>
    public const string Completed = "completed";

    /// <summary>A step stopped or turned down, by the person or a supervisor, and nothing went on from it.</summary>
    public const string Stopped = "stopped";

    /// <summary>A step failed and nothing went on from it.</summary>
    public const string Failed = "failed";

    /// <summary>
    /// The end of a run over at its newest quest <paramref name="quest"/>: completed where its end node is reached; else failed at its first
    /// failed step in the graph's order; else stopped at its first stopped or rejected one; else completed, its routes taken leaving nothing.
    /// </summary>
    internal static RunEnd Of(NodeGraph graph, NodeRecord record, string quest, DateTimeOffset at)
    {
        var ended = new RunEnd(quest, at) { Outcome = Completed };
        if (WorkPerformer.StatusOf(record, graph.End).State == NodeStates.Reached) return ended;
        var statuses = graph.Nodes.Select(node => WorkPerformer.StatusOf(record, node.Id)).ToList();
        if (statuses.FirstOrDefault(status => status.State == NodeStates.Failed) is { } failed) return ended with { Outcome = Failed, Node = failed.Node };
        return statuses.FirstOrDefault(status => status.State is NodeStates.Stopped or NodeStates.Rejected) is { } stopped
            ? ended with { Outcome = Stopped, Node = stopped.Node, By = stopped.By }
            : ended;
    }
}
