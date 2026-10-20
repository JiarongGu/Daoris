using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// The channel's bounds (SESSONE2a; the co-working design §2.6). First values, read again at the canary: a message's text, how many
/// messages one part posts, and how many of the newest a next part is handed.
/// </summary>
public static class ChannelBounds
{
    /// <summary>The most characters one message holds. A link travels as text, a file not at all in this version.</summary>
    public const int MaxText = 4000;

    /// <summary>The most messages one part (one session record) posts.</summary>
    public const int MaxPerPart = 10;

    /// <summary>The most messages a next part is handed on starting: the newest, within the instruction's share (SESSONE2b).</summary>
    public const int HandedNewest = 20;

    /// <summary>
    /// The most of one part's posts kept urgent (COWORK1d, D188 point 2; the message-urgency design §2.2, trap 10): a first value, a
    /// judgement, within the part's <see cref="MaxPerPart"/>. A further urgent post is kept ordinary, and its poster told so.
    /// </summary>
    public const int UrgentPerPart = 3;
}

/// <summary>Why the channel did not do what was asked of it, by a code: a tool says it in a sentence, a door answers the code.</summary>
public enum ChannelRefusal
{
    None,

    /// <summary>No words, or only whitespace.</summary>
    Empty,

    /// <summary>Words past <see cref="ChannelBounds.MaxText"/> characters.</summary>
    TooLong,

    /// <summary>No ask under that id on this machine.</summary>
    NotFound,

    /// <summary>The connector names no session this machine holds.</summary>
    NoSession,

    /// <summary>The session is on no ask, so it has no channel: work or a request goes in a quest.</summary>
    NoAsk,

    /// <summary>A helper or a reviewer: each answers one question, and neither posts nor reads (D155, D163).</summary>
    Helper,

    /// <summary>A <c>to</c> that is not one of the ask's repositories.</summary>
    BadTo,

    /// <summary>A <c>re</c> the ask's channel does not hold.</summary>
    BadRe,

    /// <summary>The part has posted <see cref="ChannelBounds.MaxPerPart"/> messages already.</summary>
    TooMany,

    /// <summary>The ask is a teammate's, held on another machine and only read here (SESSONE2g): a post to it is theirs to take.</summary>
    HeldElsewhere,
}

/// <summary>What the channel shows in time order: a message, or one of Daoris's lines.</summary>
public interface IChannelItem
{
    /// <summary>When it was posted, or when the quest reached the state the line says.</summary>
    DateTimeOffset At { get; }
}

/// <summary>
/// One message on an ask's channel (SESSONE2a). Kept as the record shape that can travel (SESSONE2d): an id the poster's machine made
/// with <see cref="Origin"/> naming the machine it came from where it was not made here, and words that carry no machine path.
/// </summary>
/// <param name="Id">Short and stable; <c>origin/id</c> across machines.</param>
/// <param name="Ask">The ask it is on.</param>
/// <param name="Workspace">The ask's circle, which decides where the record travels.</param>
/// <param name="At">When it was posted.</param>
/// <param name="Session">The session that posted it; null for the person.</param>
/// <param name="Repository">The poster's repository, for a session.</param>
/// <param name="Agent">The poster's harness adapter, a name and never a model (D24), for a session.</param>
/// <param name="Entry">The poster's entry (D167 point 2), for a session: its read mark's key.</param>
/// <param name="To">The one repository it is addressed to; null for everyone.</param>
/// <param name="Re">The message it answers, where it answers one.</param>
/// <param name="Question">Whether its poster marked it a question.</param>
/// <param name="Text">The words as written, at most <see cref="ChannelBounds.MaxText"/> characters.</param>
/// <param name="Origin">The machine that posted it, where not this one; null for this machine's own.</param>
public sealed record ChannelMessage(
    string Id, string Ask, string Workspace, DateTimeOffset At, string? Session, string? Repository, string? Agent,
    string? Entry, string? To, string? Re, bool Question, string Text, string? Origin = null) : IChannelItem
{
    /// <summary>Whether the person wrote it: no session did. A session's message is that agent's claim, never the person's.</summary>
    public bool ByPerson => Session is null;

    /// <summary>A question no message has answered yet; false for every other message. Read from the channel, never stored.</summary>
    public bool Open { get; init; }

    /// <summary>
    /// When its sender asked it to reach a working session (COWORK1d, D188 point 2; the message-urgency design §9): true, urgent, at
    /// the session's next step where its door takes words then; false, ordinary, at a natural point. Said on every message kept from
    /// COWORK1d on, decided in the insert's own statement (<see cref="ChannelBounds.UrgentPerPart"/>). Null only on a message from
    /// before, or from an older teammate's machine, which keeps its old reach; null never means ordinary.
    /// </summary>
    public bool? Urgent { get; init; }
}

