using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The salvage folder's rule, the Disk space card's <c>salvage</c> kind (<see cref="KeepKinds.Salvage"/>): each migration's folder
/// under <c>data/salvage</c> holds the copies that are the undo of one migration, and once a day in the tidy's Keeping pass the
/// folders whose newest file is older than the kind's days go. The person's press takes a younger one early (<c>space free</c>).
/// </summary>
internal sealed class SalvageKeeping(Func<DateTimeOffset> clock)
{
    private readonly object _gate = new();
    private DateTimeOffset? _daily;

    /// <summary>One migration's folder: its name, its place, the time of its newest file's making or writing, and its bytes.</summary>
    internal sealed record Folder(string Name, string Path, DateTimeOffset Newest, long Bytes);

    /// <summary>Whether the day's removal is due: a day since the last, and a folder past its days.</summary>
    internal bool Due(string home, KeepPolicy policy)
    {
        lock (_gate)
        {
            if (_daily is { } ran && clock() - ran < TimeSpan.FromDays(1)) return false;
        }

        if (Folders(home).Any(folder => clock() - folder.Newest >= policy.SalvageDays)) return true;
        lock (_gate) _daily = clock();
        return false;
    }

    /// <summary>The day's removal: how many folders went, and their bytes. A folder held open stays for the next day.</summary>
    internal (int Removed, long Bytes) Keep(string home, KeepPolicy policy)
    {
        var now = clock();
        var removed = 0;
        long bytes = 0;
        foreach (var folder in Folders(home).Where(folder => now - folder.Newest >= policy.SalvageDays))
        {
            try
            {
                TreeSetAside.DeleteNeverFollowing(folder.Path);
                removed++;
                bytes += folder.Bytes;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // In use now; the next day's pass takes it.
            }
        }

        lock (_gate) _daily = now;
        return (removed, bytes);
    }

    /// <summary>When the rule next takes a folder: the earliest day, how many go then, and their bytes; null where none is held.</summary>
    internal static JsonObject? Next(string home, KeepPolicy policy)
    {
        var folders = Folders(home);
        if (folders.Count == 0) return null;
        var day = folders.Min(each => each.Newest).UtcDateTime.Date;
        var that = folders.Where(each => each.Newest.UtcDateTime.Date == day).ToList();
        return new JsonObject { ["at"] = Moments.Write(that.Min(each => each.Newest) + policy.SalvageDays), ["count"] = that.Count, ["bytes"] = that.Sum(each => each.Bytes) };
    }

    /// <summary>Each migration's folder under the salvage entry, by name; none where the entry has none. A link is no folder of it.</summary>
    internal static IReadOnlyList<Folder> Folders(string home)
    {
        string root;
        try
        {
            root = DaorisHome.Entry(home, HomeEntry.Salvage);
        }
        catch (HomeEntryTwiceException)
        {
            return [];
        }

        if (!Directory.Exists(root)) return [];
        var folders = new List<Folder>();
        foreach (var path in Directory.EnumerateDirectories(root).OrderBy(each => each, StringComparer.Ordinal))
        {
            var info = new DirectoryInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            var newest = Latest(info);
            long bytes = 0;
            var walk = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
            foreach (var file in info.EnumerateFiles("*", walk))
            {
                bytes += file.Length;
                var made = Latest(file);
                if (made > newest) newest = made;
            }

            folders.Add(new Folder(info.Name, path, newest, bytes));
        }

        return folders;
    }

    // A copy keeps its source's write time on some systems, so the time of its making counts too.
    private static DateTimeOffset Latest(FileSystemInfo info) =>
        new DateTimeOffset(info.CreationTimeUtc > info.LastWriteTimeUtc ? info.CreationTimeUtc : info.LastWriteTimeUtc, TimeSpan.Zero);
}
