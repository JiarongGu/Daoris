namespace Daoris.Driver;

public sealed partial class SessionTrees
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
    /// The files under a shared ignored folder of a tree that were written after the tree opened and that the checkout's copy
    /// of the folder does not hold (TREEKEEP1): what the session made there, which no build re-makes. At most
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
            if (!File.Exists(Path.Combine(checkoutRoot, relative))) found.Add(relative);
        }

        return found;
    }
}
