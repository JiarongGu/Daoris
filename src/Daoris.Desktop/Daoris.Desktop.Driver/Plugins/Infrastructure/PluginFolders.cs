namespace Daoris.Driver;

/// <summary>
/// The home's plugin folders on this machine (STRUCT3a): what the catalogue reads, and how the placeholders a manifest
/// writes become this machine's paths. The module's Application reads and writes the home only through here.
/// </summary>
internal static class PluginFolders
{
    /// <summary>
    /// Each folder under <paramref name="root"/> holding a manifest, by name in ordinal order: its name, its path and its
    /// manifest's path. A dot-folder (`.data/`, a stage, a trial's scratch) is the catalogue's own, never a plugin. Null where
    /// <paramref name="root"/> is not there.
    /// </summary>
    public static IReadOnlyList<(string Name, string Path, string Manifest)>? Installed(string root)
    {
        if (!Directory.Exists(root)) return null;

        var found = new List<(string Name, string Path, string Manifest)>();
        foreach (var folder in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(folder)!;
            if (name.StartsWith('.')) continue;
            var manifest = Path.Combine(folder, PluginCatalog.ManifestName);
            if (!File.Exists(manifest)) continue;
            found.Add((name, folder, manifest));
        }

        return found;
    }

    /// <summary>A file's text, or why it could not be read, in the system's words.</summary>
    public static (string? Text, string? Error) ReadText(string path)
    {
        try
        {
            return (File.ReadAllText(path), null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, error.Message);
        }
    }

    /// <summary>A file's text, or null where there is no such file; a file there that cannot be read throws.</summary>
    public static string? TextIfThere(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// <summary>A file written beside and renamed over, its folder made first.</summary>
    public static void WriteText(string folder, string path, string text)
    {
        Directory.CreateDirectory(folder);
        AtomicFile.WriteText(path, text);
    }

    /// <summary>
    /// An icon's file: whether it is there as a file, its length, and its bytes where it is at most <paramref name="most"/>
    /// and could be read; null bytes for one larger or unreadable.
    /// </summary>
    public static (bool Exists, long Length, byte[]? Bytes) ReadIcon(string path, int most)
    {
        var file = new FileInfo(path);
        if (!file.Exists) return (false, 0, null);
        if (file.Length > most) return (true, file.Length, null);
        try
        {
            return (true, file.Length, File.ReadAllBytes(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (true, file.Length, null);
        }
    }

    /// <summary>A folder's own name, from its whole path on this machine.</summary>
    public static string NameOf(string folder) => Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)));

    /// <summary>
    /// <see cref="PluginCatalog.Placeholder"/> and <see cref="PluginCatalog.DataPlaceholder"/> in a manifest's word, made the
    /// install folder and its data folder on this machine.
    /// </summary>
    /// <remarks>
    /// The data folder is the install folder's sibling under <see cref="PluginCatalog.DataFolder"/>, by the same name:
    /// a plugin's folder IS its id, so the two are one derivation and never disagree — unless a reader of
    /// a folder outside the home names where it keeps things (<c>PluginCatalog.ReadFolder</c>).
    /// </remarks>
    public static string Expand(string text, string folder, string? data)
    {
        var plugin = text.Contains(PluginCatalog.Placeholder, StringComparison.Ordinal);
        var keeps = text.Contains(PluginCatalog.DataPlaceholder, StringComparison.Ordinal);
        if (!plugin && !keeps) return text;

        var install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var kept = data ?? Path.Combine(Path.GetDirectoryName(install)!, PluginCatalog.DataFolder, Path.GetFileName(install));
        return Path.GetFullPath(text
            .Replace(PluginCatalog.Placeholder, install, StringComparison.Ordinal)
            .Replace(PluginCatalog.DataPlaceholder, kept, StringComparison.Ordinal));
    }
}
