namespace Daoris.Driver;

/// <summary>
/// The end of a run's worktree, performed through its door once the work has landed: each tree the run owns is ended behind the clean-up's proof
/// (<see cref="TreeReleases"/>) and the node reached with what went and what stayed and why. It never fails: a tree with uncommitted work or
/// commits only it holds stays, said, and the sweep ends it once its work lands. In a graph that sets nothing apart it finds no tree and says so.
/// </summary>
[NodeKind(NodeKinds.Release, NodeGroup.Git, NodeKinds.Line)]
internal sealed class ReleaseNode : BuiltInNode, IStarts
{
    /// <summary>Reached by Daoris's release alone; never the person's skip, which would read as ended.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Release, NodeReach.Performed, NodeSource.Release, null, false, PersonSkips: false, MayEnd: true, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override string NeverEndBecause => "it is reached once the trees are ended.";

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Release);

    public override string? Title => "End the worktree";

    public override NodeLook Look => NodeLook.Line;

    public override IReadOnlyList<string> Places { get; } = [PlaceKind.Git];

    /// <summary>It comes after a <c>land</c>: the work is what its trees are ended for.</summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var lands = graph.Nodes.Where(each => each.Kind == NodeKinds.Land).Select(each => each.Id).ToList();
        var local = graph.Wires.Where(wire => WireKinds.Ordered.Contains(wire.Kind) && wire.From is { Unit: null } && wire.To.Unit is null).ToList();
        if (lands.Count > 0 && NodeGraphStarts.Reach(lands, local, forward: true).Contains(node.Id)) yield break;
        yield return new WireProblem(GraphRefusal.ReleaseLanding, null, node.Id,
            $"node `{node.Id}`: a `{NodeKinds.Release}` ends the trees once the work landed, and no way from a `{NodeKinds.Land}` reaches it.");
    }

    public INodeDoor? Door(NodeDoorWiring wiring) => new ReleaseDoor(wiring.Home, said => wiring.Say("release.run", said), wiring.MayStart);

    /// <summary>Begun in the pass; its door ends the trees after the rounds.</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>A run's active <c>release</c>, handed to its door with the branches its run owns.</summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<IReleaseDoor>(Row.Kind) is not { } door || kept.Scope.Root.Length == 0) return;
        var branches = kept.Scope.Branches.Select(branch => branch.Records.Branch).OfType<GrownBranch>()
            .Where(entry => entry.GoneAt is null).Select(entry => entry.Branch).Distinct(StringComparer.Ordinal).ToList();
        door.Start(new ReleaseStart(kept.Governed.Unit, kept.Node, kept.Status.Attempt, kept.Scope.Run.Binding.Repository, kept.Scope.Root, branches)
        {
            Authority = kept.Authority,
        });
    }
}
