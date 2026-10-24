using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>A file a finished tree's commit changed, by name, with the lines added and removed; -1 for each where git counts none (a binary).</summary>
public sealed record WorkRecordFile(string Path, int Added, int Removed);

/// <summary>One commit of a finished tree's, read by <c>--first-parent</c> from its tip (the keep-size design §2.1): its id, subject and files.</summary>
public sealed record WorkRecordCommit(string Id, string Subject, IReadOnlyList<WorkRecordFile> Files);

/// <summary>
/// Which hold kept a finished tree's commits as its branch went (the keep-size design §1.1, §3.3): <c>line</c>, <c>landing</c>,
/// <c>squash</c>, <c>content</c>, <c>carried</c> or <c>empty</c>; where (the branch or ref named), and in which form.
/// </summary>
public sealed record WorkRecordHold(string Kind, string? Where, string? Form);

/// <summary>What a finished tree held that no commit holds, by name (§2.1's last row): at most the walk's limit of names, the rest counted.</summary>
/// <param name="Count">Every such path, named or not.</param>
/// <param name="Cut">The names stop at the limit; <paramref name="Count"/> says how many there were.</param>
public sealed record WorkRecordLeft(IReadOnlyList<string> Names, int Count, bool Cut);

/// <summary>
/// The driver's post of a finished tree's record (KEEPSIZE1b, the keep-size design §2.2): the manifest's facts and the commits read from
/// the tip. Keyed by the tree's set-aside identity, <c>(workspace, repository, folder)</c>, where the folder is <c>&lt;tree&gt;-&lt;moment&gt;</c>.
/// </summary>
/// <param name="Tree">The tree's path before the move: the sessions whose row names it are the record's.</param>
/// <param name="Moved">When the tree was moved aside: only a session made before it is the record's.</param>
/// <param name="Commits">The tree's commits, oldest first; null where the tip no longer read, and <paramref name="CommitsUnread"/> says why.</param>
/// <param name="Whole">Whether <c>whole</c> was set at the move.</param>
public sealed record WorkRecordPost(
    string Workspace, string Repository, string Folder, string Tree, DateTimeOffset Moved, string? Unit, string Branch, string? Line,
    WorkRecordHold Hold, string Tip, string? GrewFrom, string? Ref, IReadOnlyList<WorkRecordCommit>? Commits, string? CommitsUnread,
    WorkRecordLeft Left, bool Whole);

/// <summary>A session the record names (§2.2's <c>work_record_sessions</c>), and the commits its base attributes to it, or null where none can be read.</summary>
public sealed record WorkRecordSession(string Session, IReadOnlyList<string>? Commits);

/// <summary>A finished tree's record, as kept (the keep-size design §2): what the driver posted, when it was kept, and its sessions.</summary>
public sealed record WorkRecord(WorkRecordPost Post, DateTimeOffset At, int Schema, IReadOnlyList<WorkRecordSession> Sessions);

/// <summary>
/// The records are stamped newer than this build reads (the keep-size design §2.2, as KSCHEMA1 refuses the index's): no row is read or
/// written, and a post is refused, so the driver keeps its folder <c>deleted</c>, awaiting its record, and says why.
/// </summary>
public sealed class NewerWorkRecordsException(int found, int known)
    : Exception($"The records of finished trees were written by a newer Daoris (version {found}; this build reads {known}), so this one keeps none of them.")
{
    /// <summary>The stamp the store carries.</summary>
    public int Found { get; } = found;

    /// <summary>The version this build writes.</summary>
    public int Known { get; } = known;
}

/// <summary>
/// A finished tree's record (KEEPSIZE1b, D195's keep-size note; the keep-size design §2.2): <c>work_records</c>, one row per finished tree,
/// and <c>work_record_sessions</c>, the sessions that ran in it, in <c>knowledge.db</c> beside the sessions' rows. Kept, not derived: the
/// index's rebuild never touches them, and they carry their own version in <c>work_record_version</c>, never lowered. History: they go with
/// their last session (<see cref="ForgetSessionAsync"/>, which <see cref="SessionStore.DeleteAsync"/> calls), or with a clear of history
/// once no session names them. Machine-local: no wire carries them, and a shared host maps no door to them.
/// </summary>
/// <remarks>
/// Not yet searched (KEEPSIZE1c builds <c>work_records_fts</c>): this build writes and keeps them.
/// </remarks>
public sealed class WorkRecordStore
{
    /// <summary>The version of the records' tables this build writes and reads.</summary>
    public const int Version = 1;

