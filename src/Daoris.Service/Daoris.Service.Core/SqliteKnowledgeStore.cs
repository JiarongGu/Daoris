using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// Keeps entries in a SQLite file, with an FTS5 index for search.
/// </summary>
/// <remarks>
/// SQLite because it needs no server, no container and no setup — the local mode has to be fully
/// useful with nothing installed, and a store that must be provisioned before it can be tried is one
/// that gets tried late.
///
/// <para><b>No migrations, deliberately.</b> The index is <em>derived</em> data: a local deployment
/// re-reads it from the repositories in seconds, and a shared one — which can see no repository — is
/// re-fed whole by each desktop's next sync tick (D47 §9). A schema change therefore does not need
/// migrating, it needs rebuilding — so the schema carries a version, and an older one drops the tables
/// and starts over. A NEWER one refuses to open (<see cref="NewerStoreException"/>, KSCHEMA1): every
/// host on a machine opens this file, and an older build that rebuilt a newer store would break the
/// newer host reading it. The cognition sibling's storage uses a migration runner because its data is
/// authored and cannot be regenerated; the same choice here would be ceremony guarding something that
/// is not at risk.</para>
///
/// <para>FTS5 ships in the standard SQLite build, so full-text search costs no extra dependency and
/// replaces a hand-rolled scorer with a ranked one.</para>
/// </remarks>
public sealed class SqliteKnowledgeStore : IKnowledgeStore, IAsyncDisposable
{
    /// <summary>Bump when the schema changes. An older version rebuilds rather than migrates; a newer one refuses.</summary>
    /// <remarks>2 — entries carry their workspace (D48).</remarks>
    // 3: the FTS rows carry CJK text cut into bigrams (`Text.Segment`), so every existing index is
    //    rebuilt from the raw entries on open — the rows it held were never findable in 中文.
    // 4: the FTS rows carry each identifier's words beside it (`Text.Segment`, ORIENT1f): a row written
    //    before holds `ProbeLock` as one token, which a question in words never finds.
    // 5: entries keep the lines of their file they are (`first_line`, `last_line`, ORIENT2e), and the kinds
    //    gain `Index`: an entry written before names no lines, which a hit then could not name.
    internal const int SchemaVersion = 5;

    private readonly SqliteConnection _connection;
    private readonly ConnectionGate _db;

    private SqliteKnowledgeStore(SqliteConnection connection)
    {
        _connection = connection;
        _db = ConnectionGate.For(connection);
    }

    /// <summary>
    /// Whether opening this store dropped an index an older schema had written (a version bump).
    /// </summary>
    /// <remarks>
    /// The composer reads it to forget which commits a fed deployment held (REV3): the entries went,
    /// and a claim that they are held would refuse the very re-feed that restores them.
    /// </remarks>
    public bool Rebuilt { get; private set; }


