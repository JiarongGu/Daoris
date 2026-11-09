using LibGit2Sharp;

namespace Daoris.Driver;

/// <summary>
/// The commit graph's questions in one library call, each answered as git's <c>merge-base</c> and <c>rev-list --count</c> answer it:
/// two commits' best common ancestor, whether one is the other's ancestor, the commits no other of a set holds, and the commits
/// reachable from some tips and none of others. Null where git must answer.
/// </summary>
/// <remarks>
/// <para><b>One best base, or git's choice.</b> Where two commits share more than one best common ancestor (a criss-cross), git and
/// libgit2 each pick one by their own order, which a second's tie between the bases' dates decides; that question is git's. Another
/// best base exists exactly where a commit both reach is not reachable from the one found.</para>
/// <para>Shallow boundaries are read as git reads them, from the repository's <c>shallow</c> file at each call.</para>
/// </remarks>
internal static class GitLibraryWalks
{
    /// <summary><c>merge-base A B</c>: the base and exit 0, nothing and exit 1 where they share none, null where there are several.</summary>
    internal static GitLibraryAnswer? MergeBase(GitLibraryCall call, Commit one, Commit other)
    {
        if (call.Repository.ObjectDatabase.FindMergeBase(one, other) is not { } found) return new(1, "");
        var mine = Reach(call, [one], [found]).Select(commit => commit.Sha).ToHashSet(StringComparer.Ordinal);
        if (mine.Count > 0 && Reach(call, [other], [found]).Any(commit => mine.Contains(commit.Sha))) return null;
        return new(0, found.Sha + "\n");
    }

    /// <summary><c>merge-base --is-ancestor A B</c>: exit 0 where A is B or one of its ancestors, else 1.</summary>
    internal static GitLibraryAnswer Ancestry(GitLibraryCall call, Commit ancestor, Commit of) => new(Descends(call, ancestor, of) ? 0 : 1, "");

    /// <summary><c>merge-base --independent</c>: each commit no other of them reaches, once, in the order asked.</summary>
    internal static GitLibraryAnswer Independent(GitLibraryCall call, IReadOnlyList<Commit> commits)
    {
        var distinct = commits.DistinctBy(commit => commit.Sha).ToList();
        var kept = distinct.Where(commit => !distinct.Any(other => other.Sha != commit.Sha && Descends(call, commit, other)));
        return new(0, string.Concat(kept.Select(commit => commit.Sha + "\n")));
    }

    /// <summary><c>rev-list --count</c>: the commits reachable from <paramref name="tips"/> and from none of <paramref name="not"/>.</summary>
    internal static GitLibraryAnswer Count(GitLibraryCall call, IReadOnlyList<Commit> tips, IReadOnlyList<Commit> not) =>
        new(0, $"{Reach(call, tips, not).Count()}\n");

    /// <summary><c>rev-list --left-right --count A...B</c>: the commits only the left reaches, a tab, those only the right reaches.</summary>
    internal static GitLibraryAnswer Sides(GitLibraryCall call, Commit left, Commit right) =>
        new(0, $"{Reach(call, [left], [right]).Count()}\t{Reach(call, [right], [left]).Count()}\n");

    private static bool Descends(GitLibraryCall call, Commit ancestor, Commit of) =>
        ancestor.Sha == of.Sha || call.Repository.ObjectDatabase.FindMergeBase(ancestor, of)?.Sha == ancestor.Sha;

    private static IEnumerable<Commit> Reach(GitLibraryCall call, IReadOnlyList<Commit> from, IReadOnlyList<Commit> not) =>
        call.Repository.Commits.QueryBy(new CommitFilter
        {
            IncludeReachableFrom = from,
            ExcludeReachableFrom = not,
            SortBy = CommitSortStrategies.None,
        });
}
