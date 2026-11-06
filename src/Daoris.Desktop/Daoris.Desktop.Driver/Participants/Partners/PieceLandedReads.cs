namespace Daoris.Driver;

/// <summary>
/// A piece read where its lead's tree is gone (COAGENT1b2, D199; the co-agent design §2.4): its lead's work landed and the tree was
/// tidied, or it was discarded. The piece's ref lives in the repository itself, so the repository's own checkout is what is read, named
/// by the registry since the tree's checkout can no longer be asked; and the lead's landing record names the commit its work landed at,
/// against which the frozen commit is read taken in or not.
/// </summary>
internal static class PieceLandedReads
{
    /// <summary>The checkout of <paramref name="repository"/> in <paramref name="workspace"/> as the registry names it, where it is on this machine; null where none is.</summary>
    public static async Task<string?> CheckoutAsync(
        Func<CancellationToken, Task<IReadOnlyList<RepoView>>> registry, string? repository, string? workspace, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repository)) return null;
        var space = RemoteTarget.Workspace(workspace);
        var root = (await registry(ct).ConfigureAwait(false)).FirstOrDefault(repo =>
            string.Equals(repo.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(repo.Workspace, space, StringComparison.OrdinalIgnoreCase))?.Root;
        return root is { Length: > 0 } && Directory.Exists(root) ? root : null;
    }

    /// <summary>
    /// The commit <paramref name="lead"/>'s work landed at, and whether <paramref name="commit"/> is its ancestor in <paramref name="root"/>. Null
    /// where its work has not landed, so a tree discarded with no landing leaves the piece where it stands, and where git cannot say.
    /// </summary>
    public static async Task<(string Tip, bool Ancestor)?> ReadAsync(LandedBranches landings, string lead, string root, string commit, CancellationToken ct)
    {
        if (landings.Landing(lead) is not { Tip: { Length: > 0 } tip }) return null;
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", commit, tip], ct).ConfigureAwait(false);
        return code switch
        {
            0 => (tip, true),
            1 => (tip, false),
            _ => null,
        };
    }
}
