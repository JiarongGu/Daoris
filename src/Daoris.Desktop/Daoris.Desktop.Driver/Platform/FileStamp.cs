using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>A file's or a folder's stat, from the one call that finds it: a reparse point of any kind is a link, whose own stat does not move with its target.</summary>
internal readonly record struct FileStat(long Length, long Written, bool Link, bool Folder = false)
{
    /// <summary>The stat of the file or folder at <paramref name="path"/>; null where nothing stands there.</summary>
    internal static FileStat? Of(string path)
    {
        try
        {
            FileSystemInfo info = new FileInfo(path);
            if (!info.Exists) info = new DirectoryInfo(path);
            if (!info.Exists) return null;
            return new FileStat(
                info is FileInfo file ? file.Length : 0, info.LastWriteTimeUtc.Ticks, info.Attributes.HasFlag(FileAttributes.ReparsePoint), info is DirectoryInfo);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether this stat may stand for its bytes from <paramref name="now"/>, the volume's clock read after it: git's rule for a racy index
    /// entry, since a second write of one length inside the clock's grain leaves the same stat.
    /// </summary>
    internal bool SettledAt(DateTime? now) => !Link && now is { } at && at.Ticks - Written >= Grain.Ticks;

    /// <summary>How long past a write its stat is trusted to move with every later write.</summary>
    internal static readonly TimeSpan Grain = TimeSpan.FromSeconds(2);
}

/// <summary>What one ask of a file's stamp answered: its stat, its content's SHA-256 (null where absent), and the bytes where this ask read them.</summary>
internal sealed record FileStamped(string Path, FileStat? Stat, string? Content, byte[]? Bytes)
{
    /// <summary>The file stands and did not read: its content is one of its own, equal to nothing.</summary>
    internal bool Unread { get; init; }
}

/// <summary>
/// A file's content by its stat: the bytes hashed again only where the stat moved or is not trusted, so a warm ask is one stat.
/// </summary>
/// <remarks>
/// <para><b>The stat before the bytes is the reference.</b> It is read, then the volume's clock (<see cref="VolumeClock"/>), then the
/// bytes, then the stat again. A stat that moved during the read answers this ask and keeps nothing. One that did not is kept only where
/// the clock read before the bytes stands two seconds past its write time; trust is fixed as the bytes are hashed, and time never promotes an
/// entry, so an untrusted file is read at every ask.</para>
/// <para><b>A link</b> is read at every ask. An unreadable file answers a content of its own, equal to nothing.</para>
/// </remarks>
internal static class FileStamp
{
    // Bounded by count: the paths are a few files a repository, and a long run's fixtures are dropped together.
    private const int Bound = 4096;

    private static readonly ConcurrentDictionary<string, (FileStat Stat, string Content)> Kept = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(string Path, Type Kind), (string Content, object Parsed)> Parses = new();

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static void Forget(string path) => Kept.TryRemove(path, out _);

    /// <summary>The stamp of <paramref name="path"/>; with <paramref name="bytes"/>, its bytes read and handed back whatever is kept.</summary>
    internal static FileStamped Of(string path, bool bytes = false)
    {
        var before = FileStat.Of(path);
        if (before is not { } stat || stat.Folder)
        {
            Kept.TryRemove(path, out _);
            return new FileStamped(path, before, null, null);
        }

        if (!bytes && !stat.Link && Kept.TryGetValue(path, out var kept) && kept.Stat == stat) return new FileStamped(path, stat, kept.Content, null);

        var now = VolumeClock.Now(path);
        byte[] read;
        try
        {
            read = File.ReadAllBytes(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Kept.TryRemove(path, out _);
            return new FileStamped(path, stat, "unreadable:" + Guid.NewGuid().ToString("N"), null) { Unread = true };
        }

        var content = Hash(read);
        if (FileStat.Of(path) == stat && stat.SettledAt(now))
        {
            if (Kept.Count >= Bound) Kept.Clear();
            Kept[path] = (stat, content);
        }
        else
        {
            Kept.TryRemove(path, out _);
        }

        return new FileStamped(path, stat, content, read);
    }

    /// <summary>
    /// The file's stamp and <paramref name="parse"/> of its bytes, made once a content: a parse is kept by the content it is made from,
    /// and an ask whose stamp answers that content takes it without reading the file.
    /// </summary>
    internal static (FileStamped Stamp, T? Parsed) Parsed<T>(string path, Func<byte[], T> parse) where T : class
    {
        var stamp = Of(path);
        if (stamp.Content is null) return (stamp, null);
        var key = (path, typeof(T));
        if (Parses.TryGetValue(key, out var made) && made.Content == stamp.Content && made.Parsed is T kept) return (stamp, kept);

        if (stamp.Bytes is null) stamp = Of(path, bytes: true);
        if (stamp.Bytes is not { } bytes) return (stamp, null);
        var parsed = parse(bytes);
        if (Parses.Count >= Bound) Parses.Clear();
        Parses[key] = (stamp.Content!, parsed);
        return (stamp, parsed);
    }
}
