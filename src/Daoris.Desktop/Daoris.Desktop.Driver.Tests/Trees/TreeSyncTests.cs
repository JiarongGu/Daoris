using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR6, bringing a repository up to date after its pull request merged: the line pulled by a fast-forward only,
/// the landed branches whose work reached it deleted, and the branches still at work replayed onto it — only their
/// own commits, cut at the commit each grew from. The owner's case, the suite's longest test, has this class to itself
/// (PROC1b); the rest of the sync is <see cref="TreeSyncStepTests"/>, <see cref="TreeSyncLineTests"/> and
/// <see cref="TreeSyncReplayTests"/>.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network</b>: `origin` is a bare repository under the scratch folder, and the
/// platform's squash merge is a second clone of it (<see cref="LandedFixture"/>).
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSyncTests : TreeSyncFixture
{
    /// <summary>
    /// The owner's case: a branch B landed from a session and squash-merged by a pull request, so one new commit on
    /// the line holds its content and none of its commits; a session S that grew from B's tip with one commit of its
    /// own, in use and then finished, and the branch its landing made. After the press: the line fast-forwarded, B
    /// deleted, S and its landed branch holding S's one commit on the new line and none of B's, and nothing else touched.
    /// </summary>
    [Fact]
    public async Task After_a_squash_merged_pull_request_the_line_moves_the_parent_goes_and_its_child_keeps_only_its_own_commit()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "topic");
        var topic = await RevAsync(root, "topic");

        var (parent, b, step) = await ParentAndStepAsync(trees, root);
        // Where S's session record says it began: its tree's HEAD at the spawn, which was B's tip.
        var began = await RevAsync(root, b);
        Assert.Equal(began, await trees.ReviewBaseAsync(step.Path, began));
        await GitAsync(root, "push", "--quiet", "origin", b);
        await SquashOnPlatformAsync(origin, b);
        var remote = await GitAsync(origin, "for-each-ref");
        var repositories = Repositories(root);

        // While S's session runs, S is left, and so is B: S still holds B's commits, which the line does not.
        var busy = new HashSet<string> { step.Path };
        var plan = await trees.SyncPlanAsync(repositories, busy);
        Assert.Equal(PullKind.FastForward, Assert.Single(plan.Lines).Kind);
        Assert.Equal(1, plan.Lines[0].Commits);
        Assert.Equal(RebaseKind.InUse, plan.Rebases.Single(item => item.Branch == step.Branch).Kind);
        Assert.Equal(LandedKind.LeanedOn, plan.Deletes.Single(item => item.Branch == b).Kind);
        Assert.NotEqual(await RevAsync(root, "main"), await RevAsync(root, "origin/main"));

        var first = await trees.SyncAsync(repositories, busy);
        Assert.True(first.Lines.Single().Moved, first.Lines.Single().Message);
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(root, "main"));
        Assert.False(first.Rebases.Single(result => result.Item.Branch == step.Branch).Replayed);
        Assert.Contains(b, await GitAsync(root, "branch", "--list"));

        // S finishes, and its landing makes a branch (LANDSYNC1, D87's 2026-10-15 amendment): B's two commits, on the line by content
        // since the squash, are left out, S's own is replayed onto the line, and S's tree follows it, so the sync has nothing to replay.
        var landed = await trees.LandAsync(step.Path, new LandingSubject("s2", "q2", "Second"));
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal([LeftOutWhy.Content, LeftOutWhy.Content], landed.LeftOut!.Select(commit => commit.Why));
        var b2 = landed.Branch!;

        var plan2 = await trees.SyncPlanAsync(repositories, Nobody);
        Assert.Equal(PullKind.UpToDate, plan2.Lines.Single().Kind);
        Assert.Equal(RebaseKind.UpToDate, plan2.Rebases.Single(item => item.Branch == step.Branch).Kind);
        var l2 = plan2.Rebases.Single(item => item.Branch == b2);
        Assert.True(l2.Landed);
        Assert.Equal(RebaseKind.UpToDate, l2.Kind);
        Assert.True(plan2.Deletes.Single(item => item.Branch == b).Removable);

        var done = await trees.SyncAsync(repositories, Nobody);
        Assert.True(done.Deletes.Single(result => result.Item.Branch == b).Removed);

        var main = await RevAsync(root, "main");
        Assert.Equal(["s1"], await SubjectsAsync(root, $"main..{step.Branch}"));
        Assert.Equal(main, await RevAsync(root, $"{step.Branch}~1"));
        Assert.Equal(await RevAsync(root, step.Branch), await RevAsync(root, b2));
        Assert.Equal(["s.txt"], (await GitAsync(root, "diff", "--name-only", "main", step.Branch)).Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain(b, await GitAsync(root, "branch", "--list"));

        // The records follow the move: the landing's branch is still the landing's, and both now grow from the line.
        Assert.Equal(await RevAsync(root, b2), trees.Recorded.Of("engine", b2)!.Tip);
        Assert.Equal(main, trees.Recorded.Of("engine", b2)!.From);
        Assert.Null(trees.Recorded.Of("engine", b));
        Assert.Equal(main, trees.Grown.Of("engine", step.Branch)!.From);
        // Its review measures from the line now: B's tip is no longer in its history, and would show the line's own work.
        Assert.Equal(main, await trees.ReviewBaseAsync(step.Path, began));

        // Nothing else: the person's branch, the checkout, the step's tree, and the remote, which nothing pushed to.
        Assert.Equal(topic, await RevAsync(root, "topic"));
        Assert.Equal("main", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(root, "status", "--porcelain")).Trim());
        Assert.Equal("", (await GitAsync(step.Path, "status", "--porcelain")).Trim());
        Assert.Equal(remote, await GitAsync(origin, "for-each-ref"));

        // Brought up to date, it stays so: a second look finds nothing to do.
        var after = await trees.SyncPlanAsync(repositories, Nobody);
        Assert.All(after.Rebases, item => Assert.Equal(RebaseKind.UpToDate, item.Kind));
        Assert.Empty(after.Deletes);
    }
}
