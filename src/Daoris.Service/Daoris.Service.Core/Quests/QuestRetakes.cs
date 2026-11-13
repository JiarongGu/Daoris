namespace Daoris.Knowledge;

/// <summary>
/// A take asked of a quest this machine already holds, by a session of that quest, while the session that took it has ended
/// failed or stopped: the take passes to the asker, so a carry-on or a retry goes on rather than standing down against a holder
/// that is no longer there. The one release of a dead holder's take; a live holder, or another machine's take, is refused as ever.
/// </summary>
public sealed class QuestRetakes(QuestStore quests, SessionLedger ledger)
{
    /// <summary>
    /// The sentence that answers the take as the asker's own, or null where the take is not a dead holder's to pass: the asker is not
    /// a running session of this quest here, another session of it still runs, the claim is not this machine's, or no session
    /// here took it and ended failed or stopped.
    /// </summary>
    public async Task<string?> AdoptAsync(string questId, string session, CancellationToken ct = default)
    {
        var id = questId.TrimStart('#');
        if (await quests.FindAsync(id, ct).ConfigureAwait(false) is not { Status: QuestStatus.Taken }) return null;
        if (QuestLog.Claim(await quests.HistoryAsync(id, ct).ConfigureAwait(false), quests.Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed)) return null;
        if (await ledger.FindAsync(session, ct).ConfigureAwait(false) is not { Active: true, Origin: null } asker
            || !string.Equals(asker.Quest, id, StringComparison.OrdinalIgnoreCase)) return null;

        var records = (await ledger.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false))
            .Where(record => record.Origin is null && string.Equals(record.Quest, id, StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(record.Id, session, StringComparison.Ordinal))
            .ToList();
        if (records.Any(record => record.Active)) return null;
        if (records.Where(record => record.Took).OrderByDescending(record => record.Created).FirstOrDefault() is not
            { State: SessionState.Failed or SessionState.Stopped } holder) return null;

        if (!await ledger.MarkTookAsync(session, id, ct).ConfigureAwait(false)) return null;
        return $"Quest `#{id}` was taken by session `{holder.Id}`, which {holder.StateName} before it finished: its take is yours now. "
               + "Carry on from its tree and close it.";
    }
}
