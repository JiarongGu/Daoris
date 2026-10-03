using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GIT1b (D147 §4): the reads behind a page asked through a stand-in for git, so what is asked, how often, and what is kept
/// in memory is held without a process. A repository here is a folder with a <c>.git</c> folder in it, which is all the
/// reads look at before they ask git. <c>GitReadsTests</c> reads the same views from real repositories.
/// </summary>
public sealed class GitReadsAskTests : IDisposable
{
    // Each holds hex letters, so a read asked in capitals is a read of the same commit only where the reads say so.
    private const string Root = "11111111111111111111111111111111111111aa";
    private const string Side = "22222222222222222222222222222222222222bb";
    private const string Line = "33333333333333333333333333333333333333cc";
    private const string Merge = "44444444444444444444444444444444444444dd";

    private readonly string _scratch;
    private readonly string _engine;

    public GitReadsAskTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "git-reads-ask", Guid.NewGuid().ToString("N")[..8]);
        _engine = Repository("engine");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* the next run's folder is its own */ }
        catch (UnauthorizedAccessException) { /* the same */ }
    }

    private static string Listed(string commit, string parents, string subject = "work", string? mark = null) =>
        (mark is null ? "" : mark + "\0")
        + string.Join('\0', commit, parents, "Fixture", "fixture@example.test", "2026-10-04T06:13:35+11:00", "2026-10-04T06:13:35+11:00", subject) + "\0";

    private static string Meta(string commit, string parents, string message = "work\n") =>
        string.Join('\0', commit, parents, "Ann", "ann@example.test", "2026-10-04T06:13:35+11:00", "Bo", "bo@example.test",
            "2026-10-04T06:13:36+11:00", message.Split('\n')[0], message) + "\0";

    private const string OneFile =
        ":100644 100644 1111111 2222222 M\0a.txt\0" + "1\t1\ta.txt\0" + "\0"
        + "diff --git a/a.txt b/a.txt\nindex 1111111..2222222 100644\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-one\n+two\n";

    private static bool IsMeta(IReadOnlyList<string> arguments) => arguments.Contains("log") && arguments.Contains("--max-count=1");

    private static bool IsDiff(IReadOnlyList<string> arguments) => arguments.Contains("diff");

    [Fact]
    public async Task A_commit_is_two_calls_and_reading_it_again_starts_none()
    {
        var git = new StandInGit().Answer(IsMeta, Meta(Merge, $"{Line} {Side}", "Merge topic\n\nbody\n")).Answer(IsDiff, OneFile);
        var reads = new GitReads(new GitReadCache(), git.Read);

        var first = await reads.CommitAsync(_engine, Merge);

        Assert.Equal(GitReadRefusal.None, first.Refusal);
        Assert.False(first.Remembered);
        var detail = first.Value!;
        Assert.Equal((Merge, Line, true, "Merge topic\n\nbody"), (detail.Commit, detail.Against, detail.Merge, detail.Message));
        Assert.Equal("a.txt", Assert.Single(detail.Changes!.Files).Path);
        Assert.Equal(2, git.Asked.Count);
        // The changes are REVIEW3's one-call reader's, over the first parent to the commit.
        Assert.Contains($"{Line}..{Merge}", git.Asked[1]);
        Assert.Contains("--raw", git.Asked[1]);
        Assert.Contains("--numstat", git.Asked[1]);
        Assert.Contains("-p", git.Asked[1]);
        Assert.Equal([$"git show -s {Merge}", $"git diff {Line} {Merge}"], first.Commands);

        // Its id in capitals is the same commit: named only by ids, the answer cannot have changed.
        var again = await reads.CommitAsync(_engine, Merge.ToUpperInvariant());

        Assert.True(again.Remembered);
        Assert.Same(detail, again.Value);
        Assert.Equal(first.Commands, again.Commands);
        Assert.Equal(2, git.Asked.Count);
    }

    /// <summary>A root commit has no parent: its changes are read against the empty tree of its repository's hash.</summary>
    [Theory]
    [InlineData(Root, GitReads.EmptyTree)]
    [InlineData("5555555555555555555555555555555555555555555555555555555555555555", GitReads.EmptyTreeSha256)]
    public async Task A_root_commit_s_changes_are_read_against_the_empty_tree(string commit, string empty)
    {
        var git = new StandInGit().Answer(IsMeta, Meta(commit, "")).Answer(IsDiff, OneFile);

        var read = await new GitReads(new GitReadCache(), git.Read).CommitAsync(_engine, commit);

        Assert.Null(read.Value!.Against);
        Assert.False(read.Value.Merge);
        Assert.Contains($"{empty}..{commit}", git.Asked[1]);
        Assert.Equal($"git show --format= {commit}", read.Commands[1]);
    }

    /// <summary>🔴 A ref moves and a commit id does not: a read is asked of whole ids, and git is not asked anything else.</summary>
    [Theory]
    [InlineData("main")]
    [InlineData("1111111")]
    [InlineData("HEAD~1")]
    [InlineData("-n")]
    public async Task Anything_but_a_whole_commit_id_is_refused_before_git_is_asked(string named)
    {
        var git = new StandInGit();
        var reads = new GitReads(new GitReadCache(), git.Read);

        Assert.Equal(GitReadRefusal.NotACommit, (await reads.CommitAsync(_engine, named)).Refusal);
        Assert.Equal(GitReadRefusal.NotACommit, (await reads.GraphAsync(_engine, [Line, named])).Refusal);
        Assert.Equal(GitReadRefusal.NotACommit, (await reads.HistoryAsync(_engine, named, "a.txt")).Refusal);
        Assert.Equal(GitReadRefusal.NotACommit, (await reads.BlameAsync(_engine, named, "a.txt")).Refusal);
        Assert.Equal(GitReadRefusal.NotACommit, (await reads.CompareAsync(_engine, Line, named)).Refusal);
        Assert.Equal(GitReadRefusal.NotACommit, (await reads.HoldingAsync(_engine, named)).Refusal);
        Assert.Empty(git.Asked);
    }

    /// <summary>🔴 git walks up: a folder with no <c>.git</c> of its own is said, and git is not asked.</summary>
    [Fact]
    public async Task A_root_that_is_not_the_top_of_a_repository_is_refused_before_git_is_asked()
    {
        var git = new StandInGit();
        var reads = new GitReads(new GitReadCache(), git.Read);
        var inside = Directory.CreateDirectory(Path.Combine(_engine, "src")).FullName;

        foreach (var root in new[] { inside, Path.Combine(_scratch, "missing"), "" })
        {
            var read = await reads.CommitAsync(root, Merge);
            Assert.Equal(GitReadRefusal.NotTop, read.Refusal);
            Assert.Contains("not the top of a repository of its own", read.Problem);
        }

        Assert.Empty(git.Asked);
    }

    /// <summary>A read git could not answer is said and not kept: a fetch may bring the commit, and then it answers.</summary>
    [Fact]
    public async Task A_read_git_did_not_answer_is_not_kept()
    {
        var git = new StandInGit().Answer(IsMeta, "", code: 128);
        var reads = new GitReads(new GitReadCache(), git.Read);

        var first = await reads.CommitAsync(_engine, Merge);
        var second = await reads.CommitAsync(_engine, Merge);

        Assert.Equal(GitReadRefusal.NotAnswered, first.Refusal);
        Assert.Contains(Merge[..8], first.Problem);
        Assert.False(second.Remembered);
        Assert.Equal(2, git.Asked.Count);
        Assert.Equal([$"git show -s {Merge}"], first.Commands);
    }

    /// <summary>The fields answered and the changes not: the commit is not answered, and nothing of it is kept.</summary>
    [Fact]
    public async Task A_commit_whose_changes_git_did_not_answer_is_not_answered()
    {
        var git = new StandInGit().Answer(IsMeta, Meta(Merge, Line)).Answer(IsDiff, "", code: 128);
        var reads = new GitReads(new GitReadCache(), git.Read);

        Assert.Equal(GitReadRefusal.NotAnswered, (await reads.CommitAsync(_engine, Merge)).Refusal);
        Assert.Equal(0, reads.Cache.Count);
    }

    /// <summary>
    /// A shallow repository's history moves when it is deepened, so nothing read from one is kept: its <c>shallow</c> file,
    /// found on disk with no process, says it is one.
    /// </summary>
    [Fact]
    public async Task Nothing_read_from_a_shallow_repository_is_kept()
    {
        File.WriteAllText(Path.Combine(_engine, ".git", "shallow"), Root + "\n");
        var git = new StandInGit().Answer(IsMeta, Meta(Merge, Line)).Answer(IsDiff, OneFile);
        var reads = new GitReads(new GitReadCache(), git.Read);

        await reads.CommitAsync(_engine, Merge);
        var again = await reads.CommitAsync(_engine, Merge);

        Assert.False(again.Remembered);
        Assert.Equal(4, git.Asked.Count);
    }

    /// <summary>The same ids in another repository are another answer: the key starts with the repository's git directory.</summary>
    [Fact]
    public async Task An_answer_is_kept_per_repository()
    {
        var game = Repository("game");
        var git = new StandInGit().Answer(IsMeta, Meta(Merge, Line)).Answer(IsDiff, OneFile);
        var reads = new GitReads(new GitReadCache(), git.Read);

        await reads.CommitAsync(_engine, Merge);
        var other = await reads.CommitAsync(game, Merge);

        Assert.False(other.Remembered);
        Assert.Equal(4, git.Asked.Count);
    }

    /// <summary>
    /// A session's tree is a linked worktree of its repository (D51), with one object store between them: what one read is
    /// kept for the other, by the common git directory its <c>.git</c> file and <c>commondir</c> name.
    /// </summary>
    [Fact]
    public async Task A_linked_worktree_shares_its_repository_s_memory()
    {
        var own = Directory.CreateDirectory(Path.Combine(_engine, ".git", "worktrees", "s-1")).FullName;
        File.WriteAllText(Path.Combine(own, "commondir"), "../..\n");
        var tree = Directory.CreateDirectory(Path.Combine(_scratch, "trees", "s-1")).FullName;
        File.WriteAllText(Path.Combine(tree, ".git"), $"gitdir: {own}\n");
        var git = new StandInGit().Answer(IsMeta, Meta(Merge, Line)).Answer(IsDiff, OneFile);
        var reads = new GitReads(new GitReadCache(), git.Read);

        await reads.CommitAsync(_engine, Merge);
        var fromTree = await reads.CommitAsync(tree, Merge);

        Assert.True(fromTree.Remembered);
        Assert.Equal(2, git.Asked.Count);
    }

    /// <summary>
    /// A graph page is one call from its tips, a page at a time. The tips are put in one order before they are asked or
    /// kept, so the call and the key agree whichever order the page sends them in.
    /// </summary>
    [Fact]
    public async Task A_graph_page_is_one_call_and_its_tips_in_any_order_are_one_answer()
    {
        var answer = string.Concat(Enumerable.Range(0, GitReads.GraphPage + 1).Select(i => Listed(Id(i), Id(i + 1))));
        var git = new StandInGit().Answer(arguments => arguments.Contains("log"), answer);
        var reads = new GitReads(new GitReadCache(), git.Read);

        var page = await reads.GraphAsync(_engine, [Line, Side, Line.ToUpperInvariant()], page: 1);

        Assert.Equal(GitReadRefusal.None, page.Refusal);
        Assert.Equal((GitReads.GraphPage, true, 1), (page.Value!.Commits.Count, page.Value.More, page.Value.Page));
        Assert.Equal([Side, Line], page.Value.Tips);
        var asked = Assert.Single(git.Asked);
        Assert.Equal([Side, Line, "--"], asked.TakeLast(3));
        Assert.Contains("--skip=200", asked);
        Assert.Equal($"git log --graph --date-order {Side} {Line}", Assert.Single(page.Commands));

        var again = await reads.GraphAsync(_engine, [Side, Line], page: 1);
        Assert.True(again.Remembered);
        Assert.Single(git.Asked);

        // Another page is another answer.
        await reads.GraphAsync(_engine, [Side, Line], page: 0);
        Assert.Equal(2, git.Asked.Count);
    }

    /// <summary>A branch's page: the branch and its line since they parted, both sides in the order named.</summary>
    [Fact]
    public async Task A_walk_since_two_parted_names_exactly_two_tips_in_their_order()
    {
        var git = new StandInGit().Answer(arguments => arguments.Contains("log"), Listed(Side, Root, mark: "<") + Listed(Line, Root, mark: ">"));
        var reads = new GitReads(new GitReadCache(), git.Read);

        var page = await reads.GraphAsync(_engine, [Line, Side], sinceParted: true);

        Assert.Equal([(Side, GitSide.Left), (Line, GitSide.Right)], page.Value!.Commits.Select(each => (each.Commit, each.Side)));
        Assert.False(page.Value.More);
        Assert.Contains($"{Line}...{Side}", git.Asked[0]);
        Assert.Contains("--left-right", git.Asked[0]);

        // The same two tips walked whole are another answer.
        await reads.GraphAsync(_engine, [Line, Side]);
        Assert.Equal(2, git.Asked.Count);

        Assert.Equal(GitReadRefusal.NotACommit, (await reads.GraphAsync(_engine, [Line], sinceParted: true)).Refusal);
        Assert.Equal(GitReadRefusal.NotACommit, (await reads.GraphAsync(_engine, [])).Refusal);
        Assert.Equal(2, git.Asked.Count);
    }

    /// <summary>One call carries its tips on its command line, which has a length: past the bound the walk is refused, and says so.</summary>
    [Fact]
    public async Task More_tips_than_one_call_carries_are_refused()
    {
        var git = new StandInGit();
        var tips = Enumerable.Range(0, GitReads.GraphTips + 1).Select(Id).ToList();

        var read = await new GitReads(new GitReadCache(), git.Read).GraphAsync(_engine, tips);

        Assert.Equal(GitReadRefusal.TooManyTips, read.Refusal);
        Assert.Contains($"{GitReads.GraphTips}", read.Problem);
        Assert.Empty(git.Asked);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(GitReads.LastPage + 1)]
    public async Task A_page_out_of_range_is_refused(int page)
    {
        var git = new StandInGit();
        var reads = new GitReads(new GitReadCache(), git.Read);

        Assert.Equal(GitReadRefusal.NotAPage, (await reads.GraphAsync(_engine, [Line], page)).Refusal);
        Assert.Equal(GitReadRefusal.NotAPage, (await reads.HistoryAsync(_engine, Line, "a.txt", page)).Refusal);
        Assert.Empty(git.Asked);
    }

    /// <summary>
    /// <c>--follow</c> ignores <c>--skip</c> (Git 2.53 printed nothing for <c>--skip=1</c>), so a page of a file's history asks
    /// for every commit up to its end and keeps its own hundred: still one call.
    /// </summary>
    [Fact]
    public async Task A_page_of_a_file_s_history_asks_up_to_its_end_and_keeps_its_own()
    {
        var answer = string.Concat(Enumerable.Range(0, 2 * GitReads.HistoryPage + 1)
            .Select(i => "\u0001" + Listed(Id(i), Id(i + 1)).TrimEnd('\0') + "\0\nM\0a.txt\0"));
        var git = new StandInGit().Answer(arguments => arguments.Contains("--follow"), answer);
        var reads = new GitReads(new GitReadCache(), git.Read);

        var page = await reads.HistoryAsync(_engine, Merge, "a.txt", page: 1);

        Assert.Equal(GitReadRefusal.None, page.Refusal);
        Assert.Contains($"--max-count={2 * GitReads.HistoryPage + 1}", Assert.Single(git.Asked));
        Assert.Equal(
            Enumerable.Range(GitReads.HistoryPage, GitReads.HistoryPage).Select(Id),
            page.Value!.Changes.Select(change => change.Commit.Commit));
        Assert.True(page.Value.More);
        Assert.Equal((Merge, "a.txt", 1), (page.Value.Commit, page.Value.Path, page.Value.Page));
        Assert.Equal($"git log --follow {Merge} -- a.txt", Assert.Single(page.Commands));

        Assert.True((await reads.HistoryAsync(_engine, Merge, "a.txt", page: 1)).Remembered);
        // Another path is another answer.
        Assert.False((await reads.HistoryAsync(_engine, Merge, "b.txt", page: 1)).Remembered);
    }

    /// <summary>A path is the repository's: one that is empty, rooted, or climbs out is refused before git is asked.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("../outside.txt")]
    [InlineData("src/../../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("a\0b")]
    public async Task A_path_outside_the_repository_is_refused(string path)
    {
        var git = new StandInGit();
        var reads = new GitReads(new GitReadCache(), git.Read);

        Assert.Equal(GitReadRefusal.NotAPath, (await reads.HistoryAsync(_engine, Merge, path)).Refusal);
        Assert.Equal(GitReadRefusal.NotAPath, (await reads.BlameAsync(_engine, Merge, path)).Refusal);
        Assert.Empty(git.Asked);
    }

    [Fact]
    public async Task A_blame_is_one_call_and_kept()
    {
        var answer = $"{Root} 1 1 1\nauthor F\nauthor-mail <f@example.test>\nauthor-time 1\nauthor-tz +0000\nsummary first\nfilename a.txt\n\tone\n";
        var git = new StandInGit().Answer(arguments => arguments.Contains("blame"), answer);
        var reads = new GitReads(new GitReadCache(), git.Read);

        var blame = await reads.BlameAsync(_engine, Merge, "a.txt");

        Assert.Equal("one", Assert.Single(Assert.Single(blame.Value!.Runs).Lines).Text);
        Assert.Equal($"git blame {Merge} -- a.txt", Assert.Single(blame.Commands));
        Assert.True((await reads.BlameAsync(_engine, Merge, "a.txt")).Remembered);
        Assert.Single(git.Asked);
    }

    /// <summary>A binary file's blame is known from its first line, and git is stopped there; the answer is kept like any other.</summary>
    [Fact]
    public async Task A_binary_file_s_blame_stops_git_and_says_binary()
    {
        var answer = $"{Root} 1 1 1\nauthor F\nauthor-mail <f@example.test>\nauthor-time 1\nauthor-tz +0000\nsummary s\nfilename b.dat\n\tx\0y\n"
                     + string.Concat(Enumerable.Range(2, 50).Select(i => $"{Root} {i} {i}\n\tmore\n"));
        var git = new StandInGit(piece: 64).Answer(arguments => arguments.Contains("blame"), answer);

        var blame = await new GitReads(new GitReadCache(), git.Read).BlameAsync(_engine, Merge, "b.dat");

        Assert.True(blame.Value!.Binary);
        Assert.True(git.Stopped);
    }

    /// <summary>
    /// A compare is two calls: the commits only on each side, then what the right side changed since the two parted
    /// (<c>A...B</c>), or everything that differs (<c>A..B</c>), each kept apart.
    /// </summary>
    [Fact]
    public async Task A_compare_is_two_calls_and_each_way_is_its_own_answer()
    {
        var git = new StandInGit()
            .Answer(arguments => arguments.Contains("log"), Listed(Side, Root, mark: "<") + Listed(Line, Root, mark: ">"))
            .Answer(IsDiff, OneFile);
        var reads = new GitReads(new GitReadCache(), git.Read);

        var parted = await reads.CompareAsync(_engine, Side, Line);

        Assert.Equal((Side, Line, false), (parted.Value!.Left, parted.Value.Right, parted.Value.Everything));
        Assert.Equal([(Side, GitSide.Left), (Line, GitSide.Right)], parted.Value.Commits.Select(each => (each.Commit, each.Side)));
        Assert.Equal("a.txt", Assert.Single(parted.Value.Changes!.Files).Path);
        Assert.Equal(2, git.Asked.Count);
        Assert.Contains($"{Side}...{Line}", git.Asked[0]);
        Assert.Contains("--left-right", git.Asked[0]);
        Assert.Contains($"{Side}...{Line}", git.Asked[1]);
        Assert.Equal([$"git log --left-right --date-order {Side}...{Line}", $"git diff {Side}...{Line}"], parted.Commands);

        var everything = await reads.CompareAsync(_engine, Side, Line, everything: true);
        Assert.False(everything.Remembered);
        Assert.Contains($"{Side}..{Line}", git.Asked[3]);

        Assert.True((await reads.CompareAsync(_engine, Side, Line)).Remembered);
        Assert.True((await reads.CompareAsync(_engine, Side, Line, everything: true)).Remembered);
        Assert.Equal(4, git.Asked.Count);
    }

    /// <summary>The branches holding a commit move with every commit made, so they are read each time and never kept.</summary>
    [Fact]
    public async Task The_branches_holding_a_commit_are_never_kept()
    {
        var git = new StandInGit().Answer(arguments => arguments.Contains("--contains"), "refs/heads/main\0\0\n");
        var reads = new GitReads(new GitReadCache(), git.Read);

        var first = await reads.HoldingAsync(_engine, Merge);
        var second = await reads.HoldingAsync(_engine, Merge);

        Assert.Equal(["refs/heads/main"], first.Value!.Branches);
        Assert.False(second.Remembered);
        Assert.Equal(2, git.Asked.Count);
        Assert.Equal(0, reads.Cache.Count);
    }

    /// <summary>A ref is resolved to its commit first, by one <c>rev-parse</c>, and the resolution is never kept: a ref moves.</summary>
    [Fact]
    public async Task A_ref_is_resolved_to_its_commit_and_never_kept()
    {
        var git = new StandInGit().Answer(arguments => arguments.Contains("rev-parse"), Merge + "\n");
        var reads = new GitReads(new GitReadCache(), git.Read);

        var resolved = await reads.ResolveAsync(_engine, "origin/main");
        await reads.ResolveAsync(_engine, "origin/main");

        Assert.Equal(("origin/main", Merge), (resolved.Value!.Revision, resolved.Value.Commit));
        Assert.Equal(["rev-parse", "--verify", "--quiet", "--end-of-options", "origin/main^{commit}"], git.Asked[0]);
        Assert.Equal(2, git.Asked.Count);
        Assert.Equal(0, reads.Cache.Count);

        foreach (var named in new[] { "", "-n", "a b", "main\nHEAD" })
        {
            Assert.Equal(GitReadRefusal.NotACommit, (await reads.ResolveAsync(_engine, named)).Refusal);
        }

        Assert.Equal(2, git.Asked.Count);
    }

    /// <summary>A name git resolves to nothing is not answered, in Daoris's words naming it.</summary>
    [Fact]
    public async Task A_ref_git_cannot_resolve_is_not_answered()
    {
        var git = new StandInGit().Answer(arguments => arguments.Contains("rev-parse"), "", code: 1);

        var resolved = await new GitReads(new GitReadCache(), git.Read).ResolveAsync(_engine, "gone");

        Assert.Equal(GitReadRefusal.NotAnswered, resolved.Refusal);
        Assert.Contains("gone", resolved.Problem);
    }

    private static string Id(int i) => i.ToString("x40");

    private string Repository(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        return root;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }

    /// <summary>git as the reads ask it: an answer per kind of call, handed over in pieces, each start counted. Unscripted calls fail as git would.</summary>
    private sealed class StandInGit(int piece = 1 << 20)
    {
        private readonly List<(Func<IReadOnlyList<string>, bool> When, string Answer, int Code)> _answers = [];

        public List<IReadOnlyList<string>> Asked { get; } = [];

        /// <summary>Whether a reader stopped git before its answer ended.</summary>
        public bool Stopped { get; private set; }

        public StandInGit Answer(Func<IReadOnlyList<string>, bool> when, string answer, int code = 0)
        {
            _answers.Add((when, answer, code));
            return this;
        }

        public Task<int> Read(string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
        {
            Asked.Add([.. arguments]);
            var found = _answers.FirstOrDefault(each => each.When(arguments));
            if (found.When is null) return Task.FromResult(128);

            for (var at = 0; at < found.Answer.Length; at += piece)
            {
                if (take(found.Answer.AsMemory(at, Math.Min(piece, found.Answer.Length - at)))) continue;
                Stopped = true;
                return Task.FromResult(0);
            }

            return Task.FromResult(found.Code);
        }
    }
}
