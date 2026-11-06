namespace Daoris.Driver;

/// <summary>
/// The second opinion before the landing (FABRIC-WIRE1g; the fabric events design §E4, D173's FABRIC-WORKFLOW2 note, choice 3), observed from its
/// gate's judgement (<see cref="OpinionGateReading"/>).
/// </summary>
[NodeKind(NodeKinds.Opinion, NodeGroup.Checks, NodeKinds.Check)]
public sealed class OpinionNode : BuiltInNode, IObserves
{
    public NodeObservation Observe(NodeReading reading) => OpinionGateReading.Observe(reading);

    /// <summary>
    /// FABRIC-WORKFLOW2: D157's opinion never fails (a required one waits), and the person may pass it over, as its gate's unavailable reads as
    /// skipped. FABRIC-WIRE1g: the gate's own record passes it over where its rule asks none, or an optional one none could be had.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Opinion, NodeReach.Observed, NodeSource.Opinion, null, false, true, true, true) { SkipBy = NodeSource.Opinion };

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Branch;

    public override IReadOnlyList<NodeOptionField> Options { get; } = [NodeOptionFields.Text(NodeOptionFields.Role)];

    public override NodePart Part(NodeDeclaration node) =>
        new(WorkflowKinds.Opinion, WorkflowParticipation.Agent, WorkflowExecutor.Agent, Lowered: WorkflowKinds.Opinion);

    /// <summary>The second opinion is a review's work.</summary>
    public override IReadOnlyList<string> RoleTakes { get; } = [RoleWork.Review];

    public override string? Title => "Second opinion";

    public override NodeLook Look => NodeLook.Check;

    /// <summary>A gate before every landing (FABRIC-WORKFLOW2), not asked to reach a look, whose set-up the review gate holds while an agent reads it.</summary>
    public override LandingGuard Guard => LandingGuard.Before;
}
