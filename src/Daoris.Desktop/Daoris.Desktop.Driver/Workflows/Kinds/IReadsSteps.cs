namespace Daoris.Driver;

/// <summary>
/// A kind whose nodes the pass hands what they read beside the unit's records: the tips of their targets and what their door wrote. The
/// runtime asks each such kind of the graph once a unit and a pass (<see cref="NodeStepReads"/>), and names none.
/// </summary>
internal interface IReadsSteps
{
    /// <summary><paramref name="reads"/> with what this kind's nodes of <paramref name="governed"/> read in <paramref name="home"/>.</summary>
    Task<NodeGitReads> ReadAsync(GovernedUnit governed, string home, NodeGitReads reads, CancellationToken ct);

    /// <summary>What its door wrote of <paramref name="run"/> let go of, at the run's settled end beside its step refs.</summary>
    void Release(string home, string run);
}

/// <summary>The pass's reads for the kinds of a unit's graph that read their own (<see cref="IReadsSteps"/>); as they stand where none does.</summary>
internal static class NodeStepReads
{
    /// <summary>What every such kind's door wrote of <paramref name="run"/> let go of, once its step refs are.</summary>
    public static void Release(string home, string run)
    {
        foreach (var kind in NodeKit.Built.Units.OfType<IReadsSteps>()) kind.Release(home, run);
    }

    public static async Task<NodeGitReads?> ReadAsync(GovernedUnit governed, string home, NodeGitReads? reads, CancellationToken ct)
    {
        foreach (var kind in governed.Graph.Nodes.Select(node => NodeKit.Of(node.Kind)).OfType<IReadsSteps>().Distinct())
        {
            reads = await kind.ReadAsync(governed, home, reads ?? new NodeGitReads(), ct).ConfigureAwait(false);
        }

        return reads;
    }
}
