using System.Text;

namespace Daoris.Driver;

/// <summary>Why a file was not read for its preview, or <see cref="None"/> when it was (PREVIEW1, D111).</summary>
public enum FilePreviewRefusal
{
    /// <summary>Read: the answer carries the file.</summary>
    None,

    /// <summary>The path names somewhere outside the tree.</summary>
    Outside,

    /// <summary>The path goes through a link inside the tree that leads out of it, or one that cannot be resolved.</summary>
    LinkLeaves,

    /// <summary>The path is under `.git`, which is git's own and not the work's.</summary>
    GitFolder,

    /// <summary>There is no tree here to read from: none named, or the one named is gone.</summary>
    NoTree,

    /// <summary>The path is not a file now: deleted, moved, a folder, or not readable.</summary>
    NotAFile,

    /// <summary>
    /// Read from a landed branch (REVIEW2, D113): the path is not a file that branch holds — deleted or moved there, a
    /// folder, or a link, which git keeps as the link and the preview does not follow.
    /// </summary>
    NotOnBranch,
}

/// <summary>A file as its preview shows it.</summary>
/// <param name="Path">Relative to the tree, with forward slashes, as it was asked for — never the machine's path.</param>
/// <param name="Size">The whole file's size in bytes, however much of it <paramref name="Text"/> holds.</param>
/// <param name="Binary">Whether git would call it binary; then <paramref name="Text"/> is null.</param>
/// <param name="Text">The file's text, or its first <see cref="FilePreview.Budget"/> bytes cut at a line's end.</param>
/// <param name="Truncated">Whether the file holds more than <paramref name="Text"/>.</param>
public sealed record PreviewedFile(string Path, long Size, bool Binary, string? Text, bool Truncated)
{
    /// <summary>The landed branch it was read from, once the session's tree is gone (REVIEW2, D113); null for the file on disk.</summary>
    public string? Branch { get; init; }
}

/// <summary>What reading a file for its preview answered: the file, or why not.</summary>
/// <param name="Branch">The landed branch the reading asked, where it asked one (REVIEW2), so a refusal can name it.</param>
public sealed record FilePreviewResult(FilePreviewRefusal Refusal, PreviewedFile? File, string? Branch = null)
{
    public static FilePreviewResult Refused(FilePreviewRefusal why, string? branch = null) => new(why, null, branch);
}

/// <summary>
/// A file in a session's tree, read for the person to look at without leaving the window (PREVIEW1, D111).
/// </summary>
/// <remarks>
/// <para><b>Reads, and never writes</b> (D55: there is no editor). The disk as it is now, not git's
/// committed copy: a live session's last edit is uncommitted, and the preview says what the file is.</para>
///
/// <para><b>Only inside the tree.</b> A path is judged three ways: where its string lands, where each link
/// on the way leads, and whether any of it is `.git`. The string alone is the tree guard's lesson (PERM3):
/// a link inside the tree can lead out, and the bytes are then somewhere nobody asked to show.</para>
///
/// <para><b>Bounded, and the bound is stated</b> (design §5): the first <see cref="Budget"/> bytes, cut at a
/// line's end so no character is split, with the whole size said beside it.</para>
/// </remarks>
public static class FilePreview
{
    /// <summary>How much of a file a preview holds before the rest is only counted.</summary>
    public const int Budget = 256 * 1024;

    /// <summary>How far in a NUL makes a file binary — git's own test (`buffer_is_binary`, 8,000 bytes).</summary>
    public const int BinaryProbe = 8000;

    /// <summary>How many links one path may pass through before it is taken to be a loop.</summary>
    private const int LinkHops = 32;

    /// <summary>Read <paramref name="path"/> in <paramref name="tree"/> for its preview.</summary>
    /// <param name="tree">The session's tree, or the repository's checkout a conversation runs in.</param>
    /// <param name="path">Relative to the tree (forward or back slashes), or absolute inside it.</param>
    public static Task<FilePreviewResult> ReadAsync(string tree, string path, CancellationToken ct = default) =>
        ReadAsync(tree, path, LinkTarget, ct);

