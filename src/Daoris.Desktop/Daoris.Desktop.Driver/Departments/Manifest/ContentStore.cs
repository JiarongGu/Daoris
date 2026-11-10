namespace Daoris.Driver;

/// <summary>
/// A place's text content, by hash, under the home: what a review's diff and a return's merge read of a file a manifest named. Only
/// text is kept (at most 1 MB, no NUL byte in its first 8,000 bytes, git's test), and only while the store is under its bound; past it
/// nothing new is kept and <see cref="Full"/> says so, so the review shows those paths without a diff.
/// </summary>
public sealed class ContentStore(string folder, long bound = ContentStore.Bound)
{
    /// <summary>A place's store, at most 1 GB.</summary>
    public const long Bound = 1L << 30;

    private const int Largest = 1 << 20;
    private const int Sniffed = 8000;

    /// <summary>Whether the bound refused a blob since this store opened.</summary>
    public bool Full { get; private set; }

    /// <summary>Where the blob of <paramref name="sha256"/> is kept.</summary>
    public string PathOf(string sha256) => Path.Combine(folder, sha256);

    /// <summary>Keep <paramref name="content"/> as <paramref name="sha256"/>'s blob where it is text and the bound allows.</summary>
    /// <returns>Whether it is kept now, already or newly.</returns>
    public bool Keep(string sha256, byte[] content)
    {
        if (content.Length > Largest || Array.IndexOf(content, (byte)0, 0, Math.Min(content.Length, Sniffed)) >= 0) return false;
        var path = PathOf(sha256);
        if (File.Exists(path)) return true;
        _size ??= Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateFiles().Sum(file => file.Length) : 0;
        if (_size + content.Length > bound)
        {
            Full = true;
            return false;
        }

        Directory.CreateDirectory(folder);
        AtomicFile.WriteBytes(path, content);
        _size += content.Length;
        return true;
    }

    private long? _size;

    /// <summary>Remove every blob no kept manifest names.</summary>
    public void Sweep(IReadOnlySet<string> named)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var blob in Directory.EnumerateFiles(folder))
        {
            if (!named.Contains(Path.GetFileName(blob))) File.Delete(blob);
        }

        _size = null;
    }
}
