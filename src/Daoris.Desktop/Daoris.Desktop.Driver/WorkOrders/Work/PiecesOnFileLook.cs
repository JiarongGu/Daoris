namespace Daoris.Driver;

/// <summary>The pieces on file, whose reservations every starter after counts under the cap.</summary>
[LookPass("pieces", LookPhase.Read, "unable")]
internal sealed class PiecesOnFileLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        round.State.Snapshot = round.State.Snapshot with { Pieces = new PartnerFiles(wiring.Home).Reservations() };
        return Task.CompletedTask;
    }
}
