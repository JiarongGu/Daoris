using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Ports.Store;

namespace Daoris.Driver;

/// <summary>A file or folder of a home's history, with where it sits under the Daoris folder and what it holds.</summary>
/// <param name="Relative">Its place under the Daoris folder, <c>/</c>-separated: the place it keeps in the backup.</param>
public sealed record PurgePath(string Relative, string Full, bool Folder, long Bytes, int Files);

/// <summary>A history table of one of the home's stores, and the rows it holds.</summary>
/// <param name="Store">The store's entry name (<c>desktop-db</c>, <c>knowledge</c>).</param>
public sealed record PurgeTable(string Store, string Relative, string Table, long Rows);

/// <summary>What a purge of a home's history would move and empty: listed before anything is done.</summary>
/// <param name="Folder">The Daoris folder the home names, which every path is relative to.</param>
/// <param name="Backup">The folder of purges beside it, where each purge's dated folder is made.</param>
public sealed record PurgePlan(string Folder, string Backup, IReadOnlyList<PurgePath> Paths, IReadOnlyList<PurgeTable> Tables)
{
    public long Bytes => Paths.Sum(path => path.Bytes);

    public long Rows => Tables.Sum(table => table.Rows);
}

/// <summary>What a purge did: the dated folder it moved into, and what it moved and emptied; <see cref="Failed"/> where it stopped.</summary>
public sealed record PurgeDone(string Into, IReadOnlyList<PurgePath> Moved, IReadOnlyList<PurgeTable> Emptied, string? Failed);

/// <summary>
/// A home's history purged once: every file and folder <see cref="HomeHistory"/> names as history moved, at the same relative path, into
/// <c>purged/&lt;UTC stamp&gt;/</c> beside the home (the install's folder, or the home's parent), and every history table of the two stores
/// emptied after the whole store is copied there. Configuration is never touched. The caller holds the home: nothing else may run on it.
/// </summary>
/// <remarks>
/// The stores go first, each emptied in one transaction after its copy, so a store that refuses leaves every file in place; then the
/// files, each one rename on the folder's own volume. A move that fails stops the purge there and says so: what moved is in the backup,
/// and the next purge moves the rest into a folder of its own. A <c>purge.json</c> in the dated folder lists what moved.
/// </remarks>
public static class HomePurge
{
    /// <summary>The folder of purges, beside the home.</summary>
    public const string BackupFolder = "purged";

    /// <summary>The list a purge writes into its dated folder.</summary>
    public const string SummaryFile = "purge.json";

    /// <summary>What a purge of <paramref name="home"/> would move and empty; reads, writes nothing.</summary>
    public static PurgePlan Plan(string home)
    {
        var (folder, _) = DaorisHome.FolderOf(home);
        var places = new List<string>();
        foreach (var entry in HomeHistory.Entries) places.AddRange(DaorisHome.Places(home, entry));
        foreach (var part in HomeHistory.WorkflowRecords) places.AddRange(DaorisHome.Places(home, HomeEntry.Workflows, part));

        var paths = new List<PurgePath>();
        // The shortest first, so a place inside one already taken (a session's files under the sessions' old folder) is taken with it.
        foreach (var place in places.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(place => place.Length))
        {
            if (!File.Exists(place) && !Directory.Exists(place)) continue;
            if (Relative(folder, place) is not { } relative) continue;
            if (paths.Any(taken => Under(taken.Full, place))) continue;
            paths.Add(Measure(relative, place));
        }

        var tables = new List<PurgeTable>();
        tables.AddRange(Tables(home, folder, HomeEntry.DesktopDb, HomeHistory.DesktopTables));
        tables.AddRange(Tables(home, folder, HomeEntry.Knowledge, HomeHistory.ServiceTables));
        return new(folder, Path.Combine(BesideOf(folder), BackupFolder), [.. paths.OrderBy(path => path.Relative, StringComparer.Ordinal)], tables);
    }

