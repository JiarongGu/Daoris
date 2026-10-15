using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR6 and WSR7's line: pulled by a fast-forward only, as a ref where nothing has it checked out, never over uncommitted
/// work or a divergence; the list fetches and moves nothing of the person's; and by default only the repositories holding a
/// branch of Daoris's are looked at (D112). Split from <see cref="TreeSyncTests"/> (PROC1b).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSyncLineTests : TreeSyncFixture
{
    /// <summary>
    /// The line checked out nowhere moves as a ref, from the commit it was judged at; checked out in the repository's
    /// own checkout with uncommitted work, it stays, and so does the work. The repository holds nothing of Daoris's,
    /// so it is included as `--all` includes it (D112).
    /// </summary>
    [Fact]
    public async Task The_line_moves_as_a_ref_where_nothing_has_it_checked_out_and_stays_under_uncommitted_work()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");

        // On the line, with work in flight: nothing moves, and the work is untouched.
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "in flight\n");
        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody, scope: SyncScope.Everything);
        Assert.Equal(PullKind.Dirty, plan.Lines.Single().Kind);
        var before = await RevAsync(root, "main");
        var held = await trees.SyncAsync(Repositories(root), Nobody, scope: SyncScope.Everything);
        Assert.False(held.Lines.Single().Moved);
        Assert.Equal(before, await RevAsync(root, "main"));
        Assert.Equal("in flight\n", await File.ReadAllTextAsync(Path.Combine(root, "shared.txt")));

        // The person moves to a branch of their own: the line is checked out nowhere, and moves as a ref.
        await GitAsync(root, "checkout", "--quiet", "-b", "mine");
        Assert.Equal(PullKind.FastForward, (await trees.SyncPlanAsync(Repositories(root), Nobody, scope: SyncScope.Everything)).Lines.Single().Kind);
        var moved = await trees.SyncAsync(Repositories(root), Nobody, scope: SyncScope.Everything);
        Assert.True(moved.Lines.Single().Moved, moved.Lines.Single().Message);
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(root, "main"));
        Assert.Equal("mine", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("in flight\n", await File.ReadAllTextAsync(Path.Combine(root, "shared.txt")));
    }

    /// <summary>
    /// A line with commits of its own that origin lacks, and origin with commits it lacks, is left: only a fast-forward
    /// is Daoris's. Included by name, as `--repository` includes it (D112).
    /// </summary>
    [Fact]
    public async Task A_line_that_diverged_from_origin_is_left()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        await CommitAsync(root, "local.txt", "local\n", "not pushed");
        var before = await RevAsync(root, "main");

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody, scope: SyncScope.Named(["engine"]));
        Assert.Equal(PullKind.Diverged, plan.Lines.Single().Kind);
        var done = await trees.SyncAsync(Repositories(root), Nobody, scope: SyncScope.Named(["engine"]));
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
        // WSR7: the look writes no FETCH_HEAD — only origin's refs are its to move.
        var fetchHead = Path.Combine(root, (await GitAsync(root, "rev-parse", "--git-path", "FETCH_HEAD")).Trim());
        Assert.False(File.Exists(fetchHead));

        Directory.Move(origin, origin + ".gone");
        var offline = await trees.SyncPlanAsync(Repositories(root), Nobody);
        var pull = offline.Lines.Single();
        Assert.NotNull(pull.Fetch);
        // What the first fetch brought is still known, so the line still has somewhere to go.
        Assert.Equal(PullKind.FastForward, pull.Kind);
        Assert.EndsWith("(not fetched)", SyncWords.Describe(pull));
        // Judged against what origin said when the first look fetched it, which moved `origin/main` a moment ago.
        Assert.NotNull(pull.LastFetch);
        Assert.InRange(pull.LastFetch!.Value, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal("file", pull.Reach);
        Assert.Contains("(1): engine (last fetched", string.Join("\n", SyncWords.NotFetched(offline.Lines, DateTimeOffset.UtcNow)));
        Assert.False(File.Exists(fetchHead));
    }

    /// <summary>
    /// WSR7 (D112): by default a look takes only the repositories holding a branch of Daoris's — a session branch, or
    /// one a landing recorded that still stands — and lists every other with a checkout apart, unfetched and unmoved.
    /// Every one is taken when asked for, and a press takes each repository a listed row names.
    /// </summary>
    [Fact]
    public async Task By_default_only_the_repositories_holding_Daoris_branches_are_looked_at_and_the_rest_are_listed_apart()
    {
        var (engine, engineOrigin) = await RepositoryWithOriginAsync("engine");
        var (game, gameOrigin) = await RepositoryWithOriginAsync("game");
        var (tools, toolsOrigin) = await RepositoryWithOriginAsync("tools");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(engine, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "work\n", "the session's");
        // `tools` holds a branch a landing recorded, and nothing else of Daoris's.
        await GitAsync(tools, "branch", "feature/q1-first");
        trees.Recorded.Record(new LandedBranch(
            "tools", "aurora", "feature/q1-first", "main", await RevAsync(tools, "feature/q1-first"), "s1", "q1", "First", DateTimeOffset.UtcNow));
        // `game` has a record whose branch is gone: a record is not a branch.
        trees.Recorded.Record(new LandedBranch(
            "game", "aurora", "feature/q0-gone", "main", new string('0', 40), "s0", "q0", "Gone", DateTimeOffset.UtcNow));
        foreach (var origin in new[] { engineOrigin, gameOrigin, toolsOrigin })
        {
            await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        }

        var gameTip = await RevAsync(game, "main");
        var gameSeen = await RevAsync(game, "origin/main");
        (string, string?, string?)[] all = [("engine", "aurora", engine), ("game", "aurora", game), ("tools", "aurora", tools)];

        Assert.Equal(["engine", "tools"], (await trees.SyncScopeAsync(all)).Where(each => each.Holds).Select(each => each.Repository));

        var plan = await trees.SyncPlanAsync(all, Nobody);
        Assert.Equal(["engine", "tools"], plan.Lines.Select(pull => pull.Repository));
        Assert.Equal(["engine", "tools"], plan.Looked.Select(each => each.Repository));
        Assert.Equal("game", Assert.Single(plan.Apart).Repository);
        // Apart is untouched: not even fetched.
        Assert.Equal(gameSeen, await RevAsync(game, "origin/main"));

        var done = await trees.SyncAsync(all, Nobody, fetch: true);
        Assert.DoesNotContain(done.Lines, result => result.Pull.Repository == "game");
        Assert.Equal(gameTip, await RevAsync(game, "main"));
        Assert.Contains(done.Apart, each => each.Repository == "game");

        // Included, every one or by the rows the person saw listed, it is looked at and moved like any other.
        var everything = await trees.SyncPlanAsync(all, Nobody, scope: SyncScope.Everything);
        Assert.Equal(PullKind.FastForward, everything.Lines.Single(pull => pull.Repository == "game").Kind);
        Assert.Empty(everything.Apart);
        var listed = await trees.SyncAsync(all, Nobody, only: new HashSet<string> { "game:main" });
        Assert.True(listed.Lines.Single(result => result.Pull.Repository == "game").Moved);
        Assert.Equal(await RevAsync(game, "origin/main"), await RevAsync(game, "main"));
    }
}
