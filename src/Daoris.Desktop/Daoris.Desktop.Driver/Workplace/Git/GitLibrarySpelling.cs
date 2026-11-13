namespace Daoris.Driver;

/// <summary>
/// A folder's path as git prints it: absolute, with forward slashes, each name in the case the file system keeps it and the drive's letter
/// in capitals. Null where a name along it is a link or a short (8.3) name, which git resolves to another path.
/// </summary>
internal static class GitLibrarySpelling
{
    internal static string? Real(string path)
    {
        try
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root)) return null;
            var spelt = root.TrimEnd('\\', '/');
            if (OperatingSystem.IsWindows())
            {
                if (spelt.Length != 2 || spelt[1] != ':') return null;
                spelt = char.ToUpperInvariant(spelt[0]) + ":";
            }

            var at = root;
            foreach (var name in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                if (name.Contains('~')) return null;
                var found = new DirectoryInfo(at).EnumerateFileSystemInfos(name).FirstOrDefault();
                if (found is null || found.LinkTarget is not null || found.Attributes.HasFlag(FileAttributes.ReparsePoint)) return null;
                spelt += "/" + found.Name;
                at = found.FullName;
            }

            return spelt;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
