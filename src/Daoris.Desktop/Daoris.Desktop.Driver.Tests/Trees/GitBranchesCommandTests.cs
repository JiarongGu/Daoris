using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GIT1c (D147 §3.3, D50): <c>daoris-driver git branches</c>, the terminal's read door to the branch list: its words, its
/// scope (D112) and its written form, from lists built here. The read itself is <c>GitBranchesTests</c>'.
/// </summary>
public sealed class GitBranchesCommandTests
{
    private const string Main = "1111111111111111111111111111111111111111";
    private const string Session = "2222222222222222222222222222222222222222";
    private const string Landed = "3333333333333333333333333333333333333333";
    private const string Topic = "5555555555555555555555555555555555555555";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-04T12:00:00Z");

    [Theory]
    [InlineData(new[] { "branches" }, null, false, false)]
    [InlineData(new[] { "branches", "--all" }, null, true, false)]
    [InlineData(new[] { "branches", "--repository", "engine", "--json" }, "engine", false, true)]
    [InlineData(new[] { "branches", "--json", "--all", "--repository", "game" }, "game", true, true)]
    public void The_words_ask_for_the_list(string[] args, string? repository, bool all, bool json)
    {
        var asked = GitBranchesCommand.Read(args, out var problem);

        Assert.Null(problem);
        Assert.Equal(new GitBranchesAsk { Repository = repository, All = all, Json = json }, asked);
    }

    [Theory]
    [InlineData(new string[0], "`branches`")]
    [InlineData(new[] { "fetch" }, "`fetch`")]
    [InlineData(new[] { "branches", "--repository" }, "`--repository`")]
    [InlineData(new[] { "branches", "--repository", "--all" }, "`--repository`")]
    [InlineData(new[] { "branches", "--yes" }, "`--yes`")]
    public void Anything_else_is_the_usage_and_says_what_was_not_understood(string[] args, string named)
    {
        Assert.Null(GitBranchesCommand.Read(args, out var problem));
        Assert.Contains(named, problem);
    }

    /// <summary>D112's scope, as <c>trees sync</c> takes it: those holding Daoris's branches, every one with <c>--all</c>, and a repository named.</summary>
    [Fact]
    public void The_scope_is_d112s()
    {
        var held = new SyncRepository("engine", "aurora", Holds: true);
        var apart = new SyncRepository("game", "aurora", Holds: false);

        Assert.True(GitBranchesCommand.Scope(new GitBranchesAsk()).Includes(held));
        Assert.False(GitBranchesCommand.Scope(new GitBranchesAsk()).Includes(apart));
        Assert.True(GitBranchesCommand.Scope(new GitBranchesAsk { All = true }).Includes(apart));
        Assert.True(GitBranchesCommand.Scope(new GitBranchesAsk { Repository = "game" }).Includes(apart));
    }

    private static GitBranchList List() => new(
        [
            new RepositoryBranches("engine", "aurora", new GitLine("main", LineSource.Workspace) { Commit = Main, Origin = Main, Ahead = 0, Behind = 0 })
            {
                Holds = true,
                Fetch = new GitFetch(Now.AddHours(-3), Heard: true),
                Branches =
                [
                    new GitBranch("main", GitBranchKind.Line, Main) { Worktree = "D:/repos/engine", Ahead = 0, Behind = 0 },
                    new GitBranch("daoris/s-1a2b3c4d", GitBranchKind.Session, Session)
                    {
                        Worktree = "D:/data/trees/aurora/engine/s-1a2b3c4d", Ahead = 2, Behind = 0,
                        Session = new GitSessionBranch { Session = "newest", State = "working", Quest = "21", GrewFrom = "feature/12-even" },
                    },
                    new GitBranch("daoris/s-0f0f0f0f", GitBranchKind.Session, Session) { Ahead = 1, Behind = 3, Session = new GitSessionBranch() },
                    new GitBranch("feature/12-even", GitBranchKind.Landed, Landed)
                    {
                        Ahead = 1, Behind = 0,
                        Landed = new GitLandedBranch("s-even", Now.AddDays(-3), GitOrigin.InStep)
                        {
                            Quest = "12", Title = "Even", Plugin = "azure", Pushed = true, PullRequest = "https://example.test/pr/7", OriginCommit = Landed,
                            PullRequestState = new PullRequestState(PullRequestStates.Open) { PullRequest = "https://example.test/pr/7", Plugin = "azure", AskedAt = Now.AddHours(-1) },
                            PullRequestAskFailed = new PullRequestAskFailed(PluginEvents.Late, "azure", Now.AddMinutes(-30)),
                        },
                    },
                    new GitBranch("feature/15-gone", GitBranchKind.Landed, Landed)
                    {
                        Ahead = 1, Behind = 0, Landed = new GitLandedBranch("s-gone", Now.AddDays(-1), GitOrigin.Gone) { Pushed = true },
                    },
                    new GitBranch("topic", GitBranchKind.Yours, Topic) { Worktree = "D:/scratch/topic", Ahead = 1, Behind = 4 },
                    new GitBranch("older", GitBranchKind.Yours, Topic) { Ahead = 0, Behind = 0 },
                    new GitBranch("origin/release", GitBranchKind.Origin, Topic) { Ahead = 4, Behind = 1 },
                ],
                Commands = ["git for-each-ref \"--format=…\" refs/heads refs/remotes/origin"],
            },
            new RepositoryBranches("old", "aurora", new GitLine("main", LineSource.Checkout) { Commit = Main })
            {
                Holds = true,
                Missing = [GitRefs.WorktreePath, GitRefs.AheadBehind],
                Fetch = new GitFetch(Now.AddDays(-2), Heard: false),
            },
            new RepositoryBranches("broken", "default", new GitLine(null, LineSource.None)) { Holds = true, Problem = "fatal: bad object refs/heads/broken" },
        ],
        [new SyncRepository("game", "aurora", false), new SyncRepository("site", "aurora", false)]);

