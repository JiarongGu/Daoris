using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// What the content proof found: the work held (<see cref="Hold"/>), not held, or unread (<see cref="Unread"/>, git's
/// failure in a clause) where a read it needs failed. Unread is never held and never "not held": a caller keeps what it would keep
/// for either, and says it could not read.
/// </summary>
/// <param name="Hold">Where the work is held; null where it is not, or where <paramref name="Unread"/> says git could not tell.</param>
/// <param name="Unread">Why git could not tell, or null where the proof was read through.</param>
internal sealed record ContentProof(ContentHold? Hold, string? Unread)
{
    /// <summary>The proof read through and found nothing holding the work.</summary>
    public static readonly ContentProof NotHeld = new(null, null);

    /// <summary>
    /// The ref the work is found held on, at the commit the proof compared: what a removal by this proof rests
    /// on, and what its delete verifies. Null where the work is not held.
    /// </summary>
    internal (string Ref, string Commit)? At { get; init; }

    /// <summary>
    /// The commits the line lacks, as a landing reads them (<see cref="ReplayHolds"/>), where the work is not held and the line
    /// holds some of its commits under ids of their own: what a session told to rebase after its landing still has to land. Null where that
    /// reading was not asked (the line has not moved since the branch left it) or lacks none of them.
    /// </summary>
    internal IReadOnlyList<LandingCommit>? Lacks { get; init; }

    /// <summary>
    /// The commits a refusal names as not taken, one a line: those the line lacks where the reading found them, else
    /// <paramref name="unlanded"/>, the ones ancestry counted, as git's <c>--oneline</c> listed them.
    /// </summary>
    internal string Lacking(string unlanded) => Lacks is { Count: > 0 } lacks
        ? string.Join('\n', lacks.Select(each => $"{TextBounds.ShortCommit(each.Sha, 7)} {each.Subject}"))
        : unlanded.Trim();

    /// <summary>What a removal by it rests on: <see cref="At"/>, or nothing.</summary>
    internal IReadOnlyList<(string Ref, string Commit)> Rests => At is { } at ? [at] : [];

    /// <summary>The work is held, on <paramref name="reference"/> at <paramref name="commit"/>.</summary>
    public static ContentProof Of(ContentHold hold, string reference, string commit) => new(hold, null) { At = (reference, commit) };

    /// <summary>A read failed, and nothing found the work held.</summary>
    public static ContentProof UnreadBy(string why) => new(null, why);
}
