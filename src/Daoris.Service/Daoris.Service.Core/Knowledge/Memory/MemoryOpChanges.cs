using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// What moved in the memory log since a cursor: the rows still to settle with the journal's newest position, and the references whose rows
/// moved past a position (the memory coordinator design §3).
/// </summary>
internal sealed class MemoryOpChanges(
    ConnectionGate db,
    Func<SqliteCommand, CancellationToken, Task<IReadOnlyList<MemoryOp>>> readAll,
    Func<Func<Task>?> betweenReads)
{
    /// <summary>
    /// The rows of <paramref name="repositories"/> still to settle (awaiting, ready, applying, held) and the journal's newest position, read in
    /// one transaction, so a row another connection commits is in the rows or past the cursor.
    /// </summary>
    public Task<MemoryBootstrap> BootstrapAsync(IReadOnlyCollection<string> repositories, CancellationToken ct) =>
        db.InTransactionAsync(async (_, inside) =>
        {
            IReadOnlyList<MemoryOp> unsettled;
            await using (var read = db.Command())
            {
                read.CommandText = $"""
                    SELECT {MemoryOpStore.Columns} FROM memory_ops
                    WHERE state IN ('awaiting', 'ready', 'applying', 'held') AND repository IN (SELECT value FROM json_each($scope))
                    ORDER BY rowid
                    """;
                read.Parameters.AddWithValue("$scope", JsonSerializer.Serialize(repositories));
                unsettled = await readAll(read, inside).ConfigureAwait(false);
            }

            if (betweenReads() is { } between) await between().ConfigureAwait(false);
            var (store, _, newest) = await JournalAsync(inside).ConfigureAwait(false);
            return new MemoryBootstrap(new(store, newest, Scope(repositories)), unsettled);
        }, ct, deferred: true);

    /// <summary>The journal's newest position over <paramref name="repositories"/>, as a cursor <see cref="ChangesAsync"/> reads past.</summary>
    public Task<MemoryCursor> CursorAsync(IReadOnlyCollection<string> repositories, CancellationToken ct) =>
        db.InTransactionAsync(async (_, inside) =>
        {
            var (store, _, newest) = await JournalAsync(inside).ConfigureAwait(false);
            return new MemoryCursor(store, newest, Scope(repositories));
        }, ct, deferred: true);

    /// <summary>
    /// The references whose rows moved past <paramref name="cursor"/> in <paramref name="repositories"/>, a replacement's predecessors with
    /// its successor; a re-validation where the cursor is another store's, below the journal's floor, or of another scope.
    /// </summary>
    public Task<MemoryChanges> ChangesAsync(MemoryCursor cursor, IReadOnlyCollection<string> repositories, CancellationToken ct) =>
        db.InTransactionAsync(async (_, inside) =>
        {
            var (store, floor, newest) = await JournalAsync(inside).ConfigureAwait(false);
            if (!string.Equals(cursor.Scope, Scope(repositories), StringComparison.Ordinal) || !string.Equals(cursor.Store, store, StringComparison.Ordinal)
                || floor > cursor.Position + 1 || newest < cursor.Position)
            {
                return new MemoryChanges(true, cursor, []);
            }

            await using var read = db.Command();
            // The journal names a row's target or the entry it wrote; a replacement's predecessors are read from its row.
            read.CommandText = """
                SELECT repository, entry FROM (
                  SELECT o.repository, j.entry, j.position FROM event_journal j JOIN memory_ops o ON o.id = j.record
                  WHERE j.family = 'memory' AND j.position > $since AND j.position <= $newest AND j.entry IS NOT NULL
                    AND o.repository IN (SELECT value FROM json_each($scope))
                  UNION ALL
                  SELECT o.repository, json_extract(p.value, '$.entry'), j.position
                  FROM event_journal j JOIN memory_ops o ON o.id = j.record, json_each(o.predecessors) p
                  WHERE j.family = 'memory' AND j.position > $since AND j.position <= $newest AND o.kind = 'supersede'
                    AND o.repository IN (SELECT value FROM json_each($scope)))
                GROUP BY repository, entry ORDER BY min(position)
                """;
            read.Parameters.AddWithValue("$since", cursor.Position);
            read.Parameters.AddWithValue("$newest", newest);
            read.Parameters.AddWithValue("$scope", JsonSerializer.Serialize(repositories));
            var changed = new List<MemoryReference>();
            await using var reader = await read.ExecuteReaderAsync(inside).ConfigureAwait(false);
            while (await reader.ReadAsync(inside).ConfigureAwait(false)) changed.Add(new(reader.GetString(0), reader.GetString(1)));
            return new MemoryChanges(false, cursor with { Position = newest }, changed);
        }, ct, deferred: true);

    /// <summary>The journal's store, the lowest position it keeps (one past the newest where it keeps none) and its newest.</summary>
    private async Task<(string Store, long Floor, long Newest)> JournalAsync(CancellationToken inside)
    {
        await using var read = db.Command();
        read.CommandText = """
            SELECT (SELECT id FROM store_identity WHERE one = 1),
                   COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'event_journal'), 0),
                   (SELECT min(position) FROM event_journal)
            """;
        await using var reader = await read.ExecuteReaderAsync(inside).ConfigureAwait(false);
        await reader.ReadAsync(inside).ConfigureAwait(false);
        var newest = reader.GetInt64(1);
        return (reader.IsDBNull(0) ? "" : reader.GetString(0), reader.IsDBNull(2) ? newest + 1 : reader.GetInt64(2), newest);
    }

    private static string Scope(IReadOnlyCollection<string> repositories) =>
        string.Join(',', repositories.Order(StringComparer.Ordinal).Select(name => name.Length.ToString(CultureInfo.InvariantCulture) + ":" + name));
}
