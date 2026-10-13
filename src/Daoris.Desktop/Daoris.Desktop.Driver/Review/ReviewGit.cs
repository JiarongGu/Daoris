namespace Daoris.Driver;

/// <summary>
/// What the review asks git of a tree (REVIEWENV1c, design §2.6, §3.2): its <c>HEAD</c>, and whether a commit holds another. Review's
/// Infrastructure (STRUCT3c, D166's note), reached through <see cref="ReviewGateReads"/>' <c>ReviewGate.HeadAsync</c> and
/// <c>HoldsAsync</c>, and the set-up's post (<see cref="ReviewSetUpPosts"/>).
/// </summary>
internal static class ReviewGit
{
    /// <summary>A set-up step's tree's <c>HEAD</c> as its post reads it (design §2.6), git's own answer; null where it gives none.</summary>
    public static Task<string?> TreeHeadAsync(string tree, CancellationToken ct) => WorkingTree.HeadAsync(tree, ct);

    /// <summary>The tree's <c>HEAD</c>, by its full id; null where git cannot say.</summary>
    public static async Task<string?> HeadAsync(string tree, CancellationToken ct)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, head, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", "HEAD"], ct).ConfigureAwait(false);
        return code == 0 && head.Trim() is { Length: > 0 } sha ? sha : null;
    }

    /// <summary>Whether <paramref name="tip"/> is <paramref name="commit"/> or an ancestor of it; null where git cannot say.</summary>
    public static async Task<bool?> HoldsAsync(string tree, string tip, string commit, CancellationToken ct)
    {
        if (!EvidenceCodes.IsObjectId(commit)) return null;
        var (code, _, _) = await WorkingTree.GitAsync(tree, ["merge-base", "--is-ancestor", tip, commit], ct).ConfigureAwait(false);
        return code switch { 0 => true, 1 => false, _ => null };
    }
}
