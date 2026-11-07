using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// The messages of every ask's channel, and each entry's read mark. Machine-local service records beside the
/// asks they are on; Daoris's own lines are not here, they are read from the quests.
/// </summary>
public sealed class ChannelStore
{
    private readonly ConnectionGate _db;

    private ChannelStore(SqliteConnection connection)
    {
        _db = ConnectionGate.For(connection);
        Addressed = new(_db);
        Members = new(_db);
    }

    /// <summary>A line's own channel and the messages to one part, kept apart from this store's.</summary>
    public ChannelAddressedStore Addressed { get; }

    /// <summary>The agents outside Daoris on a channel, never synced.</summary>
    public ChannelMemberStore Members { get; }

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

        // The channel's revision, the change stream's `channel` family, one row moved by every insert,
        // update and delete of a message. The newest `seq` would not do: an ask's channel forgotten with it deletes older messages
        // and leaves the newest in place. Kept by triggers, as the asks' is, so a connector built before them still moves it.
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

        // A message's level, said on every message kept from now; a message from before says
        // none, which keeps its old reach and is never read as ordinary.
        await SchemaColumns.EnsureAsync(_db, "channel", "urgent", "urgent INTEGER NULL", ct).ConfigureAwait(false);
        await ChannelMemberStore.EnsureSchemaAsync(_db, ct).ConfigureAwait(false);

        // A message kept as a row of the event journal, minted by a trigger in the
        // statement that writes it. After the columns the trigger reads (`urgent` is the last added), so it names none that is not there.
        await using (var journal = _db.Command())
        {
            journal.CommandText = EventJournal.ChannelSchema;
            await journal.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Each entry's acknowledgment of each message, by its id, which a mark by moment could not keep. A
        // channel from before has marks and no rows: every message at or before an entry's mark counts as taken, written once as the
        // table is made, so a message mirrored later with an older moment is still the entry's to be handed.
        await using var probe = _db.Command();
        probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'channel_acks'";
        var had = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
        await using var acks = _db.Command();
        acks.CommandText = """
            CREATE TABLE IF NOT EXISTS channel_acks (
              ask     TEXT NOT NULL,
              entry   TEXT NOT NULL,
              message TEXT NOT NULL,
              state   TEXT NOT NULL,
              session TEXT NULL,
              at      TEXT NOT NULL,
              PRIMARY KEY (ask, entry, message)
            );
            """;
        await acks.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (!had) await AcknowledgedByMarksAsync(ct).ConfigureAwait(false);

        await ChannelAddressedStore.EnsureSchemaAsync(_db, ct).ConfigureAwait(false);
        return this;
    }

    /// <summary>A channel from before the acknowledgments: each message at or before an entry's mark, taken. Called inside the gate.</summary>
    private async Task AcknowledgedByMarksAsync(CancellationToken ct)
    {
        var marks = new List<(string Ask, string Entry, DateTimeOffset Through)>();
        await using (var read = _db.Command())
        {
            read.CommandText = "SELECT ask, entry, read_at FROM channel_marks WHERE entry <> $person";
            read.Parameters.AddWithValue("$person", ChannelDesk.PersonEntry);
            await using var reader = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (Moments.Read(reader.GetString(2)) is { } through) marks.Add((reader.GetString(0), reader.GetString(1), through));
            }
        }

        foreach (var (ask, entry, through) in marks)
        {
            var taken = new List<string>();
            await using (var messages = _db.Command())
            {
                messages.CommandText = "SELECT id, at FROM channel WHERE ask = $ask";
                messages.Parameters.AddWithValue("$ask", ask);
                await using var reader = await messages.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    if (Moments.Read(reader.GetString(1)) is { } at && at <= through) taken.Add(reader.GetString(0));
                }
            }

