using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// One row of the event journal as a reader is answered it (FOUNDATION1 l-i, the foundation design §4.1): the raw transition and the snapshot
/// its coding needs, the columns the capture schema names, never a text. <c>NoteCodes</c> and <c>Marks</c> are the lists the triggers wrote,
/// comma-joined.
/// </summary>
public sealed record JournalRow(
    long Position, long At, string Record, string? Family, string Change, string? StateOld, string? StateNew, bool? LimitedOld, bool? LimitedNew,
    string? NoteCodes, string? Marks, string? Entry, string? FromSession, string? FromWhy, string? FromBy, string? Workspace, string? Quest,
    string? Word, string? WordAuthor, string? WordLevel, bool? WordKeyed, long? WordsOver, string? Op, string? Ask, string? Message);

/// <summary>A page of the journal after a cursor, with where the journal stands: the store's identity, its floor and its newest position.</summary>
public sealed record JournalPage(string Store, long Floor, long Newest, IReadOnlyList<JournalRow> Rows);

/// <summary>
/// The journal read by position (FOUNDATION1 l-i): a short read-only connection of its own, so a reader never waits on the stream's observer
/// or a writer under write-ahead logging. A database with no journal yet answers an empty page.
/// </summary>
public static class JournalRows
{
    /// <summary>The door it answers at (a local host's only, beside the change stream).</summary>
    public const string Route = "/api/journal";

    /// <summary>The most rows one read answers.</summary>
    public const int Most = 500;

    private const string Columns =
        "position, at, record, family, change, state_old, state_new, limited_old, limited_new, note_codes, marks, entry, from_session, from_why, from_by, "
        + "workspace, quest, word, word_author, word_level, word_keyed, words_over, op, ask, message";

    /// <summary>The rows with a position past <paramref name="since"/>, oldest first, at most <paramref name="limit"/> (and <see cref="Most"/>).</summary>
    public static async Task<JournalPage> ReadAsync(string database, long since, int limit, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(ct).ConfigureAwait(false);
        if (await ScalarAsync(connection, "SELECT count(*) FROM sqlite_master WHERE name IN ('event_journal', 'store_identity')", ct) < 2)
        {
            return new JournalPage("", 0, 0, []);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM store_identity WHERE one = 1";
        var store = (await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) as string ?? "";
        var newest = await ScalarAsync(connection, "SELECT COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'event_journal'), 0)", ct);
        var floor = await ScalarAsync(connection, $"SELECT COALESCE((SELECT min(position) FROM event_journal), {newest + 1})", ct);

        await using var read = connection.CreateCommand();
        read.CommandText = $"SELECT {Columns} FROM event_journal WHERE position > $since ORDER BY position LIMIT $limit";
        read.Parameters.AddWithValue("$since", since);
        read.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, Most));
        await using var reader = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var rows = new List<JournalRow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
            long? Number(int i) => reader.IsDBNull(i) ? null : reader.GetInt64(i);
            bool? Flag(int i) => reader.IsDBNull(i) ? null : reader.GetInt64(i) != 0;
            rows.Add(new JournalRow(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), Text(3), reader.GetString(4), Text(5), Text(6), Flag(7), Flag(8), Text(9),
                Text(10), Text(11), Text(12), Text(13), Text(14), Text(15), Text(16), Text(17), Text(18), Text(19), Flag(20), Number(21), Text(22), Text(23),
                Text(24)));
        }

        return new JournalPage(store, floor, newest, rows);
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
    }
}
