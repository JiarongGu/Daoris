namespace Daoris.Driver;

/// <summary>
/// The home's plugin folders on this machine (STRUCT3a): what the catalogue reads, and how the placeholders a manifest
/// writes become this machine's paths. The module reads and writes the home only through here.
/// </summary>
internal static class PluginFolders
{
    /// <summary>
    /// Each folder under <paramref name="root"/> holding a manifest, by name in ordinal order: its name, its path and its
    /// manifest's path. A dot-folder (`.data/`, a stage, a trial's scratch) is the catalogue's own, never a plugin. Null where
    /// <paramref name="root"/> is not there. The home's `plugins/` and the install's offers are read alike.
    /// </summary>
    public static IReadOnlyList<(string Name, string Path, string Manifest)>? WithManifests(string root)
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

    /// <summary>Whether a folder is there.</summary>
    public static bool There(string folder) => Directory.Exists(folder);

    /// <summary>Whether a file is there.</summary>
    public static bool HasFile(string path) => File.Exists(path);

    /// <summary>A path made whole on this machine: against the folder this process stands in, where it is not already.</summary>
    public static string WholePath(string path) => Path.GetFullPath(path);

    /// <summary>A file's text, or why it could not be read, in the system's words.</summary>
    public static (string? Text, string? Error) ReadText(string path)
    {
        try
        {
            return (AtomicFile.ReadText(path), null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, error.Message);
        }
    }

    /// <summary>A file's text, or null where there is no such file; a file there that cannot be read throws.</summary>
    public static string? TextIfThere(string path) => File.Exists(path) ? AtomicFile.ReadText(path) : null;

    /// <summary>A file written beside and renamed over, its folder made first.</summary>
    public static void WriteText(string folder, string path, string text)
    {
        Directory.CreateDirectory(folder);
        AtomicFile.WriteText(path, text);
    }

    /// <summary>A file inside a staged copy, which moves whole; written beside and renamed all the same, as UTF-8 with no byte-order mark (DEDUP3a).</summary>
    public static void WriteFile(string path, string text) => AtomicFile.WriteText(path, text);

    /// <summary>
    /// Whether two folders hold the same files, byte for byte, by relative path, leaving out the install's own record of where it came
    /// from (<see cref="PluginSource.FileName"/>), which an install writes and an offer never has (LANDPIN3).
    /// </summary>
    public static bool SameFiles(string left, string right)
    {
        static List<string> Files(string root) => [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(relative => relative != PluginSource.FileName)
            .Order(StringComparer.Ordinal)];

        try
        {
            var (a, b) = (Files(left), Files(right));
            return a.SequenceEqual(b, StringComparer.Ordinal)
                && a.All(relative => File.ReadAllBytes(Path.Combine(left, relative)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(right, relative)))); // read-shares: two plugin folders, laid whole
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // What cannot be read cannot be said the same: the copy is said as changed, and never replaced unread.
            return false;
        }
    }

    /// <summary>A folder made, with every folder above it.</summary>
    public static void Make(string folder) => Directory.CreateDirectory(folder);

    /// <summary>File by file; a link is neither followed nor copied, as the CLI's copy does (D3).</summary>
    public static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var entry in new DirectoryInfo(from).EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null) continue;
            var target = Path.Combine(to, entry.Name);
            if (entry is DirectoryInfo directory) CopyTree(directory.FullName, target);
            else if (entry is FileInfo file) file.CopyTo(target);
        }
    }

    /// <summary>A staged folder renamed into its place, as every folder Daoris owns is (<see cref="AtomicFile.MoveFolder"/>).</summary>
    public static void MoveInto(string staging, string target) => AtomicFile.MoveFolder(staging, target);

    /// <summary>A folder moved whole; it throws where something holds it open.</summary>
    public static void Move(string from, string to) => Directory.Move(from, to);

    /// <summary>A folder deleted where it can be: a dot-folder left behind is never read as a plugin, so one that cannot be is nobody's.</summary>
    public static void Forget(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
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
            return (true, file.Length, File.ReadAllBytes(path)); // read-shares: a plugin's icon, laid with its folder
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (true, file.Length, null);
        }
    }

    /// <summary>When a folder was made, or null where it is not there or cannot be asked: an install and an update each make one whole (D103).</summary>
    public static DateTimeOffset? MadeAt(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? new DateTimeOffset(Directory.GetCreationTimeUtc(folder), TimeSpan.Zero) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// A folder's files and bytes, counted up to <paramref name="bound"/> entries or for <paramref name="time"/>,
    /// whichever comes first, with <see cref="PageData.More"/> set past either. A link is counted and never followed.
    /// </summary>
    public static PageData Count(string folder, int bound, TimeSpan time)
    {
        if (!Directory.Exists(folder)) return new(folder, false, 0, 0, false, null);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var files = 0;
        var bytes = 0L;
        var entries = 0;
        DateTimeOffset? changed = null;
        var waiting = new Stack<DirectoryInfo>([new DirectoryInfo(folder)]);
        while (waiting.Count > 0)
        {
            IEnumerable<FileSystemInfo> inside;
            try
            {
                inside = waiting.Pop().EnumerateFileSystemInfos();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var item in inside)
            {
                if (entries >= bound || clock.Elapsed > time) return new(folder, true, files, bytes, true, changed);
                entries++;
                var written = new DateTimeOffset(item.LastWriteTimeUtc, TimeSpan.Zero);
                if (changed is null || written > changed) changed = written;
                if (item is FileInfo file)
                {
                    files++;
                    bytes += file.Length;
                }
                else if (item is DirectoryInfo directory && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    waiting.Push(directory);
                }
            }
        }

        return new(folder, true, files, bytes, false, changed);
    }

    /// <summary>
    /// The test files a folder carries, relative, as <c>node --test</c> finds them by default (<see cref="PageData.IsTest"/>),
    /// never under <c>node_modules</c> or a dot-folder, nor through a link; at most <paramref name="bound"/> entries are looked at.
    /// </summary>
    public static IReadOnlyList<string> Tests(string folder, int bound)
    {
        if (!Directory.Exists(folder)) return [];
        var found = new List<string>();
        var entries = 0;
        var waiting = new Stack<(DirectoryInfo Directory, string Relative, bool UnderTest)>([(new DirectoryInfo(folder), "", false)]);
        while (waiting.Count > 0 && entries < bound)
        {
            var (directory, relative, underTest) = waiting.Pop();
            IEnumerable<FileSystemInfo> inside;
            try
            {
                inside = directory.EnumerateFileSystemInfos();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var item in inside)
            {
                if (++entries > bound) break;
                var path = relative.Length == 0 ? item.Name : $"{relative}/{item.Name}";
                if (item is DirectoryInfo inner)
                {
                    if (inner.Name is "node_modules" || inner.Name.StartsWith('.') || inner.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    waiting.Push((inner, path, underTest || inner.Name == "test"));
                }
                else if (PageData.IsTest(item.Name, underTest))
                {
                    found.Add(path);
                }
            }
        }

        return [.. found.Order(StringComparer.Ordinal)];
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
