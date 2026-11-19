using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Ports.Store;

namespace Daoris.Driver;

/// <summary>A file or folder of a home's history, with where it sits under the Daoris folder and what it holds.</summary>
/// <param name="Relative">Its place under the Daoris folder, <c>/</c>-separated: the place it keeps in the backup.</param>
public sealed record PurgePath(string Relative, string Full, bool Folder, long Bytes, int Files);

/// <summary>One of the home's two stores: its entry name, where it is, and where it sits in the backup.</summary>
public sealed record PurgeStore(string Name, string Full, string Relative);

/// <summary>A history table of one of the home's stores, and the rows it holds that a purge takes.</summary>
/// <param name="Store">The store's entry name (<c>desktop-db</c>, <c>knowledge</c>).</param>
public sealed record PurgeTable(string Store, string Relative, string Table, long Rows);

/// <summary>What a purge of a home's history would move and empty: listed before anything is done.</summary>
/// <param name="Folder">The Daoris folder the home names, which every path is relative to.</param>
/// <param name="Backup">The folder of purges, <c>purged/</c> in the Daoris folder, where each purge's dated folder is made.</param>
public sealed record PurgePlan(
    string Folder, string Backup, IReadOnlyList<PurgePath> Paths, IReadOnlyList<PurgeTable> Tables, IReadOnlyList<PurgeStore> Stores)
{
    public long Bytes => Paths.Sum(path => path.Bytes);

    public long Rows => Tables.Sum(table => table.Rows);

    /// <summary>No history is in the home: no path, and no row in any table.</summary>
    public bool Nothing => Paths.Count == 0 && Rows == 0;
}

/// <summary>What a purge did: the dated folder it moved into, what it moved and emptied, and <see cref="Failed"/> where it stopped.</summary>
public sealed record PurgeDone(string Into, IReadOnlyList<PurgePath> Moved, IReadOnlyList<PurgeTable> Emptied, string? Failed)
{
    /// <summary>Something of the home changed, so a stop part-way leaves a backup holding it.</summary>
    public bool Changed => Moved.Count > 0 || Emptied.Count > 0;
}

/// <summary>
/// A home's history purged once: every file and folder <see cref="HomeHistory"/> names as history moved, at the same relative path, into
/// <c>purged/&lt;UTC stamp&gt;/</c> in the Daoris folder (an install's root, so beside its <c>data/</c>), and every history table of the two stores
/// emptied after the whole store is copied there. Configuration is never touched. The caller holds the home: no Daoris process may run on it.
/// </summary>
/// <remarks>
/// <para>A session's connector and a person's own agent write the knowledge store directly, so neither store is taken for granted:
/// both take the write lock first (<c>BEGIN IMMEDIATE</c>), a holder that will not let go stops the purge before anything changes, each
/// store is copied while it is held so no row is committed between the copy and the delete, and both commit together after both
/// are emptied. The files move after the stores, so a store that refuses leaves every file in place.</para>
///
/// <para>The quests that go are written into <c>quest_forgotten</c> first, so a remote's operations on them are passed over at the next fetch
/// and make no half quest. A file move that fails stops the purge there and says so: what moved is in the backup, and the next purge moves the
/// rest into a folder of its own. A <c>purge.json</c> in the dated folder lists what moved.</para>
/// </remarks>
public static class HomePurge
{
    /// <summary>The folder of purges, in the Daoris folder.</summary>
    public const string BackupFolder = "purged";

    /// <summary>The list a purge writes into its dated folder.</summary>
    public const string SummaryFile = "purge.json";

    private static readonly string[] QuestTables = ["quests", "quest_log"];