/// <summary>
/// One of Daoris's lines (SESSONE2a; design §2.2): a quest of the ask in the state it stands in, read from the quest and never stored.
/// Coded, so the page words it in the reader's language (D142).
/// </summary>
/// <param name="Code"><c>published</c>, <c>taken</c>, <c>done</c>, <c>declined</c>, or <c>awaiting</c>: done, waiting on the person's yes (D133 §4).</param>
/// <param name="Quest">The quest's id, the line's door.</param>
/// <param name="Name">What a list calls the quest, as its publisher wrote it.</param>
/// <param name="From">The asker: a repository, or <c>ask #id</c>.</param>
/// <param name="To">The repository asked.</param>
/// <param name="At">When the quest was published, or last moved.</param>
public sealed record ChannelLine(string Code, string Quest, string Name, string From, string To, DateTimeOffset At) : IChannelItem;

/// <summary>An ask's channel as one entry reads it.</summary>
/// <param name="Messages">The messages in the read, oldest first.</param>
/// <param name="Lines">Daoris's lines in the read, oldest first.</param>
/// <param name="Unread">How many of the channel's items are after this entry's mark, counted before a read moved it.</param>
/// <param name="Mark">The moment the entry had read up to before this read moved it; null when it never has.</param>
public sealed record ChannelRead(
    IReadOnlyList<ChannelMessage> Messages, IReadOnlyList<ChannelLine> Lines, int Unread, DateTimeOffset? Mark = null)
{
    /// <summary>Both kinds in time order, a line before a message of the same moment.</summary>
    public IReadOnlyList<IChannelItem> Items =>
        [.. Lines.Cast<IChannelItem>().Concat(Messages).OrderBy(item => item.At)];
}

/// <summary>An ask the mirrored messages name and this machine does not hold (SESSONE2h): all this machine knows of it.</summary>
/// <param name="Id">The ask's id.</param>
/// <param name="Workspace">Its circle, from its messages.</param>
/// <param name="Origins">The machines its messages came from, ordered; empty where only this machine's own are kept.</param>
/// <param name="Latest">The moment of its newest message.</param>
/// <param name="Messages">How many of its messages are kept.</param>
/// <param name="Unread">How many are after the person's local mark.</param>
public sealed record ElsewhereAsk(string Id, string Workspace, IReadOnlyList<string> Origins, DateTimeOffset Latest, int Messages, int Unread);

/// <summary>What a post did.</summary>
public sealed record ChannelOutcome(ChannelRefusal Refusal, ChannelMessage? Message);

/// <summary>What a session's read did: its refusal, or the ask it read and what it read.</summary>
public sealed record ChannelReadOutcome(ChannelRefusal Refusal, Ask? Ask, ChannelRead? Read);

/// <summary>
/// The messages of every ask's channel, and each entry's read mark (SESSONE2a; design §2.5). Machine-local service records beside the
/// asks they are on; Daoris's own lines are not here, they are read from the quests.
/// </summary>
public sealed class ChannelStore
{
    private readonly ConnectionGate _db;

    private ChannelStore(SqliteConnection connection) => _db = ConnectionGate.For(connection);

