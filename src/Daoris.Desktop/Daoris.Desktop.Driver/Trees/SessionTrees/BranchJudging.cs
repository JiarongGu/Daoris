using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>A branch as judged for the replay, with what the press acts on: its tip, its tree, where its own commits start.</summary>
internal sealed record RebaseJudged(RebaseItem Item, string? Tip = null, string? Tree = null, string? Cut = null);

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
    internal async Task<(List<RebaseJudged> Branches, List<LandedItem> Deletes)> JudgeBranchesAsync(
        string root, string repository, string workspace, string? line, string? onto, HashSet<string> busy, CancellationToken ct,
        IReadOnlyList<StateAnswer>? asked = null)
    {
        var worktrees = await WorktreesAsync(root, ct).ConfigureAwait(false);
        var judged = new List<RebaseJudged>();
        foreach (var branch in await LandedProofs.SessionBranchesAsync(root, ct).ConfigureAwait(false))
        {
            var grown = context.Grown.Of(repository, branch);
            judged.Add(await JudgeReplayAsync(
                root, repository, workspace, branch, landed: false, worktrees.GetValueOrDefault(branch), grown?.From, grown?.GrewFrom,
                pushedOnRecord: false, line, onto, busy, ct).ConfigureAwait(false));
        }

        var deletes = new List<LandedItem>();
        var entries = LandedProofs.EntriesOf(context.Recorded.All(), repository);
        if (entries.Count == 0) return (judged, deletes);

        // The session branches that stay as they are after the press: a replayed one shares only the line's commits.
        var staying = judged.Where(each => !each.Item.Replays).Select(each => each.Item.Branch).ToList();
        var config = asked is null ? null : context.Config();
        foreach (var landed in await cleanups.JudgeLandedAsync(root, repository, workspace, entries, staying, stale: null, ct).ConfigureAwait(false))
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
                    entry.Pushed, line, onto, busy, ct).ConfigureAwait(false),
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
    internal async Task<RebaseJudged> JudgeReplayAsync(
        string root, string repository, string workspace, string branch, bool landed, string? tree, string? grow, string? grewFrom,
        bool pushedOnRecord, string? line, string? onto, HashSet<string> busy, CancellationToken ct)
    {
        RebaseJudged Item(string kind, string? tip = null, string? cut = null, string? cutBy = null, int commits = 0, string? detail = null) =>
            new(new RebaseItem(repository, workspace, branch, landed, kind, line, cut is null ? null : cut[..Math.Min(8, cut.Length)], cutBy,
                grewFrom, commits, detail), tip, tree, cut);

        var tip = await CommitOfAsync(root, $"refs/heads/{branch}", ct).ConfigureAwait(false);
        if (tip is null) return Item(RebaseKind.Unknown, detail: "the branch is gone");
        if (onto is null) return Item(RebaseKind.Unknown, tip, detail: line is null ? "no line is set and git names none" : $"there is no `{line}` here, nor `origin/{line}`");

        if (tree is not null)
        {
            // A landing's branch is never replayed where someone has it checked out; a session's only in its own tree.
            if (landed || !context.Holds(tree)) return Item(RebaseKind.CheckedOut, tip);
            if (busy.Contains(Normal(tree))) return Item(RebaseKind.InUse, tip);
            if (!Directory.Exists(tree)) return Item(RebaseKind.Unknown, tip, detail: "its tree is gone from disk, and git still names it (`git worktree prune` forgets it)");
            var (_, dirty, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(dirty)) return Item(RebaseKind.Dirty, tip, detail: $"{dirty.Trim().Split('\n').Length} path(s) uncommitted");
        }

        if (await IsAncestorAsync(root, onto, tip, ct).ConfigureAwait(false)) return Item(RebaseKind.UpToDate, tip);
        if (pushedOnRecord || await OnRemoteAsync(root, branch, ct).ConfigureAwait(false)) return Item(RebaseKind.Pushed, tip);

        var (baseCode, baseOut, baseErr) = await WorkingTree.GitAsync(root, ["merge-base", tip, onto], ct).ConfigureAwait(false);
        if (baseCode != 0) return Item(RebaseKind.Unknown, tip, detail: $"git found no point where it left the line: {FirstLine(baseErr)}");
        var leaves = baseOut.Trim();

        string cut;
        string cutBy;
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
        }

        return Item(RebaseKind.Replay, tip, cut, cutBy, await CountAsync(root, $"{cut}..{tip}", ct).ConfigureAwait(false));
    }

    /// <summary>How far back the proof by content looks for where a branch's own work starts, before taking where it leaves the line.</summary>
    private const int ContentWalk = 64;

    /// <summary>
    /// Where a branch with no record starts its own work (WSR6): walking back from its tip along its first parents,
    /// the newest commit whose work reads on the line by the clean-up's proof; none, and where it leaves the line.
    /// </summary>
    private static async Task<(string Cut, string CutBy)> CutByContentAsync(
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