    /// <summary>
    /// What a purge of <paramref name="home"/> would move and empty; reads, writes nothing.
    /// </summary>
    /// <param name="knowledgeDb">Where the environment moved the knowledge store to, or null for the home's own.</param>
    public static PurgePlan Plan(string home, string? knowledgeDb = null)
    {
        var (folder, _) = DaorisHome.FolderOf(home);
        var backup = Path.GetFullPath(Path.Combine(folder, BackupFolder));
        var places = new List<string>();
        foreach (var entry in HomeHistory.Entries) places.AddRange(DaorisHome.Places(home, entry));
        foreach (var part in HomeHistory.WorkflowRecords) places.AddRange(DaorisHome.Places(home, HomeEntry.Workflows, part));

        var paths = new List<PurgePath>();
        // The shortest first, so a place inside one already taken (a session's files under the sessions' old folder) is taken with it.
        foreach (var place in places.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(place => place.Length))
        {
            if (!File.Exists(place) && !Directory.Exists(place)) continue;
            if (paths.Any(taken => Under(taken.Full, place)) || Under(backup, place) || Under(place, backup)) continue;
            foreach (var part in Parts(place))
            {
                if (Relative(folder, part) is { } relative) paths.Add(Measure(relative, part));
            }
        }

        var desktop = Stored(home, folder, HomeEntry.DesktopDb, null);
        var knowledge = Stored(home, folder, HomeEntry.Knowledge, knowledgeDb);
        var tables = new List<PurgeTable>();
        if (desktop is not null) tables.AddRange(Tables(desktop, HomeHistory.DesktopTables, null));
        if (knowledge is not null) tables.AddRange(Tables(knowledge, HomeHistory.ServiceTables, HomeHistory.ServiceWhere));
        PurgeStore?[] stores = [desktop, knowledge];
        return new(folder, backup, [.. paths.OrderBy(path => path.Relative, StringComparer.Ordinal)], tables, [.. stores.OfType<PurgeStore>()]);
    }

