namespace Daoris.Driver;

/// <summary>A recovery ref the person asked to delete, with the commit they were shown it at (RECOVERYDROP1); null where the terminal named it alone.</summary>
public sealed record RecoveryDrop(string Ref, string? Commit);

/// <summary>A recovery ref that was deleted, and the commit it named.</summary>
public sealed record RecoveryDropped(string Ref, string Commit);

/// <summary>A recovery ref that was kept, and why, in a sentence both doors say.</summary>
public sealed record RecoverySkipped(string Ref, string Why);

/// <summary>What a press deleted and what it skipped.</summary>
public sealed record RecoveryDrops(IReadOnlyList<RecoveryDropped> Dropped, IReadOnlyList<RecoverySkipped> Skipped);

/// <summary>
/// The person's press to delete the recovery refs nothing holds (RECOVERYDROP1, amending D102's CLEANUP1 note): the one way past
/// CLEANUP1's rule that a recovery ref whose commit no branch or tag of the person's holds is kept at any age. One act, two doors
/// (D50): the screen's <c>RECOVERY_DROP</c> and the terminal's <c>daoris-driver work recovery drop</c>.
/// </summary>
public static class RecoveryDropping
{
    /// <summary>The sentence a press refuses with where this process does not hold the home's lease; null where it does.</summary>
    public static string? WithoutHome(string home) => throw new NotImplementedException();

    /// <summary>
    /// Delete each asked ref that nothing of the person's holds at this moment, each in one <c>update-ref --stdin</c> transaction that
    /// verifies the ref still names the commit it was shown at. A ref that moved, one a branch or a tag now holds, one whose holders
    /// were not read, one gone already and one outside <c>refs/daoris/discarded/</c> is skipped and said.
    /// </summary>
    public static Task<RecoveryDrops> DropAsync(
        string root, string repository, IReadOnlyCollection<string> landed, IReadOnlyList<RecoveryDrop> asked, CancellationToken ct) =>
        throw new NotImplementedException();

    /// <summary>Every recovery ref nothing holds, read now and deleted at the commit read.</summary>
    public static Task<RecoveryDrops> DropUnheldAsync(string root, string repository, IReadOnlyCollection<string> landed, CancellationToken ct) =>
        throw new NotImplementedException();

    /// <summary>The words of an answer, a line each: what was deleted and what was skipped and why.</summary>
    public static IEnumerable<string> Words(string repository, RecoveryDrops drops) => throw new NotImplementedException();
}
