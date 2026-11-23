namespace Daoris.Driver;

/// <summary>
/// What can be read of a tree's change from its top alone, with no walk: each entry directly under the tree's folder (a file's time and
/// length; a folder's time, which moves when something is added to it or taken out of it) and the tree's index and <c>HEAD</c> in its
/// git directory. Taken before a tree is judged and read again under the tidy's hold (<see cref="HoldLooks"/>).
/// </summary>
/// <remarks>
/// It sees a path made, removed or renamed at the top or one level down, a top file's write, and what git did to the tree. It does not see
/// a file written in place deeper down: the folder moved aside keeps the judgement's moment, which the purge holds a later write against.
/// </remarks>
internal sealed class TreeStamp
{
    private readonly Dictionary<string, string> _marks;

    private TreeStamp(Dictionary<string, string> marks) => _marks = marks;

    /// <summary>The stamps of the tree's top, its index and its <c>HEAD</c>; null where its folder cannot be read, which keeps the tree.</summary>
    internal static TreeStamp? Take(string tree)
    {
        if (SetAsideFaults.Current?.NoStamp == true) return null;
        try
        {
            var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
            var marks = new Dictionary<string, string>(StringComparer.Ordinal);
            var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
            foreach (var entry in new DirectoryInfo(top).EnumerateFileSystemInfos("*", options))
            {
                marks[entry.Name] = entry is FileInfo file ? $"{file.LastWriteTimeUtc.Ticks}:{file.Length}" : $"{entry.LastWriteTimeUtc.Ticks}:folder";
            }

            if (RefFiles.GitDirOf(top) is { } git)
            {
                foreach (var name in new[] { "index", "HEAD" })
                {
                    var info = new FileInfo(Path.Combine(git, name));
                    marks[$"git/{name}"] = info.Exists ? $"{info.LastWriteTimeUtc.Ticks}:{info.Length}" : "absent";
                }
            }

            return new TreeStamp(marks);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>What differs from <paramref name="earlier"/>, as a clause naming up to three paths and how many; null where nothing does.</summary>
    internal string? Differences(TreeStamp earlier)
    {
        var names = earlier._marks.Keys.Union(_marks.Keys, StringComparer.Ordinal)
            .Where(name => earlier._marks.GetValueOrDefault(name) != _marks.GetValueOrDefault(name))
            .Order(StringComparer.Ordinal)
            .ToList();
        return names.Count == 0 ? null : $"{names.Count} path(s) changed between the judgement and the move: {string.Join(", ", names.Take(3))}";
    }
}
