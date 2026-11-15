namespace Daoris.Driver;

/// <summary>
/// What a <c>push</c> does where its branch no longer stands (the person merged it through the platform, which deleted it): nothing is pushed, and
/// the line's remote is read once instead, through the one fetch of a line (<see cref="TreeRefs.FetchAsync"/>, the workspace's own access, the
/// git policy that governs every fetch). Where the line then holds the landing's recorded tip, by ancestry or as the same change (a squash merge),
/// its proof is written on the landing (<see cref="LandedBranches.Delivered"/>) and the next look passes the push over as delivered
/// (<see cref="NothingToDeliver"/>); where it does not, or the remote could not be read, the answer is a sentence the person can act on.
/// </summary>
internal static class PushLineRead
{
    /// <summary>The code a push fails with where its branch is gone and the line does not hold its work.</summary>
    public const string BranchGone = "push-branch-gone";

    /// <summary>The code a push fails with where its branch is gone and the line's remote could not be read.</summary>
    public const string LineUnread = "push-line-unread";

    private static readonly IReadOnlySet<string> NoHolders = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>What the read came to: the line holds the work (<see cref="Code"/> null), or a failure's code and its sentence.</summary>
    internal sealed record Answer(string? Code, string Said)
    {
        public bool Held => Code is null;
    }

    /// <summary>The answer for <paramref name="start"/>, or null where its branch stands and the push goes on.</summary>
    public static async Task<Answer?> ReadAsync(string home, PushStart start, Func<DateTimeOffset> now, CancellationToken ct)
    {
        if (await TreeRefs.CommitOfAsync(start.Root, $"refs/heads/{start.Branch}", ct).ConfigureAwait(false) is not null) return null;

        var landed = new LandedBranches(home);
        var entry = landed.Entries().LastOrDefault(each => string.Equals(each.Repository, start.Repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(each.Branch, start.Branch, StringComparison.Ordinal) && string.Equals(each.Tip, start.Tip, StringComparison.OrdinalIgnoreCase));
        if (entry?.Line is not { } line)
        {
            return new(BranchGone, $"`{start.Branch}` no longer stands and its landing names no line to read, so nothing was pushed: skip this step once the work is known to be delivered.");
        }

        if (PullRequestReading.OnTheLine(entry) is not null) return new(null, $"`{start.Branch}` is on `{line}` already.");

        var (failed, _) = await TreeRefs.FetchAsync(home, start.Root, line, ct, start.Repository, start.Workspace).ConfigureAwait(false);
        if (failed is not null)
        {
            return new(LineUnread, $"`{start.Branch}` no longer stands and origin's `{line}` could not be read ({failed.TrimEnd().TrimEnd('.')}), so whether it holds the work is not known: "
                + $"fetch origin's `{line}` once it can be reached, or skip this step if the work is delivered.");
        }

        var refs = RefSnapshot.Take(start.Root);
        var subject = new EndSubject(entry.Branch, entry.Tip, EndRole.Landing, entry) { Gone = true };
        var verdict = await EndJudgement.JudgeAsync(start.Root, EndCycle.Recorded(entry), subject, line, NoHolders, null, ct, refs).ConfigureAwait(false);
        if (verdict is { Ended: true, Where: { } where } && EndKeeping.RepositoryIdentity(start.Root) is { } identity)
        {
            var forms = await LandedProofs.LineFormsAsync(start.Root, line, ct, refs).ConfigureAwait(false);
            var on = forms.FirstOrDefault(form => string.Equals(form.Name, where, StringComparison.Ordinal))?.Ref;
            var onAt = on is null ? null : await TreeRefs.CommitOfAsync(start.Root, on, ct, refs).ConfigureAwait(false);
            if (EndKeeping.Next(null, verdict, onAt, identity, now()) is { } held)
            {
                landed.Delivered(entry, held);
                return new(null, $"`{start.Branch}` no longer stands, and `{where}` holds its work ({WorkInventory.ProofSaid(held.By, where)}), so nothing is pushed.");
            }
        }

        return new(BranchGone, $"`{start.Branch}` no longer stands and origin's `{line}` does not hold its work, so nothing was pushed: restore the branch, or skip this step if the work is delivered.");
    }
}
