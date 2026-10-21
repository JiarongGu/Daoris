using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// A judged branch's own commits replayed onto the line and proven before the branch moves (WSR6; PART10j, out of
/// <c>SessionTrees.Sync.cs</c>), and the grown record's tidying after: over the trees' context, whose trees home a replay made
/// aside lives under and whose records say where each branch now grows from.
/// </summary>
internal sealed class BranchReplays(TreeContext context, PassLeftovers leftovers)
{
    /// <summary>What a replay's own tree's folder name starts with, beside a session tree's <c>s-</c> and an opinion's copy's <c>o-</c>.</summary>
    internal const string Prefix = "r-";

    /// <summary>A branch the press replayed: where it was, where it went, and where its own commits started.</summary>
    internal sealed record Replayed(string OldTip, string NewTip, string Cut);

    /// <summary>
    /// The replays in the order that keeps shared commits shared: a branch inside another first, and a session's own
    /// branch before a landing's at the same commit, so the landing's takes the session's new commit.
    /// </summary>
    internal static async Task<IReadOnlyList<RebaseJudged>> DeepestLastAsync(string root, List<RebaseJudged> judged, CancellationToken ct)
    {
        var depths = new Dictionary<RebaseJudged, int>();
        foreach (var each in judged) depths[each] = each.Item.Replays && each.Tip is not null ? await CountAsync(root, each.Tip, ct).ConfigureAwait(false) : 0;
        return [.. judged.OrderBy(each => depths[each]).ThenBy(each => each.Item.Landed ? 1 : 0)];
    }

    /// <summary>
    /// Replay one branch's own commits onto the line (WSR6), in a tree of Daoris's own, then prove its own work
    /// survived before the branch moves. A conflict aborts, and the branch is left as it was.
    /// </summary>
    /// <remarks>
    /// <para><b>The replay happens detached, and the branch moves last</b>, from the commit it was judged at: a session's
    /// tree is detached where it stands, a landing's branch is replayed in a tree made for it. Only once the new
    /// commit is proven does the ref move, so a refusal at any step leaves the branch where it was.</para>
    ///
    /// <para>🔴 <b>The proof</b> (the first real post-merge run): between the old tip and the new one, only files the
    /// line itself changed between the cut and where the branch now grows from may differ — everything else of the
    /// branch's must read exactly as before. Where the line brought nothing but the squash of the work the branch grew
    /// from, the two tips read the same.</para>
    /// </remarks>
    internal async Task<RebaseResult> ReplayAsync(
        string root, string repository, string workspace, string line, string onto, RebaseJudged judged, List<Replayed> replayed,
        CancellationToken ct)
    {
        var item = judged.Item;
        var tip = judged.Tip!;
        var cut = judged.Cut!;
        var (baseCommit, upstream) = await WithinAsync(root, replayed, tip, cut, ct).ConfigureAwait(false) is { } within
            ? (within.NewTip, within.OldTip)
            : (onto, cut);
        // LANDSYNC3: the commits the line holds beyond where the replay starts are skipped, the runs between them replayed; null for a
        // plain replay. None left to replay is the line's commit itself, while the proof still reads from where its own work started.
        var stretches = StretchesBeyond(judged, upstream);
        var replayFrom = stretches is { Count: 0 } ? tip : upstream;

        string? now;
        string words;
        var tree = judged.Tree;
        if (tree is not null)
        {
            // 🔴 The session's own tree, read again immediately before: still on this branch, at the commit judged, clean.
            var (_, onOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
            var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
            if (onOut.Trim() != item.Branch || await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) != tip || !string.IsNullOrWhiteSpace(dirty))
            {
                return new(item, false, $"`{item.Branch}` {TreeSync.LeftSince}");
            }

            (now, words) = await ReplayDetachedAsync(tree, item.Branch, baseCommit, replayFrom, tip, stretches is { Count: > 0 } ? stretches : null, ct)
                .ConfigureAwait(false);
            if (now is null)
            {
                return new(item, false, await BackOnAsync(tree, item.Branch, tip, ct).ConfigureAwait(false)
                    ? $"`{item.Branch}` would not replay onto `{line}`: {words} The replay was aborted, and the branch is as it was."
                    : $"`{item.Branch}` would not replay onto `{line}`: {words} Git could not put its tree back on it: look at the tree before "
                      + "anything else (`git rebase --abort`, then `git switch` to the branch).");
            }
        }
        else if (replayFrom == tip)
        {
            now = baseCommit;
        }
        else
        {
            (now, words) = await ReplayAsideAsync(root, workspace, repository, tip, baseCommit, replayFrom, stretches is { Count: > 0 } ? stretches : null, ct)
                .ConfigureAwait(false);
            if (now is null) return new(item, false, $"`{item.Branch}` would not replay onto `{line}`: {words} Nothing was moved.");
        }

        // 🔴 Proven before the branch moves: nothing of its own changed but what the line brought.
        if (await BeyondTheLineAsync(root, tip, now, upstream, baseCommit, ct).ConfigureAwait(false) is { } beyond)
        {
            if (tree is not null) await BackOnAsync(tree, item.Branch, tip, ct).ConfigureAwait(false);
            return new(item, false, $"`{item.Branch}`'s replay changed what the line did not bring ({beyond}), so it was not kept: the branch "
                + "is as it was.");
        }

        // Only from the commit it was judged at: git refuses the move if it is elsewhere now.
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            root, ["update-ref", "-m", $"daoris: bring {item.Branch} up to date onto {line}", $"refs/heads/{item.Branch}", now, tip], ct)
            .ConfigureAwait(false);
        if (tree is not null) await WorkingTree.GitAsync(tree, ["switch", "--quiet", item.Branch], ct).ConfigureAwait(false);
        if (moved != 0) return new(item, false, $"`{item.Branch}` moved since the list, so it was left: {Failure(moveErr)}");

