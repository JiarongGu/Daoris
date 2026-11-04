namespace Daoris.Driver;

/// <summary>
/// A quest whose last session ended past its transient bound under a role that moves on it (STAFFPOLICY1e): the course decides once,
/// and the move is written down before the planner reads it, so a restart or a second look reads the one decision.
/// </summary>
[LookPass("policy", LookPhase.Read, "handovers")]
internal sealed class StaffPolicyLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var staff = wiring.Harnesses.StaffFor(wiring.Home);
        var snapshot = round.State.Snapshot;
        snapshot = snapshot with { FailuresAt = StaffCourse.FailuresAt(staff, snapshot) };
        var (policyMoving, policyHolds) = StaffCourse.Look(staff, snapshot, wiring.Config, wiring.Harnesses.Clock());
        var limitHolds = StaffCourse.LimitHolds(staff, snapshot);
        round.State.Snapshot = snapshot with
        {
            PolicyMoving = policyMoving,
            PolicyHolds = limitHolds.Count == 0 ? policyHolds : policyHolds.Concat(limitHolds).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        };
        return Task.CompletedTask;
    }
}
