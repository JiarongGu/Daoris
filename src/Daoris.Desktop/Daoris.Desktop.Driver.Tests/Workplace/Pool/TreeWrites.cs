using System.IO.Enumeration;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What an act wrote into a tree, counted from the file system as the design's R5.2 counts it: a file is written when it is new or its
/// size or its write time moved between two snapshots, and deleted when it is gone. The tree's own <c>.git</c> entry is not counted.
/// </summary>
internal sealed class TreeWrites
{
    private readonly Dictionary<string, (long Size, long Ticks)> _files;

    private TreeWrites(Dictionary<string, (long Size, long Ticks)> files) => _files = files;

    internal int Files => _files.Count;

    internal long Bytes => _files.Values.Sum(each => each.Size);

    internal static TreeWrites Of(string folder)
    {
        var files = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.System, IgnoreInaccessible = true };
        var walk = new FileSystemEnumerable<(string Path, long Size, long Ticks)>(
            folder,
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.Length, entry.LastWriteTimeUtc.UtcTicks),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory,
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !(entry.FileName.SequenceEqual(".git") && entry.Directory.Length <= folder.Length),
        };
        foreach (var (path, size, ticks) in walk)
        {
            var relative = Path.GetRelativePath(folder, path);
            if (relative != ".git") files[relative] = (size, ticks);
        }

        return new TreeWrites(files);
    }

    /// <summary>The files and bytes written, and the files deleted, between this snapshot and <paramref name="later"/>.</summary>
    internal (int Written, long Bytes, int Deleted) Until(TreeWrites later)
    {
        var written = 0;
        var bytes = 0L;
        foreach (var (path, now) in later._files)
        {
            if (_files.TryGetValue(path, out var was) && was == now) continue;
            written++;
            bytes += now.Size;
        }

        return (written, bytes, _files.Keys.Count(path => !later._files.ContainsKey(path)));
    }
}
