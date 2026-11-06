namespace Daoris.Driver;

/// <summary>The agent's done on the ledger (§C2): a claim, observed, never skipped since <i>held</i> rests on it, and never a graph's end.</summary>
[NodeKind(NodeKinds.Claimed, NodeGroup.Work)]
internal sealed class ClaimedNode : BuiltInNode, IObserves
{
    /// <summary>The option name round 4 gave <c>claimed</c> for the hold on the work's requests, refused by name (INTEGRATE1o, C28).</summary>
    private const string WorkRequestsOption = "requests";

    public override NodeAuthority Row { get; } =
        new(NodeKinds.Claimed, NodeReach.Observed, NodeSource.Ledger, null, false, false, MayEnd: false, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Quest;

    public override IReadOnlyList<NodeOptionField> Options { get; } = [NodeOptionFields.Text(NodeOptionFields.Role)];

    /// <summary>
    /// The hold on the work's requests is the kind <see cref="NodeKinds.WorkRequests"/> (§17.6 point 8). Written as an option here, a build that
    /// knows <c>claimed</c> would read it and run the work with the hold dropped, so no file may say it.
    /// </summary>
    public override string? Problem(NodeDeclaration node) => node.Options.ContainsKey(WorkRequestsOption) ? WorkRequestsOption : null;

    public override NodePart Part(NodeDeclaration node) =>
        new(WorkflowKinds.Work, WorkflowParticipation.Agent, WorkflowExecutor.Agent, Lowered: WorkflowKinds.Work);

    /// <summary>The work step as design work, which a build role takes too.</summary>
    public override IReadOnlyList<string> RoleTakes { get; } = [RoleWork.Design, RoleWork.Build];

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Claimed);

    public override string? Title => "The work";

    public override NodeLook Look => NodeLook.Work;

    public override bool EndsByTheLine => true;

    public NodeObservation Observe(NodeReading reading) => ClaimedReading.Observe(reading);
}
