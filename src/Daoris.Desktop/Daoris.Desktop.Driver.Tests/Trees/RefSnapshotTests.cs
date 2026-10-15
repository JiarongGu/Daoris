using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A judgement reads a checkout's refs once (PROC1d-ii): `rev-parse --verify` was 389 of PROC1a's 1278 product starts, a branch's
/// tip, a line's form, an upstream asked one at a time. A snapshot answers what git would, says where it cannot so git is asked
/// as before, and is read again after any write its own flow makes.
/// </summary>
/// <remarks>
/// The fast half: the read is a function handed in, so what is counted is how often git would have been asked. The real
/// checkout's answers, against git's own, are <c>TreeSyncStartsTests</c>', in the Process half.
/// </remarks>
public sealed class RefSnapshotTests
{
    private const string Main = "1111111111111111111111111111111111111111";
    private const string Origin = "2222222222222222222222222222222222222222";
    private const string Work = "3333333333333333333333333333333333333333";
    private const string Moved = "4444444444444444444444444444444444444444";
    private const string Tag = "5555555555555555555555555555555555555555";

    private int _reads;
    private string? _output = Refs(Work);

    /// <summary>What <c>for-each-ref</c> prints in <see cref="RefSnapshot.Format"/>: name, object, its type, the upstream in full and short.</summary>
    private static string Refs(string work) => string.Join('\n',
        $"refs/heads/daoris/aurora-1 {work} commit  ",
        $"refs/heads/daoris/aurora-2 {Main} commit  ",
        $"refs/heads/feature/q2 {Work} commit refs/remotes/origin/feature/q2 origin/feature/q2",
        $"refs/heads/main {Main} commit refs/remotes/origin/main origin/main",
        $"refs/heads/odd {Tag} tag  ",
        $"refs/remotes/origin/HEAD {Origin} commit  ",
        $"refs/remotes/origin/main {Origin} commit  ") + "\n";

    private RefSnapshot Take() => RefSnapshot.Take(_ =>
    {
        Interlocked.Increment(ref _reads);
        return Task.FromResult(_output);
    });

    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task A_ref_it_read_answers_as_git_would()
    {
        var refs = Take();

        Assert.Equal((true, Main), await refs.CommitAsync("refs/heads/main", None));
        Assert.Equal((true, Origin), await refs.CommitAsync("refs/remotes/origin/main", None));
        Assert.Equal((true, Origin), await refs.CommitAsync("refs/remotes/origin/HEAD", None));
        Assert.True(await refs.HasAsync("refs/heads/feature/q2", None));
        Assert.Equal((true, "refs/remotes/origin/main", "origin/main"), await refs.UpstreamAsync("main", None));
        Assert.Equal((true, (string?)null, (string?)null), await refs.UpstreamAsync("daoris/aurora-1", None));
        Assert.Equal(["daoris/aurora-1", "daoris/aurora-2"], await refs.BranchesAsync("daoris/", None));
        Assert.Equal(1, _reads);
    }

    [Fact]
    public async Task A_ref_it_does_not_hold_answers_null_as_rev_parse_verify_fails()
    {
        var refs = Take();

        Assert.Equal((true, (string?)null), await refs.CommitAsync("refs/heads/gone", None));
        Assert.False(await refs.HasAsync("refs/remotes/origin/gone", None));
        Assert.Equal((true, (string?)null), await refs.CommitAsync("refs/remotes/origin/feature/q2", None));
    }

    [Theory]
    [InlineData("HEAD")]
    [InlineData("main")]
    [InlineData("refs/tags/v1")]
    [InlineData("refs/remotes/fork/main")]
    [InlineData("refs/daoris/discarded/daoris/aurora-1")]
    [InlineData(Main)]
    [InlineData("refs/heads/main^")]
    [InlineData("refs/heads/main~1")]
    [InlineData("refs/heads/main@{upstream}")]
    [InlineData("refs/heads/a..b")]
    [InlineData("refs/heads/")]
    // Only case tells it from one it holds: on a disk blind to case, git still finds a loose ref so named.
    [InlineData("refs/heads/Main")]
    public async Task What_it_cannot_say_is_asked_of_git(string revision)
    {
        var refs = Take();

        Assert.False((await refs.CommitAsync(revision, None)).Known);
        Assert.Null(await refs.HasAsync(revision, None));
    }

