namespace Daoris.Driver;

/// <summary>git asked in a tree: its arguments, answered with its exit code and what it printed.</summary>
public delegate Task<(int Code, string Stdout, string Stderr)> CaptureGit(string tree, IReadOnlyList<string> arguments, CancellationToken ct);

/// <summary>
/// The asker's work in progress, captured for its helper (COAGENT1a, D199 point 5; the co-agent design §2.5): the bytes of every
/// changed tracked file, the deletions and every untracked file not ignored, with their manifest, read at the asker's turn boundary;
/// then laid over the helper's copy, which is removed as before with nothing read back from it.
/// </summary>
/// <remarks>
/// <para><b>It changes nothing of the asker's.</b> git is asked with <c>--no-optional-locks</c> (and <c>GIT_OPTIONAL_LOCKS=0</c>),
/// so its status refreshes no index; nothing writes the asker's index, refs or files.</para>
///
/// <para><b>Read, read again.</b> The listing is read, the bytes, then the listing again and every byte's hash: a capture that
/// changed under it is thrown away and taken once more, and one that changed again is refused <see cref="HelperWhy.CaptureMoved"/>.
/// The turn boundary is the look's (<see cref="HelperJudge"/>'s <c>turn</c>); this read is what holds where a turn began after it.</para>
///
/// <para><b>Links are never followed out.</b> A carried path under a link is refused; a link itself is refused where it leads out
/// of the tree and left out, said in the manifest, where it stays inside, since a copy is not handed a link.</para>
///
/// <para>A top file, not the Domain's (FLAT1b-ii): it runs git and reads the asker's files. Its rules are
/// <see cref="HelperCaptures"/>'.</para>
/// </remarks>
public static class HelperCapture
{
    /// <summary>The asker's work, read with git unless a test hands in its answers.</summary>
    public static async Task<HelperCaptureRead> TakeAsync(string tree, CaptureGit? git = null, CancellationToken ct = default)
    {
        git ??= GitAsync;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var first = await ListAsync(tree, git, ct).ConfigureAwait(false);
            if (first.Refused is { } refused) return refused;

            var read = Read(tree, first);
            if (read.Moved) continue;
            if (read.Refused is { } unread) return unread;

            var second = await ListAsync(tree, git, ct).ConfigureAwait(false);
            if (second.Refused is { } again) return again;
            if (second.Commit == first.Commit && second.Listing == first.Listing && Unchanged(tree, read.Captured!)) return new(read.Captured);
        }

