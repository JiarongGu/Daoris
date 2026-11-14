namespace Daoris.Driver;

/// <summary>Whether a commit tracks a submodule, which a switch leaves where it stands, so such a repository is never pooled.</summary>
internal static class PoolSubmodules
{
    /// <summary>The path of a submodule the commit tracks; null where it tracks none. A commit git cannot read answers as if it tracked one.</summary>
    internal static async Task<string?> TrackedAsync(string root, string commit, CancellationToken ct)
    {
        if (await MachinePorts.Repository.EntriesAsync(root, commit, [], true, ct).ConfigureAwait(false) is not { } entries) return $"git could not read {commit}";
        return entries.FirstOrDefault(entry => entry.Mode == "160000")?.Path;
    }
}
