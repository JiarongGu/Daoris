namespace Daoris.Driver;

/// <summary>
/// What *Open* in Settings → This Daoris opens: the folder a folder row or a file row's id names, as
/// this Daoris runs. The page sends the id the read gave it and never a path, so a page, or anything speaking as one, cannot have the
/// file manager opened on a place Daoris does not keep.
/// </summary>
/// <remarks>
/// An id is <c>home</c> (the Daoris folder), one of the top folders (<see cref="WiringSizes.TopFolders"/>), or a file's (an entry of
/// the settings files or the records): a file's is opened by its folder, never by running it.
/// </remarks>
public static class WiringFolderPaths
{
    /// <summary>The id of the Daoris folder itself.</summary>
    public const string HomeId = "home";

    /// <summary>The folder an id names, or <c>FOLDER_UNKNOWN</c> for an id the read never gave or one this Daoris keeps no place for.</summary>
    public static (string? Folder, string? Why) Resolve(WiringWorld world, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return (null, "FOLDER_UNKNOWN");
        if (world.Home is not { } home) return (null, "FOLDER_UNKNOWN");
        if (id == HomeId) return (home, null);

        if (WiringSizes.TopFolders.Contains(id, StringComparer.Ordinal))
        {
            return (Path.Combine(SystemWiring.RootOf(home) ?? home, id), null);
        }

        var file = WiringFiles.SettingsFiles.Select(each => (each.Entry, each.Override))
            .Concat(WiringFiles.Records)
            .Where(each => each.Entry.Name == id)
            .Select(each => ((HomeEntry Entry, string? Override)?)each)
            .FirstOrDefault();
        if (file is not { } found) return (null, "FOLDER_UNKNOWN");
        var (path, _) = WiringFiles.PathOf(world, found.Entry, found.Override);
        if (path is null) return (null, "FOLDER_UNKNOWN");
        return (Directory.Exists(path) ? path : Path.GetDirectoryName(Path.GetFullPath(path)), null);
    }
}