    /// <summary>
    /// The purge itself: <paramref name="plan"/>'s stores locked, copied and emptied together, then its paths moved, into a folder named for
    /// <paramref name="at"/>. The caller holds the home's lease and has found no session running; a store another process holds for writing stops
    /// it before anything is changed.
    /// </summary>
    public static PurgeDone Run(PurgePlan plan, DateTimeOffset at, MachineLog? log = null)
    {
        var emptied = new List<PurgeTable>();
        var moved = new List<PurgePath>();
        var into = "";
        string? failed = null;
        var stores = plan.Stores.Where(store => plan.Tables.Any(table => table.Store == store.Name && table.Rows > 0)).ToList();
        var held = new List<(PurgeStore Store, DbConnection Connection)>();
        var committed = false;
        try
        {
            foreach (var store in stores)
            {
                var connection = StoreEngineSource.Connect(store.Full, DesktopOpen.ReadWrite);
                held.Add((store, connection));
                Execute(connection, "PRAGMA busy_timeout = 1500");
                Execute(connection, "PRAGMA foreign_keys = OFF");
                try
                {
                    Execute(connection, "BEGIN IMMEDIATE");
                }
                catch (DbException)
                {
                    return new("", [], [], $"{store.Relative} is held by another process (a connector or an agent writing it): nothing was changed. "
                        + "Close what holds it and run this again.");
                }
            }

            into = Dated(plan.Backup, at);
            Directory.CreateDirectory(into);
            foreach (var (store, _) in held) Copy(store.Full, Path.Combine(into, store.Relative));
            foreach (var (store, connection) in held)
            {
                var mine = plan.Tables.Where(table => table.Store == store.Name).ToList();
                Forget(connection, mine, at);
                foreach (var table in mine) Execute(connection, DeleteFrom(table));
            }

            foreach (var (store, connection) in held)
            {
                Execute(connection, "COMMIT");
                committed = true;
                emptied.AddRange(plan.Tables.Where(table => table.Store == store.Name));
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
            failed = committed ? error.Message : $"{error.Message} Nothing was changed.";
            if (!committed) Undo(held, into);
        }
        finally
        {
            foreach (var (_, connection) in held) connection.Dispose();
            StoreEngineSource.Current.ReleaseConnections();
        }

        var done = new PurgeDone(into, moved, emptied, failed);
        if (into.Length > 0 && Directory.Exists(into))
        {
            try
            {
                File.WriteAllText(Path.Combine(into, SummaryFile), Summary(done, at));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                done = done with { Failed = failed ?? $"the list {SummaryFile} could not be written: {error.Message}" };
            }
        }

        if (done.Failed is null) log?.Info("home.purged", ("paths", moved.Count), ("tables", emptied.Count), ("rows", emptied.Sum(table => table.Rows)));
        else log?.Warn("home.purge-failed", ("paths", moved.Count), ("tables", emptied.Count));
        return done;
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

    /// <summary>
    /// A history folder as the pieces that move: itself, or where it holds a child that stays (<see cref="HomeHistory.Spared"/>), each other
    /// child on its own, so the older layout's requests and markers stay in a sessions folder that is otherwise moved.
    /// </summary>
    private static IEnumerable<string> Parts(string place)
    {
        if (!Directory.Exists(place)) return [place];
        var name = Path.GetFileName(place);
        var children = Directory.EnumerateFileSystemEntries(place).ToList();
        var spared = children.Any(child => HomeHistory.Spared(name, Path.GetFileName(child), Directory.Exists(child)));
        return spared ? children.Where(child => !HomeHistory.Spared(name, Path.GetFileName(child), Directory.Exists(child))) : [place];
    }

    /// <summary>The store the entry names, at the place the environment moved it to where that is given; null where there is no file.</summary>
    private static PurgeStore? Stored(string home, string folder, HomeEntry entry, string? wired)
    {
        var store = wired is { Length: > 0 } ? Path.GetFullPath(wired) : DaorisHome.Places(home, entry).FirstOrDefault(File.Exists);
        if (store is null || !File.Exists(store)) return null;
        return new(entry.Name, store, Relative(folder, store) ?? Path.GetFileName(store));
    }

    private static IEnumerable<PurgeTable> Tables(PurgeStore store, IReadOnlyList<string> history, IReadOnlyDictionary<string, string>? where)
    {
        using var connection = StoreEngineSource.Connect(store.Full, DesktopOpen.ReadOnly);
        var present = Names(connection);
        return [.. history.Where(present.Contains).Select(table => new PurgeTable(store.Name, store.Relative, table, Count(connection, table, where)))];

        static long Count(DbConnection connection, string table, IReadOnlyDictionary<string, string>? where)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"" + (where is not null && where.TryGetValue(table, out var condition) ? $" WHERE {condition}" : "");
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The store at <paramref name="store"/> copied whole to <paramref name="copy"/> through a reader, which a writer holding the lock does not stop.</summary>
    private static void Copy(string store, string copy)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        using var reader = StoreEngineSource.Connect(store, DesktopOpen.ReadOnly);
        Execute(reader, $"VACUUM INTO '{copy.Replace("'", "''", StringComparison.Ordinal)}'");
    }

    /// <summary>Every quest the purge takes, written into <c>quest_forgotten</c> before its rows go, so a remote's later operations on it are passed over.</summary>
    private static void Forget(DbConnection connection, IReadOnlyList<PurgeTable> tables, DateTimeOffset at)
    {
        var present = Names(connection);
        if (!tables.Any(table => QuestTables.Contains(table.Table)) || !present.Contains("quest_forgotten")) return;
        var ids = QuestTables.Where(present.Contains).Select(table => table == "quests"
            ? "SELECT id FROM quests"
            : "SELECT quest AS id FROM quest_log WHERE quest IS NOT NULL");
        var moment = at.ToString("O", CultureInfo.InvariantCulture);
        Execute(connection, $"INSERT OR IGNORE INTO quest_forgotten (id, at) SELECT id, '{moment}' FROM ({string.Join(" UNION ", ids)})");
    }

    private static string DeleteFrom(PurgeTable table) =>
        $"DELETE FROM \"{table.Table}\"" + (table.Store == HomeEntry.Knowledge.Name && HomeHistory.ServiceWhere.TryGetValue(table.Table, out var condition) ? $" WHERE {condition}" : "");

    /// <summary>A purge that stopped before it committed: every store rolled back, and the folder it made taken away.</summary>
    private static void Undo(List<(PurgeStore Store, DbConnection Connection)> held, string into)
    {
        foreach (var (_, connection) in held)
        {
            try
            {
                Execute(connection, "ROLLBACK");
            }
            catch (DbException)
            {
                // It holds no transaction: the lock never came, or the commit had already happened.
            }
        }

        try
        {
            if (into.Length > 0 && Directory.Exists(into)) Directory.Delete(into, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The partial copy stays where it is; the next purge makes a folder of its own.
        }
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static PurgePath Measure(string relative, string place)
    {
        if (File.Exists(place)) return new(relative, place, false, new FileInfo(place).Length, 1);
        var files = new DirectoryInfo(place).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }).ToList();
        return new(relative, place, true, files.Sum(file => file.Length), files.Count);
    }

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
