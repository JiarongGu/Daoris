using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GIT1a (D147 §2.2, §4.1): one repository's branch list read through a stand-in for git, so what is asked, in what order
/// and how often is held without a process: one <c>for-each-ref</c>, a call more only where a landed branch is exceptional,
/// and each fallback for a git without an atom. <c>GitBranchesTests</c> reads the same list from real repositories.
/// </summary>
public sealed class GitBranchesReadTests
{
    private const string Main = "1111111111111111111111111111111111111111";
    private const string OriginMain = "9999999999999999999999999999999999999999";
    private const string Session = "2222222222222222222222222222222222222222";
    private const string Landed = "3333333333333333333333333333333333333333";
    private const string Moved = "4444444444444444444444444444444444444444";
    private const string Topic = "5555555555555555555555555555555555555555";
    private const string Pushed = "6666666666666666666666666666666666666666";
    private const string Elsewhere = "7777777777777777777777777777777777777777";

    private static string Record(
        string name, string commit, string distance = "", string worktree = "", string symref = "", string at = "2026-10-04T01:00:00Z",
        string subject = "work") =>
        string.Join('\0', name, commit, at, symref, worktree, distance, subject) + "\0\n";

    /// <summary>A stand-in for git: an answer per command, and every command asked, in order. Anything unscripted fails as git would.</summary>
    private sealed class StandInGit
    {
        private readonly Dictionary<string, (int Code, string Stdout, string Stderr)> _answers = new(StringComparer.Ordinal);

        public List<string> Asked { get; } = [];

        public StandInGit Answer(IReadOnlyList<string> arguments, string stdout, int code = 0, string stderr = "")
        {
            _answers[string.Join(' ', arguments)] = (code, stdout, stderr);
            return this;
        }

        public Task<(int Code, string Stdout, string Stderr)> Run(IReadOnlyList<string> arguments, CancellationToken ct)
        {
            var key = string.Join(' ', arguments);
            Asked.Add(key);
            return Task.FromResult(_answers.TryGetValue(key, out var answer) ? answer : (128, "", $"fatal: not scripted: {key}"));
        }
    }

    private static GitRepositoryAsk Ask(string? line = "main") => new("engine", "aurora", new Line(line, line is null ? LineSource.None : LineSource.Workspace))
    {
        Landings =
        [
            new LandedBranch("engine", "aurora", "feature/12-even", "main", Landed, "s-even", "12", "Even", DateTimeOffset.Parse("2026-10-01T00:00:00Z"))
            {
                Plugin = "azure", Pushed = true, PullRequest = "https://example.test/pr/7", PushedTip = Landed,
            },
            new LandedBranch("engine", "aurora", "feature/13-ahead", "main", Landed, "s-ahead", "13", "Ahead", DateTimeOffset.Parse("2026-10-02T00:00:00Z")),
            new LandedBranch("engine", "aurora", "feature/14-mine", "main", Landed, "s-mine", "14", "Mine", DateTimeOffset.Parse("2026-10-02T00:00:00Z")),
            new LandedBranch("engine", "aurora", "feature/15-gone", "main", Landed, "s-gone", null, null, DateTimeOffset.Parse("2026-10-03T00:00:00Z"))
            {
                Pushed = true,
            },
            // Another repository's landing of the same name is not this one's.
            new LandedBranch("game", "aurora", "topic", "main", Topic, "s-game", null, null, DateTimeOffset.Parse("2026-10-03T00:00:00Z")),
        ],
        Grown = [new GrownBranch("engine", "aurora", "daoris/s-1a2b3c4d", "main", Main, "feature/12-even", DateTimeOffset.Parse("2026-10-03T00:00:00Z"))],
        Sessions =
        [
            new SessionRecord("older", "engine", "completed") { Tree = "D:/data/trees/aurora/engine/s-1a2b3c4d", Created = DateTimeOffset.Parse("2026-10-03T00:00:00Z") },
            new SessionRecord("newest", "engine", "working") { Tree = @"D:\data\trees\aurora\engine\s-1a2b3c4d", Quest = "21", Created = DateTimeOffset.Parse("2026-10-04T00:00:00Z") },
            new SessionRecord("other", "game", "working") { Tree = "D:/data/trees/aurora/game/s-1a2b3c4d", Created = DateTimeOffset.Parse("2026-10-05T00:00:00Z") },
        ],
    };

