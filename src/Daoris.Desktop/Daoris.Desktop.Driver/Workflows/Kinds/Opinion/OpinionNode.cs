namespace Daoris.Driver;

/// <summary>
/// The second opinion before the landing, observed from its
/// gate's judgement (<see cref="OpinionGateReading"/>).
/// </summary>
[NodeKind(NodeKinds.Opinion, NodeGroup.Checks, NodeKinds.Check)]
public sealed class OpinionNode : BuiltInNode, IObserves, IAsksReads, IWaitsOnRole
{
    public PassRead Asks => PassRead.Gates;

    string? IWaitsOnRole.Waits(NodeDeclaration node, NodeStatus status) => status.Holds == OpinionGateReading.Vacant ? PersonWaits.Vacant : null;

    public NodeObservation Observe(NodeReading reading) => OpinionGateReading.Observe(reading);

    public bool ObservesFailed(NodeStatus status) => OpinionStepWaits.EndedSaid(status.Code) is not null;

    /// <summary>
    /// An opinion fails only on a pass that ended without giving one (<see cref="OpinionStepWaits.EndedSaid"/>); a required one nobody could
    /// read waits, and the person may pass it over, as its gate's unavailable reads as skipped. The gate's own record passes it over where its
    /// rule asks none, or an optional one nobody could be had for.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Opinion, NodeReach.Observed, NodeSource.Opinion, NodeSource.Opinion, false, true, true, true) { SkipBy = NodeSource.Opinion };

    public override IReadOnlyList<string> Waits => OpinionStepWaits.Codes;

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Branch;

    public override string Role => StepRoles.Opinion;

    public override NodePart Part(NodeDeclaration node) =>
        new(WorkflowKinds.Opinion, WorkflowParticipation.Agent, WorkflowExecutor.Agent, Lowered: WorkflowKinds.Opinion);

    /// <summary>The second opinion is a review's work.</summary>
    public override IReadOnlyList<string> RoleTakes { get; } = [RoleWork.Review];

    public override string? Title => "Second opinion";

    public override NodeLook Look => NodeLook.Check;

    /// <summary>A gate before every landing, not asked to reach a look, whose set-up the review gate holds while an agent reads it.</summary>
    public override LandingGuard Guard => LandingGuard.Before;
}
