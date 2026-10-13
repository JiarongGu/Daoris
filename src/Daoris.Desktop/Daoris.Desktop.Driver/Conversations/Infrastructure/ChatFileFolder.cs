namespace Daoris.Driver;

/// <summary>
/// A session's attached files on the disk (CONV4c): <c>&lt;home&gt;/sessions/&lt;id&gt;/files</c>, each written beside and renamed,
/// and found by its leaf. Conversations' Infrastructure (STRUCT3h, D166's note): what is kept, and under which leaf, is
/// <see cref="ChatFiles"/>'s rule; the Application's <c>ChatFiles.Keep</c> and <c>Find</c> compose the two.
/// </summary>
internal static class ChatFileFolder
{
    /// <summary>Each file written into <paramref name="folder"/> under its leaf, unless one lies there already; what is kept, in order.</summary>
    public static IReadOnlyList<KeptFile> Keep(string folder, IReadOnlyList<(string Name, string Leaf, byte[] Content)> keeping)
    {
        Directory.CreateDirectory(folder);
        var kept = new List<KeptFile>();
        foreach (var (name, leaf, content) in keeping)
        {
            var path = Path.Combine(folder, leaf);
            if (!File.Exists(path))
            {
                AtomicFile.WriteBytes(path, content);
            }

            kept.Add(new KeptFile(name, path));
        }

        return kept;
    }

    /// <summary>The newest file in <paramref name="folder"/> whose leaf <paramref name="named"/> takes; null for none, or no folder.</summary>
    public static string? Newest(string folder, Func<string, bool> named)
    {
        if (!Directory.Exists(folder)) return null;
        return Directory.EnumerateFiles(folder)
            .Where(each => named(Path.GetFileName(each)))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
