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
/// </remarks>
public sealed class QuestStore
{
    private readonly SqliteConnection _connection;

    private QuestStore(SqliteConnection connection) => _connection = connection;

    public static async Task<QuestStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new QuestStore(connection);
        await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
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
    /// A short handle derived from who asked, of whom, and for what.
    /// </summary>
    /// <remarks>
    /// Content-derived so publishing the same quest twice collides rather than multiplying — an agent
    /// that retries should not produce a second copy of the same ask. A chain's step also derives from
    /// its PARENT (D65 §4): "Verify in the browser" is a title many chains will use, and a step that
    /// collided with an earlier quest of those words would quietly join somebody else's closed quest.
    /// A quest with no parent keeps exactly the id it always had.
    /// </remarks>
    internal static string MakeId(string from, string to, string title, string? parent = null) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{from}->{to}:{title.Trim()}" + (parent is null ? "" : $"<-{parent}"))))[..6].ToLowerInvariant();

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
        var existing = await FindAsync(id, ct).ConfigureAwait(false);
        if (existing is not null) return existing;

        var quest = new Quest(
            id, from, to, title, body, QuestStatus.Open, null, now, now,
            Workspace: Workspaces.Normalize(workspace))
        {
            Links = links ?? [],
            Attachments = attachments ?? [],
            Then = then ?? [],
        };

        await InsertAsync(quest, transaction: null, ct).ConfigureAwait(false);
        return quest;
    }

    /// <summary>
    /// Write a new row. OR IGNORE, because the id is the ask: a step a crash-and-retry publishes twice
    /// is one quest, and the row already there is the one that stands.
    /// </summary>
    private async Task InsertAsync(Quest quest, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO quests (id, sender, receiver, title, body, status, note, filed, updated, workspace, links, attachments, then_steps, parent)
            VALUES ($id, $sender, $receiver, $title, $body, $status, NULL, $filed, $updated, $workspace, $links, $attachments, $then, $parent)
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
        command.Parameters.AddWithValue("$filed", quest.Filed.ToString("O"));
        command.Parameters.AddWithValue("$updated", quest.Updated.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
    /// reason is refused by the caller; an illegal move is refused HERE, in the store, because the
    /// guarded UPDATE is what makes "the quest state machine is the only lock" true when two hosts'
    /// callers race over one file (D47 §5) — a check the caller ran a moment earlier decides nothing.
    /// </summary>
    /// <returns>
    /// The quest as it now stands and whether this call moved it; a null quest means no such id.
    /// A refused move returns the row unchanged, so the caller can name the state that refused it.
    /// </returns>
    public async Task<QuestMove> MoveAsync(
        string id, QuestStatus status, string? note, DateTimeOffset now, CancellationToken ct = default)
    {
        // The table, inlined into the WHERE so winning the move and writing it are one statement:
        // Taken only from Open (the atomic take), closed only from live, terminal states immovable —
        // and a mirror row never moves here at all (home IS NULL): its transitions happen at its home,
        // and only the next mirror writes the result back (D47 §5).
        var allowed = AllowedFrom(status).ToList();
        if (allowed.Count == 0)
        {
            // Nothing moves TO Open — refused here rather than rendered, because an empty IN () is a
            // SQLite syntax error dressed as a safe default.
            return new(await FindAsync(id, ct).ConfigureAwait(false), Moved: false);
        }

        var from = string.Join(", ", allowed.Select(s => $"'{s}'"));

        // A close that finishes a chain's step publishes the next one IN THE SAME TRANSACTION (D65 §4):
        // there is no moment at which the work is done and the chain lost, and a close another host
        // wins publishes nothing here. The chain is read first — it is fixed at publish, so reading it
        // before the guarded UPDATE cannot race anything.
        var next = status == QuestStatus.Done && await FindAsync(id, ct).ConfigureAwait(false) is { } closing
            ? NextStep(closing, now)
            : null;

        await using var transaction = next is null
            ? null
            : (SqliteTransaction)await _connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        bool moved;
        await using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                $"UPDATE quests SET status = $status, note = $note, updated = $updated WHERE id = $id AND home IS NULL AND status IN ({from})";
            command.Parameters.AddWithValue("$status", status.ToString());
            command.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value);
            command.Parameters.AddWithValue("$updated", now.ToString("O"));
            command.Parameters.AddWithValue("$id", id);
            moved = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
        }

        if (transaction is not null)
        {
            if (moved) await InsertAsync(next!, transaction, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        return new(
            await FindAsync(id, ct).ConfigureAwait(false), moved,
            moved && next is not null ? await FindAsync(next.Id, ct).ConfigureAwait(false) : null);
    }

    /// <summary>The states a move to <paramref name="target"/> may start from — D47 §5's table.</summary>
    private static IEnumerable<string> AllowedFrom(QuestStatus target) => target switch
    {
        QuestStatus.Taken => [nameof(QuestStatus.Open)],
        QuestStatus.Done or QuestStatus.Declined => [nameof(QuestStatus.Open), nameof(QuestStatus.Taken)],
        _ => [],
    };

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

    public async Task<Quest?> FindAsync(string id, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
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

    private static string StepsJson(IReadOnlyList<QuestStep> steps) => Json(writer =>
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
    });

    private static IReadOnlyList<QuestStep> ReadSteps(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => new QuestStep(
                item.GetProperty("to").GetString() ?? "",
                item.GetProperty("title").GetString() ?? "",
                item.GetProperty("body").GetString() ?? ""))
            .ToList();
    }

    // Written and read by hand rather than through the reflection serializer, for the same reason the
    // registration store's lists are: nothing here may quietly stop working under AOT.
    private static string LinksJson(IReadOnlyList<string> links) => Json(writer =>
    {
        writer.WriteStartArray();
        foreach (var link in links) writer.WriteStringValue(link);
        writer.WriteEndArray();
    });

    private static string AttachmentsJson(IReadOnlyList<QuestAttachment> attachments) => Json(writer =>
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
    });

    private static string Json(Action<System.Text.Json.Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream)) write(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static IReadOnlyList<string> ReadLinks(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => item.GetString())
            .OfType<string>()
            .ToList();
    }

    private static IReadOnlyList<QuestAttachment> ReadAttachments(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => new QuestAttachment(
                item.GetProperty("name").GetString() ?? "",
                item.GetProperty("sha256").GetString() ?? "",
                item.GetProperty("bytes").GetInt64()))
            .ToList();
    }
}
