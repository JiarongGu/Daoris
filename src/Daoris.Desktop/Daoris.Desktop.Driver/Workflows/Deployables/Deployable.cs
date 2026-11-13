namespace Daoris.Driver;

/// <summary>A resource a deployable provides or consumes, and how: <c>owns</c> or <c>migrates</c> provided; <c>mirror</c>, <c>calls</c> or <c>reads</c> consumed.</summary>
public sealed record DeployableUse(string Resource, string Mode);

/// <summary>
/// One thing a repository releases, as its own <c>daoris.json</c> declares it under <c>deployables</c>: an id unique in the repository, the
/// repository-relative paths that build it, and what it provides and consumes by resource. A repository may hold several on one build.
/// </summary>
public sealed record Deployable(string Id, IReadOnlyList<string> Paths, IReadOnlyList<DeployableUse> Provides, IReadOnlyList<DeployableUse> Consumes);

/// <summary>A repository's deployables as read from its manifest: each entry that reads, and why each other one does not.</summary>
/// <param name="Items">The entries that read, in the declared order.</param>
/// <param name="Problems">One sentence per entry left out, or for a manifest or a list that does not read.</param>
public sealed record DeclaredDeployables(IReadOnlyList<Deployable> Items, IReadOnlyList<string> Problems)
{
    /// <summary>The manifest's blob id where it was read from a line; what a plan's deployables are read at.</summary>
    public string? Blob { get; init; }
}

/// <summary>How a declared deployable's release target stands: named by one release node per workflow, by none, or by several in one.</summary>
public enum DeployableStanding
{
    /// <summary>One release node in each workflow that names it.</summary>
    Named,

    /// <summary>No release node names it: its target is undeclared.</summary>
    Undeclared,

    /// <summary>Two or more nodes of one workflow name it, so which releases it is not said.</summary>
    Several,
}

/// <summary>A release node naming a deployable: its workflow and node, and the earliest act that deploys into its target.</summary>
/// <param name="DeploysOn">One of <see cref="DeployableTargets.Acts"/>: as declared, else <c>push</c>.</param>
/// <param name="DeploysOnDeclared">Whether the node's values declare it, rather than its being read as <c>push</c>.</param>
public sealed record DeployableNode(string Workflow, string Node, string DeploysOn, bool DeploysOnDeclared);

/// <summary>A declared deployable with the release nodes that name it, and how its target stands.</summary>
public sealed record DeployableTarget(Deployable Deployable, IReadOnlyList<DeployableNode> Nodes, DeployableStanding Standing);

/// <summary>Each declared deployable's target, and what in the values does not fit the declaration.</summary>
public sealed record DeployableTargetsRead(IReadOnlyList<DeployableTarget> Targets, IReadOnlyList<string> Problems);
