namespace Daoris.Driver;

/// <summary>
/// The commits a failed session left beyond its start, for each open quest it failed before taking, so the planner
/// carries the quest on in that tree rather than building its work again in a new one. No git starts where no such quest is.
/// </summary>
[LookPass("committed", LookPhase.Read, "policy")]
internal sealed class CommittedLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var handed = round.State.Snapshot;
        round.State.Snapshot = await wiring.Stages.TimeAsync("committed", () => CommittedWork.LookAsync(handed, CommittedWork.CountAsync, ct))
            .ConfigureAwait(false);
    }
}
