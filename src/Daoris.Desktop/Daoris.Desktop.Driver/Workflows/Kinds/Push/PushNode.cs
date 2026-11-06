namespace Daoris.Driver;

/// <summary>
/// Daoris's own push of the branch the <c>land</c> made (G8, D241 point 2): performed through the push act, reached when git's remote-tracking ref
/// holds the pushed commit.
/// </summary>
[NodeKind(NodeKinds.Push, NodeGroup.Landing, NodeKinds.Land)]
internal sealed class PushNode : BuiltInNode, IObserves, IStarts
{
    /// <summary>Failed by the push act's refusal. Never skipped, since a skip would read as pushed where nothing was; the person's doors are stop and Try again.</summary>
    public override NodeAuthority Row { get; } = new(NodeKinds.Push, NodeReach.Performed, NodeSource.Git, NodeSource.Git, false, PersonSkips: false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    public override string? Evidence => BuiltInNodes.EvidenceSchema(NodeKinds.Push);

    public override string? Title => "The push";

    public override NodeLook Look => NodeLook.Line;

    public override bool ReadsTheLanding => true;

    public override bool EndsByTheLine => true;

    public NodeObservation Observe(NodeReading reading) => BuiltInNodes.Pushed(reading);

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
        if (kept.Doors.Door<IPushDoor>() is not { } door || kept.Landed is not { } landed || landed.Pushed && landed.PushedTip == landed.Tip) return;
        var watched = kept.Governed.Watched;
        door.Start(new PushStart(kept.Governed.Unit.Id, kept.Node, watched.Repository, watched.Workspace, watched.Root, landed.Branch, landed.Tip));
    }
}