    public static Task<ChannelStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default) =>
        ConnectionGate.For(connection).RunAsync(() => new ChannelStore(connection).EnsureSchemaAsync(ct), ct);

    private async Task<ChannelStore> EnsureSchemaAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        // `seq` is this machine's arrival order; `id` is the record's own, which a feed will carry as `origin/id`. The marks are one per
        // (ask, entry): the person's is the entry `person`.
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS channel (
              seq        INTEGER PRIMARY KEY AUTOINCREMENT,
              id         TEXT NOT NULL,
              ask        TEXT NOT NULL,
              workspace  TEXT NOT NULL,
              at         TEXT NOT NULL,
              session    TEXT NULL,
              repository TEXT NULL,
              agent      TEXT NULL,
              entry      TEXT NULL,
              to_repo    TEXT NULL,
              re         TEXT NULL,
              question   INTEGER NOT NULL DEFAULT 0,
              text       TEXT NOT NULL,
              origin     TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS channel_id ON channel (COALESCE(origin, ''), id);
            CREATE INDEX IF NOT EXISTS channel_ask ON channel (ask, seq);
            CREATE INDEX IF NOT EXISTS channel_session ON channel (session);
            CREATE TABLE IF NOT EXISTS channel_marks (
              ask     TEXT NOT NULL,
              entry   TEXT NOT NULL,
              read_at TEXT NOT NULL,
              PRIMARY KEY (ask, entry)
            );
            -- SESSONE2d: how far this machine has pushed its own messages (by `seq`) and fetched the team's, per workspace.
            CREATE TABLE IF NOT EXISTS channel_cursor (
              workspace TEXT NOT NULL PRIMARY KEY,
              pushed    INTEGER NOT NULL DEFAULT 0,
              fetched   INTEGER NOT NULL DEFAULT 0
            );
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // PUSH1d1 (D182 point 5): the channel's revision, the change stream's `channel` family, one row moved by every insert,
        // update and delete of a message. The newest `seq` would not do: an ask's channel forgotten with it deletes older messages
        // and leaves the newest where it was. Kept by triggers, as the asks' is, so a connector built before them still moves it.
        // The read marks and the cursors move nothing: a mark is one reader's place, moved by a read and by the driver as a part
        // starts, and a cursor is the sync's own.
        await using var revision = _db.Command();
        revision.CommandText = """
            CREATE TABLE IF NOT EXISTS channel_revision (
              one      INTEGER PRIMARY KEY CHECK (one = 1),
              revision INTEGER NOT NULL
            );
            INSERT OR IGNORE INTO channel_revision (one, revision) VALUES (1, 0);

            CREATE TRIGGER IF NOT EXISTS channel_revision_kept AFTER INSERT ON channel
            BEGIN
              UPDATE channel_revision SET revision = revision + 1 WHERE one = 1;
            END;

            CREATE TRIGGER IF NOT EXISTS channel_revision_changed AFTER UPDATE ON channel
            BEGIN
              UPDATE channel_revision SET revision = revision + 1 WHERE one = 1;
            END;

            CREATE TRIGGER IF NOT EXISTS channel_revision_forgotten AFTER DELETE ON channel
            BEGIN
              UPDATE channel_revision SET revision = revision + 1 WHERE one = 1;
            END;
            """;
        await revision.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // COWORK1d (the message-urgency design §9): a message's level, said on every message kept from now; a message from before says
        // none, which keeps its old reach and is never read as ordinary.
        await SchemaColumns.EnsureAsync(_db, "channel", "urgent", "urgent INTEGER NULL", ct).ConfigureAwait(false);
        return this;
    }

    /// <summary>
    /// Keep a message, unless its session has posted <paramref name="perPart"/> already: one statement, so two posts at once cannot both
    /// pass the count. A message of no session (the person's) has no cap.
    /// </summary>
    /// <returns>False when the cap refused it, and nothing was kept.</returns>
    public async Task<bool> AddAsync(ChannelMessage message, int perPart, CancellationToken ct = default) =>
        await KeepAsync(message, perPart, ct).ConfigureAwait(false) is not null;

    /// <summary>
    /// <see cref="AddAsync"/>, answering the message as kept. Its level is decided in the same statement (COWORK1d, trap 10): a part's
    /// urgent post past <paramref name="urgentPerPart"/> of its urgent ones is kept ordinary, an explicit false, so two posts at once
    /// cannot both pass the bound; a message of no session has none.
    /// </summary>
    /// <returns>The message as kept, its <see cref="ChannelMessage.Urgent"/> as the statement decided; null when the cap refused it.</returns>
    public Task<ChannelMessage?> KeepAsync(
        ChannelMessage message, int perPart, CancellationToken ct = default, int urgentPerPart = ChannelBounds.UrgentPerPart) =>
        _db.RunAsync<ChannelMessage?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO channel (id, ask, workspace, at, session, repository, agent, entry, to_repo, re, question, text, origin, urgent)
            SELECT $id, $ask, $workspace, $at, $session, $repository, $agent, $entry, $to, $re, $question, $text, $origin,
              CASE WHEN $urgent = 1 AND $session IS NOT NULL
                        AND (SELECT COUNT(*) FROM channel WHERE session = $session AND urgent = 1) >= $urgentCap THEN 0
                   ELSE $urgent END
            WHERE $session IS NULL OR (SELECT COUNT(*) FROM channel WHERE session = $session) < $cap
            RETURNING urgent
            """;
        command.Parameters.AddWithValue("$id", message.Id);
        command.Parameters.AddWithValue("$ask", message.Ask);
        command.Parameters.AddWithValue("$workspace", message.Workspace);
        command.Parameters.AddWithValue("$at", Moments.Write(message.At));
        command.Parameters.AddWithValue("$session", (object?)message.Session ?? DBNull.Value);
        command.Parameters.AddWithValue("$repository", (object?)message.Repository ?? DBNull.Value);
        command.Parameters.AddWithValue("$agent", (object?)message.Agent ?? DBNull.Value);
        command.Parameters.AddWithValue("$entry", (object?)message.Entry ?? DBNull.Value);
        command.Parameters.AddWithValue("$to", (object?)message.To ?? DBNull.Value);
        command.Parameters.AddWithValue("$re", (object?)message.Re ?? DBNull.Value);
        command.Parameters.AddWithValue("$question", message.Question ? 1 : 0);
        command.Parameters.AddWithValue("$text", message.Text);
        command.Parameters.AddWithValue("$origin", (object?)message.Origin ?? DBNull.Value);
        command.Parameters.AddWithValue("$urgent", Level(message.Urgent));
        command.Parameters.AddWithValue("$cap", perPart);
        command.Parameters.AddWithValue("$urgentCap", urgentPerPart);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        return message with { Urgent = reader.IsDBNull(0) ? null : reader.GetInt32(0) != 0 };
    }, ct);

    /// <summary>The ask's messages, oldest arrival first; a question's <see cref="ChannelMessage.Open"/> is read from the answers among them.</summary>
    public Task<IReadOnlyList<ChannelMessage>> OfAsync(string ask, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<ChannelMessage>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM channel WHERE ask = $ask ORDER BY seq";
        command.Parameters.AddWithValue("$ask", ask);
        var read = new List<ChannelMessage>();
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) read.Add(Read(reader));
        }

        var answered = read.Where(m => m.Re is not null).Select(m => m.Re!).ToHashSet(StringComparer.Ordinal);
        return [.. read.Select(m => m with { Open = m.Question && !answered.Contains(m.Id) })];
    }, ct);

    /// <summary>
    /// This machine's OWN messages in a workspace after <paramref name="seq"/>, in arrival order, each with its number: what a push sends
    /// (SESSONE2d). A message fed down from a teammate is never in it. A message is never changed once kept, so the arrival number is
    /// a cursor with nothing to re-send.
    /// </summary>
    public Task<IReadOnlyList<(ChannelMessage Message, long Seq)>> OwnChangedSinceAsync(
        long seq, string workspace, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<(ChannelMessage Message, long Seq)>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM channel
            WHERE origin IS NULL AND seq > $seq AND workspace = $workspace COLLATE NOCASE
            ORDER BY seq
            """;
        command.Parameters.AddWithValue("$seq", seq);
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        var read = new List<(ChannelMessage, long)>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) read.Add((Read(reader), reader.GetInt64(reader.GetOrdinal("seq"))));
        return read;
    }, ct);

    /// <summary>
    /// Keep a teammate's message, which carries its <see cref="ChannelMessage.Origin"/>, once: the same <c>(origin, id)</c> is the same
    /// message, so a feed or a page seen again changes nothing. No cap applies, and no read mark moves.
    /// </summary>
    public Task MirrorAsync(ChannelMessage message, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT OR IGNORE INTO channel (id, ask, workspace, at, session, repository, agent, entry, to_repo, re, question, text, origin, urgent)
            VALUES ($id, $ask, $workspace, $at, $session, $repository, $agent, $entry, $to, $re, $question, $text, $origin, $urgent)
            """;
        // COWORK1d: the level as its machine said it; none from an older machine, kept as none.
        command.Parameters.AddWithValue("$urgent", Level(message.Urgent));
        command.Parameters.AddWithValue("$id", message.Id);
        command.Parameters.AddWithValue("$ask", message.Ask);
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(message.Workspace));
        command.Parameters.AddWithValue("$at", Moments.Write(message.At));
        command.Parameters.AddWithValue("$session", (object?)message.Session ?? DBNull.Value);
        command.Parameters.AddWithValue("$repository", (object?)message.Repository ?? DBNull.Value);
        command.Parameters.AddWithValue("$agent", (object?)message.Agent ?? DBNull.Value);
        command.Parameters.AddWithValue("$entry", (object?)message.Entry ?? DBNull.Value);
        command.Parameters.AddWithValue("$to", (object?)message.To ?? DBNull.Value);
        command.Parameters.AddWithValue("$re", (object?)message.Re ?? DBNull.Value);
        command.Parameters.AddWithValue("$question", message.Question ? 1 : 0);
        command.Parameters.AddWithValue("$text", message.Text);
        command.Parameters.AddWithValue("$origin", message.Origin ?? throw new ArgumentException("a mirrored message names its origin", nameof(message)));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// A remote's side: the team's messages it holds after <paramref name="since"/>, in arrival order, one page at a time, leaving out
    /// <paramref name="caller"/>'s own, which it already has, while still moving the page past them (as the session records do).
    /// </summary>
    public Task<ChannelFetch> TeamSinceAsync(long since, string? caller, int limit = 500, CancellationToken ct = default) =>
        _db.RunAsync<ChannelFetch>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM channel WHERE origin IS NOT NULL AND seq > $since ORDER BY seq LIMIT $take";
        command.Parameters.AddWithValue("$since", since);
        command.Parameters.AddWithValue("$take", limit + 1);
        var scanned = new List<(ChannelMessage Message, long Seq)>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) scanned.Add((Read(reader), reader.GetInt64(reader.GetOrdinal("seq"))));

        var more = scanned.Count > limit;
        var page = more ? scanned[..limit] : scanned;
        return new ChannelFetch(
            [.. page.Where(m => !string.Equals(m.Message.Origin, caller, StringComparison.OrdinalIgnoreCase)).Select(m => m.Message)],
            page.Count == 0 ? since : page[^1].Seq,
            more);
    }, ct);

    /// <summary>How far this machine has pushed its own messages to a workspace's remote, and fetched the team's.</summary>
    public Task<(long Pushed, long Fetched)> CursorAsync(string workspace, CancellationToken ct = default) =>
        _db.RunAsync<(long Pushed, long Fetched)>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT pushed, fetched FROM channel_cursor WHERE workspace = $workspace";
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? (reader.GetInt64(0), reader.GetInt64(1)) : (0, 0);
    }, ct);

    /// <summary>Move a workspace's cursors forward, never back, so a late answer cannot re-send the past.</summary>
    public Task AdvanceCursorAsync(string workspace, long? pushed = null, long? fetched = null, CancellationToken ct = default) =>
        _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO channel_cursor (workspace, pushed, fetched) VALUES ($workspace, $pushed, $fetched)
            ON CONFLICT (workspace) DO UPDATE SET
              pushed = MAX(pushed, excluded.pushed), fetched = MAX(fetched, excluded.fetched)
            """;
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        command.Parameters.AddWithValue("$pushed", pushed ?? 0);
        command.Parameters.AddWithValue("$fetched", fetched ?? 0);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>A message's level as its column keeps it: 1 urgent, 0 ordinary, null for one from before.</summary>
    private static object Level(bool? urgent) => urgent is { } said ? said ? 1 : 0 : DBNull.Value;

    private static ChannelMessage Read(SqliteDataReader reader)
    {
        string Text(string column) => reader.GetString(reader.GetOrdinal(column));
        string? Maybe(string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));
        var urgent = reader.GetOrdinal("urgent");
        return new ChannelMessage(
            Text("id"), Text("ask"), Text("workspace"), Moments.ReadRequired(Text("at")), Maybe("session"), Maybe("repository"),
            Maybe("agent"), Maybe("entry"), Maybe("to_repo"), Maybe("re"), reader.GetInt32(reader.GetOrdinal("question")) != 0,
            Text("text"), Maybe("origin"))
        {
            Urgent = reader.IsDBNull(urgent) ? null : reader.GetInt32(urgent) != 0,
        };
    }

    /// <summary>
    /// Every ask the kept messages name, each with its circle and the moments of its messages by origin (SESSONE2h): what a list of the
    /// asks held elsewhere is made of. The messages' words are not read.
    /// </summary>
    public Task<IReadOnlyList<(string Ask, string Workspace, string? Origin, DateTimeOffset At)>> NamedAsksAsync(CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<(string Ask, string Workspace, string? Origin, DateTimeOffset At)>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = "SELECT ask, workspace, origin, at FROM channel ORDER BY seq";
            var read = new List<(string Ask, string Workspace, string? Origin, DateTimeOffset At)>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                read.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), Moments.ReadRequired(reader.GetString(3))));
            }

            return read;
        }, ct);

    /// <summary>The moment an entry last read the ask's channel up to, or null when it never has.</summary>
    public Task<DateTimeOffset?> MarkAsync(string ask, string entry, CancellationToken ct = default) => _db.RunAsync<DateTimeOffset?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT read_at FROM channel_marks WHERE ask = $ask AND entry = $entry";
        command.Parameters.AddWithValue("$ask", ask);
        command.Parameters.AddWithValue("$entry", entry);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is string at ? Moments.Read(at) : null;
    }, ct);

    /// <summary>Move an entry's mark forward to <paramref name="at"/>; a mark never moves back.</summary>
    public Task MoveMarkAsync(string ask, string entry, DateTimeOffset at, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var read = _db.Command();
        read.CommandText = "SELECT read_at FROM channel_marks WHERE ask = $ask AND entry = $entry";
        read.Parameters.AddWithValue("$ask", ask);
        read.Parameters.AddWithValue("$entry", entry);
        if (await read.ExecuteScalarAsync(ct).ConfigureAwait(false) is string held && Moments.Read(held) is { } mark && mark >= at) return;

        await using var write = _db.Command();
        write.CommandText = """
            INSERT INTO channel_marks (ask, entry, read_at) VALUES ($ask, $entry, $at)
            ON CONFLICT (ask, entry) DO UPDATE SET read_at = $at
            """;
        write.Parameters.AddWithValue("$ask", ask);
        write.Parameters.AddWithValue("$entry", entry);
        write.Parameters.AddWithValue("$at", Moments.Write(at));
        await write.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Remove an ask's channel with the ask: its messages and its marks. Content-derived ids mean the same words asked again are the same ask.</summary>
    public Task ForgetAsync(string ask, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "DELETE FROM channel WHERE ask = $ask; DELETE FROM channel_marks WHERE ask = $ask;";
        command.Parameters.AddWithValue("$ask", ask);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);
}

