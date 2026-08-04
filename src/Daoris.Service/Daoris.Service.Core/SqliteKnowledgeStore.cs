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
/// <para><b>No migrations, deliberately.</b> The index is <em>derived</em> data: every entry in it can
/// be read again from the repositories in seconds. A schema change therefore does not need migrating,
/// it needs rebuilding — so the schema carries a version, and a mismatch drops the tables and starts
/// over. The cognition sibling's storage uses a migration runner because its data is authored and
/// cannot be regenerated; the same choice here would be ceremony guarding something that is not at
/// risk.</para>
///
/// <para>FTS5 ships in the standard SQLite build, so full-text search costs no extra dependency and
/// replaces a hand-rolled scorer with a ranked one.</para>
/// </remarks>
public sealed class SqliteKnowledgeStore : IKnowledgeStore, IAsyncDisposable
{
    /// <summary>Bump when the schema changes. A mismatch rebuilds rather than migrates.</summary>
    private const int SchemaVersion = 1;

    private readonly SqliteConnection _connection;

    private SqliteKnowledgeStore(SqliteConnection connection) => _connection = connection;

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
        await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        var version = Convert.ToInt32(await ScalarAsync("PRAGMA user_version;", ct).ConfigureAwait(false));
        if (version != SchemaVersion)
        {
            await ExecuteAsync("DROP TABLE IF EXISTS entries_fts; DROP TABLE IF EXISTS entries;", ct)
                .ConfigureAwait(false);
        }

        await ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS entries (
                id            TEXT PRIMARY KEY,
                repository    TEXT NOT NULL,
                kind          INTEGER NOT NULL,
                provenance    INTEGER NOT NULL,
                title         TEXT NOT NULL,
                body          TEXT NOT NULL,
                relative_path TEXT NOT NULL,
                anchor        TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_entries_repository ON entries(repository);

            -- id is stored but not indexed: it is how a hit gets back to its row, never a search term.
            CREATE VIRTUAL TABLE IF NOT EXISTS entries_fts
                USING fts5(id UNINDEXED, title, body, tokenize='unicode61');
            """, ct).ConfigureAwait(false);

        await ExecuteAsync($"PRAGMA user_version = {SchemaVersion};", ct).ConfigureAwait(false);
    }

    public async Task ReplaceRepositoryAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct = default)
    {
        await using var transaction = await _connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Both tables, in one transaction: an FTS row whose entry is gone would return a hit that
        // cannot be resolved, which reads as data loss rather than as a stale index.
        await using (var delete = _connection.CreateCommand())
        {
            delete.Transaction = (SqliteTransaction)transaction;
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
            await using var insert = _connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText =
                """
                INSERT INTO entries (id, repository, kind, provenance, title, body, relative_path, anchor)
                VALUES ($id, $repo, $kind, $prov, $title, $body, $path, $anchor);
                INSERT INTO entries_fts (id, title, body) VALUES ($id, $title, $body);
                """;
            insert.Parameters.AddWithValue("$id", entry.Id);
            insert.Parameters.AddWithValue("$repo", entry.Repository);
            insert.Parameters.AddWithValue("$kind", (int)entry.Kind);
            insert.Parameters.AddWithValue("$prov", (int)entry.Provenance);
            insert.Parameters.AddWithValue("$title", entry.Title);
            insert.Parameters.AddWithValue("$body", entry.Body);
            insert.Parameters.AddWithValue("$path", entry.RelativePath);
            insert.Parameters.AddWithValue("$anchor", (object?)entry.Anchor ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM entries;";
        return await ReadAllAsync(command, ct).ConfigureAwait(false);
    }

    public async Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM entries WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return (await ReadAllAsync(command, ct).ConfigureAwait(false)).FirstOrDefault();
    }

    internal const string Columns = "id, repository, kind, provenance, title, body, relative_path, anchor";

    /// <summary>The same columns, in the same order, qualified for a join. <see cref="Read"/> reads by ordinal.</summary>
    internal const string QualifiedColumns =
        "e.id, e.repository, e.kind, e.provenance, e.title, e.body, e.relative_path, e.anchor";

    internal SqliteConnection Connection => _connection;

    internal static KnowledgeEntry Read(SqliteDataReader reader) => new(
        reader.GetString(1),
        (EntryKind)reader.GetInt32(2),
        (Provenance)reader.GetInt32(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7));

    private static async Task<IReadOnlyList<KnowledgeEntry>> ReadAllAsync(SqliteCommand command, CancellationToken ct)
    {
        var entries = new List<KnowledgeEntry>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) entries.Add(Read(reader));
        return entries;
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<object?> ScalarAsync(string sql, CancellationToken ct)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
