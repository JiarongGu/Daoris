namespace Daoris.Driver;

/// <summary>
/// The walk over a tree's shared ignored folders (TREEKEEP1): what a session wrote there that the checkout does not hold. It reads
/// nothing of the trees' context, so it is static.
/// </summary>
internal static class TreeScratch
{
    /// <summary>
    /// The folders a build or an install writes and writes again (TREEKEEP1): an ignored path under one of these, which the
    /// registered checkout holds too, goes with the tree unasked, as D102's SQUASHTIDY1c rule always let it. Matched by any
    /// path segment, so <c>src/app/node_modules/</c> is one. Any other shared ignored folder is looked into
    /// (<see cref="WrittenSince"/>), since a session's own scratch (<c>devtools/.tmp</c>) lives in such a folder too.
    /// </summary>
    private static readonly HashSet<string> BuildFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "dist", "build", "out", ".angular", ".next", "coverage", "target", "__pycache__", ".venv",
        ".gradle",
    };

    /// <summary>The most files looked at under one shared ignored folder; past it the folder counts as the tree's own (TREEKEEP1).</summary>
    internal const int ScratchWalkLimit = 5000;

    /// <summary>Whether an ignored path lies under a build or dependency folder (any segment of it names one).</summary>
    internal static bool UnderBuildFolder(string path) =>
        path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(BuildFolders.Contains);

    /// <summary>
    /// The files under a shared ignored folder of a tree that were written after the tree opened and that the checkout
    /// does not hold as written, absent or different (TREEKEEP1): what the session made or changed there, which no build re-makes. At most
    /// <see cref="ScratchWalkLimit"/> files are looked at; a folder with more is <paramref name="tooLarge"/>, and counts as the
    /// tree's own, since never removing it unasked is the conservative answer.
    /// </summary>
    /// <param name="entry">The folder, relative to both roots, as git names it.</param>
    /// <param name="opened">When the tree opened: a file written no later than this is not the session's.</param>
    internal static List<string> WrittenSince(string treeRoot, string checkoutRoot, string entry, DateTime opened, out bool tooLarge)
    {
        tooLarge = false;
        var found = new List<string>();
        var folder = Path.Combine(treeRoot, entry.TrimEnd('/'));
        if (!Directory.Exists(folder)) return found;
        var seen = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(folder, "*", options))
        {
            if (++seen > ScratchWalkLimit)
            {
                tooLarge = true;
                return found;
            }

            if (File.GetLastWriteTimeUtc(file) <= opened) continue;
            var relative = Path.GetRelativePath(treeRoot, file).Replace('\\', '/');
            if (!SameBytes(file, Path.Combine(checkoutRoot, relative))) found.Add(relative);
        }

        return found;
    }

    /// <summary>
    /// Whether the checkout holds a file at the path with the same length and the same bytes: only then is the tree's file
    /// no work lost. Lengths are compared first and bytes only where they match, streamed through a small buffer.
    /// </summary>
    private static bool SameBytes(string tree, string checkout)
    {
        try
        {
            if (!File.Exists(checkout) || new FileInfo(tree).Length != new FileInfo(checkout).Length) return false;
            using var a = new FileStream(tree, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var b = new FileStream(checkout, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var left = new byte[8192];
            var right = new byte[8192];
            while (true)
            {
                var n = a.ReadAtLeast(left, left.Length, throwOnEndOfStream: false);
                var m = b.ReadAtLeast(right, right.Length, throwOnEndOfStream: false);
                if (n != m || !left.AsSpan(0, n).SequenceEqual(right.AsSpan(0, m))) return false;
                if (n < left.Length) return true;
            }
        }
        catch (IOException)
        {
            // A file that cannot be read cannot be shown the same: it counts as the tree's own, which keeps the tree.
            return false;
        }
    }
}
