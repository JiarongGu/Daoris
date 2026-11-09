namespace Daoris.Driver;

/// <summary>
/// What each running unit changed: a git read of each running tree that moved since the last look
/// (<see cref="TreeChangesKept"/>), and the quests whose integration request is open, each read where the open list lacks it, for the planner's
/// comparison at a start.
/// </summary>
[LookPass("overlaps", LookPhase.Read, "committed")]
internal sealed class OverlapsLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var committedLook = round.State.Snapshot;
        var integrating = new IntegrationStore(wiring.Home).Open().Select(request => request.Quest).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = TreeChangesKept.Of(wiring.Home);
        var looked = await wiring.Stages.TimeAsync("overlaps", () => WorkOverlaps.LookAsync(committedLook, integrating, kept.ReadAsync, ct))
            .ConfigureAwait(false);
        round.State.Snapshot = looked with
        {
            IntegratingQuests = await IntegratingQuests.ReadAsync(looked, integrating, wiring.Service.EveryQuestAsync, ct).ConfigureAwait(false),
        };
    }
}
