namespace Daoris.Driver;

/// <summary>The limits the pool's acts read.</summary>
internal static class PoolLimits
{
    /// <summary>A slot whose ignored bytes outside <c>node_modules</c> pass this has its builds moved aside whole at its next take.</summary>
    internal const long RenewBytes = 4L << 30;
}

/// <summary>
/// The set-aside folders a take makes of a line that may come back: each is marked with its line's name, and held as that line's own
/// (<see cref="SetAsideClass.Line"/>) while the line's record can go on and the line has not finished. The line's finish gathers them into
/// its own folder, so its record names them and they go as the finished line's folder does.
/// </summary>
internal static class PoolAsides
{
    private const string Suffix = ".line";

    /// <summary>Whether the record of a line can still go on: the caller that knows the sessions answers; the default holds.</summary>
    internal static Func<string, bool> RecordGoes { get; set; } = _ => true;

    internal static string MarkerOf(string folder) => Path.TrimEndingDirectorySeparator(folder) + Suffix;

    internal static void Mark(string folder, string line) => AtomicFile.WriteText(MarkerOf(folder), line);

    /// <summary>The line a set-aside folder is held for; null where it is held for none.</summary>
    internal static string? LineOf(string folder)
    {
        try
        {
            return File.Exists(MarkerOf(folder)) ? File.ReadAllText(MarkerOf(folder)).Trim() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether the folder is held as its line's own: marked, and the line's record can go on.</summary>
    internal static bool Holds(string folder) => LineOf(folder) is { Length: > 0 } line && RecordGoes(line);

    /// <summary>
    /// The folders held for <paramref name="line"/> moved into <paramref name="target"/>, each top at its own relative place, and their
    /// markers taken away. A top that cannot move stays, with its marker, for the next look.
    /// </summary>
    internal static void GatherInto(PoolSite site, string line, string target)
    {
        var root = Path.Combine(DaorisHome.Entry(site.Home, HomeEntry.Trees), TreeSetAside.SetAsideFolder, site.Workspace, site.Repository);
        if (!Directory.Exists(root)) return;
        foreach (var marker in Directory.EnumerateFiles(root, "*" + Suffix).ToList())
        {
            var folder = marker[..^Suffix.Length];
            if (LineOf(folder) != line || !Directory.Exists(folder) || string.Equals(Path.GetFullPath(folder), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                foreach (var top in Directory.EnumerateFileSystemEntries(folder).ToList())
                {
                    var to = Path.Combine(target, Path.GetFileName(top));
                    for (var n = 2; Path.Exists(to); n++) to = Path.Combine(target, $"{Path.GetFileName(top)}-{n}");
                    Directory.CreateDirectory(target);
                    if (Directory.Exists(top)) Directory.Move(top, to);
                    else File.Move(top, to);
                }

                Directory.Delete(folder);
                File.Delete(marker);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Left where it is, marked, for the next look.
            }
        }
    }
}
