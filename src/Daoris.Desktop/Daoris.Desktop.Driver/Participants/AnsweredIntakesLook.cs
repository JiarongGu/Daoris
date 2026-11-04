namespace Daoris.Driver;

/// <summary>
/// A parked intake has no process left to watch (it asked the person and ended), so its record ends when the person answers the ask
/// (D65 §1b). Looked at every tick, whatever the intake setting says now: turning intakes off must not strand one that is waiting.
/// </summary>
[LookPass("answered", LookPhase.Planned)]
internal sealed class AnsweredIntakesLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct) =>
        await wiring.Stages
            .TimeAsync("answered", () => wiring.Intakes.ConcludeAnsweredAsync(round.State.Snapshot.Active, round.Events, round.State.Concluded, ct))
            .ConfigureAwait(false);
}
