namespace Daoris.Driver;

/// <summary>
/// The look's family fan-out (<see cref="FamilyFanout"/>): a producer's line that moved past a consumer's pin opens or retargets the edge's one
/// pin-move quest, a red move's break is filed, and a hold that has outlasted its edge's lag bound is told to the person once
/// (<see cref="FamilyNotices"/>). Only the lease's loop acts, as a line's verification does.
/// </summary>
[LookPass("family", LookPhase.After, "departures")]
internal sealed class FamilyLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var registry = round.State.Snapshot.Repositories;
        if (!registry.Any(row => row.Root is { Length: > 0 }) || !HomeLease.HeldHere(wiring.Home)) return;
        var now = wiring.Clock.GetUtcNow();
        var quests = await wiring.Service.EveryQuestAsync(ct).ConfigureAwait(false);
        var done = await FamilyFanout.RunAsync(registry, quests, wiring.Service, (repo, token) => TipAsync(repo, token), now, ct).ConfigureAwait(false);
        round.Events.AddRange(done.Said);
        var occurrences = FamilyNotices.Occurrences(done.Told, registry, now);
        if (occurrences.Count > 0) new News(wiring.Home).Claim(FamilyNotices.Teller, occurrences, null, now);
    }

    private async Task<string?> TipAsync(RepoView repo, CancellationToken ct)
    {
        var line = (await CanonicalLine.ResolveAsync(repo.Root!, repo.Repository, repo.Workspace, wiring.Config, ct).ConfigureAwait(false)).Branch;
        return line is { Length: > 0 } ? await LineBatching.TipAsync(repo.Root!, line, ct).ConfigureAwait(false) : null;
    }
}
