using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR6, bringing a repository up to date after its pull request merged: the line pulled by a fast-forward only,
/// the landed branches whose work reached it deleted, and the branches still at work replayed onto it — only their
/// own commits, cut at the commit each grew from.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network</b>: `origin` is a bare repository under the scratch folder, and the
/// platform's squash merge is a second clone of it (<see cref="LandedFixture"/>).
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class TreeSyncTests : LandedFixture
{
    private static readonly HashSet<string> Nobody = [];

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

        // S finishes, and its landing makes a branch at its tip.
        var landed = await trees.LandAsync(step.Path, new LandingSubject("s2", "q2", "Second"));
        Assert.True(landed.Landed, landed.Message);
        var b2 = landed.Branch!;

        var plan2 = await trees.SyncPlanAsync(repositories, Nobody);
        Assert.Equal(PullKind.UpToDate, plan2.Lines.Single().Kind);
        var s = plan2.Rebases.Single(item => item.Branch == step.Branch);
        Assert.Equal(RebaseKind.Replay, s.Kind);
        Assert.Equal(CutBy.Record, s.CutBy);
        Assert.Equal(parent.Branch, s.GrewFrom);
        Assert.Equal(1, s.Commits);
        var l2 = plan2.Rebases.Single(item => item.Branch == b2);
        Assert.True(l2.Landed);
        Assert.Equal(RebaseKind.Replay, l2.Kind);
        Assert.Equal(1, l2.Commits);
        Assert.True(plan2.Deletes.Single(item => item.Branch == b).Removable);

        var done = await trees.SyncAsync(repositories, Nobody);
        Assert.All(done.Rebases, result => Assert.True(result.Replayed, result.Message));
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

    /// <summary>
    /// The line checked out nowhere moves as a ref, from the commit it was judged at; checked out in the repository's
    /// own checkout with uncommitted work, it stays, and so does the work.
    /// </summary>
    [Fact]
    public async Task The_line_moves_as_a_ref_where_nothing_has_it_checked_out_and_stays_under_uncommitted_work()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");

        // On the line, with work in flight: nothing moves, and the work is untouched.
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "in flight\n");
        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);
        Assert.Equal(PullKind.Dirty, plan.Lines.Single().Kind);
        var before = await RevAsync(root, "main");
        var held = await trees.SyncAsync(Repositories(root), Nobody);
        Assert.False(held.Lines.Single().Moved);
        Assert.Equal(before, await RevAsync(root, "main"));
        Assert.Equal("in flight\n", await File.ReadAllTextAsync(Path.Combine(root, "shared.txt")));

        // The person moves to a branch of their own: the line is checked out nowhere, and moves as a ref.
        await GitAsync(root, "checkout", "--quiet", "-b", "mine");
        Assert.Equal(PullKind.FastForward, (await trees.SyncPlanAsync(Repositories(root), Nobody)).Lines.Single().Kind);
        var moved = await trees.SyncAsync(Repositories(root), Nobody);
        Assert.True(moved.Lines.Single().Moved, moved.Lines.Single().Message);
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(root, "main"));
        Assert.Equal("mine", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("in flight\n", await File.ReadAllTextAsync(Path.Combine(root, "shared.txt")));
    }

    /// <summary>A line with commits of its own that origin lacks, and origin with commits it lacks, is left: only a fast-forward is Daoris's.</summary>
    [Fact]
    public async Task A_line_that_diverged_from_origin_is_left()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        await CommitAsync(root, "local.txt", "local\n", "not pushed");
        var before = await RevAsync(root, "main");

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);
        Assert.Equal(PullKind.Diverged, plan.Lines.Single().Kind);
        var done = await trees.SyncAsync(Repositories(root), Nobody);
        Assert.False(done.Lines.Single().Moved);
        Assert.Equal(before, await RevAsync(root, "main"));
    }

    /// <summary>
    /// The list fetches and moves nothing of the person's: every local branch is where it was. A fetch that fails
    /// is named, and the list is made from what the checkout already knows.
    /// </summary>
    [Fact]
    public async Task The_list_moves_no_branch_and_a_fetch_that_fails_is_named()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "work\n", "the session's");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        var heads = await GitAsync(root, "for-each-ref", "refs/heads");

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);
        Assert.Null(plan.Lines.Single().Fetch);
        Assert.Equal(PullKind.FastForward, plan.Lines.Single().Kind);
        Assert.Equal(heads, await GitAsync(root, "for-each-ref", "refs/heads"));

        Directory.Move(origin, origin + ".gone");
        var offline = await trees.SyncPlanAsync(Repositories(root), Nobody);
        Assert.NotNull(offline.Lines.Single().Fetch);
        // What the first fetch brought is still known, so the line still has somewhere to go.
        Assert.Equal(PullKind.FastForward, offline.Lines.Single().Kind);
        Assert.Contains("not fetched", SyncWords.Describe(offline.Lines.Single()));
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
        var holder = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            ArgumentList = { "cat-file", "--batch" },
            WorkingDirectory = tree.Path,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
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

    private static (string, string?, string?)[] Repositories(string root) => [("engine", "aurora", root)];

    /// <summary>
    /// B: a session's work in two commits, landed as a branch; then S, the chain's next step, grown from B's tip with
    /// one commit of its own; then the parent's tree tidied, as a landing rule's tidy leaves it, its work on B.
    /// </summary>
    private static async Task<(TreeOpened Parent, string B, TreeOpened Step)> ParentAndStepAsync(SessionTrees trees, string root)
    {
        var parent = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(parent.Path, "shared.txt", "one\ntwo\n", "b1");
        await CommitAsync(parent.Path, "b.txt", "b\n", "b2");
        var landed = await trees.LandAsync(parent.Path, new LandingSubject("s1", "q1", "First"));
        Assert.True(landed.Landed, landed.Message);

        var step = await trees.OpenAsync(root, "engine", "aurora", from: parent.Branch);
        Assert.Equal(parent.Branch, step.GrewFrom);
        await CommitAsync(step.Path, "s.txt", "s\n", "s1");
        var removed = await trees.RemoveAsync(parent.Path);
        Assert.True(removed.Removed, removed.Message);
        return (parent, landed.Branch!, step);
    }

    /// <summary>A commit someone else pushed to origin's line — the platform's side, through a clone of the bare origin.</summary>
    private async Task PlatformCommitAsync(string origin, string file, string content, string message)
    {
        var platform = Path.Combine(Scratch, $"platform-{Guid.NewGuid():N}"[..20]);
        await GitAsync(Scratch, "clone", "--quiet", origin, platform);
        await CommitAsync(platform, file, content, message);
        await GitAsync(platform, "push", "--quiet", "origin", "main");
    }

    private static async Task<string> RevAsync(string root, string revision) => (await GitAsync(root, "rev-parse", revision)).Trim();

    private static async Task<string[]> SubjectsAsync(string root, string range) =>
        (await GitAsync(root, "log", "--format=%s", range)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
