using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// D236: the store writes ahead into a log beside its file and syncs each commit, never a rollback journal made and deleted at every
/// commit. The mode is the file's own, so every host that opens it after the first writes the same way; the sync is each connection's,
/// so the store sets it at every open.
/// </summary>
public sealed class StoreJournalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-journal-" + Guid.NewGuid().ToString("N")[..8]);

    private string Database => Path.Combine(_root, "knowledge.db");

    public StoreJournalTests() => Directory.CreateDirectory(_root);

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

    [Fact]
    public async Task A_store_writes_ahead_beside_its_file_and_another_connection_finds_the_file_in_that_mode()
    {
        await using (var store = await SqliteKnowledgeStore.OpenAsync(Database))
        {
            await store.ReplaceRepositoryAsync("alpha", []);
            Assert.True(File.Exists(Database + "-wal"), "a commit wrote no log beside the store's file");
            Assert.False(File.Exists(Database + "-journal"), "a commit left a rollback journal");
            Assert.Equal("wal", Scalar(Database, "PRAGMA journal_mode;"));
        }
    }

    [Fact]
    public async Task Each_open_syncs_every_commit_in_full()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        await connection.OpenAsync();
        await StoreJournal.SetAsync(connection, CancellationToken.None);
        using var read = connection.CreateCommand();
        read.CommandText = "PRAGMA synchronous;";
        Assert.Equal(2L, (long)(await read.ExecuteScalarAsync())!);
        read.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", (string)(await read.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task A_store_in_memory_opens_as_it_did()
    {
        await using var store = await SqliteKnowledgeStore.OpenAsync(":memory:");
        await store.ReplaceRepositoryAsync("alpha", []);
    }

    private static string Scalar(string database, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar())!;
    }
}
