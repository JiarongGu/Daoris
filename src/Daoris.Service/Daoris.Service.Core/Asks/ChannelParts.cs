using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>What waits for a session: the ask it is on where it is on one, the origin whose channel it is, and the messages.</summary>
public sealed record ChannelWaiting(Ask? Ask, string? Origin, IReadOnlyList<ChannelMessage> Waiting)
{
    public static ChannelWaiting None { get; } = new(null, null, []);
}

/// <summary>An addressed message for one part as the driver reads it: the message, and what that part's line acknowledged of it.</summary>
public sealed record AddressedFor(ChannelMessage Message, ChannelAck? Ack);

/// <summary>
/// A line's own channel and the messages to one part, kept in <c>channel_addressed</c> beside the ask's <c>channel</c>. Older builds read
/// <c>channel</c> and the sync carries it, so nothing here reaches an audience it was not for, and nothing here travels. Each part's
/// receipt is its own row, by the session that wrote it and its entry.
/// </summary>
public sealed class ChannelAddressedStore
{
    private readonly ConnectionGate _db;

    internal ChannelAddressedStore(ConnectionGate db) => _db = db;

    /// <summary>The two tables, and the channel's revision moved by their messages as by every other. Called inside the gate.</summary>
    internal static async Task EnsureSchemaAsync(ConnectionGate db, CancellationToken ct)
    {
        await using var command = db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS channel_addressed (
              seq        INTEGER PRIMARY KEY AUTOINCREMENT,
              id         TEXT NOT NULL UNIQUE,
              origin     TEXT NOT NULL,
              workspace  TEXT NOT NULL,
              at         TEXT NOT NULL,
              session    TEXT NULL,
              repository TEXT NULL,
              agent      TEXT NULL,
              entry      TEXT NULL,
              to_session TEXT NULL,
              to_repo    TEXT NULL,
              re         TEXT NULL,
              question   INTEGER NOT NULL DEFAULT 0,
              text       TEXT NOT NULL,
              urgent     INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS channel_addressed_origin ON channel_addressed (origin, seq);
            CREATE INDEX IF NOT EXISTS channel_addressed_session ON channel_addressed (session);
            CREATE TABLE IF NOT EXISTS channel_addressed_acks (
              message TEXT NOT NULL,
              session TEXT NOT NULL,
              entry   TEXT NULL,
              state   TEXT NOT NULL,
              at      TEXT NOT NULL,
              PRIMARY KEY (message, session)
            );
            CREATE TRIGGER IF NOT EXISTS channel_addressed_revision_kept AFTER INSERT ON channel_addressed
            BEGIN
              UPDATE channel_revision SET revision = revision + 1 WHERE one = 1;
            END;
            CREATE TRIGGER IF NOT EXISTS channel_addressed_revision_forgotten AFTER DELETE ON channel_addressed
            BEGIN
              UPDATE channel_revision SET revision = revision + 1 WHERE one = 1;
            END;
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Keep a message, unless its session has posted <paramref name="perPart"/> already across both tables; its level decided in the same
    /// statement, as an ask's message's is, so two posts at once cannot both pass a bound. A message of no session has no cap.
    /// </summary>
    /// <returns>The message as kept; null when the cap refused it, and nothing was kept.</returns>
    public Task<ChannelMessage?> KeepAsync(
        ChannelMessage message, int perPart, CancellationToken ct = default, int urgentPerPart = ChannelBounds.UrgentPerPart) =>
        _db.InTransactionAsync<ChannelMessage?>(async (transaction, inside) =>
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO channel_addressed (id, origin, workspace, at, session, repository, agent, entry, to_session, to_repo, re, question, text, urgent)
            SELECT $id, $origin, $workspace, $at, $session, $repository, $agent, $entry, $part, $to, $re, $question, $text,
              CASE WHEN $urgent = 1 AND $session IS NOT NULL
                        AND (SELECT COUNT(*) FROM channel WHERE session = $session AND urgent = 1)
                          + (SELECT COUNT(*) FROM channel_addressed WHERE session = $session AND urgent = 1) >= $urgentCap THEN 0
                   ELSE $urgent END
            WHERE $session IS NULL
               OR (SELECT COUNT(*) FROM channel WHERE session = $session) + (SELECT COUNT(*) FROM channel_addressed WHERE session = $session) < $cap
            RETURNING urgent
            """;
        command.Parameters.AddWithValue("$id", message.Id);
        command.Parameters.AddWithValue("$origin", message.Channel ?? throw new ArgumentException("an addressed message names its channel", nameof(message)));
        command.Parameters.AddWithValue("$workspace", message.Workspace);
        command.Parameters.AddWithValue("$at", Moments.Write(message.At));
        command.Parameters.AddWithValue("$session", (object?)message.Session ?? DBNull.Value);
        command.Parameters.AddWithValue("$repository", (object?)message.Repository ?? DBNull.Value);
        command.Parameters.AddWithValue("$agent", (object?)message.Agent ?? DBNull.Value);
        command.Parameters.AddWithValue("$entry", (object?)message.Entry ?? DBNull.Value);
        command.Parameters.AddWithValue("$part", (object?)message.Part ?? DBNull.Value);
        command.Parameters.AddWithValue("$to", (object?)message.To ?? DBNull.Value);
        command.Parameters.AddWithValue("$re", (object?)message.Re ?? DBNull.Value);
        command.Parameters.AddWithValue("$question", message.Question ? 1 : 0);
        command.Parameters.AddWithValue("$text", message.Text);
        command.Parameters.AddWithValue("$urgent", ChannelStore.Level(message.Urgent));
        command.Parameters.AddWithValue("$cap", perPart);
        command.Parameters.AddWithValue("$urgentCap", urgentPerPart);
        await using var reader = await command.ExecuteReaderAsync(inside).ConfigureAwait(false);
        if (!await reader.ReadAsync(inside).ConfigureAwait(false)) return null;
        return message with { Urgent = reader.IsDBNull(0) ? null : reader.GetInt32(0) != 0 };
    }, ct);

    /// <summary>The messages on <paramref name="origin"/>'s channel, oldest arrival first, a question's openness read from its answers.</summary>
    public Task<IReadOnlyList<ChannelMessage>> OfAsync(string origin, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<ChannelMessage>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM channel_addressed WHERE origin = $origin ORDER BY seq";
        command.Parameters.AddWithValue("$origin", origin);
        var read = new List<ChannelMessage>();
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) read.Add(Read(reader));
        }

        var answered = read.Where(m => m.Re is not null).Select(m => m.Re!).ToHashSet(StringComparer.Ordinal);
        return [.. read.Select(m => m with { Open = m.Question && !answered.Contains(m.Id) })];
    }, ct);

    /// <summary>Every part's receipt of the messages on <paramref name="origin"/>'s channel: the message, the session, its entry and its ack.</summary>
    public Task<IReadOnlyList<(string Message, string Session, string? Entry, ChannelAck Ack)>> AcksAsync(string origin, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<(string Message, string Session, string? Entry, ChannelAck Ack)>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT a.message, a.session, a.entry, a.state, a.at FROM channel_addressed_acks a
            JOIN channel_addressed m ON m.id = a.message WHERE m.origin = $origin
            """;
        command.Parameters.AddWithValue("$origin", origin);
        var read = new List<(string, string, string?, ChannelAck)>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            read.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                new ChannelAck(reader.GetString(3), reader.GetString(1), Moments.ReadRequired(reader.GetString(4)))));
        }

