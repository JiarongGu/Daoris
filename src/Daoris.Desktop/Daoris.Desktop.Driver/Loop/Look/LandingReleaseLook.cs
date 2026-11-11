namespace Daoris.Driver;

/// <summary>
/// The supervisor's landing decisions released after the plan (<see cref="LandingReleaseLine"/>), the person's holds read from its verdicts.
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