    /// <summary>
    /// The same, with what a path's link resolves to handed in: a full path, or null where the path is no link.
    /// A throw is a link nobody can resolve, which is refused, since it cannot be shown to stay inside.
    /// </summary>
    internal static async Task<FilePreviewResult> ReadAsync(
        string tree, string path, Func<string, string?> linkTarget, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tree) || !Directory.Exists(tree)) return FilePreviewResult.Refused(FilePreviewRefusal.NoTree);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
        // A tree reached through a link of its own is the same tree by its target's name.
        string? rootTarget;
        try
        {
            rootTarget = linkTarget(root) is { } resolved ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolved)) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            rootTarget = null;
        }

        var asked = Relative(root, path);
        if (asked.Refusal is not FilePreviewRefusal.None) return FilePreviewResult.Refused(asked.Refusal);

        // Walked a segment at a time, following each link to where it leads and judging THAT, then going on
        // from there. A link back inside starts the walk again from the tree, so a chain is judged whole.
        var segments = asked.Segments;
        var hops = 0;
        var at = 0;
        var current = root;
        while (at < segments.Count)
        {
            current = Path.Combine(current, segments[at]);
            string? target;
            try
            {
                target = linkTarget(current);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return FilePreviewResult.Refused(FilePreviewRefusal.LinkLeaves);
            }

            if (target is null)
            {
                at++;
                continue;
            }

            if (++hops > LinkHops) return FilePreviewResult.Refused(FilePreviewRefusal.LinkLeaves);

            var landed = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(current)!, target));
            var within = Inside(root, landed) ? root : rootTarget is not null && Inside(rootTarget, landed) ? rootTarget : null;
            if (within is null) return FilePreviewResult.Refused(FilePreviewRefusal.LinkLeaves);

            var rest = Path.GetRelativePath(within, landed).Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => segment != ".")
                .Concat(segments.Skip(at + 1))
                .ToList();
            if (rest.Any(IsGit)) return FilePreviewResult.Refused(FilePreviewRefusal.GitFolder);

            segments = rest;
            current = root;
            at = 0;
        }

        return await ReadFileAsync(current, string.Join('/', asked.Segments), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A file as a session's landed branch holds it (REVIEW2, D113), for the preview of a session whose landing tidied
    /// its tree away: <c>git cat-file blob</c> of what the branch's tree names at that path, in the repository's own
    /// checkout.
    /// </summary>
    /// <remarks>
    /// <para><b>A read of objects, and nothing else</b>: the checkout's working tree and index are never asked, so what
    /// the person has in flight there is neither shown nor touched. The same bound and binary test as a file on disk.</para>
    ///
    /// <para>🔴 <b>The checkout is proven first</b>, since git walks UP (FIX-LOG); and <b>only the landing's own
    /// branch</b> is read: one standing and still holding the commit the landing made it at. Anything else — no
    /// checkout, the branch gone, a branch of that name that is someone else's now — is <see cref="FilePreviewRefusal.NoTree"/>,
    /// since then neither the tree nor the branch is here.</para>
    ///
    /// <para><b>The path is judged as a path in the tree was</b>: outside the repository, and under `.git`, are refused
    /// before git is asked. A path the conversation named inside the tree that is gone is the same path on the branch.
    /// Git does not follow a link inside a tree, and neither does this: a link, a folder, or a path the branch lacks is
    /// <see cref="FilePreviewRefusal.NotOnBranch"/>.</para>
    /// </remarks>
    /// <param name="root">The repository's checkout on this machine, from the registry; null where it has none.</param>
    /// <param name="tree">The session's tree as its record names it, where a path the page sends is inside it.</param>
    public static async Task<FilePreviewResult> ReadLandedAsync(
        string? root, LandedBranch entry, string path, string? tree = null, CancellationToken ct = default)
    {
        if (root is null || !await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false)) return FilePreviewResult.Refused(FilePreviewRefusal.NoTree);
        // The record's words become git's arguments; a trace names a branch that is gone.
        if (entry.GoneAt is not null || !WorkingTree.IsCommitId(entry.Tip) || !BranchName.IsValid(entry.Branch))
        {
            return FilePreviewResult.Refused(FilePreviewRefusal.NoTree);
        }

        var (tipCode, tipOut, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{entry.Branch}^{{commit}}"], ct).ConfigureAwait(false);
        if (tipCode != 0) return FilePreviewResult.Refused(FilePreviewRefusal.NoTree);
        var tip = tipOut.Trim();
        var (ours, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", entry.Tip, tip], ct).ConfigureAwait(false);
        if (ours != 0) return FilePreviewResult.Refused(FilePreviewRefusal.NoTree);

        var checkout = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var asked = InsideTree(tree, path) is { } named ? Relative(named, path) : Relative(checkout, path);
        if (asked.Refusal is not FilePreviewRefusal.None) return FilePreviewResult.Refused(asked.Refusal, entry.Branch);
        var relative = string.Join('/', asked.Segments);

        // Literal, so a `*` or a `:(` in a name is that name and not a pattern.
        var (listCode, listed, _) = await WorkingTree.GitAsync(
            root, ["--literal-pathspecs", "ls-tree", "-l", "-z", tip, "--", relative], ct).ConfigureAwait(false);
        if (listCode != 0 || Listed(listed, relative) is not { } blob) return FilePreviewResult.Refused(FilePreviewRefusal.NotOnBranch, entry.Branch);

        var read = await WorkingTree.GitBytesAsync(root, ["cat-file", "blob", blob.Object], (int)Math.Min(blob.Size, Budget), ct).ConfigureAwait(false);
        if (read is not { } bytes) return FilePreviewResult.Refused(FilePreviewRefusal.NotOnBranch, entry.Branch);

        return new(FilePreviewRefusal.None, Decode(bytes.Bytes, bytes.Count, blob.Size, relative) with { Branch = entry.Branch }, entry.Branch);
    }

    /// <summary>
    /// The one file <c>git ls-tree -l -z</c> listed at exactly <paramref name="path"/> — a blob, not a link or a folder —
    /// with its object and its size; null where it listed none.
    /// </summary>
    internal static (string Object, long Size)? Listed(string output, string path)
    {
        foreach (var record in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // `<mode> SP <type> SP <object> SP+ <size> TAB <path>`: the size is padded, and a folder's is `-`.
            var tab = record.IndexOf('\t');
            if (tab < 0 || !string.Equals(record[(tab + 1)..], path, StringComparison.Ordinal)) continue;
            var fields = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 4 || fields[1] != "blob" || fields[0] is not ("100644" or "100755")) return null;
            return long.TryParse(fields[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var size)
                ? (fields[2], size)
                : null;
        }

        return null;
    }

    /// <summary>The tree <paramref name="path"/> names a place inside, where it is a whole path into it; else null.</summary>
    private static string? InsideTree(string? tree, string path)
    {
        if (string.IsNullOrWhiteSpace(tree) || string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var spelled = OperatingSystem.IsWindows() ? path.Replace('\\', '/') : path;
            if (!Path.IsPathRooted(spelled)) return null;
            var named = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
            return Inside(named, Path.GetFullPath(spelled)) ? named : null;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>The asked-for path as segments under the tree, or why it is not under it.</summary>
    private static (FilePreviewRefusal Refusal, IReadOnlyList<string> Segments) Relative(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (FilePreviewRefusal.NotAFile, []);

        string full;
        try
        {
            // A back slash is a separator on Windows alone: elsewhere it may be part of a name.
            var spelled = OperatingSystem.IsWindows() ? path.Replace('\\', '/') : path;
            full = Path.GetFullPath(Path.IsPathRooted(spelled) ? spelled : Path.Combine(root, spelled));
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return (FilePreviewRefusal.Outside, []);
        }

        if (SamePath(full, root)) return (FilePreviewRefusal.NotAFile, []);
        if (!Inside(root, full)) return (FilePreviewRefusal.Outside, []);

        var segments = Path.GetRelativePath(root, full).Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        // A colon inside the tree is an alternate stream on Windows, or a drive-relative path: neither is a file here.
        if (OperatingSystem.IsWindows() && segments.Any(segment => segment.Contains(':'))) return (FilePreviewRefusal.Outside, []);
        if (segments.Any(IsGit)) return (FilePreviewRefusal.GitFolder, []);
        return (FilePreviewRefusal.None, segments);
    }

    private static async Task<FilePreviewResult> ReadFileAsync(string file, string named, CancellationToken ct)
    {
        if (!File.Exists(file)) return FilePreviewResult.Refused(FilePreviewRefusal.NotAFile);

        try
        {
            // Shared every way: an agent may be writing the file while the person reads it.
            await using var stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
            var size = stream.Length;
            var buffer = new byte[(int)Math.Min(size, Budget)];
            var read = 0;
            while (read < buffer.Length)
            {
                var got = await stream.ReadAsync(buffer.AsMemory(read), ct).ConfigureAwait(false);
                if (got == 0) break;
                read += got;
            }

            return new(FilePreviewRefusal.None, Decode(buffer, read, size, named));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return FilePreviewResult.Refused(FilePreviewRefusal.NotAFile);
        }
    }

    /// <summary>The bytes read as a preview's file: binary by git's test, or text cut at a line's end.</summary>
    private static PreviewedFile Decode(byte[] buffer, int read, long size, string named)
    {
        var bytes = buffer.AsSpan(0, read);
        if (bytes[..Math.Min(read, BinaryProbe)].IndexOf((byte)0) >= 0)
        {
            return new PreviewedFile(named, size, Binary: true, Text: null, Truncated: false);
        }

        var truncated = size > read;
        if (truncated)
        {
            // Cut at the last line's end, so no line is half shown and no character is split.
            var end = bytes.LastIndexOf((byte)'\n');
            if (end >= 0) bytes = bytes[..(end + 1)];
        }

        var preamble = Encoding.UTF8.Preamble;
        if (bytes.StartsWith(preamble)) bytes = bytes[preamble.Length..];
        return new PreviewedFile(named, size, Binary: false, Encoding.UTF8.GetString(bytes), truncated);
    }

    /// <summary>What the path's link leads to, followed to its end, or null where the path is no link.</summary>
    private static string? LinkTarget(string path)
    {
        FileSystemInfo info = new FileInfo(path);
        if (info.LinkTarget is null)
        {
            // A folder's link reads as a folder: ask it as one where it is one.
            if (!Directory.Exists(path)) return null;
            info = new DirectoryInfo(path);
            if (info.LinkTarget is null) return null;
        }

        return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
            ?? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, info.LinkTarget));
    }

    private static readonly char[] Separators = ['/', '\\'];

    private static bool IsGit(string segment) => string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), PathComparison);

    /// <summary>Whether <paramref name="path"/> is under <paramref name="root"/> — by folder, never by string prefix.</summary>
    private static bool Inside(string root, string path)
    {
        var folder = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return Path.TrimEndingDirectorySeparator(path).StartsWith(folder, PathComparison);
    }
}
