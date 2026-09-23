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
/// <param name="Home">
/// Where this quest LIVES, when that is not here (D47 §5). Null is the normal case: a quest of this
/// store's own. Non-null marks a mirror row — a copy of another store's authority, kept for reading
/// and planning — and a mirror never moves locally: transitions happen at the home, and the next
/// mirror carries the result back. One home per quest is what makes reconciliation a non-problem.
/// </param>
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
    string? Home = null,
    string Workspace = Workspaces.Default)
{
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
/// the replay, never a second truth beside it. A mirror row is the one exception: it is another
/// store's record, and nothing happened to it here.</para>
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
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using (var command = _connection.CreateCommand())
        {
            // The log's position is this store's order of appending, and so the order a history
            // replays in. Machine + sequence names one operation anywhere; position names it here.
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS quest_log (
                  position  INTEGER PRIMARY KEY,
                  quest     TEXT NOT NULL,
                  kind      TEXT NOT NULL,
                  machine   TEXT NOT NULL,
                  sequence  INTEGER NOT NULL,
                  at        TEXT NOT NULL,
                  payload   TEXT NOT NULL,
                  UNIQUE (machine, sequence)
                );
                CREATE INDEX IF NOT EXISTS quest_log_quest ON quest_log (quest, position);
                CREATE TABLE IF NOT EXISTS quest_machine (
                  one INTEGER PRIMARY KEY CHECK (one = 1),
                  id  TEXT NOT NULL
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
                  home        TEXT NULL,
                  workspace   TEXT NOT NULL DEFAULT '{Workspaces.Default}',
                  links       TEXT NOT NULL DEFAULT '[]',
                  attachments TEXT NOT NULL DEFAULT '[]',
                  then_steps  TEXT NOT NULL DEFAULT '[]',
                  parent      TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS quests_receiver ON quests (receiver, status);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // A store created before the remote existed has no home column; one from before workspaces has
        // no workspace; one from before quests carried anything (D65) has neither list. Their quests
        // must survive the upgrade — home NULL, the one workspace there was, and nothing carried, which
        // is exactly right, because everything in them was its own and carried nothing.
        foreach (var (column, definition) in new[]
        {
            ("home", "home TEXT NULL"),
            ("workspace", $"workspace TEXT NOT NULL DEFAULT '{Workspaces.Default}'"),
            ("links", "links TEXT NOT NULL DEFAULT '[]'"),
            ("attachments", "attachments TEXT NOT NULL DEFAULT '[]'"),
            // A chain (D65 §4): `then` is an SQL keyword, so the column says what it holds.
            ("then_steps", "then_steps TEXT NOT NULL DEFAULT '[]'"),
            ("parent", "parent TEXT NULL"),
        })
        {
            await using var probe = _connection.CreateCommand();
            probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info('quests') WHERE name = $name";
            probe.Parameters.AddWithValue("$name", column);
            var present = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false));
            if (present == 0)
            {
                await using var alter = _connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE quests ADD COLUMN {definition}";
                await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
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
    /// because two hosts open one store and only one of them may write the histories. A mirror row is
    /// left alone: it is its home's record, and nothing happened to it here.
    /// </remarks>
    private async Task GiveHistoriesAsync(CancellationToken ct)
    {
        const string Unlogged = """
            SELECT * FROM quests
            WHERE home IS NULL AND NOT EXISTS (SELECT 1 FROM quest_log WHERE quest_log.quest = quests.id)
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
        SqliteTransaction transaction, CancellationToken ct)
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
        command.Parameters.AddWithValue("$payload", PayloadJson(note, published));
        var sequence = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));

        // Handed back as it will read back: a publish carries the quest as asked, open from this moment.
        return new QuestOperation(
            quest, kind, Machine, sequence, at, note,
            published is null ? null : published with { Status = QuestStatus.Open, Note = null, Filed = at, Updated = at, Home = null });
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
            INSERT INTO quests (id, sender, receiver, title, body, status, note, filed, updated, home, workspace, links, attachments, then_steps, parent)
            VALUES ($id, $sender, $receiver, $title, $body, $status, $note, $filed, $updated, NULL, $workspace, $links, $attachments, $then, $parent)
            ON CONFLICT (id) DO UPDATE SET
              sender = excluded.sender, receiver = excluded.receiver, title = excluded.title, body = excluded.body,
              status = excluded.status, note = excluded.note, filed = excluded.filed, updated = excluded.updated,
              home = NULL, workspace = excluded.workspace, links = excluded.links, attachments = excluded.attachments,
              then_steps = excluded.then_steps, parent = excluded.parent
            """;
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

    /// <summary>A quest's history, in the order it replays — empty for a mirror row or an unknown id.</summary>
    public Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id, CancellationToken ct = default) =>
        HistoryAsync(id, transaction: null, ct);

    private async Task<IReadOnlyList<QuestOperation>> HistoryAsync(
        string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT quest, kind, machine, sequence, at, payload FROM quest_log WHERE quest = $id ORDER BY position";
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

            // A mirror row never moves here at all: its transitions happen at its home, and only the
            // next mirror writes the result back (D47 §5). And nothing moves TO open.
            if (held is null || held.Home is not null || QuestTransitions.KindFor(status) is not { } kind)
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
    /// Copy another store's quest into this one, whole. The row is marked with its home, which is what
    /// makes it immovable locally — a mirror renders and plans; it never decides (D47 §5). Idempotent
    /// by the content-derived id: mirroring the same quest again is an update, never a duplicate.
    /// </summary>
    public async Task MirrorAsync(Quest quest, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        // What a quest carries is fixed at publish, like its words — but a mirror row written by a
        // version that did not know about carrying has nothing, so the home's record overwrites it.
        command.CommandText = """
            INSERT INTO quests (id, sender, receiver, title, body, status, note, filed, updated, home, workspace, links, attachments, then_steps, parent)
            VALUES ($id, $sender, $receiver, $title, $body, $status, $note, $filed, $updated, $home, $workspace, $links, $attachments, $then, $parent)
            ON CONFLICT (id) DO UPDATE SET
              status = $status, note = $note, updated = $updated, home = $home, workspace = $workspace,
              links = $links, attachments = $attachments, then_steps = $then, parent = $parent
            """;
        command.Parameters.AddWithValue("$links", LinksJson(quest.Links));
        command.Parameters.AddWithValue("$attachments", AttachmentsJson(quest.Attachments));
        command.Parameters.AddWithValue("$then", StepsJson(quest.Then));
        command.Parameters.AddWithValue("$parent", (object?)quest.Parent ?? DBNull.Value);
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(quest.Workspace));
        command.Parameters.AddWithValue("$id", quest.Id);
        command.Parameters.AddWithValue("$sender", quest.From);
        command.Parameters.AddWithValue("$receiver", quest.To);
        command.Parameters.AddWithValue("$title", quest.Title);
        command.Parameters.AddWithValue("$body", quest.Body);
        command.Parameters.AddWithValue("$status", quest.Status.ToString());
        command.Parameters.AddWithValue("$note", (object?)quest.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$filed", quest.Filed.ToString("O"));
        command.Parameters.AddWithValue("$updated", quest.Updated.ToString("O"));
        command.Parameters.AddWithValue("$home", (object?)quest.Home ?? "remote");
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

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
        reader.IsDBNull(reader.GetOrdinal("home")) ? null : reader.GetString(reader.GetOrdinal("home")),
        Workspaces.Normalize(reader.GetString(reader.GetOrdinal("workspace"))))
    {
        Links = ReadLinks(reader.GetString(reader.GetOrdinal("links"))),
        Attachments = ReadAttachments(reader.GetString(reader.GetOrdinal("attachments"))),
        Then = ReadSteps(reader.GetString(reader.GetOrdinal("then_steps"))),
        Parent = reader.IsDBNull(reader.GetOrdinal("parent")) ? null : reader.GetString(reader.GetOrdinal("parent")),
    };

    /// <summary>An operation as its log row holds it — a publish's payload is the quest as asked.</summary>
    private static QuestOperation ReadOperation(SqliteDataReader reader)
    {
        var quest = reader.GetString(0);
        var kind = Enum.Parse<QuestOperationKind>(reader.GetString(1), ignoreCase: true);
        var at = DateTimeOffset.Parse(reader.GetString(4));

        using var document = System.Text.Json.JsonDocument.Parse(reader.GetString(5));
        var payload = document.RootElement;
        var note = payload.TryGetProperty("note", out var said) ? said.GetString() : null;
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

        return new QuestOperation(quest, kind, reader.GetString(2), reader.GetInt64(3), at, note, published);
    }

    /// <summary>How a kind is written in the log: its name in lowercase, as the design names it.</summary>
    private static string KindText(QuestOperationKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>
    /// What an operation carries. A publish carries the quest's words and everything it carries — all
    /// a replay needs to make the quest, on this machine or another — and a move carries its note.
    /// </summary>
    private static string PayloadJson(string? note, Quest? published) => Json(writer =>
    {
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
