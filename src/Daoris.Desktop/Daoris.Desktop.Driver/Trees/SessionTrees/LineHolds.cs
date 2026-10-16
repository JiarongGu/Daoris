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

/// <summary>One commit beyond a branch's cut as the look's one log reads it (LANDSYNC3): whether the line has its patch, and its files.</summary>
internal sealed record BeyondCommit(string Sha, bool Held, IReadOnlyList<string> Files);

/// <summary>
/// A branch's commits beyond its cut as <i>Bring up to date</i> judges them (LANDSYNC3, D87's 2026-10-15 amendment point 1): what the
/// line holds is left out, so the count is what the replay carries.
/// </summary>
/// <param name="Cut">
/// Where its own commits start after: moved past the commits it begins with that the line holds, so the replay is a plain one from
/// there; the last of them where the line holds every one.
/// </param>
/// <param name="Commits">The commits beyond <paramref name="Cut"/>, oldest first, merges aside.</param>
/// <param name="Skips">Among them, those the line holds: the replay skips each, replaying the runs between.</param>
/// <param name="LeftOut">Every commit the line holds by patch beyond the cut WSR6 made, the ones the cut moved past and the skips.</param>
/// <param name="ByContent">
/// How many commits the walk by content moved the cut past, where it did: the run up to it reads on the line, a squash of several.
/// WSR6's words say that run as the cut (<see cref="CutBy.Content"/>), not as commits left out.
/// </param>
internal sealed record HeldBeyond(string Cut, IReadOnlyList<string> Commits, IReadOnlyList<string> Skips, int LeftOut, int ByContent = 0)
{
    /// <summary>The commits the replay carries.</summary>
    public int Kept => Commits.Count - Skips.Count;

    /// <summary>The line holds some of the commits beyond the cut WSR6 made, by patch or by content (LANDSYNC2's question).</summary>
    public bool HoldsAny => LeftOut + ByContent > 0;
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
        // STARTGIT1: a tree still at the commit it is judged against (a session's just opened) left the line where the line stands and
        // has no commits beyond it, as `merge-base` and `log` would answer; neither is asked. Both are full commit ids.
        if (tip.Length == 40 && string.Equals(tip, onto, StringComparison.OrdinalIgnoreCase)) return new(onto, tip, [], [], []);

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
        kept.Count == 0 ? null : (await TrialTreeAsync(root, onto, kept, ct).ConfigureAwait(false)).Conflicts;

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

    /// <summary>
    /// <i>Bring up to date</i>'s judgement of a branch's commits beyond <paramref name="cut"/> (LANDSYNC3, D87's amendment point 1): (b)'s
    /// patch ids and the commits' files in one log, then, where <paramref name="walk"/> and the line changed every file the first commit
    /// it lacks touched, (a)'s walk by content, which proves commit after commit and so is asked only where it can find a squash. Null
    /// where git could not list the commits.
    /// </summary>
    /// <remarks>
    /// The look's own form of <see cref="JudgeAsync"/>, held to its git starts (UIFREEZE1, PROC1d): the one log takes the place of the
    /// count WSR6 made, and the line's files are read only where a commit could be the line's by content.
    /// </remarks>
    /// <param name="leaves">Where the branch leaves the line: the newest commit it shares with <paramref name="onto"/>.</param>
    /// <param name="cut">Where its own commits start after, as WSR6 cut it: <paramref name="leaves"/>, or a commit beyond it.</param>
    /// <param name="walk">Whether the walk by content may move the cut: not where a record or the walk already cut it.</param>
    internal static async Task<HeldBeyond?> JudgeBeyondAsync(
        string root, string tip, string onto, string leaves, string cut, string? line, bool walk, CancellationToken ct)
    {
        if (await ReadBeyondAsync(root, tip, onto, cut == leaves ? null : cut, ct).ConfigureAwait(false) is not { } beyond) return null;
        if (walk && beyond.Any(each => !each.Held)
            && await LandedProofs.NamesAsync(root, leaves, onto, ct).ConfigureAwait(false) is { } brought
            && MayHoldByContent(beyond, brought))
        {
            var (byContent, by) = await BranchJudging.CutByContentAsync(root, tip, leaves, line, onto, ct).ConfigureAwait(false);
            if (by == CutBy.Content && await ReadBeyondAsync(root, tip, onto, byContent, ct).ConfigureAwait(false) is { } after)
            {
                return Beyond(byContent, after) with { ByContent = beyond.Count - after.Count };
            }
        }

        return Beyond(cut, beyond);
    }

    /// <summary>
    /// The commits beyond <paramref name="cut"/> (where the branch leaves <paramref name="onto"/>, for null), oldest first, merges
    /// aside, each marked where a commit the line gained since has its patch, with the files it touched: one start. Null where git
    /// could not list them.
    /// </summary>
    internal static async Task<IReadOnlyList<BeyondCommit>?> ReadBeyondAsync(string root, string tip, string onto, string? cut, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(
            root,
            [
                "-c", "core.quotePath=false", "log", "--reverse", "--topo-order", "--cherry-mark", "--right-only", "--no-merges",
                "--format=%x1e%m%H", "--name-only", $"{onto}...{tip}", .. cut is null ? Array.Empty<string>() : [$"^{cut}"],
            ],
            ct).ConfigureAwait(false);
        return code != 0 ? null : ReadBeyond(output);
    }

    /// <summary><see cref="ReadBeyondAsync"/>'s log read: each commit after its separator, <c>=</c> where the line holds its patch, then its files.</summary>
    internal static IReadOnlyList<BeyondCommit> ReadBeyond(string log) =>
    [
        .. log.Split('\u001e', StringSplitOptions.RemoveEmptyEntries)
            .Select(each => each.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(lines => lines.Length > 0 && lines[0].Length > 1)
            .Select(lines => new BeyondCommit(lines[0][1..], lines[0][0] == '=', lines[1..])),
    ];

    /// <summary>
    /// What the line holds of <paramref name="beyond"/>, cut at <paramref name="cut"/> (LANDSYNC3): the commits it begins with that the
    /// line holds move the cut past them, so the replay is WSR6's plain one; any held after a commit it lacks is skipped.
    /// </summary>
    internal static HeldBeyond Beyond(string cut, IReadOnlyList<BeyondCommit> beyond)
    {
        var at = 0;
        while (at < beyond.Count && beyond[at].Held) at++;
        var rest = beyond.Skip(at).ToList();
        var skips = rest.Where(each => each.Held).Select(each => each.Sha).ToList();
        return new(at == 0 ? cut : beyond[at - 1].Sha, [.. rest.Select(each => each.Sha)], skips, at + skips.Count);
    }

    /// <summary>
    /// Whether the walk by content could find any of <paramref name="beyond"/> on the line: a run from where the branch left it reads
    /// there only where the line changed every file the run changed, so at least those the first commit it lacks by patch touched.
    /// </summary>
    /// <param name="brought">The files the line changed since the branch left it.</param>
    internal static bool MayHoldByContent(IReadOnlyList<BeyondCommit> beyond, IReadOnlyCollection<string> brought)
    {
        if (beyond.FirstOrDefault(each => !each.Held && each.Files.Count > 0) is not { } first) return false;
        var changed = brought.ToHashSet(StringComparer.Ordinal);
        return first.Files.All(changed.Contains);
    }

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
