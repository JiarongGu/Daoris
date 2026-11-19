using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// Where the person left a conversation: the newest event of a part of its line they had on screen while the window had focus (its
/// number and its time), when that was, and how many of its run's steps had passed then (null where its session runs none).
/// </summary>
public sealed record ReadMark(string Part, long Seq, DateTimeOffset At, DateTimeOffset Read, int? Steps);

/// <summary>
/// Each conversation's read mark on this machine, one row by the conversation's id (a record's <c>line</c>, else its own id) in the
/// desktop store's <c>read_marks</c>. A mark moves only forward: in the same part to a later event, to another part only by an event
/// kept later, since a line's parts follow one another; so every window that opens the conversation lands where the person left off.
/// </summary>
/// <remarks>
/// This machine's, never the record's: what one person has read is no fact about the work. A store that does not read keeps no mark,
/// and the conversation opens at its tail as before; a write the store refuses is a <see cref="DriverException"/>.
/// </remarks>
public sealed class ReadMarks(string home, Func<StoreSettled>? settled = null)
{
    public const string Family = "read_marks";

    private const string Columns = "part, seq, at, read, steps";

    // One writer at a time in this process, so two windows' moves each read the mark the other left; the store's lock holds the other processes.
    private static readonly Lock Gate = new();

    private string Location => DesktopStore.LocationIn(home);

    /// <summary>The mark of <paramref name="conversation"/>, or null where none is kept or the store does not read.</summary>
    public ReadMark? Read(string conversation)
    {
        if (!Names(conversation)) return null;
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            if (store is null || store.Version < DesktopStore.ReadMarksVersion) return null;
            return store.Query($"SELECT {Columns} FROM read_marks WHERE conversation = $c", Row, ("$c", conversation)).FirstOrDefault();
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Move <paramref name="conversation"/>'s mark to <paramref name="mark"/> where that is forward; the mark that then stands.</summary>
    /// <exception cref="DriverException">An id that names no conversation or part, or a store that would not take the write.</exception>
    public ReadMark Move(string conversation, ReadMark mark)
    {
        if (!Names(conversation) || !Names(mark.Part))
        {
            throw new DriverException($"`{conversation}` and `{mark.Part}` name no conversation and part, so no mark moved.");
        }

        lock (Gate)
        {
            try
            {
                using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
                if (store.Stamp.Version < DesktopStore.ReadMarksVersion) throw new DriverException("this store keeps no read marks yet.");
                ReadMark standing = mark;
                store.Write(Family, write =>
                {
                    var was = write.Query($"SELECT {Columns} FROM read_marks WHERE conversation = $c", Row, ("$c", conversation)).FirstOrDefault();
                    if (was is not null && !Forward(was, mark))
                    {
                        standing = was;
                        return;
                    }

                    write.Execute(
                        """
                        INSERT INTO read_marks (conversation, part, seq, at, read, steps) VALUES ($c, $part, $seq, $at, $read, $steps)
                        ON CONFLICT (conversation) DO UPDATE SET
                          part = excluded.part, seq = excluded.seq, at = excluded.at, read = excluded.read, steps = excluded.steps
                        """,
                        ("$c", conversation), ("$part", mark.Part), ("$seq", mark.Seq), ("$at", Moments.Write(mark.At)),
                        ("$read", Moments.Write(mark.Read)), ("$steps", mark.Steps));
                });
                return standing;
            }
            catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
            {
                throw new DriverException($"a conversation's read mark could not be written: {error.Message}");
            }
        }
    }

    /// <summary>Whether <paramref name="next"/> is past <paramref name="was"/>: a later event of the same part, or another part's later event.</summary>
    public static bool Forward(ReadMark was, ReadMark next) =>
        string.Equals(was.Part, next.Part, StringComparison.Ordinal) ? next.Seq > was.Seq : next.At > was.At;

    private static bool Names(string id) => SessionEventText.IsId(id);

    private static ReadMark Row(DbDataReader row) => new(
        row.GetString(0), row.GetInt64(1), Moment(row.GetString(2)), Moment(row.GetString(3)), row.IsDBNull(4) ? null : row.GetInt32(4));

    private static DateTimeOffset Moment(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
