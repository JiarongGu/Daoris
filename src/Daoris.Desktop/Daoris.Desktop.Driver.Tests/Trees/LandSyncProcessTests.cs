using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDSYNC1b (D87's 2026-10-15 amendment, points 3 and 7, over real git): the landing's sync where LANDSYNC1's own cases did not
/// reach. A branch of the tree's own (TREEFOLLOW1) whose line holds some of its commits is replayed in place and keeps its name, or,
/// on its remote, refused; a chain's branch the landing advances takes only what neither it nor the line holds, replayed onto its tip;
/// and the session's tree follows what landed, or stays and says why, the landing standing either way. Real git in a scratch
/// repository, so this is the suite's Process half; nothing reaches a network (an <c>origin</c> is a bare repository beside it).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandSyncProcessTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private const string Branch = "feature/0fda18-fix-the-gap";

    /// <summary>
    /// Point 3: the tree's own branch, not on its remote, whose first commit the line took as a cherry-pick and changed the same file
    /// after: that commit is left out by its patch id,
    /// the rest replayed in place onto the line's tip, the branch keeps its name, no other branch is made, and the tree stands on it.
    /// </summary>
    [Fact]
    public async Task The_tree_s_own_branch_is_replayed_in_place_leaving_out_what_the_line_holds()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree.Path, "picked.txt", "picked\n", "the part picked");
        var picked = await HeadAsync(tree.Path);
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the rest of the work");
        await PickAsync(root, picked);
        var onLine = await HeadAsync(root);
        // The line goes on with the picked file, so the walk by content cannot read it and its patch id finds it.
        await CommitAsync(root, "picked.txt", "picked\nand the line's own\n", "the line goes on with it");
        var line = await HeadAsync(root);

        var plan = await trees.PlanAsync(tree.Path, Subject);

        var left = Assert.Single(plan.LeftOut!);
        Assert.Equal((picked, LeftOutWhy.Patch, onLine), (left.Sha, left.Why, left.On));
        Assert.Equal(["the rest of the work"], plan.Lands!.Select(commit => commit.Subject));

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("feature/own", landed.Branch);
        Assert.StartsWith("lands `feature/own`, the session's own branch, replayed in place — 1 commit(s) replayed onto `main`, leaving out 1 the line holds: "
            + $"`{picked[..8]}` “the part picked” (on the line as `{onLine[..8]}`, the same change).", landed.Message);
        Assert.Equal(picked, Assert.Single(landed.LeftOut!).Sha);
        Assert.Equal(line, (await GitAsync(root, "rev-parse", "feature/own~1")).Trim());
        Assert.Equal("the rest of the work", (await GitAsync(root, "log", "--format=%s", "main..feature/own")).Trim());
        Assert.Equal((await GitAsync(root, "rev-parse", "feature/own")).Trim(), await HeadAsync(tree.Path));
        Assert.Equal("feature/own", (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
        Assert.Contains("The session's tree now stands on what landed", landed.Message);
        Assert.False(await BranchAsync(root, Branch), "no other branch is made");
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// Point 3, D109: the same branch on its remote is never rewritten, so where the line holds some of it the landing is refused as a
    /// conflict is, with the rebase words naming what the line holds; nothing is made, and the tree, its branch and the remote stand.
    /// </summary>
    [Fact]
    public async Task The_tree_s_own_branch_on_its_remote_is_refused_where_the_line_holds_some_of_it()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree.Path, "picked.txt", "picked\n", "the part picked");
        var picked = await HeadAsync(tree.Path);
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the rest of the work");
        var tip = await HeadAsync(tree.Path);
        await GitAsync(tree.Path, "push", "--quiet", "-u", "origin", "feature/own");
        await PickAsync(root, picked);
        await GitAsync(root, "push", "--quiet", "origin", "main");
        var before = await StateAsync(root, tree);

        var plan = await trees.PlanAsync(tree.Path, Subject);
        var refused = await trees.LandAsync(tree.Path, Subject);

        Assert.Equal(picked, Assert.Single(plan.LeftOut!).Sha);
        Assert.NotNull(plan.Conflict);
        Assert.False(refused.Landed, refused.Message);
        Assert.Equal(AutoLandingCode.Refused, refused.Refusal);
        Assert.StartsWith("Nothing was landed: the line already holds some of this work, and `feature/own` is on its remote, and Daoris does not rewrite "
            + "a pushed branch.", refused.Message);
        Assert.Contains($"`{picked[..8]}` “the part picked”", refused.Conflict!.Words);
        Assert.StartsWith("Rebase your work onto `main`: the line moved since your work began and already holds some of it", refused.Conflict.Words);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", "origin/feature/own")).Trim());
    }

    /// <summary>
    /// Point 3 for an advance: a chain's branch the first landing made, then more work in the same tree, the first of it cherry-picked
    /// onto the line. The press moves the branch on by only what neither it nor the line holds, replayed onto its tip, says what it
    /// left out, keeps the advance, and moves the session's tree to the branch's new tip (point 7).
    /// </summary>
    [Fact]
    public async Task An_advance_takes_only_what_neither_the_branch_nor_the_line_holds_and_the_tree_follows()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the first work");
        var first = await trees.LandAsync(tree.Path, Subject);
        Assert.True(first.Landed, first.Message);
        var standsAt = (await GitAsync(root, "rev-parse", Branch)).Trim();

        await CommitAsync(tree.Path, "picked.txt", "picked\n", "the part picked");
        var picked = await HeadAsync(tree.Path);
        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        await PickAsync(root, picked);
        var onLine = await HeadAsync(root);

        var advanced = await trees.LandAsync(tree.Path, Subject);

        Assert.True(advanced.Landed, advanced.Message);
        Assert.Equal(Branch, advanced.Branch);
        Assert.Equal(standsAt, advanced.AdvancedFrom);
        Assert.StartsWith($"moved `{Branch}` on from {standsAt[..8]} to this session's work — 1 more commit(s), 2 from `main` in all.", advanced.Message);
        Assert.Contains($" It left out 1 the line holds: `{picked[..8]}` “the part picked” (on the line as `{onLine[..8]}`, the same change).",
            advanced.Message);
        Assert.Equal(picked, Assert.Single(advanced.LeftOut!).Sha);
        // What neither held, replayed onto the branch's tip: the branch grows by one commit, and holds no copy of the line's.
        Assert.Equal(standsAt, (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
        Assert.Equal("more of the work\nthe first work", (await GitAsync(root, "log", "--format=%s", $"main..{Branch}")).Trim());
        Assert.Equal("", (await GitAsync(root, "ls-tree", "--name-only", Branch, "picked.txt")).Trim());
        var tip = (await GitAsync(root, "rev-parse", Branch)).Trim();
        var advance = Assert.Single(trees.Recorded.Landing("s1a2b3c4")!.Advances);
        Assert.Equal((standsAt, tip), (advance.From, advance.To));
        // Point 7: the tree follows, on its own branch, clean.
        Assert.Equal(tip, await HeadAsync(tree.Path));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
        Assert.Contains("The session's tree now stands on what landed", advanced.Message);
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>
    /// Point 7's other half: a session still running or waiting holds its tree, so the tree stays at its own commits and the answer
    /// says why; the landing stands, its branch made from the replay.
    /// </summary>
    [Fact]
    public async Task A_tree_a_session_holds_stays_and_the_landing_stands()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "picked.txt", "picked\n", "the part picked");
        var picked = await HeadAsync(tree.Path);
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the rest of the work");
        var tip = await HeadAsync(tree.Path);
        await PickAsync(root, picked);
        var line = await HeadAsync(root);

        var landed = await trees.LandAsync(tree.Path, Subject,
            inUse: _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tree.Path }));

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("The session's tree stays where it was (a session still running or waiting holds it), so its next turn starts from its old commits.",
            landed.Message);
        Assert.DoesNotContain("now stands on what landed", landed.Message);
        Assert.Equal(line, (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
        Assert.Equal("the rest of the work", (await GitAsync(root, "log", "--format=%s", $"main..{Branch}")).Trim());
        Assert.Equal(tip, await HeadAsync(tree.Path));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal(2, await TreesAsync(root));
    }

    /// <summary>The line takes one commit as a cherry-pick, a new id with the same patch.</summary>
    private static Task PickAsync(string root, string commit) =>
        GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", "-x", commit);

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    /// <summary>How many working trees git lists for the checkout: a tree of Daoris's own made for a replay is gone after it.</summary>
    private static async Task<int> TreesAsync(string root) =>
        (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal));

    /// <summary>What a refused landing must leave as it was, as <c>LineHoldsLandingTests</c> reads it.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/", "refs/daoris/")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "status", "--porcelain")).Trim(),
            (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
}
