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

        Task<string?> Why(SupervisorLandingDue due)
        {
            var status = WorkPerformer.StatusOf(records.Read(RunUnits.IdOf(due.Run)) ?? new NodeRecord(RunUnits.IdOf(due.Run)), due.Node);
            if (status.State != NodeStates.Active || status.Attempt != due.Attempt) return Task.FromResult<string?>(SupervisorWhy.Answered);
            var line = snapshot.LastRun.Values.FirstOrDefault(record => record.Session == due.Session);
            return Task.FromResult(OwnerHolds.Holds(line, plan) ? SupervisorWhy.OwnerHeld : null);
        }

        string? Word(SupervisorLandingDue due) => RunNodeWords.Word(
            records, due.Run, due.Node, SupervisorLandingActs.WordOf(due.Do) ?? due.Do,
            due.Do == SupervisorLandingActs.Reject ? due.Words : null, by: new NodeWordBy(due.Generation, due.Id), attempt: due.Attempt).Refused;

        if (await wiring.Supervisor.ReleaseLandingsAsync(new LandingRelease(Why, Word), DateTimeOffset.UtcNow, ct).ConfigureAwait(false) is { Count: > 0 })
        {
            round.Events.Add("supervisor released a run's step it was to accept or approve, as the step's own word, for the node pass to apply.");
        }
    }
}