/// <summary>
/// The judgement over an ask's channel (SESSONE2a; the co-working design §2): who may post, what a message may say and to whom, what a
/// read holds, and Daoris's lines, read from the ask's quests. The store holds the messages; this decides, for the connector's two tools
/// and the HTTP host's two doors alike.
/// </summary>
/// <remarks>
/// A message asks nothing of anyone and starts nothing: it is read at a session's next look (SESSONE2b), and work stays a quest (D79).
/// </remarks>
public sealed class ChannelDesk(
    ChannelStore store, AskDesk asks, AskStore askStore, SessionLedger ledger, QuestStore quests, ISessionEntryFacts facts)
{
    /// <summary>The entry the person reads as: their read mark's key.</summary>
    public const string PersonEntry = "person";

    /// <summary>The address that means every repository, said the way a person or an agent may say it.</summary>
    private const string Everyone = "everyone";

    /// <summary>Post as the session the connector names: its record gives the ask (through its entry), its repository and its adapter.</summary>
    /// <param name="urgent">
    /// The poster's level (COWORK1d, D188 point 2): urgent asks the session's next step, ordinary (the default) a natural point. Kept
    /// urgent for at most <see cref="ChannelBounds.UrgentPerPart"/> of the part's posts; the outcome's message says what was kept.
    /// </param>
    public async Task<ChannelOutcome> PostAsSessionAsync(
        string? sessionId, string? text, string? to, string? re, bool question, DateTimeOffset now, bool urgent = false,
        CancellationToken ct = default)
    {
        var (refusal, ask, session) = await SpeakerAsync(sessionId, ct).ConfigureAwait(false);
        if (refusal != ChannelRefusal.None) return new(refusal, null);

        var judged = await JudgeAsync(ask!, text, to, re, ct).ConfigureAwait(false);
        if (judged.Refusal != ChannelRefusal.None) return new(judged.Refusal, null);

        var entry = (await ledger.WithEntryAsync(session!, ct).ConfigureAwait(false)).Entry;
        var message = new ChannelMessage(
            NewId(), ask!.Id, ask.Workspace, now, session!.Id, session.Repository, session.Adapter, entry,
            judged.To, judged.Re, question, judged.Text!) { Urgent = urgent };
        return await store.KeepAsync(message, ChannelBounds.MaxPerPart, ct).ConfigureAwait(false) is { } kept
            ? new(ChannelRefusal.None, kept)
            : new(ChannelRefusal.TooMany, null);
    }

    /// <summary>
    /// Post as the person (D167 point 7): kept on the channel by no session, and on the ask as a word of the <c>channel</c> kind (D133 §1),
    /// which an older build passes over as it does any kind it does not know. A person has no cap.
    /// </summary>
    /// <param name="urgent">Their level (COWORK1d): urgent unless they asked ordinary, their default as their words' is (D188 point 2).</param>
    public async Task<ChannelOutcome> PostAsPersonAsync(
        string askId, string? text, string? to, string? re, DateTimeOffset now, CancellationToken ct = default, bool urgent = true)
    {
        if (await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } ask)
        {
            return new(await IsMirroredAsync(askId, ct).ConfigureAwait(false) ? ChannelRefusal.HeldElsewhere : ChannelRefusal.NotFound, null);
        }

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
    /// Null when this machine holds no such ask and has mirrored no message of one. An ask held elsewhere (SESSONE2g) reads from the
    /// messages the sync mirrored, read-only: no mark moves and Daoris's lines, which come from this machine's quests, are not in it.
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
        return new ChannelRead(
            [.. mirrored.Where(m => since is null || m.At > since).OrderBy(m => m.At)], [],
            mirrored.Count(m => mark is null || m.At > mark), mark);
    }

    /// <summary>
    /// The asks the mirrored messages name that this machine does not hold (SESSONE2h), newest message first. No ask record is here, so a
    /// title is not: the messages carry their circle, the machines they came from, and when. Unread is against the person's local mark;
    /// each is readable at <see cref="ReadAsync"/> by its id.
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
                group.Max(row => row.At), group.Count(), group.Count(row => mark is null || row.At > mark)));
        }

        return [.. listed.OrderByDescending(ask => ask.Latest)];
    }

    private async Task<bool> IsMirroredAsync(string askId, CancellationToken ct) =>
        (await store.OfAsync(askId.TrimStart('#'), ct).ConfigureAwait(false)).Count > 0;

    /// <summary>
    /// A session's read: what is new since its entry last read (or since <paramref name="since"/>, whatever the mark says), and the mark
    /// moves. The same speaker's rules as a post: a session on an ask, and no helper or reviewer.
    /// </summary>
    public async Task<ChannelReadOutcome> ReadAsSessionAsync(string? sessionId, DateTimeOffset? since, CancellationToken ct = default)
    {
        var (refusal, ask, session) = await SpeakerAsync(sessionId, ct).ConfigureAwait(false);
        if (refusal != ChannelRefusal.None) return new(refusal, null, null);

        var entry = (await ledger.WithEntryAsync(session!, ct).ConfigureAwait(false)).Entry!;
        var from = since ?? await store.MarkAsync(ask!.Id, entry, ct).ConfigureAwait(false);
        return new(ChannelRefusal.None, ask, await ReadOfAsync(ask!, entry, from, moveMark: true, ct).ConfigureAwait(false));
    }

    /// <summary>Remove an ask's channel with the ask (D95).</summary>
    public Task ForgetAsync(string askId, CancellationToken ct = default) => store.ForgetAsync(askId.TrimStart('#'), ct);

    private async Task<ChannelRead> ReadOfAsync(Ask ask, string entry, DateTimeOffset? from, bool moveMark, CancellationToken ct)
    {
        var mark = await store.MarkAsync(ask.Id, entry, ct).ConfigureAwait(false);
        var messages = await store.OfAsync(ask.Id, ct).ConfigureAwait(false);
        var lines = await LinesAsync(ask, ct).ConfigureAwait(false);

        var newest = messages.Select(m => m.At).Concat(lines.Select(l => l.At)).DefaultIfEmpty().Max();
        var unread = messages.Count(m => mark is null || m.At > mark) + lines.Count(l => mark is null || l.At > mark);
        if (moveMark && (messages.Count > 0 || lines.Count > 0)) await store.MoveMarkAsync(ask.Id, entry, newest, ct).ConfigureAwait(false);

        return new ChannelRead(
            [.. messages.Where(m => from is null || m.At > from)], [.. lines.Where(l => from is null || l.At > from)], unread, mark);
    }

    /// <summary>
    /// Move an entry's mark to <paramref name="through"/>, or to the newest item when none is given (the driver moves a part's mark when
    /// the part starts, to the moment it handed the channel over), and answer the channel as it then reads. Null when no such ask.
    /// </summary>
    public async Task<ChannelRead?> MarkReadAsync(string askId, string entry, DateTimeOffset? through, CancellationToken ct = default)
    {
        if (await asks.FindAsync(askId, ct).ConfigureAwait(false) is not { } ask) return null;
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
        ChannelRefusal.HeldElsewhere => "held-elsewhere",
        _ => refusal.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// Daoris's lines for the ask (design §2.2), read from its quests as they stand and never stored: each quest asked by the ask or
    /// published by a session on it, published, and where it has moved, taken, done, declined, or done and awaiting the person's yes.
    /// </summary>
    private async Task<IReadOnlyList<ChannelLine>> LinesAsync(Ask ask, CancellationToken ct)
    {
        var lines = new List<ChannelLine>();
        foreach (var quest in await QuestsOfAsync(ask, ct).ConfigureAwait(false))
        {
            lines.Add(new ChannelLine("published", quest.Id, quest.Name, quest.From, quest.To, quest.Filed));
            var code = quest.Status switch
            {
                QuestStatus.Taken => "taken",
                QuestStatus.Done when quest.Held => "awaiting",
                QuestStatus.Done => "done",
                QuestStatus.Declined => "declined",
                _ => null,
            };
            if (code is not null) lines.Add(new ChannelLine(code, quest.Id, quest.Name, quest.From, quest.To, quest.Updated));
        }

        return [.. lines.OrderBy(line => line.At)];
    }

    /// <summary>The quests that belong to the ask: asked by it (chain steps included), or whose entry's origin is it (D167 point 2).</summary>
    private async Task<IReadOnlyList<Quest>> QuestsOfAsync(Ask ask, CancellationToken ct)
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

    /// <summary>The session that speaks and the ask its entry names, or why it may not.</summary>
    private async Task<(ChannelRefusal Refusal, Ask? Ask, Session? Session)> SpeakerAsync(string? sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || await ledger.FindAsync(sessionId.Trim(), ct).ConfigureAwait(false) is not { } session)
        {
            return (ChannelRefusal.NoSession, null, null);
        }

        if (session.Serves is not null || session.Opinion is not null) return (ChannelRefusal.Helper, null, null);

        var key = (await ledger.WithEntryAsync(session, ct).ConfigureAwait(false)).Entry ?? "";
        var origin = key.IndexOf('/') is > 0 and var slash ? key[..slash] : key;
        var prefix = SessionEntries.AskOrigin("");
        if (!origin.StartsWith(prefix, StringComparison.Ordinal) || origin.Length == prefix.Length
            || await asks.FindAsync(origin[prefix.Length..], ct).ConfigureAwait(false) is not { } ask)
        {
            return (ChannelRefusal.NoAsk, null, null);
        }

        return (ChannelRefusal.None, ask, session);
    }

    /// <summary>The words trimmed and bounded, and where they are addressed and what they answer, each as the ask's channel holds it.</summary>
    private async Task<(ChannelRefusal Refusal, string? Text, string? To, string? Re)> JudgeAsync(
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
