namespace Daoris.Driver;

/// <summary>What git says of a session's tree for <c>content.holds</c> (LANDNODE1k), read as a landing door reads it.</summary>
public static class HoldReads
{
    /// <summary>
    /// What the line holds of <paramref name="tree"/>'s work at <paramref name="tip"/>: the head's sentence where the line or a branch of the person's
    /// holds it by content, and D88's proof. Null where the tree does not stand at that commit, so nothing is read at another.
    /// </summary>
    public static async Task<HoldContent?> ContentAsync(SessionTrees trees, string tree, string tip, CancellationToken ct = default)
    {
        if (!string.Equals(TreeTips.Read(tree), tip, StringComparison.OrdinalIgnoreCase)) return null;
        var carried = await trees.DiscardOfferAsync(tree, ct).ConfigureAwait(false);
        var unlanded = await trees.UnlandedHereAsync(tree, ct).ConfigureAwait(false);
        var ahead = unlanded == 0 ? await trees.AheadOfLineAsync(tree, ct).ConfigureAwait(false) : unlanded;
        return new HoldContent(carried?.NotLanded, unlanded, ahead);
    }
}
