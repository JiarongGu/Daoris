namespace Daoris.Knowledge;

/// <summary>
/// A teammate's ask's channel as this machine takes part in it: an ask held on another machine, of which this one holds the
/// messages the sync mirrored. The person here, and a session here on a quest of that ask, post to it as to an ask's channel: the
/// message is kept in <c>channel</c> as this machine's own, at its level, and the sync carries it to the holder, whose parts take it as
/// a teammate's claim, never their person's words. A session here reads it as an entry reads an ask's channel, its mark and its
/// acknowledgments kept on this machine.
/// </summary>
/// <remarks>
/// No ask record is here, so nothing is kept as the ask's word and Daoris's lines, read from the holder's quests, are not in it. A
/// <c>to</c> may name everyone or a repository the ask's messages were posted from or to; a part of the ask's lines elsewhere is the
/// holder's, never addressed from here.
/// </remarks>
public sealed class TeammateAskChannel(ChannelStore store, Func<string, CancellationToken, Task<bool>>? holderGone = null)
{
    private const string Everyone = "everyone";

    /// <summary>The person's post: no cap, as on an ask of theirs. Not found where no message of that ask is mirrored here.</summary>
    public async Task<ChannelOutcome> PostAsPersonAsync(
        string askId, string? text, string? to, string? re, bool urgent, DateTimeOffset now, CancellationToken ct)
    {
        var held = await store.OfAsync(askId, ct).ConfigureAwait(false);
        if (held.Count == 0) return new(ChannelRefusal.NotFound, null);

        var judged = Judge(held, text, to, re);
        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);

