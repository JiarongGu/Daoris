namespace Daoris.Driver;

/// <summary>
/// The supervisor's go-on, move or wait on a quest's referred point, released after the plan through the quest's own door, its points read
/// from the planner's verdicts: a go-on and a move are the judgement the person's press makes (<see cref="QuestRetry.Decide"/>), a park's
/// mark or a policy's one approval; a wait is the cut part's wait for its choice's reset (<see cref="StaffMoves.Wait"/>).
/// </summary>
[LookPass("course-release", LookPhase.Planned, "rule-acts")]
internal sealed class CourseReleaseLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var plan = round.State.Plan;
        var snapshot = round.State.Snapshot;
        var path = DriverConfig.ResolvePath(wiring.Home);
        var points = ReferredPoints.For(wiring.Home, wiring.Events, wiring.Marks);
        bool Release(string quest, string act)
        {
            if (act == SupervisorLimitActs.Wait)
            {
                return plan.FirstOrDefault(each => string.Equals(each.Quest.Id, quest, StringComparison.OrdinalIgnoreCase)) is { Verdict: StartVerdict.PolicyHeld, HeldBy: { Limit: true } cut }
                       && new StaffMoves(wiring.Home).Wait(quest, cut.Actor ?? RoleWork.Build, cut.Session);
            }

            try
            {
                var decision = QuestRetry.Decide(quest, plan);
                StandingChoices.Write(path, decision.Change);
                return decision.Approve is not { } role || new StaffMoves(wiring.Home).Approve(quest, role);
            }
            catch (Exception error) when (error is ChoiceRefusal or DriverConfigUnreadableException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        var line = points.Line(
            session => snapshot.LastRun.Values.FirstOrDefault(record => record.Session == session),
            session => snapshot.LastRun.Values.FirstOrDefault(record => record.Session == session)?.Running == true, plan, Release);
        if (await wiring.Supervisor.ReleaseCoursesAsync(line, DateTimeOffset.UtcNow, SupervisorRules.QuestCounts, ct).ConfigureAwait(false) is { Count: > 0 })
        {
            round.Events.Add("supervisor released a quest its rule referred through the quest's own door, between sessions.");
        }
    }
}
