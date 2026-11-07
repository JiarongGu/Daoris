using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.SystemWiring;

namespace Daoris.Driver;

/// <summary>The settings files and records of the home as the wiring answers them: whether each is there, how big, when written and whether it reads.</summary>
internal static class WiringFiles
{
    /// <summary>The settings files a person's choice lives in, each with the variable that overrides it and the door to where it is edited.</summary>
    internal static readonly (HomeEntry Entry, string? Override, string Door)[] SettingsFiles =
    [
        (HomeEntry.DriverConfig, null, "driver"),
        (HomeEntry.Harnesses, WiringVariables.HarnessFile, "agents"),
        (HomeEntry.Accounts, null, "agents"),
        (HomeEntry.Permissions, null, "agents"),
        (HomeEntry.Tools, null, "tools"),
        (HomeEntry.PluginState, null, "plugins"),
        (HomeEntry.BrowserSettings, null, "browser"),
        (HomeEntry.Update, null, "driver"),
        (HomeEntry.Remotes, WiringVariables.RemotesFile, "workspace"),
        (HomeEntry.Keys, null, "agents"),
    ];

    /// <summary>The records, the same way, with no door.</summary>
    internal static readonly (HomeEntry Entry, string? Override)[] Records =
    [
        (HomeEntry.Usage, null), (HomeEntry.Landings, null), (HomeEntry.SessionBranches, null), (HomeEntry.Cooling, null),
        (HomeEntry.Windows, null), (HomeEntry.Reads, null), (HomeEntry.Sessions, null), (HomeEntry.Knowledge, WiringVariables.KnowledgeDb),
    ];


    internal static JsonArray Files(WiringWorld world, SizeReading? reading, string? remotesWhy)
    {
        var rows = new JsonArray();
        foreach (var (entry, overrides, door) in SettingsFiles) rows.Add(FileRow(world, reading, entry, overrides, door, record: false, remotesWhy));
        foreach (var (entry, overrides) in Records) rows.Add(FileRow(world, reading, entry, overrides, door: null, record: true, remotesWhy));
        return rows;
    }

    /// <summary>Where an entry's file is as it runs: its override's value, the driver's config where it is found, else the home's; or why it has none.</summary>
    internal static (string? Path, string? Why) PathOf(WiringWorld world, HomeEntry entry, string? overrides)
    {
        var named = overrides is null ? null : world.Process(overrides);
        string? why = world.Home is null ? "no-home" : null;
        if (entry == HomeEntry.DriverConfig) return (ConfigPath(world), why);
        if (!string.IsNullOrWhiteSpace(named)) return (named.Trim(), why);
        if (world.Home is not { } home) return (null, why);
        try
        {
            return (DaorisHome.Entry(home, entry), why);
        }
        catch (HomeEntryTwiceException)
        {
            return (null, "twice");
        }
    }

    internal static JsonObject FileRow(
        WiringWorld world, SizeReading? reading, HomeEntry entry, string? overrides, string? door, bool record, string? remotesWhy)
    {
        var named = overrides is null ? null : world.Process(overrides);
        var (path, pathWhy) = PathOf(world, entry, overrides);
        var isFile = path is not null && File.Exists(path);
        var present = isFile || (path is not null && Directory.Exists(path));
        var row = new JsonObject
        {
            ["id"] = entry.Name,
            ["folder"] = entry.Category.ToString().ToLowerInvariant(),
        };
        Fact(row, "path", path, pathWhy);
        row["present"] = present;

        long? bytes = null;
        if (isFile)
        {
            bytes = Length(path!);
            // A database's write-ahead log is the database's.
            if (path!.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) bytes += Length(path + "-wal") ?? 0;
        }
        else if (present && reading is not null)
        {
            bytes = reading.Entries.GetValueOrDefault(entry.Name);
        }

        Fact(row, "bytes", bytes, !present ? "absent" : "not-measured");
        row["written"] = present ? Written(path!) : null;

        bool? reads = null;
        string? readsWhy = null;
        if (record) readsWhy = "record";
        else if (!present) readsWhy = "absent";
        else if (entry == HomeEntry.Remotes) (reads, readsWhy) = (remotesWhy is null, remotesWhy);
        // 🔴 A sign-in's file is sized, never read: its folder holds keys.
        else if (entry.Category == HomeCategory.Profiles) readsWhy = "private";
        else (reads, readsWhy) = Parses(path!);
        row["reads"] = reads;
        row["readsWhy"] = readsWhy;

        row["overriddenBy"] = string.IsNullOrWhiteSpace(named) ? null : overrides;
        row["door"] = door;
        row["record"] = record;
        return row;
    }

    internal static long? Length(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string? Written(string path)
    {
        try
        {
            return Moments.Write(new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether a settings file parses, said as a code: what the parser said is never passed on, since it may quote the file.</summary>
    internal static (bool? Reads, string? Why) Parses(string path)
    {
        if (!File.Exists(path)) return (null, "folder");
        try
        {
            using var stream = AtomicFile.OpenRead(path);
            using var _ = JsonDocument.Parse(stream);
            return (true, null);
        }
        catch (JsonException)
        {
            return (false, "unparsed");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (false, "unreadable");
        }
    }
}
