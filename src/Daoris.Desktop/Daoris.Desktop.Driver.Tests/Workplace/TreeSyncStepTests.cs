using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR6's replay of a chain's next step once the branch it grew from was squash-merged: cut where its own work begins when
/// no record says where it started, and left while a session is starting in its repository or opened since the list.
/// Split from <see cref="TreeSyncTests"/> (PROC1b).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSyncStepTests : TreeSyncFixture
{
    /// <summary>
    /// A tree opened before Daoris recorded where branches start, and a landing recorded before it kept `from`:
    /// the cut is found by content — the newest commit whose work reads on the line — with the same result.
    /// </summary>
    [Fact]
    public async Task With_no_record_of_where_it_started_the_cut_is_where_its_work_first_differs_from_the_line()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, b, step) = await ParentAndStepAsync(trees, root);
        var landed = await trees.LandAsync(step.Path, new LandingSubject("s2", "q2", "Second"));
        Assert.True(landed.Landed, landed.Message);
        await GitAsync(root, "push", "--quiet", "origin", b);
        await SquashOnPlatformAsync(origin, b);

        File.Delete(trees.Grown.FilePath);
        trees.Recorded.Record(trees.Recorded.Of("engine", landed.Branch!)! with { From = null });

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);
        foreach (var branch in new[] { step.Branch, landed.Branch! })
        {
            var item = plan.Rebases.Single(each => each.Branch == branch);
            Assert.Equal(RebaseKind.Replay, item.Kind);
            Assert.Equal(CutBy.Content, item.CutBy);
            Assert.Equal(1, item.Commits);
        }

        var done = await trees.SyncAsync(Repositories(root), Nobody);
        Assert.All(done.Rebases, result => Assert.True(result.Replayed, result.Message));
        Assert.Equal(["s1"], await SubjectsAsync(root, $"main..{step.Branch}"));
        Assert.Equal(await RevAsync(root, step.Branch), await RevAsync(root, landed.Branch!));
        Assert.DoesNotContain(b, await GitAsync(root, "branch", "--list"));
    }

    /// <summary>
    /// WSR6's open window, closed (LEFT2): while a session is starting in the repository, which holds its trees from
    /// choosing one until its record is open, the press leaves the line's pull and every replay as they were and says why
    /// (GITLOCK1: the pull writes the checkout, under the same hold); and a session a driver opened after the list is seen
    /// by the press's own look inside the hold, so its branch is left in use. Only then, with nothing starting and nothing
    /// in use, does the branch move.
    /// </summary>
    [Fact]
    public async Task A_replay_leaves_a_repository_a_session_is_starting_in_and_sees_one_opened_since_the_list()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, b, step) = await ParentAndStepAsync(trees, root);
        await GitAsync(root, "push", "--quiet", "origin", b);
        await SquashOnPlatformAsync(origin, b);
        var stepTip = await RevAsync(root, step.Branch);
        var line = await RevAsync(root, "main");

        using (var starting = TreeLock.TryStarting(Home, root))
        {
            Assert.NotNull(starting);
            var held = await trees.SyncAsync(Repositories(root), Nobody, fetch: true);

            Assert.False(held.Lines.Single().Moved, held.Lines.Single().Message);
            Assert.Contains("a session was starting in `engine`", held.Lines.Single().Message);
            Assert.Equal(line, await RevAsync(root, "main"));

            // The person pulls the line themselves: the press then has a replay to make, and leaves it too.
            await GitAsync(root, "merge", "--ff-only", "--quiet", "origin/main");
            var replaying = await trees.SyncAsync(Repositories(root), Nobody);
            var left = replaying.Rebases.Single(result => result.Item.Branch == step.Branch);
            Assert.False(left.Replayed);
            Assert.Contains("a session was starting in `engine`", left.Message);
            Assert.Equal(stepTip, await RevAsync(root, step.Branch));
        }

        var opened = await trees.SyncAsync(
            Repositories(root), Nobody, inUseNow: _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { step.Path }));
        var inUse = opened.Rebases.Single(result => result.Item.Branch == step.Branch);
        Assert.Equal(RebaseKind.InUse, inUse.Item.Kind);
        Assert.False(inUse.Replayed);
        Assert.Equal(stepTip, await RevAsync(root, step.Branch));

        var done = await trees.SyncAsync(Repositories(root), Nobody);
        Assert.True(done.Rebases.Single(result => result.Item.Branch == step.Branch).Replayed);
    }
}