        return read;
    }, ct);

    /// <summary>
    /// One part's receipt of a message raised to <paramref name="state"/>, never lowered; <see cref="ChannelAcks.Released"/> lets its claim
    /// go. A claim is the part's own row, so another part of its line has its own.
    /// </summary>
    /// <returns>Whether the row changed.</returns>
    public Task<bool> AcknowledgeAsync(string message, string session, string? entry, string state, DateTimeOffset at, CancellationToken ct = default) =>
        _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = state == ChannelAcks.Released
            ? "DELETE FROM channel_addressed_acks WHERE message = $message AND session = $session AND state = 'taking'"
            : $"""
              INSERT INTO channel_addressed_acks (message, session, entry, state, at) VALUES ($message, $session, $entry, $state, $at)
              ON CONFLICT (message, session) DO UPDATE SET state = excluded.state, entry = COALESCE(excluded.entry, channel_addressed_acks.entry), at = excluded.at
              WHERE {ChannelStore.Rank("excluded.state")} > {ChannelStore.Rank("channel_addressed_acks.state")}
              """;
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$session", session);
        command.Parameters.AddWithValue("$entry", (object?)entry ?? DBNull.Value);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>Remove the messages on <paramref name="origin"/>'s channel with their receipts, as an ask's channel goes with it.</summary>
    public Task ForgetAsync(string origin, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            DELETE FROM channel_addressed_acks WHERE message IN (SELECT id FROM channel_addressed WHERE origin = $origin);
            DELETE FROM channel_addressed WHERE origin = $origin;
            """;
        command.Parameters.AddWithValue("$origin", origin);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    private static ChannelMessage Read(SqliteDataReader reader)
    {
        string Text(string column) => reader.GetString(reader.GetOrdinal(column));
        string? Maybe(string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));
        var urgent = reader.GetOrdinal("urgent");
        var origin = Text("origin");
        return new ChannelMessage(
            Text("id"), ChannelDesk.AskOfOrigin(origin) ?? "", Text("workspace"), Moments.ReadRequired(Text("at")), Maybe("session"),
            Maybe("repository"), Maybe("agent"), Maybe("entry"), Maybe("to_repo"), Maybe("re"), reader.GetInt32(reader.GetOrdinal("question")) != 0,
            Text("text"))
        {
            Urgent = reader.IsDBNull(urgent) ? null : reader.GetInt32(urgent) != 0,
            Channel = origin,
            Part = Maybe("to_session"),
        };
    }
}

/// <summary>
/// The judgement over a line's own channel and the messages to one part: who a <c>to</c> may name, which of them is for a reader, what each
/// reached. A part is named by its session and must be another part of the poster's own entry on this machine; a part of another line, or
/// a teammate's, is refused, never widened to everyone. A message to a part is for the head of that part's line as the say door walks it,
/// so a part that continues it after a hand-over takes it and an unrelated sibling does not.
/// </summary>
public sealed class ChannelParts(ChannelDesk desk, ChannelStore store, SessionLedger ledger, ISessionEntryFacts facts)
{
    private const string Everyone = "everyone";

    private ChannelAddressedStore Addressed => store.Addressed;

    /// <summary>
    /// The part a <paramref name="to"/> names where it names a session: refused <see cref="ChannelRefusal.PartElsewhere"/> for a teammate's,
    /// <see cref="ChannelRefusal.BadPart"/> for the poster itself, a helper's, a reviewer's or one whose entry is not
    /// <paramref name="entry"/> (or, with <paramref name="underOrigin"/>, not under that origin). Null with no refusal where it names none.
    /// </summary>
    public async Task<(Session? Part, ChannelRefusal Refusal)> NamedAsync(
        string? to, string? entry, Session? poster, CancellationToken ct, string? underOrigin = null)
    {
        var named = to?.Trim() ?? "";
        if (named.Length == 0 || named.Equals(Everyone, StringComparison.OrdinalIgnoreCase)) return (null, ChannelRefusal.None);
        // A teammate's mirror is read too, so a part that lives elsewhere is refused by name rather than read as a repository.
        if (await facts.SessionAsync(named, ct).ConfigureAwait(false) is not { } part) return (null, ChannelRefusal.None);
        if (part.Origin is not null) return (null, ChannelRefusal.PartElsewhere);
        if (part.Id == poster?.Id || part.Serves is not null || part.Opinion is not null) return (null, ChannelRefusal.BadPart);
        var key = (await ledger.WithEntryAsync(part, ct).ConfigureAwait(false)).Entry;
        var fits = underOrigin is null
            ? string.Equals(key, entry, StringComparison.Ordinal)
            : key?.StartsWith(underOrigin + "/", StringComparison.Ordinal) == true;
        return fits ? (part, ChannelRefusal.None) : (null, ChannelRefusal.BadPart);
    }

    /// <summary>A session's post on <paramref name="origin"/>'s channel, to one <paramref name="part"/> or, with none, to everyone on it or one of its repositories.</summary>
    public async Task<ChannelOutcome> PostAsync(
        Session session, string entry, string origin, Ask? ask, Session? part, string? text, string? to, string? re, bool question, bool urgent,
        DateTimeOffset now, CancellationToken ct)
    {
        var judged = await JudgeAsync(origin, ask, text, part is null ? to : null, re, ct).ConfigureAwait(false);
        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);
        var row = new ChannelMessage(
            NewId(), ask?.Id ?? "", ask?.Workspace ?? session.Workspace, now, session.Id, session.Repository, session.Door, entry,
            judged.To, judged.Re, question, judged.Text!) { Urgent = urgent, Channel = origin, Part = part?.Id };
        return await Addressed.KeepAsync(row, ChannelBounds.MaxPerPart, ct).ConfigureAwait(false) is { } kept
            ? new(ChannelRefusal.None, kept)
            : new(ChannelRefusal.TooMany, null);
    }

    /// <summary>
    /// The person's post to one part of a line of work on <paramref name="ask"/>: that part's alone, and never kept as the ask's word, since
    /// the ask's words reach every part of it.
    /// </summary>
    public async Task<ChannelOutcome> PostAsPersonAsync(Ask ask, Session part, string? text, string? re, bool urgent, DateTimeOffset now, CancellationToken ct)
    {
        var origin = SessionEntries.AskOrigin(ask.Id);
        var judged = await JudgeAsync(origin, ask, text, null, re, ct).ConfigureAwait(false);
        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);
        var row = new ChannelMessage(
            NewId(), ask.Id, ask.Workspace, now, Session: null, Repository: null, Agent: null, Entry: null, To: null, judged.Re,
            Question: false, judged.Text!) { Urgent = urgent, Channel = origin, Part = part.Id };
        await Addressed.KeepAsync(row, int.MaxValue, ct).ConfigureAwait(false);
        return new(ChannelRefusal.None, row);
    }

    /// <summary>
    /// The person's post on a line's own channel, to everyone on it or one of its repositories: their words, with no cap, kept apart from
    /// any ask's channel and never travelling.
    /// </summary>
    public async Task<ChannelOutcome> PostAsPersonOnLineAsync(
        string origin, string workspace, string? text, string? to, bool urgent, DateTimeOffset now, CancellationToken ct)
    {
        var judged = await JudgeAsync(origin, null, text, to, null, ct).ConfigureAwait(false);
        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);
        var row = new ChannelMessage(
            NewId(), "", workspace, now, Session: null, Repository: null, Agent: null, Entry: null, judged.To, Re: null, Question: false,
            judged.Text!) { Urgent = urgent, Channel = origin };
        await Addressed.KeepAsync(row, int.MaxValue, ct).ConfigureAwait(false);
        return new(ChannelRefusal.None, row);
    }

    /// <summary>
    /// What a session's own read returns: the messages for it its line has not settled, or with <paramref name="since"/> every one for it
    /// after that moment, each acknowledged read by this part before the answer reaches the agent.
    /// </summary>
    public async Task<IReadOnlyList<ChannelMessage>> ReadAsync(Session reader, string entry, string origin, DateTimeOffset? since, DateTimeOffset now, CancellationToken ct)
    {
        List<ChannelMessage> mine = [.. (await ForAsync(reader, entry, origin, ct).ConfigureAwait(false))
            .Where(each => since is null ? !ChannelAcks.Settles(each.Ack?.State) : each.Message.At > since).Select(each => each.Message)];
        foreach (var message in mine)
        {
            await Addressed.AcknowledgeAsync(message.Id, reader.Id, entry, ChannelAcks.Read, now, ct).ConfigureAwait(false);
        }

        return mine;
    }

    /// <summary>The messages its line has not settled, for what waits for a session.</summary>
    public async Task<IReadOnlyList<ChannelMessage>> UnsettledAsync(Session reader, string entry, string origin, CancellationToken ct) =>
        [.. (await ForAsync(reader, entry, origin, ct).ConfigureAwait(false)).Where(each => !ChannelAcks.Settles(each.Ack?.State)).Select(each => each.Message)];

    /// <summary>
    /// The driver's read of what waits for one part it runs, each with what its line acknowledged, a claim whose holder no longer runs read
    /// as none. The driver's call is how it says it reads these: one that never asks is handed none, and they wait for the part's own read.
    /// Null where no such session is held here, or it is a helper's or a reviewer's.
    /// </summary>
    public async Task<IReadOnlyList<AddressedFor>?> WaitingAsync(string session, CancellationToken ct = default)
    {
        var (refusal, _, reader, entry, origin) = await desk.SpeakerAsync(session, ct).ConfigureAwait(false);
        if (refusal is not (ChannelRefusal.None or ChannelRefusal.NoAsk) || reader is null || entry is null || origin is null) return null;
        var found = new List<AddressedFor>();
        foreach (var each in await ForAsync(reader, entry, origin, ct).ConfigureAwait(false))
        {
            var stale = each.Ack is { State: ChannelAcks.Taking, Session: { } holder } && holder != reader.Id && await desk.StaleAsync(holder, ct).ConfigureAwait(false);
            found.Add(stale ? each with { Ack = null } : each);
        }

        return found;
    }

    /// <summary>One part's acknowledgment of the messages to it by their ids, each raised, never lowered; only those for it are acknowledged.</summary>
    /// <returns>The messages whose acknowledgment changed; null where no such session is held here.</returns>
    public async Task<IReadOnlyList<string>?> AcknowledgeAsync(
        string session, string state, IReadOnlyList<string> messages, DateTimeOffset now, CancellationToken ct = default)
    {
        if (await WaitingAsync(session, ct).ConfigureAwait(false) is not { } waiting) return null;
        if (!ChannelAcks.Known(state)) return [];
        var entry = (await ledger.WithEntryAsync((await ledger.FindAsync(session, ct).ConfigureAwait(false))!, ct).ConfigureAwait(false)).Entry;
        var ours = waiting.Select(each => each.Message.Id).ToHashSet(StringComparer.Ordinal);
        var changed = new List<string>();
        foreach (var id in messages.Where(ours.Contains).Distinct(StringComparer.Ordinal))
        {
            if (await Addressed.AcknowledgeAsync(id, session, entry, state, now, ct).ConfigureAwait(false)) changed.Add(id);
        }

        return changed;
    }

    /// <summary>
    /// What each message to one part reached in that part's line: the best that any part of the line observed, else it waits. Never more
    /// than was observed.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> ReceiptsAsync(string origin, IReadOnlyList<ChannelMessage> addressed, CancellationToken ct)
    {
        var receipts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (addressed.Count == 0) return receipts;
        var acks = await Addressed.AcksAsync(origin, ct).ConfigureAwait(false);
        foreach (var message in addressed)
        {
            var line = message.Part is { } part && await ledger.FindAsync(part, ct).ConfigureAwait(false) is { } named
                ? (await ledger.WalkLineAsync(named, ct).ConfigureAwait(false)).Records.Select(record => record.Id).ToHashSet(StringComparer.Ordinal)
                : [];
            var best = acks.Where(each => each.Message == message.Id && line.Contains(each.Session)).Select(each => each.Ack.State)
                .OrderByDescending(Rank).FirstOrDefault();
            receipts[message.Id] = best switch
            {
                ChannelAcks.Taking => ChannelReceipts.Held,
                ChannelAcks.Sent => ChannelReceipts.Sent,
                ChannelAcks.Taken => ChannelReceipts.Taken,
                ChannelAcks.Read => ChannelReceipts.Read,
                _ => ChannelReceipts.Waiting,
            };
        }

        return receipts;
    }

    /// <summary>
    /// The messages for <paramref name="reader"/> on its origin's channel, oldest first, each with its best acknowledgment: one to a part where
    /// the reader is now the head of that part's line; one to everyone on a line's own channel, or to the reader's repository, posted by no
    /// part of the reader's own entry.
    /// </summary>
    private async Task<IReadOnlyList<AddressedFor>> ForAsync(Session reader, string entry, string origin, CancellationToken ct)
    {
        var rows = await Addressed.OfAsync(origin, ct).ConfigureAwait(false);
        if (rows.Count == 0) return [];
        var acks = await Addressed.AcksAsync(origin, ct).ConfigureAwait(false);
        var found = new List<AddressedFor>();
        foreach (var row in rows)
        {
            IEnumerable<ChannelAck> held;
            if (row.Part is { } part)
            {
                if (await ledger.FindAsync(part, ct).ConfigureAwait(false) is not { } named) continue;
                var walk = await ledger.WalkLineAsync(named, ct).ConfigureAwait(false);
                if (walk.Head?.Id != reader.Id) continue;
                var line = walk.Records.Select(record => record.Id).ToHashSet(StringComparer.Ordinal);
                held = acks.Where(each => each.Message == row.Id && line.Contains(each.Session)).Select(each => each.Ack);
            }
            else
            {
                if (string.Equals(row.Entry, entry, StringComparison.Ordinal)) continue;
                if (row.To is { } repository && !string.Equals(repository, reader.Repository, StringComparison.OrdinalIgnoreCase)) continue;
                held = acks.Where(each => each.Message == row.Id && string.Equals(each.Entry, entry, StringComparison.Ordinal)).Select(each => each.Ack);
            }

            found.Add(new AddressedFor(row, held.OrderByDescending(each => Rank(each.State)).FirstOrDefault()));
        }

        return found;
    }

    /// <summary>
    /// A message's words, its <c>to</c> and its <c>re</c> judged on <paramref name="origin"/>'s channel: <c>to</c> everyone or one repository
    /// whose line of work is on the origin; <c>re</c> a message of the origin's channel, either table.
    /// </summary>
    internal async Task<(ChannelRefusal Refusal, string? Text, string? To, string? Re)> JudgeAsync(
        string origin, Ask? ask, string? text, string? to, string? re, CancellationToken ct)
    {
        var words = text?.Trim() ?? "";
        if (words.Length == 0) return (ChannelRefusal.Empty, null, null, null);
        if (words.Length > ChannelBounds.MaxText) return (ChannelRefusal.TooLong, null, null, null);

        string? address = null;
        var named = to?.Trim().TrimStart('#') ?? "";
        if (named.Length > 0 && !named.Equals(Everyone, StringComparison.OrdinalIgnoreCase))
        {
            address = (await RepositoriesAsync(origin, ct).ConfigureAwait(false))
                .FirstOrDefault(repository => repository.Equals(named, StringComparison.OrdinalIgnoreCase));
            if (address is null) return (ChannelRefusal.BadTo, null, null, null);
        }

        string? answers = null;
        if (!string.IsNullOrWhiteSpace(re))
        {
            var id = re.Trim();
            answers = (await Addressed.OfAsync(origin, ct).ConfigureAwait(false)).FirstOrDefault(m => m.Id == id)?.Id
                      ?? (ask is null ? null : (await store.OfAsync(ask.Id, ct).ConfigureAwait(false)).FirstOrDefault(m => m.Id == id)?.Id);
            if (answers is null) return (ChannelRefusal.BadRe, null, null, null);
        }

        return (ChannelRefusal.None, words, address, answers);
    }

    /// <summary>The repositories whose lines of work this machine holds on <paramref name="origin"/>: what a line's own channel reaches.</summary>
    private async Task<IReadOnlyList<string>> RepositoriesAsync(string origin, CancellationToken ct)
    {
        var prefix = origin + "/";
        var held = await ledger.ListAsync(includeClosed: true, ct: ct).ConfigureAwait(false);
        return
        [
            .. (await ledger.WithEntriesAsync(held.Where(s => s.Origin is null), ct).ConfigureAwait(false))
                .Select(s => s.Entry).OfType<string>()
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(key => key[prefix.Length..])
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static int Rank(string state) => state switch
    {
        ChannelAcks.Partly => 0,
        ChannelAcks.Taking => 1,
        ChannelAcks.Sent => 2,
        ChannelAcks.Read => 3,
        ChannelAcks.Taken => 4,
        _ => -1,
    };

    /// <summary>Ten characters, so an addressed message's id is never one of an ask's channel's eight.</summary>
    internal static string NewId() => Guid.NewGuid().ToString("N")[..10];
}
