namespace Daoris.Driver;

/// <summary>What a reservation came to: the row written, the session that holds the tree, or why it waits for the next pass.</summary>
/// <param name="Row">The row, written <see cref="TreeHandOffPhases.Reserved"/>; null where it wrote none.</param>
/// <param name="Holder">The session or step that holds the tree, which the hand-off does not take.</param>
/// <param name="Waits">Why it read and wrote nothing now; asked again at the next pass.</param>
public sealed record TreeReserved(TreeHandOff? Row, TreeHolder? Holder, string? Waits);

/// <summary>
/// A tree reserved for a hand-off, under the tree's landing hold and then the repository's lock alone: the holders read, the row written,
/// both let go, and only then is anything written in the tree.
/// </summary>
/// <remarks>
/// The lock alone is what a start holds shared from before it chooses its tree until its record is open, so whichever of a start and a
/// reservation comes first, the other sees: a start holding it leaves the reservation to the next pass, after which the holders name its
/// session; a reservation holding it leaves the start to its next look, where it reads the row inside its hold. The landing hold is let go
/// with the lock, since a hand-off lives across turns and restarts and a handle would be lost at a crash.
/// </remarks>
internal static class TreeReserving
{
    public static async Task<TreeReserved> ReserveAsync(string home, ServiceClient service, TreeHandOffs offs, TreeHandOff row, CancellationToken ct)
    {
        using var landing = await LandingHolds.TakeAsync(home, row.Tree, ct, TimeSpan.Zero).ConfigureAwait(false);
        if (landing is null) return new(null, null, $"a landing of `{Path.GetFileName(row.Tree)}` is under way");
        using var alone = TreeLock.TryWriting(home, row.Tree, row.Repository, HeldBy.Reserving);
        if (alone is null) return new(null, null, TreeLock.Held(row.Repository, TreeLock.HolderOf(home, row.Tree)));
        var holders = await TidyHolders.HoldersAsync(home, service, ct).ConfigureAwait(false);
        var key = TreeHandOffs.KeyOf(row.Tree);
        if (holders.FirstOrDefault(each => TreeHandOffs.KeyOf(each.Tree) == key) is { } holder) return new(null, holder, null);
        return offs.Write(row) is { } written ? new(written, null, null) : new(null, null, $"`{Path.GetFileName(row.Tree)}` is reserved already");
    }
}
