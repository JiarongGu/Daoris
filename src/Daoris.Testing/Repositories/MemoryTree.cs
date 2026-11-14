namespace Daoris.Testing;

/// <summary>One checkout of a memory repository: what HEAD stands on, the files in its folder, and the lock git keeps on it.</summary>
internal sealed class MemoryTree(string path, string? branch, string? detached)
{
    internal string Path { get; } = path;

    /// <summary>The branch HEAD names, or null where it is detached.</summary>
    internal string? Branch { get; set; } = branch;

    /// <summary>The commit a detached HEAD stands at.</summary>
    internal string? Detached { get; set; } = detached;

    /// <summary>The files in the folder, by path from its top.</summary>
    internal Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>The paths that are submodules, which hold no file here.</summary>
    internal HashSet<string> Submodules { get; } = new(StringComparer.Ordinal);

    /// <summary>The tracked paths marked assume-unchanged.</summary>
    internal HashSet<string> Hidden { get; } = new(StringComparer.Ordinal);

    internal bool Locked { get; set; }

    internal string? Reason { get; set; }

    /// <summary>Whether the folder is gone, which a prune forgets.</summary>
    internal bool Gone { get; set; }
}

/// <summary>The rules a <c>.gitignore</c> at a checkout's top gives: a name, a directory (<c>name/</c>) or an extension (<c>*.ext</c>), at any depth.</summary>
internal static class MemoryIgnores
{
    /// <summary>The top an ignored path is told at (a directory's with its slash, a file's whole), or null where nothing ignores it.</summary>
    internal static string? Top(IReadOnlyDictionary<string, string> files, string path)
    {
        if (!files.TryGetValue(".gitignore", out var text)) return null;
        var rules = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).Where(line => line.Length > 0 && line[0] != '#').ToList();
        var parts = path.Split('/');
        for (var at = 0; at < parts.Length; at++)
        {
            var last = at == parts.Length - 1;
            foreach (var rule in rules)
            {
                var directory = rule.EndsWith('/');
                var name = rule.TrimEnd('/');
                var matches = name.StartsWith("*.", StringComparison.Ordinal) ? parts[at].EndsWith(name[1..], StringComparison.Ordinal) : parts[at] == name;
                if (!matches || (directory && last)) continue;
                return last ? path : string.Join('/', parts.Take(at + 1)) + "/";
            }
        }

        return null;
    }
}
