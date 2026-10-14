using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// One session's tree brought up to date at the person's press (REVIEWBASE2-i, D164's REVIEWBASE2 note), for what WSR6's look
/// leaves alone: an open conversation's tree, and a tree detached from its branch. The line is fetched once, as the person, and the
/// tree is replayed onto it by WSR6's own replay (D109).
/// </summary>
public sealed partial class SessionTrees
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
    public async Task<TreeUpdate> UpdateTreeAsync(string tree, Func<CancellationToken, Task<bool>> running, CancellationToken ct = default)
    {
        static TreeUpdate Refused(TreeFacts facts) => new(false, TreeUpdateWords.Refused(facts.First()!.Value, facts));

        // 🔴 Nothing else of a folder that is no tree of this home's is read: git would walk up from it to the repository above.
        var full = Path.GetFullPath(tree);
        var (workspace, repository) = _context.OwnerOf(full);
        var facts = new TreeFacts(repository);
        if (!Holds(full) || repository.Length == 0 || !await WorkingTree.IsTopLevelAsync(full, ct).ConfigureAwait(false))
        {
            return Refused(facts with { NotOurs = true });
        }

        facts = facts with { Running = await running(ct).ConfigureAwait(false) };
        if (facts.First() is not null) return Refused(facts);
        facts = facts with { Uncommitted = await StatusPathsAsync(full, ct).ConfigureAwait(false) };
        if (facts.First() is not null) return Refused(facts);

        string? branch;
        try
        {
            branch = await WorkingTree.BranchAsync(full, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return new(false, TreeUpdateWords.Left(facts, RebaseKind.Unknown, null, error.Message));
        }

        // The repository's own checkout, whose line is the repository's (as the review reads it, REVIEWBASE1).
        var (rootCode, commonDir, rootErr) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)
            .ConfigureAwait(false);
        if (rootCode != 0) return new(false, TreeUpdateWords.Left(facts, RebaseKind.Unknown, null, Failure(rootErr)));
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(commonDir.Trim()))!;
        var line = (await _context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        facts = facts with { Line = line, Branch = branch, NoLine = line is null };
        if (facts.First() is not null) return Refused(facts);

        using var held = TreeLock.TryReplaying(home, workspace, repository);
        facts = facts with { Held = held is null };
        if (facts.First() is not null) return Refused(facts);

        // The one network step (point 2). FetchAsync names no address only where there is no `origin` at all: nothing to fetch.
        var (failed, url) = await FetchAsync(root, line, ct).ConfigureAwait(false);
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
    /// A tree on a branch, by WSR6's judgement and replay (<see cref="BranchJudging.JudgeReplayAsync"/>, <see cref="BranchReplays.ReplayAsync"/>): its own commits
    /// cut where its record says it grew from, rebased in its tree detached, proven, and the branch moved last from the commit judged,
    /// its grown ledger recorded. A branch of its own (TREEFOLLOW1) has no entry of its own, and grew where its session branch did.
    /// </summary>
    private async Task<TreeUpdate> UpdateBranchAsync(
        string tree, string root, string repository, string workspace, string line, string onto, string branch, TreeFacts facts,
        CancellationToken ct)
    {
        var grown = Grown.Of(repository, branch) ?? (BranchOfTree(tree) is { } layout ? Grown.Of(repository, layout.Branch) : null);
        // Nothing is in use by WSR6's word here: what runs in the tree was asked of the caller, inside the hold.
        var judged = await Judging.JudgeReplayAsync(
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

        var result = await Replays.ReplayAsync(root, repository, workspace, line, onto, judged, [], ct).ConfigureAwait(false);
        if (!result.Replayed) return new(false, result.Message);
        var now = (await CommitOfAsync(root, $"refs/heads/{branch}", ct).ConfigureAwait(false))!;
        return new(true, result.Message, new TreeMoved(line, judged.Tip!, now, branch, await CountAsync(root, $"{onto}..{now}", ct).ConfigureAwait(false)));
    }

    /// <summary>
    /// A detached tree rebased in place (point 4): its own commits are those after the newest commit it shares with the line
    /// (<see cref="ReviewRangeAsync"/>, REVIEWBASE1), so none of the line's is replayed as its own; there is no ref to move. As WSR6
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
        var from = (await Ranges.ReviewRangeAsync(tree, "", ct).ConfigureAwait(false)).From;
        if (from.Length == 0)
        {
            return new(false, TreeUpdateWords.Left(new TreeFacts(), RebaseKind.Unknown, null, "git found no point where it left the line"));
        }

        var had = await CountAsync(root, $"{from}..{head}", ct).ConfigureAwait(false);

        // 🔴 Read again immediately before: still detached, at the commit judged, clean.
        if (await DetachedAtAsync(tree, head, ct).ConfigureAwait(false) is not true || await StatusPathsAsync(tree, ct).ConfigureAwait(false) > 0)
        {
            return new(false, TreeUpdateWords.MovedSince);
        }

        var (failed, words) = await BranchReplays.RebaseInAsync(tree, onto, from, ct).ConfigureAwait(false);
        if (failed) return new(false, TreeUpdateWords.NotReplayed(line, words, await PutBackAsync(tree, head, ct).ConfigureAwait(false)));

        // 🔴 Proven before it is kept: nothing of its own changed but what the line brought.
        var now = await CommitOfAsync(tree, "HEAD", ct).ConfigureAwait(false);
        var beyond = now is null ? "git could not say where it went" : await BranchReplays.BeyondTheLineAsync(root, head, now, from, onto, ct).ConfigureAwait(false);
        if (beyond is not null) return new(false, TreeUpdateWords.NotKept(beyond, await PutBackAsync(tree, head, ct).ConfigureAwait(false)));

        var kept = await CountAsync(root, $"{onto}..{now}", ct).ConfigureAwait(false);
        return new(true, TreeUpdateWords.Replayed(line, kept, had), new TreeMoved(line, head, now!, null, kept));
    }

    /// <summary>
    /// The line's latest as this checkout knows it once fetched: origin's where the line here is behind it or absent, which is what
    /// WSR6's pull would take; else the line here. Null where neither is here.
    /// </summary>
    private static async Task<string?> LatestAsync(string root, string line, CancellationToken ct)
    {
        var here = await CommitOfAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false);
        var origin = await CommitOfAsync(root, $"refs/remotes/origin/{line}", ct).ConfigureAwait(false);
        if (here is null || origin is null) return here ?? origin;
        return await IsAncestorAsync(root, here, origin, ct).ConfigureAwait(false) ? origin : here;
    }

    /// <summary>The paths a tree's <c>git status --porcelain</c> lists: its uncommitted work, untracked files included.</summary>
    private static async Task<int> StatusPathsAsync(string tree, CancellationToken ct)
    {
        var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
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
