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
        if (await MachinePorts.Repository.EntriesAsync(root, commit, [], true, ct).ConfigureAwait(false) is not { } entries) return $"git could not read {commit}";
        return entries.FirstOrDefault(entry => entry.Mode == "160000")?.Path;
    }
}
