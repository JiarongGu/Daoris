namespace Daoris.Knowledge;

/// <summary>
/// The take behind a stand-down, read at a session's open: a quest's newest record here stood down, yet its claim is this machine's and an
/// earlier record here took it and ended failed or stopped. That stand-down is a carry-on refused against the dead holder's own take
/// (<see cref="QuestRetakes"/> passes it on), so it says nothing of whose the quest is, and the quest goes on from the taker.
/// </summary>
internal static class StandDownTakers
{
    /// <summary>The record the quest is carried on from, or null where the stand-down stands: the claim is not this machine's, or no earlier record here took and ended so.</summary>
    public static async Task<Session?> BehindAsync(QuestStore quests, SessionStore sessions, Session stoodDown, CancellationToken ct)
    {
        if (stoodDown is not { State: SessionState.StoodDown, Quest: { } quest }) return null;
        if (QuestLog.Claim(await quests.HistoryAsync(quest, ct).ConfigureAwait(false), quests.Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed)) return null;

        var records = (await sessions.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false))
            .Where(record => record.Origin is null && string.Equals(record.Quest, quest, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (records.Any(record => record.Active)) return null;

        var taker = records.Where(record => record.Took && record.Created < stoodDown.Created).OrderByDescending(record => record.Created).FirstOrDefault();
        // A stop that is the person's is theirs to release, whatever stood down after it.
        return taker is { State: SessionState.Failed } or { State: SessionState.Stopped, Interrupted: true } ? taker : null;
    }
}
