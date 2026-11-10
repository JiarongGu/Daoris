namespace Daoris.Driver;

/// <summary>
/// Daoris's own push of the branch the <c>land</c> made: performed through the push act, reached when git's remote-tracking ref
/// holds the pushed commit.
/// </summary>
[NodeKind(NodeKinds.Push, NodeGroup.Git, NodeKinds.Land)]
internal sealed class PushNode : BuiltInNode, IObserves, IStarts, IAsksReads
{
    public PassRead Asks => PassRead.Remote;

    /// <summary>
    /// Failed by the push act's refusal. The person never skips it, since a skip would read as pushed where nothing pushed; the person's doors
    /// are stop and Try again. The landing record passes it over where the landing made no branch (<see cref="NothingToDeliver"/>).
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Push, NodeReach.Performed, NodeSource.Git, NodeSource.Git, false, PersonSkips: false, true, true) { SkipBy = NodeSource.Landing };

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    /// <summary>It pushes the branch a <c>land</c> of the <c>branch</c> method made, so a way from each landing of a method reaches it.</summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var ordered = graph.Wires.Where(wire => WireKinds.Ordered.Contains(wire.Kind) && wire.From is { Unit: null } && wire.To.Unit is null).ToList();
        foreach (var land in graph.Nodes.Where(each => each.Kind == NodeKinds.Land))
        {
            if (NodeOptions.Text(land, "method") is not { } method || !LandNodes.Methods.Contains(method, StringComparer.Ordinal)) continue;
            if (!NodeGraphStarts.Reach([land.Id], ordered, forward: true).Contains(node.Id))
            {
                yield return new WireProblem(GraphRefusal.LandTail, null, node.Id,
                    $"node `{node.Id}`: a `{NodeKinds.Push}` pushes the branch `{land.Id}` made, and no way from `{land.Id}` reaches it.");
            }

            if (method != LandNodes.Branch)
            {
                yield return new WireProblem(GraphRefusal.LandTail, null, node.Id, $"node `{node.Id}` is a `{NodeKinds.Push}`, which pushes the branch of a `land` "
                    + $"whose method is `{LandNodes.Branch}`: choose that method on the landing.");
            }
        }
    }

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Push);

    public override string? Title => "The push";

    public override NodeLook Look => NodeLook.Line;

    public override bool ReadsTheLanding => true;

    public override bool EndsByTheLine => true;

    public override NodeArrivals Arrives { get; } = NodeArrivals.Taking(("ref", WireValues.Ref));

    public NodeObservation Observe(NodeReading reading) => PushReading.Observe(reading);

    /// <summary>The push act of the act journal, under the loop's lease and drain rules.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => new PushDoor(wiring.Home, said => wiring.Say("push.run", said), wiring.MayStart);

    /// <summary>Begun in the pass and read from the landing record; its door takes it after the rounds.</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>
    /// A run's <c>push</c>, active at the tip the landing recorded, started once on the door: the branch the <c>land</c> made is pushed through
    /// the push act. The door is idempotent by node and tip, and the act is the person's press's own, so the two cannot push twice.
    /// </summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<IPushDoor>(Row.Kind) is not { } door || kept.Landed is not { } landed || landed.Pushed && landed.PushedTip == landed.Tip) return;
        var watched = kept.Governed.Watched;
        door.Start(new PushStart(kept.Governed.Unit.Id, kept.Node, watched.Repository, watched.Workspace, watched.Root, landed.Branch, landed.Tip) { Authority = kept.Authority });
    }
}