    /// <summary>The repository the owner's case reads: its line, a session's branch, four landed, the person's own, and origin's.</summary>
    private static string Everything() =>
        Record("refs/heads/main", Main, "0 0", worktree: "D:/repos/engine")
        + Record("refs/heads/daoris/s-1a2b3c4d", Session, "2 0", worktree: "D:/data/trees/aurora/engine/s-1a2b3c4d", at: "2026-10-04T03:00:00Z")
        + Record("refs/heads/feature/12-even", Landed, "1 0", at: "2026-10-01T00:00:00Z")
        + Record("refs/heads/feature/13-ahead", Moved, "3 0", at: "2026-10-02T00:00:00Z")
        + Record("refs/heads/feature/14-mine", Topic, "1 4", at: "2026-10-02T00:00:00Z")
        + Record("refs/heads/feature/15-gone", Landed, "1 0", at: "2026-10-03T00:00:00Z")
        + Record("refs/heads/topic", Topic, "1 4", worktree: "D:/scratch/topic", at: "2026-10-04T02:00:00Z")
        + Record("refs/heads/older", Elsewhere, "5 9", at: "2026-09-01T00:00:00Z")
        + Record("refs/remotes/origin/HEAD", OriginMain, "0 2", symref: "refs/remotes/origin/main")
        + Record("refs/remotes/origin/main", OriginMain, "0 2")
        + Record("refs/remotes/origin/feature/12-even", Landed, "1 0")
        + Record("refs/remotes/origin/feature/13-ahead", Pushed, "2 0")
        + Record("refs/remotes/origin/release", Elsewhere, "4 1");

    private static StandInGit Common() => new StandInGit()
        .Answer(GitRefs.Arguments(trees: true, against: "refs/heads/main"), Everything())
        // feature/13-ahead moved since its landing: still the landing's while its history holds the recorded tip.
        .Answer(["merge-base", "--is-ancestor", Landed, "refs/heads/feature/13-ahead"], "")
        // feature/14-mine took the recorded name since: its history does not hold the tip, so it is the person's.
        .Answer(["merge-base", "--is-ancestor", Landed, "refs/heads/feature/14-mine"], "", code: 1)
        .Answer(["rev-list", "--left-right", "--count", "refs/heads/feature/13-ahead...refs/remotes/origin/feature/13-ahead"], "1\t0\n");

    [Fact]
    public async Task One_for_each_ref_names_every_branch_by_its_kind()
    {
        var git = Common();

        var read = await GitBranches.ReadAsync(Ask(), git.Run);

        Assert.Null(read.Problem);
        Assert.Empty(read.Missing);
        Assert.True(read.Holds);
        Assert.Equal(
            [
                ("main", GitBranchKind.Line),
                ("daoris/s-1a2b3c4d", GitBranchKind.Session),
                ("feature/15-gone", GitBranchKind.Landed),
                ("feature/13-ahead", GitBranchKind.Landed),
                ("feature/12-even", GitBranchKind.Landed),
                ("topic", GitBranchKind.Yours),
                ("feature/14-mine", GitBranchKind.Yours),
                ("older", GitBranchKind.Yours),
                ("origin/release", GitBranchKind.Origin),
            ],
            read.Branches.Select(branch => (branch.Name, branch.Kind)));

        var topic = read.Branches.Single(branch => branch.Name == "topic");
        Assert.Equal((Topic, 1, 4, "D:/scratch/topic"), (topic.Commit, topic.Ahead, topic.Behind, topic.Worktree));
        Assert.Equal((4, 1), (read.Branches[^1].Ahead, read.Branches[^1].Behind));
    }

    /// <summary>
    /// 🔴 One process for the list, never one per branch (REVIEW3): the calls past the first are each a landed branch's own
    /// exception, a tip that moved or a copy on origin that differs, and nothing else asks git anything.
    /// </summary>
    [Fact]
    public async Task The_list_is_one_call_and_only_an_exceptional_landed_branch_costs_one_more()
    {
        var git = Common();

        var read = await GitBranches.ReadAsync(Ask(), git.Run);

        Assert.Equal(
            [
                string.Join(' ', GitRefs.Arguments(trees: true, against: "refs/heads/main")),
                $"merge-base --is-ancestor {Landed} refs/heads/feature/13-ahead",
                $"merge-base --is-ancestor {Landed} refs/heads/feature/14-mine",
                "rev-list --left-right --count refs/heads/feature/13-ahead...refs/remotes/origin/feature/13-ahead",
            ],
            git.Asked);
        Assert.Equal(git.Asked.Select(asked => GitRefs.Command(asked.Split(' '))), read.Commands);
    }