    /// <summary>
    /// The purge itself: <paramref name="plan"/>'s stores copied and emptied, its paths moved, into a folder named for
    /// <paramref name="at"/>. The caller holds the home's lease; nothing of Daoris runs on it.
    /// </summary>
    public static PurgeDone Run(PurgePlan plan, DateTimeOffset at, MachineLog? log = null)
    {
        var into = Dated(plan.Backup, at);
        Directory.CreateDirectory(into);
        var emptied = new List<PurgeTable>();
        var moved = new List<PurgePath>();
        string? failed = null;
        try
        {
            foreach (var store in plan.Tables.GroupBy(table => table.Relative))
            {
                Empty(Path.Combine(plan.Folder, store.Key), Path.Combine(into, store.Key), [.. store.Select(table => table.Table)]);
                emptied.AddRange(store);
            }

            foreach (var path in plan.Paths)
            {
                var target = Path.Combine(into, path.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (path.Folder) Directory.Move(path.Full, target);
                else File.Move(path.Full, target);
                moved.Add(path);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DbException)
        {
            failed = error.Message;
        }
        finally
        {
            StoreEngineSource.Current.ReleaseConnections();
        }

        var done = new PurgeDone(into, moved, emptied, failed);
        File.WriteAllText(Path.Combine(into, SummaryFile), Summary(done, at));
        if (failed is null) log?.Info("home.purged", ("paths", moved.Count), ("tables", emptied.Count), ("rows", emptied.Sum(table => table.Rows)));
        else log?.Warn("home.purge-failed", ("paths", moved.Count), ("tables", emptied.Count));
        return done;
    }

    /// <summary>The store at <paramref name="store"/> copied whole to <paramref name="copy"/>, then each of <paramref name="tables"/> emptied in one transaction.</summary>
    private static void Empty(string store, string copy, IReadOnlyList<string> tables)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        StoreEngineSource.Current.Backup(store, copy);
        using var connection = StoreEngineSource.Connect(store, DesktopOpen.ReadWrite);
        Execute(connection, null, "PRAGMA foreign_keys = OFF");
        using var transaction = connection.BeginTransaction();
        foreach (var table in tables) Execute(connection, transaction, $"DELETE FROM \"{table}\"");
        transaction.Commit();
    }

    private static IEnumerable<PurgeTable> Tables(string home, string folder, HomeEntry entry, IReadOnlyList<string> history)
    {
        if (DaorisHome.Places(home, entry).FirstOrDefault(File.Exists) is not { } store || Relative(folder, store) is not { } relative) return [];
        using var connection = StoreEngineSource.Connect(store, DesktopOpen.ReadOnly);
        var present = Names(connection);
        return [.. history.Where(present.Contains).Select(table => new PurgeTable(entry.Name, relative, table, Count(connection, table)))];
    }

    /// <summary>The tables a store holds, by name.</summary>
    public static IReadOnlySet<string> Names(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read()) names.Add(reader.GetString(0));
        return names;
    }

    private static long Count(DbConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(DbConnection connection, DbTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static PurgePath Measure(string relative, string place)
    {
        if (File.Exists(place)) return new(relative, place, false, new FileInfo(place).Length, 1);
        var files = new DirectoryInfo(place).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }).ToList();
        return new(relative, place, true, files.Sum(file => file.Length), files.Count);
    }

    /// <summary>Where the folder of purges sits: in an install's own folder, else beside the home.</summary>
    private static string BesideOf(string folder) =>
        File.Exists(Path.Combine(folder, DaorisHome.InstallMarker)) ? folder : Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder)) ?? folder;

    /// <summary>The purge's own folder, named for its moment in UTC; a second purge in the same second takes the next number.</summary>
    private static string Dated(string backup, DateTimeOffset at)
    {
        var stamp = at.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var into = Path.Combine(backup, stamp);
        for (var next = 2; Directory.Exists(into); next++) into = Path.Combine(backup, $"{stamp}-{next}");
        return into;
    }

    private static string? Relative(string folder, string place)
    {
        var relative = Path.GetRelativePath(folder, place);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? null : relative.Replace('\\', '/');
    }

    private static bool Under(string folder, string place) =>
        place.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Summary(PurgeDone done, DateTimeOffset at) => new JsonObject
    {
        ["at"] = at.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
        ["failed"] = done.Failed,
        ["moved"] = new JsonArray([.. done.Moved.Select(path => (JsonNode)new JsonObject { ["path"] = path.Relative, ["bytes"] = path.Bytes, ["files"] = path.Files })]),
        ["emptied"] = new JsonArray([.. done.Emptied.Select(table => (JsonNode)new JsonObject { ["store"] = table.Relative, ["table"] = table.Table, ["rows"] = table.Rows })]),
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
}
