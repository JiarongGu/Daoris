using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// The judgement over an ask's channel: who may post, what a message may say and to whom, what a
/// read holds, and Daoris's lines, read from the ask's quests. The store holds the messages; this decides, for the connector's two tools
/// and the HTTP host's two doors alike.
/// </summary>
/// <remarks>
/// A message asks nothing of anyone and starts nothing: it is read at a session's next look, and work stays a quest.
/// </remarks>
public sealed class ChannelDesk(
    ChannelStore store, AskDesk asks, AskStore askStore, SessionLedger ledger, QuestStore quests, ISessionEntryFacts facts)
{
    /// <summary>The entry the person reads as: their read mark's key.</summary>
    public const string PersonEntry = "person";

    /// <summary>The address that means every repository, said the way a person or an agent may say it.</summary>
    private const string Everyone = "everyone";

    private ChannelParts? _parts;

    /// <summary>A line's own channel and the messages to one part.</summary>
    public ChannelParts Parts => _parts ??= new(this, store, ledger, facts);

    /// <summary>The agents outside Daoris on a channel, each a member of its own.</summary>
    public ChannelMembers Members => new(this, store, asks);

    /// <summary>The members a channel holds now and whether their channel's claims have expired.</summary>
    public ChannelMemberRoll Roll => new(this, store, asks, ledger);

    /// <summary>A teammate's ask's channel, which the person and a session here on it post to and read.</summary>
    private TeammateAskChannel Teammates => new(store, StaleAsync);

    /// <summary>A line's own channel as the person reads it.</summary>
    public LineChannel Line => new(this, store);

    /// <summary>Post as the session the connector names: its record gives the ask (through its entry), its repository and its adapter.</summary>
    /// <param name="urgent">
    /// The poster's level: urgent asks the session's next step, ordinary (the default) a natural point. Kept
    /// urgent for at most <see cref="ChannelBounds.UrgentPerPart"/> of the part's posts; the outcome's message says what it kept.
    /// </param>
    /// <remarks>
    /// A line on no ask posts on its own channel, and a <paramref name="to"/> naming a session is to that part alone (<see cref="ChannelParts"/>).
    /// A line on a teammate's ask posts on that ask's channel, which the sync carries to its holder (<see cref="TeammateAskChannel"/>).
    /// </remarks>
    public async Task<ChannelOutcome> PostAsSessionAsync(
        string? sessionId, string? text, string? to, string? re, bool question, DateTimeOffset now, bool urgent = false,
        CancellationToken ct = default)
    {
        var (refusal, ask, session, entry, origin) = await SpeakerAsync(sessionId, ct).ConfigureAwait(false);
        var elsewhere = TeammateAsk(refusal, origin);
        if (refusal != ChannelRefusal.None && elsewhere is null) return new(refusal, null);

        var (part, partRefusal) = await Parts.NamedAsync(to, entry, session, ct).ConfigureAwait(false);
        if (partRefusal != ChannelRefusal.None) return new(partRefusal, null);
        if (part is null && elsewhere is not null)
        {
            return await Teammates.PostAsSessionAsync(session!, entry!, elsewhere, text, to, re, question, urgent, now, ct).ConfigureAwait(false);
        }

        if (part is not null || ask is null) return await Parts.PostAsync(session!, entry!, origin!, ask, part, text, to, re, question, urgent, now, ct).ConfigureAwait(false);

        var judged = await JudgeAsync(ask, text, to, re, ct).ConfigureAwait(false);
        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);

        var message = new ChannelMessage(
            NewId(), ask.Id, ask.Workspace, now, session!.Id, session.Repository, session.Door, entry,
            judged.To, judged.Re, question, judged.Text!) { Urgent = urgent };
        return await store.KeepAsync(message, ChannelBounds.MaxPerPart, ct).ConfigureAwait(false) is { } kept
            ? new(ChannelRefusal.None, kept)
            : new(ChannelRefusal.TooMany, null);
    }

    /// <summary>
    /// Post as the person: kept on the channel by no session, and on the ask as a word of the <c>channel</c> kind,
    /// which an older build passes over as it does any kind it does not know. A person has no cap.
    /// </summary>
    /// <param name="urgent">Their level: urgent unless they asked ordinary, their default as their words' is.</param>
    /// <remarks>On a teammate's ask, one whose messages the sync mirrored here, the post is kept to travel to its holder.</remarks>
    public async Task<ChannelOutcome> PostAsPersonAsync(
        string askId, string? text, string? to, string? re, DateTimeOffset now, CancellationToken ct = default, bool urgent = true)
    {
        if (await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } ask)
        {
            var id = askId.TrimStart('#');
            var (elsewhere, refused) = await Parts.NamedAsync(to, null, null, ct, SessionEntries.AskOrigin(id)).ConfigureAwait(false);
            if (refused != ChannelRefusal.None) return new(refused, null);
            if (elsewhere is not null) return new(ChannelRefusal.BadPart, null);
            return await Teammates.PostAsPersonAsync(id, text, to, re, urgent, now, ct).ConfigureAwait(false);
        }

        var (part, partRefusal) = await Parts.NamedAsync(to, null, null, ct, SessionEntries.AskOrigin(ask.Id)).ConfigureAwait(false);
        if (partRefusal != ChannelRefusal.None) return new(partRefusal, null);
        if (part is not null) return await Parts.PostAsPersonAsync(ask, part, text, re, urgent, now, ct).ConfigureAwait(false);

        var judged = await JudgeAsync(ask, text, to, re, ct).ConfigureAwait(false);
        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);

        var message = new ChannelMessage(
            NewId(), ask.Id, ask.Workspace, now, Session: null, Repository: null, Agent: null, Entry: null, judged.To, judged.Re,
            Question: false, judged.Text!) { Urgent = urgent };
        await store.AddAsync(message, int.MaxValue, ct).ConfigureAwait(false);
        await askStore.RecordWordAsync(ask.Id, new AskWord(AskWordKind.Channel, judged.Text!, now), now, ct).ConfigureAwait(false);
        return new(ChannelRefusal.None, message);
    }

    /// <summary>
    /// What the channel holds for <paramref name="entry"/>: every message and line of it, or those after <paramref name="since"/>. The
    /// unread count is against the entry's mark. <paramref name="moveMark"/> moves the mark to the newest item, as a session's read does.
    /// Null when this machine holds no such ask and has mirrored no message of one. An ask held elsewhere reads from the
    /// messages the sync mirrored: this read moves no mark there (the person's moves at <see cref="MarkReadAsync"/>), and
    /// Daoris's lines, which come from this machine's quests, are not in it. The person's count is <see cref="Counts"/>'s.
    /// </summary>
    public async Task<ChannelRead?> ReadAsync(
        string askId, string entry, DateTimeOffset? since, bool moveMark, CancellationToken ct = default)
    {
        if (await asks.FindAsync(askId, ct).ConfigureAwait(false) is { } ask)
        {
            return await ReadOfAsync(ask, entry, since, moveMark, ct).ConfigureAwait(false);
        }

        var id = askId.TrimStart('#');
        var mirrored = await store.OfAsync(id, ct).ConfigureAwait(false);
        if (mirrored.Count == 0) return null;

        var mark = await store.MarkAsync(id, entry, ct).ConfigureAwait(false);
        var read = new ChannelRead(
            [.. mirrored.Where(m => since is null || m.At > since).OrderBy(m => m.At)], [],
            mirrored.Count(m => Counts(m, entry) && (mark is null || m.At > mark)), mark);
        // A part here on the ask is handed what its entry has not settled, so the look reads its acknowledgments as an ask's.
        return entry == PersonEntry ? read : read with { Acknowledged = await Teammates.AcknowledgedAsync(id, entry, ct).ConfigureAwait(false) };
    }

    /// <summary>
    /// The asks the mirrored messages name that this machine does not hold, newest message first. No ask record is here, so a
    /// title is not: the messages carry their circle, the machines they came from, and when. Unread is against the person's local mark,
    /// counted as <see cref="Counts"/> counts the person's; each is readable at <see cref="ReadAsync"/> by its id.
    /// </summary>
    public async Task<IReadOnlyList<ElsewhereAsk>> ElsewhereAsync(CancellationToken ct = default)
    {
        var named = await store.NamedAsksAsync(ct).ConfigureAwait(false);
        var listed = new List<ElsewhereAsk>();
        foreach (var group in named.GroupBy(row => row.Ask, StringComparer.Ordinal))
        {
            if (await asks.FindAsync(group.Key, ct).ConfigureAwait(false) is not null) continue;

            var mark = await store.MarkAsync(group.Key, PersonEntry, ct).ConfigureAwait(false);
            listed.Add(new ElsewhereAsk(
                group.Key, group.First().Workspace,
                [.. group.Select(row => row.Origin).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                group.Max(row => row.At), group.Count(),
                group.Count(row => (!row.ByPerson || row.Origin is not null) && (mark is null || row.At > mark))));
        }

        return [.. listed.OrderByDescending(ask => ask.Latest)];
    }

    /// <summary>
    /// Whether a message counts toward an entry's unread: for the person, only
    /// what an agent or a teammate wrote, never their own post on this machine; for a session's entry, every message. Daoris's lines
    /// count for a session's entry alone.
    /// </summary>
    private static bool Counts(ChannelMessage message, string entry) =>
        entry != PersonEntry || !message.ByPerson || message.Origin is not null;

    /// <summary>
    /// A session's read: what is new since its entry last read (or since <paramref name="since"/>, whatever the mark says), and the mark
    /// moves. The same speaker's rules as a post: a session on an ask, and no helper or reviewer.
    /// </summary>
    /// <remarks>
    /// 🔴 Read from the last read, it also returns each message for its entry
    /// that the entry has neither taken nor read, whatever its moment, so one mirrored late is still read. Each message for its entry
    /// that it returns, whole, is acknowledged <see cref="ChannelAcks.Read"/> before the answer reaches the agent: the agent's own call,
    /// which it makes again with <c>since</c> if the answer is lost. The mark then moves for display (<see cref="MoveEntryMarkAsync"/>).
    /// </remarks>
    public async Task<ChannelReadOutcome> ReadAsSessionAsync(
        string? sessionId, DateTimeOffset? since, CancellationToken ct = default, DateTimeOffset? now = null)
    {
        var (refusal, ask, session, speaking, origin) = await SpeakerAsync(sessionId, ct).ConfigureAwait(false);
        var elsewhere = TeammateAsk(refusal, origin);
        if (refusal != ChannelRefusal.None && elsewhere is null) return new(refusal, null, null);
        var entry = speaking!;

        var mine = await Parts.ReadAsync(session!, entry, origin!, since, now ?? DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        if (elsewhere is not null)
        {
            return await Teammates.ReadAsSessionAsync(session!, entry, origin!, elsewhere, since, mine, now ?? DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        }

        if (ask is null) return new(ChannelRefusal.None, null, new ChannelRead(mine, [], mine.Count), entry) { Origin = origin };

        var mark = await store.MarkAsync(ask.Id, entry, ct).ConfigureAwait(false);
        var from = since ?? mark;
        var messages = await store.OfAsync(ask.Id, ct).ConfigureAwait(false);
        var lines = await LinesAsync(ask, ct).ConfigureAwait(false);
        var acknowledged = await AcknowledgedAsync(ask.Id, entry, ct).ConfigureAwait(false);

        var unread = messages.Count(m => mark is null || m.At > mark) + lines.Count(l => mark is null || l.At > mark);
        List<ChannelMessage> returned = [.. messages.Where(m => from is null || m.At > from
            || (since is null && IsFor(m, entry) && !ChannelAcks.Settles(acknowledged.GetValueOrDefault(m.Id)?.State)))];
        foreach (var message in returned.Where(m => IsFor(m, entry)))
        {
            await store.AcknowledgeAsync(ask.Id, entry, message.Id, ChannelAcks.Read, session!.Id, now ?? DateTimeOffset.UtcNow, ct)
                .ConfigureAwait(false);
        }

        await MoveEntryMarkAsync(ask, entry, messages, lines, ct).ConfigureAwait(false);
        var read = new ChannelRead([.. returned.Concat(mine).OrderBy(m => m.At)], [.. lines.Where(l => from is null || l.At > from)], unread + mine.Count, mark)
        {
            Acknowledged = await AcknowledgedAsync(ask.Id, entry, ct).ConfigureAwait(false),
        };
        return new(ChannelRefusal.None, ask, read, entry) { Origin = origin };
    }

    /// <summary>
    /// An entry's acknowledgment of messages on an ask's channel, each by its
    /// id: a part's claim (<see cref="ChannelAcks.Taking"/>, one part's only), its letting go, a start's packet, the write and the
    /// door's word. The entry's mark then moves for display, to before the oldest message for it not yet sent, taken or read.
    /// </summary>
    /// <returns>The messages whose acknowledgment changed; null where this machine holds no such ask.</returns>
    public async Task<IReadOnlyList<string>?> AcknowledgeAsync(
        string askId, string entry, string? session, string state, IReadOnlyList<string> messages, DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } ask)
        {
            return await Teammates.AcknowledgeAsync(askId.TrimStart('#'), entry, session, state, messages, now, ct).ConfigureAwait(false);
        }

        if (!ChannelAcks.Known(state) || string.IsNullOrWhiteSpace(entry) || entry == PersonEntry) return [];

        var held = await AcknowledgedAsync(ask.Id, entry, ct).ConfigureAwait(false);
        var changed = new List<string>();
        foreach (var id in messages.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            bool moved;
            if (state == ChannelAcks.Taking)
            {
                if (string.IsNullOrWhiteSpace(session)) continue;
                // A claim left by a part that no longer runs (a restart cut it off) is the entry's next part's to take.
                var stale = held.GetValueOrDefault(id) is { State: ChannelAcks.Taking, Session: { } holder }
                            && holder != session && await StaleAsync(holder, ct).ConfigureAwait(false)
                    ? holder
                    : null;
                moved = await store.ClaimAsync(ask.Id, entry, id, session, stale, now, ct).ConfigureAwait(false);
            }
            else
            {
                moved = await store.AcknowledgeAsync(ask.Id, entry, id, state, session, now, ct).ConfigureAwait(false);
            }

            if (moved) changed.Add(id);
        }

        if (changed.Count > 0)
        {
            await MoveEntryMarkAsync(
                ask, entry, await store.OfAsync(ask.Id, ct).ConfigureAwait(false), await LinesAsync(ask, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false);
        }

        return changed;
    }

    /// <summary>
    /// The messages waiting for a session: for its entry on its ask's channel, and neither sent, taken nor read
    /// by it, with the ask they are on; and those to it, or on its line's own channel, its line has not settled. What the connector's
    /// answers name; none for a helper and a reviewer.
    /// </summary>
    public async Task<ChannelWaiting> WaitingForAsync(string? sessionId, CancellationToken ct = default)
    {
        var (refusal, ask, session, entry, origin) = await SpeakerAsync(sessionId, ct).ConfigureAwait(false);
        if (refusal != ChannelRefusal.None) return ChannelWaiting.None;

        var addressed = await Parts.UnsettledAsync(session!, entry!, origin!, ct).ConfigureAwait(false);
        if (ask is null) return new(null, origin, addressed);
        var acknowledged = await AcknowledgedAsync(ask.Id, entry!, ct).ConfigureAwait(false);
        var messages = await store.OfAsync(ask.Id, ct).ConfigureAwait(false);
        return new(ask, origin,
            [.. messages.Where(m => IsFor(m, entry!) && !ChannelAcks.Settles(acknowledged.GetValueOrDefault(m.Id)?.State)).Concat(addressed).OrderBy(m => m.At)]);
    }

    /// <summary>
    /// What each message reached, for each repository it addresses, by message id: a reply from that
    /// repository answered it; else its entry's acknowledgment; else it waits, or is not reached once the ask has no quest open and no
    /// part of that entry is here (§4.2's expiry, judged as it is read and never stored, so a quest opened again makes it wait again).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ChannelReceipt>>> ReceiptsAsync(
        Ask ask, IReadOnlyList<ChannelMessage> messages, CancellationToken ct = default)
    {
        var receipts = new Dictionary<string, IReadOnlyList<ChannelReceipt>>(StringComparer.Ordinal);
        if (messages.Count == 0) return receipts;

        var repositories = await RepositoriesAsync(ask, ct).ConfigureAwait(false);
        var open = (await QuestsOfAsync(ask, ct).ConfigureAwait(false)).Any(q => q.Status is QuestStatus.Open or QuestStatus.Taken);
        var here = open ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : await EntriesHereAsync(ask, ct).ConfigureAwait(false);
        var rows = await store.AcksOfAsync(ask.Id, ct).ConfigureAwait(false);
        // A claim whose part no longer runs holds nothing: the message waits for the entry's next part (the design §6).
        var stale = new HashSet<string>(StringComparer.Ordinal);
        foreach (var holder in rows.Where(row => row.Ack.State == ChannelAcks.Taking).Select(row => row.Ack.Session).OfType<string>().Distinct())
        {
            if (await StaleAsync(holder, ct).ConfigureAwait(false)) stale.Add(holder);
        }

        var acks = rows.Where(row => !(row.Ack.State == ChannelAcks.Taking && row.Ack.Session is { } holder && stale.Contains(holder)))
            .ToDictionary(row => (row.Entry.ToLowerInvariant(), row.Message), row => row.Ack);

        foreach (var message in messages)
        {
            IReadOnlyList<string> addressed = message.To is { } to
                ? [to]
                : [.. repositories.Where(r => !string.Equals(r, message.Repository, StringComparison.OrdinalIgnoreCase))];
            receipts[message.Id] = [.. addressed.Select(repository =>
            {
                var entry = $"{SessionEntries.AskOrigin(ask.Id)}/{repository}";
                if (messages.Any(m => m.Re == message.Id && string.Equals(m.Repository, repository, StringComparison.OrdinalIgnoreCase)))
                {
                    return new ChannelReceipt(repository, ChannelReceipts.Answered);
                }

                var state = acks.GetValueOrDefault((entry.ToLowerInvariant(), message.Id))?.State switch
                {
                    ChannelAcks.Taking => ChannelReceipts.Held,
                    ChannelAcks.Sent => ChannelReceipts.Sent,
                    ChannelAcks.Taken => ChannelReceipts.Taken,
                    ChannelAcks.Read => ChannelReceipts.Read,
                    ChannelAcks.Partly => ChannelReceipts.Partly,
                    _ => open || here.Contains(entry) ? ChannelReceipts.Waiting : ChannelReceipts.NotReached,
                };
                return new ChannelReceipt(repository, state);
            })];
        }

        return receipts;
    }

    /// <summary>Remove an ask's channel with the ask, and the messages to one part on its origin.</summary>
    public async Task ForgetAsync(string askId, CancellationToken ct = default)
    {
        await store.ForgetAsync(askId.TrimStart('#'), ct).ConfigureAwait(false);
        await store.Addressed.ForgetAsync(SessionEntries.AskOrigin(askId.TrimStart('#')), ct).ConfigureAwait(false);
    }

    private async Task<ChannelRead> ReadOfAsync(Ask ask, string entry, DateTimeOffset? from, bool moveMark, CancellationToken ct)
    {
        var mark = await store.MarkAsync(ask.Id, entry, ct).ConfigureAwait(false);
        var messages = await store.OfAsync(ask.Id, ct).ConfigureAwait(false);
        var lines = await LinesAsync(ask, ct).ConfigureAwait(false);

        var newest = messages.Select(m => m.At).Concat(lines.Select(l => l.At)).DefaultIfEmpty().Max();
        // The person's count passes over their own posts and Daoris's lines; the mark still moves through every item.
        var unread = messages.Count(m => Counts(m, entry) && (mark is null || m.At > mark))
                     + (entry == PersonEntry ? 0 : lines.Count(l => mark is null || l.At > mark));
        if (moveMark && (messages.Count > 0 || lines.Count > 0)) await store.MoveMarkAsync(ask.Id, entry, newest, ct).ConfigureAwait(false);

        var read = new ChannelRead(
            [.. messages.Where(m => from is null || m.At > from)], [.. lines.Where(l => from is null || l.At > from)], unread, mark);
        // The person reads what each message reached; an entry reads what it acknowledged, by message id, a claim whose part
        // no longer runs read as none, so the entry's next part is handed it. The person reads the messages to one part each beside them.
        if (entry != PersonEntry)
        {
            return read with { Acknowledged = await LiveAsync(await AcknowledgedAsync(ask.Id, entry, ct).ConfigureAwait(false), ct).ConfigureAwait(false) };
        }

        var origin = SessionEntries.AskOrigin(ask.Id);
        var addressed = await store.Addressed.OfAsync(origin, ct).ConfigureAwait(false);
        return read with
        {
            Receipts = await ReceiptsAsync(ask, messages, ct).ConfigureAwait(false),
            Addressed = [.. addressed.Where(m => from is null || m.At > from)],
            AddressedReceipts = await Parts.ReceiptsAsync(origin, addressed, ct).ConfigureAwait(false),
        };
    }

    /// <summary>The acknowledgments with each claim whose holder no longer runs left out (the design §6: a restart's claim is the next part's).</summary>
    private async Task<IReadOnlyDictionary<string, ChannelAck>> LiveAsync(IReadOnlyDictionary<string, ChannelAck> acknowledged, CancellationToken ct)
    {
        var stale = new HashSet<string>(StringComparer.Ordinal);
        foreach (var holder in acknowledged.Values.Where(ack => ack.State == ChannelAcks.Taking).Select(ack => ack.Session).OfType<string>().Distinct())
        {
            if (await StaleAsync(holder, ct).ConfigureAwait(false)) stale.Add(holder);
        }

        return stale.Count == 0
            ? acknowledged
            : acknowledged.Where(pair => !(pair.Value.State == ChannelAcks.Taking && pair.Value.Session is { } holder && stale.Contains(holder)))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    /// <summary>An entry's acknowledgments on an ask's channel, by message id.</summary>
    private async Task<IReadOnlyDictionary<string, ChannelAck>> AcknowledgedAsync(string ask, string entry, CancellationToken ct) =>
        (await store.AcksOfAsync(ask, ct).ConfigureAwait(false))
            .Where(row => string.Equals(row.Entry, entry, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(row => row.Message, row => row.Ack, StringComparer.Ordinal);

    /// <summary>
    /// The entry's mark, kept for display and for a driver from before: moved to the newest item before the
    /// oldest message for the entry it has not yet had sent, taken or read, or to the newest item where none waits. Never back.
    /// </summary>
    private async Task MoveEntryMarkAsync(
        Ask ask, string entry, IReadOnlyList<ChannelMessage> messages, IReadOnlyList<ChannelLine> lines, CancellationToken ct)
    {
        var acknowledged = await AcknowledgedAsync(ask.Id, entry, ct).ConfigureAwait(false);
        var oldest = messages.Where(m => IsFor(m, entry) && !ChannelAcks.Settles(acknowledged.GetValueOrDefault(m.Id)?.State))
            .OrderBy(m => m.At).FirstOrDefault();
        var before = messages.Select(m => m.At).Concat(lines.Select(l => l.At)).Where(at => oldest is null || at < oldest.At).ToList();
        if (before.Count > 0) await store.MoveMarkAsync(ask.Id, entry, before.Max(), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether a message is for an entry (the driver's <c>ChannelEntry.IsFor</c>'s twin): addressed to its repository or to everyone,
    /// and posted by no part of the entry itself.
    /// </summary>
    private static bool IsFor(ChannelMessage message, string entry)
    {
        var repository = entry.IndexOf('/') is > 0 and var slash ? entry[(slash + 1)..] : "";
        return (message.To is null || string.Equals(message.To, repository, StringComparison.OrdinalIgnoreCase))
               && !string.Equals(message.Entry, entry, StringComparison.Ordinal);
    }

    /// <summary>Whether a claim's holder no longer runs: its record ended or parked, its inbox gone with its run.</summary>
    internal async Task<bool> StaleAsync(string holder, CancellationToken ct) =>
        await ledger.FindAsync(holder, ct).ConfigureAwait(false) is { } record
        && record.State is not (SessionState.Queued or SessionState.Starting or SessionState.Working);

    /// <summary>The entries on the ask with a part of this machine's still active: queued, starting, working or parked.</summary>
    private async Task<HashSet<string>> EntriesHereAsync(Ask ask, CancellationToken ct)
    {
        var prefix = SessionEntries.AskOrigin(ask.Id) + "/";
        var active = await ledger.ListAsync(includeClosed: false, ct: ct).ConfigureAwait(false);
        return new HashSet<string>(
            (await ledger.WithEntriesAsync(active.Where(s => s.Origin is null), ct).ConfigureAwait(false))
                .Select(s => s.Entry)
                .OfType<string>()
                .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Move an entry's mark to <paramref name="through"/>, or to the newest item when none is given (the driver moves a part's mark when
    /// the part starts, to the moment it handed the channel over), and answer the channel as it then reads. Null when no such ask.
    /// </summary>
    /// <remarks>
    /// The person's mark is kept on a teammate's ask too, one this machine holds
    /// no record of and whose messages the sync mirrored, moved to <paramref name="through"/> or to the newest mirrored message. The
    /// person's entry alone: a session's entry there is null, as an ask nowhere is. The mark is this machine's, never on the sync wire.
    /// </remarks>
    public async Task<ChannelRead?> MarkReadAsync(string askId, string entry, DateTimeOffset? through, CancellationToken ct = default)
    {
        if (await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } ask)
        {
            if (entry != PersonEntry) return null;
            var id = askId.TrimStart('#');
            var mirrored = await store.OfAsync(id, ct).ConfigureAwait(false);
            if (mirrored.Count == 0) return null;

            await store.MoveMarkAsync(id, entry, through ?? mirrored.Max(m => m.At), ct).ConfigureAwait(false);
            return await ReadAsync(id, entry, since: null, moveMark: false, ct).ConfigureAwait(false);
        }

        if (through is { } moment) await store.MoveMarkAsync(ask.Id, entry, moment, ct).ConfigureAwait(false);
        else await ReadOfAsync(ask, entry, from: null, moveMark: true, ct).ConfigureAwait(false);

        return await ReadOfAsync(ask, entry, from: null, moveMark: false, ct).ConfigureAwait(false);
    }

    /// <summary>A refusal's code, as a door answers it.</summary>
    public static string Spell(ChannelRefusal refusal) => refusal switch
    {
        ChannelRefusal.TooLong => "too-long",
        ChannelRefusal.NotFound => "not-found",
        ChannelRefusal.NoSession => "no-session",
        ChannelRefusal.NoAsk => "no-ask",
        ChannelRefusal.BadTo => "bad-to",
        ChannelRefusal.BadRe => "bad-re",
        ChannelRefusal.TooMany => "too-many",
        ChannelRefusal.BadPart => "bad-part",
        ChannelRefusal.PartElsewhere => "part-elsewhere",
        ChannelRefusal.NotMember => "not-member",
        _ => refusal.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// Daoris's lines for the ask (design §2.2), read from its quests as they stand and never stored: each quest asked by the ask or
    /// published by a session on it, published, and where it has moved, taken, done, declined, or done and awaiting the person's yes.
    /// </summary>
    internal async Task<IReadOnlyList<ChannelLine>> LinesAsync(Ask ask, CancellationToken ct)
    {
        var lines = new List<ChannelLine>();
        foreach (var quest in await QuestsOfAsync(ask, ct).ConfigureAwait(false))
        {
            lines.Add(new ChannelLine("published", quest.Id, quest.Name, quest.From, quest.To, quest.Filed));
            var code = quest.Status switch
            {
                QuestStatus.Taken => "taken",
                QuestStatus.Done when quest.Held || quest.Left => "awaiting",
                QuestStatus.Done => "done",
                QuestStatus.Declined => "declined",
                _ => null,
            };
            if (code is not null) lines.Add(new ChannelLine(code, quest.Id, quest.Name, quest.From, quest.To, quest.Updated));
        }

        return [.. lines.OrderBy(line => line.At)];
    }

    /// <summary>The quests that belong to the ask: asked by it (chain steps included), or whose entry's origin is it.</summary>
    internal async Task<IReadOnlyList<Quest>> QuestsOfAsync(Ask ask, CancellationToken ct)
    {
        var origin = SessionEntries.AskOrigin(ask.Id);
        var held = new List<Quest>();
        foreach (var quest in await quests.ListAsync(includeClosed: true, workspace: ask.Workspace, ct: ct).ConfigureAwait(false))
        {
            if (AskDesk.AskOf(quest.From) == ask.Id
                || await SessionEntries.OriginOfQuestAsync(quest, facts, ct).ConfigureAwait(false) == origin)
            {
                held.Add(quest);
            }
        }

        return held;
    }

    /// <summary>
    /// The session that speaks, its entry, the origin whose channel it speaks on, and the ask that origin names where it names one; or why it
    /// may not. Every origin has a channel: only a helper and a reviewer have none. One whose ask is not held here is answered
    /// <see cref="ChannelRefusal.NoAsk"/> with its origin, which the post and the read take as a teammate's ask.
    /// </summary>
    internal async Task<(ChannelRefusal Refusal, Ask? Ask, Session? Session, string? Entry, string? Origin)> SpeakerAsync(string? sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || await ledger.FindAsync(sessionId.Trim(), ct).ConfigureAwait(false) is not { } session)
        {
            return (ChannelRefusal.NoSession, null, null, null, null);
        }

        if (session.Serves is not null || session.Opinion is not null) return (ChannelRefusal.Helper, null, null, null, null);

        var key = (await ledger.WithEntryAsync(session, ct).ConfigureAwait(false)).Entry ?? "";
        var origin = key.IndexOf('/') is > 0 and var slash ? key[..slash] : key;
        if (origin.Length == 0) return (ChannelRefusal.NoAsk, null, session, key, null);
        if (AskOfOrigin(origin) is not { } askId) return (ChannelRefusal.None, null, session, key, origin);
        return await asks.FindAsync(askId, ct).ConfigureAwait(false) is { } ask
            ? (ChannelRefusal.None, ask, session, key, origin)
            : (ChannelRefusal.NoAsk, null, session, key, origin);
    }

    /// <summary>The teammate's ask a speaker refused <see cref="ChannelRefusal.NoAsk"/> is on, its origin naming an ask not held here; else null.</summary>
    private static string? TeammateAsk(ChannelRefusal refusal, string? origin) =>
        refusal == ChannelRefusal.NoAsk && origin is not null ? AskOfOrigin(origin) : null;

    /// <summary>The ask an origin names (<c>ask:&lt;id&gt;</c>), or null for any other origin.</summary>
    public static string? AskOfOrigin(string origin)
    {
        var prefix = SessionEntries.AskOrigin("");
        return origin.StartsWith(prefix, StringComparison.Ordinal) && origin.Length > prefix.Length ? origin[prefix.Length..] : null;
    }

    /// <summary>The words trimmed and bounded, and where they are addressed and what they answer, each as the ask's channel holds it.</summary>
    internal async Task<(ChannelRefusal Refusal, string? Text, string? To, string? Re)> JudgeAsync(
        Ask ask, string? text, string? to, string? re, CancellationToken ct)
    {
        var words = text?.Trim() ?? "";
        if (words.Length == 0) return (ChannelRefusal.Empty, null, null, null);
        if (words.Length > ChannelBounds.MaxText) return (ChannelRefusal.TooLong, null, null, null);

        string? address = null;
        var named = to?.Trim().TrimStart('#') ?? "";
        if (named.Length > 0 && !named.Equals(Everyone, StringComparison.OrdinalIgnoreCase))
        {
            address = (await RepositoriesAsync(ask, ct).ConfigureAwait(false))
                .FirstOrDefault(repository => repository.Equals(named, StringComparison.OrdinalIgnoreCase));
            if (address is null) return (ChannelRefusal.BadTo, null, null, null);
        }

        string? answers = null;
        if (!string.IsNullOrWhiteSpace(re))
        {
            answers = (await store.OfAsync(ask.Id, ct).ConfigureAwait(false)).FirstOrDefault(m => m.Id == re.Trim())?.Id;
            if (answers is null) return (ChannelRefusal.BadRe, null, null, null);
        }

        return (ChannelRefusal.None, words, address, answers);
    }

    /// <summary>The repositories the ask reaches: every receiver and every repository asker among its quests.</summary>
    private async Task<IReadOnlyList<string>> RepositoriesAsync(Ask ask, CancellationToken ct)
    {
        var names = new List<string>();
        foreach (var quest in await QuestsOfAsync(ask, ct).ConfigureAwait(false))
        {
            names.Add(quest.To);
            if (AskDesk.AskOf(quest.From) is null) names.Add(quest.From);
        }

        return [.. names.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