    [Fact]
    public async Task The_line_says_how_it_stands_to_origins_copy()
    {
        var read = await GitBranches.ReadAsync(Ask(), Common().Run);

        Assert.Equal(new GitLine("main", LineSource.Workspace) { Commit = Main, Origin = OriginMain, Ahead = 2, Behind = 0 }, read.Line);
        Assert.Equal("D:/repos/engine", read.Branches[0].Worktree);
    }

    /// <summary>A session's branch is named by the newest session record on the tree of its name, in its repository, and by where it grew from.</summary>
    [Fact]
    public async Task A_session_branch_is_named_by_its_session()
    {
        var read = await GitBranches.ReadAsync(Ask(), Common().Run);

        var branch = read.Branches.Single(each => each.Kind == GitBranchKind.Session);
        Assert.Equal((2, 0), (branch.Ahead, branch.Behind));
        Assert.Equal(new GitSessionBranch { Session = "newest", State = "working", Quest = "21", GrewFrom = "feature/12-even", From = Main }, branch.Session);
        Assert.Null(branch.Landed);
    }

    /// <summary>A session branch no record names is still a session's branch, said with nothing more.</summary>
    [Fact]
    public async Task A_session_branch_no_record_names_is_listed_without_a_session()
    {
        var read = await GitBranches.ReadAsync(Ask() with { Sessions = null, Grown = [] }, Common().Run);

        var branch = read.Branches.Single(each => each.Kind == GitBranchKind.Session);
        Assert.Equal(new GitSessionBranch(), branch.Session);
    }

    [Fact]
    public async Task A_landed_branch_says_whether_it_is_pushed_and_its_pull_request()
    {
        var read = await GitBranches.ReadAsync(Ask(), Common().Run);
        GitLandedBranch Of(string name) => read.Branches.Single(branch => branch.Name == name).Landed!;

        var even = Of("feature/12-even");
        Assert.Equal((GitOrigin.InStep, "s-even", "12", "Even", "azure", true, "https://example.test/pr/7"),
            (even.Origin, even.Session, even.Quest, even.Title, even.Plugin, even.Pushed, even.PullRequest));
        Assert.Equal(Landed, even.OriginCommit);

        var ahead = Of("feature/13-ahead");
        Assert.Equal((GitOrigin.Ahead, 1, 0, Pushed), (ahead.Origin, ahead.OriginAhead, ahead.OriginBehind, ahead.OriginCommit));
        Assert.False(ahead.Pushed);

        // Recorded as pushed, and its name is gone from origin: --prune after its pull request merged, or deleted there.
        Assert.Equal(GitOrigin.Gone, Of("feature/15-gone").Origin);
        Assert.Null(Of("feature/15-gone").OriginCommit);
    }

    [Theory]
    [InlineData("0\t2\n", GitOrigin.Behind)]
    [InlineData("1\t2\n", GitOrigin.Diverged)]
    [InlineData("", GitOrigin.Unknown)]
    public async Task A_landed_branch_whose_copy_on_origin_differs_says_how(string counted, string origin)
    {
        var git = Common().Answer(
            ["rev-list", "--left-right", "--count", "refs/heads/feature/13-ahead...refs/remotes/origin/feature/13-ahead"], counted,
            code: counted.Length == 0 ? 128 : 0);

        var read = await GitBranches.ReadAsync(Ask(), git.Run);

        Assert.Equal(origin, read.Branches.Single(branch => branch.Name == "feature/13-ahead").Landed!.Origin);
    }

    /// <summary>A landing recorded and never pushed has no copy on origin to compare.</summary>
    [Fact]
    public async Task A_landed_branch_never_pushed_says_so()
    {
        var ask = Ask() with { Landings = [Ask().Landings[0] with { Pushed = false, PullRequest = null, Plugin = null, PushedTip = null }] };
        var answer = Record("refs/heads/main", Main, "0 0") + Record("refs/heads/feature/12-even", Landed, "1 0");
        var git = new StandInGit().Answer(GitRefs.Arguments(trees: true, against: "refs/heads/main"), answer);

        var read = await GitBranches.ReadAsync(ask, git.Run);

        Assert.Equal(GitOrigin.NotPushed, read.Branches.Single(branch => branch.Name == "feature/12-even").Landed!.Origin);
        Assert.Single(git.Asked);
    }