            foreach (var id in taken)
            {
                await using var write = _db.Command();
                write.CommandText = """
                    INSERT OR IGNORE INTO channel_acks (ask, entry, message, state, session, at) VALUES ($ask, $entry, $message, $state, NULL, $at)
                    """;
                write.Parameters.AddWithValue("$ask", ask);
                write.Parameters.AddWithValue("$entry", entry);
                write.Parameters.AddWithValue("$message", id);
                write.Parameters.AddWithValue("$state", ChannelAcks.Taken);
                write.Parameters.AddWithValue("$at", Moments.Write(through));
                await write.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>An acknowledgment's rank, in SQL: a state never moves back to a lower one (<see cref="ChannelAcks"/>).</summary>
    internal static string Rank(string state) =>
        $"CASE {state} WHEN 'partly' THEN 0 WHEN 'taking' THEN 1 WHEN 'sent' THEN 2 WHEN 'read' THEN 3 WHEN 'taken' THEN 4 ELSE -1 END";

    /// <summary>
    /// One part's claim on a message for its entry, in one statement: it succeeds only where the entry holds no
    /// acknowledgment of it, one a start's packet left <c>partly</c> for another part, or a claim <paramref name="stale"/> names, whose
    /// holder no longer runs. So two parts of one entry never both take a message.
    /// </summary>
    /// <returns>Whether this part claimed it.</returns>
    public Task<bool> ClaimAsync(
        string ask, string entry, string message, string session, string? stale, DateTimeOffset at, CancellationToken ct = default) =>
        _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO channel_acks (ask, entry, message, state, session, at) VALUES ($ask, $entry, $message, 'taking', $session, $at)
            ON CONFLICT (ask, entry, message) DO UPDATE SET state = 'taking', session = $session, at = $at
            WHERE (channel_acks.state = 'partly' AND COALESCE(channel_acks.session, '') <> $session)
               OR (channel_acks.state = 'taking' AND $stale IS NOT NULL AND channel_acks.session = $stale)
            """;
        command.Parameters.AddWithValue("$ask", ask);
        command.Parameters.AddWithValue("$entry", entry);
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$session", session);
        command.Parameters.AddWithValue("$stale", (object?)stale ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// An entry's acknowledgment of a message raised to <paramref name="state"/>, never lowered: a late
    /// <c>sent</c> leaves <c>taken</c> untouched. <see cref="ChannelAcks.Released"/> lets a part's claim go instead.
    /// </summary>
    /// <returns>Whether the row changed.</returns>
    public Task<bool> AcknowledgeAsync(
        string ask, string entry, string message, string state, string? session, DateTimeOffset at, CancellationToken ct = default) =>
        _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = state == ChannelAcks.Released
            ? """
              DELETE FROM channel_acks
              WHERE ask = $ask AND entry = $entry AND message = $message AND state = 'taking' AND session = $session
              """
            : $"""
              INSERT INTO channel_acks (ask, entry, message, state, session, at) VALUES ($ask, $entry, $message, $state, $session, $at)
              ON CONFLICT (ask, entry, message) DO UPDATE SET
                state = excluded.state, session = COALESCE(excluded.session, channel_acks.session), at = excluded.at
              WHERE {Rank("excluded.state")} > {Rank("channel_acks.state")}
              """;
        command.Parameters.AddWithValue("$ask", ask);
        command.Parameters.AddWithValue("$entry", entry);
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$session", (object?)session ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>Every entry's acknowledgments on an ask's channel, by entry and message id.</summary>
    public Task<IReadOnlyList<(string Entry, string Message, ChannelAck Ack)>> AcksOfAsync(string ask, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<(string Entry, string Message, ChannelAck Ack)>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT entry, message, state, session, at FROM channel_acks WHERE ask = $ask";
        command.Parameters.AddWithValue("$ask", ask);
        var read = new List<(string, string, ChannelAck)>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            read.Add((reader.GetString(0), reader.GetString(1), new ChannelAck(
                reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), Moments.ReadRequired(reader.GetString(4)))));
        }

        return read;
    }, ct);

    /// <summary>
    /// Keep a message, unless its session has posted <paramref name="perPart"/> already: one statement, so two posts at once cannot both
    /// pass the count. A message of no session (the person's) has no cap.
    /// </summary>
    /// <returns>False when the cap refused it, and nothing is kept.</returns>
    public async Task<bool> AddAsync(ChannelMessage message, int perPart, CancellationToken ct = default) =>
        await KeepAsync(message, perPart, ct).ConfigureAwait(false) is not null;

    /// <summary>
    /// <see cref="AddAsync"/>, answering the message as kept. Its level is decided in the same statement: a part's
    /// urgent post past <paramref name="urgentPerPart"/> of its urgent ones is kept ordinary, an explicit false, so two posts at once
    /// cannot both pass the bound; a message of no session has none.
    /// </summary>
    /// <returns>The message as kept, its <see cref="ChannelMessage.Urgent"/> as the statement decided; null when the cap refused it.</returns>
    /// <remarks>
    /// A write transaction of its own, as every <c>RETURNING</c> write is (ConnectionGate.InTransactionAsync says why).
    /// </remarks>
    public Task<ChannelMessage?> KeepAsync(
        ChannelMessage message, int perPart, CancellationToken ct = default, int urgentPerPart = ChannelBounds.UrgentPerPart) =>
        _db.InTransactionAsync<ChannelMessage?>(async (transaction, inside) =>
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO channel (id, ask, workspace, at, session, repository, agent, entry, to_repo, re, question, text, origin, urgent)
            SELECT $id, $ask, $workspace, $at, $session, $repository, $agent, $entry, $to, $re, $question, $text, $origin,
              CASE WHEN $urgent = 1 AND $session IS NOT NULL
                        AND (SELECT COUNT(*) FROM channel WHERE session = $session AND urgent = 1)
                          + (SELECT COUNT(*) FROM channel_addressed WHERE session = $session AND urgent = 1) >= $urgentCap THEN 0
                   ELSE $urgent END
            WHERE $session IS NULL
               OR (SELECT COUNT(*) FROM channel WHERE session = $session) + (SELECT COUNT(*) FROM channel_addressed WHERE session = $session) < $cap
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
        await using var reader = await command.ExecuteReaderAsync(inside).ConfigureAwait(false);
        if (!await reader.ReadAsync(inside).ConfigureAwait(false)) return null;
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
    /// A message fed down from a teammate is never in it. A message is never changed once kept, so the arrival number is
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
        // The level as its machine said it; none from an older machine, kept as none.
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
    internal static object Level(bool? urgent) => urgent is { } said ? said ? 1 : 0 : DBNull.Value;

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
    /// Every ask the kept messages name, each with its circle and the moments of its messages by origin: what a list of the
    /// asks held elsewhere is made of, and whether a person wrote each (no session did). The messages' words are not read.
    /// </summary>
    public Task<IReadOnlyList<(string Ask, string Workspace, string? Origin, DateTimeOffset At, bool ByPerson)>> NamedAsksAsync(CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<(string Ask, string Workspace, string? Origin, DateTimeOffset At, bool ByPerson)>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = "SELECT ask, workspace, origin, at, session IS NULL FROM channel ORDER BY seq";
            var read = new List<(string Ask, string Workspace, string? Origin, DateTimeOffset At, bool ByPerson)>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                read.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                    Moments.ReadRequired(reader.GetString(3)), reader.GetInt64(4) != 0));
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
        command.CommandText = "DELETE FROM channel WHERE ask = $ask; DELETE FROM channel_marks WHERE ask = $ask; DELETE FROM channel_acks WHERE ask = $ask;";
        command.Parameters.AddWithValue("$ask", ask);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);
}
