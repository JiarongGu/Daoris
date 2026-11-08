namespace Daoris.Driver;

/// <summary>
/// Whether a commit tracks a submodule. A switch between commits that track different submodule commits leaves the submodule's checkout
/// where it stands, which no judgement can read as clean, so a repository that tracks one is never pooled.
/// </summary>
internal static class PoolSubmodules
{
    /// <summary>The path of a submodule the commit tracks; null where it tracks none. A commit git cannot read answers as if it tracked one.</summary>
    internal static async Task<string?> TrackedAsync(string root, string commit, CancellationToken ct)
    {
        var (code, listed, err) = await WorkingTree.GitAsync(root, ["ls-tree", "-r", "-z", commit], ct).ConfigureAwait(false);
        if (code != 0) return $"git could not read {commit}: {TreeGit.FirstLine(err)}";
        foreach (var entry in listed.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.StartsWith("160000 ", StringComparison.Ordinal)) return entry[(entry.IndexOf('\t') + 1)..];
        }

        return null;
    }
}
