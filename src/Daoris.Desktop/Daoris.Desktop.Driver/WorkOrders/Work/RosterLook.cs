namespace Daoris.Driver;

/// <summary>
/// The roster told what the snapshot started, from the roster's mark taken before it; the snapshot as it stands is the one the
/// channel, the reopens, the reviews and the opinions read.
/// </summary>
[LookPass("roster", LookPhase.Read, "overlaps-told")]
internal sealed class RosterLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        wiring.Harnesses.Look(round.State.Snapshot.Started, round.State.Mark);
        round.State.Looked = round.State.Snapshot;
        return Task.CompletedTask;
    }
}
