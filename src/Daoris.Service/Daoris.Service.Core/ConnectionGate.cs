using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// One transaction at a time on a connection — shared by every store that opens one on it.
/// </summary>
/// <remarks>
/// <para>A host holds ONE connection for all its stores and answers requests at once, and SQLite does
/// not nest transactions: a second <c>BEGIN</c> on a connection whose first has not ended fails with
/// <i>cannot start a transaction within a transaction</i>. Every quest write is a transaction, so a
/// take arriving while the page publishes is exactly that case.</para>
///
/// <para>Keyed by the connection, not the store, because two stores over one connection collide as
/// surely as two calls on one store: an index refresh and a publish are the same failure. Across
/// processes the file's own write lock serializes them, which is what <c>BEGIN IMMEDIATE</c> is for.</para>
/// </remarks>
internal static class ConnectionGate
{
    private static readonly ConditionalWeakTable<SqliteConnection, SemaphoreSlim> Gates = new();

    public static SemaphoreSlim For(SqliteConnection connection) =>
        Gates.GetValue(connection, _ => new SemaphoreSlim(1, 1));
}