    [Fact]
    public void Each_repositorys_line_says_what_set_it_how_it_stands_to_origin_and_when_it_was_fetched()
    {
        var said = Said(List());

        Assert.Contains("engine  aurora\n", said);
        Assert.Contains("  line `main`, the line set for its workspace, at 11111111: in step with origin's copy; last fetched 3 hours ago\n", said);
        Assert.Contains("  line the canonical line `main`, at 11111111: origin holds no copy of it here; a fetch was tried 2 days ago and heard nothing\n", said);
        Assert.Contains("  no line: none is set and git names none; never fetched here\n", said);
    }

    [Fact]
    public void Each_branch_is_said_under_its_kind_with_what_names_it()
    {
        var said = Said(List());

        Assert.Contains("  sessions' branches (2)\n", said);
        Assert.Contains("    daoris/s-1a2b3c4d  22222222  2 ahead  session newest (working), quest #21; grew from `feature/12-even`; its tree D:/data/trees/aurora/engine/s-1a2b3c4d\n", said);
        Assert.Contains("    daoris/s-0f0f0f0f  22222222  1 ahead, 3 behind  no session record names its tree; no tree holds it\n", said);
        Assert.Contains("  landed (2)\n", said);
        // PLUGHOOK1c: its pull request's kept state, with when and who answered, and the failed ask beside it.
        Assert.Contains("    feature/12-even  33333333  1 ahead  landed for session s-even, quest #12 \"Even\"; pushed by azure, in step with origin's copy; "
            + "pull request https://example.test/pr/7; its pull request: open, as `azure` answered at 2026-10-04 11:00 UTC; "
            + "asking `azure` again at 2026-10-04 11:30 UTC failed (`late`)\n", said);
        Assert.Contains("    feature/15-gone  33333333  1 ahead  landed for session s-gone; pushed, and gone from origin since\n", said);
        Assert.Contains("  yours (2)\n", said);
        Assert.Contains("    topic  55555555  1 ahead, 4 behind  checked out in D:/scratch/topic\n", said);
        Assert.Contains("    older  55555555  on the line\n", said);
        Assert.Contains("  origin's: 1 branch with no local branch here; `git branch -r` lists them\n", said);
        // The line is said in its head, never again as a row of its own.
        Assert.DoesNotContain("    main  ", said);
    }

    /// <summary>D147 §3.3: a read's terminal door is git itself, so each repository says the git calls that answered it, ready to copy.</summary>
    [Fact]
    public void Each_repository_says_the_git_that_answered_it()
    {
        Assert.Contains("  read by, in its checkout:\n    git for-each-ref \"--format=…\" refs/heads refs/remotes/origin\n", Said(List()));
    }

    [Fact]
    public void A_git_without_an_atom_and_a_repository_git_could_not_read_are_said()
    {
        var said = Said(List());

        Assert.Contains("  this Git counts each branch on its own: `%(ahead-behind)` came in Git 2.41\n", said);
        Assert.Contains("  this Git does not say which tree holds a branch (`%(worktreepath)` came in Git 2.23), so `git worktree list` said it\n", said);
        Assert.Contains("  git could not list its branches: fatal: bad object refs/heads/broken\n", said);
    }

    /// <summary>D112: the repositories holding nothing of Daoris's are named in one line, with how to include them.</summary>
    [Fact]
    public void The_repositories_left_apart_are_named_with_how_to_include_them()
    {
        Assert.EndsWith(
            "git: not listed, since they hold no branch of Daoris's (2): game, site. `--all` lists them, and `--repository <name>` one of them.\n",
            Said(List()));
    }

