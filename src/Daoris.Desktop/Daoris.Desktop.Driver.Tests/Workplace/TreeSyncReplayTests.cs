using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR6's replays, and what they leave: a step grown from work not on the line waits, a pushed or dirty branch is left, a
/// conflict is aborted and named, the press acts only on what was listed, and a tree folder held open is said plainly and
/// removed once free. Split from <see cref="TreeSyncTests"/> (PROC1b).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSyncReplayTests : TreeSyncFixture
{
    /// <summary>
    /// A step that grew from a branch whose pull request is still open waits: replaying only its own commits would
    /// lose the work it builds on, and the branch it grew from is on its remote, so it is not Daoris's to replay.
    /// </summary>
    [Fact]
    public async Task A_step_that_grew_from_work_not_on_the_line_waits_and_a_pushed_branch_is_left()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var (_, b, step) = await ParentAndStepAsync(trees, root);
        await GitAsync(root, "push", "--quiet", "origin", b);
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        var bTip = await RevAsync(root, b);
        var stepTip = await RevAsync(root, step.Branch);

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);
        Assert.Equal(PullKind.FastForward, plan.Lines.Single().Kind);
        Assert.Equal(RebaseKind.Waits, plan.Rebases.Single(item => item.Branch == step.Branch).Kind);
        Assert.Equal(RebaseKind.Pushed, plan.Rebases.Single(item => item.Branch == b).Kind);

        var done = await trees.SyncAsync(Repositories(root), Nobody);
        Assert.True(done.Lines.Single().Moved);
        Assert.DoesNotContain(done.Rebases, result => result.Replayed);
        Assert.Equal(bTip, await RevAsync(root, b));
        Assert.Equal(stepTip, await RevAsync(root, step.Branch));
    }

    /// <summary>A conflict aborts the replay and names the file; the branch and its tree are as they were.</summary>
    [Fact]
    public async Task A_replay_that_conflicts_is_aborted_and_named_and_the_branch_is_as_it_was()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "shared.txt", "mine\n", "the session's");
        await PlatformCommitAsync(origin, "shared.txt", "theirs\n", "Someone else's (#8)");
        var tip = await RevAsync(root, tree.Branch);

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);
        Assert.Equal(RebaseKind.Replay, plan.Rebases.Single().Kind);
        Assert.Equal(CutBy.Line, plan.Rebases.Single().CutBy);

        var done = await trees.SyncAsync(Repositories(root), Nobody);
        var result = done.Rebases.Single();
        Assert.False(result.Replayed);
        Assert.Contains("CONFLICT", result.Message);
        Assert.Contains("shared.txt", result.Message);
        Assert.Contains("aborted", result.Message);
        Assert.Equal(tip, await RevAsync(root, tree.Branch));
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
        // The line still moved: each step stands on its own.
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(root, "main"));
    }

    /// <summary>A session branch on its remote, or with uncommitted work in its tree, is left and named.</summary>
    [Fact]
    public async Task A_pushed_session_branch_and_one_with_uncommitted_work_are_left()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var pushed = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(pushed.Path, "p.txt", "p\n", "pushed work");
        await GitAsync(root, "push", "--quiet", "origin", pushed.Branch);
        var dirty = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(dirty.Path, "d.txt", "d\n", "committed work");
        await File.WriteAllTextAsync(Path.Combine(dirty.Path, "d.txt"), "uncommitted\n");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);
        Assert.Equal(RebaseKind.Pushed, plan.Rebases.Single(item => item.Branch == pushed.Branch).Kind);
        Assert.Equal(RebaseKind.Dirty, plan.Rebases.Single(item => item.Branch == dirty.Branch).Kind);

        var done = await trees.SyncAsync(Repositories(root), Nobody);
        Assert.DoesNotContain(done.Rebases, result => result.Replayed);
        Assert.Equal("uncommitted\n", await File.ReadAllTextAsync(Path.Combine(dirty.Path, "d.txt")));
    }

    /// <summary>
    /// The press acts only on the rows the person saw listed: a branch listed is replayed, one not listed is not,
    /// and a row that is no longer what was listed is left and says so.
    /// </summary>
    [Fact]
    public async Task The_press_acts_only_on_what_was_listed()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var one = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(one.Path, "one.txt", "1\n", "one");
        var two = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(two.Path, "two.txt", "2\n", "two");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        await trees.SyncPlanAsync(Repositories(root), Nobody);
        var twoTip = await RevAsync(root, two.Branch);

        var done = await trees.SyncAsync(Repositories(root), Nobody, only: new HashSet<string> { "engine:main", $"engine:{one.Branch}" });

        Assert.True(done.Lines.Single().Moved);
        Assert.True(done.Rebases.Single().Replayed, done.Rebases.Single().Message);
        Assert.Equal(one.Branch, done.Rebases.Single().Item.Branch);
        Assert.Equal(twoTip, await RevAsync(root, two.Branch));
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, $"{one.Branch}~1"));
    }

    /// <summary>
    /// A tidy whose tree's folder something holds open (the first real post-merge run, on Windows): git lets go of the
    /// tree and the folder stays, empty. The removal says so plainly and still removes the branch its proof cleared, and
    /// the clean-up removes the folder once nothing holds it. Elsewhere the folder simply goes.
    /// </summary>
    [Fact]
    public async Task A_tree_folder_held_open_is_said_plainly_and_the_clean_up_removes_it_once_free()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        // Something whose working folder is the tree, as a terminal left open in it would be.
        var holder = Process.Start(TestGit.Past(new ProcessStartInfo
        {
            FileName = "git",
            ArgumentList = { "cat-file", "--batch" },
            WorkingDirectory = tree.Path,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }))!;
        try
        {
            var removed = await trees.RemoveAsync(tree.Path);
            Assert.True(removed.Removed, removed.Message);
            Assert.DoesNotContain(tree.Branch, await GitAsync(root, "branch", "--list"));
            if (Directory.Exists(tree.Path))
            {
                Assert.Contains("is left behind", removed.Message);
                Assert.Contains("holds it open", removed.Message);
            }
        }
        finally
        {
            // `cat-file --batch` ends at the end of its input.
            holder.StandardInput.Close();
            await holder.WaitForExitAsync();
            holder.Dispose();
        }

        await trees.CleanAsync([], Nobody);
        Assert.False(Directory.Exists(tree.Path));
    }
}
