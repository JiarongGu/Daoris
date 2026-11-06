namespace Daoris.Driver;

/// <summary>
/// The unit's landing as a step Daoris performs (LANDNODE1a; the land node design §2), 落地: the graph's one landing, by its method's options
/// (<see cref="LandNodes"/>). It takes no <c>accept</c>: it starts once its wires hold, and the person's Accept is a <c>review</c>'s (LANDNODE1c2).
/// </summary>
[NodeKind(NodeKinds.Land, NodeGroup.Landing)]
internal sealed class LandNode : BuiltInNode, IObserves, IStarts
{
    /// <summary>
    /// Reached when the landing record holds the bound tip, failed by the landing's own refusal. Never skipped, since a skip would read as landed;
    /// the person's doors are stop and Try again.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Land, NodeReach.Performed, NodeSource.Landing, NodeSource.Landing, false, PersonSkips: false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Branch;

    public override IReadOnlyList<NodeOptionField> Options { get; } =
    [
        NodeOptionFields.Choice("method", LandNodes.Methods),
        NodeOptionFields.Text("target"),
        NodeOptionFields.Text("pattern"),
        NodeOptionFields.Text("plugin"),
        NodeOptionFields.Text(LandNodes.KeyOption),
        NodeOptionFields.Switch("squash"),
        NodeOptionFields.Text("message"),
    ];

    public override string? Problem(NodeDeclaration node) => LandNodes.Problem(node);

    /// <summary>Drawn as the landing; with no press of its own, unless a stored <c>accept</c> it was saved with before LANDNODE1c2 says the person's.</summary>
    public override NodePart Part(NodeDeclaration node) =>
        !node.Options.TryGetValue(LandNodes.AcceptOption, out var accept) || NodeOptions.Value(accept) is "automatic"
            ? new(WorkflowKinds.Landing, WorkflowParticipation.Automatic, WorkflowExecutor.Daoris, Lowered: WorkflowKinds.Landing)
            : new(WorkflowKinds.Landing, WorkflowParticipation.You, WorkflowExecutor.Daoris, WorkflowPress.Accept, WorkflowKinds.Landing);

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Land);

    public override string? Title => "The landing";

    public override NodeLook Look => NodeLook.Landing;

    public override bool EndsByTheLine => true;

    /// <summary>Reached where the landing record holds the bound tip, by the landing's authority (LANDNODE1c).</summary>
    public NodeObservation Observe(NodeReading reading) => LandReading.Observe(reading,NodeSource.Landing);

    /// <summary>The door a run's <c>land</c> is performed through, landing as the review's Accept lands it, off the pass (WORKFLOWCLEAN1 1c).</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) =>
        wiring.Landing is { } landing ? new LandingDoor(landing, said => wiring.Say("landing.run", said), wiring.MayStart) : null;

    /// <summary>Begun in the pass, and read from the landing record as <c>handed</c> is; its door takes it after the rounds.</summary>
    public Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => Task.FromResult<WorkPerformed?>(starting.Begin());

    /// <summary>
    /// A run's <c>land</c>, active at the tip this pass read, started once on the door: a node reaches active only once its wires hold, its
    /// subject is due and the gate holds nothing, and what authorizes it is the run's review word, or the standing authority judged again
    /// before the first effect. The door is idempotent by node and tip; its delivery key fences a press and a pass at once.
    /// </summary>
    public void Keep(NodeKept kept)
    {
        if (kept.Doors.Door<ILandingDoor>(Row.Kind) is not { } door || kept.Scope.Branch is not { } branch
            || kept.Status.Binding is not { SubjectRevision: { } tip } binding)
        {
            return;
        }

        var governed = kept.Governed;
        door.Start(new LandStart(governed.Unit.Id, governed.Run, kept.Node.Id, branch.Id, branch.Executors, governed.Watched.Repository,
            governed.Watched.Workspace, tip, binding.GraphDigest, governed.Graph.Nodes.FirstOrDefault(each => each.Kind == NodeKinds.Review)?.Id));
    }
}
