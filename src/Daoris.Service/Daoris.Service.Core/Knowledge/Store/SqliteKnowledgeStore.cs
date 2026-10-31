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
/// and starts over. A NEWER one is left as it is and refused, the index alone (<see cref="Refusal"/>,
/// KSCHEMA1): the store opens, and every operation on the index throws. The cognition sibling's
/// storage uses a migration runner because its data is
/// authored and cannot be regenerated; the same choice here would be ceremony guarding something that
/// is not at risk.</para>
///
/// <para>FTS5 ships in the standard SQLite build, so full-text search costs no extra dependency and
/// replaces a hand-rolled scorer with a ranked one.</para>
/// </remarks>
public sealed class SqliteKnowledgeStore : IKnowledgeStore, IAsyncDisposable
{
    /// <summary>Bump when the schema changes. An older version rebuilds rather than migrates; a newer one is refused.</summary>
    /// <remarks>2 — entries carry their workspace (D48).</remarks>
    // 3: the FTS rows carry CJK text cut into bigrams (`Text.Segment`), so every existing index is
    //    rebuilt from the raw entries on open — the rows it held were never findable in 中文.
    // 4: the FTS rows carry each identifier's words beside it (`Text.Segment`, ORIENT1f): a row written
    //    before holds `ProbeLock` as one token, which a question in words never finds.
    // 5: entries keep the lines of their file they are (`first_line`, `last_line`, ORIENT2e), and the kinds
    //    gain `Index`: an entry written before names no lines, which a hit then could not name.
    // 6: each repository's reading is kept beside its entries and written with them (KNOWCENTRAL2a), and both move to
    //    tables named by this version, which a connector an older build opened before the upgrade never reaches.
    internal const int SchemaVersion = 6;

    /// <summary>The index's tables, named by <see cref="SchemaVersion"/> so each bump moves them out of an older open connector's reach.</summary>
    internal static readonly string EntriesTable = $"index_entries_{SchemaVersion}";

    internal static readonly string FtsTable = $"index_fts_{SchemaVersion}";

    internal static readonly string ReadingsTable = $"index_readings_{SchemaVersion}";

    /// <summary>Every index table a build has kept, each full-text one before the table it indexes: a rebuild drops them all.</summary>
    private static IEnumerable<string> EveryIndexTable() =>
        new[] { "entries_fts", "entries" }.Concat(Enumerable.Range(6, SchemaVersion - 5)
            .SelectMany(version => new[] { $"index_fts_{version}", $"index_entries_{version}", $"index_readings_{version}" }));

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

    /// <summary>
    /// Why the index answers nothing here: a newer Daoris wrote it (KSCHEMA1). Null when it answers. Set at open, and
    /// then the index's tables are neither dropped nor stamped, and each operation on them throws it again.
    /// </summary>
    public NewerIndexException? Refusal { get; private set; }

    /// <summary>Open (or create) a store at a path. Use <c>":memory:"</c> for a throwaway one.</summary>
    public static async Task<SqliteKnowledgeStore> OpenAsync(string path, CancellationToken ct = default)
    {
        var connection = new SqliteConnection(ConnectionString(path));

        await connection.OpenAsync(ct).ConfigureAwait(false);
        var store = new SqliteKnowledgeStore(connection);
        try
        {
            await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // A store that did not open is closed here, so the file is not held by a connection nothing will use.
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return store;
    }

    /// <summary>
    /// The connection string a store at <paramref name="path"/> opens with. Its pool is named by this text, so a test that lets
    /// go of its file clears this pool and no other (FLAKE3).
    /// </summary>
    internal static string ConnectionString(string path) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadWriteCreate,
        // Shared cache keeps an in-memory database alive across the pool for the process.
        Cache = path == ":memory:" ? SqliteCacheMode.Shared : SqliteCacheMode.Default,
    }.ToString();

