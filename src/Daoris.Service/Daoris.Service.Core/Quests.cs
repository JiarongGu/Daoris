using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>Where a quest is in its life. Four states, because anything finer is status for its own sake.</summary>
public enum QuestStatus
{
    /// <summary>Published, nobody has taken it.</summary>
    Open,

    /// <summary>A repository's agent has accepted it.</summary>
    Taken,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Turned down — a real answer, and the reason is the part the asker can act on.</summary>
    Declined,
}

/// <param name="Id">Short, stable handle — quotable in a commit message.</param>
/// <param name="From">The repository that asked.</param>
/// <param name="To">The repository being asked. Must have adopted, or there is nobody to answer.</param>
/// <param name="Title">One line: what is wanted.</param>
/// <param name="Body">Why, and the evidence — never the prescribed change.</param>
/// <param name="Status">Where it is.</param>
/// <param name="Note">The reason, when declined or finished.</param>
/// <param name="Filed">When it was published.</param>
/// <param name="Updated">When its status last moved.</param>
/// <param name="Workspace">
/// The circle this quest belongs to (D48). Both sides share it by construction — the exchange refuses
/// a publish that would cross — so one field, not two, and a scoped list can trust it.
/// </param>
public sealed record Quest(
    string Id,
    string From,
    string To,
    string Title,
    string Body,
    QuestStatus Status,
    string? Note,
    DateTimeOffset Filed,
    DateTimeOffset Updated,
    string Workspace = Workspaces.Default)
{
    /// <summary>
    /// Moves that lost to another machine's (D68 §5), in the order they were recorded — kept, not
    /// dropped, until a person acts. Empty for a quest nobody raced.
    /// </summary>
    public IReadOnlyList<QuestConflict> Conflicts { get; init; } = [];

    /// <summary>
    /// Addresses the quest carries — a ticket, a page, a document (D65 §2). They travel with it
    /// everywhere the quest does, and are given in the order the asker gave them.
    /// </summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>
    /// Files the quest carries, BY NAME (D65 §2). The bytes are machine-local — kept under the home of
    /// the machine that published them, the transcript's boundary (D47 §4) — so a record read anywhere
    /// else says a file exists without being able to open it.
    /// </summary>
    public IReadOnlyList<QuestAttachment> Attachments { get; init; } = [];

    /// <summary>
    /// What happens when this closes done (D65 §4): the next step is published as part of the close,
    /// asked on behalf of the same asker and carrying the rest. Empty for an ordinary quest. A chain is
    /// data on the quest — no engine runs it; the driver drives each step as it drives any quest.
    /// </summary>
    public IReadOnlyList<QuestStep> Then { get; init; } = [];

    /// <summary>The quest whose close published this one, when it is a step of a chain.</summary>
    public string? Parent { get; init; }
}

/// <summary>One step of a chain: whom to ask next, and what (D65 §4).</summary>
/// <param name="To">The repository asked — addressable from the chain's asker, judged when composed.</param>
/// <param name="Title">One line. <c>{parent}</c> becomes the id of the quest this step follows.</param>
/// <param name="Body">Why, and the evidence. <c>{parent}</c> is expanded here too.</param>
public sealed record QuestStep(string To, string Title, string Body);

/// <summary>A file a quest carries, as its record names it — never where it lies on a disk.</summary>
/// <param name="Name">The file's own name, made safe to keep: what a reader and a session see.</param>
/// <param name="Sha256">Its content's hash, lowercase hex — the file's identity, and how it is found.</param>
/// <param name="Bytes">Its size.</param>
public sealed record QuestAttachment(string Name, string Sha256, long Bytes);

/// <param name="Quest">The quest as it now stands — null when no such id exists.</param>
/// <param name="Moved">Whether THIS call moved it. False with a non-null quest is a refused move.</param>
/// <param name="FollowUp">The chain's next step, published by this close — null when there was none.</param>
public sealed record QuestMove(Quest? Quest, bool Moved, Quest? FollowUp = null);

/// <summary>What a dismissal did: the quest as it now stands (null when there is no such quest), and how many conflicts went.</summary>
public sealed record QuestDismissal(Quest? Quest, int Dismissed);

/// <summary>
/// Quests, held by the service rather than written into anyone's repository.
/// </summary>
/// <remarks>
/// <para><b>This is a server responsibility, and the first version got it wrong.</b> The original
/// implementation had one repository's agent write a quest straight into another repository's backlog
/// file. That is the very thing `repository-owns-its-work` forbids — an outside edit is still an
/// outside edit when it is one file and uncommitted, and it arrives from the party that knows that
/// codebase least. Building the tool that way made the rule's own tooling break the rule.</para>
///
/// <para><b>So the service holds them and repositories pull.</b> An agent publishes a quest here; the
/// receiving repository's own agent reads what is addressed to it and decides what to do — including
/// materializing it into its backlog, which is then that repository editing itself. Nobody reaches
/// across.</para>
///
/// <para><b>Only an adopted repository can be addressed.</b> A quest for a repository with no manifest
/// has nobody to answer it and no client to see it, so it would sit in a queue nobody reads. Refusing
/// at publish time says that immediately, rather than letting it look delivered.</para>
///
/// <para><b>A quest is its history</b> (D68, SYNC1). Every verb appends an operation to
/// <c>quest_log</c>, stamped with this store's machine and that machine's next sequence number, and
/// the <c>quests</c> table is rewritten from replaying the quest's history through
/// <see cref="QuestTransitions"/> in the same transaction — so what the platform reads is a cache of
/// the replay, never a second truth beside it.</para>
///
/// <para><b>A remote is where histories meet</b> (D68 §3, design §8): a machine fetches what the
/// remote accepted, rebases what it has not pushed on top, and pushes; the remote accepts a quest's
/// operations only when nothing reached that quest since the push was rebased. Both halves live here,
/// behind the same table: <see cref="IntegrateAsync"/>, <see cref="PendingAsync"/> and
/// <see cref="AcceptedAsync"/> for a machine, <see cref="OperationsSinceAsync"/> and
/// <see cref="ReceiveAsync"/> for a remote.</para>
/// </remarks>
public sealed class QuestStore
{
    /// <summary>
    /// Hex characters in a quest id: 48 bits (design §7). Six were enough while a machine held its own
    /// quests; once every machine holds every quest touching its repositories, unrelated asks collide.
    /// </summary>
    internal const int IdLength = 12;

    /// <summary>What an id was before it widened — a quest published then keeps the id it was quoted by.</summary>
    private const int LegacyIdLength = 6;

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate;

    private QuestStore(SqliteConnection connection)
    {
        _connection = connection;
        _gate = ConnectionGate.For(connection);
    }

    /// <summary>
    /// This store's machine: the stable id every operation it writes is stamped with (D68 §2) — never
    /// the key, which rotates.
    /// </summary>
    /// <remarks>
    /// Kept in the store, beside the sequence it numbers, rather than in a file of its own: a machine
    /// whose store was deleted starts its sequence again at one, and under the same id that would
    /// name operations a remote already holds. A new store is a new machine, and cannot be anything else.
    /// </remarks>
    public string Machine { get; private set; } = "";

