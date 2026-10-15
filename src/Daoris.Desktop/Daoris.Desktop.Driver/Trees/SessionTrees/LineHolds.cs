namespace Daoris.Driver;

/// <summary>
/// What the line holds of a tree's commits beyond where it left the line (LANDSYNC1, D87's 2026-10-15 amendment point 1), and what
/// it lacks: the one judgement a landing makes per commit, which an entry's Accept (ACCEPTALL1), a session going on in an old tree
/// (LANDSYNC2) and <i>Bring up to date</i> (LANDSYNC3) ask as well.
/// </summary>
/// <param name="Onto">The line's commit the tree was judged against.</param>
/// <param name="Leaves">Where the tree left the line: the newest commit it shares with <paramref name="Onto"/>.</param>
/// <param name="Commits">Every commit beyond the line, oldest first, merges aside.</param>
/// <param name="Kept">What the line lacks, oldest first: what a landing replays.</param>
/// <param name="LeftOut">What the line holds, oldest first, and why.</param>
internal sealed record LineHeld(
    string Onto, string Leaves, IReadOnlyList<LandingCommit> Commits, IReadOnlyList<LandingCommit> Kept, IReadOnlyList<LeftOutCommit> LeftOut)
{
    /// <summary>The line holds none of the tree's commits: the landing makes its branch from the session's as made (D87).</summary>
    public bool HoldsNone => LeftOut.Count == 0;
}

/// <summary>
/// The per-commit judgement of what the line holds (LANDSYNC1): first WSR6's walk by content (D109 §2), which reads a squash of
/// several commits; then, beyond the run it finds, each commit whose patch id a commit the line gained since has, which finds a
/// one-commit squash or a cherry-pick even where the line has changed the same files since. What neither finds stays in, until a
/// replay finds it empty. Patch id judges single commits only (D109's 2026-10-15 note): whether a whole tree is on the line stays
/// SQUASHTIDY1f's proof, asked first.
/// </summary>
/// <remarks>It reads git and writes nothing; the trial (<see cref="TrialAsync"/>) writes objects only, never a ref or a tree.</remarks>
internal static class LineHolds
{
    /// <summary>
    /// Judge <paramref name="tip"/>'s commits beyond <paramref name="onto"/> (a commit): what the line holds and what it lacks. Null
    /// where git cannot say where the tree left the line, or list its commits.
    /// </summary>
    internal static async Task<LineHeld?> JudgeAsync(string root, string tip, string onto, string? line, CancellationToken ct)
    {
        var (baseCode, baseOut, _) = await WorkingTree.GitAsync(root, ["merge-base", tip, onto], ct).ConfigureAwait(false);
        if (baseCode != 0) return null;
        var leaves = baseOut.Trim();
        if (await CommitsAsync(root, $"{onto}..{tip}", ct).ConfigureAwait(false) is not { } commits) return null;
        // The line gained nothing since the tree left it, so it can hold none of the tree's commits.
        if (leaves == onto || commits.Count == 0) return new(onto, leaves, commits, commits, []);

        var left = new Dictionary<string, LeftOutCommit>(StringComparer.Ordinal);
        // (a) The run from where it left the line whose work reads there by content: a squash of several.
        var (cut, by) = await BranchJudging.CutByContentAsync(root, tip, leaves, line, onto, ct).ConfigureAwait(false);
        if (by == CutBy.Content)
        {
            foreach (var held in await CommitsAsync(root, $"{onto}..{cut}", ct).ConfigureAwait(false) ?? [])
            {
                left[held.Sha] = new LeftOutCommit(held.Sha, held.Subject, LeftOutWhy.Content);
            }
        }

        // (b) Beyond it, each commit a commit the line gained since has the patch of: `git cherry` marks it `-`.
        var (cherryCode, cherry, _) = await WorkingTree.GitAsync(root, ["cherry", onto, tip, cut], ct).ConfigureAwait(false);
        var same = cherryCode != 0 ? [] : cherry.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(each => each.StartsWith("- ", StringComparison.Ordinal))
            .Select(each => each[2..].Trim())
            .ToList();
        if (same.Count > 0)
        {
            var candidates = await SameOnLineAsync(root, onto, tip, ct).ConfigureAwait(false);
            foreach (var sha in same)
            {
                var commit = commits.FirstOrDefault(each => each.Sha == sha);
                if (commit is null || left.ContainsKey(sha)) continue;
                left[sha] = new LeftOutCommit(sha, commit.Subject, LeftOutWhy.Patch, await PairAsync(root, sha, candidates, ct).ConfigureAwait(false));
            }
        }

        return new(onto, leaves, commits,
            [.. commits.Where(each => !left.ContainsKey(each.Sha))],
            [.. commits.Where(each => left.ContainsKey(each.Sha)).Select(each => left[each.Sha])]);
    }

