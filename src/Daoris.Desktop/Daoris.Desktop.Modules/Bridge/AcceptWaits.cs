using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The node door's Accept on a run whose <c>land</c> the runtime performs after the review: kept, it waits for what the try came to as the
/// landing's press did (<see cref="PerformedWaits"/>, within <see cref="GraphPasses.Bound"/>), and is answered as that press answered
/// (<see cref="PerformedAnswers"/>): landed, why nothing landed, carried on, or <i>accepted; landing</i> where no try ended in time. A word
/// kept early, on another act, or on a run nothing lands after its review waits for nothing.
/// </summary>
public static class AcceptWaits
{
    /// <summary>Whether a <c>land</c> follows <paramref name="review"/> in <paramref name="graph"/>, so its Accept waits for the landing.</summary>
    public static bool Waits(NodeGraph graph, string review) =>
        ReviewStops.After(graph, review).Any(id => graph.Nodes.FirstOrDefault(node => node.Id == id)?.Kind == NodeKinds.Land);

    /// <summary>What the landing <paramref name="done"/> accepted came to; null where it waits for none.</summary>
    public static async Task<TreeLanding?> AwaitAsync(DriverLoop loop, NodeWorded done, CancellationToken ct)
    {
        if (done is not { Done: true, Early: false, Act: NodeWordActs.Accept }) return null;
        var records = new NodeRecordFiles(loop.Home);
        if (new GraphRuns(records).Find(done.Run) is not { Version.Graph: { } graph } run || !Waits(graph, done.Node)) return null;
        if (records.Read(run.Unit)?.Statuses.GetValueOrDefault(done.Node) is not { } status) return null;
        if (ReviewRecords.Read(loop.Home, done.Run, done.Node, status.Attempt) is not { Reference.Tip: { } tip } reviewed) return null;
        var session = reviewed.Parts.LastOrDefault(part => part.Session is not null)?.Session ?? done.Run;
        return PerformedAnswers.Of(await PerformedWaits.AwaitAsync(loop, session, run.Unit, tip, ct).ConfigureAwait(false));
    }
}