        replayed.Add(new Replayed(tip, now, cut));
        var kept = await CountAsync(root, $"{onto}..{now}", ct).ConfigureAwait(false);
        // What the line holds is said apart from what became empty (LANDSYNC3): the one was judged before, the other found by the replay.
        var message = kept == 0 && item.Commits == 0 && judged.LeftOut > 0
            ? $"moved `{item.Branch}` onto `{line}`: it holds nothing of its own beyond work that reached the line ({judged.LeftOut} commit(s) the line holds left out)."
            : $"replayed `{item.Branch}` onto `{line}`: {kept} commit(s) of its own"
              + (judged.LeftOut > 0 ? $", leaving out {judged.LeftOut} the line holds" : "")
              + (kept < item.Commits ? $" ({item.Commits - kept} became empty, their changes already on the line)." : ".");

        // Where it grows from now is the line: the record says so, so the next press and the review cut here.
        try
        {
            if (item.Landed) context.Recorded.Moved(repository, item.Branch, now, onto);
            else context.Grown.Record(new GrownBranch(repository, workspace, item.Branch, line, onto, null, DateTimeOffset.UtcNow, context.Grown.Of(repository, item.Branch)?.Now));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            message += $" Daoris could not record where it now grows from ({error.Message}).";
        }

        return new(item, true, message);
    }

    /// <summary>
    /// A branch this press already replayed whose old commits this one holds past its own start — the nearest,
    /// replayed last — so this one is replayed onto its new commits and the two stay shared; null for none.
    /// </summary>
    private static async Task<Replayed?> WithinAsync(string root, List<Replayed> replayed, string tip, string cut, CancellationToken ct)
    {
        for (var at = replayed.Count - 1; at >= 0; at--)
        {
            var done = replayed[at];
            if (done.OldTip == cut) continue;
            if (await IsAncestorAsync(root, done.OldTip, tip, ct).ConfigureAwait(false)
                && await IsAncestorAsync(root, cut, done.OldTip, ct).ConfigureAwait(false))
            {
                return done;
            }
        }

        return null;
    }

    /// <summary>
    /// <c>git rebase --onto</c> in a tree whose HEAD is the branch (or a detached copy of it), aborted on any failure.
    /// </summary>
    /// <remarks>
    /// The person's configuration still speaks — their identity, their signing, their hooks — since the replay runs
    /// as them, except two settings that would reach beyond the one branch judged: <c>rebase.updateRefs</c> would
    /// move every other branch pointing into the range, and <c>rebase.autoStash</c> would carry uncommitted work
    /// the judgement found none of.
    /// </remarks>
    internal static async Task<(bool Failed, string Words)> RebaseInAsync(string tree, string onto, string upstream, CancellationToken ct)
    {
        var (code, output, err) = await WorkingTree.GitAsync(
            tree, ["-c", "rebase.updateRefs=false", "-c", "rebase.autoStash=false", "rebase", "--onto", onto, upstream],
            environment: null, SyncBounds.Replay, ct).ConfigureAwait(false);
        if (code == 0) return (false, "");

        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        var conflicts = (output + "\n" + err).Split('\n')
            .Select(each => each.Trim())
            .Where(each => each.StartsWith("CONFLICT", StringComparison.Ordinal))
            .ToList();
        return (true, conflicts.Count > 0
            ? string.Join("; ", conflicts.Take(3)) + (conflicts.Count > 3 ? $"; and {conflicts.Count - 3} more." : ".")
            : Failure(err.Length > 0 ? err : output));
    }

    /// <summary>
    /// A session's own tree, detached where it stands and replayed there (WSR6): the new commit, or null and git's
    /// words. The branch itself is not moved here, and on a failure the tree is left detached for
    /// <see cref="BackOnAsync"/> to put back.
    /// </summary>
    /// <param name="tip">The commit the tree stands on, detached from it here.</param>
    /// <param name="stretches">The runs to replay where the line holds commits between them (LANDSYNC3); null replays all from <paramref name="upstream"/>.</param>
    private static async Task<(string? Commit, string Words)> ReplayDetachedAsync(
        string tree, string branch, string onto, string upstream, string tip, IReadOnlyList<(string From, string To)>? stretches, CancellationToken ct)
    {
        var (detached, _, detachErr) = await WorkingTree.GitAsync(tree, ["switch", "--quiet", "--detach"], ct).ConfigureAwait(false);
        if (detached != 0) return (null, $"git would not detach its tree to replay it: {Failure(detachErr)}");
        if (stretches is not null)
        {
            var made = await RebaseStretchesAsync(tree, onto, stretches, switchFirst: stretches[0].To != tip, ct).ConfigureAwait(false);
            return (made.Now, made.Words);
        }

        var (failed, words) = await RebaseInAsync(tree, onto, upstream, ct).ConfigureAwait(false);
        return failed ? (null, words) : (await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false), "");
    }

    /// <summary>
    /// The runs a replay from <paramref name="upstream"/> carries where the line holds commits between them (LANDSYNC3): null where it
    /// skips none, so the replay is a plain one; empty where the line holds every one left.
    /// </summary>
    private static IReadOnlyList<(string From, string To)>? StretchesBeyond(RebaseJudged judged, string upstream)
    {
        if (judged.Skips.Count == 0 || judged.Beyond is not { } beyond) return null;
        var from = 0;
        if (upstream != judged.Cut)
        {
            // Replayed onto a branch this press already replayed (WithinAsync): only the commits beyond that one's old tip.
            var at = beyond.ToList().IndexOf(upstream);
            if (at < 0) return null;
            from = at + 1;
        }

        var rest = beyond.Skip(from).ToList();
        var skips = judged.Skips.Where(rest.Contains).ToHashSet(StringComparer.Ordinal);
        return skips.Count == 0 ? null : Stretches(rest, skips);
    }

    /// <summary>A session's tree put back on its branch after a replay that was not kept — true where it is on it, at the commit judged.</summary>
    private static async Task<bool> BackOnAsync(string tree, string branch, string tip, CancellationToken ct)
    {
        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        await WorkingTree.GitAsync(tree, ["switch", "--quiet", branch], ct).ConfigureAwait(false);
        var (_, onOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return onOut.Trim() == branch && await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) == tip;
    }

    /// <summary>
    /// The files that differ between the branch's old tip and its new one beyond those the line changed between where
    /// its own commits started and what they were replayed onto (WSR6) — null where there are none; git unable to say
    /// is itself a reason not to keep the replay.
    /// </summary>
    internal static async Task<string?> BeyondTheLineAsync(string root, string tip, string now, string upstream, string onto, CancellationToken ct)
    {
        var changed = await LandedProofs.NamesAsync(root, tip, now, ct).ConfigureAwait(false);
        var brought = await LandedProofs.NamesAsync(root, upstream, onto, ct).ConfigureAwait(false);
        if (changed is null || brought is null) return "git could not compare the two";
        var beyond = changed.Where(path => !brought.Contains(path, StringComparer.Ordinal)).ToList();
        return beyond.Count == 0 ? null : string.Join(", ", beyond.Take(3)) + (beyond.Count > 3 ? $" and {beyond.Count - 3} more" : "");
    }

    /// <summary>
    /// A branch nobody has checked out, replayed in a tree made for it under the trees home and removed after (WSR6):
    /// the new commit, or null and git's words. The branch itself is not moved here.
    /// </summary>
    /// <remarks>
    /// Under the trees home, where Daoris's trees live, and like every git call here with long paths on: the first real
    /// post-merge run, done by hand in a tree made elsewhere without them, could not check out the repository's
    /// deepest files.
    /// </remarks>
    private async Task<(string? Commit, string Words)> ReplayAsideAsync(
        string root, string workspace, string repository, string tip, string onto, string upstream,
        IReadOnlyList<(string From, string To)>? stretches, CancellationToken ct)
    {
        var (aside, notMade) = await OpenAsideAsync(root, workspace, repository, stretches?[0].To ?? tip, ct).ConfigureAwait(false);
        if (aside is null) return (null, notMade);
        try
        {
            if (stretches is not null)
            {
                var made = await RebaseStretchesAsync(aside, onto, stretches, switchFirst: false, ct).ConfigureAwait(false);
                return (made.Now, made.Words);
            }

            var (failed, words) = await RebaseInAsync(aside, onto, upstream, ct).ConfigureAwait(false);
            return failed ? (null, words) : (await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false), "");
        }
        finally
        {
            await CloseAsideAsync(root, aside).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A tree of Daoris's own for one replay, under the trees home, detached at <paramref name="at"/> (WSR6, LANDSYNC1): what a
    /// replay a kill cut off left in the repository's trees moved aside first (<see cref="PassLeftovers"/>, STRAYOTHERS1), then the
    /// tree added locked under words naming this process for as long as the replay runs, so the next replay tells a tree in use
    /// from a leftover. Its path, or null and git's words.
    /// </summary>
    private async Task<(string? Aside, string Words)> OpenAsideAsync(string root, string workspace, string repository, string at, CancellationToken ct)
    {
        var trees = Path.Combine(context.TreesRoot, workspace, repository);
        try
        {
            await leftovers.ClearReplaysAsync(root, trees, DateTimeOffset.UtcNow, TreeOpenings.Running, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A leftover that could not be cleared is the next replay's to try again, never a reason to refuse this one.
        }

        var aside = Path.Combine(trees, $"{Prefix}{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(trees);
        var (added, _, addErr) = await WorkingTree.GitAsync(
            root, ["worktree", "add", "--quiet", "--detach", "--lock", "--reason", PassLeftovers.HeldBy(Environment.ProcessId), aside, at], ct)
            .ConfigureAwait(false);
        return added == 0 ? (aside, "") : (null, $"Daoris could not make a tree to replay it in: {Failure(addErr)}");
    }

    /// <summary>
    /// A replay's own tree removed, its lock with it (<c>--force</c> twice), whatever the replay's token says, since a tree left
    /// locked by a process that still runs would wait for that process to end (STRAYOTHERS1). Daoris's own tree, made a moment ago
    /// for this one replay and holding nothing of anyone's: its commits are in the repository, so removing it loses nothing.
    /// </summary>
    private static Task CloseAsideAsync(string root, string aside) =>
        WorkingTree.GitAsync(root, ["worktree", "remove", "--force", "--force", aside], CancellationToken.None);

    /// <summary>What a landing's replay made (LANDSYNC1): the new tip, or null and git's words, and whether those name a conflict.</summary>
    internal sealed record KeptReplay(string? Now, string Words, bool Conflict)
    {
        /// <summary>The generated files it took at the line's copy where only those conflicted (LANDREBASE1), in the order met.</summary>
        public IReadOnlyList<string> Generated { get; init; } = [];

        /// <summary>Those written again from the replayed tree by the writers the person accepted (GENREGEN1); none where none ran.</summary>
        public GeneratedWriters.Written Written { get; init; } = GeneratedWriters.Written.None;
    }

    /// <summary>
    /// The commits a landing keeps replayed onto <paramref name="onto"/>, in order, in a tree of Daoris's own made for it under the
    /// trees home and removed after (LANDSYNC1, D87's 2026-10-15 amendment point 2): <see cref="ReplayAsideAsync"/>'s shape, one
    /// <c>git rebase --onto</c> per stretch of <paramref name="commits"/> it keeps, each onto the last one's new tip. A commit that
    /// becomes empty is dropped by git as WSR6's replay drops one; a conflict aborts it, and nothing is moved. LANDREBASE1: a conflict
    /// only in files the repository marks generated is not the session's, so those take the line's copy and the replay goes on
    /// (<see cref="GeneratedPaths"/>); one in any other file still aborts it. GENREGEN1: where it took any, the writers the person
    /// accepted for their folders write them again from the replayed tree, in a commit on top (<see cref="GeneratedWriters"/>).
    /// </summary>
    /// <param name="commits">Every commit beyond the line, oldest first, which <paramref name="kept"/> is a subset of.</param>
    internal async Task<KeptReplay> ReplayKeptAsideAsync(
        string root, string workspace, string repository, string onto, IReadOnlyList<LandingCommit> commits, IReadOnlyList<LandingCommit> kept,
        CancellationToken ct)
    {
        if (kept.Count == 0) return new(onto, "", false);
        var keeps = kept.Select(each => each.Sha).ToHashSet(StringComparer.Ordinal);
        var stretches = Stretches([.. commits.Select(each => each.Sha)], commits.Select(each => each.Sha).Where(sha => !keeps.Contains(sha)).ToHashSet(StringComparer.Ordinal));

        var (aside, notMade) = await OpenAsideAsync(root, workspace, repository, stretches[0].To, ct).ConfigureAwait(false);
        if (aside is null) return new(null, notMade, false);
        try
        {
            var made = await RebaseStretchesAsync(aside, onto, stretches, switchFirst: false, ct, takesGenerated: true).ConfigureAwait(false);
            if (made.Now is null || made.Generated.Count == 0) return made;
            var writers = await AcceptedWritersAsync(root, repository, workspace, onto, ct).ConfigureAwait(false);
            var written = await GeneratedWriters.WriteAsync(aside, writers, made.Generated, ct).ConfigureAwait(false);
            return written.Paths.Count == 0
                ? made with { Written = written }
                : made with { Now = await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false) ?? made.Now, Written = written };
        }
        finally
        {
            // Daoris's own tree, made a moment ago for this one replay and holding nothing of anyone's, as WSR6's is.
            await CloseAsideAsync(root, aside).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The generated folders' writers the person accepted for <paramref name="repository"/>, as declared at <paramref name="onto"/>
    /// (GENREGEN1); none where the driver's settings could not be read, and the replay leaves the line's copy as it would without one.
    /// </summary>
    private async Task<IReadOnlyList<GeneratedWriters.Writer>> AcceptedWritersAsync(
        string root, string repository, string workspace, string onto, CancellationToken ct)
    {
        DriverConfig config;
        try
        {
            config = context.Config();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return [];
        }

        return await GeneratedWriters.AcceptedAsync(config, root, repository, workspace, onto, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The runs of <paramref name="commits"/> (oldest first) between those <paramref name="skips"/> names, each as the commit before its
    /// first and its last: what one <c>git rebase --onto</c> replays (LANDSYNC1; LANDSYNC3 for <i>Bring up to date</i>).
    /// </summary>
    internal static IReadOnlyList<(string From, string To)> Stretches(IReadOnlyList<string> commits, IReadOnlySet<string> skips)
    {
        var stretches = new List<(string From, string To)>();
        string? first = null;
        string? last = null;
        foreach (var commit in commits)
        {
            if (!skips.Contains(commit))
            {
                first ??= commit;
                last = commit;
                continue;
            }

            if (first is not null) stretches.Add(($"{first}^", last!));
            first = null;
        }

        if (first is not null) stretches.Add(($"{first}^", last!));
        return stretches;
    }

    /// <summary>
    /// <paramref name="commits"/> (oldest first) replayed in <paramref name="tree"/>, detached at <paramref name="tip"/>, onto
    /// <paramref name="onto"/>, skipping those the line holds (LANDSYNC3, a detached tree's <i>Bring up to date</i>): the answer
    /// <see cref="RebaseInAsync"/> gives, run by run. On a failure HEAD may stand anywhere, for the caller to put back.
    /// </summary>
    internal static async Task<(bool Failed, string Words)> RebaseRunsInAsync(
        string tree, string tip, string onto, IReadOnlyList<string> commits, IReadOnlyList<string> skips, CancellationToken ct)
    {
        var stretches = Stretches(commits, skips.ToHashSet(StringComparer.Ordinal));
        if (stretches.Count == 0) return await RebaseInAsync(tree, onto, tip, ct).ConfigureAwait(false);
        var made = await RebaseStretchesAsync(tree, onto, stretches, switchFirst: stretches[0].To != tip, ct).ConfigureAwait(false);
        return made.Now is null ? (true, made.Words) : (false, "");
    }

    /// <summary>
    /// <paramref name="stretches"/> replayed in <paramref name="tree"/>, detached, each onto the last one's new tip, starting at
    /// <paramref name="onto"/>: the new tip, or null and git's words, and whether those name a conflict. Each run is taken up detached
    /// at its last commit first (the first only where <paramref name="switchFirst"/>: a tree opened there already stands on it).
    /// </summary>
    /// <param name="takesGenerated">
    /// A landing's replay (LANDREBASE1): a stop whose every conflicted file the repository marks generated takes the line's copy of
    /// those and goes on, and the answer names them. False stops at any conflict, as WSR6's replays do.
    /// </param>
    private static async Task<KeptReplay> RebaseStretchesAsync(
        string tree, string onto, IReadOnlyList<(string From, string To)> stretches, bool switchFirst, CancellationToken ct, bool takesGenerated = false)
    {
        var current = onto;
        var generated = new List<string>();
        for (var at = 0; at < stretches.Count; at++)
        {
            if (at > 0 || switchFirst)
            {
                var (moved, _, moveErr) = await WorkingTree.GitAsync(tree, ["switch", "--quiet", "--detach", stretches[at].To], ct).ConfigureAwait(false);
                if (moved != 0) return new(null, $"git would not take up its next commits to replay: {Failure(moveErr)}", false);
            }

            var (failed, words) = takesGenerated
                ? await RebaseTakingGeneratedAsync(tree, current, stretches[at].From, generated, ct).ConfigureAwait(false)
                : await RebaseInAsync(tree, current, stretches[at].From, ct).ConfigureAwait(false);
            if (failed) return new(null, words, words.StartsWith("CONFLICT", StringComparison.Ordinal));
            current = await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) ?? current;
        }

        return new(current, "", false) { Generated = generated };
    }

    /// <summary>
    /// <see cref="RebaseInAsync"/>'s replay, where a stop whose every conflicted file the repository marks generated
    /// (<see cref="GeneratedPaths"/>, LANDREBASE1) is not the session's conflict: those files take the line's side, the side the replay
    /// builds on, and the replay goes on, a commit left empty by it skipped as git would drop one. Any other stop aborts as
    /// <see cref="RebaseInAsync"/> does, with git's <c>CONFLICT</c> lines. The files taken are added to <paramref name="generated"/>.
    /// </summary>
    private static async Task<(bool Failed, string Words)> RebaseTakingGeneratedAsync(
        string tree, string onto, string upstream, List<string> generated, CancellationToken ct)
    {
        var (code, output, err) = await WorkingTree.GitAsync(
            tree, ["-c", "rebase.updateRefs=false", "-c", "rebase.autoStash=false", "rebase", "--onto", onto, upstream],
            environment: null, SyncBounds.Replay, ct).ConfigureAwait(false);
        // Each stop is one commit; a bound on them keeps a replay that never settles from going round for ever.
        for (var stops = 0; code != 0 && stops < GeneratedPaths.StopsAtMost; stops++)
        {
            var (listed, unmerged, _) = await WorkingTree.GitAsync(tree, ["-c", "core.quotePath=false", "diff", "--name-only", "--diff-filter=U"], ct)
                .ConfigureAwait(false);
            var paths = unmerged.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (listed != 0 || paths.Length == 0 || await GeneratedPaths.AllAsync(tree, paths, ct).ConfigureAwait(false) is not true) break;

            foreach (var path in paths)
            {
                // The line's side, `--ours` in a rebase; a path the line removed goes, as it does there.
                var (taken, _, _) = await WorkingTree.GitAsync(tree, ["checkout", "--ours", "--", path], ct).ConfigureAwait(false);
                await WorkingTree.GitAsync(tree, taken == 0 ? ["add", "--", path] : ["rm", "--quiet", "--cached", "--force", "--", path], ct)
                    .ConfigureAwait(false);
                if (!generated.Contains(path, StringComparer.Ordinal)) generated.Add(path);
            }

            // Nothing left of the commit once its generated files are the line's: skipped, as a commit that becomes empty is dropped.
            var (same, _, _) = await WorkingTree.GitAsync(tree, ["diff", "--cached", "--quiet", "HEAD"], ct).ConfigureAwait(false);
            (code, output, err) = await WorkingTree.GitAsync(
                tree, same == 0 ? ["rebase", "--skip"] : ["-c", "core.editor=:", "rebase", "--continue"], environment: null, SyncBounds.Replay, ct)
                .ConfigureAwait(false);
        }

        if (code == 0) return (false, "");
        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        var conflicts = (output + "\n" + err).Split('\n')
            .Select(each => each.Trim())
            .Where(each => each.StartsWith("CONFLICT", StringComparison.Ordinal))
            .ToList();
        return (true, conflicts.Count > 0 ? LineHolds.Joined(conflicts) : Failure(err.Length > 0 ? err : output));
    }

    /// <summary>The record forgets session branches that are gone, so a name is never judged by a start it no longer has.</summary>
    internal async Task ForgetGoneAsync(string root, string repository, CancellationToken ct)
    {
        // 🔴 A list git could not give is not "every branch is gone".
        var (code, refs, _) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
        if (code != 0) return;
        var standing = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var gone = new List<string>();
        foreach (var entry in context.Grown.All()
                     .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase) && !standing.Contains(entry.Branch)))
        {
            // TREEFOLLOW1: a tree its agent moved onto a branch of its own outlives the session branch, and the entry is what says
            // where its work is: it goes once the tree and that branch are gone too.
            if (entry.Now is { } now)
            {
                if (Directory.Exists(Path.Combine(context.TreesRoot, entry.Workspace, entry.Repository, Path.GetFileName(entry.Branch)))) continue;
                var (stands, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{now}"], ct).ConfigureAwait(false);
                if (stands == 0) continue;
                // RECORDUNREAD1: that branch unread (a broken ref) is not gone; only git's own "missing" says so.
                if (!(await ContentHolds.MissingAsync(root, $"refs/heads/{now}", ct).ConfigureAwait(false)).Missing) continue;
            }

            // RECORDUNREAD1: the list leaves a broken ref out with a warning, so a branch it did not name is gone only on git's own word.
            if (!(await ContentHolds.MissingAsync(root, $"refs/heads/{entry.Branch}", ct).ConfigureAwait(false)).Missing) continue;
            gone.Add(entry.Branch);
        }

        if (gone.Count == 0) return;
        try
        {
            context.Grown.Forget(repository, gone);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A record that could not be tidied only keeps a name no branch has; the next press tries again.
        }
    }
}
