namespace Daoris.Driver;

/// <summary>Two running units changing one path are each told once, through the service's say.</summary>
[LookPass("overlaps-told", LookPhase.Read, "overlaps")]
internal sealed class OverlapsToldLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var overlapLook = round.State.Snapshot;
        await wiring.Stages.TimeAsync("overlaps-told", () => WorkOverlaps.TellAsync(wiring.Home, overlapLook, async (session, text, key, token) =>
            await wiring.Service.SayAsync(session, text, ct: token, from: DaorisWords.From, key: key, running: true).ConfigureAwait(false), ct))
            .ConfigureAwait(false);
    }
}