        return HelperCaptureRead.Refused(HelperWhy.CaptureMoved);
    }

    /// <summary>
    /// Lay <paramref name="capture"/> over the helper's fresh copy: each deletion taken out, each file's bytes written, never through
    /// a link. Null where it was laid; otherwise why not, in a sentence.
    /// </summary>
    public static string? LayOver(string copy, HelperCaptured capture)
    {
        var root = Path.GetFullPath(copy);
        try
        {
            foreach (var path in capture.Deleted)
            {
                if (Inside(root, path) is not { } full) return $"`{path}` is not a path inside the copy.";
                if (LinkAbove(root, full) is { } link) return $"`{link}` in the copy is a link.";
                if (File.Exists(full) || IsLink(full)) File.Delete(full);
            }

            foreach (var file in capture.Files)
            {
                if (Inside(root, file.Path) is not { } full) return $"`{file.Path}` is not a path inside the copy.";
                if (LinkAbove(root, full) is { } link) return $"`{link}` in the copy is a link.";
                if (!capture.Bytes.TryGetValue(file.Path, out var bytes)) return $"`{file.Path}` was not carried.";
                if (Directory.Exists(full) && !IsLink(full)) return $"`{file.Path}` is a folder in the copy.";
                if (IsLink(full)) File.Delete(full);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllBytes(full, bytes);
            }

            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    private sealed record Listed(string Commit, string Listing, IReadOnlyList<StatusEntry> Entries, HelperCaptureRead? Refused = null)
    {
        public static Listed Refuse(HelperCaptureRead refused) => new("", "", [], refused);
    }

    private sealed record ReadFiles(HelperCaptured? Captured, HelperCaptureRead? Refused, bool Moved = false);

    private static readonly IReadOnlyDictionary<string, string> NoLocks = new Dictionary<string, string> { ["GIT_OPTIONAL_LOCKS"] = "0" };

    // An environment of its own, so no flow's cached answer stands in for a tree that may have moved (WorkingTree's GitFacts).
    private static Task<(int Code, string Stdout, string Stderr)> GitAsync(string tree, IReadOnlyList<string> arguments, CancellationToken ct) =>
        WorkingTree.GitAsync(tree, arguments, NoLocks, timeout: null, ct);

    /// <summary>The commit and the listing, with what refuses the capture before a byte is read.</summary>
    private static async Task<Listed> ListAsync(string tree, CaptureGit git, CancellationToken ct)
    {
        var (headCode, head, headError) = await git(tree, ["rev-parse", "--verify", "HEAD"], ct).ConfigureAwait(false);
        if (headCode != 0) return Listed.Refuse(HelperCaptureRead.Refused(HelperWhy.Unreadable, headError.Trim()));

        var (code, listing, error) = await git(
            tree,
            ["--no-optional-locks", "-c", "core.quotePath=false", "status", "--porcelain=v2", "-z", "--untracked-files=all", "--ignore-submodules=none"],
            ct).ConfigureAwait(false);
        if (code != 0) return Listed.Refuse(HelperCaptureRead.Refused(HelperWhy.Unreadable, error.Trim()));
        if (HelperCaptures.ParseStatus(listing) is not { } entries) return Listed.Refuse(HelperCaptureRead.Refused(HelperWhy.Unreadable, "git's status did not read"));

        if (entries.FirstOrDefault(entry => entry.Kind == 'u') is { } unmerged) return Listed.Refuse(HelperCaptureRead.Refused(HelperWhy.CaptureMerge, unmerged.Path));
        // An untracked folder git did not enter is judged where it is read: a link there is a link's, any other a repository's own.
        if (entries.FirstOrDefault(entry => entry is { Submodule: true, Kind: not '?' }) is { } submodule)
        {
            return Listed.Refuse(HelperCaptureRead.Refused(HelperWhy.CaptureSubmodule, submodule.Path));
        }

        if (entries.Count + entries.Count(entry => entry.From is not null) > HelperCaptures.PathBound)
        {
            return Listed.Refuse(HelperCaptureRead.Refused(HelperWhy.CaptureBound, $"more than {HelperCaptures.PathBound} paths"));
        }

        return new Listed(head.Trim(), listing, entries);
    }

    /// <summary>Every listed path read from the tree: its bytes, its deletion, or why the capture is refused.</summary>
    private static ReadFiles Read(string tree, Listed listed)
    {
        var root = Path.GetFullPath(tree);
        var tracked = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        var folders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in listed.Entries)
        {
            tracked[entry.Path] = entry.Kind != '?' || tracked.GetValueOrDefault(entry.Path);
            if (entry.From is { } from) tracked[from] = true;
            if (entry is { Kind: '?', Submodule: true }) folders.Add(entry.Path);
        }

        var files = new List<CapturedFile>();
        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var deleted = new List<string>();
        var left = new List<CaptureLeftOut>();
        long total = 0;
        foreach (var (path, isTracked) in tracked)
        {
            if (Inside(root, path) is not { } full) return Refuse(HelperWhy.CaptureUnreadable, path);
            if (LinkAbove(root, full) is { } above)
            {
                // A path under a link is never read through it: refused where the link leads out, left out where it stays inside.
                if (LinkTarget(Path.Combine(root, above.Replace('/', Path.DirectorySeparatorChar))) is not { } led || Inside(root, led) is null)
                {
                    return Refuse(HelperWhy.CaptureLink, above);
                }

                left.Add(new CaptureLeftOut(path, HelperCaptures.LeftLink));
                continue;
            }

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(full);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                // Gone: a tracked path is a deletion; an untracked one went while it was read, and the capture is taken again.
                if (!isTracked) return new ReadFiles(null, null, Moved: true);
                deleted.Add(path);
                continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return Refuse(HelperWhy.CaptureUnreadable, path);
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                var target = LinkTarget(full);
                if (target is null || Inside(root, target) is null) return Refuse(HelperWhy.CaptureLink, path);
                left.Add(new CaptureLeftOut(path, HelperCaptures.LeftLink));
                continue;
            }

            if (folders.Contains(path)) return Refuse(HelperWhy.CaptureSubmodule, path);

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                // A tracked file a folder now stands in place of: the file is gone, and what the folder holds is listed for itself.
                if (isTracked) deleted.Add(path);
                continue;
            }

            byte[] read;
            try
            {
                if (new FileInfo(full).Length > HelperCaptures.FileBound) return Refuse(HelperWhy.CaptureBound, path);
                read = File.ReadAllBytes(full);
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return new ReadFiles(null, null, Moved: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return Refuse(HelperWhy.CaptureUnreadable, path);
            }

            total += read.LongLength;
            if (read.LongLength > HelperCaptures.FileBound || total > HelperCaptures.TotalBound) return Refuse(HelperWhy.CaptureBound, path);
            files.Add(new CapturedFile(path, HelperCaptures.HashOf(read), read.LongLength));
            bytes[path] = read;
        }

        var id = HelperCaptures.IdOf(listed.Commit, files, deleted);
        return new ReadFiles(new HelperCaptured(id, listed.Commit, DateTimeOffset.UtcNow, files, deleted, left) { Bytes = bytes }, null);

        static ReadFiles Refuse(string code, string path) => new(null, HelperCaptureRead.Refused(code, path));
    }

    /// <summary>Whether every carried file still holds the bytes read, and every deletion is still gone.</summary>
    private static bool Unchanged(string tree, HelperCaptured capture)
    {
        var root = Path.GetFullPath(tree);
        try
        {
            foreach (var file in capture.Files)
            {
                if (Inside(root, file.Path) is not { } full || HelperCaptures.HashOf(File.ReadAllBytes(full)) != file.Hash) return false;
            }

            return capture.Deleted.All(path => Inside(root, path) is { } full && !File.Exists(full) && !IsLink(full));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The full path of a repository-relative <paramref name="path"/> under <paramref name="root"/>, or null where it leads out.</summary>
    private static string? Inside(string root, string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var within = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(within, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    /// <summary>The first folder between <paramref name="root"/> and <paramref name="full"/> that is a link, repository-relative; null where none is.</summary>
    private static string? LinkAbove(string root, string full)
    {
        var folder = Path.GetDirectoryName(full);
        var links = new List<string>();
        while (folder is not null && folder.Length > root.TrimEnd(Path.DirectorySeparatorChar).Length)
        {
            if (IsLink(folder)) links.Add(folder);
            folder = Path.GetDirectoryName(folder);
        }

        return links.Count == 0 ? null : Path.GetRelativePath(root, links[^1]).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsLink(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Where a link leads, as a full path; null where it names no target.</summary>
    private static string? LinkTarget(string full)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(full) ? new DirectoryInfo(full) : new FileInfo(full);
            if (info.LinkTarget is not { } target) return null;
            return Path.GetFullPath(Path.IsPathRooted(target) ? target : Path.Combine(Path.GetDirectoryName(full)!, target));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
