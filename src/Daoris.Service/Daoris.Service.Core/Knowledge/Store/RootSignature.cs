namespace Daoris.Knowledge;

/// <summary>
/// What one registered root looks like when the index reads it: the commit its <c>HEAD</c> named and the
/// newest write among its tracked record folders. A host over the registered roots judges each root by this at use, so a
/// record merged into a root becomes searchable without anyone telling the index; two equal signatures are one reading.
/// </summary>
/// <param name="Commit">The full commit <c>HEAD</c> resolves to, or null for a folder that is no git checkout or has no commit yet.</param>
/// <param name="Newest">The latest write time (UTC ticks) among the record files the scan reads, 0 where there are none.</param>
/// <param name="Files">How many record files that is, so a deletion moves the signature too.</param>
public sealed record RootSignature(string? Commit, long Newest, int Files)
{
    /// <summary>Folders no record lives in, skipped whatever their depth: a build's output, an install, every agent's checkout.</summary>
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "worktrees", "bin", "obj", "_fixtures", "local", "dist", "wwwroot",
    };

    /// <summary>The signature of a root as it stands: two file-system stats per record, one read of <c>HEAD</c>.</summary>
    /// <param name="documents">The documents folder the deployment names, relative to the root, or null.</param>
    /// <param name="index">The index folder the deployment names, relative to the root, or null.</param>
    public static RootSignature Of(string root, string? documents = null, string? index = null)
    {
        var newest = 0L;
        var files = 0;
        void Visit(string folder, bool recursive)
        {
            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(folder).EnumerateFileSystemInfos();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry is DirectoryInfo directory)
                {
                    if (recursive && !Skipped.Contains(directory.Name)) Visit(directory.FullName, recursive: true);
                    continue;
                }

                if (!IsRecord(entry.Name)) continue;
                files++;
                newest = Math.Max(newest, entry.LastWriteTimeUtc.Ticks);
            }
        }

        // The root's own top (its declaration, lock and backlog), then each folder the scan walks.
        Visit(root, recursive: false);
        var visited = new List<string>();
        foreach (var named in UncommittedRecords.Folders(root, documents, index))
        {
            var place = Path.GetFullPath(Path.Combine(root, named.Replace('/', Path.DirectorySeparatorChar)));
            // A place inside one already walked is counted once, with it.
            if (visited.Any(above => place.StartsWith(above + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
            if (Directory.Exists(place))
            {
                visited.Add(place);
                Visit(place, recursive: true);
            }
            else if (File.Exists(place))
            {
                files++;
                newest = Math.Max(newest, File.GetLastWriteTimeUtc(place).Ticks);
            }
        }

        return new RootSignature(Head(root), newest, files);
    }

    /// <summary>The commit a checkout's <c>HEAD</c> resolves to, or null where it names none.</summary>
    public static string? Head(string root)
    {
        try
        {
            if (CheckoutFreshness.GitFolders(root) is not { } git) return null;
            var head = File.ReadAllText(Path.Combine(git.Own, "HEAD")).Trim();
            if (!head.StartsWith("ref:", StringComparison.Ordinal)) return head.Length > 0 ? head : null;

            var reference = head["ref:".Length..].Trim();
            var loose = Path.Combine(git.Common, reference.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(loose)) return File.ReadAllText(loose).Trim();

            var packed = Path.Combine(git.Common, "packed-refs");
            if (!File.Exists(packed)) return null;
            foreach (var line in File.ReadLines(packed))
            {
                var parts = line.Split(' ', 2);
                if (parts.Length == 2 && parts[1].Trim() == reference) return parts[0];
            }

            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsRecord(string name) =>
        name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);
}
