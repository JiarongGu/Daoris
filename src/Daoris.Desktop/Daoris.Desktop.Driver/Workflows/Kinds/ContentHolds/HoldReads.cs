namespace Daoris.Driver;

/// <summary>What git says of a session's tree for <c>content.holds</c>, read as a landing door reads it.</summary>
public static class HoldReads
{
    /// <summary>
    /// What the line holds of <paramref name="tree"/>'s work at <paramref name="tip"/>: the head's sentence where the line or a branch of the person's
    /// holds it by content, and the unlanded-commit proof. Null where the tree does not stand at that commit, so nothing is read at another.
    /// </summary>
    public static async Task<HoldContent?> ContentAsync(SessionTrees trees, string tree, string tip, CancellationToken ct = default)
    {
        if (!string.Equals(TreeTips.Read(tree), tip, StringComparison.OrdinalIgnoreCase)) return null;
        // One read of the tree's work gives the head's sentence and the unlanded count, so the layout, the branch and the log are asked once.
        var work = await trees.WorkAsync(tree, ct).ConfigureAwait(false);
        var carried = work is null ? null : DiscardOffer.Of(tree, work);
        var unlanded = work?.Unlanded;
        var ahead = unlanded == 0 ? await trees.AheadOfLineAsync(tree, ct).ConfigureAwait(false) : unlanded;
        return new HoldContent(carried?.NotLanded, unlanded, ahead);
    }
}