    public static async Task<QuestStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new QuestStore(connection);
        await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
        store.Machine = await store.EnsureMachineAsync(ct).ConfigureAwait(false);
        await store.GiveHistoriesAsync(ct).ConfigureAwait(false);
        await store.RecacheUnnamedConflictsAsync(ct).ConfigureAwait(false);
        return store;
    }

    /// <summary>
    /// A cache written before dismissals (SYNC6c) holds conflicts without the sequence that names them,
    /// and a conflict that cannot be named cannot be dismissed. Those quests are replayed from the log,
    /// which has always kept the sequence — the cache is only ever what the history replays to.
    /// </summary>
    private async Task RecacheUnnamedConflictsAsync(CancellationToken ct)
    {
        const string Unnamed = "SELECT id FROM quests WHERE conflicts <> '[]' AND conflicts NOT LIKE '%\"sequence\"%'";

        await using (var probe = _connection.CreateCommand())
        {
            probe.CommandText = $"SELECT EXISTS ({Unnamed})";
            if (Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0) return;
        }

        await InTransactionAsync(async transaction =>
        {
            var ids = new List<string>();
            await using (var select = _connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = Unnamed;
                await using var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) ids.Add(reader.GetString(0));
            }

            foreach (var id in ids)
            {
                if (QuestLog.Replay(await HistoryAsync(id, transaction, ct).ConfigureAwait(false)) is { } quest)
                {
                    await WriteCacheAsync(quest, transaction, ct).ConfigureAwait(false);
                }
            }

            return ids.Count;
        }, ct).ConfigureAwait(false);
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using (var command = _connection.CreateCommand())
        {
            // The log's position is this store's order of appending — and, on a remote, the NUMBER it
            // gives what it accepts (design §8). Machine + sequence names one operation anywhere;
            // position names it here; `remote` is where the remote placed it, null while pending.
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS quest_log (
                  position  INTEGER PRIMARY KEY,
                  quest     TEXT NOT NULL,
                  kind      TEXT NOT NULL,
                  machine   TEXT NOT NULL,
                  sequence  INTEGER NOT NULL,
                  at        TEXT NOT NULL,
                  payload   TEXT NOT NULL,
                  remote    INTEGER NULL,
                  UNIQUE (machine, sequence)
                );
                CREATE INDEX IF NOT EXISTS quest_log_quest ON quest_log (quest, position);
                CREATE TABLE IF NOT EXISTS quest_machine (
                  one INTEGER PRIMARY KEY CHECK (one = 1),
                  id  TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS quest_cursor (
                  workspace TEXT PRIMARY KEY COLLATE NOCASE,
                  number    INTEGER NOT NULL
                );
                -- How each circle's last pass ended (SYNC6a): when it last reached its remote, when it
                -- last tried, the quests it left behind and the wall it hit. Kept in the store rather
                -- than in a host's memory, so a restarted host still knows when the circle last synced.
                CREATE TABLE IF NOT EXISTS quest_passes (
                  workspace TEXT PRIMARY KEY COLLATE NOCASE,
                  synced    TEXT NULL,
                  tried     TEXT NOT NULL,
                  behind    TEXT NOT NULL,
                  problem   TEXT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS quests (
                  id        TEXT PRIMARY KEY,
                  sender    TEXT NOT NULL,
                  receiver  TEXT NOT NULL,
                  title     TEXT NOT NULL,
                  body      TEXT NOT NULL,
                  status    TEXT NOT NULL,
                  note      TEXT NULL,
                  filed     TEXT NOT NULL,
                  updated   TEXT NOT NULL,
                  workspace   TEXT NOT NULL DEFAULT '{Workspaces.Default}',
                  links       TEXT NOT NULL DEFAULT '[]',
                  attachments TEXT NOT NULL DEFAULT '[]',
                  then_steps  TEXT NOT NULL DEFAULT '[]',
                  parent      TEXT NULL,
                  conflicts   TEXT NOT NULL DEFAULT '[]'
                );
                CREATE INDEX IF NOT EXISTS quests_receiver ON quests (receiver, status);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // A store from before workspaces has no workspace; one from before quests carried anything
        // (D65) has neither list; one from before sync has no conflicts, and a log with no numbers.
        // Their quests survive the upgrade — the one workspace there was, and nothing carried or
        // raced, which is exactly right, because everything in them was its own.
        foreach (var (table, column, definition) in new[]
        {
            ("quests", "workspace", $"workspace TEXT NOT NULL DEFAULT '{Workspaces.Default}'"),
            ("quests", "links", "links TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "attachments", "attachments TEXT NOT NULL DEFAULT '[]'"),
            // A chain (D65 §4): `then` is an SQL keyword, so the column says what it holds.
            ("quests", "then_steps", "then_steps TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "parent", "parent TEXT NULL"),
            ("quests", "conflicts", "conflicts TEXT NOT NULL DEFAULT '[]'"),
            ("quest_log", "remote", "remote INTEGER NULL"),
        })
        {
            if (!await HasColumnAsync(table, column, ct).ConfigureAwait(false))
            {
                await using var alter = _connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {definition}";
                await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        // The mirror is gone (design §8). Its rows were copies of a remote's quests, so they are
        // dropped rather than migrated: this machine's cursor starts at zero, and its first fetch
        // brings every one of them back as history. The column that marked them goes with them.
        if (await HasColumnAsync("quests", "home", ct).ConfigureAwait(false))
        {
            await using var drop = _connection.CreateCommand();
            drop.CommandText = """
                DELETE FROM quests WHERE home IS NOT NULL;
                ALTER TABLE quests DROP COLUMN home;
                """;
            await drop.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task<bool> HasColumnAsync(string table, string column, CancellationToken ct)
    {
        await using var probe = _connection.CreateCommand();
        probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $name";
        probe.Parameters.AddWithValue("$name", column);
        return Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
    }

    /// <summary>
    /// The store's machine id, made the first time any host opens it. One fixed row, so two hosts
    /// opening a new store at once cannot each make one: the second insert is ignored and both read
    /// the first.
    /// </summary>
    private async Task<string> EnsureMachineAsync(CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO quest_machine (one, id) VALUES (1, $id);
            SELECT id FROM quest_machine WHERE one = 1;
            """;
        command.Parameters.AddWithValue(
            "$id", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());
        return (string)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Give every quest of this store's own that the log has never seen its history, from what its row
    /// says: published when it was filed, and the move to where it stands when it last moved.
    /// </summary>
    /// <remarks>
    /// A quest is not derivable from anything, so a store from before the log is migrated rather than
    /// rebuilt (the index's rule is for what can be re-read). Checked again inside the transaction,
    /// because two hosts open one store and only one of them may write the histories.
    /// </remarks>
    private async Task GiveHistoriesAsync(CancellationToken ct)
    {
        const string Unlogged = """
            SELECT * FROM quests
            WHERE NOT EXISTS (SELECT 1 FROM quest_log WHERE quest_log.quest = quests.id)
            ORDER BY filed, id
            """;

        await using (var probe = _connection.CreateCommand())
        {
            probe.CommandText = $"SELECT EXISTS ({Unlogged})";
            if (Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0) return;
        }

        await InTransactionAsync(async transaction =>
        {
            var unlogged = new List<Quest>();
            await using (var select = _connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = Unlogged;
                await using var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) unlogged.Add(Read(reader));
            }

            foreach (var quest in unlogged) await GiveHistoryAsync(quest, transaction, ct).ConfigureAwait(false);
            return unlogged.Count;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>One quest's history, from its row — the operations that replay to exactly what it says.</summary>
    private async Task<IReadOnlyList<QuestOperation>> GiveHistoryAsync(
        Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        var history = new List<QuestOperation>
        {
            await AppendAsync(quest.Id, QuestOperationKind.Published, quest.Filed, null, quest, transaction, ct)
                .ConfigureAwait(false),
        };

        if (QuestTransitions.KindFor(quest.Status) is { } moved)
        {
            history.Add(await AppendAsync(quest.Id, moved, quest.Updated, quest.Note, null, transaction, ct)
                .ConfigureAwait(false));
        }

        return history;
    }

    /// <summary>
    /// Run <paramref name="work"/> in a write transaction taken at once (BEGIN IMMEDIATE), not at its
    /// first write: the judgement is made on what it reads, so the read must already hold the lock, or
    /// two hosts over one file could both judge the same open quest and both append a take (D47 §5).
    /// </summary>
    /// <remarks>
    /// <para>Within one host, the connection's gate holds every other transaction off until this one
    /// ends — SQLite does not nest them, and a host answers requests at once (<see cref="ConnectionGate"/>).</para>
    ///
    /// <para>It COMMITS whenever the work returns, including a refusal that wrote nothing. A statement
    /// another request runs meanwhile, outside any transaction, joins this one rather than failing
    /// (measured on this driver version, and pinned by a test) — so a rollback would quietly undo
    /// somebody else's write along with our nothing. Only a throw rolls back.</para>
    /// </remarks>
    private async Task<T> InTransactionAsync<T>(Func<SqliteTransaction, Task<T>> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var transaction = _connection.BeginTransaction(deferred: false);
            var result = await work(transaction).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// A handle derived from who asked, of whom, and for what — twelve hex characters (design §7).
    /// </summary>
    /// <remarks>
    /// Content-derived so publishing the same quest twice collides rather than multiplying — an agent
    /// that retries should not produce a second copy of the same ask, and the same ask made on two
    /// machines is one quest. A chain's step also derives from its PARENT (D65 §4): "Verify in the
    /// browser" is a title many chains will use, and a step that collided with an earlier quest of
    /// those words would quietly join somebody else's closed quest. The widening kept the hash, so an
    /// id from before it is exactly the first six characters of the same ask's id now.
    /// </remarks>
    internal static string MakeId(string from, string to, string title, string? parent = null) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{from}->{to}:{title.Trim()}" + (parent is null ? "" : $"<-{parent}"))))[..IdLength].ToLowerInvariant();

    /// <summary>Publish a quest. Returns the existing one unchanged if it was already asked.</summary>
    /// <param name="workspace">
    /// The circle both sides share — decided by <see cref="QuestExchange"/>, which is where the
    /// same-workspace clause lives. The store holds state; it does not judge who may ask whom.
    /// </param>
    /// <param name="links">Addresses the quest carries, already judged by the exchange.</param>
    /// <param name="attachments">Files the quest carries, by name — the bytes are never this store's.</param>
    /// <param name="then">The chain after this quest (D65 §4), already judged by the exchange.</param>
    public async Task<Quest> PublishAsync(
        string from, string to, string title, string body, DateTimeOffset now,
        string? workspace = null,
        IReadOnlyList<string>? links = null,
        IReadOnlyList<QuestAttachment>? attachments = null,
        IReadOnlyList<QuestStep>? then = null,
        CancellationToken ct = default)
    {
        var id = MakeId(from, to, title);
        return await InTransactionAsync(async transaction =>
        {
            // The same ask already held is the answer, whichever width of id it was published under.
            var existing = await FindAsync(id, transaction, ct).ConfigureAwait(false)
                           ?? await FindAsync(id[..LegacyIdLength], transaction, ct).ConfigureAwait(false);
            if (existing is not null) return existing;

            var asked = new Quest(
                id, from, to, title, body, QuestStatus.Open, null, now, now,
                Workspace: Workspaces.Normalize(workspace))
            {
                Links = links ?? [],
                Attachments = attachments ?? [],
                Then = then ?? [],
            };

            return await PublishInAsync(asked, transaction, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Append a quest's `published` and write its row from the replay, inside a transaction.</summary>
    private async Task<Quest> PublishInAsync(Quest asked, SqliteTransaction transaction, CancellationToken ct)
    {
        var published = await AppendAsync(
            asked.Id, QuestOperationKind.Published, asked.Filed, null, asked, transaction, ct).ConfigureAwait(false);
        var quest = QuestLog.Replay([published])!;
        await WriteCacheAsync(quest, transaction, ct).ConfigureAwait(false);
        return quest;
    }

    /// <summary>
    /// Append one operation, stamped with this machine and its next sequence number — counted inside
    /// the caller's write transaction, so two hosts over one file cannot take the same number.
    /// </summary>
    private async Task<QuestOperation> AppendAsync(
        string quest, QuestOperationKind kind, DateTimeOffset at, string? note, Quest? published,
        SqliteTransaction transaction, CancellationToken ct, QuestOperationRef? dismisses = null)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quest_log (quest, kind, machine, sequence, at, payload)
            VALUES ($quest, $kind, $machine,
                    (SELECT COALESCE(MAX(sequence), 0) + 1 FROM quest_log WHERE machine = $machine),
                    $at, $payload)
            RETURNING sequence
            """;
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$kind", KindText(kind));
        command.Parameters.AddWithValue("$machine", Machine);
        command.Parameters.AddWithValue("$at", at.ToString("O"));
        command.Parameters.AddWithValue("$payload", PayloadJson(note, published, attempted: null, dismisses));
        var sequence = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));

        // Handed back as it will read back: a publish carries the quest as asked, open from this moment.
        return new QuestOperation(
            quest, kind, Machine, sequence, at, note,
            published is null ? null : published with { Status = QuestStatus.Open, Note = null, Filed = at, Updated = at, Conflicts = [] },
            Dismisses: dismisses);
    }

    /// <summary>
    /// Keep an operation another machine made, under ITS machine and sequence — what a remote receives
    /// and what a machine fetches. <paramref name="number"/> is where a remote placed it; a remote
    /// itself keeps none, because its own position is the number.
    /// </summary>
    /// <returns>The position it was kept at — on a remote, the number it was given.</returns>
    private async Task<long> KeepAsync(
        QuestOperation operation, long? number, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quest_log (quest, kind, machine, sequence, at, payload, remote)
            VALUES ($quest, $kind, $machine, $sequence, $at, $payload, $remote)
            RETURNING position
            """;
        command.Parameters.AddWithValue("$quest", operation.Quest);
        command.Parameters.AddWithValue("$kind", KindText(operation.Kind));
        command.Parameters.AddWithValue("$machine", operation.Machine);
        command.Parameters.AddWithValue("$sequence", operation.Sequence);
        command.Parameters.AddWithValue("$at", operation.At.ToString("O"));
        command.Parameters.AddWithValue(
            "$payload", PayloadJson(operation.Note, operation.Published, operation.Attempted, operation.Dismisses));
        command.Parameters.AddWithValue("$remote", (object?)number ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    /// <summary>Where this store keeps an operation, by the machine and sequence that name it anywhere.</summary>
    private async Task<long?> PositionOfAsync(
        string machine, long sequence, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT position FROM quest_log WHERE machine = $machine AND sequence = $sequence";
        command.Parameters.AddWithValue("$machine", machine);
        command.Parameters.AddWithValue("$sequence", sequence);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is { } position and not DBNull
            ? Convert.ToInt64(position)
            : null;
    }

    /// <summary>
    /// Write a quest's row as its replay gives it. The row is a cache, so it is written whole: a
    /// replay is the only thing that decides what it says.
    /// </summary>
    private async Task WriteCacheAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quests (id, sender, receiver, title, body, status, note, filed, updated, workspace, links, attachments, then_steps, parent, conflicts)
            VALUES ($id, $sender, $receiver, $title, $body, $status, $note, $filed, $updated, $workspace, $links, $attachments, $then, $parent, $conflicts)
            ON CONFLICT (id) DO UPDATE SET
              sender = excluded.sender, receiver = excluded.receiver, title = excluded.title, body = excluded.body,
              status = excluded.status, note = excluded.note, filed = excluded.filed, updated = excluded.updated,
              workspace = excluded.workspace, links = excluded.links, attachments = excluded.attachments,
              then_steps = excluded.then_steps, parent = excluded.parent, conflicts = excluded.conflicts
            """;
        command.Parameters.AddWithValue("$conflicts", ConflictsJson(quest.Conflicts));
        command.Parameters.AddWithValue("$links", LinksJson(quest.Links));
        command.Parameters.AddWithValue("$attachments", AttachmentsJson(quest.Attachments));
        command.Parameters.AddWithValue("$then", StepsJson(quest.Then));
        command.Parameters.AddWithValue("$parent", (object?)quest.Parent ?? DBNull.Value);
        command.Parameters.AddWithValue("$workspace", quest.Workspace);
        command.Parameters.AddWithValue("$id", quest.Id);
        command.Parameters.AddWithValue("$sender", quest.From);
        command.Parameters.AddWithValue("$receiver", quest.To);
        command.Parameters.AddWithValue("$title", quest.Title);
        command.Parameters.AddWithValue("$body", quest.Body);
        command.Parameters.AddWithValue("$status", quest.Status.ToString());
        command.Parameters.AddWithValue("$note", (object?)quest.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$filed", quest.Filed.ToString("O"));
        command.Parameters.AddWithValue("$updated", quest.Updated.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A quest's history, in the order it replays (design §8): what a remote accepted, by its number,
    /// then what this machine has not pushed, in the order it was made. Empty for an unknown id.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id, CancellationToken ct = default) =>
        InGateAsync(() => HistoryAsync(id, transaction: null, ct), ct);

    private async Task<IReadOnlyList<QuestOperation>> HistoryAsync(
        string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {OperationColumns} FROM quest_log WHERE quest = $id
            ORDER BY remote IS NULL, remote, position
            """;
        command.Parameters.AddWithValue("$id", id);

        var history = new List<QuestOperation>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) history.Add(ReadOperation(reader));
        return history;
    }

    /// <summary>
    /// The chain's next step as the close of <paramref name="parent"/> publishes it: asked on behalf of
    /// the same asker, of the step's receiver, with <c>{parent}</c> expanded, carrying the rest.
    /// </summary>
    private static Quest? NextStep(Quest parent, DateTimeOffset now)
    {
        if (parent.Then.Count == 0) return null;
        var step = parent.Then[0];
        var title = step.Title.Replace("{parent}", $"#{parent.Id}", StringComparison.Ordinal);
        return new Quest(
            MakeId(parent.From, step.To, title, parent.Id), parent.From, step.To, title,
            step.Body.Replace("{parent}", $"#{parent.Id}", StringComparison.Ordinal),
            QuestStatus.Open, null, now, now, Workspace: parent.Workspace)
        {
            Then = parent.Then.Skip(1).ToList(),
            Parent = parent.Id,
        };
    }

    /// <summary>
    /// Move a quest to a new status, atomically, honouring the transition table. Declining without a
    /// reason is refused by the caller; an illegal move is refused HERE, in the store, because judging
    /// the replayed history inside the write transaction is what makes "the quest state machine is the
    /// only lock" true when two hosts' callers race over one file (D47 §5) — a check the caller ran a
    /// moment earlier decides nothing.
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call moved it; a null quest means no such id.
    /// A refused move returns the row unchanged, so the caller can name the state that refused it.
    /// </returns>
    public Task<QuestMove> MoveAsync(
        string id, QuestStatus status, string? note, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async transaction =>
        {
            var held = await FindAsync(id, transaction, ct).ConfigureAwait(false);

            // Every verb commits here, whichever machines share the quest (D68 §1) — and nothing moves
            // TO open.
            if (held is null || QuestTransitions.KindFor(status) is not { } kind)
            {
                return new QuestMove(held, Moved: false);
            }

            var history = await HistoryAsync(id, transaction, ct).ConfigureAwait(false);
            if (history.Count == 0)
            {
                // A row the log never saw — written by something older than the log since this store
                // was opened. It is given its history now, exactly as the open would have, and keeps it
                // even if the move is refused: that write is the store's, and true.
                history = await GiveHistoryAsync(held, transaction, ct).ConfigureAwait(false);
            }

            if (!QuestTransitions.Allows(QuestLog.Replay(history)!.Status, status))
            {
                return new QuestMove(held, Moved: false);
            }

            var operation = await AppendAsync(id, kind, now, note, null, transaction, ct).ConfigureAwait(false);
            var moved = QuestLog.Replay([.. history, operation])!;
            await WriteCacheAsync(moved, transaction, ct).ConfigureAwait(false);

            // A close that finishes a chain's step publishes the next one IN THE SAME TRANSACTION
            // (D65 §4): there is no moment at which the work is done and the chain lost, and a close
            // another host wins publishes nothing here. A step already published is the one that
            // stands — the id is the ask.
            Quest? followUp = null;
            if (status == QuestStatus.Done && NextStep(moved, now) is { } next)
            {
                followUp = await FindAsync(next.Id, transaction, ct).ConfigureAwait(false)
                           ?? await PublishInAsync(next, transaction, ct).ConfigureAwait(false);
            }

            return new QuestMove(moved, Moved: true, followUp);
        }, ct);

    /// <summary>
    /// A person dismisses a conflict (SYNC6c) — one named by its machine and sequence, or, naming none,
    /// every one the quest carries. Each is a <see cref="QuestOperationKind.Dismissed"/> operation,
    /// committed here and carried by the next pass like any other, so the conflict goes on every
    /// machine. It moves no status.
    /// </summary>
    /// <returns>The quest as it now stands (null: no such quest) and how many conflicts went.</returns>
    public Task<QuestDismissal> DismissAsync(
        string id, string? machine, long? sequence, DateTimeOffset now, CancellationToken ct = default) =>
        InTransactionAsync(async transaction =>
        {
            var history = await HistoryAsync(id, transaction, ct).ConfigureAwait(false);
            if (QuestLog.Replay(history) is not { } quest)
            {
                // No such quest — or a row the log never saw, which has raced nobody and carries none.
                return new QuestDismissal(await FindAsync(id, transaction, ct).ConfigureAwait(false), 0);
            }

            var named = quest.Conflicts
                .Where(conflict => machine is null
                    || (conflict.Machine == machine && (sequence is null || conflict.Sequence == sequence)))
                .ToList();
            var written = new List<QuestOperation>(history);
            foreach (var conflict in named)
            {
                written.Add(await AppendAsync(
                    id, QuestOperationKind.Dismissed, now, note: null, published: null, transaction, ct,
                    new QuestOperationRef(conflict.Machine, conflict.Sequence)).ConfigureAwait(false));
            }

            var standing = QuestLog.Replay(written)!;
            if (named.Count > 0) await WriteCacheAsync(standing, transaction, ct).ConfigureAwait(false);
            return new QuestDismissal(standing, named.Count);
        }, ct);

    // ——— A machine's half of the sync (D68 §3, design §8).

    /// <summary>The last number this machine fetched from a workspace's remote — zero before its first fetch.</summary>
    public Task<long> CursorAsync(string workspace, CancellationToken ct = default) =>
        InGateAsync(() => CursorAsync(workspace, transaction: null, ct), ct);

    private async Task<long> CursorAsync(string workspace, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT number FROM quest_cursor WHERE workspace = $workspace";
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is { } number and not DBNull
            ? Convert.ToInt64(number)
            : 0;
    }

    /// <summary>
    /// Take what a workspace's remote accepted, in its order, and rebase what this machine has not
    /// pushed on top — through the one table. Answers the cursor it now stands at and every move that
    /// became a conflict on the way.
    /// </summary>
    /// <param name="workspace">The circle whose remote these came from: what a publish among them is filed under here (SYNC0a).</param>
    /// <param name="fetched">The remote's operations, each carrying its number.</param>
    /// <param name="through">The last number the fetch covered; the cursor moves there.</param>
    /// <remarks>
    /// An operation this machine already holds — its own, pushed and fetched back — takes the number it
    /// was given; any other is kept under the machine that made it. Then every quest the fetch touched
    /// is replayed: accepted operations by number, then pending ones. A pending move that no longer
    /// applies becomes a <see cref="QuestOperationKind.Conflict"/> and is never dropped (design §5).
    /// </remarks>
    public Task<QuestIntegration> IntegrateAsync(
        string workspace, IReadOnlyList<QuestOperation> fetched, long through, CancellationToken ct = default) =>
        InTransactionAsync(async transaction =>
        {
            var circle = Workspaces.Normalize(workspace);
            var touched = new List<string>();
            foreach (var operation in fetched.Where(o => o.Number is not null).OrderBy(o => o.Number))
            {
                if (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, ct).ConfigureAwait(false)
                    is { } held)
                {
                    await NumberAsync(held, operation.Number!.Value, transaction, ct).ConfigureAwait(false);
                }
                else
                {
                    var filed = operation.Published is null
                        ? operation
                        : operation with { Published = operation.Published with { Workspace = circle } };
                    await KeepAsync(filed, operation.Number, transaction, ct).ConfigureAwait(false);
                }

                if (!touched.Contains(operation.Quest, StringComparer.Ordinal)) touched.Add(operation.Quest);
            }

            var conflicts = new List<QuestOperation>();
            foreach (var quest in touched)
            {
                conflicts.AddRange(await RebaseAsync(quest, transaction, ct).ConfigureAwait(false));
            }

            var cursor = Math.Max(await CursorAsync(circle, transaction, ct).ConfigureAwait(false), through);
            await using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO quest_cursor (workspace, number) VALUES ($workspace, $number)
                    ON CONFLICT (workspace) DO UPDATE SET number = excluded.number
                    """;
                command.Parameters.AddWithValue("$workspace", circle);
                command.Parameters.AddWithValue("$number", cursor);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            return new QuestIntegration(cursor, conflicts);
        }, ct);

    /// <summary>
    /// Replay one quest — accepted first, pending on top — and rewrite what did not survive: a move
    /// becomes a conflict, a publish of an ask already held is the same ask again, and a follow-up
    /// that only a lost close published goes with it. Answers the conflicts made.
    /// </summary>
    private async Task<IReadOnlyList<QuestOperation>> RebaseAsync(
        string id, SqliteTransaction transaction, CancellationToken ct)
    {
        var conflicts = new List<QuestOperation>();
        Quest? quest = null;

        // Once this machine's take has lost, its later moves on the quest were made on a claim it never
        // held (D69): an offline session that finished would otherwise close the quest over the winner's
        // take, because the table allows done from taken. Every pending operation is this machine's.
        var claimLost = false;
        foreach (var operation in await HistoryAsync(id, transaction, ct).ConfigureAwait(false))
        {
            var lostClaim = claimLost && operation.Number is null && QuestTransitions.Target(operation.Kind) is not null;
            if (!lostClaim && (operation.Number is not null || QuestLog.Applies(quest, operation)))
            {
                quest = QuestLog.Applies(quest, operation) ? QuestLog.Step(quest, operation) : quest;
                continue;
            }

            var position = (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, ct)
                .ConfigureAwait(false))!.Value;
            if (operation.Kind == QuestOperationKind.Published)
            {
                // The same ask, published first elsewhere: the first publish is the quest, as it
                // always was, and a second copy was never anybody's decision.
                await ForgetAsync(position, transaction, ct).ConfigureAwait(false);
                continue;
            }

            var lost = operation with
            {
                Kind = QuestOperationKind.Conflict, Attempted = QuestTransitions.Target(operation.Kind),
            };
            await RewriteAsync(position, lost, transaction, ct).ConfigureAwait(false);
            quest = QuestLog.Step(quest, lost);
            conflicts.Add(lost);
            claimLost |= operation.Kind == QuestOperationKind.Taken;

            if (operation.Kind == QuestOperationKind.Done)
            {
                await ForgetFollowUpsAsync(id, transaction, ct).ConfigureAwait(false);
            }
        }

        if (quest is not null) await WriteCacheAsync(quest, transaction, ct).ConfigureAwait(false);
        return conflicts;
    }

    /// <summary>
    /// A follow-up published only by a close that has now lost (D65 §4): it was the chain moving on,
    /// and the chain did not move. Forgotten only while nothing else has happened to it — a follow-up
    /// somebody has acted on, or that another machine holds, stays.
    /// </summary>
    private async Task ForgetFollowUpsAsync(string parent, SqliteTransaction transaction, CancellationToken ct)
    {
        var children = new List<string>();
        await using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT id FROM quests WHERE parent = $parent";
            command.Parameters.AddWithValue("$parent", parent);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) children.Add(reader.GetString(0));
        }

        foreach (var child in children)
        {
            var history = await HistoryAsync(child, transaction, ct).ConfigureAwait(false);
            if (history is not [{ Kind: QuestOperationKind.Published, Number: null } only]) continue;

            await ForgetAsync(
                (await PositionOfAsync(only.Machine, only.Sequence, transaction, ct).ConfigureAwait(false))!.Value,
                transaction, ct).ConfigureAwait(false);
            await using var drop = _connection.CreateCommand();
            drop.Transaction = transaction;
            drop.CommandText = "DELETE FROM quests WHERE id = $id";
            drop.Parameters.AddWithValue("$id", child);
            await drop.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task NumberAsync(long position, long number, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE quest_log SET remote = $number WHERE position = $position";
        command.Parameters.AddWithValue("$number", number);
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task ForgetAsync(long position, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM quest_log WHERE position = $position AND remote IS NULL";
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Rewrite a pending operation in place — a rebase rewriting a commit nobody else has seen.</summary>
    private async Task RewriteAsync(
        long position, QuestOperation operation, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE quest_log SET kind = $kind, payload = $payload WHERE position = $position AND remote IS NULL";
        command.Parameters.AddWithValue("$kind", KindText(operation.Kind));
        command.Parameters.AddWithValue(
            "$payload", PayloadJson(operation.Note, operation.Published, operation.Attempted));
        command.Parameters.AddWithValue("$position", position);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What this machine has not pushed for a workspace's quests, in the order it was made — only for
    /// quests whose receiver <paramref name="shared"/> says may leave the machine (design §8).
    /// </summary>
    /// <param name="shared">
    /// Whether a receiver is joined in this workspace — here or on a teammate's machine. Silence means
    /// local: a quest to anything else never appears here, however long it waits.
    /// </param>
    public Task<IReadOnlyList<QuestOperation>> PendingAsync(
        string workspace, Func<string, bool> shared, CancellationToken ct = default) =>
        InGateAsync<IReadOnlyList<QuestOperation>>(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = $"""
                SELECT {OperationColumns}, quests.receiver FROM quest_log
                JOIN quests ON quests.id = quest_log.quest
                WHERE quest_log.remote IS NULL AND quests.workspace = $workspace COLLATE NOCASE
                ORDER BY quest_log.position
                """;
            command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));

            var pending = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (shared(reader.GetString(7))) pending.Add(ReadOperation(reader));
            }

            return pending;
        }, ct);

    /// <summary>
    /// Where THIS machine's claim on a quest stands (D68 §4, D69): held once a remote numbered its take,
    /// unconfirmed while the take is only here, lost once the rebase made it a conflict — and none when
    /// this machine never took it. A take on a machine with no remote is never numbered, so it stays
    /// unconfirmed, which nothing acts on.
    /// </summary>
    public Task<QuestClaim> ClaimAsync(string id, CancellationToken ct = default) =>
        InGateAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = $"SELECT {OperationColumns} FROM quest_log WHERE quest = $id AND machine = $machine";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$machine", Machine);

            var claim = QuestClaim.None;
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var operation = ReadOperation(reader);
                if (operation.Kind == QuestOperationKind.Taken)
                {
                    return operation.Number is null ? QuestClaim.Unconfirmed : QuestClaim.Held;
                }

                if (operation is { Kind: QuestOperationKind.Conflict, Attempted: QuestStatus.Taken }) claim = QuestClaim.Lost;
            }

            return claim;
        }, ct);

    /// <summary>
    /// How a circle's pass ended (SYNC6a). A pass that reached the remote moves <see cref="QuestStanding.Synced"/>;
    /// one that hit a wall keeps it and names the wall, because when the circle last synced and why
    /// the last try did not are two facts.
    /// </summary>
    public Task RecordPassAsync(string workspace, QuestSyncReport report, DateTimeOffset at, CancellationToken ct = default) =>
        InTransactionAsync(async transaction =>
        {
            await using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO quest_passes (workspace, synced, tried, behind, problem)
                VALUES ($workspace, $synced, $tried, $behind, $problem)
                ON CONFLICT (workspace) DO UPDATE SET
                  synced = COALESCE($synced, synced), tried = $tried, behind = $behind, problem = $problem
                """;
            command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
            command.Parameters.AddWithValue("$synced", report.Problem is null ? at.ToString("O") : DBNull.Value);
            command.Parameters.AddWithValue("$tried", at.ToString("O"));
            // A list of quest ids, kept the way links are: a plain array of strings.
            command.Parameters.AddWithValue("$behind", LinksJson(report.Behind));
            command.Parameters.AddWithValue("$problem", (object?)report.Problem ?? DBNull.Value);
            return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    /// <summary>
    /// Where a circle stands (SYNC6a): what this machine has not pushed, the quests carrying a conflict,
    /// and how its last pass ended.
    /// </summary>
    /// <param name="shared">Whether a receiver may leave the machine — what <see cref="PendingAsync"/> reads.
    /// What never leaves is not ahead of anything.</param>
    public async Task<QuestStanding> StandingAsync(
        string workspace, Func<string, bool> shared, CancellationToken ct = default)
    {
        var circle = Workspaces.Normalize(workspace);
        var ahead = (await PendingAsync(circle, shared, ct).ConfigureAwait(false)).Count;
        return await InGateAsync(async () =>
        {
            var conflicts = new List<string>();
            await using (var command = _connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT id FROM quests WHERE workspace = $workspace COLLATE NOCASE AND conflicts <> '[]' ORDER BY updated DESC";
                command.Parameters.AddWithValue("$workspace", circle);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) conflicts.Add(reader.GetString(0));
            }

            await using (var command = _connection.CreateCommand())
            {
                command.CommandText = "SELECT synced, tried, behind, problem FROM quest_passes WHERE workspace = $workspace";
                command.Parameters.AddWithValue("$workspace", circle);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return new QuestStanding(
                        ahead,
                        ReadLinks(reader.GetString(2)),
                        conflicts,
                        reader.IsDBNull(0) ? null : Time(reader.GetString(0)),
                        Time(reader.GetString(1)),
                        reader.IsDBNull(3) ? null : reader.GetString(3));
                }
            }

            return new QuestStanding(ahead, [], conflicts, Synced: null, Tried: null, Problem: null);
        }, ct).ConfigureAwait(false);
    }

    private static DateTimeOffset Time(string text) =>
        DateTimeOffset.Parse(text, null, System.Globalization.DateTimeStyles.RoundtripKind);

    /// <summary>Record the numbers a push was given — the operations stop being pending.</summary>
    public Task AcceptedAsync(IReadOnlyList<QuestAcceptance> accepted, CancellationToken ct = default) =>
        InTransactionAsync(async transaction =>
        {
            foreach (var acceptance in accepted)
            {
                if (await PositionOfAsync(acceptance.Machine, acceptance.Sequence, transaction, ct).ConfigureAwait(false)
                    is { } position)
                {
                    await NumberAsync(position, acceptance.Number, transaction, ct).ConfigureAwait(false);
                }
            }

            return accepted.Count;
        }, ct);

    // ——— A remote's half.

    /// <summary>What this store accepted after <paramref name="since"/>, in order — numbered by position.</summary>
    /// <param name="limit">A page; <see cref="QuestFetch.More"/> says whether another follows.</param>
    public Task<QuestFetch> OperationsSinceAsync(long since, int limit = 500, CancellationToken ct = default) =>
        InGateAsync(async () =>
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = $"""
                SELECT {OperationColumns}, position FROM quest_log
                WHERE position > $since ORDER BY position LIMIT $take
                """;
            command.Parameters.AddWithValue("$since", since);
            command.Parameters.AddWithValue("$take", limit + 1);

            var operations = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                operations.Add(ReadOperation(reader) with { Number = reader.GetInt64(7) });
            }

            var more = operations.Count > limit;
            var page = more ? operations[..limit] : operations;
            return new QuestFetch(page, page.Count == 0 ? since : page[^1].Number!.Value, more);
        }, ct);

    /// <summary>
    /// Judge a push, quest by quest (design §8): <b>behind</b> when anything reached the quest after
    /// <paramref name="base"/> that this push did not carry; <b>refused</b> when an operation does not
    /// apply through the table or a publish fails <paramref name="judge"/>; otherwise every operation is
    /// kept under the machine that made it and numbered.
    /// </summary>
    /// <param name="base">The number the pushing machine rebased on — its cursor.</param>
    /// <param name="judge">The exchange's say over a publish, null when it is fit.</param>
    /// <param name="workspaceOf">Where a publish is filed HERE — the receiving side's wiring, never the push's (SYNC0a).</param>
    public Task<QuestPush> ReceiveAsync(
        long @base, IReadOnlyList<QuestOperation> pushed, Func<Quest, string?> judge, Func<Quest, string> workspaceOf,
        CancellationToken ct = default) =>
        InTransactionAsync(async transaction =>
        {
            var accepted = new List<QuestAcceptance>();
            var behind = new List<string>();
            var refused = new List<QuestPushRefusal>();
            var carried = pushed.Select(o => (o.Machine, o.Sequence)).ToHashSet();

            foreach (var group in pushed.GroupBy(o => o.Quest, StringComparer.Ordinal))
            {
                var fresh = new List<QuestOperation>();
                foreach (var operation in group)
                {
                    // Already here — a push retried after its answer was lost: the number it was given.
                    if (await PositionOfAsync(operation.Machine, operation.Sequence, transaction, ct).ConfigureAwait(false)
                        is { } held)
                    {
                        accepted.Add(new(operation.Machine, operation.Sequence, held));
                    }
                    else
                    {
                        fresh.Add(operation);
                    }
                }

                if (fresh.Count == 0) continue;
                if (await MovedSinceAsync(group.Key, @base, carried, transaction, ct).ConfigureAwait(false))
                {
                    behind.Add(group.Key);
                    continue;
                }

                var quest = QuestLog.Replay(await HistoryAsync(group.Key, transaction, ct).ConfigureAwait(false));
                string? why = null;
                var staged = new List<QuestOperation>();
                foreach (var operation in fresh)
                {
                    var filed = operation.Published is null
                        ? operation
                        : operation with { Published = operation.Published with { Workspace = workspaceOf(operation.Published) } };
                    why = filed.Published is { } asked ? judge(asked) : null;
                    if (why is null && !QuestLog.Applies(quest, filed))
                    {
                        why = quest is null
                            ? $"`{KindText(filed.Kind)}` on quest `#{group.Key}`, which this deployment has never had published."
                            : $"`{KindText(filed.Kind)}` does not apply to quest `#{group.Key}`, which is {quest.Status}.";
                    }

                    if (why is not null) break;
                    quest = QuestLog.Step(quest, filed);
                    staged.Add(filed);
                }

                if (why is not null)
                {
                    refused.Add(new(group.Key, why));
                    continue;
                }

                foreach (var operation in staged)
                {
                    var number = await KeepAsync(operation, number: null, transaction, ct).ConfigureAwait(false);
                    accepted.Add(new(operation.Machine, operation.Sequence, number));
                }

                await WriteCacheAsync(quest!, transaction, ct).ConfigureAwait(false);
            }

            return new QuestPush(accepted, behind, refused);
        }, ct);

    /// <summary>Whether anything this push did not carry reached a quest after <paramref name="base"/>.</summary>
    private async Task<bool> MovedSinceAsync(
        string quest, long @base, IReadOnlySet<(string Machine, long Sequence)> carried,
        SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT machine, sequence FROM quest_log WHERE quest = $quest AND position > $base";
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$base", @base);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!carried.Contains((reader.GetString(0), reader.GetInt64(1)))) return true;
        }

        return false;
    }

    /// <summary>
    /// A read the sync depends on, taken inside the connection's gate: a statement run while another
    /// request's transaction is open joins it (<see cref="InTransactionAsync{T}"/>), and a pending
    /// operation read from a transaction that then rolled back would be pushed as if it existed.
    /// </summary>
    private async Task<T> InGateAsync<T>(Func<Task<T>> read, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await read().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The columns <see cref="ReadOperation"/> reads, in its order.</summary>
    private const string OperationColumns =
        "quest_log.quest, quest_log.kind, quest_log.machine, quest_log.sequence, quest_log.at, quest_log.payload, quest_log.remote";

    public Task<Quest?> FindAsync(string id, CancellationToken ct = default) =>
        FindAsync(id, transaction: null, ct);

    private async Task<Quest?> FindAsync(string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM quests WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// Quests addressed to one repository, or every quest when none is named.
    /// </summary>
    /// <remarks>
    /// Open and taken first: what is outstanding is the question worth asking, and a finished queue
    /// pushing live work off the end is how a list stops being read.
    /// </remarks>
    public async Task<IReadOnlyList<Quest>> ListAsync(
        string? receiver = null, bool includeClosed = false, string? workspace = null,
        CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            SELECT * FROM quests
            WHERE ($receiver IS NULL OR receiver = $receiver)
              AND ($workspace IS NULL OR workspace = $workspace COLLATE NOCASE)
              {(includeClosed ? "" : "AND status IN ('Open', 'Taken')")}
            ORDER BY CASE status WHEN 'Open' THEN 0 WHEN 'Taken' THEN 1 ELSE 2 END, filed
            """;
        command.Parameters.AddWithValue("$receiver", (object?)receiver ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$workspace", workspace is null ? DBNull.Value : Workspaces.Normalize(workspace));

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }

    private static Quest Read(SqliteDataReader reader) => new(
        reader.GetString(reader.GetOrdinal("id")),
        reader.GetString(reader.GetOrdinal("sender")),
        reader.GetString(reader.GetOrdinal("receiver")),
        reader.GetString(reader.GetOrdinal("title")),
        reader.GetString(reader.GetOrdinal("body")),
        Enum.Parse<QuestStatus>(reader.GetString(reader.GetOrdinal("status"))),
        reader.IsDBNull(reader.GetOrdinal("note")) ? null : reader.GetString(reader.GetOrdinal("note")),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("filed"))),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("updated"))),
        Workspaces.Normalize(reader.GetString(reader.GetOrdinal("workspace"))))
    {
        Links = ReadLinks(reader.GetString(reader.GetOrdinal("links"))),
        Attachments = ReadAttachments(reader.GetString(reader.GetOrdinal("attachments"))),
        Then = ReadSteps(reader.GetString(reader.GetOrdinal("then_steps"))),
        Parent = reader.IsDBNull(reader.GetOrdinal("parent")) ? null : reader.GetString(reader.GetOrdinal("parent")),
        Conflicts = ReadConflicts(reader.GetString(reader.GetOrdinal("conflicts"))),
    };

    /// <summary>An operation as its log row holds it (<see cref="OperationColumns"/>) — a publish's payload is the quest as asked.</summary>
    private static QuestOperation ReadOperation(SqliteDataReader reader)
    {
        var quest = reader.GetString(0);
        var kind = Enum.Parse<QuestOperationKind>(reader.GetString(1), ignoreCase: true);
        var at = DateTimeOffset.Parse(reader.GetString(4));

        using var document = System.Text.Json.JsonDocument.Parse(reader.GetString(5));
        var payload = document.RootElement;
        var note = payload.TryGetProperty("note", out var said) ? said.GetString() : null;
        QuestStatus? attempted = payload.TryGetProperty("attempted", out var tried)
            ? Enum.Parse<QuestStatus>(tried.GetString()!, ignoreCase: true)
            : null;
        var published = kind != QuestOperationKind.Published
            ? null
            : new Quest(
                quest,
                payload.GetProperty("from").GetString() ?? "",
                payload.GetProperty("to").GetString() ?? "",
                payload.GetProperty("title").GetString() ?? "",
                payload.GetProperty("body").GetString() ?? "",
                QuestStatus.Open, null, at, at,
                Workspace: Workspaces.Normalize(payload.GetProperty("workspace").GetString()))
            {
                Links = ReadLinks(payload.GetProperty("links")),
                Attachments = ReadAttachments(payload.GetProperty("attachments")),
                Then = ReadSteps(payload.GetProperty("then")),
                Parent = payload.TryGetProperty("parent", out var parent) ? parent.GetString() : null,
            };

        QuestOperationRef? dismisses = payload.TryGetProperty("dismisses", out var named)
            ? new QuestOperationRef(named.GetProperty("machine").GetString() ?? "", named.GetProperty("sequence").GetInt64())
            : null;

        return new QuestOperation(
            quest, kind, reader.GetString(2), reader.GetInt64(3), at, note, published, attempted,
            reader.IsDBNull(6) ? null : reader.GetInt64(6), dismisses);
    }

    /// <summary>How a kind is written in the log: its name in lowercase, as the design names it.</summary>
    private static string KindText(QuestOperationKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>
    /// What an operation carries. A publish carries the quest's words and everything it carries — all
    /// a replay needs to make the quest, on this machine or another — and a move carries its note.
    /// </summary>
    private static string PayloadJson(
        string? note, Quest? published, QuestStatus? attempted, QuestOperationRef? dismisses = null) => Json(writer =>
    {
        if (dismisses is not null)
        {
            // A dismissal carries the conflict it names (SYNC6c), and nothing else.
            writer.WriteStartObject();
            writer.WriteStartObject("dismisses");
            writer.WriteString("machine", dismisses.Machine);
            writer.WriteNumber("sequence", dismisses.Sequence);
            writer.WriteEndObject();
            writer.WriteEndObject();
            return;
        }

        if (attempted is { } lost)
        {
            writer.WriteStartObject();
            writer.WriteString("attempted", lost.ToString());
            if (note is not null) writer.WriteString("note", note);
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        if (published is not null)
        {
            writer.WriteString("from", published.From);
            writer.WriteString("to", published.To);
            writer.WriteString("title", published.Title);
            writer.WriteString("body", published.Body);
            writer.WriteString("workspace", published.Workspace);
            writer.WritePropertyName("links");
            WriteLinks(writer, published.Links);
            writer.WritePropertyName("attachments");
            WriteAttachments(writer, published.Attachments);
            writer.WritePropertyName("then");
            WriteSteps(writer, published.Then);
            if (published.Parent is not null) writer.WriteString("parent", published.Parent);
        }

        if (note is not null) writer.WriteString("note", note);
        writer.WriteEndObject();
    });

    private static string ConflictsJson(IReadOnlyList<QuestConflict> conflicts) => Json(writer =>
    {
        writer.WriteStartArray();
        foreach (var conflict in conflicts)
        {
            writer.WriteStartObject();
            writer.WriteString("machine", conflict.Machine);
            writer.WriteNumber("sequence", conflict.Sequence);
            writer.WriteString("attempted", conflict.Attempted.ToString());
            if (conflict.Note is not null) writer.WriteString("note", conflict.Note);
            writer.WriteString("at", conflict.At.ToString("O"));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    private static IReadOnlyList<QuestConflict> ReadConflicts(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => new QuestConflict(
                item.GetProperty("machine").GetString() ?? "",
                Enum.Parse<QuestStatus>(item.GetProperty("attempted").GetString()!, ignoreCase: true),
                item.TryGetProperty("note", out var note) ? note.GetString() : null,
                DateTimeOffset.Parse(item.GetProperty("at").GetString()!),
                // Absent from a cache written before dismissals: zero, which the open refills.
                item.TryGetProperty("sequence", out var sequence) ? sequence.GetInt64() : 0))
            .ToList();
    }

    private static string StepsJson(IReadOnlyList<QuestStep> steps) => Json(writer => WriteSteps(writer, steps));

    private static void WriteSteps(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<QuestStep> steps)
    {
        writer.WriteStartArray();
        foreach (var step in steps)
        {
            writer.WriteStartObject();
            writer.WriteString("to", step.To);
            writer.WriteString("title", step.Title);
            writer.WriteString("body", step.Body);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static IReadOnlyList<QuestStep> ReadSteps(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadSteps(document.RootElement);
    }

    private static IReadOnlyList<QuestStep> ReadSteps(System.Text.Json.JsonElement steps) =>
        steps.EnumerateArray()
            .Select(item => new QuestStep(
                item.GetProperty("to").GetString() ?? "",
                item.GetProperty("title").GetString() ?? "",
                item.GetProperty("body").GetString() ?? ""))
            .ToList();

    // Written and read by hand rather than through the reflection serializer, for the same reason the
    // registration store's lists are: nothing here may quietly stop working under AOT.
    private static string LinksJson(IReadOnlyList<string> links) => Json(writer => WriteLinks(writer, links));

    private static void WriteLinks(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<string> links)
    {
        writer.WriteStartArray();
        foreach (var link in links) writer.WriteStringValue(link);
        writer.WriteEndArray();
    }

    private static string AttachmentsJson(IReadOnlyList<QuestAttachment> attachments) =>
        Json(writer => WriteAttachments(writer, attachments));

    private static void WriteAttachments(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<QuestAttachment> attachments)
    {
        writer.WriteStartArray();
        foreach (var attachment in attachments)
        {
            writer.WriteStartObject();
            writer.WriteString("name", attachment.Name);
            writer.WriteString("sha256", attachment.Sha256);
            writer.WriteNumber("bytes", attachment.Bytes);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string Json(Action<System.Text.Json.Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream)) write(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static IReadOnlyList<string> ReadLinks(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadLinks(document.RootElement);
    }

    private static IReadOnlyList<string> ReadLinks(System.Text.Json.JsonElement links) =>
        links.EnumerateArray()
            .Select(item => item.GetString())
            .OfType<string>()
            .ToList();

    private static IReadOnlyList<QuestAttachment> ReadAttachments(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadAttachments(document.RootElement);
    }

    private static IReadOnlyList<QuestAttachment> ReadAttachments(System.Text.Json.JsonElement attachments) =>
        attachments.EnumerateArray()
            .Select(item => new QuestAttachment(
                item.GetProperty("name").GetString() ?? "",
                item.GetProperty("sha256").GetString() ?? "",
                item.GetProperty("bytes").GetInt64()))
            .ToList();
}
