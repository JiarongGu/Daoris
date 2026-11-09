using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// A judged branch's own commits replayed onto the line and proven before the branch moves, and the grown record's tidying after: over the trees' context, whose trees home a replay made
/// aside lives under and whose records say where each branch now grows from.
/// </summary>
internal sealed class BranchReplays(TreeContext context, PassLeftovers leftovers)
{
    /// <summary>What a replay's own tree's folder name starts with, beside a session tree's <c>s-</c> and an opinion's copy's <c>o-</c>.</summary>
    internal const string Prefix = "r-";

    /// <summary>The replay that settles what the records may settle, in a tree of Daoris's own: the landing's and the <c>rebase</c> step's.</summary>
    internal ReplaySettles Settles { get; } = new(context, leftovers);

    /// <summary>A branch the press replayed: where it stood, where it went, and where its own commits started.</summary>
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
    /// Replay one branch's own commits onto the line, in a tree of Daoris's own, then prove its own work
    /// survived before the branch moves. A conflict aborts, and the branch is left where it stood.
    /// </summary>
    /// <remarks>
    /// <para><b>The replay happens detached, and the branch moves last</b>, from the commit it is judged at: a session's
    /// tree is detached where it stands, a landing's branch is replayed in a tree made for it. Only once the new
    /// commit is proven does the ref move, so a refusal at any step leaves the branch where it stood.</para>
    ///
    /// <para>🔴 <b>The proof</b>: between the old tip and the new one, only files the
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
        // The commits the line holds beyond where the replay starts are skipped, the runs between them replayed; null for a
        // plain replay. None left to replay is the line's commit itself, while the proof still reads from where its own work started.
        var stretches = StretchesBeyond(judged, upstream);
        var replayFrom = stretches is { Count: 0 } ? tip : upstream;

        string? now;
        string words;
        var tree = judged.Tree;
        if (tree is not null)
        {
            // 🔴 The session's own tree, read again immediately before: git's own working root for it, asked in the
            // same start as its branch, then still on this branch, at the commit judged, clean. A tree git binds to another checkout is
            // left before its status is read: a switch or a rebase in it would rewrite that checkout's files.
            var (onCode, onOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--show-toplevel", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
            var on = onOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (await TreeBinding.RefusalAsync(tree, ct, onCode == 0 && on.Length == 2 ? on[0] : null).ConfigureAwait(false) is { } bound)
            {
                return new(item, false, $"`{item.Branch}` was left: {bound}.");
            }

            var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
            if ((on.Length == 2 ? on[1] : "") != item.Branch || await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) != tip || !string.IsNullOrWhiteSpace(dirty))
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

        // Only from the commit it is judged at: git refuses the move if it is elsewhere now.
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            root, ["update-ref", "-m", $"daoris: bring {item.Branch} up to date onto {line}", $"refs/heads/{item.Branch}", now, tip], ct)
            .ConfigureAwait(false);
        if (tree is not null) await WorkingTree.GitAsync(tree, ["switch", "--quiet", item.Branch], ct).ConfigureAwait(false);
        if (moved != 0) return new(item, false, $"`{item.Branch}` moved since the list, so it was left: {Failure(moveErr)}");

