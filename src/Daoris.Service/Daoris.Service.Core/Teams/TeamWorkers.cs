namespace Daoris.Knowledge;

/// <summary>
/// A worker's membership: the session opened on a part's quest is a team member for that part, at the newest revision that holds it, so the
/// team the ask page shows is every session that serves it and not its leads alone.
/// </summary>
/// <param name="store">The part-to-quest memory and the plan revisions.</param>
/// <param name="ledger">Where the membership is kept.</param>
public sealed class TeamWorkers(TeamStore store, TeamLedger ledger)
{
    /// <summary>
    /// Records <paramref name="session"/> as the worker of the part its quest is; a session on a quest no plan published is no member, and a
    /// session recorded again moves its row to the newer revision.
    /// </summary>
    /// <returns>Whether a row was kept.</returns>
    public async Task<bool> KeepAsync(string session, string quest, DateTimeOffset now, CancellationToken ct = default)
    {
        if (await store.OfQuestAsync(quest, ct).ConfigureAwait(false) is not { } part) return false;
        var plans = await store.LatestAsync(part.Ask, ct).ConfigureAwait(false);
        var revision = plans.FirstOrDefault(plan => plan.Parts.Any(each => each.Id == part.Part))?.Revision ?? 1;
        await ledger.KeepAsync(new TeamMember(part.Ask, part.Part, TeamMember.Worker, session, revision, now), ct).ConfigureAwait(false);
        return true;
    }
}
