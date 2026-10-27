using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>A branch as judged for the replay, with what the press acts on: its tip, its tree, where its own commits start.</summary>
internal sealed record RebaseJudged(RebaseItem Item, string? Tip = null, string? Tree = null, string? Cut = null)
{
    /// <summary>The commits beyond <see cref="Cut"/>, oldest first, as LANDSYNC3 read them; null where they were not read.</summary>
    public IReadOnlyList<string>? Beyond { get; init; }

    /// <summary>Among <see cref="Beyond"/>, the commits the line holds, which the replay skips (LANDSYNC3).</summary>
    public IReadOnlyList<string> Skips { get; init; } = [];

    /// <summary>
    /// How many commits the line holds that the replay leaves out (LANDSYNC3): those the cut moved past, and the skips. The row's own,
    /// so the list says it as the press does (LANDSYNC3b).
    /// </summary>
    public int LeftOut => Item.LeftOut;
}

/// <summary>
/// Each branch of a repository judged for the replay onto its line (WSR6; PART10j, out of <c>SessionTrees.Sync.cs</c>): the
/// session branches and the landed ones recorded, what keeps each as it is, and where its own commits start.
/// </summary>
/// <remarks>
/// It takes the clean-up for the landing's judgement it reads (<c>JudgeLandedAsync</c>, <c>NotAskedOf</c>; PART10l), and names the
/// proof's statics by their class (<see cref="LandedProofs"/>).
/// </remarks>
internal sealed class BranchJudging(TreeContext context, LandedCleanups cleanups)
{
    /// <summary>
    /// Every session branch and every recorded landed branch of one repository, judged for the replay onto
    /// <paramref name="onto"/> — and the landed branches whose work reached the line, which go instead.
    /// </summary>
    /// <param name="asked">The look's asks (PLUGHOOK1c), for each landed row to say why it was not asked; null at the press, which asks nothing.</param>
    /// <param name="refs">The refs the caller's judgement read (PROC1d-ii); null takes this judgement's own.</param>
    internal async Task<(List<RebaseJudged> Branches, List<LandedItem> Deletes)> JudgeBranchesAsync(
        string root, string repository, string workspace, string? line, string? onto, HashSet<string> busy, CancellationToken ct,
        IReadOnlyList<StateAnswer>? asked = null, RefSnapshot? refs = null)
    {
        // One read of the refs and the working trees for every branch judged here (PROC1d-ii, -iii); a write in this flow drops it.
        refs ??= RefSnapshot.Take(root);
        var worktrees = await WorktreesAsync(root, ct, refs).ConfigureAwait(false);
        var judged = new List<RebaseJudged>();
        foreach (var branch in await LandedProofs.SessionBranchesAsync(root, ct, refs).ConfigureAwait(false))
        {
            var grown = context.Grown.Of(repository, branch);
            judged.Add(await JudgeReplayAsync(
                root, repository, workspace, branch, landed: false, worktrees.GetValueOrDefault(branch), grown?.From, grown?.GrewFrom,
                pushedOnRecord: false, line, onto, busy, ct, refs).ConfigureAwait(false));
        }

        var deletes = new List<LandedItem>();
        var entries = LandedProofs.EntriesOf(context.Recorded.All(), repository);
        if (entries.Count == 0) return (judged, deletes);

        // The session branches that stay as they are after the press: a replayed one shares only the line's commits.
        var staying = judged.Where(each => !each.Item.Replays).Select(each => each.Item.Branch).ToList();
        var config = asked is null ? null : context.Config();
        foreach (var landed in await cleanups.JudgeLandedAsync(root, repository, workspace, entries, staying, stale: null, ct, refs).ConfigureAwait(false))
        {
            // What its pull request's answer was, and why the look did not ask where it would have (PLUGHOOK1c, design §2.5).
            var item = asked is null ? landed.Item : landed.Item with { NotAsked = cleanups.NotAskedOf(landed.Item, asked, config!) };
            if (item.Removable || item.Kind == LandedKind.LeanedOn)
            {
                deletes.Add(item);
                continue;
            }

            var entry = entries.First(each => each.Branch == item.Branch);
            RebaseItem Left(string kind, int commits = 0, string? detail = null) =>
                new(repository, workspace, item.Branch, true, kind, line, null, null, null, commits, detail);
            var replay = item.Kind switch
            {
                LandedKind.Differs => await JudgeReplayAsync(
                    root, repository, workspace, item.Branch, landed: true, worktrees.GetValueOrDefault(item.Branch), entry.From, null,
                    entry.Pushed, line, onto, busy, ct, refs).ConfigureAwait(false),
                LandedKind.CheckedOut => new RebaseJudged(Left(RebaseKind.CheckedOut)),
                LandedKind.AheadOfRemote => new RebaseJudged(Left(RebaseKind.Pushed, item.Commits)),
                _ => new RebaseJudged(Left(RebaseKind.Unknown, detail: item.Detail)),
            };
            judged.Add(replay with
            {
                Item = replay.Item with { State = item.State, StateCode = item.StateCode, AskFailed = item.AskFailed, NotAsked = item.NotAsked },
            });
        }

        return (judged, deletes);
    }

