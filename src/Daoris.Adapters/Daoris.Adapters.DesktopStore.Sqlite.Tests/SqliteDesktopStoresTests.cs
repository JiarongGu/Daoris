using System.Data.Common;
using Daoris.Ports.Store;
using Daoris.Testing;
using Xunit;

namespace Daoris.Adapters.DesktopStore.Sqlite.Tests;

[Trait("Category", "Needs")]
[Trait("Needs", "filesystem")]
public sealed class SqliteDesktopStoresTests : IDisposable
{
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "store-files", Guid.NewGuid().ToString("N"));

    private DesktopStoreEngineOptions Options => new(1, new MovedClock(), "files");

    [Fact]
    public void A_store_is_made_with_its_folder_and_keeps_its_journal_in_write_ahead_mode()
    {
        var place = Path.Combine(_folder, "deeper", "desktop.db");

        using var engine = SqliteDesktopStores.Engines.Open(place, Options);

        Assert.True(File.Exists(place));
        Assert.Equal("wal", engine.Read(connection => Scalar(connection, "PRAGMA journal_mode")));
        Assert.Equal("1", engine.Read(connection => Scalar(connection, "PRAGMA foreign_keys")));
    }

    [Fact]
    public void A_read_only_connection_reads_what_a_store_holds_and_writes_nothing()
    {
        var place = Path.Combine(_folder, "desktop.db");
        using (var engine = SqliteDesktopStores.Engines.Open(place, Options))
            engine.Apply(1, 1, (connection, transaction) => Exec(connection, transaction, "CREATE TABLE store_version (one INTEGER PRIMARY KEY, version INTEGER NOT NULL, writer INTEGER NOT NULL, build TEXT NOT NULL, at TEXT NOT NULL)"));

        using var reader = SqliteDesktopStores.Engines.Connect(place, DesktopOpen.ReadOnly);

        Assert.Equal("1", Scalar(reader, "SELECT version FROM store_version"));
        Assert.ThrowsAny<DbException>(() => Exec(reader, null, "CREATE TABLE refused (id INTEGER)"));
    }

    [Fact]
    public void A_file_that_is_not_there_is_not_made_by_a_read_write_connection()
    {
        var place = Path.Combine(_folder, "missing.db");

        Assert.ThrowsAny<DbException>(() => SqliteDesktopStores.Engines.Connect(place, DesktopOpen.ReadWrite));

        Assert.False(File.Exists(place));
    }

    public void Dispose()
    {
        SqliteDesktopStores.Engines.ReleaseConnections();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or DirectoryNotFoundException)
        {
            // Scratch under the build's output; the next run finds it again.
        }
    }

    private static string Scalar(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    private static void Exec(DbConnection connection, DbTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
