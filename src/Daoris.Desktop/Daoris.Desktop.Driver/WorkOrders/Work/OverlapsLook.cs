namespace Daoris.Driver;

/// <summary>
/// What each running unit changed (INTEGRATE1l, the integration design §9): one git read per running session, and the quests whose
/// integration request is open, for the planner's comparison at a start.
/// </summary>
[LookPass("overlaps", LookPhase.Read, "committed")]
internal sealed class OverlapsLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var committedLook = round.State.Snapshot;
        var integrating = new IntegrationStore(wiring.Home).Open().Select(request => request.Quest).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        round.State.Snapshot = await wiring.Stages.TimeAsync("overlaps", () => WorkOverlaps.LookAsync(committedLook, integrating, WorkOverlaps.DiffAsync, ct))
            .ConfigureAwait(false);
    }
}
