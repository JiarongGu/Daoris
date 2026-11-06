namespace Daoris.Driver;

/// <summary>
/// The unit's landing as a step Daoris performs (LANDNODE1a; the land node design §2), 落地: the graph's one landing, by its method's options
/// (<see cref="LandNodes"/>). It takes no <c>accept</c>: it starts once its wires hold, and the person's Accept is a <c>review</c>'s (LANDNODE1c2).
/// </summary>
[NodeKind(NodeKinds.Land, NodeGroup.Landing)]
internal sealed class LandNode : BuiltInNode
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

    public override string? Evidence => BuiltInNodes.EvidenceSchema(NodeKinds.Land);

    public override string? Title => "The landing";

    public override NodeLook Look => NodeLook.Landing;
}
