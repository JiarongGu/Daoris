using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// How a test lets go of its own SQLite file (FLAKE3). A store's closed connection goes back to its pool, and the pool keeps
/// the file open, so a test clears that pool before it deletes the file: the pool of the database it names, never every pool.
/// </summary>
/// <remarks>
/// <c>ClearAllPools</c> clears every pool in the process, and xUnit runs this project's classes in parallel. Microsoft.Data.Sqlite
/// marks a pooled connection active before it records who opened it, and a pool's clear disposes an active connection with
/// nobody recorded as leaked: so a clear in one class disposed a connection another class had just opened on its own file, and
/// that class's next command met <c>ObjectDisposedException</c>. No other class opens a test's own file, so its own pool's
/// clear reaches nothing being opened. A test's raw connection to such a file opens with <c>Pooling=False</c> and needs none.
/// </remarks>
internal static class SqlitePools
{
    /// <summary>Clear the pool a store opened at each of <paramref name="databases"/> uses; one no store opened clears nothing.</summary>
    public static void Release(params string[] databases)
    {
        foreach (var database in databases)
        {
            using var named = new SqliteConnection(SqliteKnowledgeStore.ConnectionString(database));
            SqliteConnection.ClearPool(named);
        }
    }
}
