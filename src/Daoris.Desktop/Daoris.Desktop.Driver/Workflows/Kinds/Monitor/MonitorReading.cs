namespace Daoris.Driver;

/// <summary>
/// A <c>monitor</c>'s reading (D245's note; D247 §4 point 1): the state the one step it follows reports through that step's own reading
/// (<see cref="IWatchable"/>), read into its outcome. Reached once the state settles, its outcome the state; pending while the state is open or
/// unanswered, or where it follows no one step that reports a state, which the save refuses. Nothing fails it.
/// </summary>
internal static class MonitorReading
{
    public static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.NotBound(reading) is { } refused) return refused;
        if (Watched(reading.Follows) is not { } watched) return NodeObserving.Seen(reading, null, "it follows no one step that reports a state");

        var read = watched.Read(reading);
        if (read.Refused is { } why) return NodeObserving.Seen(reading, null, read.Said) with { Refused = why };
        if (read.Unknown) return NodeObserving.Seen(reading, NodeStates.Unknown, read.Said);
        if (read.State is not { } state) return NodeObserving.Seen(reading, null, read.Said);

        var reached = NodeObserving.Reached(reading, NodeSource.Watched, read.Revision, read.Record, read.Artifact, read.Said);
        return reached with { Acceptance = reached.Acceptance! with { Outcome = state } };
    }

    /// <summary>The step a monitor watches: the one node wired straight into it, where its kind reports a state; else null.</summary>
    internal static IWatchable? Watched(IReadOnlyList<NodeDeclaration> follows) =>
        follows.Count == 1 && NodeKit.Of(follows[0].Kind) is IWatchable watched ? watched : null;
}
