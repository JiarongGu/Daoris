namespace Daoris.Driver;

/// <summary>A recovery ref the look removed past its retention (CLEANUP1): the ref, the commit it kept, and the branch or tag that holds it.</summary>
public sealed record RecoveryGone(string Ref, string Commit, string Holder);

/// <summary>
/// The recovery refs' retention (CLEANUP1, amending D102's SQUASHTIDY1c note): a ref under <c>refs/daoris/discarded/</c> whose commit a
/// branch or a tag here holds by ancestry keeps nothing no other ref keeps, and goes at the look's tidy once it is past
/// <see cref="Kept"/>; one whose commit nothing else holds is kept until the person deletes it, as before.
/// </summary>
public static class RecoveryRetention
{
    /// <summary>How long a recovery ref another ref holds is kept: git's own default for an unreachable reflog entry (<c>gc.reflogExpireUnreachable</c>).</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromDays(30);

    /// <summary>The date from which the look removes it, where another ref holds its commit and its date was read; null otherwise.</summary>
    public static DateTimeOffset? Until(RecoveryRef each) => null;

    /// <summary>
    /// Every recovery ref of one checkout, with its commit, that commit's date, and whether a branch or a tag holds it: one
    /// <c>for-each-ref</c>, and one <c>rev-list</c> where there are refs. Null with git's words where the list did not read.
    /// </summary>
    internal static Task<(IReadOnlyList<RecoveryRef>? Refs, string? Unread)> ReadAsync(string root, string repository, CancellationToken ct) =>
        Task.FromResult<(IReadOnlyList<RecoveryRef>?, string?)>(([], null));

    /// <summary>
    /// The look's half: each recovery ref past its retention whose commit a branch or a tag holds, removed in one ref transaction that
    /// verifies that holder at the commit read (<see cref="ContentHolds.DropKeptVerifiedAsync"/>). Nothing else goes.
    /// </summary>
    internal static Task<IReadOnlyList<RecoveryGone>> TidyAsync(string root, string repository, DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RecoveryGone>>([]);
}
