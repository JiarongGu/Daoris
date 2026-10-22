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

    private int _listed;
    private string? _trees = Trees();

    /// <summary>What <c>worktree list --porcelain</c> prints: the checkout on the line, a session's tree, a locked one detached.</summary>
    private static string Trees(string branch = "daoris/aurora-1") => string.Join('\n',
        $"worktree {Tree("engine")}", $"HEAD {Main}", "branch refs/heads/main", "",
        $"worktree {Tree("one")}", $"HEAD {Work}", $"branch refs/heads/{branch}", "",
        $"worktree {Tree("two")}", $"HEAD {Main}", "detached", "locked being opened", "") + "\n";

    private static string Tree(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "trees", name));

    private RefSnapshot Take() => RefSnapshot.Take(_ =>
    {
        Interlocked.Increment(ref _reads);
        return Task.FromResult(_output);
    }, _ =>
    {
        Interlocked.Increment(ref _listed);
        return Task.FromResult(_trees);
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

    /// <summary>
    /// PROC1d-iii: the working trees are read once for a judgement, as its refs are (`worktree list` was 47 of PROC1a's product
    /// starts), and a write in its flow drops them with the refs, so the next question lists them as they are now.
    /// </summary>
    [Fact]
    public async Task The_working_trees_are_listed_once_a_judgement_and_again_after_a_write()
    {
        var refs = Take();
        Assert.Equal(0, _listed);

        var listed = await TreeWorktrees.EntriesAsync("unused", None, refs);
        Assert.Equal(
            [(Tree("engine"), "main", false, null), (Tree("one"), "daoris/aurora-1", false, null), (Tree("two"), null, true, "being opened")],
            listed!.Select(entry => (entry.Path, entry.Branch, entry.Locked, entry.LockReason)));
        Assert.Equal(Tree("one"), (await TreeWorktrees.WorktreesAsync("unused", None, refs))["daoris/aurora-1"]);
        RefSnapshot.Ran(["worktree", "list", "--porcelain"]);
        Assert.NotNull(await TreeWorktrees.EntriesAsync("unused", None, refs));
        Assert.Equal(1, _listed);
        Assert.Equal(0, _reads);

        _trees = Trees("daoris/aurora-2");
        RefSnapshot.Ran(["worktree", "remove", Tree("one")]);

        Assert.Equal(Tree("one"), (await TreeWorktrees.WorktreesAsync("unused", None, refs))["daoris/aurora-2"]);
        Assert.Equal(2, _listed);
    }

    /// <summary>A list git could not give is asked again at the next question, as each caller asked it before: never an empty list.</summary>
    [Fact]
    public async Task A_list_git_could_not_give_is_asked_again()
    {
        _trees = null;
        var refs = Take();

        Assert.Null(await TreeWorktrees.EntriesAsync("unused", None, refs));
        await Assert.ThrowsAsync<DriverException>(() => TreeWorktrees.WorktreesAsync("unused", None, refs));
        Assert.Equal(2, _listed);

        _trees = Trees();
        Assert.NotNull(await TreeWorktrees.EntriesAsync("unused", None, refs));
        Assert.NotNull(await TreeWorktrees.EntriesAsync("unused", None, refs));
        Assert.Equal(3, _listed);
    }

    /// <summary>The test seam that makes a list fail (AUTOTIDY1) still makes it null where the judgement holds one.</summary>
    [Fact]
    public async Task A_list_the_seam_fails_is_null_whatever_the_snapshot_holds()
    {
        var refs = Take();
        var context = new TreeContext("unused", new LandingPlugins("unused")) { ListFails = _ => true };
        Assert.NotNull(await TreeWorktrees.EntriesAsync("unused", None, refs));

        Assert.Null(await context.Worktrees.ListAsync("unused", None, refs));
        Assert.Null(await context.Worktrees.ReadAsync("unused", None, refs));
        Assert.Equal(1, _listed);
    }

    private static readonly string Root = Tree("engine");

    /// <summary>The questions of commits by their full ids alone a judgement asks most (PROC1d-iv), as the product asks them.</summary>
    private static string[][] ByIds =>
    [
        ["merge-base", "--is-ancestor", Main, Work],
        ["--no-optional-locks", "diff", "--no-ext-diff", "--no-renames", "--ignore-submodules=none", "--name-only", "-z", Main, Work],
        ["rev-list", "--count", Work, "--not", Main, Origin],
        ["rev-list", "--count", $"{Main}..{Work}"],
    ];

    // A question through the judgement's memo: each start the memo lets through is counted, and git's answer is the one handed in.
    private static Task<(int Code, string Stdout, string Stderr)> AskAsync(
        string root, string[] arguments, List<string> asked, (int Code, string Stdout, string Stderr) answer)
    {
        Task<(int Code, string Stdout, string Stderr)> Start()
        {
            lock (asked) asked.Add(string.Join(' ', arguments));
            return Task.FromResult(answer);
        }

        return RefSnapshot.Asked(root, arguments, Start) ?? Start();
    }

    /// <summary>
    /// PROC1d-iv: a question of commits by their full ids alone (whether one holds another, the files two differ in, how many commits
    /// one has beyond others) is asked of a checkout's git once a judgement, and every later asker is handed git's answer as given.
    /// Three branches that left the line at one commit asked the same pair each. A landing's table keeps the same questions.
    /// </summary>
    [Fact]
    public async Task A_question_of_commits_by_their_ids_is_asked_once_a_judgement()
    {
        RefSnapshot.Take(_ => Task.FromResult(_output), root: Root);
        var asked = new List<string>();

        foreach (var question in ByIds)
        {
            Assert.True(GitFacts.Kept(question), string.Join(' ', question));
            for (var each = 0; each < 2; each++)
            {
                Assert.Equal((0, $"{question[0]} said\n", ""), await AskAsync(Root, question, asked, (0, $"{question[0]} said\n", "")));
            }
        }

        Assert.Equal(ByIds.Select(question => string.Join(' ', question)), asked);
    }

    /// <summary>
    /// PROC1d-iv: what names a ref or the head, reads the files, or could read another way under the person's configuration is never
    /// kept: a judgement asks git each time, as before.
    /// </summary>
    [Theory]
    [InlineData("merge-base --is-ancestor {main} refs/heads/main")]
    [InlineData("merge-base --is-ancestor HEAD {work}")]
    [InlineData("merge-base --is-ancestor 1111111 {work}")]
    [InlineData("diff --no-ext-diff --no-renames --ignore-submodules=none --name-only -z {main}")]
    [InlineData("diff --no-ext-diff --no-renames --ignore-submodules=none --name-only -z --cached {main}")]
    [InlineData("diff --no-ext-diff --no-renames --ignore-submodules=none --name-only -z {main} {work} -- a.txt")]
    [InlineData("diff --no-ext-diff --no-renames --ignore-submodules=none --name-only -z {main} refs/heads/main")]
    [InlineData("diff --no-ext-diff --name-only -z {main} {work}")]
    [InlineData("diff --no-ext-diff --no-renames --ignore-submodules=none -z {main} {work}")]
    [InlineData("rev-list --count {main}..HEAD")]
    [InlineData("rev-list --count {work} --not origin/main")]
    [InlineData("rev-list --count {work} --not --remotes")]
    [InlineData("rev-list --first-parent --max-count=64 {work} --not {main}")]
    [InlineData("status --porcelain")]
    public async Task A_question_naming_a_ref_the_head_or_the_files_is_asked_each_time(string question)
    {
        var arguments = question.Replace("{main}", Main).Replace("{work}", Work).Split(' ');
        RefSnapshot.Take(_ => Task.FromResult(_output), root: Root);
        var asked = new List<string>();

        await AskAsync(Root, arguments, asked, (0, "", ""));
        await AskAsync(Root, arguments, asked, (0, "", ""));

        Assert.Equal(2, asked.Count);
        Assert.False(GitFacts.Kept(arguments), question);
    }

    /// <summary>
    /// PROC1d-iv, RECORDUNREAD1: only an answer git gave is kept; a git that could not answer (a missing object's 128, a start that
    /// failed, a throw, a cancel) is asked again at the next question, never read as "no". `merge-base`'s 1 is its "no".
    /// </summary>
    [Fact]
    public async Task Only_an_answer_git_gave_is_kept()
    {
        RefSnapshot.Take(_ => Task.FromResult(_output), root: Root);
        var asked = new List<string>();
        string[] ancestor = ByIds[0];
        string[] differ = ByIds[1];

        foreach (var code in new[] { 128, -1, 2 })
        {
            asked.Clear();
            await AskAsync(Root, ancestor, asked, (code, "", "fatal"));
            await AskAsync(Root, ancestor, asked, (code, "", "fatal"));
            Assert.Equal(2, asked.Count);
        }

        asked.Clear();
        await AskAsync(Root, differ, asked, (1, "", "error"));
        await AskAsync(Root, differ, asked, (1, "", "error"));
        Assert.Equal(2, asked.Count);

        var thrown = 0;
        Task<(int, string, string)> Throws()
        {
            thrown++;
            return Task.FromException<(int, string, string)>(new InvalidOperationException("no git"));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => RefSnapshot.Asked(Root, ByIds[2], Throws)!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RefSnapshot.Asked(Root, ByIds[2], Throws)!);
        Assert.Equal(2, thrown);

        asked.Clear();
        await AskAsync(Root, ancestor, asked, (1, "", ""));
        Assert.Equal((1, "", ""), await AskAsync(Root, ancestor, asked, (0, "", "")));
        Assert.Single(asked);
    }

    /// <summary>
    /// PROC1d-iv: a judgement keeps only its own checkout's answers, and only while it holds: another repository asks its own git; the
    /// judgement again before an act takes its own refs and so asks afresh (D88); a write this flow starts drops what was kept with the
    /// refs; and a seam's snapshot, no checkout's, keeps nothing.
    /// </summary>
    [Fact]
    public async Task Another_checkout_the_judgement_again_and_a_write_each_ask_git_again()
    {
        var question = ByIds[0];
        var asked = new List<string>();
        RefSnapshot.Take(_ => Task.FromResult(_output), root: Root);

        await AskAsync(Root, question, asked, (0, "", ""));
        await AskAsync(Root.ToUpperInvariant() + Path.DirectorySeparatorChar, question, asked, (0, "", ""));
        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 2, asked.Count);
        await AskAsync(Tree("other"), question, asked, (1, "", ""));
        Assert.Equal(OperatingSystem.IsWindows() ? 2 : 3, asked.Count);

        asked.Clear();
        RefSnapshot.Take(_ => Task.FromResult(_output), root: Root);
        await AskAsync(Root, question, asked, (0, "", ""));
        await AskAsync(Root, question, asked, (0, "", ""));
        Assert.Single(asked);

        RefSnapshot.Ran(["rev-list", "--count", $"{Main}..{Work}"]);
        await AskAsync(Root, question, asked, (0, "", ""));
        Assert.Single(asked);
        RefSnapshot.Ran(["update-ref", "-d", "refs/heads/daoris/aurora-1", Work]);
        await AskAsync(Root, question, asked, (0, "", ""));
        Assert.Equal(2, asked.Count);
    }

    /// <summary>PROC1d-iv: what a judgement kept ends with it: its caller, after it returns, asks git, and a seam's snapshot keeps nothing.</summary>
    [Fact]
    public async Task What_a_judgement_kept_is_not_its_caller_s_after()
    {
        var asked = new List<string>();

        async Task JudgeAsync()
        {
            await Task.Yield();
            RefSnapshot.Take(_ => Task.FromResult(_output), root: Root);
            await AskAsync(Root, ByIds[0], asked, (0, "", ""));
            await AskAsync(Root, ByIds[0], asked, (0, "", ""));
        }

        await JudgeAsync();
        Assert.Single(asked);
        Assert.Null(RefSnapshot.Asked(Root, ByIds[0], () => Task.FromResult((0, "", ""))));

        RefSnapshot.Take(_ => Task.FromResult(_output));
        Assert.Null(RefSnapshot.Asked(Root, ByIds[0], () => Task.FromResult((0, "", ""))));
    }
}
