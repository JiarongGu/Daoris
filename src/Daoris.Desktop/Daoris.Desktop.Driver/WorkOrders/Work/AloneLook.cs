namespace Daoris.Driver;

/// <summary>
/// The answers that would take their line on alone a third time in a row with nobody between, marked for the person first so the
/// planner leaves them waiting (COWORK1e). A record needs its line read only where answers alone wait on it; one a supervisor's held
/// decision holds is not picked, so nothing goes on from it inside the window.
/// </summary>
[LookPass("alone", LookPhase.Read, "supervisor")]
internal sealed class AloneLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var looked = round.State.Snapshot;
        await wiring.Stages.TimeAsync("alone", () => round.State.Alone
            .LookAsync(
                wiring.Service,
                looked.LastRun.Values.Concat(looked.NamedByReopen.Values).Where(record => !looked.SupervisorHolds(record)),
                DateTimeOffset.UtcNow, ct)).ConfigureAwait(false);
    }
}
