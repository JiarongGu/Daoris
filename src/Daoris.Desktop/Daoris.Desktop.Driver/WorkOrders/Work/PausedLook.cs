namespace Daoris.Driver;

/// <summary>
/// The quests of every paused work, read again at each look, onto the snapshot the plan reads: the planner holds each.
/// </summary>
[LookPass("paused", LookPhase.Read, "snapshot")]
internal sealed class PausedLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct) =>
        round.State.Snapshot = await wiring.Stages
            .TimeAsync("paused", () => PausedWork.LookAsync(wiring.Service, wiring.Config, round.State.Snapshot, ct))
            .ConfigureAwait(false);
}
