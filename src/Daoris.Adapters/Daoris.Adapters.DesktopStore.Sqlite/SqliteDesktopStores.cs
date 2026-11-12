using System.Data.Common;
using Daoris.Ports.Store;
using Microsoft.Data.Sqlite;

namespace Daoris.Adapters.DesktopStore.Sqlite;

/// <summary>The engines a root hands the application for the desktop store: this adapter's, on SQLite files.</summary>
public static class SqliteDesktopStores
{
    /// <summary>How long a read-only connection waits on the file's lock, in seconds: a reader never waits on a writer for long.</summary>
    private const int ReadBusySeconds = 5;

    /// <summary>The engines: open a store, connect to any file of the home, release the pooled connections.</summary>
    public static DesktopStoreEngines Engines { get; } = new(SqliteDesktopStoreEngine.Open, Connect, Backup, SqliteConnection.ClearAllPools);

    private static void Backup(string source, string target)
    {
        using (var from = (SqliteConnection)Connect(source, DesktopOpen.ReadWrite))
        using (var to = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString()))
        {
            to.Open();
            from.BackupDatabase(to);
        }

        SqliteConnection.ClearAllPools();
    }

    private static DbConnection Connect(string path, DesktopOpen mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode switch
            {
                DesktopOpen.ReadOnly => SqliteOpenMode.ReadOnly,
                DesktopOpen.ReadWrite => SqliteOpenMode.ReadWrite,
                _ => SqliteOpenMode.ReadWriteCreate,
            },
            Pooling = false,
            DefaultTimeout = ReadBusySeconds,
        }.ToString());
        try
        {
            connection.Open();
            if (mode == DesktopOpen.ReadOnly)
            {
                // The connection string's timeout is the commands', and the file's lock wait is its own to set.
                using var wait = connection.CreateCommand();
                wait.CommandText = $"PRAGMA busy_timeout = {ReadBusySeconds * 1000}";
                wait.ExecuteNonQuery();
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
