namespace Daoris.Driver;

/// <summary>
/// Which of a tree's folders the look may drop once it is set aside (KEEP1a, D195 point 3; the cleanup design §5): its package folders,
/// each a <c>node_modules</c> beside a <c>package.json</c> git tracks, whose producer discards it whole at every clean install, so nothing
/// kept there outlives the next. Judged before the repository's hold and checked again inside it by the last look's own reads
/// (<see cref="TreeSetAside.LastLookAsync"/>, Fable F1.5). Static: it reads git and the paths it is handed, nothing of the trees' context.
/// </summary>
/// <remarks>
/// 🔴 <b>Where anything about a path is unknown, it stays</b>: git's status, the index or the folders that could not be read judge
/// nothing droppable, and the tree moves aside whole with no manifest, as before KEEP1a.
/// </remarks>
internal static class TreeSlims
{
    /// <summary>The one folder name the slim takes: a package folder's.</summary>
    internal const string PackageFolder = "node_modules";

    /// <summary>
    /// The judgement, before the hold (Fable F1.5), in <c>SweepPlanAsync</c>: each folder git ignores whole
    /// (<c>status --ignored=traditional --untracked-files=normal</c> names it <c>!! &lt;path&gt;/</c>; one git lists file by file, a tracked
    /// file inside it, is not one) whose last segment is <c>node_modules</c> and whose folder above holds a <c>package.json</c> the index
    /// lists, in git's spelling, in order. None where git could not answer.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> JudgeAsync(string tree, CancellationToken ct)
    {
        var (code, porcelain, _) = await WorkingTree.GitAsync(
            tree, ["status", "--porcelain=v1", "-z", "--ignored=traditional", "--untracked-files=normal"], ct).ConfigureAwait(false);
        if (code != 0) return [];
        var candidates = new List<string>();
        var entries = porcelain.Split('\0');
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            if (entry.StartsWith("!! ", StringComparison.Ordinal))
            {
                var path = entry[3..];
                if (path.EndsWith('/') && Refusal(path.TrimEnd('/')) is null) candidates.Add(path.TrimEnd('/'));
                continue;
            }

            // A rename's or a copy's source follows its entry, and is no path of its own.
            if (entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') at++;
        }

        if (candidates.Count == 0) return [];
        var (listed, files, _) = await WorkingTree.GitAsync(
            tree, ["ls-files", "-z", "--", .. candidates.Select(path => ":(literal)" + PackageJsonOf(path))], ct).ConfigureAwait(false);
        if (listed != 0) return [];
        var indexed = files.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        return [.. candidates.Where(path => indexed.Contains(PackageJsonOf(path))).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The judgement checked again inside the hold by the last look's own reads (Fable F1.5): its index read still lists each folder's
    /// <c>package.json</c>; no repository lies at, under or above it; and no link lies on the way to it from the tree's top, it included.
    /// A link inside it is a package manager's, which goes as a link with its target untouched (TIDIEDLINK1).
    /// </summary>
    /// <param name="indexed">Every path the tree's index lists, in git's spelling.</param>
    /// <param name="nested">Every folder below the tree's top holding a <c>.git</c>, from the last look's one walk.</param>
    /// <param name="links">Every link that walk met, never followed.</param>
    internal static IReadOnlyList<string> Confirmed(
        IReadOnlyList<string> judged, IReadOnlySet<string> indexed, IReadOnlyList<string> nested, IReadOnlyList<string> links) =>
    [
        .. judged.Where(path => Refusal(path) is null
                                && indexed.Contains(PackageJsonOf(path))
                                && !nested.Any(repository => Same(repository, path) || Under(repository, path) || Under(path, repository))
                                && !links.Any(link => Same(link, path) || Under(path, link))),
    ];

    /// <summary>
    /// Why a manifest's path may not be taken (Codex 2.5; the cleanup design §5 step 2), read from its words alone: none, rooted, a drive
    /// or a backslash in it, a part empty, <c>.</c> or <c>..</c>, or a last part that is no package folder's. Null where it may.
    /// </summary>
    internal static string? Refusal(string path)
    {
        if (string.IsNullOrEmpty(path)) return "it names no path";
        if (path.Contains('\\') || path.Contains(':') || path.StartsWith('/') || Path.IsPathRooted(path)) return "it is rooted, or not in git's spelling";
        var parts = path.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or "..")) return "a part of it is empty or climbs";
        return parts[^1] == PackageFolder ? null : "it names no package folder";
    }

    /// <summary>The <c>package.json</c> beside a package folder, in git's spelling.</summary>
    internal static string PackageJsonOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "package.json" : $"{path[..slash]}/package.json";
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="child"/> lies below <paramref name="parent"/> by whole segments, compared without case.</summary>
    private static bool Under(string child, string parent) => child.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
}
