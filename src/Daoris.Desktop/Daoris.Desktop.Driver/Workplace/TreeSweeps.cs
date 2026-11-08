using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeLeftovers;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// The session branches' half of the clean-up (out of <c>SessionTrees.cs</c>):
/// every session branch judged, the list, and the press's one path for both doors, the person's and the look's own. Over the
/// trees' context, the pull request's kept answers, the content proof, the follow's ledger, the squash's record and the look's
/// set-aside. The landed branches' half (<see cref="LandedCleanups"/>) and the look's pass (<see cref="AutoTidies"/>) take it.
/// </summary>
internal sealed class TreeSweeps(
    TreeContext context, PullRequestAsks asks, ContentHolds content, TreeFollows follows, TreeSquashes squashes, TreeSetAside setAside)
{
    /// <summary>
    /// Every session branch in these repositories, with what it holds — the list a person reads
    /// before the clean-up is pressed. Nothing is changed.
    /// </summary>
    /// <param name="inUse">The trees sessions still running or waiting name — kept whatever they hold.</param>
    /// <param name="only">The branches to judge, as <c>repository:branch</c>; null for every one. The look's own tidy judges its few.</param>
    /// <param name="byItself">
    /// Judged for the look's own tidy: an ignored path only the tree holds does not make it <c>dirty</c>, since the
    /// look moves the tree's folder aside rather than deleting it. The press's list and the press keep it <c>dirty</c>.
    /// </param>
    /// <param name="landings">
    /// For the look's own tidy: the branches landings made whose work the line holds, through which a landed session
    /// branch's work is on the line too. Null for the press's list and the press, which take a landed branch wherever it is.
    /// </param>
    /// <param name="slims">
    /// For the look's judgement before its hold: each row the look would take is judged for the package folders its
    /// tree may drop once set aside (<see cref="SweepItem.Droppable"/>), so the hold gains no <c>status --ignored</c> of its own.
    /// </param>
    internal async Task<IReadOnlyList<SweepItem>> SweepPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default, IReadOnlySet<string>? only = null, bool byItself = false, IReadOnlyList<LineLanding>? landings = null,
        bool slims = false)
    {
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var items = new List<SweepItem>();
        foreach (var (repository, workspace, root) in repositories)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            // The repository's refs read once for every branch judged here; a write in this flow drops it.
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
            // A list git could not give is never read as "no tree here": every branch is kept, saying why.
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
            // The completed pull requests the record keeps, confirmed here: nothing is asked at a list or a press.
            var carriers = await asks.CarriersAsync(root, repository, line, ct, refs).ConfigureAwait(false);
            foreach (var branch in named)
            {
                var item = (await JudgeAsync(
                    root, repository, RemoteTarget.Workspace(workspace), branch, worktrees.GetValueOrDefault(branch), line, busy, carriers, ct,
                    follows.MovedOnTo(listed, RemoteTarget.Workspace(workspace), repository, branch), byItself, landings, refs)
                    .ConfigureAwait(false)).Item;
                if (slims && setAside.GoesByItself(item) && item.Tree is { } tree && Directory.Exists(tree))
                {
                    item = item with { Droppable = await TreeSlims.JudgeAsync(tree, ct).ConfigureAwait(false) };
                }

                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// The clean-up: every session branch the proof clears goes, with its tree, and the rest are
    /// kept and named. 🔴 Each is judged again right before it goes — a list is a fact about a moment.
    /// </summary>
    /// <param name="only">The branches the person saw listed to go, as <c>repository:branch</c>; null for every one the proof clears now.</param>
    internal Task<IReadOnlyList<SweepResult>> SweepAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default) =>
        SweepAsync(repositories, inUse, only, byItself: false, ct);

    /// <summary>
    /// The clean-up's one path, for both doors: the person's press takes every row the proof clears
    /// (<see cref="SweepItem.Removable"/>) and removes its tree; the look's own tidy takes the same kinds behind its guards
    /// (<see cref="TreeSetAside.GoesByItself"/>), judges a tree's own ignored paths as no keep, asks its last guards
    /// (<see cref="TreeSetAside.LastLookAsync"/>), and moves the tree's folder aside rather than deleting it
    /// (<see cref="TreeSetAside.MoveAsideAsync"/>). The judgement, the judgement again, the commits a content-held branch keeps on a
    /// ref of their own, and the delete at the commit judged are the same for both.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A destructive step is never cancelled halfway</b>: once a branch's removal begins, each git command is
    /// awaited to its end whatever <paramref name="ct"/> says, so a closing loop never lets go of the repository's hold while
    /// git is still removing. Cancellation is heard between branches.
    /// </remarks>
    /// <param name="landings">The look's: the branches landings made whose work the line holds, as <see cref="SweepPlanAsync"/> takes them.</param>
    /// <param name="droppable">
    /// The look's: by branch, the package folders its judgement before the hold found its tree may drop, which the last look
    /// checks again and the move writes in a manifest beside the folder before it goes. None, and every folder moves whole.
    /// </param>
    internal async Task<IReadOnlyList<SweepResult>> SweepAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only, bool byItself, CancellationToken ct, IReadOnlyList<LineLanding>? landings = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? droppable = null)
    {
        var known = repositories.ToList();
        // One look at each checkout's line for the list and every item after it, the look's own tidy's as the person's.
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
            // Judged again from the refs and the working trees as they are now: never the list's, since a
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
            // The commit of its judgement: the one its delete must still find, and the one a pull request's answer
            // removed, for the record of it. And what that judgement's proof rested on, each ref at the commit it read: the delete verifies exactly these, and nothing looks for them again.
            var rests = new List<(string Ref, string Commit)>();
            // The moment before the judgement reads what the tree holds, the finished
            // folder's threshold, and what it held that no commit holds, which its record names.
            var read = DateTimeOffset.UtcNow;
            var seen = new List<ContentHolds.TreeHolds>();
            var (now, judged) = await JudgeAsync(
                    root, item.Repository, item.Workspace, item.Branch, worktrees.GetValueOrDefault(item.Branch), line, busy, carriers, ct,
                    follows.MovedOnTo(listed, item.Workspace, item.Repository, item.Branch), byItself, landings, fresh, rests, seen)
                .ConfigureAwait(false);
            if (!goes(now) || judged is null)
            {
                results.Add(new(now, false, "it changed since the list, and is kept"));
                continue;
            }

            // The package folders judged before the hold, checked again by the last look's own reads.
            var slim = new List<string>();
            if (byItself && await setAside.LastLookAsync(root, now, ct, droppable?.GetValueOrDefault(now.Branch), slim).ConfigureAwait(false) is { } last)
            {
                results.Add(new(now, false, last.Sentence) { Kept = last.Code, Cause = last.Cause });
                continue;
            }

            // The delete refuses a judgement that rested on nothing, but the tip's holder appended below would
            // hide that, so the branch is kept here, said, before any ref is written or anything moves.
            if (rests.Count == 0)
            {
                results.Add(new(now, false, $"{ContentHolds.NothingRested}, so it is kept"));
                continue;
            }

            // From here each step removes, or finishes a removal: none is cancelled halfway.
            var done = CancellationToken.None;

            // Held by content: its commits are kept on a ref of their own, named here and made in the delete's own act,
            // and wherever the line's own branch does not hold the tip by ancestry, a squash's, a carried branch's,
            // a landing branch's and origin's form of the line included, on both paths: the exact tip is held by a ref that stays.
            string? kept = null;
            RecoveryName? keep = null;
            var lineAt = now.HeldBy is null ? await LineOwnHoldsAsync(root, line, judged, done).ConfigureAwait(false) : null;
            if (lineAt is null)
            {
                var (name, why) = await RecoveryKeeps.NameAsync(root, now.Branch, judged, done).ConfigureAwait(false);
                if (name is null)
                {
                    results.Add(new(now, false, $"Daoris could not keep its commits on a ref of their own ({why}), so it is kept"));
                    continue;
                }

                (keep, kept) = (name, name.Ref);
            }

            // 🔴 The exact tip's holder rests in the delete's own transaction,
            // at the commit read, beside what the judgement rested on: the recovery ref made or verified there at the tip, or the line's own
            // branch at the commit that held it. That holder moved or gone between here and the delete refuses the delete.
            // One verify for each ref: git refuses two for one ref in a transaction, and one the judgement already rests on is verified already.
            if (lineAt is not null && !rests.Any(rest => rest.Ref == $"refs/heads/{line}")) rests.Add(($"refs/heads/{line}", lineAt));
            if (SetAsideFaults.Current?.AfterTipHeld is { } afterHeld) await afterHeld(root).ConfigureAwait(false);

            string? left = null;
            string? movedTo = null;
            if (now.Tree is { } tree && byItself)
            {
                // Every finished tree's manifest carries the record's inputs; one not written keeps the tree.
                var grown = context.Grown.Of(now.Repository, now.Branch);
                var (landing, landingWhy) = LandingOf(now, grown?.Unit);
                var finished = new FinishedTree(
                    read, now.Workspace, tree, line, judged, grown?.From, grown?.Unit,
                    new FinishedHold(now.Hold ?? now.Kind, now.Where, now.Where is null ? null : now.Where.StartsWith("origin/", StringComparison.Ordinal) ? "origin" : "branch"),
                    kept, FinishedLeft.Of(seen.FirstOrDefault()), KeepPolicy.Of(context.Home).SetAside.Whole)
                {
                    Landing = landing,
                    LandingWhy = landingWhy,
                };
                // The look never deletes a tree's folder: a write that lands after its last look lands in the folder moved aside.
                var moved = await setAside.MoveAsideAsync(root, now, done, slim, finished).ConfigureAwait(false);
                if (!moved.Gone)
                {
                    // A branch kept makes no recovery ref, which only the delete's act makes. Where its folder could not be put back, its
                    // manifest stays to be resumed, and names the ref that act would make.
                    results.Add(new(now, false, moved.Sentence) { Kept = moved.Code });
                    continue;
                }

                movedTo = moved.To;
            }
            else if (now.Tree is { } pressed)
            {
                // Each link in the tree goes as a link first, since git's removal follows a junction out of it.
                if (TreeLinks.Unlinked(pressed) is { } linked)
                {
                    results.Add(new(now, false, linked));
                    continue;
                }

                var (removeCode, _, removeErr) = await WorkingTree.GitAsync(
                    root, ["worktree", "remove", pressed], done).ConfigureAwait(false);
                if (removeCode != 0)
                {
                    // A tree git let go of whose folder something holds open: the tree is gone all the same.
                    var (letGo, why) = await LetGoAsync(root, pressed, done).ConfigureAwait(false);
                    if (!letGo)
                    {
                        results.Add(new(now, false, $"git would not remove its tree: {FirstLine(removeErr)}"));
                        continue;
                    }

                    left = why;
                }
            }

            // Only while it is still the commit judged, and in one transaction with what its proof rested on: exactly what the judgement returned.
            var stays = await content.DeleteJudgedAsync(root, now.Repository, now.Branch, judged, rests, done, keep).ConfigureAwait(false);
            // The manifest says the delete's outcome; a refused one is never armed.
            if (movedTo is not null) TreeSetAside.Mark(SlimManifest.PathFor(movedTo), stays is null ? SlimStates.Deleted : SlimStates.Refused, DateTimeOffset.UtcNow);
            // Resume: the delete of a branch with no tree marks the manifest of its folder moved aside before, at that branch and tip.
            // A registration whose folder a failed put-back left aside goes the same way.
            if (stays is null && movedTo is null) setAside.MarkDeletedOf(now.Workspace, now.Repository, now.Branch, judged, DateTimeOffset.UtcNow);
            if (stays is not null)
            {
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
    /// One session branch judged, and the commit of its judgement: every question is asked of
    /// that one id, read once, and a removal deletes the branch only while it is still that commit. Null where
    /// git could not read it.
    /// </summary>
    /// <param name="carriers">The completed pull requests the record keeps that carried work here; a branch they carried goes.</param>
    /// <param name="byItself">For the look's own tidy: an ignored path only the tree holds is no keep, since the look moves the folder aside.</param>
    /// <param name="landings">For the look's own tidy: the branches landings made whose work the line holds.</param>
    /// <param name="refs">The judgement's refs; null asks git.</param>
    /// <param name="rests">Where a removal follows: filled with what the proof that cleared it rested on, each ref
    /// at the commit the proof read, for the delete to verify exactly; null where nothing is removed by this judgement.</param>
    private async Task<(SweepItem Item, string? Tip)> JudgeAsync(
        string root, string repository, string workspace, string branch, string? tree, string? line,
        HashSet<string> busy, IReadOnlyList<PullRequestCarrier> carriers, CancellationToken ct, string? treeOn = null, bool byItself = false,
        IReadOnlyList<LineLanding>? landings = null, RefSnapshot? refs = null, List<(string Ref, string Commit)>? rests = null,
        List<ContentHolds.TreeHolds>? seen = null)
    {
        // The ignored paths its tree shares with the checkout, on every row once read. Whether it made commits since it
        // grew, on every row once its tip is read: a tree that never moved is a conversation's place, whatever its kind.
        var shared = 0;
        var worked = false;
        SweepItem Item(string kind, int commits = 0, string? where = null, string? detail = null) =>
            new(repository, workspace, branch, tree, kind, commits, where, detail) { IgnoredShared = shared, TreeOn = treeOn, Worked = worked };

        if (tree is not null && busy.Contains(Normal(tree))) return (Item(SweepKind.InUse), null);

        // A registration whose folder is gone holds nothing on disk; git cannot be asked in it, and its removal says so.
        if (tree is not null && Directory.Exists(tree))
        {
            // A tree git binds to another checkout is never removed: what git says it holds is that checkout's, so
            // the row is kept as one whose holds could not be read, saying what binds it, on the list as at the press and the look.
            if (await TreeBinding.RefusalAsync(tree, ct).ConfigureAwait(false) is { } bound)
            {
                return (Item(SweepKind.Dirty, detail: bound) with { Unread = true }, null);
            }

            // Everything no commit holds, whatever git's settings hide: what a removal would destroy.
            // The output the repository declares counts only as the owner accepted it for integration.
            var declared = await context.ScratchAsync(root, repository, workspace, ct, line).ConfigureAwait(false);
            var holds = await ContentHolds.HoldsAsync(
                tree, root, declared, ct, new LinePlaces(context.Home).SlotAt(workspace, repository, tree)).ConfigureAwait(false);
            if (holds is null) return (Item(SweepKind.Dirty, detail: "git could not say what its tree holds") with { Unread = true }, null);
            seen?.Add(holds);
            shared = holds.Shared.Count;
            if (holds.Uncommitted.Count > 0) return (Item(SweepKind.Dirty, detail: $"{holds.Uncommitted.Count} path(s) uncommitted"), null);
            // The press deletes the tree's folder, so what only it holds keeps it. The look takes a tree whose only holds are ignored paths,
            // the tree's own scratch included: by the owner's decision a finished session's scratch goes with its tree, named in its record,
            // not kept for fourteen days.
            if (!byItself && holds.OwnSaid is { } own) return (Item(SweepKind.Dirty, detail: own), null);
        }

        // From the judgement's refs where they say: a branch they do not hold is git's quiet failure, with no words.
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
            // A part its chain's tidy ended is on the line by that record alone, resting on the recovery ref the tidy kept its commits
            // on, and judged here no more: the tidy is the one judgement of a part a squashing chain carried.
            if (ChainTidies.Of(context.Recorded.All(), repository, branch, tip) is { Part.KeptOn: { } keptOn } tidied)
            {
                rests?.Add((keptOn, tip));
                return (Item(SweepKind.Landed, unlanded.Length, tidied.Landing.Branch, string.Join('\n', unlanded.Take(3))) with { OnLine = true, Hold = "tidy" }, tip);
            }

            // A squashing landing's record holds them, as the branch it made would by ancestry, so the row
            // is landed where that branch is, with no ref to keep.
            if (await squashes.SquashedHoldAsync(root, repository, tip, ct, refs).ConfigureAwait(false) is { } squash)
            {
                // The record vouches while the landing's branch stands at the commit read, so it rests on that.
                var squashedOn = squash.Entry;
                rests?.Add(($"refs/heads/{squashedOn.Branch}", squash.At));
                // The look's only once the line holds the branch the landing made, or the work by content; and then on that too.
                var squashOnLine = false;
                if (byItself && await LineHoldingAsync(root, line, squash.At, ct, refs).ConfigureAwait(false) is { At: { } formAt } form)
                {
                    squashOnLine = true;
                    rests?.Add((form.Ref, formAt));
                }
                else if (byItself && await ContentHolds.HeldByContentAsync(root, tip, line, content.LandedHere(repository), ct, refs).ConfigureAwait(false)
                             is { Hold: { } byContent } held && IsLineForm(byContent.Where, line))
                {
                    squashOnLine = true;
                    rests?.AddRange(held.Rests);
                }

                return (Item(SweepKind.Landed, unlanded.Length, squashedOn.Branch, string.Join('\n', unlanded.Take(3))) with { OnLine = squashOnLine, Hold = "squash" }, tip);
            }

            // A squash left these commits on no branch of the person's, and a completed pull request carried them.
            // A branch checked out outside the trees home is the person's to move, so it is never taken on the platform's word.
            if ((tree is null || context.Holds(tree)) && await PullRequestAsks.CarrierOfAsync(root, tip, carriers, ct).ConfigureAwait(false) is { } carrier)
            {
                rests?.AddRange(carrier.Rests);
                return (Item(SweepKind.Carried, unlanded.Length, carrier.Entry.Branch, string.Join('\n', unlanded.Take(3))) with { CarriedBy = carrier.Entry, Hold = "carried" }, tip);
            }

            // The review's Discard's proof by content, so the list offers what that door would take unforced. Its
            // work is where the row says, and the row is landed. Not a branch checked out outside the trees home, as above.
            if (tree is null || context.Holds(tree))
            {
                var proof = await ContentHolds.HeldByContentAsync(root, tip, line, content.LandedHere(repository), ct, refs).ConfigureAwait(false);
                if (proof.Hold is { } held)
                {
                    // The ref it found the work on, at the commit it compared.
                    rests?.AddRange(proof.Rests);
                    return (Item(SweepKind.Landed, unlanded.Length, held.Where, string.Join('\n', unlanded.Take(3)))
                        with { HeldBy = held, OnLine = byItself && IsLineForm(held.Where, line), Hold = "content" }, tip);
                }

                // A proof git could not read is no "unlanded" finding: the row is kept as unread, as the reads above are.
                if (proof.Unread is { } why)
                {
                    return (Item(SweepKind.Unlanded, unlanded.Length, detail: $"git could not tell whether its work is held by content: {why}") with { Unread = true }, tip);
                }
            }

            return (Item(SweepKind.Unlanded, unlanded.Length, detail: string.Join('\n', unlanded.Take(3))), tip);
        }

        var against = line is null ? null : await ComparableAsync(root, line, ct, refs).ConfigureAwait(false);
        // The line is compared at the commit read, which an empty branch's removal rests on.
        var againstAt = against is null ? null : await TreeRefs.CommitOfAsync(root, against, ct, refs).ConfigureAwait(false);
        if (against is not null && againstAt is not null)
        {
            var (_, ahead, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", $"{againstAt}..{tip}"], ct).ConfigureAwait(false);
            if (ahead.Trim() == "0")
            {
                rests?.Add((against, againstAt));
                return (Item(SweepKind.Empty, where: line) with { Hold = "empty" }, tip);
            }
        }

        // Where it landed, for the list: the first branch of the person's that holds its tip. And the holder its removal rests on, out of the same read: a form of the line first, else a branch of the person's, each at the commit read.
        if (await ContentHolds.HoldersAsync(root, tip, branch, ct).ConfigureAwait(false) is not { } holders)
        {
            return (Item(SweepKind.Unlanded, detail: "git could not list the branches that hold it") with { Unread = true }, null);
        }

        if (ContentHolds.HolderOf(holders, line, ContentHolds.PersonsRef) is not { } holder)
        {
            return (Item(SweepKind.Unlanded, detail: "the branch of yours that held it went as it was judged"), null);
        }

        rests?.Add(holder);
        var where = holders.Select(pair => pair.Ref).Where(ContentHolds.PersonsRef).Select(ContentHolds.Named).FirstOrDefault();
        var (_, count, _) = await WorkingTree.GitAsync(
            root, ["rev-list", "--count", tip, "--not", againstAt ?? "HEAD"], ct).ConfigureAwait(false);
        // For the look, the form of the line that holds it, which it names; none where only a branch of the person's does.
        var onLine = byItself && line is not null && (holder.Ref == $"refs/heads/{line}" || holder.Ref == $"refs/remotes/origin/{line}")
            ? ContentHolds.Named(holder.Ref)
            : null;
        // Or the form a landing's branch that holds it is proven on: the line holds this work through that branch, which
        // the look takes after this one, as the press does. Waiting for the line by ancestry, a squash merge would keep both.
        var hold = onLine is not null ? "line" : "branch";
        string? landingBranch = null;
        if (byItself && onLine is null && await LandingHoldingAsync(root, tip, landings, ct).ConfigureAwait(false) is { } landing)
        {
            onLine = landing.Line;
            hold = "landing";
            landingBranch = landing.Branch;
            // And on what that landing's proof rested on, so the line rewound since its proof keeps it.
            rests?.Add(($"refs/heads/{landing.Branch}", landing.Tip));
            rests?.AddRange(landing.Rests);
        }

        return (Item(SweepKind.Landed, int.TryParse(count.Trim(), out var n) ? n : 0, onLine ?? where)
            with { OnLine = onLine is not null, Hold = hold, LandingBranch = landingBranch }, tip);
    }

    /// <summary>
    /// The form of the line that holds <paramref name="commit"/> by ancestry: the line's own branch, else origin's
    /// copy, which a fetch brings a platform's merge to before anyone pulls it. Null where neither does, or git could not say. Asked at
    /// the commit the form is read at, which a removal on it rests on.
    /// </summary>
    private static async Task<LandedProofs.LineForm?> LineHoldingAsync(string root, string? line, string commit, CancellationToken ct, RefSnapshot? refs = null)
    {
        foreach (var form in await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false))
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", commit, form.Rev], ct).ConfigureAwait(false);
            if (code == 0) return form;
        }

        return null;
    }

    /// <summary>
    /// Whether the line's own branch, <c>refs/heads/&lt;line&gt;</c>, holds <paramref name="tip"/> by ancestry: the one hold
    /// that needs no recovery ref. Any other answer, origin's copy alone included, writes one.
    /// </summary>
    /// <returns>The commit the line's own branch is read at, where it holds the tip; null where it does not, or git could not say.</returns>
    private static async Task<string?> LineOwnHoldsAsync(string root, string? line, string tip, CancellationToken ct)
    {
        if (line is null) return null;
        var (readCode, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], ct).ConfigureAwait(false);
        if (readCode != 0 || at.Trim() is not { Length: > 0 } commit) return null;
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, commit], ct).ConfigureAwait(false);
        return code == 0 ? commit : null;
    }

    /// <summary>
    /// The landing that carried a finished tree's work, from the landings' record before the tree goes:
    /// the one its hold names (a landing's or a squash's branch), else the one its work unit is among; none, with why, where neither is
    /// recorded. Never a judgement's or a move's moment in place of the landing's.
    /// </summary>
    private (FinishedLanding? Landing, string? Why) LandingOf(SweepItem item, string? unit)
    {
        var entries = context.Recorded.All().Where(entry => string.Equals(entry.Repository, item.Repository, StringComparison.OrdinalIgnoreCase)).ToList();
        // A landing's hold names the form of the line in `Where`, so its landing is found by its own branch.
        var by = item.Hold switch { "landing" => item.LandingBranch, "squash" => item.Where, _ => null };
        var found = (by is null ? null : entries.FirstOrDefault(entry => entry.Branch == by))
            ?? (unit is null ? null : entries.Where(entry => entry.Units.Contains(unit, StringComparer.Ordinal)).OrderByDescending(entry => entry.LandedAt).FirstOrDefault());
        return found is null
            ? (null, unit is null ? "no work unit names its branch, so no landing could be found for it" : "no landing is recorded for its work unit")
            : (new FinishedLanding(found.Branch, found.Line, found.LandedAt, found.PullRequest), null);
    }

    /// <summary>Whether a content proof's holder is a form of the line (<see cref="LandedProofs.LineFormsAsync"/>'s names), not a branch of the person's.</summary>
    private static bool IsLineForm(string where, string? line) =>
        line is not null && (where == line || where == $"origin/{line}");

    /// <summary>
    /// The form of the line a landing's branch holding <paramref name="tip"/> is proven on, the first that holds it;
    /// null where none does, or git could not say. A removal on it rests on that branch at the tip judged, and
    /// on what its own proof rested on: a landing whose proof names nothing it rested on holds nothing here.
    /// </summary>
    private static async Task<LineLanding?> LandingHoldingAsync(string root, string tip, IReadOnlyList<LineLanding>? landings, CancellationToken ct)
    {
        foreach (var landing in (landings ?? []).Where(each => each.Rests.Count > 0))
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, landing.Tip], ct).ConfigureAwait(false);
            if (code == 0) return landing;
        }

        return null;
    }
}

/// <summary>
/// A branch a landing made whose work the line holds: the landing's proof found it on <paramref name="Line"/> by content or
/// by ancestry, or its pull request's completed merge is there. A session branch whose commits it holds has its work on the line
/// through it, so the look takes that session branch before it, as the press does, and neither waits on the other.
/// </summary>
/// <param name="Tip">The commit it is judged at.</param>
/// <param name="Line">The form of the line its proof named (<c>main</c>, <c>origin/main</c>).</param>
internal sealed record LineLanding(string Branch, string Tip, string Line)
{
    /// <summary>
    /// What its own proof rested on, each ref at the commit read: the form of the line it is proved on. A
    /// session branch's removal through it rests on these too, so the line rewound since keeps that branch. None, and it holds nothing.
    /// </summary>
    internal IReadOnlyList<(string Ref, string Commit)> Rests { get; init; } = [];
}
