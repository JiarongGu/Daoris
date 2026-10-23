using System.Diagnostics;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP1a-i (D202 point 2, the git-app design P1.5): one counting seam beneath the three runners. Each start raises
/// <see cref="WorkingTree.Started"/>, is classified by kind and node, counted in its flow's budget and timed at the gate; and it
/// reaches git with the arguments its call site passed, nothing added (Codex R3-9). No git runs here: the gate's stand-in sees each
/// start's <see cref="ProcessStartInfo"/> and answers it with none.
/// </summary>
public sealed class GitLaunchTests
{
    /// <summary>
    /// The census of today's arguments per kind: every command Review's chain starts, as its call site spells it (REVIEWLOCK1's
    /// flags among them), with the kind and the review's node the seam names it by. A call site that changes its arguments changes a
    /// row here, and the Process half's <c>ReviewStartsTests</c> holds the chain sends exactly these.
    /// </summary>
    public static TheoryData<string[], GitKind, string> Today => new()
    {
        { ["rev-parse", "--show-toplevel"], GitKind.Layout, "T" },
        { ["rev-parse", "--path-format=absolute", "--git-common-dir"], GitKind.Layout, "P" },
        { ["merge-base", "--is-ancestor", "0123abcd", "HEAD"], GitKind.Walk, "K" },
        { ["rev-parse", "--abbrev-ref", "HEAD"], GitKind.Layout, "E" },
        { ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], GitKind.Layout, "G1" },
        { ["rev-parse", "--verify", "--quiet", "refs/heads/main^{commit}"], GitKind.Layout, "G2" },
        { ["rev-parse", "--verify", "--quiet", "refs/heads/master^{commit}"], GitKind.Layout, "G3" },
        { ["merge-base", "HEAD", "refs/heads/main"], GitKind.Walk, "B1" },
        { ["merge-base", "HEAD", "refs/remotes/origin/main"], GitKind.Walk, "B2" },
        { ["rev-list", "--count", "refs/heads/main", "refs/remotes/origin/main", "--not", "HEAD"], GitKind.Walk, "N1" },
        { ["rev-list", "--count", "HEAD", "--not", "refs/heads/main", "refs/remotes/origin/main"], GitKind.Walk, "N2" },
        { ["merge-base", "--independent", "0123abcd", "4567cdef"], GitKind.Walk, "I" },
        { ["merge-tree", "--write-tree", "0123abcd", "4567cdef"], GitKind.MergeTree, "M" },
        { Range("0123abcd..HEAD"), GitKind.ObjectDiff, "D" },
        { Range("HEAD"), GitKind.WorktreeDiff, "W" },
        { ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"], GitKind.Status, "S" },
        {
            [
                "--no-optional-locks", "-c", "core.quotePath=false", "diff", "--no-index", "--no-color", "--no-ext-diff", "--src-prefix=a/",
                "--dst-prefix=b/", "--", "/dev/null", "new.txt",
            ],
            GitKind.UntrackedDiff, "U"
        },
        { ["symbolic-ref", "--short", "-q", "HEAD"], GitKind.Layout, "H" },
    };

    /// <summary><c>WorkingTree.RangeAsync</c>'s arguments over <paramref name="range"/>, as it spells them.</summary>
    private static string[] Range(string range) =>
    [
        "--no-optional-locks", "-c", "diff.autoRefreshIndex=false", "-c", "core.quotePath=false", "diff",
        "--no-color", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "-M", "-z", "--raw", "--numstat", "-p", range,
    ];

    [Theory]
    [MemberData(nameof(Today))]
    public void Each_of_todays_review_commands_is_classed_by_its_kind_and_named_by_its_node(string[] arguments, GitKind kind, string node)
    {
        Assert.Equal(kind, GitLaunch.KindOf(arguments));
        Assert.Equal(node, GitLaunch.NodeOf(arguments, node == "U" ? GitBudget.Untracked : GitBudget.Review));
        // Outside the review's budgets a start is named by its subcommand: the offer's own `status` is no review's S.
        Assert.Equal(GitLaunch.Command(arguments)[0], GitLaunch.NodeOf(arguments, GitBudget.Offer));
    }