    [Fact]
    public async Task A_ref_that_is_no_commit_is_asked_of_git_for_its_commit_and_is_there_all_the_same()
    {
        var refs = Take();

        Assert.False((await refs.CommitAsync("refs/heads/odd", None)).Known);
        Assert.True(await refs.HasAsync("refs/heads/odd", None));
    }

    [Fact]
    public async Task A_branch_it_does_not_hold_may_still_have_an_upstream_so_git_is_asked()
    {
        var refs = Take();

        // Its configuration may outlive the branch: git reads `<branch>@{upstream}` from it.
        Assert.False((await refs.UpstreamAsync("gone", None)).Known);
    }

    [Fact]
    public async Task A_read_git_could_not_give_says_nothing_and_is_not_asked_again()
    {
        _output = null;
        var refs = Take();

        Assert.False((await refs.CommitAsync("refs/heads/main", None)).Known);
        Assert.Null(await refs.HasAsync("refs/heads/gone", None));
        Assert.False((await refs.UpstreamAsync("main", None)).Known);
        Assert.Null(await refs.BranchesAsync("daoris/", None));
        Assert.Equal(1, _reads);
    }

    [Fact]
    public async Task A_line_it_cannot_parse_makes_the_whole_read_unread()
    {
        _output = $"refs/heads/main {Main} commit\nrefs/heads/other\n";
        var refs = Take();

        Assert.False((await refs.CommitAsync("refs/heads/gone", None)).Known);
        Assert.Null(RefSnapshot.Parse(_output));
        Assert.NotNull(RefSnapshot.Parse(Refs(Work)));
        Assert.NotNull(RefSnapshot.Parse(""));
    }

    [Fact]
    public async Task It_is_read_at_its_first_question_and_once()
    {
        var refs = Take();
        Assert.Equal(0, _reads);

        await refs.CommitAsync("refs/heads/main", None);
        await refs.CommitAsync("refs/heads/daoris/aurora-1", None);
        await refs.HasAsync("refs/remotes/origin/main", None);

        Assert.Equal(1, _reads);
    }

