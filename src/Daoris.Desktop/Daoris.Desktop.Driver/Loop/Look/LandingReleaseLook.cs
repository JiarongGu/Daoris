namespace Daoris.Driver;

/// <summary>
/// The supervisor's accept, approve or reject of a run's step, released after the plan as the node's word on the run's record, through
/// the node's own press door: its window past, the step read again as still waiting on the attempt the decision names, and the person's
/// stop or pause of the line read from the planner's verdicts. The next node pass applies the word, or drops it where its decision is no
/// longer live.
/// </summary>
[LookPass("landing-release", LookPhase.Planned, "course-release")]
internal sealed class LandingReleaseLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var plan = round.State.Plan;
        var snapshot = round.State.Snapshot;
        var home = wiring.Home;
        var records = new NodeRecordFiles(home, () => HomeLease.HeldHere(home));
        var line = LandingReleaseLine.Of(home, records, due => OwnerHolds.Holds(snapshot.LastRun.Values.FirstOrDefault(record => record.Session == due.Session), plan));

        if (await wiring.Supervisor.ReleaseLandingsAsync(line, DateTimeOffset.UtcNow, ct).ConfigureAwait(false) is { Count: > 0 })
        {
            round.Events.Add("supervisor released a run's step it was to accept or approve, as the step's own word, for the node pass to apply.");
        }
    }
}
