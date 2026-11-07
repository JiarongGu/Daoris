namespace Daoris.Driver;

/// <summary>
/// The supervisor's go-on on a quest's referred point (CONTEXTDRIVE1j; the context counters design §4.6 point 3), released after the plan: the
/// counts only a driven quest has, parked by its failed sessions or asking at a part's end, are read from the planner's verdicts, so each
/// held go-on whose moment came is released through the quest's own door, its <i>Try again</i>, between sessions. The same judgement the
/// person's press makes (<see cref="QuestRetry.Decide"/>), written as it is: a park's mark, or a policy's one approval.
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
        bool Retry(string quest)
        {
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
            session => snapshot.LastRun.Values.FirstOrDefault(record => record.Session == session)?.Running == true, plan, Retry);
        if (await wiring.Supervisor.ReleaseCoursesAsync(line, DateTimeOffset.UtcNow, SupervisorRules.QuestCounts, ct).ConfigureAwait(false) is { Count: > 0 })
        {
            round.Events.Add("supervisor went on with a quest its rule referred: its Try again, between sessions.");
        }
    }
}
