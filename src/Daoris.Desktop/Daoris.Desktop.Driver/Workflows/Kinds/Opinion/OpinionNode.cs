namespace Daoris.Driver;

/// <summary>
/// The second opinion before the landing, observed from its
/// gate's judgement (<see cref="OpinionGateReading"/>).
/// </summary>
[NodeKind(NodeKinds.Opinion, NodeGroup.Checks, NodeKinds.Check)]
public sealed class OpinionNode : BuiltInNode, IObserves, IAsksReads
{
    public PassRead Asks => PassRead.Gates;

    public NodeObservation Observe(NodeReading reading) => OpinionGateReading.Observe(reading);

    /// <summary>
    /// An opinion never fails (a required one waits), and the person may pass it over, as its gate's unavailable reads as
    /// skipped. The gate's own record passes it over where its rule asks none, or an optional one none could be had.
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

    /// <summary>A gate before every landing, not asked to reach a look, whose set-up the review gate holds while an agent reads it.</summary>
    public override LandingGuard Guard => LandingGuard.Before;
}
