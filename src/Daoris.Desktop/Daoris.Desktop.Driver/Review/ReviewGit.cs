namespace Daoris.Driver;

/// <summary>What the review asks git of a tree: its <c>HEAD</c>, and whether a commit holds another.</summary>
internal static class ReviewGit
{
    /// <summary>A set-up step's tree's <c>HEAD</c> as its post reads it; null where git gives none.</summary>
    public static Task<string?> TreeHeadAsync(string tree, CancellationToken ct) => WorkingTree.HeadAsync(tree, ct);

    /// <summary>The tree's <c>HEAD</c>, by its full id; null where git cannot say.</summary>
    public static async Task<string?> HeadAsync(string tree, CancellationToken ct) =>
        Directory.Exists(tree) ? await MachinePorts.Repository.HeadAsync(tree, ct).ConfigureAwait(false) : null;

    /// <summary>Whether <paramref name="tip"/> is <paramref name="commit"/> or an ancestor of it; null where git cannot say.</summary>
    public static Task<bool?> HoldsAsync(string tree, string tip, string commit, CancellationToken ct) =>
        EvidenceCodes.IsObjectId(commit) ? MachinePorts.Repository.IsAncestorAsync(tree, tip, commit, ct) : Task.FromResult<bool?>(null);
}
