namespace Daoris.Driver;

/// <summary>
/// The pieces asked of partners: judged and reserved in the common admission, each started beside the
/// look in its copy and its strict job, and carried from where it stands to its answer.
/// </summary>
[LookPass("partners", LookPhase.Read, "helpers")]
internal sealed class PartnersLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var judged = round.State.Snapshot;
        var taken = round.State.Passes;
        round.State.Passes += await wiring.Stages.TimeAsync("partners", () => wiring.Partners.PiecesAsync(judged, taken, round.Events, ct))
            .ConfigureAwait(false);
    }
}
