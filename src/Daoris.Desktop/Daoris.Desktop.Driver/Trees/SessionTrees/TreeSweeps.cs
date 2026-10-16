using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeLeftovers;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// The session branches' half of the clean-up (D88, PLUGHOOK1a, SQUASHTIDY1, AUTOTIDY1; PART10l-ii, out of <c>SessionTrees.cs</c>):
/// every session branch judged, the list, and the press's one path for both doors, the person's and the look's own. Over the
/// trees' context, the pull request's kept answers, the content proof, the follow's ledger, the squash's record and the look's
/// set-aside. The landed branches' half (<see cref="LandedCleanups"/>) and the look's pass (<see cref="AutoTidies"/>) take it.
/// </summary>
internal sealed class TreeSweeps(
    TreeContext context, PullRequestAsks asks, ContentHolds content, TreeFollows follows, TreeSquashes squashes, TreeSetAside setAside)
{
    /// <summary>
    /// Every session branch in these repositories, with what it holds (D88) — the list a person reads
    /// before the clean-up is pressed. Nothing is changed.
    /// </summary>
    /// <param name="inUse">The trees sessions still running or waiting name — kept whatever they hold.</param>
    /// <param name="only">The branches to judge, as <c>repository:branch</c>; null for every one. The look's own tidy (AUTOTIDY1) judges its few.</param>
    /// <param name="byItself">
    /// Judged for the look's own tidy (AUTOTIDY2a): an ignored path only the tree holds does not make it <c>dirty</c>, since the
    /// look moves the tree's folder aside rather than deleting it. The press's list and the press keep it <c>dirty</c>.
    /// </param>
    /// <param name="landings">
    /// For the look's own tidy (AUTOTIDY2b): the branches landings made whose work the line holds, through which a landed session
    /// branch's work is on the line too. Null for the press's list and the press, which take a landed branch wherever it is.
    /// </param>
    internal async Task<IReadOnlyList<SweepItem>> SweepPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default, IReadOnlySet<string>? only = null, bool byItself = false, IReadOnlyList<LineLanding>? landings = null)
    {
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var items = new List<SweepItem>();
        foreach (var (repository, workspace, root) in repositories)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            // The repository's refs read once for every branch judged here (PROC1d-ii); a write in this flow drops it.
            var refs = RefSnapshot.Take(root);
            var sessions = await refs.BranchesAsync(TreeFollows.SessionPrefix, ct).ConfigureAwait(false);
            if (sessions is null)
            {
                var (code, output, _) = await WorkingTree.GitAsync(
                    root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
                if (code != 0) continue;
                sessions = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            var named = sessions.Where(branch => only is null || only.Contains($"{repository}:{branch}")).ToList();
            // A list git could not give is never read as "no tree here" (AUTOTIDY1): every branch is kept, saying why.
            if (await context.Worktrees.ListAsync(root, ct, refs).ConfigureAwait(false) is not { } listed)
            {
                items.AddRange(named.Select(branch => new SweepItem(
                    repository, RemoteTarget.Workspace(workspace), branch, null, SweepKind.Unlanded, 0, null, $"git could not tell: {ListUnread}")
                {
                    Unread = true,
                }));
                continue;
            }

            var worktrees = ByBranch(listed);
            var line = (await context.LineAsync(root, repository, RemoteTarget.Workspace(workspace), ct).ConfigureAwait(false)).Branch;
            // The completed pull requests the record keeps, confirmed here (PLUGHOOK1a): nothing is asked at a list or a press.
            var carriers = await asks.CarriersAsync(root, repository, line, ct, refs).ConfigureAwait(false);
            foreach (var branch in named)
            {
                items.Add((await JudgeAsync(
                    root, repository, RemoteTarget.Workspace(workspace), branch, worktrees.GetValueOrDefault(branch), line, busy, carriers, ct,
                    follows.MovedOnTo(listed, RemoteTarget.Workspace(workspace), repository, branch), byItself, landings, refs)
                    .ConfigureAwait(false)).Item);
            }
        }

        return items;
    }

    /// <summary>
    /// The clean-up (D88): every session branch the proof clears goes, with its tree, and the rest are
    /// kept and named. 🔴 Each is judged again right before it goes — a list is a fact about a moment.
    /// </summary>
    /// <param name="only">The branches the person saw listed to go, as <c>repository:branch</c>; null for every one the proof clears now.</param>
    internal Task<IReadOnlyList<SweepResult>> SweepAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default) =>
        SweepAsync(repositories, inUse, only, byItself: false, ct);

    /// <summary>
    /// The clean-up's one path, for both doors (AUTOTIDY1): the person's press takes every row the proof clears
    /// (<see cref="SweepItem.Removable"/>) and removes its tree; the look's own tidy takes the same kinds behind its guards
    /// (<see cref="TreeSetAside.GoesByItself"/>, AUTOTIDY2a), judges a tree's own ignored paths as no keep, asks its last guards
    /// (<see cref="TreeSetAside.LastLookAsync"/>), and moves the tree's folder aside rather than deleting it
    /// (<see cref="TreeSetAside.MoveAsideAsync"/>). The judgement, the judgement again, the commits a content-held branch keeps on a
    /// ref of their own, and the delete at the commit judged are the same for both.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A destructive step is never cancelled halfway</b> (AUTOTIDY1): once a branch's removal begins, each git command is
    /// awaited to its end whatever <paramref name="ct"/> says, so a closing loop never lets go of the repository's hold while
    /// git is still removing. Cancellation is heard between branches.
    /// </remarks>
    /// <param name="landings">The look's (AUTOTIDY2b): the branches landings made whose work the line holds, as <see cref="SweepPlanAsync"/> takes them.</param>
    internal async Task<IReadOnlyList<SweepResult>> SweepAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only, bool byItself, CancellationToken ct, IReadOnlyList<LineLanding>? landings = null)
    {
        var known = repositories.ToList();
        // One look at each checkout's line for the list and every item after it, the look's own tidy's as the person's (UIFREEZE1).
        using var look = context.OneLook();
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        Func<SweepItem, bool> goes = byItself ? setAside.GoesByItself : item => item.Removable;
        var results = new List<SweepResult>();
        foreach (var item in await SweepPlanAsync(known, inUse, ct, only, byItself, landings).ConfigureAwait(false))
        {
            if (only is not null && !only.Contains($"{item.Repository}:{item.Branch}"))
            {
                continue;
            }

            if (!goes(item))
            {
                results.Add(new(item, false, "kept"));
                continue;
            }

            var root = known.First(r => string.Equals(r.Repository, item.Repository, StringComparison.OrdinalIgnoreCase)).Root!;
            // Judged again from the refs and the working trees as they are now (PROC1d-ii, -iii, D88): never the list's, since a
            // removal before it wrote.
            var fresh = RefSnapshot.Take(root);
            if (await context.Worktrees.ListAsync(root, ct, fresh).ConfigureAwait(false) is not { } listed)
            {
                results.Add(new(item with { Unread = true, Detail = ListUnread }, false, $"{ListUnread}, so it is kept") { Kept = TidyKept.Unread, Cause = TidyCause.List });
                continue;
            }

            var worktrees = ByBranch(listed);
            var line = (await context.LineAsync(root, item.Repository, item.Workspace, ct).ConfigureAwait(false)).Branch;
            var carriers = item.Kind == SweepKind.Carried ? await asks.CarriersAsync(root, item.Repository, line, ct, fresh).ConfigureAwait(false) : [];
            // The commit it was judged at (SQUASHTIDY1c): the one its delete must still find, and the one a pull request's answer
            // removed, for the record of it (PLUGHOOK1a, design §2.5).
            var (now, judged) = await JudgeAsync(
                    root, item.Repository, item.Workspace, item.Branch, worktrees.GetValueOrDefault(item.Branch), line, busy, carriers, ct,
                    follows.MovedOnTo(listed, item.Workspace, item.Repository, item.Branch), byItself, landings, fresh)
                .ConfigureAwait(false);
            if (!goes(now) || judged is null)
            {
                results.Add(new(now, false, "it changed since the list, and is kept"));
                continue;
            }

            if (byItself && await setAside.LastLookAsync(root, now, ct).ConfigureAwait(false) is { } last)
            {
                results.Add(new(now, false, last.Sentence) { Kept = last.Code, Cause = last.Cause });
                continue;
            }

            // From here each step removes, or finishes a removal: none is cancelled halfway.
            var done = CancellationToken.None;

            // Held by content (SQUASHTIDY1c): its commits are kept on a ref of their own before anything goes.
            string? kept = null;
            if (now.HeldBy is not null)
            {
                var (reference, why) = await ContentHolds.KeepDiscardedAsync(root, now.Branch, judged, done).ConfigureAwait(false);
                if (reference is null)
                {
                    results.Add(new(now, false, $"Daoris could not keep its commits on a ref of their own ({why}), so it is kept"));
                    continue;
                }

                kept = reference;
            }

            string? left = null;
            string? movedTo = null;
            if (now.Tree is { } tree && byItself)
            {
                // The look never deletes a tree's folder: a write that lands after its last look lands in the folder moved aside.
                var moved = await setAside.MoveAsideAsync(root, now, done).ConfigureAwait(false);
                if (!moved.Gone)
                {
                    // AUTOTIDY2a: a content-held branch the look kept lets go of the ref it kept its commits on, as the press's
                    // refused removal does, or each later look would keep another.
                    await ContentHolds.DropKeptAsync(root, now.Branch, kept, judged, done).ConfigureAwait(false);
                    results.Add(new(now, false, moved.Sentence) { Kept = moved.Code });
                    continue;
                }

                movedTo = moved.To;
            }
            else if (now.Tree is { } pressed)
            {
                var (removeCode, _, removeErr) = await WorkingTree.GitAsync(
                    root, ["worktree", "remove", pressed], done).ConfigureAwait(false);
                if (removeCode != 0)
                {
                    // A tree git let go of whose folder something holds open: the tree is gone all the same.
                    var (letGo, why) = await LetGoAsync(root, pressed, done).ConfigureAwait(false);
                    if (!letGo)
                    {
                        await ContentHolds.DropKeptAsync(root, now.Branch, kept, judged, done).ConfigureAwait(false);
                        results.Add(new(now, false, $"git would not remove its tree: {FirstLine(removeErr)}"));
                        continue;
                    }

                    left = why;
                }
            }

            // Only while it is still the commit judged (SQUASHTIDY1c); git's own -d asks only about the checkout's HEAD.
            var stays = await content.DeleteJudgedAsync(root, now.Branch, judged, done).ConfigureAwait(false);
            if (stays is not null)
            {
                await ContentHolds.DropKeptAsync(root, now.Branch, kept, judged, done).ConfigureAwait(false);
                var treeSaid = movedTo is not null ? $"; its tree's folder was moved aside to {movedTo}"
                    : now.Tree is null ? "" : "; its tree was removed";
                results.Add(new(now, false, stays == ContentHolds.MovedSince
                    ? $"{ContentHolds.MovedSince}, and is kept" + treeSaid
                    : $"git would not delete the branch: {stays}" + (movedTo is null ? "" : treeSaid))
                {
                    Kept = byItself ? TidyKept.Refused : null,
                    MovedTo = movedTo,
                });
                continue;
            }

            var removed = movedTo is not null ? $"removed; its tree's folder was moved aside to {movedTo}"
                : now.TreeOn is { } on ? $"removed; its tree stays, on `{on}`, where the session's work is"
                : (now.Tree is null ? "removed" : "removed, with its tree") + (left is null ? "" : $". {left}");
            results.Add(new(now, true, kept is null ? removed : $"{Sentence(removed)} {ContentHold.KeptAt(now.Branch, kept)}")
            {
                MovedTo = movedTo,
                KeptRef = kept,
            });
            if (now.CarriedBy is { } carrier)
            {
                try
                {
                    context.Recorded.Carried(carrier, new CarriedBranch(now.Branch, judged, context.Plugins.Now, CarriedBy.CleanUp));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // The branch went on a confirmed answer; only the note of it on the record is lost.
                }
            }
        }

        return results;
    }

    /// <summary>
    /// One session branch judged (D88, PLUGHOOK1a, SQUASHTIDY1), and the commit it was judged at: every question is asked of
    /// that one id, read once, and a removal deletes the branch only while it is still that commit (SQUASHTIDY1c). Null where
    /// git could not read it.
    /// </summary>
    /// <param name="carriers">The completed pull requests the record keeps that carried work here (PLUGHOOK1a); a branch they carried goes.</param>
    /// <param name="byItself">For the look's own tidy (AUTOTIDY2a): an ignored path only the tree holds is no keep, since the look moves the folder aside.</param>
    /// <param name="landings">For the look's own tidy (AUTOTIDY2b): the branches landings made whose work the line holds.</param>
    /// <param name="refs">The judgement's refs (PROC1d-ii); null asks git.</param>
    private async Task<(SweepItem Item, string? Tip)> JudgeAsync(
        string root, string repository, string workspace, string branch, string? tree, string? line,
        HashSet<string> busy, IReadOnlyList<PullRequestCarrier> carriers, CancellationToken ct, string? treeOn = null, bool byItself = false,
        IReadOnlyList<LineLanding>? landings = null, RefSnapshot? refs = null)
    {
        // The ignored paths its tree shares with the checkout, on every row once read (AUTOTIDY1). Whether it made commits since it
        // grew, on every row once its tip is read (AUTOTIDY2a): a tree that never moved is a conversation's place (D137), whatever its kind.
        var shared = 0;
        var worked = false;
        SweepItem Item(string kind, int commits = 0, string? where = null, string? detail = null) =>
            new(repository, workspace, branch, tree, kind, commits, where, detail) { IgnoredShared = shared, TreeOn = treeOn, Worked = worked };

        if (tree is not null && busy.Contains(Normal(tree))) return (Item(SweepKind.InUse), null);

        // A registration whose folder is gone holds nothing on disk; git cannot be asked in it, and its removal says so.
        if (tree is not null && Directory.Exists(tree))
        {
            // Everything no commit holds, whatever git's settings hide (SQUASHTIDY1c): what a removal would destroy.
            var holds = await ContentHolds.HoldsAsync(tree, root, ct).ConfigureAwait(false);
            if (holds is null) return (Item(SweepKind.Dirty, detail: "git could not say what its tree holds") with { Unread = true }, null);
            shared = holds.Shared.Count;
            if (holds.Uncommitted.Count > 0) return (Item(SweepKind.Dirty, detail: $"{holds.Uncommitted.Count} path(s) uncommitted"), null);
            // The press deletes the tree's folder, so what only it holds keeps it; the look moves the folder aside whole for
            // fourteen days, so an ignored path, the tree's own scratch included, is kept there and keeps nothing (AUTOTIDY2a).
            if (!byItself && holds.OwnSaid is { } own) return (Item(SweepKind.Dirty, detail: own), null);
        }

        // From the judgement's refs where they say (PROC1d-ii): a branch they do not hold is git's quiet failure, with no words.
        var (known, atTip) = refs is null ? default : await refs.CommitAsync($"refs/heads/{branch}", ct).ConfigureAwait(false);
        var tipErr = "";
        if (!known)
        {
            var (tipCode, tipOut, tipError) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct)
                .ConfigureAwait(false);
            (atTip, tipErr) = (tipCode == 0 ? tipOut.Trim() : null, tipError);
        }

        if (atTip is not { } tip) return (Item(SweepKind.Unlanded, detail: $"git could not tell: {FirstLine(tipErr)}") with { Unread = true }, null);
        worked = context.Grown.Of(repository, branch) is { } grown && !string.Equals(grown.From, tip, StringComparison.OrdinalIgnoreCase);

        var (code, log, err) = await TreeFollows.UnlandedLogAsync(root, tip, ct).ConfigureAwait(false);
        if (code != 0) return (Item(SweepKind.Unlanded, detail: $"git could not tell: {FirstLine(err)}") with { Unread = true }, null);
        var unlanded = log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (unlanded.Length > 0)
        {
            // PROCESS1b-ii (point 3): a squashing landing's record holds them, as the branch it made would by ancestry, so the row
            // is landed where that branch is, with no ref to keep.
            if (await squashes.SquashedHolderAsync(root, repository, tip, ct, refs).ConfigureAwait(false) is { } squashedOn)
            {
                // AUTOTIDY2a: the look's only once the line holds the branch the landing made, or the work by content.
                var squashOnLine = byItself
                    && (await LineHoldingAsync(root, line, $"refs/heads/{squashedOn.Branch}", ct, refs).ConfigureAwait(false) is not null
                        || (await ContentHolds.HeldByContentAsync(root, tip, line, content.LandedHere(repository), ct, refs).ConfigureAwait(false)).Hold is { } byContent
                            && IsLineForm(byContent.Where, line));
                return (Item(SweepKind.Landed, unlanded.Length, squashedOn.Branch, string.Join('\n', unlanded.Take(3))) with { OnLine = squashOnLine }, tip);
            }

            // PLUGHOOK1a: a squash left these commits on no branch of the person's, and a completed pull request carried them.
            // A branch checked out outside the trees home is the person's to move, so it is never taken on the platform's word.
            if ((tree is null || context.Holds(tree)) && await PullRequestAsks.CarrierOfAsync(root, tip, carriers, ct).ConfigureAwait(false) is { } carrier)
            {
                return (Item(SweepKind.Carried, unlanded.Length, carrier.Entry.Branch, string.Join('\n', unlanded.Take(3))) with { CarriedBy = carrier.Entry }, tip);
            }

            // SQUASHTIDY1: the review's Discard's proof by content, so the list offers what that door would take unforced. Its
            // work is where the row says, and the row is landed. Not a branch checked out outside the trees home, as above.
            if (tree is null || context.Holds(tree))
            {
                var proof = await ContentHolds.HeldByContentAsync(root, tip, line, content.LandedHere(repository), ct, refs).ConfigureAwait(false);
                if (proof.Hold is { } held)
                {
                    return (Item(SweepKind.Landed, unlanded.Length, held.Where, string.Join('\n', unlanded.Take(3)))
                        with { HeldBy = held, OnLine = byItself && IsLineForm(held.Where, line) }, tip);
                }

                // PROOF-UNREAD1: a proof git could not read is no "unlanded" finding: the row is kept as unread, as the reads above are.
                if (proof.Unread is { } why)
                {
                    return (Item(SweepKind.Unlanded, unlanded.Length, detail: $"git could not tell whether its work is held by content: {why}") with { Unread = true }, tip);
                }
            }

            return (Item(SweepKind.Unlanded, unlanded.Length, detail: string.Join('\n', unlanded.Take(3))), tip);
        }

        var against = line is null ? null : await ComparableAsync(root, line, ct, refs).ConfigureAwait(false);
        if (against is not null)
        {
            var (_, ahead, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", $"{against}..{tip}"], ct).ConfigureAwait(false);
            if (ahead.Trim() == "0") return (Item(SweepKind.Empty, where: line), tip);
        }

        // Where it landed, for the list: the first branch of the person's that holds its tip.
        var (_, holders, _) = await WorkingTree.GitAsync(
            root, ["branch", "--all", "--contains", tip, "--format=%(refname:short)"], ct).ConfigureAwait(false);
        var where = holders.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(name => !name.StartsWith("daoris/", StringComparison.Ordinal) && !name.Contains("/daoris/", StringComparison.Ordinal));
        var (_, count, _) = await WorkingTree.GitAsync(
            root, ["rev-list", "--count", tip, "--not", against ?? "HEAD"], ct).ConfigureAwait(false);
        // AUTOTIDY2a: for the look, the form of the line that holds it, which it names; none where only a branch of the person's does.
        var onLine = byItself ? await LineHoldingAsync(root, line, tip, ct, refs).ConfigureAwait(false) : null;
        // AUTOTIDY2b: or the form a landing's branch that holds it was proven on: the line holds this work through that branch, which
        // the look takes after this one, as the press does. Waiting for the line by ancestry, a squash merge would keep both.
        if (byItself && onLine is null) onLine = await LandingHoldingAsync(root, tip, landings, ct).ConfigureAwait(false);
        return (Item(SweepKind.Landed, int.TryParse(count.Trim(), out var n) ? n : 0, onLine ?? where) with { OnLine = onLine is not null }, tip);
    }

    /// <summary>
    /// The form of the line that holds <paramref name="commit"/> by ancestry (AUTOTIDY2a): the line's own branch, else origin's
    /// copy, which a fetch brings a platform's merge to before anyone pulls it. Null where neither does, or git could not say.
    /// </summary>
    private static async Task<string?> LineHoldingAsync(string root, string? line, string commit, CancellationToken ct, RefSnapshot? refs = null)
    {
        foreach (var form in await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false))
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", commit, form.Ref], ct).ConfigureAwait(false);
            if (code == 0) return form.Name;
        }

        return null;
    }

    /// <summary>Whether a content proof's holder is a form of the line (<see cref="LandedProofs.LineFormsAsync"/>'s names), not a branch of the person's.</summary>
    private static bool IsLineForm(string where, string? line) =>
        line is not null && (where == line || where == $"origin/{line}");

    /// <summary>
    /// The form of the line a landing's branch holding <paramref name="tip"/> was proven on (AUTOTIDY2b), the first that holds it;
    /// null where none does, or git could not say.
    /// </summary>
    private static async Task<string?> LandingHoldingAsync(string root, string tip, IReadOnlyList<LineLanding>? landings, CancellationToken ct)
    {
        foreach (var landing in landings ?? [])
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, landing.Tip], ct).ConfigureAwait(false);
            if (code == 0) return landing.Line;
        }

        return null;
    }
}

/// <summary>
/// A branch a landing made whose work the line holds (AUTOTIDY2b): D102's proof found it on <paramref name="Line"/> by content or
/// by ancestry, or its pull request's completed merge is there. A session branch whose commits it holds has its work on the line
/// through it, so the look takes that session branch before it, as the press does, and neither waits on the other.
/// </summary>
/// <param name="Tip">The commit it was judged at.</param>
/// <param name="Line">The form of the line its proof named (<c>main</c>, <c>origin/main</c>).</param>
internal sealed record LineLanding(string Branch, string Tip, string Line);
