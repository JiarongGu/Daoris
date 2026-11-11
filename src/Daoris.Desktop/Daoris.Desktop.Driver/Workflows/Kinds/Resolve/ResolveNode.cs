namespace Daoris.Driver;

/// <summary>
/// A problem a step could not settle, handed to whoever the workflow names (its <c>resolver</c>) with its files and hunks named, bounded per
/// stop, and returned on <c>resolved</c> by a <c>back</c> way to the rebase it came from. Its door performs the hand-off off the pass
/// (<see cref="ResolveDoor"/>). Arriving: declared before the palette offers it, so no saved graph holds one until the composition does.
/// </summary>
[NodeKind(NodeKinds.Resolve, NodeGroup.Git, NodeKinds.Rebase, Arriving = true)]
internal sealed class ResolveNode : BuiltInNode, IObserves, IStarts, IReadsSteps
{
    /// <summary>Asks per stop where its <c>retry.maxAttempts</c> says none, and each ask's time where its <c>timeoutSeconds</c> says none.</summary>
    public const int AsksAtMost = 3;

    public static readonly TimeSpan EachAsk = TimeSpan.FromSeconds(1800);

    public const string Problem = "problem";

    public const string Work = "work";

    public const string Resolver = "resolver";

    public const string Resolved = "resolved";

    /// <summary>The work's newest executor, its conversation going on; a new session in a tree of Daoris's own; a task for the person.</summary>
    public static readonly IReadOnlyList<string> Resolvers = ["session", "fresh", "person"];

    /// <summary>Reached by its proof that only the named files changed, and failed where a stop's bound is spent or its resolver stood down.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Resolve, NodeReach.Performed, NodeSource.Git, NodeSource.Git, false, PersonSkips: false, MayEnd: false, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Input;

    public override IReadOnlyList<NodeOptionField> Options { get; } = [NodeOptionFields.Choice(Resolver, Resolvers)];

    public override IReadOnlyList<string> Outcomes { get; } = [Resolved];

    public override bool TakesOutcomes(IReadOnlyList<string> outcomes) => outcomes.All(outcome => outcome == Resolved);

    public override string NeverEndBecause => "its resolution finishes the rebase its way back returns to.";

    public override IReadOnlyDictionary<string, string> Inputs { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) { [Problem] = WireValues.Problem, [Work] = WireValues.Work };

    /// <summary>Its resolver at work on a stop, and past the stop's bound the person.</summary>
    public override IReadOnlyList<string> Waits { get; } = ["session", "fresh", "person", "bound"];

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Resolve);

    public override string? Title => "The resolution";

    public override NodeLook Look => NodeLook.Line;

    public override IReadOnlyList<string> Places => PlaceKind.GitAlone;

    public NodeObservation Observe(NodeReading reading) => ResolveReading.Observe(reading);

    /// <summary>Its door, which hands the problem over off the pass; none where the loop's wiring holds no resolver.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) =>
        wiring.Resolvers is { } resolvers && wiring.Reserve is { } reserve
            ? new ResolveDoor(wiring.Home, resolvers, reserve, said => wiring.Say("resolve.run", said), wiring.MayStart)
            : null;

    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>A run's active <c>resolve</c> handed to its door once per node and attempt, with the rebase attempt whose problem it is fed.</summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<IResolveDoor>(Row.Kind) is not { } door || kept.Status.Binding is not { } binding) return;
        if (binding.Inputs.GetValueOrDefault(Problem) is not { Producer: { } producer } || binding.Inputs.GetValueOrDefault(Work)?.Revision is not { } tip) return;
        if (kept.Governed.Scope?.Branch is not { Records.Branch: { } grown }) return;
        var rebase = kept.Governed.Graph.Nodes.FirstOrDefault(node => node.Id == producer.Node);
        var target = rebase is null ? grown.Line : RebaseOnto.Of(rebase, grown.Line);
        var watched = kept.Governed.Watched;
        door.Start(new ResolveStart(kept.Governed.Run, kept.Node.Id, kept.Status.Attempt,
            new RebaseWork(watched.Root, watched.Repository, watched.Workspace, grown.Branch, tip), producer.Node, producer.Attempt,
            target is null ? "the line" : new RebaseTarget(target, "").Name)
        {
            Authority = kept.Authority,
            Bound = kept.Node.Retry?.MaxAttempts ?? AsksAtMost,
            Each = kept.Node.TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : EachAsk,
        });
    }

    public Task<NodeGitReads> ReadAsync(GovernedUnit governed, string home, NodeGitReads reads, CancellationToken ct) =>
        Task.FromResult(ResolveReading.Reads(home, reads));

    public void Release(string home, string run) => new ResolveRecords(home).Release(run);
}