        var message = new ChannelMessage(
            NewId(), askId, held[0].Workspace, now, Session: null, Repository: null, Agent: null, Entry: null, judged.To, judged.Re,
            Question: false, judged.Text!) { Urgent = urgent };
        await store.AddAsync(message, int.MaxValue, ct).ConfigureAwait(false);
        return new(ChannelRefusal.None, message);
    }

    /// <summary>A session's post, on the ask its line of work is on: the part's bounds as on any channel.</summary>
    public async Task<ChannelOutcome> PostAsSessionAsync(
        Session session, string entry, string askId, string? text, string? to, string? re, bool question, bool urgent, DateTimeOffset now,
        CancellationToken ct)
    {
        var judged = Judge(await store.OfAsync(askId, ct).ConfigureAwait(false), text, to, re);
        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);

        var message = new ChannelMessage(
            NewId(), askId, session.Workspace, now, session.Id, session.Repository, session.Door, entry, judged.To, judged.Re, question,
            judged.Text!) { Urgent = urgent };
        return await store.KeepAsync(message, ChannelBounds.MaxPerPart, ct).ConfigureAwait(false) is { } kept
            ? new(ChannelRefusal.None, kept)
            : new(ChannelRefusal.TooMany, null);
    }

    /// <summary>
    /// A session's read, as an ask's: what came after its entry's mark (or <paramref name="since"/>), and each message for its entry it
    /// has not settled, acknowledged read before the answer reaches the agent; then the mark moves. <paramref name="mine"/> is what
    /// waits for it alone, read already.
    /// </summary>
    public async Task<ChannelReadOutcome> ReadAsSessionAsync(
        Session session, string entry, string origin, string askId, DateTimeOffset? since, IReadOnlyList<ChannelMessage> mine, DateTimeOffset now,
        CancellationToken ct)
    {
        var mark = await store.MarkAsync(askId, entry, ct).ConfigureAwait(false);
        var from = since ?? mark;
        var messages = await store.OfAsync(askId, ct).ConfigureAwait(false);
        var acknowledged = (await store.AcksOfAsync(askId, ct).ConfigureAwait(false))
            .Where(row => string.Equals(row.Entry, entry, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(row => row.Message, row => row.Ack, StringComparer.Ordinal);

        List<ChannelMessage> returned = [.. messages.Where(m => from is null || m.At > from
            || (since is null && IsFor(m, entry) && !ChannelAcks.Settles(acknowledged.GetValueOrDefault(m.Id)?.State)))];
        foreach (var message in returned.Where(m => IsFor(m, entry)))
        {
            await store.AcknowledgeAsync(askId, entry, message.Id, ChannelAcks.Read, session.Id, now, ct).ConfigureAwait(false);
        }

        if (messages.Count > 0) await store.MoveMarkAsync(askId, entry, messages.Max(m => m.At), ct).ConfigureAwait(false);
        var unread = messages.Count(m => mark is null || m.At > mark) + mine.Count;
        return new(ChannelRefusal.None, null, new ChannelRead([.. returned.Concat(mine).OrderBy(m => m.At)], [], unread, mark), entry)
        {
            Origin = origin,
        };
    }

    /// <summary>
    /// An entry's acknowledgments of the mirrored messages, by message id: what the driver's look reads to hand a part here what its
    /// entry has not settled. A claim whose holder no longer runs is read as none, so the entry's next part takes it.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ChannelAck>> AcknowledgedAsync(string askId, string entry, CancellationToken ct)
    {
        var held = (await store.AcksOfAsync(askId, ct).ConfigureAwait(false))
            .Where(row => string.Equals(row.Entry, entry, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(row => row.Message, row => row.Ack, StringComparer.Ordinal);
        foreach (var (message, ack) in held.ToList())
        {
            if (ack is { State: ChannelAcks.Taking, Session: { } holder } && await GoneAsync(holder, ct).ConfigureAwait(false)) held.Remove(message);
        }

        return held;
    }

    /// <summary>
    /// An entry acknowledging mirrored messages by their ids, as on an ask held here: a claim is one part's only, the rest raise what the
    /// entry holds. Only messages this machine holds count. Null where none of that ask is mirrored here.
    /// </summary>
    /// <returns>The messages whose acknowledgment changed.</returns>
    public async Task<IReadOnlyList<string>?> AcknowledgeAsync(
        string askId, string entry, string? session, string state, IReadOnlyList<string> messages, DateTimeOffset now, CancellationToken ct)
    {
        var mirrored = (await store.OfAsync(askId, ct).ConfigureAwait(false)).Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        if (mirrored.Count == 0) return null;
        if (!ChannelAcks.Known(state) || string.IsNullOrWhiteSpace(entry) || entry == ChannelDesk.PersonEntry) return [];

        var held = await store.AcksOfAsync(askId, ct).ConfigureAwait(false);
        var changed = new List<string>();
        foreach (var id in messages.Where(mirrored.Contains).Distinct(StringComparer.Ordinal))
        {
            bool moved;
            if (state == ChannelAcks.Taking)
            {
                if (string.IsNullOrWhiteSpace(session)) continue;
                var current = held.FirstOrDefault(row => row.Message == id && string.Equals(row.Entry, entry, StringComparison.OrdinalIgnoreCase)).Ack;
                var stale = current is { State: ChannelAcks.Taking, Session: { } holder } && holder != session && await GoneAsync(holder, ct).ConfigureAwait(false)
                    ? current.Session
                    : null;
                moved = await store.ClaimAsync(askId, entry, id, session, stale, now, ct).ConfigureAwait(false);
            }
            else
            {
                moved = await store.AcknowledgeAsync(askId, entry, id, state, session, now, ct).ConfigureAwait(false);
            }

            if (moved) changed.Add(id);
        }

        return changed;
    }

    private async Task<bool> GoneAsync(string holder, CancellationToken ct) => holderGone is not null && await holderGone(holder, ct).ConfigureAwait(false);

    /// <summary>Whether a message is for an entry: to its repository or to everyone, and posted by no part of the entry itself.</summary>
    private static bool IsFor(ChannelMessage message, string entry)
    {
        var repository = entry.IndexOf('/') is > 0 and var slash ? entry[(slash + 1)..] : "";
        return (message.To is null || string.Equals(message.To, repository, StringComparison.OrdinalIgnoreCase))
               && !string.Equals(message.Entry, entry, StringComparison.Ordinal);
    }

    /// <summary>The words trimmed and bounded; <c>to</c> everyone or a repository the ask's messages name; <c>re</c> one of its messages.</summary>
    internal static (ChannelRefusal Refusal, string? Text, string? To, string? Re) Judge(
        IReadOnlyList<ChannelMessage> held, string? text, string? to, string? re)
    {
        var words = text?.Trim() ?? "";
        if (words.Length == 0) return (ChannelRefusal.Empty, null, null, null);
        if (words.Length > ChannelBounds.MaxText) return (ChannelRefusal.TooLong, null, null, null);

        string? address = null;
        var named = to?.Trim().TrimStart('#') ?? "";
        if (named.Length > 0 && !named.Equals(Everyone, StringComparison.OrdinalIgnoreCase))
        {
            address = held.SelectMany(m => new[] { m.Repository, m.To }).OfType<string>()
                .FirstOrDefault(repository => repository.Equals(named, StringComparison.OrdinalIgnoreCase));
            if (address is null) return (ChannelRefusal.BadTo, null, null, null);
        }

        string? answers = null;
        if (!string.IsNullOrWhiteSpace(re))
        {
            answers = held.FirstOrDefault(m => m.Id == re.Trim())?.Id;
            if (answers is null) return (ChannelRefusal.BadRe, null, null, null);
        }

        return (ChannelRefusal.None, words, address, answers);
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
