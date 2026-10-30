using System.Globalization;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Where a reader of the memory's moves stands (the memory coordinator design §3): the journal's store and position, and the scope it read,
/// the repositories of its workspace.
/// </summary>
public sealed record MemoryCursor(string Store, long Position, string Scope);

/// <summary>A reader's start: the rows not yet settled in its scope, and the cursor read with them.</summary>
public sealed record MemoryBootstrap(MemoryCursor Cursor, IReadOnlyList<MemoryOp> Unsettled);

/// <summary>
/// The entries whose rows moved past a cursor, an unsettled move included, and the cursor after them; or <see cref="Revalidate"/>, where
/// the cursor no longer reads this journal whole and the reader bootstraps again.
/// </summary>
public sealed record MemoryChanges(bool Revalidate, MemoryCursor Cursor, IReadOnlyList<MemoryReference> Changed);

public sealed partial class MemoryOpStore
{
    /// <summary>Run between a bootstrap's two reads; a test's, to commit from another connection there.</summary>
    internal Func<Task>? BetweenReads { get; set; }

    /// <summary>
    /// The rows of <paramref name="repositories"/> still to settle (awaiting, ready, applying, held) and the journal's newest position, read in
    /// one transaction, so a row another connection commits is in the rows or past the cursor.
    /// </summary>
    public Task<MemoryBootstrap> BootstrapAsync(IReadOnlyCollection<string> repositories, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            IReadOnlyList<MemoryOp> unsettled;
            await using (var read = _db.Command())
            {
                read.CommandText = $"""
                    SELECT {Columns} FROM memory_ops
                    WHERE state IN ('awaiting', 'ready', 'applying', 'held') AND repository IN (SELECT value FROM json_each($scope))
                    ORDER BY rowid
                    """;
                read.Parameters.AddWithValue("$scope", JsonSerializer.Serialize(repositories));
                unsettled = await ReadAllAsync(read, inside).ConfigureAwait(false);
            }

            if (BetweenReads is { } between) await between().ConfigureAwait(false);
            var (store, _, newest) = await JournalAsync(inside).ConfigureAwait(false);
            return new MemoryBootstrap(new(store, newest, Scope(repositories)), unsettled);
        }, ct, deferred: true);

    /// <summary>
    /// The references whose rows moved past <paramref name="cursor"/> in <paramref name="repositories"/>; a re-validation where the cursor is
    /// another store's, below the journal's floor, or of another scope.
    /// </summary>
    public Task<MemoryChanges> ChangesAsync(MemoryCursor cursor, IReadOnlyCollection<string> repositories, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            var (store, floor, newest) = await JournalAsync(inside).ConfigureAwait(false);
            if (!string.Equals(cursor.Scope, Scope(repositories), StringComparison.Ordinal) || !string.Equals(cursor.Store, store, StringComparison.Ordinal)
                || floor > cursor.Position + 1 || newest < cursor.Position)
            {
                return new MemoryChanges(true, cursor, []);
            }

            await using var read = _db.Command();
            read.CommandText = """
                SELECT o.repository, j.entry FROM event_journal j JOIN memory_ops o ON o.id = j.record
                WHERE j.family = 'memory' AND j.position > $since AND j.position <= $newest AND j.entry IS NOT NULL
                  AND o.repository IN (SELECT value FROM json_each($scope))
                GROUP BY o.repository, j.entry ORDER BY min(j.position)
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
        await using var read = _db.Command();
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
