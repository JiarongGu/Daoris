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
}

/// <summary>A file as its preview shows it.</summary>
/// <param name="Path">Relative to the tree, with forward slashes, as it was asked for — never the machine's path.</param>
/// <param name="Size">The whole file's size in bytes, however much of it <paramref name="Text"/> holds.</param>
/// <param name="Binary">Whether git would call it binary; then <paramref name="Text"/> is null.</param>
/// <param name="Text">The file's text, or its first <see cref="FilePreview.Budget"/> bytes cut at a line's end.</param>
/// <param name="Truncated">Whether the file holds more than <paramref name="Text"/>.</param>
public sealed record PreviewedFile(string Path, long Size, bool Binary, string? Text, bool Truncated);

/// <summary>What reading a file for its preview answered: the file, or why not.</summary>
public sealed record FilePreviewResult(FilePreviewRefusal Refusal, PreviewedFile? File)
{
    public static FilePreviewResult Refused(FilePreviewRefusal why) => new(why, null);
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
