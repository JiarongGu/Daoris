using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The index over the session events files, in the desktop store (<c>session_event_heads</c> and <c>session_event_offsets</c>): a row for
/// each long record, so a read of what its end or its newest page says reads a page or a line of the file, never the whole of it.
/// </summary>
/// <remarks>
/// <para><b>The file stays the record</b>: append-only, machine-local, read by a person or a tool as it is. A head is built by the first
/// reader that finds none, and brought up by the reader that finds the file longer than the head reached, reading only the lines written
/// since; one whose file no longer holds its last line (forgotten and written again, replaced, salvaged) is built again.</para>
/// <para><b>Short records read whole</b>: under <see cref="HeadedFrom"/> a parse costs less than a row's read. <b>Without the store</b> (a
/// folder that is not a home's, a store this build cannot open or write) <see cref="Of"/> answers null and every reader reads the file
/// whole, as it always has.</para>
/// </remarks>
internal sealed class SessionEventHeads
{
    /// <summary>A record shorter than this many bytes is read whole.</summary>
    public const long HeadedFrom = 512 * 1024;

    private const string Family = "session_event_heads";

    private const string Columns =
        "length, line_start, tail, first_seq, last_seq, turns, opening_at, opening_seq, plan_at, failed_id, first_failure, choices, reported, input, output, cache_read, cache_write";

    private static readonly Lock Gate = new();

    private static readonly Dictionary<string, SessionEventHead> Kept = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _location;

    private SessionEventHeads(string location) => _location = location;

    /// <summary>The index of the sessions folder <paramref name="directory"/>, or null where it is not a home's (<c>data/sessions</c>), which has no store beside it.</summary>
    public static SessionEventHeads? In(string directory)
    {
        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var data = Path.GetDirectoryName(full);
        if (data is null || Path.GetFileName(full) != "sessions" || Path.GetFileName(data) != "data" || Path.GetDirectoryName(data) is not { } home) return null;
        return new SessionEventHeads(DesktopStore.LocationIn(home));
    }

    /// <summary>
    /// The head that stands for the record of <paramref name="sessionId"/> at <paramref name="path"/>, brought up to its end; null for a
    /// record too short to be headed, no record, or no store to keep it in.
    /// </summary>
    public SessionEventHead? Of(string sessionId, string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length < HeadedFrom) return null;
            lock (Gate)
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var stored = Kept.GetValueOrDefault(path) ?? Row(sessionId);
                var standing = stored is not null && stored.Stands(stream);
                if (standing && stream.Length == stored!.Length) return Remember(path, stored);
                var head = (standing ? stored! : SessionEventHead.Empty).ReadOn(stream);
                Remember(path, head);
                Save(sessionId, head, rebuilt: !standing && stored is not null, from: standing ? stored!.Offsets.Count : 0);
                return head;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException or DesktopStoreException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Let go of the head of a record that is gone.</summary>
    public void Forget(string sessionId, string path)
    {
        lock (Gate)
        {
            Kept.Remove(path);
            try
            {
                if (Row(sessionId) is null) return;
                using var store = DesktopStore.Open(_location, StoreProcess.Options(StartConfirmation.Read));
                store.Write(Family, write =>
                {
                    write.Execute("DELETE FROM session_event_heads WHERE session = $s", ("$s", sessionId));
                    write.Execute("DELETE FROM session_event_offsets WHERE session = $s", ("$s", sessionId));
                });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException or DesktopStoreException)
            {
                // An index row left behind no longer stands for any file: the next record under the id builds it again.
            }
        }
    }

    /// <summary>What this process kept of the heads, let go: a test's stand-in for a process come up since.</summary>
    internal static void LetGoOfMemory()
    {
        lock (Gate) Kept.Clear();
    }

    private static SessionEventHead Remember(string path, SessionEventHead head) => Kept[path] = head;

    private SessionEventHead? Row(string sessionId)
    {
        using var store = DesktopStoreReader.Open(_location);
        if (store is null || store.Version < DesktopStore.SessionEventSpentVersion) return null;
        var offsets = store.Query("SELECT seq, at FROM session_event_offsets WHERE session = $s ORDER BY seq",
            row => new KeyValuePair<long, long>(row.GetInt64(0), row.GetInt64(1)), ("$s", sessionId));
        return store.Query($"SELECT {Columns} FROM session_event_heads WHERE session = $s", row => new SessionEventHead(
            row.GetInt64(0), row.GetInt64(1), row.GetString(2), row.GetInt64(3), row.GetInt64(4),
            new SessionSpent((int)row.GetInt64(5), (int)row.GetInt64(12), Count(row, 13), Count(row, 14), Count(row, 15), Count(row, 16)),
            row.GetInt64(6), row.GetInt64(7), row.GetInt64(8), row.IsDBNull(9) ? null : row.GetString(9), row.GetInt64(10), SessionEventHead.ReadChoices(row.GetString(11)), offsets),
            ("$s", sessionId)).FirstOrDefault();
    }

    private static long? Count(System.Data.Common.DbDataReader row, int column) => row.IsDBNull(column) ? null : row.GetInt64(column);

    private void Save(string sessionId, SessionEventHead head, bool rebuilt, int from)
    {
        try
        {
            using var store = DesktopStore.Open(_location, StoreProcess.Options(StartConfirmation.Read));
            store.Write(Family, write =>
            {
                if (rebuilt) write.Execute("DELETE FROM session_event_offsets WHERE session = $s", ("$s", sessionId));
                write.Execute(
                    $"""
                    INSERT INTO session_event_heads (session, {Columns}) VALUES ($s, $length, $lineStart, $tail, $first, $last, $turns, $openingAt, $openingSeq, $planAt, $failed, $firstFailure, $choices, $reported, $input, $output, $cacheRead, $cacheWrite)
                    ON CONFLICT (session) DO UPDATE SET length = excluded.length, line_start = excluded.line_start, tail = excluded.tail, first_seq = excluded.first_seq,
                      last_seq = excluded.last_seq, turns = excluded.turns, opening_at = excluded.opening_at, opening_seq = excluded.opening_seq, plan_at = excluded.plan_at,
                      failed_id = excluded.failed_id, first_failure = excluded.first_failure, choices = excluded.choices,
                      reported = excluded.reported, input = excluded.input, output = excluded.output, cache_read = excluded.cache_read, cache_write = excluded.cache_write
                    """,
                    ("$s", sessionId), ("$length", head.Length), ("$lineStart", head.LineStart), ("$tail", head.Tail), ("$first", head.FirstSeq),
                    ("$last", head.LastSeq), ("$turns", head.Turns), ("$reported", head.Spent.Reported), ("$input", head.Spent.Input), ("$output", head.Spent.Output),
                    ("$cacheRead", head.Spent.CacheRead), ("$cacheWrite", head.Spent.CacheWrite), ("$openingAt", head.OpeningAt), ("$openingSeq", head.OpeningSeq), ("$planAt", head.PlanAt),
                    ("$failed", head.FailedId), ("$firstFailure", head.FirstFailure), ("$choices", SessionEventHead.WriteChoices(head.Choices)));
                foreach (var (seq, at) in head.Offsets.Skip(rebuilt ? 0 : from))
                {
                    write.Execute("INSERT OR REPLACE INTO session_event_offsets (session, seq, at) VALUES ($s, $seq, $at)", ("$s", sessionId), ("$seq", seq), ("$at", at));
                }
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException or DesktopStoreException)
        {
            // The head is an index: where the store will not take it, this process keeps it and a later one reads the file again.
        }
    }
}