    /// <remarks>
    /// One write transaction, the read through the stamp (KSCHEMA1): every host on the machine opens this file, and an
    /// older build that read its own version a moment before a newer one stamped the file would write its number back
    /// over the newer tables, which the newer build's next start would then rebuild.
    /// </remarks>
    private Task EnsureSchemaAsync(CancellationToken ct) => _db.InTransactionAsync(async (_, inside) =>
    {
        var version = Convert.ToInt32(await ScalarAsync("PRAGMA user_version;", inside).ConfigureAwait(false));

        // 🔴 An older build never drops a newer index (KSCHEMA1, NewerIndexException's remarks say why), and never refuses
        // the store around it: nothing is dropped, created or stamped, and the index alone answers the refusal.
        if (version > SchemaVersion)
        {
            Refusal = new NewerIndexException(_connection.DataSource, version, SchemaVersion);
            return true;
        }

        if (version != SchemaVersion)
        {
            // An older build's open connector then fails at its next statement rather than writing what this build reads.
            await ExecuteAsync(string.Concat(EveryIndexTable().Select(table => $"DROP TABLE IF EXISTS {table};")), inside)
                .ConfigureAwait(false);
            Rebuilt = version != 0;   // 0 is a store nobody has written yet
        }

        await ExecuteAsync(
            $"""
            CREATE TABLE IF NOT EXISTS {EntriesTable} (
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
            CREATE INDEX IF NOT EXISTS {EntriesTable}_repository ON {EntriesTable}(repository);
            -- The scoped search is the common read, and it filters on this before anything else.
            CREATE INDEX IF NOT EXISTS {EntriesTable}_workspace ON {EntriesTable}(workspace);

            -- id is stored but not indexed: it is how a hit gets back to its row, never a search term.
            CREATE VIRTUAL TABLE IF NOT EXISTS {FtsTable}
                USING fts5(id UNINDEXED, title, body, tokenize='unicode61');

            -- One row a repository, kept when its entries go, so its generation never moves back.
            CREATE TABLE IF NOT EXISTS {ReadingsTable} (
                repository TEXT PRIMARY KEY,
                generation INTEGER NOT NULL,
                commit_id  TEXT,
                newest     INTEGER,
                files      INTEGER,
                fed        INTEGER NOT NULL,
                read_at    TEXT NOT NULL
            );
            """, inside).ConfigureAwait(false);

        await ExecuteAsync($"PRAGMA user_version = {SchemaVersion};", inside).ConfigureAwait(false);
        return true;
    }, ct);

