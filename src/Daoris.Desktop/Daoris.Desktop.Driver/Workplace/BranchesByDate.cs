using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A repository's local branches newest first, as the content proof's last comparison walks them: kept for as long as the refs the proof
/// counts (<see cref="CountedRefs"/>) stand as they were. The listing is the repository's whichever tree asks, and a session branch,
/// which a live session moves at every commit, is never a holder, so a tree that moved alone asks git nothing for it.
/// </summary>
internal static class BranchesByDate
{
    private static readonly ConcurrentDictionary<string, (string Version, (int Code, string Stdout, string Stderr) Listed)> Kept = new(StringComparer.OrdinalIgnoreCase);

    internal static string[] Ask => ["for-each-ref", "--sort=-committerdate", "--format=%(objectname) %(refname)", "refs/heads/"];

    internal static void Forget() => Kept.Clear();

    /// <summary>The listing from <paramref name="cwd"/>'s repository: the kept one where its counted refs are the same, else git's, kept where it answered.</summary>
    internal static async Task<(int Code, string Stdout, string Stderr)> ReadAsync(string cwd, CancellationToken ct)
    {
        var common = RefFiles.CommonDirOf(cwd);
        var version = common is null ? null : CountedRefs.Read(common)?.Version;
        if (common is null || version is null) return await WorkingTree.GitAsync(cwd, Ask, ct).ConfigureAwait(false);
        if (Kept.TryGetValue(common, out var kept) && kept.Version == version) return kept.Listed;
        var listed = await WorkingTree.GitAsync(cwd, Ask, ct).ConfigureAwait(false);
        // The refs are read again after the listing: a ref that moved while git listed is a version of its own, and nothing is kept for it.
        if (listed.Code == 0 && CountedRefs.Read(common)?.Version == version) Kept[common] = (version, listed);
        return listed;
    }
}
