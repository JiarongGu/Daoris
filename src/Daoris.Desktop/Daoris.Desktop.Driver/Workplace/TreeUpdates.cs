using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// One session's tree brought up to date at the person's press (REVIEWBASE2-i, D164's REVIEWBASE2 note), for what WSR6's look
/// leaves alone: an open conversation's tree, and a tree detached from its branch. The line is fetched once, as the person, and the
/// tree is replayed onto it by WSR6's own replay (D109). PART10k, out of <c>SessionTrees.Update.cs</c>.
/// </summary>
/// <remarks>
/// It takes the judging, the replay and the review's range as <see cref="TreeSync"/> does, and the follow for the layout's
/// <c>BranchOfTree</c> (PART10l).
/// </remarks>
internal sealed class TreeUpdates(
    TreeContext context, TreeFollows follows, BranchJudging judging, BranchReplays replays, ReviewRanges ranges)
{
    /// <summary>
    /// Bring <paramref name="tree"/> up to date with its repository's line, fetched now: a tree on a branch, the session's or one of
    /// its own (TREEFOLLOW1), has its own commits replayed and the branch moved last; a detached tree is rebased in place. Each
    /// refusal is an answer (<see cref="TreeRefusal"/>, asked in <see cref="TreeFacts.First"/>'s order) and leaves the tree as it was.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>This writes into the person's repository</b>, so WSR6's every guard is here: the replay runs as the person, never
    /// a force push and never a push; a conflict aborts it and names the files; the content proof finds nothing of the tree's own
    /// changed but what the line brought before anything is kept; a branch moves only from the commit it was judged at.</para>
    ///
    /// <para><b>Only while nothing runs in the tree</b> (point 3). Whether a turn is taking or queued, or a driven session works,
    /// is the caller's to say, since only the door knows its chat runner: <paramref name="running"/> is asked first, and again once
    /// the repository's trees are held (<see cref="TreeLock"/>, LEFT2) and the line fetched, as late as can be before the replay.
    /// The record's waiting is no refusal here, as it is in WSR6's look.</para>
    ///
    /// <para><b>The line's latest</b> is origin's where the line here is behind it, which is what WSR6's pull would take, else the
    /// line here. The person's own line is not moved: the fetch moves only origin's ref. A line that cannot be fetched is refused in
    /// git's words rather than judged against the last look's fetch (point 2); a repository with no <c>origin</c> has nothing to
    /// fetch, and is brought up to the line it has.</para>
    /// </remarks>
    /// <param name="tree">A session tree this home opened.</param>
    /// <param name="running">Whether something runs in the tree now.</param>
    /// <param name="fetched">The caller fetched the line already (a start, LANDSYNC2), so this fetches nothing and refuses no fetch.</param>
    internal async Task<TreeUpdate> UpdateTreeAsync(
        string tree, Func<CancellationToken, Task<bool>> running, CancellationToken ct = default, bool fetched = false)
    {
        static TreeUpdate Refused(TreeFacts facts) => new(false, TreeUpdateWords.Refused(facts.First()!.Value, facts));

        // One look (UIFREEZE1, PROC1d-i): git's guess at the line is asked once by the update and the judging it runs.
        using var look = context.OneLook();

        // 🔴 Nothing else of a folder that is no tree of this home's is read: git would walk up from it to the repository above.
        var full = Path.GetFullPath(tree);
        var (workspace, repository) = context.OwnerOf(full);
        var facts = new TreeFacts(repository);
        if (!context.Holds(full) || repository.Length == 0 || !await WorkingTree.IsTopLevelAsync(full, ct).ConfigureAwait(false))
        {
            return Refused(facts with { NotOurs = true });
        }

        facts = facts with { Running = await running(ct).ConfigureAwait(false) };
        if (facts.First() is not null) return Refused(facts);
        facts = facts with { Uncommitted = await StatusPathsAsync(full, ct).ConfigureAwait(false) };
        if (facts.First() is not null) return Refused(facts);

        // The repository's own checkout, whose line is the repository's (as the review reads it, REVIEWBASE1), and the tree's
        // branch, in one start (PROC1d-iii). Its branch is taken where that start gave a plain name or a detached HEAD; anything
        // else (no commit yet, or a name git had to tell from another) is asked as before, so the name is the one it was.
        var here = await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        string? branch;
        if (here.Once && here.Branch.Trim() is var named
            && !named.StartsWith("heads/", StringComparison.Ordinal) && !named.StartsWith("refs/", StringComparison.Ordinal))
        {
            branch = named == "HEAD" ? null : named;
        }
        else
        {
            try
            {
                branch = await WorkingTree.BranchAsync(full, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return new(false, TreeUpdateWords.Left(facts, RebaseKind.Unknown, null, error.Message));
            }
        }

        if (here.Code != 0) return new(false, TreeUpdateWords.Left(facts, RebaseKind.Unknown, null, Failure(here.Err)));
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(here.CommonDir.Trim()))!;
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        facts = facts with { Line = line, Branch = branch, NoLine = line is null };
        if (facts.First() is not null) return Refused(facts);

        using var held = TreeLock.TryReplaying(context.Home, workspace, repository);
        facts = facts with { Held = held is null };
        if (facts.First() is not null) return Refused(facts);

        // The one network step (point 2). FetchAsync names no address only where there is no `origin` at all: nothing to fetch.
        var (failed, url) = fetched ? (null, null) : await FetchAsync(root, line, ct, repository, workspace).ConfigureAwait(false);
        var noOrigin = failed is not null && url is null;
        facts = facts with { NotFetched = noOrigin ? null : failed };
        if (facts.First() is not null) return Refused(facts);

        facts = facts with { Running = await running(ct).ConfigureAwait(false) };
        if (facts.First() is not null) return Refused(facts);

        if (await LatestAsync(root, line!, ct).ConfigureAwait(false) is not { } onto)
        {
            return new(false, TreeUpdateWords.Left(facts, RebaseKind.Unknown, null, $"there is no `{line}` here, nor `origin/{line}`"));
        }

        if (await CommitOfAsync(full, "HEAD", ct).ConfigureAwait(false) is not { } head)
        {
            return new(false, TreeUpdateWords.Left(facts, RebaseKind.Unknown, null, "the tree has no commit"));
        }

        facts = facts with
        {
            UpToDate = await IsAncestorAsync(root, onto, head, ct).ConfigureAwait(false),
            OnRemote = branch is not null && await OnRemoteAsync(root, branch, ct).ConfigureAwait(false),
        };
        if (facts.First() is not null) return Refused(facts);

        var done = branch is null
            ? await UpdateDetachedAsync(full, root, line!, onto, head, ct).ConfigureAwait(false)
            : await UpdateBranchAsync(full, root, repository, workspace, line!, onto, branch, facts, ct).ConfigureAwait(false);
        return done.Done && noOrigin ? done with { Message = done.Message + TreeUpdateWords.NoOrigin } : done;
    }

    /// <summary>
    /// A session going on in an old tree starts from the line (LANDSYNC2, D87's 2026-10-15 amendment point 7): before a resume, words
    /// going on, a carry-on or a further part's start in a tree that stands, the line is fetched once, and where it holds some of the
    /// tree's commits (the walk by content or patch id, not merely because it moved) <see cref="UpdateTreeAsync"/> brings the tree up
    /// to date. What the session is told comes back with it.
    /// </summary>
    /// <remarks>
    /// <para><b>Never a reason not to start.</b> A fetch that fails is judged against the last one and said in point 6's words, where the
    /// person's own press refuses it (REVIEWBASE2-i point 2). A conflict, or any reason the press leaves a tree, leaves it as it was and
    /// tells the session to replay its own commits itself.</para>
    ///
    /// <para><b>Before the start's own hold.</b> The replay holds the repository's trees alone (<see cref="TreeLock"/>), which a start
    /// takes shared, so the caller asks this before it takes its own; another start in the repository at that moment is the press's
    /// <c>Held</c>, told as any reason the tree was left.</para>
    ///
    /// <para><b>A start's git, never a look's per tree</b> (UIFREEZE1): nothing where the tree is not this home's or has no line; else
    /// the fetch, the line's latest, where the tree leaves it and <see cref="LineHolds.JudgeBeyondAsync"/>'s one log, the line's files
    /// only where a squash could be read; the press's own only where the line holds some of its commits.</para>
    /// </remarks>
    internal async Task<TreeStartSync> StartInSyncAsync(string tree, CancellationToken ct = default)
    {
        // One look (UIFREEZE1, PROC1d-i): git's guess at the line is asked once by the judgement and the press it may make.
        using var look = context.OneLook();
        var full = Path.GetFullPath(tree);
        var (workspace, repository) = context.OwnerOf(full);
        if (!context.Holds(full) || repository.Length == 0 || !Directory.Exists(full)
            || !await WorkingTree.IsTopLevelAsync(full, ct).ConfigureAwait(false))
        {
            return TreeStartSync.Nothing;
        }

        var here = await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (here.Code != 0) return TreeStartSync.Nothing;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(here.CommonDir.Trim()))!;
        if ((await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch is not { } line) return TreeStartSync.Nothing;

        // The one network step. No `origin` at all is nothing to fetch, and nothing to say: the line here is all there is.
        var (failed, url) = await FetchAsync(root, line, ct, repository, workspace).ConfigureAwait(false);
        var fetch = failed is null || url is null
            ? ""
            : LandingSyncWords.NotFetched(failed, await LastFetchAsync(root, line, ct).ConfigureAwait(false), ReachOf(url), DateTimeOffset.UtcNow);
        TreeStartSync Left() => fetch.Length == 0 ? TreeStartSync.Nothing : new(null, TreeStartWords.HoldsNone(line) + fetch);

        var localLine = await CommitOfAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false);
        if (await LatestAsync(root, line, ct).ConfigureAwait(false) is not { } onto
            || await CommitOfAsync(full, "HEAD", ct).ConfigureAwait(false) is not { } head)
        {
            return Left();
        }

        var (baseCode, baseOut, _) = await WorkingTree.GitAsync(root, ["merge-base", head, onto], ct).ConfigureAwait(false);
        var leaves = baseOut.Trim();
        if (baseCode != 0 || leaves == onto) return Left();

        // Not merely because the line moved (point 7): only where it holds some of the tree's commits.
        if (await LineHolds.JudgeBeyondAsync(root, head, onto, leaves, leaves, line, walk: true, ct).ConfigureAwait(false)
            is not { HoldsAny: true } held)
        {
            return Left();
        }

        var ontoName = onto == localLine ? line : $"origin/{line}";
        var done = await UpdateTreeAsync(full, NothingRuns, ct, fetched: true).ConfigureAwait(false);
        var holds = held.LeftOut + held.ByContent;
        return done is { Done: true, Moved: { } moved }
            ? new(moved, TreeStartWords.Moved(line, ontoName, holds, moved.Commits) + fetch)
            : new(null, TreeStartWords.NotMoved(line, ontoName, holds, done.Message) + fetch);
    }

    /// <summary>
    /// A step the person went on with after reopening the work it grew from (FABRIC-CLOSE1c5, §E5 as CLOSE1c amends it): before its
    /// carried-on session starts, its tree is brought up to date with <paramref name="source"/>'s new submission at
    /// <paramref name="commit"/>, then with the line as any start's (<see cref="StartInSyncAsync"/>).
    /// </summary>
    /// <param name="tree">The stopped session's tree, which the carry-on goes on in.</param>
    /// <param name="source">The quest the step grew from, as the session is told it (<c>#q1</c>).</param>
    /// <param name="commit">The new submission's commit off the wire; null where the host names none.</param>
    /// <remarks>
    /// <para><b>A replay, as the tree sync's</b> (WSR6, D88): the step's own commits, from where its record says it grew (else where it
    /// leaves the commit), are rebased onto the commit in its own tree detached, proven, and its branch moved last from the commit
    /// judged; its record then grows from the commit. Never a merge, which the tree sync never makes either.</para>
    ///
    /// <para><b>A conflict, or any reason it is left</b>, leaves the tree exactly as it was, tells the session, and changes nothing else:
    /// the line's sync is not asked. With no commit (a host before it), or one this repository does not hold, only the line's sync
    /// runs, said once in the session's instruction. Before the start's own hold, as <see cref="StartInSyncAsync"/> is.</para>
    /// </remarks>
    internal async Task<TreeStartSync> GoOnInSyncAsync(
        string tree, string source, string? commit, CancellationToken ct = default, bool broughtForward = false)
    {
        var onto = commit is null
            ? new SubmissionOnto(null, TreeStartWords.NoSubmissionCommit(source, broughtForward), Stops: false)
            : await OntoSubmissionAsync(tree, source, commit, broughtForward, ct).ConfigureAwait(false);
        if (onto.Stops) return new(null, onto.Told);

        var line = await StartInSyncAsync(tree, ct).ConfigureAwait(false);
        var told = string.Join(" ", new[] { onto.Told, line.Told }.Where(each => each is { Length: > 0 }));
        return new(line.Moved ?? onto.Moved, told.Length == 0 ? null : told);
    }

    /// <summary>
    /// The step's tree replayed onto <paramref name="commit"/> (FABRIC-CLOSE1c5), by <see cref="GoOnInSyncAsync"/>'s rules: what moved and
    /// what the session is told, or why it was left and that nothing else may change.
    /// </summary>
    private async Task<SubmissionOnto> OntoSubmissionAsync(string tree, string source, string commit, bool broughtForward, CancellationToken ct)
    {
        var named = TextBounds.ShortCommit(commit, 8);
        SubmissionOnto Left(string why, bool conflict = false) =>
            new(null, TreeStartWords.NotOntoSubmission(source, named, why, conflict), Stops: true);

        using var look = context.OneLook();
        // 🔴 Nothing else of a folder that is no tree of this home's is read: git would walk up from it to the repository above.
        var full = Path.GetFullPath(tree);
        var (workspace, repository) = context.OwnerOf(full);
        if (!context.Holds(full) || repository.Length == 0 || !Directory.Exists(full)
            || !await WorkingTree.IsTopLevelAsync(full, ct).ConfigureAwait(false))
        {
            return SubmissionOnto.Nothing;
        }

        var here = await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (here.Code != 0) return Left($"git could not read the tree: {Failure(here.Err)}");
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(here.CommonDir.Trim()))!;

        // A commit this repository does not hold is no reason to leave the tree: the line is still synced, and that said.
        if (await CommitOfAsync(root, commit, ct).ConfigureAwait(false) is not { } onto)
        {
            return new(null, TreeStartWords.SubmissionNotHere(source, named, broughtForward), Stops: false);
        }

        if (await CommitOfAsync(full, "HEAD", ct).ConfigureAwait(false) is not { } head) return Left("the tree has no commit");
        if (await IsAncestorAsync(root, onto, head, ct).ConfigureAwait(false)) return SubmissionOnto.Nothing;
        if (await StatusPathsAsync(full, ct).ConfigureAwait(false) is > 0 and var dirty)
        {
            return Left($"its tree has uncommitted work ({dirty} path(s)), which a replay does not carry");
        }

        string? branch;
        try
        {
            branch = await WorkingTree.BranchAsync(full, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return Left($"git could not tell: {error.Message}");
        }

        using var held = TreeLock.TryReplaying(context.Home, workspace, repository);
        if (held is null) return Left($"`{repository}`'s trees are held right now, by a session starting in one or by another replay");

        // Where its own commits start: where its record says it grew from, while the tree still holds that, else where it leaves the
        // new submission. Only those are replayed, so the source's old commits it grew on are never replayed as its own.
        var grown = branch is null ? null : context.Grown.Of(repository, branch);
        string cut;
        if (grown is { From: { Length: > 0 } from } && await IsAncestorAsync(root, from, head, ct).ConfigureAwait(false))
        {
            cut = from;
        }
        else
        {
            var (baseCode, baseOut, baseErr) = await WorkingTree.GitAsync(root, ["merge-base", head, onto], ct).ConfigureAwait(false);
            if (baseCode != 0) return Left($"git found no point where it leaves that commit: {Failure(baseErr)}");
            cut = baseOut.Trim();
        }

        // 🔴 Replayed detached, the branch moved last and only from the commit judged, as WSR6's replay of a session's own tree.
        if (branch is not null)
        {
            var (detached, _, detachErr) = await WorkingTree.GitAsync(full, ["switch", "--quiet", "--detach"], ct).ConfigureAwait(false);
            if (detached != 0) return Left($"git would not detach its tree to replay it: {Failure(detachErr)}");
        }

        var (failed, words) = await BranchReplays.RebaseInAsync(full, onto, cut, ct).ConfigureAwait(false);
        if (failed)
        {
            var back = await BackAsync(full, branch, head, ct).ConfigureAwait(false);
            return Left(words + (back ? "" : " Git could not put the tree back where it was: look at it before anything else (`git rebase "
                                             + "--abort`, then `git status`)"), conflict: words.StartsWith("CONFLICT", StringComparison.Ordinal));
        }

        // 🔴 Proven before it is kept: nothing of its own changed but what the new submission brought.
        var now = await CommitOfAsync(full, "HEAD", ct).ConfigureAwait(false);
        var beyond = now is null ? "git could not say where it went" : await BranchReplays.BeyondTheLineAsync(root, head, now, cut, onto, ct).ConfigureAwait(false);
        if (beyond is not null)
        {
            await BackAsync(full, branch, head, ct).ConfigureAwait(false);
            return Left($"the replay changed what the new submission did not bring ({beyond}), so it was not kept");
        }

        if (branch is not null)
        {
            var (moved, _, moveErr) = await WorkingTree.GitAsync(
                root, ["update-ref", "-m", $"daoris: bring {branch} up to date onto {source}'s new submission", $"refs/heads/{branch}", now!, head], ct)
                .ConfigureAwait(false);
            if (moved != 0)
            {
                await BackAsync(full, branch, head, ct).ConfigureAwait(false);
                return Left($"`{branch}` moved while it was replayed: {Failure(moveErr)}");
            }

            await WorkingTree.GitAsync(full, ["switch", "--quiet", branch], ct).ConfigureAwait(false);
        }

        var kept = await CountAsync(root, $"{onto}..{now}", ct).ConfigureAwait(false);
        var told = TreeStartWords.OntoSubmission(source, named, kept);
        // Where it grows from now is the new submission: the record says so, so the next sync and the landing cut there.
        try
        {
            if (branch is not null) context.Grown.GrewOnto(repository, workspace, branch, onto, DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            told += $" Daoris could not record where it now grows from ({error.Message}).";
        }

        return new(new TreeMoved(source, head, now!, branch, kept), told, Stops: false);
    }

    /// <summary>
    /// A tree put back after a replay onto a submission that was not kept (FABRIC-CLOSE1c5): on its branch at the commit judged, or
    /// detached there where it was detached. True where it stands so again.
    /// </summary>
    private static async Task<bool> BackAsync(string tree, string? branch, string head, CancellationToken ct)
    {
        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        if (branch is null) return await PutBackAsync(tree, head, ct).ConfigureAwait(false);
        await WorkingTree.GitAsync(tree, ["switch", "--quiet", branch], ct).ConfigureAwait(false);
        var (_, onOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return onOut.Trim() == branch && await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) == head;
    }

    /// <summary>A start's tree runs nothing yet: the start is what would run in it, and asks this before it does.</summary>
    private static Task<bool> NothingRuns(CancellationToken _) => Task.FromResult(false);

    /// <summary>
    /// A tree on a branch, by WSR6's judgement and replay (<see cref="BranchJudging.JudgeReplayAsync"/>, <see cref="BranchReplays.ReplayAsync"/>): its own commits
    /// cut where its record says it grew from, rebased in its tree detached, proven, and the branch moved last from the commit judged,
    /// its grown ledger recorded. A branch of its own (TREEFOLLOW1) has no entry of its own, and grew where its session branch did.
    /// </summary>
    private async Task<TreeUpdate> UpdateBranchAsync(
        string tree, string root, string repository, string workspace, string line, string onto, string branch, TreeFacts facts,
        CancellationToken ct)
    {
        var grown = context.Grown.Of(repository, branch) ?? (follows.BranchOfTree(tree) is { } layout ? context.Grown.Of(repository, layout.Branch) : null);
        // Nothing is in use by WSR6's word here: what runs in the tree was asked of the caller, inside the hold.
        var judged = await judging.JudgeReplayAsync(
            root, repository, workspace, branch, landed: false, tree, grown?.From, grown?.GrewFrom, pushedOnRecord: false, line, onto,
            busy: [], ct).ConfigureAwait(false);
        if (!judged.Item.Replays)
        {
            return new(false, judged.Item.Kind switch
            {
                RebaseKind.UpToDate => TreeUpdateWords.Refused(TreeRefusal.UpToDate, facts),
                RebaseKind.Pushed => TreeUpdateWords.Refused(TreeRefusal.OnRemote, facts),
                RebaseKind.Dirty => TreeUpdateWords.Refused(
                    TreeRefusal.Dirty, facts with { Uncommitted = await StatusPathsAsync(tree, ct).ConfigureAwait(false) }),
                _ => TreeUpdateWords.Left(facts, judged.Item.Kind, judged.Item.GrewFrom, judged.Item.Detail),
            });
        }

        var result = await replays.ReplayAsync(root, repository, workspace, line, onto, judged, [], ct).ConfigureAwait(false);
        if (!result.Replayed) return new(false, result.Message);
        var now = (await CommitOfAsync(root, $"refs/heads/{branch}", ct).ConfigureAwait(false))!;
        return new(true, result.Message, new TreeMoved(line, judged.Tip!, now, branch, await CountAsync(root, $"{onto}..{now}", ct).ConfigureAwait(false)));
    }

    /// <summary>
    /// A detached tree rebased in place (point 4): its own commits are those after the newest commit it shares with the line
    /// (<see cref="ReviewRanges.ReviewRangeAsync"/>, REVIEWBASE1), so none of the line's is replayed as its own; there is no ref to move. As WSR6
    /// does a session's tree, it is rebased where it stands, aborted on a conflict; the content proof compares the old HEAD and the
    /// new, and where it fails HEAD is put back where it was.
    /// </summary>
    /// <remarks>
    /// No record's base cuts here: a chain's step grown from a parent's tip beyond the line would drop the parent's work it builds on,
    /// which the proof cannot see, since the line lacks that work too (WSR6's <c>Waits</c>). Cut where it leaves the line, every
    /// commit beyond the line is replayed and nothing is lost.
    /// </remarks>
    private async Task<TreeUpdate> UpdateDetachedAsync(string tree, string root, string line, string onto, string head, CancellationToken ct)
    {
        var from = (await ranges.ReviewRangeAsync(tree, "", ct).ConfigureAwait(false)).From;
        if (from.Length == 0)
        {
            return new(false, TreeUpdateWords.Left(new TreeFacts(), RebaseKind.Unknown, null, "git found no point where it left the line"));
        }

        // LANDSYNC3 (D87's 2026-10-15 amendment point 1): what the line holds of its commits is left out, as a landing leaves it out,
        // and the commits it lacks between them are replayed run by run; where git cannot list them, every commit is replayed as before.
        var held = await LineHolds.JudgeBeyondAsync(root, head, onto, from, from, line, walk: true, ct).ConfigureAwait(false);
        var had = held?.Kept ?? await CountAsync(root, $"{from}..{head}", ct).ConfigureAwait(false);

        // 🔴 Read again immediately before: still detached, at the commit judged, clean.
        if (await DetachedAtAsync(tree, head, ct).ConfigureAwait(false) is not true || await StatusPathsAsync(tree, ct).ConfigureAwait(false) > 0)
        {
            return new(false, TreeUpdateWords.MovedSince);
        }

        var (failed, words) = held is { Skips.Count: > 0 }
            ? await BranchReplays.RebaseRunsInAsync(tree, head, onto, held.Commits, held.Skips, ct).ConfigureAwait(false)
            : await BranchReplays.RebaseInAsync(tree, onto, held?.Cut ?? from, ct).ConfigureAwait(false);
        if (failed) return new(false, TreeUpdateWords.NotReplayed(line, words, await PutBackAsync(tree, head, ct).ConfigureAwait(false)));

        // 🔴 Proven before it is kept: nothing of its own changed but what the line brought.
        var now = await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false);
        var beyond = now is null ? "git could not say where it went" : await BranchReplays.BeyondTheLineAsync(root, head, now, from, onto, ct).ConfigureAwait(false);
        if (beyond is not null) return new(false, TreeUpdateWords.NotKept(beyond, await PutBackAsync(tree, head, ct).ConfigureAwait(false)));

        var kept = await CountAsync(root, $"{onto}..{now}", ct).ConfigureAwait(false);
        return new(true, TreeUpdateWords.Replayed(line, kept, had, held?.LeftOut ?? 0), new TreeMoved(line, head, now!, null, kept));
    }

    /// <summary>The paths a tree's <c>git status --porcelain</c> lists: its uncommitted work, untracked files included.</summary>
    private static async Task<int> StatusPathsAsync(string tree, CancellationToken ct)
    {
        var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        return dirty.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    /// <summary>Whether the tree is detached at <paramref name="head"/>; null where git cannot say.</summary>
    private static async Task<bool?> DetachedAtAsync(string tree, string head, CancellationToken ct)
    {
        try
        {
            return await WorkingTree.BranchAsync(tree, ct).ConfigureAwait(false) is null
                   && await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false) == head;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>A detached tree put back where it was after a replay that was not kept: true where it is detached at that commit again.</summary>
    private static async Task<bool> PutBackAsync(string tree, string head, CancellationToken ct)
    {
        await WorkingTree.GitAsync(tree, ["rebase", "--abort"], ct).ConfigureAwait(false);
        if (await DetachedAtAsync(tree, head, ct).ConfigureAwait(false) is true) return true;
        await WorkingTree.GitAsync(tree, ["switch", "--quiet", "--detach", head], ct).ConfigureAwait(false);
        return await DetachedAtAsync(tree, head, ct).ConfigureAwait(false) is true;
    }
}
