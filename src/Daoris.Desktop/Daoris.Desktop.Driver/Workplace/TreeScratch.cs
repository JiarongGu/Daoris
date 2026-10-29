using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// The folders every build makes under these names, in any project (TIDYOUTPUT1): a path under one goes with the tree even where
    /// the checkout holds no such folder, since a tool names it, not a person. A narrower list than <see cref="BuildFolders"/>, whose
    /// other names (<c>out</c>, <c>dist</c>, <c>build</c>, <c>target</c>) are also what a session exports into, so those go only
    /// where the checkout builds there too.
    /// </summary>
    private static readonly HashSet<string> MadeFolders = new(StringComparer.OrdinalIgnoreCase) { "node_modules", "bin", "obj" };

    /// <summary>The repository's gates file, at its top.</summary>
    private const string GatesFile = "daoris.gates.json";

    /// <summary>The most files looked at under one shared ignored folder; past it the folder counts as the tree's own (TREEKEEP1).</summary>
    internal const int ScratchWalkLimit = 5000;

    /// <summary>Whether an ignored path lies under a folder every build makes, whether or not the checkout holds one (<see cref="MadeFolders"/>).</summary>
    internal static bool UnderMadeFolder(string path) =>
        path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(MadeFolders.Contains);

    /// <summary>
    /// The globs a repository declares as its own output (TIDYOUTPUT1): <c>integration.scratch</c> of the registered checkout's
    /// <c>daoris.gates.json</c>, the key D215 reserves for what the landing's tidy may take with a tree. Read from the checkout, never
    /// the tree, which holds the session's own copy; none where the file is absent or unreadable.
    /// </summary>
    internal static IReadOnlyList<Regex> Declared(string checkoutRoot)
    {
        try
        {
            var file = Path.Combine(checkoutRoot, GatesFile);
            return File.Exists(file) ? ParseDeclared(File.ReadAllText(file)) : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// The declared globs of a gates file's text, each as a pattern over a repository-relative path (<c>/</c> separated, case-insensitive):
    /// <c>*</c> within one segment, <c>**</c> across any, and a trailing <c>/**</c> the folder as well as what is under it. A glob is passed
    /// over where it is absolute, leaves the repository (<c>..</c>) or opens with a wildcard, since that would cover folders the
    /// repository never named: an authored ignored file (a <c>local/</c> note) is never output by a pattern's reach alone.
    /// </summary>
    internal static IReadOnlyList<Regex> ParseDeclared(string text)
    {
        var globs = new List<Regex>();
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("integration", out var integration) || integration.ValueKind != JsonValueKind.Object
                || !integration.TryGetProperty("scratch", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return globs;
            }

            foreach (var entry in list.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String && Glob(entry.GetString()!) is { } glob) globs.Add(glob);
            }
        }
        catch (JsonException)
        {
            // A declaration that does not read declares nothing, which keeps a tree rather than discarding one.
        }

        return globs;
    }

    private static Regex? Glob(string text)
    {
        var path = text.Trim().Replace('\\', '/');
        if (path.StartsWith('/') || path.Contains(':')) return null;
        var segments = path.Trim('/').Split('/');
        if (segments[0].Length == 0 || segments.Contains("..") || segments[0].AsSpan().IndexOfAny('*', '?') >= 0) return null;
        var pattern = new StringBuilder("^");
        for (var at = 0; at < segments.Length; at++)
        {
            var last = at == segments.Length - 1;
            if (segments[at] == "**")
            {
                // A trailing /** is the folder itself and all below it; a middle one is any number of folders.
                pattern.Append(last ? "(/.*)?" : "/(.*/)?");
                continue;
            }

            if (at > 0 && segments[at - 1] != "**") pattern.Append('/');
            pattern.Append(Regex.Escape(segments[at]).Replace("\\*", "[^/]*", StringComparison.Ordinal).Replace("\\?", "[^/]", StringComparison.Ordinal));
        }

        return new Regex(pattern.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Whether a declared glob covers <paramref name="path"/> or a folder above it, as git names an ignored path (a folder with its trailing slash).</summary>
    internal static bool IsDeclared(IReadOnlyList<Regex> declared, string path)
    {
        if (declared.Count == 0) return false;
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var length = 1; length <= segments.Length; length++)
        {
            var above = string.Join('/', segments.Take(length));
            if (declared.Any(glob => glob.IsMatch(above))) return true;
        }

        return false;
    }

    /// <summary>
    /// The files under a folder of the tree that the checkout does not hold at all, which are neither declared output nor under a build's
    /// own folder (TIDYOUTPUT1): what the session made there that nothing re-makes. No file of it is the checkout's, so no write time or
    /// comparison is asked. Bounded as <see cref="WrittenSince"/> is, with the same answer past it.
    /// </summary>
    /// <param name="entry">The folder, relative to the tree, as git names it.</param>
    internal static List<string> OwnFilesIn(string treeRoot, string entry, IReadOnlyList<Regex> declared, out bool tooLarge)
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

            var relative = Path.GetRelativePath(treeRoot, file).Replace('\\', '/');
            if (!UnderMadeFolder(relative) && !IsDeclared(declared, relative)) found.Add(relative);
        }

        return found;
    }

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
    internal static List<string> WrittenSince(string treeRoot, string checkoutRoot, string entry, DateTime opened, out bool tooLarge, IReadOnlyList<Regex>? declared = null)
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
            // TIDYOUTPUT1: a build's folder inside this folder, and a file the repository declares as output, are no session's own.
            if (UnderBuildFolder(relative) || (declared is not null && IsDeclared(declared, relative))) continue;
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
