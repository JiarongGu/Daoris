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
    string? Home = null);

/// <param name="Quest">The quest as it now stands — null when no such id exists.</param>
/// <param name="Moved">Whether THIS call moved it. False with a non-null quest is a refused move.</param>
public sealed record QuestMove(Quest? Quest, bool Moved);

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
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS quests (
                  id       TEXT PRIMARY KEY,
                  sender   TEXT NOT NULL,
                  receiver TEXT NOT NULL,
                  title    TEXT NOT NULL,
                  body     TEXT NOT NULL,
                  status   TEXT NOT NULL,
                  note     TEXT NULL,
                  filed    TEXT NOT NULL,
                  updated  TEXT NOT NULL,
                  home     TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS quests_receiver ON quests (receiver, status);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // A store created before the remote existed has no home column; its quests must survive the
        // upgrade with home NULL — which is exactly right, because everything in it was its own.
        await using (var probe = _connection.CreateCommand())
        {
            probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info('quests') WHERE name = 'home'";
            var present = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false));
            if (present == 0)
            {
                await using var alter = _connection.CreateCommand();
                alter.CommandText = "ALTER TABLE quests ADD COLUMN home TEXT NULL";
                await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// A short handle derived from who asked, of whom, and for what.
    /// </summary>
    /// <remarks>
    /// Content-derived so publishing the same quest twice collides rather than multiplying — an agent
    /// that retries should not produce a second copy of the same ask.
    /// </remarks>
    private static string MakeId(string from, string to, string title) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{from}->{to}:{title.Trim()}")))[..6].ToLowerInvariant();

    /// <summary>Publish a quest. Returns the existing one unchanged if it was already asked.</summary>
    public async Task<Quest> PublishAsync(
        string from, string to, string title, string body, DateTimeOffset now, CancellationToken ct = default)
    {
        var id = MakeId(from, to, title);
        var existing = await FindAsync(id, ct).ConfigureAwait(false);
        if (existing is not null) return existing;

        var quest = new Quest(id, from, to, title, body, QuestStatus.Open, null, now, now);

        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO quests (id, sender, receiver, title, body, status, note, filed, updated)
            VALUES ($id, $sender, $receiver, $title, $body, $status, NULL, $filed, $updated)
            """;
        command.Parameters.AddWithValue("$id", quest.Id);
        command.Parameters.AddWithValue("$sender", quest.From);
        command.Parameters.AddWithValue("$receiver", quest.To);
        command.Parameters.AddWithValue("$title", quest.Title);
        command.Parameters.AddWithValue("$body", quest.Body);
        command.Parameters.AddWithValue("$status", quest.Status.ToString());
        command.Parameters.AddWithValue("$filed", quest.Filed.ToString("O"));
        command.Parameters.AddWithValue("$updated", quest.Updated.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return quest;
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

        await using var command = _connection.CreateCommand();
        command.CommandText =
            $"UPDATE quests SET status = $status, note = $note, updated = $updated WHERE id = $id AND home IS NULL AND status IN ({from})";
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        var moved = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;

        return new(await FindAsync(id, ct).ConfigureAwait(false), moved);
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
        command.CommandText = """
            INSERT INTO quests (id, sender, receiver, title, body, status, note, filed, updated, home)
            VALUES ($id, $sender, $receiver, $title, $body, $status, $note, $filed, $updated, $home)
            ON CONFLICT (id) DO UPDATE SET
              status = $status, note = $note, updated = $updated, home = $home
            """;
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
        string? receiver = null, bool includeClosed = false, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            SELECT * FROM quests
            WHERE ($receiver IS NULL OR receiver = $receiver)
              {(includeClosed ? "" : "AND status IN ('Open', 'Taken')")}
            ORDER BY CASE status WHEN 'Open' THEN 0 WHEN 'Taken' THEN 1 ELSE 2 END, filed
            """;
        command.Parameters.AddWithValue("$receiver", (object?)receiver ?? DBNull.Value);

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
        reader.IsDBNull(reader.GetOrdinal("home")) ? null : reader.GetString(reader.GetOrdinal("home")));
}
