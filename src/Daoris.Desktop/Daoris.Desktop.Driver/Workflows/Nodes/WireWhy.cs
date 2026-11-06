using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>Why the evaluation moved a node (<see cref="WireMove.Why"/>).</summary>
public static class WireWhy
{
    /// <summary>An observed built-in's records said an outcome, through <see cref="NodeObserving.Observe(WorkUnit, NodeDeclaration, NodeAcceptance, NodeGitReads?)"/>.</summary>
    public const string Observed = "observed";

    /// <summary>Its binding's revision, graph or version is no longer current (<see cref="NodeAcceptances.Lapsed"/>).</summary>
    public const string Lapsed = "lapsed";

    /// <summary>A receipt or an input its binding rests on is no longer current: an upstream invalidated, a witness lost.</summary>
    public const string Upstream = "upstream";

    /// <summary>A skip of another graph or run: it holds for its own alone.</summary>
    public const string SkipScope = "skip-scope";

    /// <summary>
    /// A failure or a rejection of another run (FABRIC-WIRE1f): an attempt's outcome other than a reach is its own run's alone, so a new run
    /// lapsed it, as a skip of another run lapses, and the node was judged again in this run. No longer given since FABRIC-RUN1e, which keeps
    /// such a failure for the person; kept so a history entry written with it still reads.
    /// </summary>
    public const string RunScope = "run-scope";

    /// <summary>An <c>all</c> join's members settled: every one reached or skipped, or one failed.</summary>
    public const string Members = "members";

    /// <summary>A <c>stop</c> sink reached on the receipt of the path into it: a failure or a rejection its binding rests on (FABRIC-WIRE1g).</summary>
    public const string Path = "path";

    /// <summary>A <c>back</c> wire was taken into it (DECIDE1c): judged again for a new attempt.</summary>
    public const string Back = "back";

    /// <summary>Its wires and inputs are satisfied.</summary>
    public const string Eligible = "eligible";

    /// <summary>Its wires or inputs are no longer satisfied.</summary>
    public const string Ineligible = "ineligible";
}