    /// <summary>
    /// Whether replaying <paramref name="kept"/> onto <paramref name="onto"/> in order would conflict, tried with
    /// <c>git merge-tree --write-tree</c> over trees alone, each commit merged with its parent as the base: git's <c>CONFLICT</c>
    /// lines where it would, else null. A git too old to try, or one that cannot say, says nothing: the press still finds it.
    /// </summary>
    /// <remarks>Objects only: it writes no ref, no tree on disk and no index, so the plan stays a look.</remarks>
    internal static async Task<string?> TrialAsync(string root, string onto, IReadOnlyList<LandingCommit> kept, CancellationToken ct) =>
        (await TrialTreeAsync(root, onto, kept, ct).ConfigureAwait(false)).Conflicts;

    /// <summary>
    /// <see cref="TrialAsync"/>'s trial, with the tree it ends at where it replays cleanly: what an entry's Accept compares to read a
    /// part that adds nothing after a later part's (ACCEPTALL1, D167's amendment point 3). <paramref name="onto"/> may be a commit
    /// or a tree. Both null where git cannot say.
    /// </summary>
    internal static async Task<(string? Tree, string? Conflicts)> TrialTreeAsync(
        string root, string onto, IReadOnlyList<LandingCommit> kept, CancellationToken ct)
    {
        var current = onto;
        foreach (var commit in kept)
        {
            var (code, output, _) = await WorkingTree.GitAsync(
                root, ["merge-tree", "--write-tree", "--name-only", "--messages", $"--merge-base={commit.Sha}^", current, commit.Sha], ct)
                .ConfigureAwait(false);
            var lines = output.Split('\n', StringSplitOptions.TrimEntries);
            if (code == 1)
            {
                var conflicts = lines.Where(each => each.StartsWith("CONFLICT", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
                return (null, conflicts.Count == 0 ? null : Joined(conflicts));
            }

            if (code != 0 || lines.Length == 0 || lines[0].Length == 0) return (null, null);
            current = lines[0];
        }

        // The tree the last step wrote, or the start's own where nothing was replayed, so two trials compare as trees.
        if (kept.Count > 0) return (current, null);
        var (treeCode, tree, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{onto}^{{tree}}"], ct).ConfigureAwait(false);
        return (treeCode == 0 ? tree.Trim() : null, null);
    }

    /// <summary>Up to three of git's <c>CONFLICT</c> lines, as <see cref="BranchReplays.RebaseInAsync"/> says them.</summary>
    internal static string Joined(IReadOnlyList<string> conflicts) =>
        string.Join("; ", conflicts.Take(3)) + (conflicts.Count > 3 ? $"; and {conflicts.Count - 3} more." : ".");

    /// <summary>The commits of a range, oldest first, merges aside, with their subjects; null where git could not list them.</summary>
    internal static async Task<IReadOnlyList<LandingCommit>?> CommitsAsync(string root, string range, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(root, ["log", "--reverse", "--no-merges", "--format=%H%x09%s", range], ct).ConfigureAwait(false);
        if (code != 0) return null;
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split('\t', 2))
            .Select(parts => new LandingCommit(parts[0], parts.Length > 1 ? parts[1] : ""))];
    }

    /// <summary>The line's commits since the tree left it that some commit of the tree's has the patch of, oldest first.</summary>
    private static async Task<IReadOnlyList<string>> SameOnLineAsync(string root, string onto, string tip, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(
            root, ["log", "--cherry-mark", "--left-only", "--no-merges", "--reverse", "--format=%m%H", $"{onto}...{tip}"], ct).ConfigureAwait(false);
        return code != 0 ? [] : [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(each => each.StartsWith('='))
            .Select(each => each[1..])];
    }

    /// <summary>
    /// The line's commit with <paramref name="sha"/>'s patch: the oldest candidate whose history, from where the two part, holds it
    /// (<c>git cherry &lt;candidate&gt; &lt;sha&gt; &lt;sha&gt;^</c> marks it <c>-</c>). Null where none is found.
    /// </summary>
    private static async Task<string?> PairAsync(string root, string sha, IReadOnlyList<string> candidates, CancellationToken ct)
    {
        if (candidates.Count == 1) return candidates[0];
        foreach (var candidate in candidates)
        {
            var (code, output, _) = await WorkingTree.GitAsync(root, ["cherry", candidate, sha, $"{sha}^"], ct).ConfigureAwait(false);
            if (code == 0 && output.TrimStart().StartsWith('-')) return candidate;
        }

        return null;
    }
}
