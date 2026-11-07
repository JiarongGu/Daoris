namespace Daoris.Driver;

/// <summary>
/// The chain's parts ended on the line once its pull request merged: each part's branch removed through the journal's delete, its
/// commits kept on a recovery ref, and what it ended written on the landing record, which the keeper and the press read. It starts only on a
/// monitor's <c>when merged</c>, so a closed request never reaches it.
/// </summary>
[NodeKind(NodeKinds.Tidy, NodeGroup.Landing, NodeKinds.Monitor)]
internal sealed class TidyNode : BuiltInNode, IObserves, IStarts
{
    /// <summary>Read from the landing record its door wrote, and nothing fails it. Never skipped, since a skip would read as tidied; the person's door is the stop.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Tidy, NodeReach.Performed, NodeSource.Landing, null, false, PersonSkips: false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    /// <summary>A way from a monitor's <c>when merged</c> reaches it: it ends a chain only once its pull request merged.</summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var monitors = graph.Nodes.Where(each => each.Kind == NodeKinds.Monitor).Select(each => each.Id).ToHashSet(StringComparer.Ordinal);
        var local = graph.Wires.Where(wire => WireKinds.Ordered.Contains(wire.Kind) && wire.From is { Unit: null } && wire.To.Unit is null).ToList();
        var merged = local.Where(wire => wire.Kind == WireKinds.When && wire.Outcome == PullRequestReading.Merged && monitors.Contains(wire.From!.Node))
            .Select(wire => wire.To.Node);
        if (!NodeGraphStarts.Reach(merged, local, forward: true).Contains(node.Id))
        {
            yield return new WireProblem(GraphRefusal.TidyMerged, null, node.Id, $"node `{node.Id}`: a `{NodeKinds.Tidy}` ends the chain's parts once its pull request "
                + $"merged, and no `{WireKinds.When} {PullRequestReading.Merged}` way from a `{NodeKinds.Monitor}` reaches it.");
        }
    }

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Tidy);

    public override string? Title => "The chain's clean-up";

    public override NodeLook Look => NodeLook.Line;

    public override bool ReadsTheLanding => true;

    public NodeObservation Observe(NodeReading reading) => TidyReading.Observe(reading);

    /// <summary>The door its chain's parts are ended through, under the loop's lease and drain rules.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => new TidyDoor(wiring.Home, said => wiring.Say("tidy.run", said), wiring.MayStart);

    /// <summary>Begun in the pass and read from the landing record its door wrote; its door takes it after the rounds.</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>
    /// A run's <c>tidy</c>, active at the tip the landing recorded, started once on the door: the monitor before it read the pull request
    /// merged, and the door ends the chain's parts. Idempotent by node and tip, and a landing the tidy already ended at that tip is not started again.
    /// </summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<ITidyDoor>(Row.Kind) is not { } door || kept.Landed is not { } landed || landed.Tidied?.Tip == landed.Tip) return;
        var watched = kept.Governed.Watched;
        door.Start(new TidyStart(kept.Governed.Unit.Id, kept.Node, watched.Repository, watched.Workspace, watched.Root, landed.Branch, landed.Tip));
    }
}
