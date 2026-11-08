namespace Daoris.Knowledge;

/// <summary>Where this machine's claim on a quest stands, read from the quest's operations.</summary>
internal sealed class QuestClaims(QuestStore store)
{
    /// <summary>Where THIS machine's claim on a quest stands, as <see cref="QuestLog.Claim"/> reads it.</summary>
    public async Task<QuestClaim> ClaimAsync(string id, CancellationToken ct = default) =>
        QuestLog.Claim(await store.HistoryAsync(id, ct).ConfigureAwait(false), store.Machine);

    /// <summary>Whether the claim reads lost to the person's reopen, not to another machine's take, so the driver's stop says so.</summary>
    public async Task<bool> LostToTheReopenAsync(string id, CancellationToken ct = default)
    {
        if (await ClaimAsync(id, ct).ConfigureAwait(false) != QuestClaim.Lost) return false;
        return QuestLog.LostToTheReopen(await store.HistoryAsync(id, ct).ConfigureAwait(false), store.Machine);
    }
}
