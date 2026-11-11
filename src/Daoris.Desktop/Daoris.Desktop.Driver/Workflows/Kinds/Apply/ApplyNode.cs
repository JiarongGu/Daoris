namespace Daoris.Driver;

/// <summary>
/// A system place's apply (design §5.3): performed through its door, which hands the change set its review record names (<see cref="EffectRecord"/>)
/// to the system's adapter, applied under each operation's precondition after the review accepted it. Reached when every operation applied,
/// when its plan changed nothing and when its work kept none to apply, said why;
/// failed <c>stale</c> with each stale resource's present version, which a way back to the work tells it, so it plans again; failed
/// <c>apply-refused</c> in the adapter's words otherwise.
/// </summary>
[NodeKind(NodeKinds.Apply, NodeGroup.Others)]
internal sealed class ApplyNode : BuiltInNode, IStarts, IWritesWork
{
    /// <summary>Reached and failed by the adapter's answer; never the person's skip, which would read as applied.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Apply, NodeReach.Performed, NodeSource.Plugin, NodeSource.Plugin, false, PersonSkips: false, MayEnd: true, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override string NeverEndBecause => "it is reached on the adapter's answer.";

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Apply);

    public override string? Title => "The apply";

    public override NodeLook Look => NodeLook.Line;

    public override IReadOnlyList<string> Places { get; } = [PlaceKind.System];

    public INodeDoor? Door(NodeDoorWiring wiring) => new ApplyDoor(wiring.Home, SystemAdapters.Starter(wiring.Home), said => wiring.Say("apply.run", said), wiring.MayStart);

    /// <summary>Begun in the pass; its door applies it after the rounds.</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>A run's active <c>apply</c>, handed to its door once per node and attempt with the record whose plan it applies.</summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<IApplyDoor>(Row.Kind) is not { } door) return;
        door.Start(new ApplyStart(kept.Governed.Unit, kept.Node, kept.Status.Attempt) { Authority = kept.Authority, Record = EffectRecord.Of(kept) });
    }
}
