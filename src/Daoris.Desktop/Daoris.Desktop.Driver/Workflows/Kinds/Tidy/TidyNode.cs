namespace Daoris.Driver;

/// <summary>
/// The chain's parts ended on the line once its pull request merged (D245 point 1): each part's branch removed through the journal's delete, its
/// commits kept on a recovery ref, and what it ended written on the landing record, which the keeper and the press read.
/// </summary>
[NodeKind(NodeKinds.Tidy, NodeGroup.Landing, NodeKinds.Monitor)]
internal sealed class TidyNode : BuiltInNode, IObserves, IStarts
{
    /// <summary>Read from the landing record its door wrote. Never skipped, since a skip would read as tidied; the person's door is the stop.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Tidy, NodeReach.Performed, NodeSource.Landing, NodeSource.Landing, false, PersonSkips: false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    public override string? Evidence => BuiltInNodes.EvidenceSchema(NodeKinds.Tidy);

    public override string? Title => "The chain's clean-up";

    public override NodeLook Look => NodeLook.Line;

    public override bool ReadsTheLanding => true;

    public NodeObservation Observe(NodeReading reading) => BuiltInNodes.Completion(reading, CompletionNodes.Tidy);

    /// <summary>The door its chain's parts are ended through, under the loop's lease and drain rules.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => new TidyDoor(wiring.Home, said => wiring.Say("tidy.run", said), wiring.MayStart);

    /// <summary>Begun in the pass and read from the landing record its door wrote; its door takes it after the rounds.</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>
    /// A run's <c>tidy</c>, active at the tip the landing recorded, started once on the door (D245): its <c>monitor</c> before it read the pull
    /// request merged or closed, and the door ends the chain's parts or fails the node. Idempotent by node and tip, and a landing the tidy
    /// already ended at that tip is not started again.
    /// </summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<ITidyDoor>() is not { } door || kept.Landed is not { } landed || landed.Tidied?.Tip == landed.Tip) return;
        var watched = kept.Governed.Watched;
        door.Start(new TidyStart(kept.Governed.Unit.Id, kept.Node, watched.Repository, watched.Workspace, watched.Root, landed.Branch, landed.Tip));
    }
}
