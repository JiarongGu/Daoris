using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The salvage folder's copy of a store is SQLite's backup: one file holding the log's rows, no journal file beside it.</summary>
public sealed class SalvageCopyTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "salvage-copy", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_root);
    }

    private string Home => Path.Combine(_root, "home");

    [Fact]
    public void A_store_with_rows_still_in_its_log_is_copied_whole_with_no_journal_file()
    {
        var path = DaorisHome.Entry(Home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var live = new SqliteConnection($"Data Source={path};Pooling=False");
        live.Open();
        using (var command = live.CreateCommand())
        {
            command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE t (v TEXT); INSERT INTO t VALUES ('in the log');";
            command.ExecuteNonQuery();
        }

        Assert.True(File.Exists(path + "-wal"));
        var run = new HomeMigrationRun(Home, "test");

        run.Keep(path);

        var copy = Path.Combine(run.Copies, Path.GetRelativePath(DaorisHome.FolderOf(Home).Folder, path));
        Assert.True(File.Exists(copy));
        Assert.False(File.Exists(copy + "-wal"));
        Assert.False(File.Exists(copy + "-shm"));
        using var read = new SqliteConnection($"Data Source={copy};Mode=ReadOnly;Pooling=False");
        read.Open();
        using var query = read.CreateCommand();
        query.CommandText = "SELECT v FROM t";
        Assert.Equal("in the log", query.ExecuteScalar());
    }

    [Fact]
    public void A_file_that_is_no_store_is_copied_as_it_is()
    {
        var path = Path.Combine(DaorisHome.FolderOf(Home).Folder, "data", "plain.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"a\":1}");
        var run = new HomeMigrationRun(Home, "test");

        run.Keep(path);

        Assert.Equal("{\"a\":1}", File.ReadAllText(Path.Combine(run.Copies, "data", "plain.json")));
    }
}