        replayed.Add(new Replayed(tip, now, cut));
        var kept = await CountAsync(root, $"{onto}..{now}", ct).ConfigureAwait(false);
        // What the line holds is said apart from what became empty: the one judged before, the other found by the replay.
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
    /// A session's own tree, detached where it stands and replayed there: the new commit, or null and git's
    /// words. The branch itself is not moved here, and on a failure the tree is left detached for
    /// <see cref="BackOnAsync"/> to put back.
    /// </summary>
    /// <param name="tip">The commit the tree stands on, detached from it here.</param>
    /// <param name="stretches">The runs to replay where the line holds commits between them; null replays all from <paramref name="upstream"/>.</param>
    private static async Task<(string? Commit, string Words)> ReplayDetachedAsync(
        string tree, string branch, string onto, string upstream, string tip, IReadOnlyList<(string From, string To)>? stretches, CancellationToken ct)
    {
        var (detached, _, detachErr) = await WorkingTree.GitAsync(tree, ["switch", "--quiet", "--detach"], ct).ConfigureAwait(false);
        if (detached != 0) return (null, $"git would not detach its tree to replay it: {Failure(detachErr)}");
        if (stretches is not null)
        {
            var made = await ReplaySettles.RebaseStretchesAsync(tree, onto, stretches, switchFirst: stretches[0].To != tip, ct).ConfigureAwait(false);
            return (made.Now, made.Words);
        }

        var (failed, words) = await RebaseInAsync(tree, onto, upstream, ct).ConfigureAwait(false);
        return failed ? (null, words) : (await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false), "");
    }

    /// <summary>
    /// The runs a replay from <paramref name="upstream"/> carries where the line holds commits between them: null where it
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

    /// <summary>A session's tree put back on its branch after a replay not kept — true where it is on it, at the commit judged.</summary>
    private static async Task<bool> BackOnAsync(string tree, string branch, string tip, CancellationToken ct)
    {
        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        await WorkingTree.GitAsync(tree, ["switch", "--quiet", branch], ct).ConfigureAwait(false);
        var (_, onOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return onOut.Trim() == branch && await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) == tip;
    }

    /// <summary>
    /// The files that differ between the branch's old tip and its new one beyond those the line changed between where
    /// its own commits started and what they were replayed onto — null where there are none; git unable to say
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
    /// A branch nobody has checked out, replayed in a tree made for it under the trees home and removed after:
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
        var (aside, notMade) = await Settles.OpenAsideAsync(root, workspace, repository, stretches?[0].To ?? tip, ct).ConfigureAwait(false);
        if (aside is null) return (null, notMade);
        try
        {
            if (stretches is not null)
            {
                var made = await ReplaySettles.RebaseStretchesAsync(aside, onto, stretches, switchFirst: false, ct).ConfigureAwait(false);
                return (made.Now, made.Words);
            }

            var (failed, words) = await RebaseInAsync(aside, onto, upstream, ct).ConfigureAwait(false);
            return failed ? (null, words) : (await CommitOfAsync(aside, "HEAD", ct).ConfigureAwait(false), "");
        }
        finally
        {
            await ReplaySettles.CloseAsideAsync(root, aside).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The runs of <paramref name="commits"/> (oldest first) between those <paramref name="skips"/> names, each as the commit before its
    /// first and its last: what one <c>git rebase --onto</c> replays.
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
    /// <paramref name="onto"/>, skipping those the line holds (a detached tree's <i>Bring up to date</i>): the answer
    /// <see cref="RebaseInAsync"/> gives, run by run. On a failure HEAD may stand anywhere, for the caller to put back.
    /// </summary>
    internal static async Task<(bool Failed, string Words)> RebaseRunsInAsync(
        string tree, string tip, string onto, IReadOnlyList<string> commits, IReadOnlyList<string> skips, CancellationToken ct)
    {
        var stretches = Stretches(commits, skips.ToHashSet(StringComparer.Ordinal));
        if (stretches.Count == 0) return await RebaseInAsync(tree, onto, tip, ct).ConfigureAwait(false);
        var made = await ReplaySettles.RebaseStretchesAsync(tree, onto, stretches, switchFirst: stretches[0].To != tip, ct).ConfigureAwait(false);
        return made.Now is null ? (true, made.Words) : (false, "");
    }

    /// <summary>The record forgets session branches that are gone, so a name is never judged by a start it no longer has.</summary>
    /// <param name="held">The judgement's refs, whose session branches are the list where they could be read; null asks git.</param>
    internal async Task ForgetGoneAsync(string root, string repository, CancellationToken ct, RefSnapshot? held = null)
    {
        // 🔴 A list git could not give is not "every branch is gone".
        IReadOnlyList<string>? branches = held is null ? null : await held.BranchesAsync(TreeFollows.SessionPrefix, ct).ConfigureAwait(false);
        if (branches is null)
        {
            var (code, refs, _) = await WorkingTree.GitAsync(
                root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
            if (code != 0) return;
            branches = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var standing = branches.ToHashSet(StringComparer.Ordinal);
        var gone = new List<string>();
        foreach (var entry in context.Grown.All()
                     .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase) && !standing.Contains(entry.Branch)))
        {
            // A tree its agent moved onto a branch of its own outlives the session branch, and the entry is what says
            // where its work is: it goes once the tree and that branch are gone too.
            if (entry.Now is { } now)
            {
                if (Directory.Exists(context.TreeAt(entry.Workspace, entry.Repository, Path.GetFileName(entry.Branch)))) continue;
                var (stands, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{now}"], ct).ConfigureAwait(false);
                if (stands == 0) continue;
                // That branch unread (a broken ref) is not gone; only git's own "missing" says so.
                if (!(await ContentHolds.MissingAsync(root, $"refs/heads/{now}", ct).ConfigureAwait(false)).Missing) continue;
            }

            // The list leaves a broken ref out with a warning, so a branch it did not name is gone only on git's own word.
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
