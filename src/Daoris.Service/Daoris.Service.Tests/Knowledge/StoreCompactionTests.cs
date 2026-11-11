using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A store that once held a bulk write does not keep its high-water mark: its log, fully checkpointed, is truncated and the pages a large
/// delete freed are returned to the file, at open. A store with little free is left as it is, and the limit on the log stays set.
/// </summary>
public sealed class StoreCompactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-compaction-" + Guid.NewGuid().ToString("N")[..8]);

    private string Database => Path.Combine(_root, "knowledge.db");

    public StoreCompactionTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        SqlitePools.Release(Database);
        for (var attempt = 0; attempt < 20 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static void Run(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>A file whose one commit wrote 24 MB into its log, fully checkpointed, then deleted it all: log and free pages at their mark.</summary>
    private SqliteConnection Bloated()
    {
        var holder = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        holder.Open();
        Run(holder, "PRAGMA journal_mode = WAL; PRAGMA wal_autocheckpoint = 0; CREATE TABLE bulk (id INTEGER PRIMARY KEY, body BLOB);");
        Run(holder, "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 24) INSERT INTO bulk (body) SELECT zeroblob(1048576) FROM n;");
        Run(holder, "DELETE FROM bulk;");
        Run(holder, "PRAGMA wal_checkpoint(PASSIVE);");
        return holder;
    }

    [Fact]
    public async Task Opening_a_store_truncates_its_log_and_returns_the_pages_a_large_delete_freed()
    {
        using var holder = Bloated();
        Assert.True(new FileInfo(Database + "-wal").Length > 16 * 1024 * 1024, "the fixture's log is not at its mark");
        var before = new FileInfo(Database).Length;

        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            Assert.True(new FileInfo(Database + "-wal").Length < 4 * 1024 * 1024, "the log kept its mark");
        }

        Assert.True(new FileInfo(Database).Length < before / 2, "the freed pages were not returned");
    }

    [Fact]
    public async Task A_store_with_little_free_is_not_vacuumed()
    {
        using var holder = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        holder.Open();
        Run(holder, "PRAGMA journal_mode = WAL; CREATE TABLE small (id INTEGER PRIMARY KEY, body TEXT); INSERT INTO small (body) VALUES ('kept');");
        var before = new FileInfo(Database).Length;

        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            Assert.Equal(before, new FileInfo(Database).Length);
        }
    }

    [Fact]
    public async Task The_log_is_limited_on_each_open()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        await connection.OpenAsync();
        await StoreJournal.SetAsync(connection, CancellationToken.None);
        using var read = connection.CreateCommand();
        read.CommandText = "PRAGMA journal_size_limit;";
        Assert.Equal(StoreJournal.LogLimit, (long)(await read.ExecuteScalarAsync())!);
    }
}