    /// <summary>A repository holding only the person's own branches holds nothing of Daoris's (D112), and nothing of theirs is judged.</summary>
    [Fact]
    public async Task Only_yours_holds_nothing_of_daoris()
    {
        var answer = Record("refs/heads/main", Main, "0 0") + Record("refs/heads/topic", Topic, "1 4");
        var git = new StandInGit().Answer(GitRefs.Arguments(trees: true, against: "refs/heads/main"), answer);

        var read = await GitBranches.ReadAsync(Ask() with { Landings = [], Grown = [], Sessions = [] }, git.Run);

        Assert.False(read.Holds);
        Assert.Equal([GitBranchKind.Line, GitBranchKind.Yours], read.Branches.Select(branch => branch.Kind));
    }

    /// <summary>With no line, nothing is counted from one, and git is asked once all the same.</summary>
    [Fact]
    public async Task With_no_line_nothing_is_counted()
    {
        var answer = Record("refs/heads/main", Main) + Record("refs/heads/topic", Topic);
        var git = new StandInGit().Answer(GitRefs.Arguments(trees: true, against: null), answer);

        var read = await GitBranches.ReadAsync(Ask(line: null) with { Landings = [] }, git.Run);

        Assert.Equal(new GitLine(null, LineSource.None), read.Line);
        Assert.Equal([GitBranchKind.Yours, GitBranchKind.Yours], read.Branches.Select(branch => branch.Kind));
        Assert.All(read.Branches, branch => Assert.Null(branch.Ahead));
        Assert.Single(git.Asked);
    }

    /// <summary>A line set to a branch this checkout has only on origin is counted from origin's copy, after git said it found no local one.</summary>
    [Fact]
    public async Task A_line_only_on_origin_is_counted_from_origins_copy()
    {
        var plain = Record("refs/heads/topic", Topic) + Record("refs/remotes/origin/main", OriginMain);
        var counted = Record("refs/heads/topic", Topic, "1 4") + Record("refs/remotes/origin/main", OriginMain, "0 0");
        var git = new StandInGit()
            .Answer(GitRefs.Arguments(trees: true, against: "refs/heads/main"), "", code: 128, stderr: "fatal: failed to find 'refs/heads/main'")
            .Answer(GitRefs.Arguments(trees: false, against: null), plain)
            .Answer(GitRefs.Arguments(trees: true, against: "refs/remotes/origin/main"), counted);

        var read = await GitBranches.ReadAsync(Ask() with { Landings = [] }, git.Run);

        Assert.Equal(new GitLine("main", LineSource.Workspace) { Origin = OriginMain }, read.Line);
        Assert.Equal((1, 4), (read.Branches.Single().Ahead, read.Branches.Single().Behind));
        Assert.Empty(read.Missing);
        Assert.Equal(3, git.Asked.Count);
    }

    /// <summary>
    /// A git without <c>%(ahead-behind)</c> (before 2.41) is still answered: each branch counted on its own, one
    /// <c>rev-list</c> each, and the line against origin's copy the same way, said in the list's head. No message is
    /// matched: what git lacks is found by asking a form it can answer.
    /// </summary>
    [Fact]
    public async Task A_git_without_ahead_behind_counts_each_branch_on_its_own()
    {
        var plain = Record("refs/heads/main", Main) + Record("refs/heads/topic", Topic) + Record("refs/remotes/origin/main", OriginMain);
        var trees = Record("refs/heads/main", Main, worktree: "D:/repos/engine") + Record("refs/heads/topic", Topic)
            + Record("refs/remotes/origin/main", OriginMain);
        var git = new StandInGit()
            .Answer(GitRefs.Arguments(trees: true, against: "refs/heads/main"), "", code: 128, stderr: "fatal: unknown field name: ahead-behind:refs/heads/main")
            .Answer(GitRefs.Arguments(trees: false, against: null), plain)
            .Answer(GitRefs.Arguments(trees: true, against: null), trees)
            .Answer(["rev-list", "--left-right", "--count", "refs/heads/topic...refs/heads/main"], "1\t4\n")
            .Answer(["rev-list", "--left-right", "--count", "refs/heads/main...refs/remotes/origin/main"], "0\t2\n");

        var read = await GitBranches.ReadAsync(Ask() with { Landings = [], Sessions = [], Grown = [] }, git.Run);

        Assert.Equal([GitRefs.AheadBehind], read.Missing);
        Assert.Equal(new GitLine("main", LineSource.Workspace) { Commit = Main, Origin = OriginMain, Ahead = 0, Behind = 2 }, read.Line);
        var topic = read.Branches.Single(branch => branch.Name == "topic");
        Assert.Equal((1, 4), (topic.Ahead, topic.Behind));
        Assert.Equal((0, 0), (read.Branches[0].Ahead, read.Branches[0].Behind));
        Assert.Equal("D:/repos/engine", read.Branches[0].Worktree);
        Assert.Equal(5, git.Asked.Count);
    }