    [Fact]
    public void Sessions_that_could_not_be_read_are_said_once_first()
    {
        Assert.StartsWith("git: sessions are not named: the service did not answer.\n", GitBranchesCommand.Say(List(), "the service did not answer", Now));
    }

    [Fact]
    public void Nothing_to_list_is_said()
    {
        Assert.Equal("git: no repository has a checkout here.\n", GitBranchesCommand.Say(new GitBranchList([], []), null, Now));
        Assert.StartsWith("git: no repository with a checkout here holds a branch of Daoris's.\n",
            GitBranchesCommand.Say(new GitBranchList([], [new SyncRepository("game", "aurora", false)]), null, Now));
    }

    /// <summary><c>--json</c> writes the list whole, origin's branches included, each field the page's route will carry (GIT1d).</summary>
    [Fact]
    public void The_written_form_holds_the_list_whole()
    {
        using var document = JsonDocument.Parse(GitBranchesCommand.Json(List(), "the service did not answer"));
        var root = document.RootElement;

        Assert.Equal(["repositories", "apart", "sessionsUnread"], root.EnumerateObject().Select(field => field.Name));
        Assert.Equal(["game", "site"], root.GetProperty("apart").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal("the service did not answer", root.GetProperty("sessionsUnread").GetString());

        var engine = root.GetProperty("repositories")[0];
        Assert.Equal(
            ["repository", "workspace", "line", "fetch", "holds", "missing", "problem", "commands", "branches"],
            engine.EnumerateObject().Select(field => field.Name));
        Assert.Equal(
            ["branch", "source", "commit", "origin", "ahead", "behind"],
            engine.GetProperty("line").EnumerateObject().Select(field => field.Name));
        Assert.True(engine.GetProperty("fetch").GetProperty("heard").GetBoolean());
        Assert.Equal(8, engine.GetProperty("branches").GetArrayLength());

        var session = engine.GetProperty("branches")[1];
        Assert.Equal(
            ["name", "kind", "commit", "at", "subject", "worktree", "ahead", "behind", "session", "landed"],
            session.EnumerateObject().Select(field => field.Name));
        Assert.Equal(["session", "state", "quest", "grewFrom", "from"], session.GetProperty("session").EnumerateObject().Select(field => field.Name));
        Assert.Equal(JsonValueKind.Null, session.GetProperty("landed").ValueKind);

        var landed = engine.GetProperty("branches")[3].GetProperty("landed");
        Assert.Equal(
            ["session", "landedAt", "origin", "quest", "title", "plugin", "pushed", "pullRequest", "pushedTip", "originCommit", "originAhead", "originBehind",
                "pullRequestState", "pullRequestAskFailed"],
            landed.EnumerateObject().Select(field => field.Name));
        Assert.Equal(GitOrigin.InStep, landed.GetProperty("origin").GetString());
        // PLUGHOOK1c (D148 point 6): the kept answer whole, as the landing record keeps it, and the failed ask beside it.
        var state = landed.GetProperty("pullRequestState");
        Assert.Equal(
            ["state", "pullRequest", "mergeCommit", "sourceCommit", "target", "how", "at", "message", "plugin", "askedAt"],
            state.EnumerateObject().Select(field => field.Name));
        Assert.Equal((PullRequestStates.Open, "azure"), (state.GetProperty("state").GetString(), state.GetProperty("plugin").GetString()));
        Assert.Equal(["code", "plugin", "at"], landed.GetProperty("pullRequestAskFailed").EnumerateObject().Select(field => field.Name));
        var gone = engine.GetProperty("branches")[4].GetProperty("landed");
        Assert.Equal(JsonValueKind.Null, gone.GetProperty("pullRequestState").ValueKind);
        Assert.Equal(JsonValueKind.Null, gone.GetProperty("pullRequestAskFailed").ValueKind);
        Assert.Equal("origin", engine.GetProperty("branches")[7].GetProperty("kind").GetString());

        var old = root.GetProperty("repositories")[1];
        Assert.Equal([GitRefs.WorktreePath, GitRefs.AheadBehind], old.GetProperty("missing").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("repositories")[2].GetProperty("fetch").ValueKind);
    }

    /// <summary>A repository named that has no checkout here is a refusal, said before git is asked anything.</summary>
    [Fact]
    public async Task A_repository_named_with_no_checkout_here_is_refused()
    {
        var output = new StringWriter();
        var sources = new GitBranchesSources([new RepoView("engine", true, null, "aurora")], DriverConfig.Empty, Path.GetTempPath());

        var exit = await GitBranchesCommand.RunAsync(new GitBranchesAsk { Repository = "engine" }, sources, output);

        Assert.Equal(1, exit);
        Assert.Equal("git: `engine` has no checkout here, so it has no branches here to list.\n", output.ToString().ReplaceLineEndings("\n"));
    }

    private static string Said(GitBranchList list) => GitBranchesCommand.Say(list, null, Now);
}