    [Fact]
    public async Task Dropped_it_reads_again_and_answers_the_refs_as_they_are_now()
    {
        var refs = Take();
        Assert.Equal((true, Work), await refs.CommitAsync("refs/heads/daoris/aurora-1", None));

        _output = Refs(Moved);
        refs.Invalidate();

        Assert.Equal((true, Moved), await refs.CommitAsync("refs/heads/daoris/aurora-1", None));
        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task A_write_this_flow_starts_drops_it_and_a_read_does_not()
    {
        var refs = Take();
        await refs.CommitAsync("refs/heads/daoris/aurora-1", None);

        RefSnapshot.Ran(["rev-parse", "--verify", "--quiet", "refs/heads/main"]);
        await refs.CommitAsync("refs/heads/daoris/aurora-1", None);
        Assert.Equal(1, _reads);

        _output = Refs(Moved);
        RefSnapshot.Ran(["update-ref", "refs/heads/daoris/aurora-1", Moved, Work]);
        Assert.Equal((true, Moved), await refs.CommitAsync("refs/heads/daoris/aurora-1", None));
        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task A_write_in_a_flow_beside_it_does_not_drop_it_and_one_in_a_flow_it_started_does()
    {
        var refs = Take();
        await refs.CommitAsync("refs/heads/main", None);

        Task beside;
        using (ExecutionContext.SuppressFlow()) beside = Task.Run(RefSnapshot.Wrote);
        await beside;
        await refs.CommitAsync("refs/heads/main", None);
        Assert.Equal(1, _reads);

        await Task.Run(RefSnapshot.Wrote);
        await refs.CommitAsync("refs/heads/main", None);
        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task A_snapshot_a_judgement_took_is_not_held_by_its_caller_after()
    {
        RefSnapshot? taken = null;

        async Task JudgeAsync()
        {
            await Task.Yield();
            taken = Take();
            await taken.CommitAsync("refs/heads/main", None);
        }

        await JudgeAsync();
        RefSnapshot.Wrote();
        await taken!.CommitAsync("refs/heads/main", None);

        Assert.Equal(1, _reads);
    }

    [Theory]
    [InlineData("rev-parse --verify --quiet refs/heads/main^{commit}", false)]
    [InlineData("for-each-ref --format=%(refname) refs/heads/", false)]
    [InlineData("merge-base --is-ancestor a b", false)]
    [InlineData("rev-list --count a..b", false)]
    [InlineData("diff --no-ext-diff --name-only -z a b", false)]
    [InlineData("log -g -1 --format=%gd refs/remotes/origin/main", false)]
    [InlineData("status --porcelain", false)]
    [InlineData("worktree list --porcelain", false)]
    [InlineData("branch --all --contains abc --format=%(refname:short)", false)]
    [InlineData("branch --list daoris/*", false)]
    [InlineData("symbolic-ref --short -q HEAD", false)]
    [InlineData("symbolic-ref --short refs/remotes/origin/HEAD", false)]
    [InlineData("remote get-url origin", false)]
    [InlineData("config --get branch.main.remote", false)]
    [InlineData("-c core.longpaths=true rev-parse --show-toplevel", false)]
    [InlineData("update-ref -d refs/heads/x abc", true)]
    [InlineData("update-ref refs/daoris/discarded/x abc", true)]
    [InlineData("branch -D x", true)]
    [InlineData("branch x main", true)]
    [InlineData("branch -d -r origin/x --contains abc", true)]
    [InlineData("branch --set-upstream-to=origin/main main", true)]
    [InlineData("worktree add -b daoris/x path main", true)]
    [InlineData("worktree remove path", true)]
    [InlineData("merge --ff-only --quiet abc", true)]
    [InlineData("fetch --quiet --no-write-fetch-head origin main", true)]
    [InlineData("push -u origin x", true)]
    [InlineData("rebase --onto a b c", true)]
    [InlineData("symbolic-ref HEAD refs/heads/x", true)]
    [InlineData("config --remove-section branch.x", true)]
    [InlineData("config branch.x.merge refs/heads/x", true)]
    [InlineData("remote add origin path", true)]
    [InlineData("-c core.longpaths=true worktree remove path", true)]
    [InlineData("commit -m x", true)]
    [InlineData("an-unknown-verb", true)]
    [InlineData("", true)]
    public void A_git_that_may_move_a_ref_or_an_upstream_is_a_write(string command, bool writes) =>
        Assert.Equal(writes, RefSnapshot.MayWrite(command.Split(' ', StringSplitOptions.RemoveEmptyEntries)));

    [Fact]
    public async Task The_reads_handed_one_start_no_git_where_it_holds_the_answer()
    {
        var refs = Take();
        var started = new List<string>();
        WorkingTree.Started.Value = arguments => { lock (started) started.Add(string.Join(' ', arguments)); };
        try
        {
            Assert.Equal(Main, await TreeRefs.CommitOfAsync("unused", "refs/heads/main", None, refs));
            Assert.Null(await TreeRefs.CommitOfAsync("unused", "refs/heads/gone", None, refs));
            Assert.Equal("refs/heads/feature/q2", await TreeGit.ComparableAsync("unused", "feature/q2", None, refs));
            Assert.False(await TreeRefs.OnRemoteAsync("unused", "feature/q2", None, refs));
            Assert.Null(await TreeGit.ComparableAsync("unused", "gone", None, refs));
            Assert.Equal(["main", "origin/main"], (await LandedProofs.LineFormsAsync("unused", "main", None, refs)).Select(form => form.Name));
            Assert.True(await TreeRefs.OnRemoteAsync("unused", "main", None, refs));
            Assert.False(await TreeRefs.OnRemoteAsync("unused", "daoris/aurora-1", None, refs));
            Assert.Null(await LandedProofs.StandingTipAsync(
                "unused", new LandedBranch("engine", "aurora", "gone", "main", Work, "s1", null, null, DateTimeOffset.UnixEpoch), None, refs));
            Assert.Equal(["daoris/aurora-1", "daoris/aurora-2"], await LandedProofs.SessionBranchesAsync("unused", None, refs));
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        Assert.Empty(started);
        Assert.Equal(1, _reads);
    }
}
