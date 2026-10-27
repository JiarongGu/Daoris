namespace Daoris.Driver;

/// <summary>The marked install containing a home, or the original home where it belongs to none.</summary>
internal static class InstallRoot
{
    internal static string? InstallOf(string? home)
    {
        if (home is null) return null;
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        if (File.Exists(Path.Combine(full, DaorisHome.InstallMarker))) return full;
        return string.Equals(Path.GetFileName(full), DaorisHome.InstallData, StringComparison.OrdinalIgnoreCase)
            && Path.GetDirectoryName(full) is { } parent && File.Exists(Path.Combine(parent, DaorisHome.InstallMarker))
                ? parent
                : null;
    }

    internal static string? RootOf(string? home) => InstallOf(home) ?? home;
}
