namespace Daoris.Knowledge;

/// <summary>A member still on its channel, and whether that channel's claims have expired (rule 15), which only the look acts on.</summary>
public sealed record ChannelMemberStanding(ChannelMember Member, bool Spent);

/// <summary>
/// The members a channel holds now, for the person's page and the driver's look. A channel's claims are spent once its ask
/// has no quest open and no part of its line, nor a helper a part asked, is still active here, judged as it is read and never stored, so a quest opened again, or a
/// part started again, makes it not spent again. A channel whose ask this machine does not hold is judged by its parts alone.
/// </summary>
public sealed class ChannelMemberRoll(ChannelDesk desk, ChannelStore store, AskDesk asks, SessionLedger ledger)
{
    /// <summary>
    /// The members of the channel <paramref name="ask"/> or a session of the line <paramref name="line"/> names, those that left left out;
    /// with neither, every member on the machine. A channel that is none is empty.
    /// </summary>
    public async Task<IReadOnlyList<ChannelMemberStanding>> ListAsync(string? ask, string? line, CancellationToken ct = default)
    {
        IReadOnlyList<ChannelMember> held;
        if (line?.Trim() is { Length: > 0 } session)
        {
            var (refusal, _, _, _, origin) = await desk.SpeakerAsync(session, ct).ConfigureAwait(false);
            held = refusal is ChannelRefusal.None or ChannelRefusal.NoAsk && origin is not null ? await store.Members.OfAsync(origin, ct).ConfigureAwait(false) : [];
        }
        else if (ask?.Trim().TrimStart('#') is { Length: > 0 } askId)
        {
            held = await store.Members.OfAsync(SessionEntries.AskOrigin(askId), ct).ConfigureAwait(false);
        }
        else
        {
            held = await store.Members.AllAsync(ct).ConfigureAwait(false);
        }

        var active = await ActiveEntriesAsync(ct).ConfigureAwait(false);
        var standing = new List<ChannelMemberStanding>();
        foreach (var member in held.Where(m => m.Left is null))
        {
            standing.Add(new(member, await SpentAsync(member.Origin, active, ct).ConfigureAwait(false)));
        }

        return standing;
    }

    private async Task<bool> SpentAsync(string origin, IReadOnlyList<string> active, CancellationToken ct)
    {
        if (active.Any(entry => entry.StartsWith(origin + "/", StringComparison.OrdinalIgnoreCase))) return false;
        if (ChannelDesk.AskOfOrigin(origin) is not { } askId || await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } held) return true;
        return !(await desk.QuestsOfAsync(held, ct).ConfigureAwait(false)).Any(q => q.Status is QuestStatus.Open or QuestStatus.Taken);
    }

    /// <summary>
    /// The entries of this machine's parts still active (queued, starting, working or parked), and of each session an active helper still
    /// serves: what a part asked of a helper is open while the helper runs, though the part has ended.
    /// </summary>
    private async Task<IReadOnlyList<string>> ActiveEntriesAsync(CancellationToken ct)
    {
        var active = (await ledger.ListAsync(includeClosed: false, ct: ct).ConfigureAwait(false)).Where(s => s.Origin is null).ToList();
        var asked = new List<Session>();
        foreach (var helper in active.Where(s => s.Serves is not null))
        {
            if (await ledger.FindAsync(helper.Serves!, ct).ConfigureAwait(false) is { } asker) asked.Add(asker);
        }

        return [.. (await ledger.WithEntriesAsync([.. active, .. asked], ct).ConfigureAwait(false)).Select(s => s.Entry).OfType<string>()];
    }
}
