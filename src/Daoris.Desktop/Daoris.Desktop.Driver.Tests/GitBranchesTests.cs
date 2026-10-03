using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GIT1a (D147 §2.2, §4.1): the branch list read from real repositories through the git Daoris runs: each kind from its
/// facts, the tree holding a branch, its distance from the line, a landed branch against its copy on origin, the last
/// fetch from <c>FETCH_HEAD</c>, and which repositories D112's scope lists. <c>GitBranchesReadTests</c> holds the calls
/// and the fallbacks with a stand-in; this holds that git answers the format as it is read.
/// </summary>
/// <remarks>🔴 <b>Nothing here reaches a network</b>: <c>origin</c> is a bare repository under the scratch folder (<see cref="LandedFixture"/>).</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitBranchesTests : LandedFixture
{
    /// <summary>
    /// The owner's case on disk: a session's tree with one commit, the branch its landing made and pushed, the person's own
    /// branch with a commit, and a branch only origin holds, fetched. Each is listed under its kind with what git says of it.
    /// </summary>
    [Fact]
    public async Task Every_branch_is_listed_by_its_kind_with_what_git_says_of_it()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "s.txt", "s\n", "s1");
        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s-real", "q1", "First"));
        Assert.True(landed.Landed, landed.Message);
        await GitAsync(root, "push", "--quiet", "origin", landed.Branch!);
        await GitAsync(root, "branch", "topic");
        await CommitOnAsync(root, "topic", "t.txt", "t\n", "t1");
        await PushFromCloneAsync(origin, "release");
        await GitAsync(root, "fetch", "--quiet", "origin");

        var sessions = new[] { new SessionRecord("s-real", "engine", "completed") { Tree = tree.Path, Quest = "q1", Created = DateTimeOffset.UtcNow } };
        var list = await GitBranches.ListAsync([("engine", "aurora", root)], Config(), Home, sessions);

        var engine = Assert.Single(list.Repositories);
        Assert.Null(engine.Problem);
        Assert.Empty(engine.Missing);
        Assert.True(engine.Holds);
        Assert.Equal(
            [("main", GitBranchKind.Line), (tree.Branch, GitBranchKind.Session), (landed.Branch!, GitBranchKind.Landed), ("topic", GitBranchKind.Yours),
             ("origin/release", GitBranchKind.Origin)],
            engine.Branches.Select(branch => (branch.Name, branch.Kind)).OrderBy(each => GitBranchKind.Order.ToList().IndexOf(each.Kind)));

        var main = await RevAsync(root, "main");
        Assert.Equal(new GitLine("main", LineSource.Checkout) { Commit = main, Origin = main, Ahead = 0, Behind = 0 }, engine.Line);
        Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(engine.Branches[0].Worktree!));

        var session = engine.Branches.Single(branch => branch.Kind == GitBranchKind.Session);
        Assert.Equal((1, 0), (session.Ahead, session.Behind));
        Assert.Equal(Path.GetFullPath(tree.Path), Path.GetFullPath(session.Worktree!));
        Assert.Equal(("s-real", "completed", "q1", main), (session.Session!.Session, session.Session.State, session.Session.Quest, session.Session.From));
        Assert.Equal("s1", session.Subject);

        var landing = engine.Branches.Single(branch => branch.Kind == GitBranchKind.Landed);
        Assert.Equal((1, 0, GitOrigin.InStep, "s-real", "q1"), (landing.Ahead, landing.Behind, landing.Landed!.Origin, landing.Landed.Session, landing.Landed.Quest));
        Assert.Null(landing.Worktree);

        var topic = engine.Branches.Single(branch => branch.Name == "topic");
        Assert.Equal((1, 0, await RevAsync(root, "topic")), (topic.Ahead, topic.Behind, topic.Commit));
        Assert.Equal((1, 0), (engine.Branches[^1].Ahead, engine.Branches[^1].Behind));

        // One call for the whole list: nothing here is exceptional.
        Assert.StartsWith("git for-each-ref \"--format=", Assert.Single(engine.Commands));
    }

    /// <summary>A landed branch moved on after it was pushed is still the landing's, and is counted against its copy on origin.</summary>
    [Fact]
    public async Task A_landed_branch_moved_on_after_its_push_is_counted_against_origins_copy()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "s.txt", "s\n", new LandingSubject("s1", "q1", "First"));
        await GitAsync(root, "push", "--quiet", "origin", landed.Branch!);
        await CommitOnAsync(root, landed.Branch!, "more.txt", "more\n", "more");

        var list = await GitBranches.ListAsync([("engine", "aurora", root)], Config(), Home, sessions: null);

        var landing = Assert.Single(list.Repositories).Branches.Single(branch => branch.Name == landed.Branch);
        Assert.Equal(GitBranchKind.Landed, landing.Kind);
        Assert.Equal((GitOrigin.Ahead, 1, 0), (landing.Landed!.Origin, landing.Landed.OriginAhead, landing.Landed.OriginBehind));
        Assert.Equal(3, list.Repositories[0].Commands.Count);
    }

    /// <summary>
    /// The last fetch is <c>FETCH_HEAD</c>'s time in the repository's own git directory, the file git names for it, and a
    /// repository nothing fetched into says never.
    /// </summary>
    [Fact]
    public async Task The_last_fetch_is_the_time_of_the_fetch_head_git_names()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");

        var before = Assert.Single((await GitBranches.ListAsync([("engine", "aurora", root)], Config(), Home, null, SyncScope.Everything)).Repositories);
        Assert.Null(before.Fetch);

        await GitAsync(root, "fetch", "--quiet", "origin");
        var named = Path.GetFullPath(Path.Combine(root, (await GitAsync(root, "rev-parse", "--git-path", "FETCH_HEAD")).Trim()));
        Assert.Equal(named, Path.Combine(GitBranches.GitDirectory(root)!, "FETCH_HEAD"));

        var after = Assert.Single((await GitBranches.ListAsync([("engine", "aurora", root)], Config(), Home, null, SyncScope.Everything)).Repositories);
        Assert.Equal(new GitFetch(new DateTimeOffset(File.GetLastWriteTimeUtc(named), TimeSpan.Zero), Heard: true), after.Fetch);
    }

    /// <summary>
    /// D112's set: by default the repositories holding a branch of Daoris's are listed and every other is named apart;
    /// <c>--all</c> lists every one, and naming one lists it.
    /// </summary>
    [Fact]
    public async Task By_default_a_repository_holding_nothing_of_daoris_is_named_apart()
    {
        var (engine, _) = await RepositoryWithOriginAsync("engine");
        var game = await RepositoryAsync("game");
        await new SessionTrees(Home).OpenAsync(engine, "engine", "aurora");
        (string, string?, string?)[] both = [("engine", "aurora", engine), ("game", "aurora", game)];

        var held = await GitBranches.ListAsync(both, Config(), Home, null);
        Assert.Equal(["engine"], held.Repositories.Select(each => each.Repository));
        Assert.Equal(["game"], held.Apart.Select(each => each.Repository));

        var all = await GitBranches.ListAsync(both, Config(), Home, null, SyncScope.Everything);
        Assert.Equal(["engine", "game"], all.Repositories.Select(each => each.Repository));
        Assert.Empty(all.Apart);

        var named = await GitBranches.ListAsync(both, Config(), Home, null, SyncScope.Named(["game"]));
        Assert.Equal(["engine", "game"], named.Repositories.Select(each => each.Repository));
    }

    /// <summary>
    /// 🔴 git walks up: a registered root that is a folder inside another repository is said, and git is not asked, so the
    /// enclosing repository's branches are never listed as its own.
    /// </summary>
    [Fact]
    public async Task A_root_inside_another_repository_is_said_and_git_is_not_asked()
    {
        var (engine, _) = await RepositoryWithOriginAsync("engine");
        var inside = Directory.CreateDirectory(Path.Combine(engine, "inside")).FullName;

        var list = await GitBranches.ListAsync([("inside", "aurora", inside)], Config(), Home, null);

        var row = Assert.Single(list.Repositories);
        Assert.Contains("not the top of a repository of its own", row.Problem);
        Assert.Empty(row.Branches);
        Assert.Empty(row.Commands);
    }

    private DriverConfig Config() => DriverConfig.Load(Path.Combine(Home, "driver.json"));

    private static async Task<string> RevAsync(string root, string revision) => (await GitAsync(root, "rev-parse", revision)).Trim();

    /// <summary>A branch made on a clone of the bare origin and pushed there, so the checkout holds it only as origin's once fetched.</summary>
    private async Task PushFromCloneAsync(string origin, string branch)
    {
        var clone = Path.Combine(Scratch, $"clone-{Guid.NewGuid():N}"[..14]);
        await GitAsync(Scratch, "clone", "--quiet", origin, clone);
        await GitAsync(clone, "switch", "--quiet", "-c", branch);
        await CommitAsync(clone, "r.txt", "r\n", "r1");
        await GitAsync(clone, "push", "--quiet", "origin", branch);
    }
}