    [Theory]
    [InlineData(new[] { "cat-file", "blob", "0123abcd" }, GitKind.Object)]
    [InlineData(new[] { "log", "-1", "--format=%H%n%cI" }, GitKind.Walk)]
    [InlineData(new[] { "log", "-p", "HEAD" }, GitKind.ObjectDiff)]
    [InlineData(new[] { "--no-optional-locks", "diff", "--cached", "--name-only" }, GitKind.IndexDiff)]
    [InlineData(new[] { "diff", "0123abcd", "4567cdef" }, GitKind.ObjectDiff)]
    [InlineData(new[] { "blame", "--porcelain", "a.txt" }, GitKind.Blame)]
    [InlineData(new[] { "update-ref", "--stdin" }, GitKind.Other)]
    [InlineData(new[] { "-c", "core.longpaths=true", "worktree", "list", "--porcelain" }, GitKind.Layout)]
    public void The_other_kinds_are_read_from_the_command_past_gits_own_options(string[] arguments, GitKind kind) =>
        Assert.Equal(kind, GitLaunch.KindOf(arguments));

    /// <summary>The three runners, each started through the seam once.</summary>
    public static TheoryData<GitRunner> Runners => new() { GitRunner.Whole, GitRunner.Streamed, GitRunner.Bytes };

    private static Task Start(GitRunner runner, string root, IReadOnlyList<string> arguments) => runner switch
    {
        GitRunner.Whole => WorkingTree.GitAsync(root, arguments, CancellationToken.None),
        GitRunner.Streamed => WorkingTree.ReadGitAsync(root, arguments, _ => true, CancellationToken.None),
        _ => WorkingTree.GitBytesAsync(root, arguments, 16, CancellationToken.None),
    };

    /// <summary>
    /// Every runner raises <see cref="WorkingTree.Started"/> once (once <c>RunGitAsync</c>'s alone), counts its start in its flow's
    /// budget with its kind and node, passes the gate, and hands git its call site's arguments after the one option every git has
    /// always been given (<c>core.longpaths</c>, <c>GitStart</c>'s), nothing more.
    /// </summary>
    [Theory]
    [MemberData(nameof(Runners))]
    public async Task Every_runner_starts_through_the_seam_once_with_its_arguments_unchanged(GitRunner runner)
    {
        string[] arguments = ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"];
        var raised = new List<IReadOnlyList<string>>();
        var handed = new List<IReadOnlyList<string>>();
        WorkingTree.Started.Value = raised.Add;
        try
        {
            using var count = GitLaunch.Count();
            using (GitLaunch.Within(GitBudget.Review))
            using (ProcessStarts.Within(info =>
                   {
                       lock (handed) handed.Add([.. info.ArgumentList]);
                       return null;
                   }))
            {
                await Start(runner, AppContext.BaseDirectory, arguments);
            }

            var start = Assert.Single(count.Starts);
            Assert.Equal(runner, start.Runner);
            Assert.Equal(GitKind.Status, start.Kind);
            Assert.Equal("S", start.Node);
            Assert.Equal(GitBudget.Review, start.Budget);
            Assert.True(start.Gated, "the start passed the gate");
            Assert.Equal(arguments, Assert.Single(raised));
            Assert.Equal(["-c", "core.longpaths=true", .. arguments], Assert.Single(handed));
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }
    }

    /// <summary>The gate's wait for a place, the creation inside it and the run are three numbers, never one (the design §1.2).</summary>
    [Fact]
    public async Task A_start_is_timed_at_the_gate_with_its_creation_apart_from_its_run()
    {
        using var count = GitLaunch.Count();
        using (ProcessStarts.Within(_ =>
               {
                   Thread.Sleep(60);
                   return null;
               }))
        {
            await WorkingTree.ReadGitAsync(AppContext.BaseDirectory, ["rev-parse", "--show-toplevel"], _ => true, CancellationToken.None);
        }

        var start = Assert.Single(count.Starts);
        Assert.Equal(GitBudget.None, start.Budget);
        Assert.Equal("rev-parse", start.Node);
        Assert.True(GitLaunched.Ms(start.CreatedTicks) >= 50, $"the creation was {GitLaunched.Ms(start.CreatedTicks)} ms");
        Assert.True(start.WholeTicks >= start.WaitedTicks + start.CreatedTicks);
    }

