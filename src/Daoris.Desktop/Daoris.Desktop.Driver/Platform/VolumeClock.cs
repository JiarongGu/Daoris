namespace Daoris.Driver;

/// <summary>
/// The clock a file's write time is compared with: the volume's, since a share stamps a write with its own clock, which may lag this
/// machine's and count in coarser steps. A local fixed volume's clock is this machine's; the home's volume, where it is not one, is read
/// from the write time of a mark touched in the home's <c>run/</c> folder (<see cref="HomeEntry.ClockMark"/>); any other volume has no
/// clock, and no stat on it is trusted.
/// </summary>
internal static class VolumeClock
{
    /// <summary>
    /// The test seam: where set, the clock a path's volume answers, so a share's lag or a fixture's age is one case. Null in production,
    /// and an async local, so classes running in parallel never see each other's.
    /// </summary>
    internal static readonly AsyncLocal<Func<string, DateTime?>?> Seam = new();

    /// <summary>The time now on <paramref name="path"/>'s volume, in UTC; null where that volume's clock cannot be read.</summary>
    internal static DateTime? Now(string path)
    {
        if (Seam.Value is { } seam) return seam(path);
        var root = RootOf(path);
        if (root is null) return null;
        if (LocalFixed(root)) return DateTime.UtcNow;
        return ProgramHome.Named is { } home && string.Equals(RootOf(home), root, StringComparison.OrdinalIgnoreCase) ? Marked(home) : null;
    }

    private static string? RootOf(string path)
    {
        try
        {
            return Path.GetPathRoot(Path.GetFullPath(path)) is { Length: > 0 } root ? root : null;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool LocalFixed(string root)
    {
        if (root.StartsWith(@"\\", StringComparison.Ordinal) || root.StartsWith("//", StringComparison.Ordinal)) return false;
        try
        {
            return new DriveInfo(root).DriveType == DriveType.Fixed;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // A byte written, so the volume's own clock stamps the write; an empty rewrite of an empty file may leave its time unmoved.
    private static DateTime? Marked(string home)
    {
        try
        {
            var mark = DaorisHome.Entry(home, HomeEntry.ClockMark);
            Directory.CreateDirectory(Path.GetDirectoryName(mark)!);
            File.WriteAllBytes(mark, [1]);
            return File.GetLastWriteTimeUtc(mark);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