    /// <summary>The schema number each record carries, of the post's shape.</summary>
    public const int RecordSchema = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly ConnectionGate _db;

    private WorkRecordStore(SqliteConnection connection) => _db = ConnectionGate.For(connection);

    /// <summary>The stamp a newer build left, where it did: this build then reads and writes no record.</summary>
    public int? NewerVersion { get; private set; }

    public static async Task<WorkRecordStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new WorkRecordStore(connection);
        await store._db.RunAsync(() => store.EnsureSchemaAsync(ct), ct).ConfigureAwait(false);
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using (var command = _db.Command())
        {
            command.CommandText = Schema;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // 🔴 A store a newer build stamped keeps its stamp, and this build touches none of its rows (the design §2.2): it never migrates them back.
        NewerVersion = await _db.InTransactionAsync(async (transaction, inside) =>
        {
            await using var stamp = _db.Command();
            stamp.Transaction = transaction;
            stamp.CommandText = """
                INSERT INTO work_record_version (one, version) VALUES (1, $version)
                ON CONFLICT(one) DO UPDATE SET version = MAX(version, excluded.version)
                RETURNING version
                """;
            stamp.Parameters.AddWithValue("$version", Version);
            var stamped = Convert.ToInt32(await stamp.ExecuteScalarAsync(inside).ConfigureAwait(false));
            return stamped > Version ? stamped : (int?)null;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The tables, made where missing; never dropped. Version 1 is the first, so there is nothing yet to migrate from.</summary>
    internal const string Schema = """
        CREATE TABLE IF NOT EXISTS work_record_version (
          one     INTEGER PRIMARY KEY CHECK (one = 1),
          version INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS work_records (
          workspace  TEXT NOT NULL,
          repository TEXT NOT NULL,
          folder     TEXT NOT NULL,
          tree       TEXT NOT NULL,
          moved      TEXT NOT NULL,
          unit       TEXT NULL,
          branch     TEXT NOT NULL,
          line       TEXT NULL,
          hold       TEXT NOT NULL,
          tip        TEXT NOT NULL,
          grew_from  TEXT NULL,
          ref        TEXT NULL,
          commits    TEXT NULL,
          unread     TEXT NULL,
          left_names TEXT NOT NULL,
          whole      INTEGER NOT NULL,
          at         TEXT NOT NULL,
          schema     INTEGER NOT NULL,
          PRIMARY KEY (workspace, repository, folder)
        );
        CREATE TABLE IF NOT EXISTS work_record_sessions (
          workspace  TEXT NOT NULL,
          repository TEXT NOT NULL,
          folder     TEXT NOT NULL,
          session    TEXT NOT NULL,
          commits    TEXT NULL,
          PRIMARY KEY (workspace, repository, folder, session)
        );
        CREATE INDEX IF NOT EXISTS work_record_sessions_session ON work_record_sessions (session);
        """;

    private NewerWorkRecordsException? Refusal => NewerVersion is { } found ? new(found, Version) : null;

    /// <summary>
    /// Keep a finished tree's record (§2.2), idempotent on <c>(workspace, repository, folder)</c>: a second post of a kept identity answers
    /// as the first did and writes nothing. The sessions are this store's rows that name the tree and were made before its move.
    /// </summary>
    /// <returns>The record as kept, and whether this post kept it.</returns>
    /// <exception cref="NewerWorkRecordsException">A newer build stamped the records.</exception>
    public Task<(WorkRecord Record, bool Kept)> PostAsync(WorkRecordPost post, DateTimeOffset now, CancellationToken ct = default)
    {
        if (Refusal is { } refused) throw refused;
        return _db.InTransactionAsync<(WorkRecord, bool)>(async (_, inside) =>
        {
            if (await FindInsideAsync(post.Workspace, post.Repository, post.Folder, inside).ConfigureAwait(false) is { } kept) return (kept, false);

            var sessions = Attributed(post, await SessionsOfAsync(post, inside).ConfigureAwait(false));
            await using (var insert = _db.Command())
            {
                insert.CommandText = """
                    INSERT INTO work_records (workspace, repository, folder, tree, moved, unit, branch, line, hold, tip, grew_from, ref, commits,
                      unread, left_names, whole, at, schema)
                    VALUES ($workspace, $repository, $folder, $tree, $moved, $unit, $branch, $line, $hold, $tip, $grew, $ref, $commits,
                      $unread, $left, $whole, $at, $schema)
                    """;
                Key(insert, post.Workspace, post.Repository, post.Folder);
                insert.Parameters.AddWithValue("$tree", post.Tree);
                insert.Parameters.AddWithValue("$moved", Moments.Write(post.Moved));
                insert.Parameters.AddWithValue("$unit", (object?)post.Unit ?? DBNull.Value);
                insert.Parameters.AddWithValue("$branch", post.Branch);
                insert.Parameters.AddWithValue("$line", (object?)post.Line ?? DBNull.Value);
                insert.Parameters.AddWithValue("$hold", JsonSerializer.Serialize(post.Hold, Json));
                insert.Parameters.AddWithValue("$tip", post.Tip);
                insert.Parameters.AddWithValue("$grew", (object?)post.GrewFrom ?? DBNull.Value);
                insert.Parameters.AddWithValue("$ref", (object?)post.Ref ?? DBNull.Value);
                insert.Parameters.AddWithValue("$commits", post.Commits is null ? DBNull.Value : JsonSerializer.Serialize(post.Commits, Json));
                insert.Parameters.AddWithValue("$unread", (object?)post.CommitsUnread ?? DBNull.Value);
                insert.Parameters.AddWithValue("$left", JsonSerializer.Serialize(post.Left, Json));
                insert.Parameters.AddWithValue("$whole", post.Whole ? 1 : 0);
                insert.Parameters.AddWithValue("$at", Moments.Write(now));
                insert.Parameters.AddWithValue("$schema", RecordSchema);
                await insert.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            foreach (var session in sessions)
            {
                await using var link = _db.Command();
                link.CommandText = """
                    INSERT INTO work_record_sessions (workspace, repository, folder, session, commits)
                    VALUES ($workspace, $repository, $folder, $session, $commits)
                    """;
                Key(link, post.Workspace, post.Repository, post.Folder);
                link.Parameters.AddWithValue("$session", session.Session);
                link.Parameters.AddWithValue("$commits", session.Commits is null ? DBNull.Value : JsonSerializer.Serialize(session.Commits, Json));
                await link.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return (new WorkRecord(post, now, RecordSchema, sessions), true);
        }, ct);
    }

    /// <summary>The record of one finished tree; null where none is kept.</summary>
    /// <exception cref="NewerWorkRecordsException">A newer build stamped the records.</exception>
    public Task<WorkRecord?> FindAsync(string workspace, string repository, string folder, CancellationToken ct = default)
    {
        if (Refusal is { } refused) throw refused;
        return _db.RunAsync(() => FindInsideAsync(workspace, repository, folder, ct), ct);
    }

    /// <summary>How many records this machine keeps (the card says the count, §4.5); none where a newer build stamped them.</summary>
    public Task<int> CountAsync(CancellationToken ct = default) => Refusal is not null ? Task.FromResult(0) : _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT COUNT(*) FROM work_records";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }, ct);

    /// <summary>
    /// A session's links gone with its row (§2.2: <see cref="SessionStore.DeleteAsync"/>, which the history clear and the single delete call
    /// inside their transaction), and each record it was the last session of. Nothing where the tables are not here or a newer build stamped
    /// them, which this build never writes into.
    /// </summary>
    internal static async Task ForgetSessionAsync(ConnectionGate db, string session, CancellationToken ct)
    {
        if (!await OursAsync(db, ct).ConfigureAwait(false)) return;
        await using var command = db.Command();
        command.CommandText = """
            CREATE TEMP TABLE IF NOT EXISTS work_records_forgetting (workspace TEXT, repository TEXT, folder TEXT);
            DELETE FROM work_records_forgetting;
            INSERT INTO work_records_forgetting SELECT workspace, repository, folder FROM work_record_sessions WHERE session = $session;
            DELETE FROM work_record_sessions WHERE session = $session;
            DELETE FROM work_records WHERE EXISTS (
              SELECT 1 FROM work_records_forgetting f
              WHERE f.workspace = work_records.workspace AND f.repository = work_records.repository AND f.folder = work_records.folder)
              AND NOT EXISTS (
              SELECT 1 FROM work_record_sessions s
              WHERE s.workspace = work_records.workspace AND s.repository = work_records.repository AND s.folder = work_records.folder);
            DELETE FROM work_records_forgetting;
            """;
        command.Parameters.AddWithValue("$session", session);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The records no session names in <paramref name="workspace"/>, kept at or before <paramref name="through"/>, gone (§2.2: a record with
    /// no session is history, taken by a clear of history by its own <c>at</c>, within the clear's range); how many went. Run inside the
    /// clear's transaction. Nothing where the tables are not this build's.
    /// </summary>
    internal static async Task<int> ForgetOrphansAsync(ConnectionGate db, string workspace, DateTimeOffset through, CancellationToken ct)
    {
        if (!await OursAsync(db, ct).ConfigureAwait(false)) return 0;
        var gone = new List<(string Repository, string Folder)>();
        await using (var read = db.Command())
        {
            read.CommandText = """
                SELECT repository, folder, at FROM work_records r WHERE workspace = $workspace COLLATE NOCASE
                  AND NOT EXISTS (SELECT 1 FROM work_record_sessions s
                    WHERE s.workspace = r.workspace AND s.repository = r.repository AND s.folder = r.folder)
                """;
            read.Parameters.AddWithValue("$workspace", workspace);
            await using var rows = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rows.ReadAsync(ct).ConfigureAwait(false))
            {
                if (Moments.Read(rows.GetString(2)) is { } at && at <= through) gone.Add((rows.GetString(0), rows.GetString(1)));
            }
        }

        foreach (var (repository, folder) in gone)
        {
            await using var delete = db.Command();
            delete.CommandText = "DELETE FROM work_records WHERE workspace = $workspace COLLATE NOCASE AND repository = $repository AND folder = $folder";
            delete.Parameters.AddWithValue("$workspace", workspace);
            delete.Parameters.AddWithValue("$repository", repository);
            delete.Parameters.AddWithValue("$folder", folder);
            await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return gone.Count;
    }

    /// <summary>Whether the records' tables are here and this build's version or older: only then does a delete touch them.</summary>
    private static async Task<bool> OursAsync(ConnectionGate db, CancellationToken ct)
    {
        await using var probe = db.Command();
        probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'work_record_version'";
        if (Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0) return false;
        await using var stamp = db.Command();
        stamp.CommandText = "SELECT version FROM work_record_version WHERE one = 1";
        return await stamp.ExecuteScalarAsync(ct).ConfigureAwait(false) is not { } found || Convert.ToInt32(found) <= Version;
    }

    private async Task<WorkRecord?> FindInsideAsync(string workspace, string repository, string folder, CancellationToken ct)
    {
        WorkRecordPost post;
        DateTimeOffset at;
        int schema;
        await using (var read = _db.Command())
        {
            read.CommandText = """
                SELECT tree, moved, unit, branch, line, hold, tip, grew_from, ref, commits, unread, left_names, whole, at, schema
                FROM work_records WHERE workspace = $workspace AND repository = $repository AND folder = $folder
                """;
            Key(read, workspace, repository, folder);
            await using var row = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await row.ReadAsync(ct).ConfigureAwait(false)) return null;
            string? Text(int at) => row.IsDBNull(at) ? null : row.GetString(at);
            post = new WorkRecordPost(
                workspace, repository, folder, row.GetString(0), Moments.ReadRequired(row.GetString(1)), Text(2), row.GetString(3), Text(4),
                JsonSerializer.Deserialize<WorkRecordHold>(row.GetString(5), Json)!, row.GetString(6), Text(7), Text(8),
                Text(9) is { } commits ? JsonSerializer.Deserialize<List<WorkRecordCommit>>(commits, Json) : null, Text(10),
                JsonSerializer.Deserialize<WorkRecordLeft>(row.GetString(11), Json)!, row.GetInt32(12) != 0);
            at = Moments.ReadRequired(row.GetString(13));
            schema = row.GetInt32(14);
        }

        var sessions = new List<WorkRecordSession>();
        await using (var links = _db.Command())
        {
            links.CommandText = """
                SELECT session, commits FROM work_record_sessions WHERE workspace = $workspace AND repository = $repository AND folder = $folder
                ORDER BY rowid
                """;
            Key(links, workspace, repository, folder);
            await using var rows = await links.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rows.ReadAsync(ct).ConfigureAwait(false))
            {
                sessions.Add(new(rows.GetString(0), rows.IsDBNull(1) ? null : JsonSerializer.Deserialize<List<string>>(rows.GetString(1), Json)));
            }
        }

        return new WorkRecord(post, at, schema, sessions);
    }

    /// <summary>Every session row that names the tree's path and was made before the move, oldest first, with its base.</summary>
    private async Task<IReadOnlyList<(string Id, string? Base)>> SessionsOfAsync(WorkRecordPost post, CancellationToken ct)
    {
        var tree = Trees.Normalize(post.Tree);
        var same = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var found = new List<(string Id, string? Base, DateTimeOffset Created)>();
        await using var read = _db.Command();
        read.CommandText = "SELECT id, tree, base_commit, created FROM sessions WHERE tree IS NOT NULL AND origin IS NULL";
        await using var rows = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rows.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!string.Equals(Trees.Normalize(rows.GetString(1)), tree, same)) continue;
            if (Moments.Read(rows.GetString(3)) is not { } created || created > post.Moved) continue;
            found.Add((rows.GetString(0), rows.IsDBNull(2) ? null : rows.GetString(2), created));
        }

        return [.. found.OrderBy(each => each.Created).ThenBy(each => each.Id, StringComparer.Ordinal).Select(each => (each.Id, each.Base))];
    }

    /// <summary>
    /// Which session made which commits (§2.1), only where the bases bound the range: every session based at the commit the tree grew from
    /// or at one of its commits, each at a different one. Then each holds the commits after its base up to the next session's. Otherwise
    /// every session is named and none attributed: never an attribution the bases cannot read.
    /// </summary>
    internal static IReadOnlyList<WorkRecordSession> Attributed(WorkRecordPost post, IReadOnlyList<(string Id, string? Base)> sessions)
    {
        var commits = post.Commits ?? [];
        int? At(string? commit) =>
            commit is null ? null
            : post.GrewFrom is { } grew && string.Equals(commit, grew, StringComparison.OrdinalIgnoreCase) ? -1
            : commits.Select((each, index) => (each, index)).FirstOrDefault(pair => string.Equals(pair.each.Id, commit, StringComparison.OrdinalIgnoreCase)) is { each: not null } hit
                ? hit.index
                : null;

        var positions = sessions.Select(session => (session.Id, At: At(session.Base))).ToList();
        var bounded = post.Commits is not null && positions.Count > 0 && positions.All(each => each.At is not null)
            && positions.Select(each => each.At).Distinct().Count() == positions.Count;
        if (!bounded) return [.. sessions.Select(session => new WorkRecordSession(session.Id, null))];

        var ordered = positions.OrderBy(each => each.At).ToList();
        var byId = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        for (var at = 0; at < ordered.Count; at++)
        {
            var from = ordered[at].At!.Value + 1;
            var to = at + 1 < ordered.Count ? ordered[at + 1].At!.Value : commits.Count - 1;
            byId[ordered[at].Id] = [.. commits.Skip(from).Take(Math.Max(0, to - from + 1)).Select(commit => commit.Id)];
        }

        return [.. sessions.Select(session => new WorkRecordSession(session.Id, byId[session.Id]))];
    }

    private static void Key(SqliteCommand command, string workspace, string repository, string folder)
    {
        command.Parameters.AddWithValue("$workspace", workspace);
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$folder", folder);
    }
}