    /// <summary>
    /// A git without <c>%(worktreepath)</c> (before 2.23) has no <c>%(ahead-behind)</c> either: which tree holds a branch
    /// comes from <c>git worktree list --porcelain</c>, one call more, and each branch is counted on its own.
    /// </summary>
    [Fact]
    public async Task A_git_without_worktreepath_asks_the_worktree_list()
    {
        var plain = Record("refs/heads/main", Main) + Record("refs/heads/daoris/s-1a2b3c4d", Session);
        var git = new StandInGit()
            .Answer(GitRefs.Arguments(trees: true, against: "refs/heads/main"), "", code: 128, stderr: "fatal: unknown field name: worktreepath")
            .Answer(GitRefs.Arguments(trees: false, against: null), plain)
            .Answer(GitRefs.Arguments(trees: true, against: null), "", code: 128, stderr: "fatal: unknown field name: worktreepath")
            .Answer(["worktree", "list", "--porcelain"],
                $"worktree D:/repos/engine\nHEAD {Main}\nbranch refs/heads/main\n\n"
                + $"worktree D:/data/trees/aurora/engine/s-1a2b3c4d\nHEAD {Session}\nbranch refs/heads/daoris/s-1a2b3c4d\n\n")
            .Answer(["rev-list", "--left-right", "--count", "refs/heads/daoris/s-1a2b3c4d...refs/heads/main"], "2\t0\n");

        var read = await GitBranches.ReadAsync(Ask() with { Landings = [] }, git.Run);

        Assert.Equal([GitRefs.WorktreePath, GitRefs.AheadBehind], read.Missing);
        Assert.Equal("D:/repos/engine", read.Branches[0].Worktree);
        var session = read.Branches.Single(branch => branch.Kind == GitBranchKind.Session);
        Assert.Equal(("D:/data/trees/aurora/engine/s-1a2b3c4d", 2, 0), (session.Worktree, session.Ahead, session.Behind));
        Assert.Equal(5, git.Asked.Count);
    }

    /// <summary>
    /// D121 §3: the list reads the git the agents run, the file Tools resolves, started by <c>WorkingTree</c>'s one start.
    /// Read from the source, as <c>EveryChildIsHandedTheToolsTests</c> holds every other start: no start of its own, and
    /// no bare <c>git</c>.
    /// </summary>
    [Fact]
    public void The_list_starts_git_only_through_the_start_tools_resolves()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver")))
        {
            folder = folder.Parent;
        }

        var sources = Directory.GetFiles(Path.Combine(folder!.FullName, "Daoris.Desktop.Driver"), "GitBranches*.cs");
        Assert.True(sources.Length >= 2, $"expected the list's sources, found {sources.Length}");
        var text = string.Concat(sources.Select(File.ReadAllText));
        Assert.Contains("WorkingTree.GitAsync(root, arguments, run)", text);
        Assert.DoesNotContain("ProcessStartInfo", text);
        Assert.DoesNotContain("Process.Start", text);
    }

    /// <summary>A repository git cannot answer for is said in git's words, and counts as holding Daoris's (D112), so it is never left out unseen.</summary>
    [Fact]
    public async Task A_repository_git_cannot_answer_for_is_said_in_gits_words()
    {
        var git = new StandInGit()
            .Answer(GitRefs.Arguments(trees: true, against: "refs/heads/main"), "", code: 128, stderr: "fatal: bad object refs/heads/broken\n")
            .Answer(GitRefs.Arguments(trees: false, against: null), "", code: 128, stderr: "fatal: bad object refs/heads/broken\nmore\n");

        var read = await GitBranches.ReadAsync(Ask(), git.Run);

        Assert.Equal("fatal: bad object refs/heads/broken", read.Problem);
        Assert.True(read.Holds);
        Assert.Empty(read.Branches);
        Assert.Equal(2, git.Asked.Count);
    }
}
