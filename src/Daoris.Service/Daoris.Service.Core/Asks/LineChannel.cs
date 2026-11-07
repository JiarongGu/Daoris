namespace Daoris.Knowledge;

/// <summary>
/// A line's own channel as the person reads it: the messages kept on the origin of a line of work
/// that is on no ask held here, a chat's, a quest session's or a teammate's ask's, to everyone on it, to one of its repositories or to one
/// part, oldest first, each to one part with what it reached in that part's line. It moves no mark: the person has none there, and a
/// part's own read is <see cref="ChannelParts"/>'.
/// </summary>
/// <remarks>A line on an ask held here has the ask's channel, whose person's read carries the same rows as its <c>addressed</c>.</remarks>
public sealed class LineChannel(ChannelDesk desk, ChannelStore store)
{
    /// <summary>
    /// The channel of <paramref name="session"/>'s line; null where no such session is held here, it has no channel (a helper, a reviewer,
    /// a record on no line), or its line is on an ask held here.
    /// </summary>
    public async Task<LineChannelRead?> ReadAsync(string session, CancellationToken ct = default)
    {
        var (refusal, ask, _, _, origin) = await desk.SpeakerAsync(session, ct).ConfigureAwait(false);
        if (refusal is not (ChannelRefusal.None or ChannelRefusal.NoAsk) || ask is not null || origin is null) return null;

        var messages = await store.Addressed.OfAsync(origin, ct).ConfigureAwait(false);
        var receipts = await desk.Parts.ReceiptsAsync(origin, [.. messages.Where(m => m.Part is not null)], ct).ConfigureAwait(false);
        return new LineChannelRead(origin, messages, receipts);
    }

    /// <summary>
    /// The person's post on <paramref name="session"/>'s line's own channel (their words, to everyone on it or one of its repositories),
    /// so a part of the line takes it at its look as it takes the person's words (a teammate's ask's line posts on that ask's channel,
    /// which travels to its holder); null where <see cref="ReadAsync"/> would read none.
    /// </summary>
    public async Task<ChannelOutcome?> PostAsPersonAsync(
        string session, string? text, string? to, bool urgent, DateTimeOffset now, CancellationToken ct = default)
    {
        var (refusal, ask, speaker, _, origin) = await desk.SpeakerAsync(session, ct).ConfigureAwait(false);
        if (refusal is not (ChannelRefusal.None or ChannelRefusal.NoAsk) || ask is not null || origin is null || speaker is null) return null;

        // A line on a teammate's ask has that ask's channel, which the sync carries to its holder, so the person's words go there.
        if (ChannelDesk.AskOfOrigin(origin) is { } teammates) return await desk.PostAsPersonAsync(teammates, text, to, null, now, ct, urgent).ConfigureAwait(false);

        return await desk.Parts.PostAsPersonOnLineAsync(origin, speaker.Workspace, text, to, urgent, now, ct).ConfigureAwait(false);
    }
}
