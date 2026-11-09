namespace Daoris.Driver;

/// <summary>
/// A <c>land</c> the runtime starts: the run's record, the node, the branch unit the pass read it
/// bound to, the sessions that executed it and the tip the node is active at. Everything the landing needs beyond it is read by the door.
/// </summary>
/// <param name="Unit">The run's own unit, <c>run:&lt;run&gt;</c>, whose record holds the node.</param>
/// <param name="Run">The run, as its binding names it.</param>
/// <param name="Node">The <c>land</c> node's id.</param>
/// <param name="Branch">The branch unit the node is bound to.</param>
/// <param name="Executors">The sessions that executed the branch unit, oldest first.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Workspace">The workspace the run is in.</param>
/// <param name="Tip">The commit the node is active at, which the landing is pinned to.</param>
/// <param name="Graph">The digest of the version the run is bound to.</param>
/// <param name="Review">The graph's <c>review</c> node, whose Accept authorizes the landing; null for a graph with none, whose authority is the standing one.</param>
public sealed record LandStart(
    string Unit, string Run, string Node, string Branch, IReadOnlyList<string> Executors, string Repository, string Workspace, string Tip, string Graph,
    string? Review) : NodeDoorStart
{
    /// <summary>One landing of a node at a tip at a time; the next pass finds it ended and judges again, which lands nothing if nothing moved.</summary>
    public override string Key => $"{Unit}\n{Node}\n{Tip}";

    /// <summary>When the node started at this tip (<see cref="NodeStatus.Since"/>); null where it started unclocked.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>Whether the landing is the person's Accept's (a review before it) rather than the standing authority's.</summary>
    public bool Reviewed => Review is not null;
}
