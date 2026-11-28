using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The moves a tree makes, as the ledger names them. A reuse, an install and a take's time to ready are noted once the keeper makes them:
/// nothing notes them before, so the ledger keeps no column of them.
/// </summary>
public static class TreeMoves
{
    public const string Made = "made";

    public const string Removed = "removed";

    public const string Restored = "restored";
}

/// <summary>One move of a repository's tree: made, removed (purged, for one set aside), or a save restored into it; the tree's own bytes before and after it.</summary>
public sealed record TreeMove(string Workspace, string Repository, string Tree, string Move, long Before, long After, DateTimeOffset At);

/// <summary>
/// One repository's reading of a day: its trees and their bytes (a hard link once), those set aside under <c>.tidied</c> apart, its saves
/// pending, the drive's free space and its floor, and the trees listed with their own bytes.
/// </summary>
public sealed record TreeReading(
    string Workspace, string Repository, DateTimeOffset At, int Trees, long SlotBytes, long LinkedBytes, int Aside, long AsideBytes,
    int SavesPending, long SavesBytes, VolumeReading? Volume, long? Floor, IReadOnlyDictionary<string, long> Listed);

/// <summary>A repository's day in the ledger: the day's last reading beside its moves, and the drive's free space at its first and last reading.</summary>
public sealed record LedgerDay(
    string Workspace, string Repository, string Day, int Trees, long SlotBytes, long LinkedBytes, int Aside, long AsideBytes, int SavesPending,
    long SavesBytes, string? Volume, long? FreeFirst, long? FreeLast, long? Floor, int Made, int Removed, int Restored, long Written, long Freed);

/// <summary>
/// The daily tree ledger per repository, in the desktop store's <c>tree_moves</c> and <c>tree_days</c>
/// (<see cref="TreeLedgerStoreStep"/>): each move a door notes, and each reading (<see cref="TreeLedgerReadings"/>).
/// </summary>
/// <remarks>Days are UTC dates. A store that does not read answers no day; a write it refuses is a <see cref="DriverException"/>.</remarks>
public sealed class TreeLedger(string home, Func<StoreSettled>? settled = null)
{
    public const string Family = "tree_ledger";

    private string Location => DesktopStore.LocationIn(home);