    /// <summary>Open (or create) a store at a path. Use <c>":memory:"</c> for a throwaway one.</summary>
    public static async Task<SqliteKnowledgeStore> OpenAsync(string path, CancellationToken ct = default)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Shared cache keeps an in-memory database alive across the pool for the process.
            Cache = path == ":memory:" ? SqliteCacheMode.Shared : SqliteCacheMode.Default,
        }.ToString());

        await connection.OpenAsync(ct).ConfigureAwait(false);
        var store = new SqliteKnowledgeStore(connection);
        try
        {
            await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // A refused store is closed here, so the file is no longer held by a build that will not use it.
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return store;
    }

    /// <remarks>
    /// One write transaction, the read through the stamp (KSCHEMA1): every host on the machine opens this file, and an
    /// older build that read its own version a moment before a newer one stamped the file would write its number back
    /// over the newer tables, which the newer build's next start would then rebuild.
    /// </remarks>
    private Task EnsureSchemaAsync(CancellationToken ct) => _db.InTransactionAsync(async (_, inside) =>
    {
        var version = Convert.ToInt32(await ScalarAsync("PRAGMA user_version;", inside).ConfigureAwait(false));

        // 🔴 An older build never drops a newer store (KSCHEMA1, NewerStoreException's remarks say why). Nothing is
        // written: the transaction rolls back on the throw.
        if (version > SchemaVersion) throw new NewerStoreException(_connection.DataSource, version, SchemaVersion);

        if (version != SchemaVersion)
        {
            await ExecuteAsync("DROP TABLE IF EXISTS entries_fts; DROP TABLE IF EXISTS entries;", inside)
                .ConfigureAwait(false);
            Rebuilt = version != 0;   // 0 is a store nobody has written yet
        }

        await ExecuteAsync(
            $"""
            CREATE TABLE IF NOT EXISTS entries (
                id            TEXT PRIMARY KEY,
                repository    TEXT NOT NULL,
                kind          INTEGER NOT NULL,
                provenance    INTEGER NOT NULL,
                title         TEXT NOT NULL,
                body          TEXT NOT NULL,
                relative_path TEXT NOT NULL,
                anchor        TEXT,
                workspace     TEXT NOT NULL DEFAULT '{Workspaces.Default}',
                first_line    INTEGER,
                last_line     INTEGER
            );
            CREATE INDEX IF NOT EXISTS ix_entries_repository ON entries(repository);
            -- The scoped search is the common read, and it filters on this before anything else.
            CREATE INDEX IF NOT EXISTS ix_entries_workspace ON entries(workspace);

            -- id is stored but not indexed: it is how a hit gets back to its row, never a search term.
            CREATE VIRTUAL TABLE IF NOT EXISTS entries_fts
                USING fts5(id UNINDEXED, title, body, tokenize='unicode61');
            """, inside).ConfigureAwait(false);

        await ExecuteAsync($"PRAGMA user_version = {SchemaVersion};", inside).ConfigureAwait(false);
        return true;
    }, ct);

    /// <remarks>
    /// The connection is every store's, and SQLite does not nest transactions: a publish arriving mid-refresh waits for
    /// this one to end (<see cref="ConnectionGate"/>), as does every other request's command (SQLITETX1). Not the
    /// caller's token once the transaction begins: a cancelled replace would roll back half a refresh (REV3).
    /// </remarks>
    public Task ReplaceRepositoryAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct = default) =>
        _db.InTransactionAsync((_, inside) => ReplaceRepositoryInAsync(repository, entries, inside), ct);

    private async Task<bool> ReplaceRepositoryInAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct)
    {
        // Both tables, in one transaction: an FTS row whose entry is gone would return a hit that
        // cannot be resolved, which reads as data loss rather than as a stale index.
        await using (var delete = _db.Command())
        {
            delete.CommandText =
                """
                DELETE FROM entries_fts WHERE id IN (SELECT id FROM entries WHERE repository = $r);
                DELETE FROM entries WHERE repository = $r;
                """;
            delete.Parameters.AddWithValue("$r", repository);
            await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        foreach (var entry in entries)
        {
            await using var insert = _db.Command();
            insert.CommandText =
                """
                INSERT INTO entries (id, repository, kind, provenance, title, body, relative_path, anchor, workspace, first_line, last_line)
                VALUES ($id, $repo, $kind, $prov, $title, $body, $path, $anchor, $workspace, $first, $last);
                INSERT INTO entries_fts (id, title, body) VALUES ($id, $ftsTitle, $ftsBody);
                """;
            insert.Parameters.AddWithValue("$id", entry.Id);
            insert.Parameters.AddWithValue("$repo", entry.Repository);
            insert.Parameters.AddWithValue("$kind", (int)entry.Kind);
            insert.Parameters.AddWithValue("$prov", (int)entry.Provenance);
            insert.Parameters.AddWithValue("$title", entry.Title);
            insert.Parameters.AddWithValue("$body", entry.Body);
            // 🔴 The FTS row is cut for search; the entries row keeps the text as written. An excerpt
            // and the Reader read the original; only the index sees ideographs as bigrams — which is
            // the only way `unicode61` finds a word inside a run of them (`Text.Segment`).
            insert.Parameters.AddWithValue("$ftsTitle", Text.Segment(entry.Title));
            insert.Parameters.AddWithValue("$ftsBody", Text.Segment(entry.Body));
            insert.Parameters.AddWithValue("$path", entry.RelativePath);
            insert.Parameters.AddWithValue("$anchor", (object?)entry.Anchor ?? DBNull.Value);
            insert.Parameters.AddWithValue("$workspace", Workspaces.Normalize(entry.Workspace));
            insert.Parameters.AddWithValue("$first", (object?)entry.Lines?.First ?? DBNull.Value);
            insert.Parameters.AddWithValue("$last", (object?)entry.Lines?.Last ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return true;
    }

    public Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT {Columns} FROM entries;";
        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }, ct);

    public Task<IReadOnlyDictionary<string, int>> CountByRepositoryAsync(CancellationToken ct = default) => _db.RunAsync<IReadOnlyDictionary<string, int>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT repository, COUNT(*) FROM entries GROUP BY repository;";
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) counts[reader.GetString(0)] = reader.GetInt32(1);
        return counts;
    }, ct);

    public Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT {Columns} FROM entries WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return (await ReadAllAsync(command, ct).ConfigureAwait(false)).FirstOrDefault();
    }, ct);

    internal const string Columns =
        "id, repository, kind, provenance, title, body, relative_path, anchor, workspace, first_line, last_line";

    /// <summary>The same columns, in the same order, qualified for a join. <see cref="Read"/> reads by ordinal.</summary>
    internal const string QualifiedColumns =
        "e.id, e.repository, e.kind, e.provenance, e.title, e.body, e.relative_path, e.anchor, e.workspace, e.first_line, e.last_line";

    /// <summary>
    /// How many columns <see cref="Columns"/> selects — so anything reading PAST them (a computed rank)
    /// moves when they do. Counted rather than written down: the one that was written down was wrong
    /// the moment a column was added.
    /// </summary>
    internal static readonly int ColumnCount = Columns.Split(',').Length;

    /// <summary>
    /// The open connection — quests share this database rather than opening a second one, because two
    /// files would be two things to back up and two that can disagree about which repositories exist.
    /// </summary>
    internal SqliteConnection Connection => _connection;

    /// <summary>The connection's gate, which every store over it and the search take for each command (SQLITETX1).</summary>
    internal ConnectionGate Gate => _db;

    internal static KnowledgeEntry Read(SqliteDataReader reader) => new(
        reader.GetString(1),
        (EntryKind)reader.GetInt32(2),
        (Provenance)reader.GetInt32(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.GetString(8),
        reader.IsDBNull(9) || reader.IsDBNull(10) ? null : new LineSpan(reader.GetInt32(9), reader.GetInt32(10)));

    private static async Task<IReadOnlyList<KnowledgeEntry>> ReadAllAsync(SqliteCommand command, CancellationToken ct)
    {
        var entries = new List<KnowledgeEntry>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) entries.Add(Read(reader));
        return entries;
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<object?> ScalarAsync(string sql, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Closed once the work in flight has ended, never under it.</summary>
    public async ValueTask DisposeAsync()
    {
        await _db.RunAsync(async () =>
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }, CancellationToken.None).ConfigureAwait(false);
    }
}
