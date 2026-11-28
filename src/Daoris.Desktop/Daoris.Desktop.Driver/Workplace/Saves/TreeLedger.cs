using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>The moves a copy makes, as the ledger names them.</summary>
public static class TreeMoves
{
    public const string Made = "made";

    public const string Reused = "reused";

    public const string Removed = "removed";

    public const string Installed = "installed";

    public const string Restored = "restored";
}

/// <summary>
/// One move of a repository's copy: made, reused, removed, its dependencies installed, or a save restored into it; the copy's own bytes
/// before and after it (a hard link is the store's, counted there once), and for a take how long it took to be ready.
/// </summary>
public sealed record TreeMove(
    string Workspace, string Repository, string Tree, string Move, long Before, long After, DateTimeOffset At, long? ReadyMs = null);

/// <summary>One repository's reading of a day: its copies and their bytes, its saves pending, the drive's free space, the copies listed.</summary>
public sealed record TreeReading(
    string Workspace, string Repository, DateTimeOffset At, int Trees, long SlotBytes, long LinkedBytes, int SavesPending, long SavesBytes,
    VolumeReading? Volume, IReadOnlyDictionary<string, long> Listed);

/// <summary>A repository's day in the ledger: the day's last reading beside its moves, and the drive's free space at its first and last reading.</summary>
public sealed record LedgerDay(
    string Workspace, string Repository, string Day, int Trees, long SlotBytes, long LinkedBytes, int SavesPending, long SavesBytes,
    string? Volume, long? FreeFirst, long? FreeLast, int Made, int Reused, int Removed, int Installed, int Restored, long Written, long Freed,
    long? ReadyP50, long? ReadyP95)
{
    /// <summary>The share of takes that reused a copy: reused over made and reused; 0 with no take.</summary>
    public double ReuseRate => Made + Reused == 0 ? 0 : (double)Reused / (Made + Reused);
}

/// <summary>
/// The daily tree ledger per repository, in the desktop store's <c>tree_moves</c> and <c>tree_days</c>
/// (<see cref="TreeLedgerStoreStep"/>): each move a door or the keeper notes, and each reading (<see cref="TreeLedgerReadings"/>).
/// </summary>
/// <remarks>Days are UTC dates. A store that does not read answers no day; a write it refuses is a <see cref="DriverException"/>.</remarks>
public sealed class TreeLedger(string home, Func<StoreSettled>? settled = null)
{
    public const string Family = "tree_ledger";

    private string Location => DesktopStore.LocationIn(home);