    /// <remarks>
    /// The connection is every store's, and SQLite does not nest transactions: a publish arriving mid-refresh waits for
    /// this one to end (<see cref="ConnectionGate"/>), as does every other request's command (SQLITETX1). Not the
    /// caller's token once the transaction begins: a cancelled replace would roll back half a refresh (REV3).
    /// </remarks>
    public Task<bool> ReplaceRepositoryAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, ReadingWrite reading,
        Func<CancellationToken, Task>? alongside = null, CancellationToken ct = default) =>
        Refused<bool>() ?? _db.InTransactionAsync(async (_, inside) =>
        {
            // The generation is read inside the write, so another connector's write between this writer's read and this
            // one refuses it here, whatever process made it.
            var held = await GenerationInAsync(repository, inside).ConfigureAwait(false);
            if (reading.Expected is { } expected && expected != held) return false;

            await ReplaceEntriesInAsync(repository, entries, inside).ConfigureAwait(false);
            await using (var write = _db.Command())
            {
                write.CommandText =
                    $"""
                    INSERT INTO {ReadingsTable} (repository, generation, commit_id, newest, files, fed, read_at)
                    VALUES ($r, $generation, $commit, $newest, $files, $fed, $at)
                    ON CONFLICT (repository) DO UPDATE SET generation = $generation, commit_id = $commit, newest = $newest,
                        files = $files, fed = $fed, read_at = $at;
                    """;
                write.Parameters.AddWithValue("$r", repository);
                write.Parameters.AddWithValue("$generation", held + 1);
                write.Parameters.AddWithValue("$commit", (object?)reading.Commit ?? DBNull.Value);
                write.Parameters.AddWithValue("$newest", (object?)reading.Signature?.Newest ?? DBNull.Value);
                write.Parameters.AddWithValue("$files", (object?)reading.Signature?.Files ?? DBNull.Value);
                write.Parameters.AddWithValue("$fed", reading.Fed ? 1 : 0);
                write.Parameters.AddWithValue("$at", Moments.Write(reading.At));
                await write.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            if (alongside is not null) await alongside(inside).ConfigureAwait(false);
            return true;
        }, ct);

    public Task RestampAsync(string repository, string workspace, CancellationToken ct = default) =>
        Refused<bool>() ?? _db.InTransactionAsync(async (_, inside) =>
        {
            var stamped = new List<(string Repository, string Workspace)>();
            await using (var find = _db.Command())
            {
                find.CommandText = $"SELECT DISTINCT repository, workspace FROM {EntriesTable} WHERE repository = $r COLLATE NOCASE;";
                find.Parameters.AddWithValue("$r", repository);
                await using var reader = await find.ExecuteReaderAsync(inside).ConfigureAwait(false);
                while (await reader.ReadAsync(inside).ConfigureAwait(false)) stamped.Add((reader.GetString(0), reader.GetString(1)));
            }

            foreach (var moved in stamped.Where(held => !Workspaces.Same(held.Workspace, workspace)).Select(held => held.Repository).Distinct(StringComparer.Ordinal))
            {
                await using var write = _db.Command();
                write.CommandText =
                    $"""
                    UPDATE {EntriesTable} SET workspace = $w WHERE repository = $r;
                    UPDATE {ReadingsTable} SET generation = generation + 1 WHERE repository = $r;
                    """;
                write.Parameters.AddWithValue("$r", moved);
                write.Parameters.AddWithValue("$w", Workspaces.Normalize(workspace));
                await write.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return true;
        }, ct);

    public Task<IReadOnlyDictionary<string, long>> GenerationsAsync(CancellationToken ct = default) =>
        Refused<IReadOnlyDictionary<string, long>>() ?? _db.RunAsync<IReadOnlyDictionary<string, long>>(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"SELECT repository, generation FROM {ReadingsTable};";
            var generations = new Dictionary<string, long>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) generations[reader.GetString(0)] = reader.GetInt64(1);
            return generations;
        }, ct);

    public Task<IndexReading?> ReadingAsync(string repository, CancellationToken ct = default) =>
        Refused<IndexReading?>() ?? _db.RunAsync(() => ReadingInAsync(repository, ct), ct);

    /// <remarks>One read transaction (<c>BEGIN</c>, deferred): a write another process commits waits for it to end.</remarks>
    public Task<HeldTogether> HeldAsync(
        IReadOnlyCollection<string> ids, Func<KnowledgeEntry, bool> withFile, CancellationToken ct = default) =>
        Refused<HeldTogether>() ?? _db.InTransactionAsync(async (_, inside) =>
        {
            var entries = new Dictionary<string, KnowledgeEntry>(StringComparer.Ordinal);
            var files = new Dictionary<(string Repository, string Path), IReadOnlyList<KnowledgeEntry>>();
            var readings = new Dictionary<string, IndexReading>(StringComparer.Ordinal);
            foreach (var id in ids.Distinct(StringComparer.Ordinal))
            {
                await using (var find = _db.Command())
                {
                    find.CommandText = $"SELECT {Columns} FROM {EntriesTable} WHERE id = $id;";
                    find.Parameters.AddWithValue("$id", id);
                    if ((await ReadAllAsync(find, inside).ConfigureAwait(false)).FirstOrDefault() is not { } entry) continue;
                    entries[id] = entry;
                }

                var entryHeld = entries[id];
                if (withFile(entryHeld) && !files.ContainsKey((entryHeld.Repository, entryHeld.RelativePath)))
                {
                    await using var file = _db.Command();
                    file.CommandText = $"SELECT {Columns} FROM {EntriesTable} WHERE repository = $r AND relative_path = $p;";
                    file.Parameters.AddWithValue("$r", entryHeld.Repository);
                    file.Parameters.AddWithValue("$p", entryHeld.RelativePath);
                    files[(entryHeld.Repository, entryHeld.RelativePath)] = await ReadAllAsync(file, inside).ConfigureAwait(false);
                }

                if (!readings.ContainsKey(entryHeld.Repository) && await ReadingInAsync(entryHeld.Repository, inside).ConfigureAwait(false) is { } reading)
                {
                    readings[entryHeld.Repository] = reading;
                }
            }

            return new HeldTogether(entries, files, readings);
        }, ct, deferred: true);

    private async Task<long> GenerationInAsync(string repository, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT generation FROM {ReadingsTable} WHERE repository = $r;";
        command.Parameters.AddWithValue("$r", repository);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is long generation ? generation : 0;
    }

    private async Task<IndexReading?> ReadingInAsync(string repository, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT generation, commit_id, newest, files, fed, read_at FROM {ReadingsTable} WHERE repository = $r;";
        command.Parameters.AddWithValue("$r", repository);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        var commit = reader.IsDBNull(1) ? null : reader.GetString(1);
        var signature = reader.IsDBNull(2) || reader.IsDBNull(3) ? null : new RootSignature(commit, reader.GetInt64(2), reader.GetInt32(3));
        return new IndexReading(repository, reader.GetInt64(0), commit, signature, reader.GetInt64(4) != 0, Moments.ReadRequired(reader.GetString(5)));
    }

    private async Task ReplaceEntriesInAsync(string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct)
    {
        // Both tables, in one transaction: an FTS row whose entry is gone would return a hit that
        // cannot be resolved, which reads as data loss rather than as a stale index.
        await using (var delete = _db.Command())
        {
            delete.CommandText =
                $"""
                DELETE FROM {FtsTable} WHERE id IN (SELECT id FROM {EntriesTable} WHERE repository = $r);
                DELETE FROM {EntriesTable} WHERE repository = $r;
                """;
            delete.Parameters.AddWithValue("$r", repository);
            await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        foreach (var entry in entries)
        {
            await using var insert = _db.Command();
            insert.CommandText =
                $"""
                INSERT INTO {EntriesTable} (id, repository, kind, provenance, title, body, relative_path, anchor, workspace, first_line, last_line)
                VALUES ($id, $repo, $kind, $prov, $title, $body, $path, $anchor, $workspace, $first, $last);
                INSERT INTO {FtsTable} (id, title, body) VALUES ($id, $ftsTitle, $ftsBody);
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
    }

    public Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default) => Refused<IReadOnlyList<KnowledgeEntry>>() ?? _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT {Columns} FROM {EntriesTable};";
        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }, ct);

    public Task<IReadOnlyList<KnowledgeEntry>> OfFileAsync(string repository, string relativePath, CancellationToken ct = default) =>
        Refused<IReadOnlyList<KnowledgeEntry>>() ?? _db.RunAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"SELECT {Columns} FROM {EntriesTable} WHERE repository = $r AND relative_path = $p;";
            command.Parameters.AddWithValue("$r", repository);
            command.Parameters.AddWithValue("$p", relativePath);
            return await ReadAllAsync(command, ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyDictionary<string, int>> CountByRepositoryAsync(CancellationToken ct = default) => Refused<IReadOnlyDictionary<string, int>>() ?? _db.RunAsync<IReadOnlyDictionary<string, int>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT repository, COUNT(*) FROM {EntriesTable} GROUP BY repository;";
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) counts[reader.GetString(0)] = reader.GetInt32(1);
        return counts;
    }, ct);

    public Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default) => Refused<KnowledgeEntry?>() ?? _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"SELECT {Columns} FROM {EntriesTable} WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return (await ReadAllAsync(command, ct).ConfigureAwait(false)).FirstOrDefault();
    }, ct);

    /// <summary>The refusal as an operation's answer, or null when the index answers (KSCHEMA1); the search asks it too.</summary>
    internal Task<T>? Refused<T>() => Refusal is { } refused ? Task.FromException<T>(refused.Again()) : null;

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
