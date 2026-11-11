namespace Daoris.Driver;

/// <summary>
/// The rule for what a plugin writes to its <c>output</c> folder under <c>.data/&lt;plugin&gt;</c> (the in-app browser's pages and
/// screenshots, say): once a day in the tidy's Keeping pass, each file not written for <see cref="Days"/> days goes, then a folder
/// it leaves empty. The plugin's other data stays: only <c>output</c> is read. It is no kind of the Disk space card.
/// </summary>
internal sealed class PluginOutputKeeping(Func<DateTimeOffset> clock)
{
    /// <summary>How long a file is kept after its last write.</summary>
    internal const int Days = 14;

    /// <summary>The folder under a plugin's data folder that holds what it writes for the person to see.</summary>
    internal const string Folder = "output";

    private readonly object _gate = new();
    private DateTimeOffset? _daily;

    /// <summary>Whether the day's prune is due: a day since the last, and a file past its days.</summary>
    internal bool Due(string home)
    {
        lock (_gate)
        {
            if (_daily is { } ran && clock() - ran < TimeSpan.FromDays(1)) return false;
        }

        if (Old(home, clock()).Count > 0) return true;
        lock (_gate) _daily = clock();
        return false;
    }

    /// <summary>The day's prune: how many files went, and their bytes. A file held open stays for the next day.</summary>
    internal (int Removed, long Bytes) Keep(string home)
    {
        var now = clock();
        var removed = 0;
        long bytes = 0;
        foreach (var (file, size) in Old(home, now))
        {
            try
            {
                File.Delete(file);
                removed++;
                bytes += size;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // In use now; the next day's prune takes it.
            }
        }

        foreach (var output in Outputs(home)) RemoveEmpty(output, root: true);
        lock (_gate) _daily = now;
        return (removed, bytes);
    }

    private static IEnumerable<string> Outputs(string home)
    {
        string data;
        try
        {
            data = Path.Combine(DaorisHome.Entry(home, HomeEntry.Plugins), PluginCatalog.DataFolder);
        }
        catch (HomeEntryTwiceException)
        {
            return [];
        }

        return Directory.Exists(data)
            ? Directory.EnumerateDirectories(data).Select(plugin => Path.Combine(plugin, Folder)).Where(Directory.Exists)
            : [];
    }

    private static List<(string File, long Bytes)> Old(string home, DateTimeOffset now)
    {
        var old = new List<(string, long)>();
        var edge = now - TimeSpan.FromDays(Days);
        var walk = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        foreach (var output in Outputs(home))
        {
            foreach (var file in Directory.EnumerateFiles(output, "*", walk))
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < edge.UtcDateTime) old.Add((file, info.Length));
            }
        }

        return old;
    }

    private static void RemoveEmpty(string folder, bool root)
    {
        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            if ((new DirectoryInfo(child).Attributes & FileAttributes.ReparsePoint) != 0) continue;
            RemoveEmpty(child, root: false);
        }

        if (root) return;
        try
        {
            if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder in use stays.
        }
    }
}
