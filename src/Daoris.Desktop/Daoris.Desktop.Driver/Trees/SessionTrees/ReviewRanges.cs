using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// Where a session tree's review is measured from, and how it stands against its line (REVIEWBASE1; PART10j, out of
/// <c>SessionTrees.Sync.cs</c>, whose sync it is not): read-only, over the trees' context.
/// </summary>
internal sealed class ReviewRanges(TreeContext context)
{
    /// <summary>The review's base for a session tree (WSR6, REVIEWBASE1): <see cref="ReviewRangeAsync"/>'s commit.</summary>
    internal async Task<string> ReviewBaseAsync(string tree, string recorded, CancellationToken ct = default) =>
        (await ReviewRangeAsync(tree, recorded, ct).ConfigureAwait(false)).From;

    /// <summary>
    /// Where a session tree's review is measured from, and how its HEAD stands against its repository's line (REVIEWBASE1, D164's
    /// note): the newest commit the work shares with the line, among the line's here, the line's on origin and the record's own.
    /// </summary>
    /// <remarks>
    /// <para>The record's base is the commit the session began at, unless bringing the branch up to date replayed it since (WSR6):
    /// then it is no longer in the tree's history, and the branch's record says where it now grows from. That alone measured a tree
    /// that took in newer line commits (merged them, rebased onto them, or checked the line out) from its old grow point, so the
    /// line's own changes read as the session's work (seen 2026-10-13: 26 files, none its own). Each line's candidate is
    /// <c>merge-base HEAD &lt;ref&gt;</c>, so it is in the tree's history by construction; <see cref="ReviewBase.Newest"/> chooses.</para>
    ///
    /// <para><b>A chain's step that merged the line</b> (REVIEWBASE3): its record's base is its parent's tip, which the line does not
    /// hold, so neither base reaches the other. The line's is the base the review names, and the diff reads from a merge of the two
    /// (<see cref="ReviewRange.Against"/>), so the parent's work is not read as the step's either. Where they conflict, the line's
    /// alone, as before.</para>
    ///
    /// <para><b>Read-only, and it fetches nothing</b>: the refs are as the last look's fetch left them. A tree that is not this
    /// home's, or a line git cannot name, keeps the record's word and says no line, as before.</para>
    /// </remarks>
    internal async Task<ReviewRange> ReviewRangeAsync(string tree, string recorded, CancellationToken ct = default)
    {
        if (!context.Holds(tree) || !Directory.Exists(tree)) return new(recorded, null);
        var full = Path.GetFullPath(tree);
        var (workspace, repository) = context.OwnerOf(full);
        var record = await RecordedBaseAsync(full, repository, recorded, ct).ConfigureAwait(false);

        var (rootCode, commonDir, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)
            .ConfigureAwait(false);
        var line = rootCode == 0
            ? (await context.LineAsync(Path.GetDirectoryName(commonDir.Trim())!, repository, workspace, ct).ConfigureAwait(false)).Branch
            : null;

        // By the refs' full names, never the short one (AUTOTIDY1): git reads a tag of that name first.
        string? here = null, onOrigin = null;
        var known = new List<string>();
        if (line is not null)
        {
            foreach (var (reference, isOrigin) in new[] { ($"refs/heads/{line}", false), ($"refs/remotes/origin/{line}", true) })
            {
                var (code, shared, _) = await WorkingTree.GitAsync(full, ["merge-base", "HEAD", reference], ct).ConfigureAwait(false);
                if (ReviewChoices.Shared(code, shared) is not { } found) continue;
                known.Add(reference);
                if (isOrigin) onOrigin = found;
                else here = found;
            }
        }

        // GITAPP1a-ii: the selection is ReviewChoices.Choose, which Review's graph calls on the same answers.
        var candidates = ReviewBase.Candidates(here, onOrigin, record);
        var maximal = candidates.Count < 2 ? null : await IndependentAsync(full, candidates, ct).ConfigureAwait(false);
        var (from, merge) = ReviewChoices.Choose(candidates, maximal, record, recorded);
        // REVIEWBASE3: one more git start, only where the line's base and the record's are incomparable.
        var against = merge is var (lineBase, recordBase) ? await MergedAsync(full, lineBase, recordBase, ct).ConfigureAwait(false) : null;

        return new(from, line is null ? null : await BehindAsync(full, line, known, ct).ConfigureAwait(false), against);
    }

    /// <summary>The line's two refs, by their full names (AUTOTIDY1), in the order the candidates are read: here, then origin's.</summary>
    internal static string[] LineRefs(string line) => [$"refs/heads/{line}", $"refs/remotes/origin/{line}"];

    /// <summary>
    /// The tree a merge of <paramref name="lineBase"/> and <paramref name="recordBase"/> would make (REVIEWBASE3), by
    /// <c>git merge-tree --write-tree</c>: objects only, no ref, no index and nothing on disk moved. Null where they conflict, since
    /// git's tree would then hold conflict markers neither side wrote, and where git cannot say (one older than 2.38): the review
    /// then reads from the line's base, as before.
    /// </summary>
    private static async Task<string?> MergedAsync(string tree, string lineBase, string recordBase, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(tree, ["merge-tree", "--write-tree", lineBase, recordBase], ct).ConfigureAwait(false);
        return ReviewChoices.Merged(code, output);
    }

    /// <summary>
    /// The record's base where the tree's history holds it; else where the branch's record says it now grows from (WSR6), where
    /// that is; else null. Each word becomes a git argument, so it must be a commit id and nothing else.
    /// </summary>
    private async Task<string?> RecordedBaseAsync(string tree, string repository, string recorded, CancellationToken ct)
    {
        bool? held = ReviewChoices.Id(recorded) ? await IsAncestorAsync(tree, recorded, "HEAD", ct).ConfigureAwait(false) : null;
        if (ReviewChoices.RecordedBase(recorded, held, null, null) is { } record) return record;

        var (_, branchOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        if (ReviewChoices.Grown(context.Grown, repository, branchOut) is not { } grown) return null;
        return ReviewChoices.RecordedBase(recorded, held, grown, await IsAncestorAsync(tree, grown, "HEAD", ct).ConfigureAwait(false));
    }

    /// <summary>Those of <paramref name="commits"/> no other of them reaches, as git names them (full ids); none where git cannot say.</summary>
    private static async Task<IReadOnlyList<string>> IndependentAsync(string tree, IReadOnlyList<string> commits, CancellationToken ct)
    {
        var (code, named, _) = await WorkingTree.GitAsync(tree, ["merge-base", "--independent", .. commits], ct).ConfigureAwait(false);
        return ReviewChoices.Independent(code, named);
    }

    /// <summary>
    /// How many commits the line here and on origin together hold that HEAD does not, and HEAD that they do not (REVIEWBASE1): the
    /// newer of the two counts, and a line that moved both ways counts both. Null where neither ref is here or git cannot count.
    /// </summary>
    private static async Task<LineBehind?> BehindAsync(string tree, string line, IReadOnlyList<string> refs, CancellationToken ct)
    {
        if (refs.Count == 0) return null;
        var (behindCode, behind, _) = await WorkingTree.GitAsync(tree, ["rev-list", "--count", .. refs, "--not", "HEAD"], ct).ConfigureAwait(false);
        var (aheadCode, ahead, _) = await WorkingTree.GitAsync(tree, ["rev-list", "--count", "HEAD", "--not", .. refs], ct).ConfigureAwait(false);
        return ReviewChoices.Behind(line, behindCode, behind, aheadCode, ahead);
    }
}
