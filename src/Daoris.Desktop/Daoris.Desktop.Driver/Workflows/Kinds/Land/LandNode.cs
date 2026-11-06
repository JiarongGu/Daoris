namespace Daoris.Driver;

/// <summary>
/// The unit's landing as a step Daoris performs (LANDNODE1a; the land node design §2), 落地: the graph's one landing, by its method's options
/// (<see cref="LandNodes"/>). It takes no <c>accept</c>: it starts once its wires hold, and the person's Accept is a <c>review</c>'s (LANDNODE1c2).
/// </summary>
[NodeKind(NodeKinds.Land, NodeGroup.Landing)]
internal sealed class LandNode : BuiltInNode, IObserves, IStarts, IMakesLanding
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

    /// <summary>
    /// One landing to a graph (LANDNODE1a), the method's tail and no other: what precedes a <c>land</c> decides whether it waits on a person, and
    /// nothing here refuses one that starts without a press (LANDNODE1c2). A land of the method <c>integrate</c> brings an <c>integrate</c> after it.
    /// </summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var standing = GraphStanding.Of(graph);
        if (!ReferenceEquals(standing.Landings[0], node))
        {
            yield return new WireProblem(GraphRefusal.LandOnce, null, node.Id,
                $"node `{node.Id}`: `{standing.Landings[0].Id}` and `{node.Id}` are both a landing, and a graph holds one `{NodeKinds.Land}`.");
        }

        if (NodeOptions.Text(node, "method") == LandNodes.Integrate && !standing.ById.Values.Any(each => each.Kind == NodeKinds.Integrate))
        {
            yield return new WireProblem(GraphRefusal.LandTail, null, node.Id,
                $"node `{node.Id}`: a `{LandNodes.Integrate}` land brings a `{NodeKinds.Integrate}` after it, and the graph holds none.");
        }
    }

    public override WireProblem? DeclarationProblem(NodeDeclaration node, string field) => field != LandNodes.AcceptOption ? null
        : new WireProblem(GraphRefusal.Accept, null, node.Id, $"node `{node.Id}`: a `land` takes no `accept`. Where you accept the work, put *Your review* before it; with no review it lands once the steps before it are done.");

    /// <summary>
    /// The first option that does not hold, by its name, or null: each held to its choices, and the method's own refusals (a pattern on a merge,
    /// a squash of an integrate). Where the graph's other nodes must stand is the graph's rules'.
    /// </summary>
    public override string? Problem(NodeDeclaration node)
    {
        var method = LandNodes.Written(node, "method");
        if (node.Options.ContainsKey("method") && (method is null || !LandNodes.Methods.Contains(method, StringComparer.Ordinal))) return "method";
        if (node.Options.ContainsKey(LandNodes.AcceptOption)) return LandNodes.AcceptOption;
        if (node.Options.ContainsKey("target") && !(LandNodes.Written(node, "target") is { } target && BranchName.IsValid(target))) return "target";
        // Every method but an integrate lands on the tree's line, so a target there would only misname it; an integrate's must also be the
        // line the repository declares, which the landing's door judges (CurrentDeliveries.StartAsync).
        if (node.Options.ContainsKey("target") && method is not null && method != LandNodes.Integrate) return "target";

        if (node.Options.ContainsKey("pattern"))
        {
            // A merge makes no branch to name.
            if (method == LandNodes.Merge || LandNodes.Written(node, "pattern") is not { } pattern || BranchPatterns.Problem(pattern) is not null) return "pattern";
        }

        // A landing hands the work to no plugin: a pull request is the plugin's own step after a push (G8, D241 point 4).
        if (node.Options.ContainsKey("plugin")) return "plugin";

        // The ticket's key (WORKFLOWCLEAN1 1a): read only for a pattern that says {ticket}, so a merge, a pattern without it and an
        // integrate (which names no branch by a pattern) refuse one; its shape is the landing key's to judge.
        if (node.Options.ContainsKey(LandNodes.KeyOption))
        {
            if (LandNodes.Written(node, LandNodes.KeyOption) is not { } key || BranchPatterns.KeyProblem(key) is not null) return LandNodes.KeyOption;
            if (method == LandNodes.Merge || (LandNodes.Written(node, "pattern") is { } written && !written.Contains("{ticket}", StringComparison.Ordinal)))
            {
                return LandNodes.KeyOption;
            }
        }

        if (node.Options.TryGetValue("squash", out var squash))
        {
            if (NodeOptions.Value(squash) is not bool on) return "squash";
            if (on && method == LandNodes.Integrate) return "squash";
        }

        return node.Options.ContainsKey("message") && !(LandNodes.Written(node, "message") is { } message && LandNodes.MessageProblem(message) is null) ? "message" : null;
    }

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

    /// <summary>An attempt a lapse left unreconciled is settled by the deliveries its door lands through, before anything new starts on the unit.</summary>
    public Task<NodeRecord> SettleAsync(NodeRecordFiles records, GovernedUnit governed, NodeRecord record, CancellationToken ct) =>
        LandAttemptSettling.SettleAsync(records, governed, record, ct);

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