    internal static string DayOf(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Notes one move.</summary>
    public void Note(TreeMove move) => Write(write => write.Execute(
        "INSERT INTO tree_moves (workspace, repository, day, at, move, tree, before, after) VALUES ($ws, $repo, $day, $at, $move, $tree, $before, $after)",
        ("$ws", move.Workspace), ("$repo", move.Repository), ("$day", DayOf(move.At)), ("$at", Moments.Write(move.At)), ("$move", move.Move),
        ("$tree", TreeSaveRecords.Key(move.Tree)), ("$before", move.Before), ("$after", move.After)));

    /// <summary>Keeps a reading as its day's: the day's first free space and moment stay, the rest is this reading's.</summary>
    public void Keep(TreeReading reading) => Write(write => write.Execute(
        """
        INSERT INTO tree_days (workspace, repository, day, trees, slot_bytes, linked_bytes, aside, aside_bytes, saves_pending, saves_bytes, volume, free_first, free_last, floor, first_at, last_at, listed)
        VALUES ($ws, $repo, $day, $trees, $slot, $linked, $aside, $asideBytes, $pending, $saved, $volume, $free, $free, $floor, $at, $at, $listed)
        ON CONFLICT (workspace, repository, day) DO UPDATE SET
          trees = excluded.trees, slot_bytes = excluded.slot_bytes, linked_bytes = excluded.linked_bytes, aside = excluded.aside,
          aside_bytes = excluded.aside_bytes, saves_pending = excluded.saves_pending, saves_bytes = excluded.saves_bytes, volume = excluded.volume,
          free_first = COALESCE(free_first, excluded.free_first), free_last = excluded.free_last, floor = excluded.floor, last_at = excluded.last_at,
          listed = excluded.listed
        """,
        ("$ws", reading.Workspace), ("$repo", reading.Repository), ("$day", DayOf(reading.At)), ("$trees", reading.Trees), ("$slot", reading.SlotBytes),
        ("$linked", reading.LinkedBytes), ("$aside", reading.Aside), ("$asideBytes", reading.AsideBytes), ("$pending", reading.SavesPending),
        ("$saved", reading.SavesBytes), ("$volume", reading.Volume?.Root), ("$free", reading.Volume?.Free), ("$floor", reading.Floor),
        ("$at", Moments.Write(reading.At)), ("$listed", JsonSerializer.Serialize(reading.Listed))));

    /// <summary>The newest reading of a repository: when, and the trees it listed with their own bytes; null before the first.</summary>
    public (DateTimeOffset At, IReadOnlyDictionary<string, long> Listed)? Last(string workspace, string repository) => Read(
        "SELECT last_at, listed FROM tree_days WHERE workspace = $ws AND repository = $repo ORDER BY day DESC LIMIT 1",
        row => ((DateTimeOffset At, IReadOnlyDictionary<string, long> Listed)?)(Moment(row.GetString(0)),
            JsonSerializer.Deserialize<Dictionary<string, long>>(row.GetString(1)) ?? []),
        ("$ws", workspace), ("$repo", repository)).FirstOrDefault();

    /// <summary>The repositories the ledger holds a reading of, so one whose trees all went is read again.</summary>
    public IReadOnlyList<(string Workspace, string Repository)> Repositories() =>
        Read("SELECT DISTINCT workspace, repository FROM tree_days", row => (row.GetString(0), row.GetString(1)));

    /// <summary>Every move of a repository at or after <paramref name="since"/>.</summary>
    public IReadOnlyList<TreeMove> Since(string workspace, string repository, DateTimeOffset since) => Read(
        "SELECT workspace, repository, tree, move, before, after, at FROM tree_moves WHERE workspace = $ws AND repository = $repo AND at >= $since ORDER BY id",
        row => new TreeMove(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetInt64(4), row.GetInt64(5), Moment(row.GetString(6))),
        ("$ws", workspace), ("$repo", repository), ("$since", Moments.Write(since)));

    /// <summary>Each repository's days from <paramref name="from"/> on, oldest first: a day with a reading or a move.</summary>
    public IReadOnlyList<LedgerDay> Days(DateOnly from)
    {
        var first = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var readings = Read(
            "SELECT workspace, repository, day, trees, slot_bytes, linked_bytes, aside, aside_bytes, saves_pending, saves_bytes, volume, free_first, free_last, floor FROM tree_days WHERE day >= $from",
            row => (Key: (row.GetString(0), row.GetString(1), row.GetString(2)), Trees: row.GetInt32(3), Slot: row.GetInt64(4), Linked: row.GetInt64(5),
                Aside: row.GetInt32(6), AsideBytes: row.GetInt64(7), Pending: row.GetInt32(8), Saved: row.GetInt64(9), Volume: row.IsDBNull(10) ? null : row.GetString(10),
                FreeFirst: Number(row, 11), FreeLast: Number(row, 12), Floor: Number(row, 13)),
            ("$from", first)).ToDictionary(row => row.Key);
        var moves = Read(
            "SELECT workspace, repository, day, move, before, after FROM tree_moves WHERE day >= $from",
            row => (Key: (row.GetString(0), row.GetString(1), row.GetString(2)), Move: row.GetString(3), Before: row.GetInt64(4), After: row.GetInt64(5)),
            ("$from", first)).ToLookup(row => row.Key);
        return readings.Keys.Concat(moves.Select(group => group.Key)).Distinct()
            .OrderBy(key => key.Item3, StringComparer.Ordinal).ThenBy(key => key.Item1, StringComparer.Ordinal).ThenBy(key => key.Item2, StringComparer.Ordinal)
            .Select(key =>
            {
                var read = readings.GetValueOrDefault(key);
                var day = moves[key].ToList();
                int Count(string move) => day.Count(row => row.Move == move);
                return new LedgerDay(key.Item1, key.Item2, key.Item3, read.Trees, read.Slot, read.Linked, read.Aside, read.AsideBytes, read.Pending, read.Saved,
                    read.Volume, read.FreeFirst, read.FreeLast, read.Floor, Count(TreeMoves.Made), Count(TreeMoves.Removed), Count(TreeMoves.Restored),
                    day.Sum(row => Math.Max(0, row.After - row.Before)), day.Sum(row => Math.Max(0, row.Before - row.After)));
            })
            .ToList();
    }

    private static long? Number(DbDataReader row, int at) => row.IsDBNull(at) ? null : row.GetInt64(at);

    private static DateTimeOffset Moment(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private IReadOnlyList<T> Read<T>(string sql, Func<DbDataReader, T> map, params (string Name, object? Value)[] values)
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            if (store is null || store.Version < DesktopStore.TreeLedgerVersion) return [];
            return store.Query(sql, map, values);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            return [];
        }
    }

    private void Write(Action<DesktopWrite> work)
    {
        try
        {
            using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
            if (store.Stamp.Version < DesktopStore.TreeLedgerVersion) throw new DriverException("this store keeps no tree ledger yet.");
            store.Write(Family, work);
        }
        catch (Exception error) when (error is DesktopStoreException or SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"the tree ledger could not be written: {error.Message}");
        }
    }
}
