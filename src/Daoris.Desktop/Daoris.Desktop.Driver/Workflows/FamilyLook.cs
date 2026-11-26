namespace Daoris.Driver;

/// <summary>
/// The look's family pass: an ask across repositories moves its consumer part's pin to the commit the producer's part landed (<see cref="AskPinning"/>);
/// then the fan-out (<see cref="FamilyFanout"/>): a producer's line that moved past a consumer's pin, and past what an open ask carries, opens or
/// retargets the edge's one pin-move quest, a red move's break is filed, and a hold that has outlasted its edge's lag bound is told to the person once
/// (<see cref="FamilyNotices"/>). Only the lease's loop acts, as a line's verification does.
/// </summary>
[LookPass("family", LookPhase.After, "departures")]
internal sealed class FamilyLook(ILookWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        var registry = round.State.Snapshot.Repositories;
        if (!FamilyFanout.HasFamily(registry) || !HomeLease.HeldHere(wiring.Home)) return;
        var now = wiring.Clock.GetUtcNow();
        var quests = await wiring.Service.EveryQuestAsync(ct).ConfigureAwait(false);
        var carried = await PinAsksAsync(registry, round, ct).ConfigureAwait(false);
        var done = await FamilyFanout.RunAsync(registry, quests, wiring.Service, TipAsync, LockAsync, now, ct, edge => carried.GetValueOrDefault(edge)).ConfigureAwait(false);
        round.Events.AddRange(done.Said);
        var occurrences = FamilyNotices.Occurrences(done.Told, registry, now);
        if (occurrences.Count > 0) new News(wiring.Home).Claim(FamilyNotices.Teller, occurrences, null, now);
    }

    /// <summary>Moves the pins the open asks owe their consumer parts, and gives back the commit each edge's open ask carries, so the fan-out asks nothing of it.</summary>
    private async Task<IReadOnlyDictionary<PinEdge, string>> PinAsksAsync(IReadOnlyList<RepoView> registry, LookRound round, CancellationToken ct)
    {
        var roots = registry.Where(repo => repo.Root is { Length: > 0 }).GroupBy(repo => repo.Repository, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Root!, StringComparer.Ordinal);
        var asks = await AskRunReads.ReadAsync(wiring.Home, roots, wiring.Config, ct).ConfigureAwait(false);
        if (asks.Runs.Count == 0) return new Dictionary<PinEdge, string>();
        var reports = await PartsReports.ReadAsync(new IntegrationStore(wiring.Home), new ServiceTrainPlans(wiring.Service), ct, asks.Asks).ConfigureAwait(false);
        var doors = new AskPinDoors();
        round.Events.AddRange(await AskPinning.RunAsync(registry, reports, asks.Runs, doors, ct).ConfigureAwait(false));
        return await AskPinning.CarriedAsync(registry, reports, asks.Runs, doors, ct).ConfigureAwait(false);
    }

    private async Task<string?> TipAsync(RepoView repo, CancellationToken ct)
    {
        var line = (await CanonicalLine.ResolveAsync(repo.Root!, repo.Repository, repo.Workspace, wiring.Config, ct).ConfigureAwait(false)).Branch;
        return line is { Length: > 0 } ? await LineBatching.TipAsync(repo.Root!, line, ct).ConfigureAwait(false) : null;
    }

    private async Task<LockText> LockAsync(RepoView repo, CancellationToken ct) =>
        await TipAsync(repo, ct).ConfigureAwait(false) is { } tip
            ? new LockText(true, await MachinePorts.Repository.TextAtAsync(repo.Root!, tip, FamilyLock.FileName, ct).ConfigureAwait(false))
            : new LockText(false, null);
}
