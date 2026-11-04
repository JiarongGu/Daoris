namespace Daoris.Driver;

/// <summary>
/// A unit a graph naming a plugin's performed node governs: the graph, its digest, the run (<see cref="NodeRuns.Of(string)"/>), each node's
/// declaration as the runtime evaluates and starts it, and the unit as the awaited set names it.
/// </summary>
/// <param name="Declared">Each node by id: a plugin's performed node with its plugin's step's <c>perform</c>, the rest as the graph declares.</param>
/// <param name="Dispatchable">The performed nodes whose plugin declares their step at the graph's version and reach: only these are sent.</param>
public sealed record GovernedUnit(
    WorkUnit Unit, NodeGraph Graph, string GraphDigest, string Run, IReadOnlyDictionary<string, NodeDeclaration> Declared,
    IReadOnlySet<string> Dispatchable, WatchedUnit Watched)
{
    /// <summary>
    /// Whether its graph's <c>quest.close</c> closes it: a quest bound at its take to this very graph. An unbound quest, or one bound to
    /// another graph, is never closed by one, and keeps today's done.
    /// </summary>
    public bool Closes { get; init; }

    /// <summary>
    /// The plugins' observed nodes whose plugin declares their step at the graph's version and reach: only these are begun, awaited and
    /// reached by a believed event.
    /// </summary>
    public IReadOnlySet<string> Observable { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Each node's place in its graph (<see cref="NodeGraphs.Places"/>): what its scope, receipts and binding name.</summary>
    public IReadOnlyDictionary<string, string> Places { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Where it is a run bound to a graph version, what its nodes observe this look; null for a unit Current's graphs or a closure graph
    /// govern. Its <see cref="Unit"/> is then the run's own, <c>run:&lt;run&gt;</c>, and <see cref="Run"/> its binding's run.
    /// </summary>
    public RunScope? Scope { get; init; }
}
