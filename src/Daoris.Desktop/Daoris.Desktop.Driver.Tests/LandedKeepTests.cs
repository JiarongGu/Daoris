using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR5a, what keeps a landed branch whatever its files say: checked out anywhere, moved since its push,
/// no longer the landing's, moved since the list, not named by the press, or leaned on by a session branch
/// that stays.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LandedKeepTests : LandedFixture
{
    /// <summary>A branch checked out anywhere — the checkout's HEAD or a worktree of the person's — is kept, whatever the proof says.</summary>
    [Fact]
    public async Task A_checked_out_branch_is_kept_even_when_its_work_is_on_the_line()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await SquashAsync(root, b.Branch!);
        var elsewhere = Path.Combine(Scratch, "elsewhere");
        await GitAsync(root, "worktree", "add", "--quiet", elsewhere, b.Branch!);

        var inWorktree = Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), b.Branch!);
        Assert.Equal(LandedKind.CheckedOut, inWorktree.Kind);

        await GitAsync(root, "worktree", "remove", elsewhere);
        await GitAsync(root, "checkout", "--quiet", b.Branch!);
        var atHead = Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), b.Branch!);
        Assert.Equal(LandedKind.CheckedOut, atHead.Kind);

        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Contains(b.Branch!, await GitAsync(root, "branch", "--list"));
    }

    /// <summary>A branch the person pushed and then moved: commits its remote does not have are kept, whatever the line holds.</summary>
    [Fact]
    public async Task A_branch_with_commits_its_remote_lacks_is_kept()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await GitAsync(root, "push", "--quiet", "-u", "origin", b.Branch!);
        await SquashOnPlatformAsync(origin, b.Branch!, deleteBranch: false);
        await GitAsync(root, "fetch", "--quiet", "origin");
        await CommitOnAsync(root, b.Branch!, "later.txt", "later\n", "after the push");

        var item = Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), b.Branch!);

        Assert.Equal(LandedKind.AheadOfRemote, item.Kind);
        Assert.Equal(1, item.Commits);
    }

    /// <summary>
    /// A person's own branch is never judged: one the record never named, and one that took a recorded
    /// name after the landing's branch was deleted — its history is not the landing's.
    /// </summary>
    [Fact]
    public async Task A_branch_that_took_a_recorded_name_since_is_not_judged()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await SquashAsync(root, b.Branch!);
        await GitAsync(root, "branch", "-D", b.Branch!);
        await GitAsync(root, "branch", b.Branch!, "main");
        await CommitOnAsync(root, b.Branch!, "mine.txt", "mine\n", "the person's own");
        // Even one whose files happen to read on the line.
        await GitAsync(root, "branch", "feature/mine", "main~1");

        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Empty(plan.Landed);
        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Contains(b.Branch!, await GitAsync(root, "branch", "--list"));
        Assert.Contains("feature/mine", await GitAsync(root, "branch", "--list"));
        // The stale entry goes: the name is the person's now.
        Assert.Empty(new LandedBranches(Home).All());
    }

    /// <summary>🔴 Checked again right before it goes: a branch that moved since the list keeps its place.</summary>
    [Fact]
    public async Task A_branch_that_moved_after_the_list_is_kept()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await SquashAsync(root, b.Branch!);
        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.True(Landed(plan, b.Branch!).Removable);

        await CommitOnAsync(root, b.Branch!, "late.txt", "late\n", "arrived after the list");
        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>(), only: new HashSet<string> { $"engine:{b.Branch}" });

        var result = done.Landed.Single();
        Assert.False(result.Removed);
        Assert.Contains("changed since the list", result.Message);
        Assert.Contains(b.Branch!, await GitAsync(root, "branch", "--list"));
    }

    /// <summary>Only what the person saw listed to go goes.</summary>
    [Fact]
    public async Task Only_the_branches_the_press_names_go()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await SquashAsync(root, b.Branch!);

        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>(), only: new HashSet<string> { "engine:feature/elsewhere" });

        Assert.Empty(done.Landed);
        Assert.Contains(b.Branch!, await GitAsync(root, "branch", "--list"));
    }

    /// <summary>
    /// A session branch kept in use leans on the landed branch for its own proof (D88 counts a branch of the
    /// person's as holding its commits): the landed branch waits for it. Not in use, both go — the session's first.
    /// </summary>
    [Fact]
    public async Task A_landed_branch_a_kept_session_branch_leans_on_waits_for_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "shared.txt", "one\ntwo\n", "the work");
        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s5", "q5", "Leaned"));
        await SquashAsync(root, landed.Branch!);

        var busy = new HashSet<string> { tree.Path };
        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], busy);
        Assert.Equal(SweepKind.InUse, plan.Sessions.Single().Kind);
        var item = Landed(plan, landed.Branch!);
        Assert.Equal(LandedKind.LeanedOn, item.Kind);
        Assert.Equal(tree.Branch, item.Where);

        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.True(done.Sessions.Single().Removed, done.Sessions.Single().Message);
        Assert.True(done.Landed.Single().Removed, done.Landed.Single().Message);
        Assert.Empty((await GitAsync(root, "branch", "--list", tree.Branch, landed.Branch!)).Trim());
    }
}
