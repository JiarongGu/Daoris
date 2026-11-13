namespace Daoris.Driver;

/// <summary>What the line holds of an ask's branch at a tip, read in its repository's own checkout since no session's tree stands behind the branch.</summary>
internal static class AskRunContent
{
    public static async Task<HoldContent?> ReadAsync(string home, WorkUnit branch, string tip, CancellationToken ct)
    {
        var (repository, workspace) = (branch.Owner.Repository ?? "", branch.Owner.Workspace ?? "");
        var root = GitFolders.Read(home).FirstOrDefault(checkout => string.Equals(checkout.Repository, repository, StringComparison.OrdinalIgnoreCase)).Root;
        if (root is null) return null;
        var line = await LineHolding.ReadInAsync(new SessionTrees(home).Context, root, repository, workspace, tip, ct).ConfigureAwait(false);
        return line is null ? null : new HoldContent(null, null, null) { Line = line };
    }
}