    internal static string DayOf(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Notes one move.</summary>
    public void Note(TreeMove move) => Write(write => write.Execute(
        "INSERT INTO tree_moves (workspace, repository, day, at, move, tree, before, after, ready) VALUES ($ws, $repo, $day, $at, $move, $tree, $before, $after, $ready)",
        ("$ws", move.Workspace), ("$repo", move.Repository), ("$day", DayOf(move.At)), ("$at", Moments.Write(move.At)), ("$move", move.Move),
        ("$tree", TreeSaveRecords.Key(move.Tree)), ("$before", move.Before), ("$after", move.After), ("$ready", move.ReadyMs)));

    /// <summary>Keeps a reading as its day's: the day's first free space and moment stay, the rest is this reading's.</summary>
    public void Keep(TreeReading reading) => Write(write => write.Execute(
        """
        INSERT INTO tree_days (workspace, repository, day, trees, slot_bytes, linked_bytes, saves_pending, saves_bytes, volume, free_first, free_last, first_at, last_at, listed)
        VALUES ($ws, $repo, $day, $trees, $slot, $linked, $pending, $saved, $volume, $free, $free, $at, $at, $listed)
        ON CONFLICT (workspace, repository, day) DO UPDATE SET
          trees = excluded.trees, slot_bytes = excluded.slot_bytes, linked_bytes = excluded.linked_bytes, saves_pending = excluded.saves_pending,
          saves_bytes = excluded.saves_bytes, volume = excluded.volume, free_first = COALESCE(free_first, excluded.free_first),
          free_last = excluded.free_last, last_at = excluded.last_at, listed = excluded.listed
        """,
        ("$ws", reading.Workspace), ("$repo", reading.Repository), ("$day", DayOf(reading.At)), ("$trees", reading.Trees), ("$slot", reading.SlotBytes),
        ("$linked", reading.LinkedBytes), ("$pending", reading.SavesPending), ("$saved", reading.SavesBytes), ("$volume", reading.Volume?.Root),
        ("$free", reading.Volume?.Free), ("$at", Moments.Write(reading.At)), ("$listed", JsonSerializer.Serialize(reading.Listed))));

    /// <summary>The newest reading of a repository: when, and the copies it listed with their own bytes; null before the first.</summary>
    public (DateTimeOffset At, IReadOnlyDictionary<string, long> Listed)? Last(string workspace, string repository) => Read(
        "SELECT last_at, listed FROM tree_days WHERE workspace = $ws AND repository = $repo ORDER BY day DESC LIMIT 1",
        row => ((DateTimeOffset At, IReadOnlyDictionary<string, long> Listed)?)(Moment(row.GetString(0)),
            JsonSerializer.Deserialize<Dictionary<string, long>>(row.GetString(1)) ?? []),
        ("$ws", workspace), ("$repo", repository)).FirstOrDefault();

    /// <summary>Every move of a repository at or after <paramref name="since"/>.</summary>
    public IReadOnlyList<TreeMove> Since(string workspace, string repository, DateTimeOffset since) => Read(
        "SELECT workspace, repository, tree, move, before, after, at, ready FROM tree_moves WHERE workspace = $ws AND repository = $repo AND at >= $since ORDER BY id",
        row => new TreeMove(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetInt64(4), row.GetInt64(5),
            Moment(row.GetString(6)), row.IsDBNull(7) ? null : row.GetInt64(7)),
        ("$ws", workspace), ("$repo", repository), ("$since", Moments.Write(since)));

    /// <summary>Each repository's days from <paramref name="from"/> on, oldest first: a day with a reading or a move.</summary>
    public IReadOnlyList<LedgerDay> Days(DateOnly from)
    {
        var first = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var readings = Read(
            "SELECT workspace, repository, day, trees, slot_bytes, linked_bytes, saves_pending, saves_bytes, volume, free_first, free_last FROM tree_days WHERE day >= $from",
            row => (Key: (row.GetString(0), row.GetString(1), row.GetString(2)), Trees: row.GetInt32(3), Slot: row.GetInt64(4), Linked: row.GetInt64(5),
                Pending: row.GetInt32(6), Saved: row.GetInt64(7), Volume: row.IsDBNull(8) ? null : row.GetString(8),
                FreeFirst: row.IsDBNull(9) ? (long?)null : row.GetInt64(9), FreeLast: row.IsDBNull(10) ? (long?)null : row.GetInt64(10)),
            ("$from", first)).ToDictionary(row => row.Key);
        var moves = Read(
            "SELECT workspace, repository, day, move, before, after, ready FROM tree_moves WHERE day >= $from",
            row => (Key: (row.GetString(0), row.GetString(1), row.GetString(2)), Move: row.GetString(3), Before: row.GetInt64(4), After: row.GetInt64(5),
                Ready: row.IsDBNull(6) ? (long?)null : row.GetInt64(6)),
            ("$from", first)).ToLookup(row => row.Key);
        return readings.Keys.Concat(moves.Select(group => group.Key)).Distinct()
            .OrderBy(key => key.Item3, StringComparer.Ordinal).ThenBy(key => key.Item1, StringComparer.Ordinal).ThenBy(key => key.Item2, StringComparer.Ordinal)
            .Select(key =>
            {
                var read = readings.GetValueOrDefault(key);
                var day = moves[key].ToList();
                int Count(string move) => day.Count(row => row.Move == move);
                var ready = day.Where(row => row.Ready is not null).Select(row => row.Ready!.Value).Order().ToList();
                return new LedgerDay(key.Item1, key.Item2, key.Item3, read.Trees, read.Slot, read.Linked, read.Pending, read.Saved, read.Volume,
                    read.FreeFirst, read.FreeLast, Count(TreeMoves.Made), Count(TreeMoves.Reused), Count(TreeMoves.Removed), Count(TreeMoves.Installed),
                    Count(TreeMoves.Restored), day.Sum(row => Math.Max(0, row.After - row.Before)), day.Sum(row => Math.Max(0, row.Before - row.After)),
                    Percentile(ready, 50), Percentile(ready, 95));
            })
            .ToList();
    }

    /// <summary>The nearest-rank percentile of sorted <paramref name="values"/>; null where there are none.</summary>
    private static long? Percentile(IReadOnlyList<long> values, int percent) =>
        values.Count == 0 ? null : values[Math.Clamp((int)Math.Ceiling(percent / 100.0 * values.Count) - 1, 0, values.Count - 1)];

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