    /// <summary>
    /// One branch, judged for the replay (WSR6): what keeps it as it is, and otherwise where its own commits start.
    /// </summary>
    /// <remarks>
    /// <para><b>Where its own commits start.</b> Where it grew from the line, where it leaves the line — a plain
    /// rebase. Where its record says it started beyond the line — a chain's step on the step before's tip — that
    /// commit, and only once its work reads on the line, by the clean-up's proof by content: then the step
    /// before's commits drop, since a squash merge put their content on the line and none of them. Until then it
    /// waits, since replaying only its own would lose the work it builds on. With no record (a tree opened before
    /// Daoris kept one), the newest commit of it whose work reads on the line, else where it leaves the line.</para>
    /// </remarks>
    /// <param name="refs">The judgement's refs (PROC1d-ii); null asks git.</param>
    internal async Task<RebaseJudged> JudgeReplayAsync(
        string root, string repository, string workspace, string branch, bool landed, string? tree, string? grow, string? grewFrom,
        bool pushedOnRecord, string? line, string? onto, HashSet<string> busy, CancellationToken ct, RefSnapshot? refs = null)
    {
        RebaseJudged Item(string kind, string? tip = null, string? cut = null, string? cutBy = null, int commits = 0, string? detail = null) =>
            new(new RebaseItem(repository, workspace, branch, landed, kind, line, cut is null ? null : TextBounds.ShortCommit(cut, 8), cutBy,
                grewFrom, commits, detail), tip, tree, cut);

        var tip = await CommitOfAsync(root, $"refs/heads/{branch}", ct, refs).ConfigureAwait(false);
        if (tip is null) return Item(RebaseKind.Unknown, detail: "the branch is gone");
        if (onto is null) return Item(RebaseKind.Unknown, tip, detail: line is null ? "no line is set and git names none" : $"there is no `{line}` here, nor `origin/{line}`");

        if (tree is not null)
        {
            // A landing's branch is never replayed where someone has it checked out; a session's only in its own tree.
            if (landed || !context.Holds(tree)) return Item(RebaseKind.CheckedOut, tip);
            if (busy.Contains(Normal(tree))) return Item(RebaseKind.InUse, tip);
            if (!Directory.Exists(tree)) return Item(RebaseKind.Unknown, tip, detail: "its tree is gone from disk, and git still names it (`git worktree prune` forgets it)");
            var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(dirty)) return Item(RebaseKind.Dirty, tip, detail: $"{dirty.Trim().Split('\n').Length} path(s) uncommitted");
        }

        // Where it leaves the line answers whether it already holds the line's tip too, so one start asks both (UIFREEZE1, LANDSYNC3).
        var (baseCode, baseOut, baseErr) = await WorkingTree.GitAsync(root, ["merge-base", tip, onto], ct).ConfigureAwait(false);
        var leaves = baseOut.Trim();
        if (baseCode == 0 && leaves == onto) return Item(RebaseKind.UpToDate, tip);
        if (pushedOnRecord || await OnRemoteAsync(root, branch, ct, refs).ConfigureAwait(false)) return Item(RebaseKind.Pushed, tip);
        if (baseCode != 0) return Item(RebaseKind.Unknown, tip, detail: $"git found no point where it left the line: {FirstLine(baseErr)}");

        string cut;
        string cutBy;
        var walked = false;
        if (grow is not null && await IsAncestorAsync(root, grow, tip, ct).ConfigureAwait(false))
        {
            if (await IsAncestorAsync(root, grow, leaves, ct).ConfigureAwait(false))
            {
                (cut, cutBy) = (leaves, CutBy.Line);
            }
            else
            {
                var proof = await LandedProofs.ProveAsync(root, grow, line, [new LandedProofs.LineForm(onto, onto)], ct).ConfigureAwait(false);
                if (proof.Kind is not (LandedKind.OnLine or LandedKind.Merged)) return Item(RebaseKind.Waits, tip);
                (cut, cutBy) = (grow, CutBy.Record);
            }
        }
        else
        {
            (cut, cutBy) = await CutByContentAsync(root, tip, leaves, line, onto, ct).ConfigureAwait(false);
            walked = true;
        }

        // 🔴 LANDSYNC3 (D87's amendment point 1): judged as a landing judges, so what the line holds is neither counted nor replayed. A
        // branch grown from the line has the walk by content asked too, where it could find a squash of several of its commits.
        if (await LineHolds.JudgeBeyondAsync(root, tip, onto, leaves, cut, line, walk: !walked && cutBy == CutBy.Line, ct).ConfigureAwait(false)
            is not { } held)
        {
            return Item(RebaseKind.Replay, tip, cut, cutBy, await CountAsync(root, $"{cut}..{tip}", ct).ConfigureAwait(false));
        }

        var replay = Item(RebaseKind.Replay, tip, held.Cut, held.ByContent > 0 ? CutBy.Content : cutBy, held.Kept);
        return replay with { Item = replay.Item with { LeftOut = held.LeftOut }, Beyond = held.Commits, Skips = held.Skips };
    }

    /// <summary>How far back the proof by content looks for where a branch's own work starts, before taking where it leaves the line.</summary>
    private const int ContentWalk = 64;

    /// <summary>
    /// Where a branch with no record starts its own work (WSR6): walking back from its tip along its first parents,
    /// the newest commit whose work reads on the line by the clean-up's proof; none, and where it leaves the line.
    /// The landing's judgement of what the line holds walks the same way (<see cref="LineHolds"/>, LANDSYNC1).
    /// </summary>
    internal static async Task<(string Cut, string CutBy)> CutByContentAsync(
        string root, string tip, string leaves, string? line, string onto, CancellationToken ct)
    {
        var (code, list, _) = await WorkingTree.GitAsync(
            root, ["rev-list", "--first-parent", $"--max-count={ContentWalk}", tip, "--not", leaves], ct).ConfigureAwait(false);
        if (code != 0) return (leaves, CutBy.Line);
        foreach (var commit in list.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var proof = await LandedProofs.ProveAsync(root, commit, line, [new LandedProofs.LineForm(onto, onto)], ct).ConfigureAwait(false);
            if (proof.Kind is LandedKind.OnLine or LandedKind.Merged) return (commit, CutBy.Content);
        }

        return (leaves, CutBy.Line);
    }
}