    /// <summary>A test's stand-in answer is counted as a start, as <c>Started</c> always counted it, and never passes the gate.</summary>
    [Fact]
    public async Task A_stand_in_answer_is_counted_and_not_gated()
    {
        WorkingTree.Answered.Value = _ => (0, "", "");
        try
        {
            using var count = GitLaunch.Count();
            await WorkingTree.GitAsync(AppContext.BaseDirectory, ["rev-parse", "--show-toplevel"], CancellationToken.None);

            Assert.False(Assert.Single(count.Starts).Gated);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }

    /// <summary>A budget is the flow's for its span alone, and an inner count's starts are its outer count's too.</summary>
    [Fact]
    public async Task A_budget_holds_for_its_span_and_counts_nest()
    {
        WorkingTree.Answered.Value = _ => (0, "", "");
        try
        {
            using var outer = GitLaunch.Count();
            using (GitLaunch.Within(GitBudget.Offer))
            {
                using var inner = GitLaunch.Count();
                await WorkingTree.GitAsync(AppContext.BaseDirectory, ["status"], CancellationToken.None);
                Assert.Equal(1, inner.In(GitBudget.Offer));
            }

            await WorkingTree.GitAsync(AppContext.BaseDirectory, ["status"], CancellationToken.None);

            Assert.Equal([GitBudget.Offer, GitBudget.None], outer.Starts.Select(start => start.Budget));
            Assert.Equal(GitBudget.None, GitLaunch.Budget);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }

    /// <summary>
    /// The source census (the design §10.1, P1.5): under <c>Trees/</c>, git starts through <c>WorkingTree.GitStart</c>, and only the
    /// three runners beneath the seam call it or hand a start to the gate. A git started there any other way would be uncounted.
    /// </summary>
    [Fact]
    public void No_git_is_started_under_trees_outside_the_seam()
    {
        var root = Path.Combine(EveryChildIsHandedTheToolsTests.SourceRoot(), "Daoris.Desktop.Driver", "Trees");
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var source = Regex.Replace(File.ReadAllText(file).Replace("\r\n", "\n"), @"//[^\n]*", "");
            // git's start info (GitStart), a start handed to the gate, a child run, the runtime's start, or a git's info built by hand.
            var starts = Regex.Matches(
                source,
                @"\bGitStart\s*\(|\bProcessStarts\.\w+\.(?:StartAsync|RunAsync)\s*\(|\bStartAsync\s*\(\s*info\b|\bChildProcess\.RunAsync\s*\(|\bProcess\.Start\b|ProcessStartInfo\s*\(\s*""git").Count;
            if (starts > 0) found.Add($"{relative}: {starts}");
        }

        // WorkingTree: GitStart's definition, its three calls (one per runner), the Whole runner's two ChildProcess runs and the other
        // two runners' gate starts. GeneratedWriters: a generator's shell (GEN1), which is no git.
        Assert.Equal(["SessionTrees/GeneratedWriters.cs: 1", "WorkingTree.cs: 8"], found.Order(StringComparer.Ordinal));

        var tree = File.ReadAllText(Path.Combine(root, "WorkingTree.cs"));
        foreach (var runner in new[] { "RunLaunchedAsync", "BytesLaunchedAsync", "ReadLaunchedAsync" })
        {
            Assert.Contains($"() => {runner}(", tree, StringComparison.Ordinal);
            Assert.Equal(2, Regex.Matches(tree, $@"\b{runner}\(").Count);
        }

        Assert.Equal(3, Regex.Matches(tree, @"GitLaunch\.LaunchAsync\(").Count);
    }
}
