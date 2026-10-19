using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every git call has a bound (PARALLEL1e, D181 point 4): git's calls ran with no timeout, so one that never answered (a
/// lock, a prompt nobody types, a scan) held its caller for ever. The default is the call's kind's; a caller that knows its
/// call is long (a fetch, a replay) passes its own.
/// </summary>
/// <remarks>
/// The fast half: the bounds read from the arguments, and git's run stood in for through <see cref="WorkingTree.Runs"/>, so no
/// process starts. That a run past its patience, or whose token is cancelled, has its tree ended is the runner's, held by
/// <c>ChildProcessTests</c>.
/// </remarks>
public sealed class GitBoundsTests
{
    [Theory]
    [InlineData("rev-parse", "--verify", "HEAD")]
    [InlineData("status", "--porcelain=v1")]
    [InlineData("log", "--format=%H", "-1")]
    [InlineData("diff", "--name-only", "a", "b")]
    [InlineData("merge-base", "--is-ancestor", "a", "b")]
    [InlineData("for-each-ref", "refs/heads")]
    [InlineData("cat-file", "blob", "abc")]
    [InlineData("ls-files", "-z")]
    [InlineData("worktree", "list", "--porcelain")]
    [InlineData("update-ref", "refs/heads/x", "abc")]
    [InlineData("branch", "--show-current")]
    public void A_question_or_a_refs_move_is_bounded_as_a_question(params string[] arguments)
    {
        Assert.Equal(GitBounds.Question, GitBounds.Of(arguments));
    }

    [Theory]
    [InlineData("worktree", "add", "path", "branch")]
    [InlineData("worktree", "remove", "--force", "path")]
    [InlineData("switch", "main")]
    [InlineData("rebase", "--onto", "a", "b")]
    [InlineData("merge", "--ff-only", "a")]
    [InlineData("commit", "-m", "x")]
    [InlineData("clone", "--no-checkout", "a", "b")]
    [InlineData("fetch", "origin")]
    [InlineData("push", "origin", "x")]
    [InlineData("checkout", "--detach", "abc")]
    [InlineData("stash", "push")]
    [InlineData("gc")]
    public void Work_on_a_tree_or_the_network_is_bounded_as_work(params string[] arguments)
    {
        Assert.Equal(GitBounds.Work, GitBounds.Of(arguments));
    }

    /// <summary>A command not named is given the longer bound: a write cut short would be left half done.</summary>
    [Fact]
    public void A_command_not_named_is_bounded_as_work()
    {
        Assert.Equal(GitBounds.Work, GitBounds.Of(["filter-repo", "--force"]));
        Assert.Equal(GitBounds.Work, GitBounds.Of([]));
    }

    [Fact]
    public void Options_before_the_command_are_read_past()
    {
        Assert.Equal(GitBounds.Work, GitBounds.Of(["-c", "rebase.updateRefs=false", "-c", "rebase.autoStash=false", "rebase", "--onto", "a", "b"]));
        Assert.Equal(GitBounds.Question, GitBounds.Of(["-C", "some/where", "status"]));
        Assert.Equal(GitBounds.Question, GitBounds.Of(["--no-pager", "--git-dir=x/.git", "log", "-1"]));
    }

    [Fact]
    public void Every_bound_is_finite_and_a_question_the_shorter()
    {
        Assert.True(GitBounds.Question > TimeSpan.Zero);
        Assert.True(GitBounds.Work > GitBounds.Question);
        Assert.NotEqual(Timeout.InfiniteTimeSpan, GitBounds.Question);
        Assert.NotEqual(Timeout.InfiniteTimeSpan, GitBounds.Work);
    }

    [Fact]
    public void A_callers_own_bound_is_kept()
    {
        var long_ = TimeSpan.FromMinutes(30);

        Assert.Equal(long_, GitBounds.Of(["clone", "a", "b"], long_));
        Assert.Equal(SyncBounds.Fetch, GitBounds.Of(["fetch", "origin"], SyncBounds.Fetch));
        Assert.Equal(GitBounds.Question, GitBounds.Of(["status"], null));
    }

    /// <summary>The runner is handed the kind's bound and the caller's own token, where the caller passed no bound.</summary>
    [Fact]
    public async Task A_question_runs_under_its_bound_and_its_callers_token()
    {
        using var cancel = new CancellationTokenSource();
        var seen = new List<(IReadOnlyList<string> Arguments, TimeSpan Bound, CancellationToken Token)>();

        await RunsAsync(
            (arguments, bound, ct) =>
            {
                seen.Add((arguments, bound, ct));
                return Task.FromResult(new ChildRun(true, 0, "abc\n", "", false));
            },
            async () =>
            {
                var (code, stdout, _) = await WorkingTree.GitAsync("repo", ["rev-parse", "HEAD"], cancel.Token);
                Assert.Equal(0, code);
                Assert.Equal("abc\n", stdout);
            });

        var (_, runBound, token) = Assert.Single(seen);
        Assert.Equal(GitBounds.Question, runBound);
        Assert.Equal(cancel.Token, token);
    }

    [Fact]
    public async Task A_bound_the_caller_passes_is_the_one_its_git_runs_under()
    {
        var bounds = new List<TimeSpan>();

        await RunsAsync(
            (_, bound, _) =>
            {
                bounds.Add(bound);
                return Task.FromResult(new ChildRun(true, 0, "", "", false));
            },
            () => WorkingTree.GitAsync("repo", ["fetch", "origin"], environment: null, SyncBounds.Fetch, CancellationToken.None));

        Assert.Equal([SyncBounds.Fetch], bounds);
    }

    /// <summary>
    /// The row's proof (PARALLEL1e): a git that never answers ends at its bound (the runner ends its tree there), and its answer
    /// is a failure saying so, which a caller reads as it reads any failed read: here a landed proof, unknown.
    /// </summary>
    [Fact]
    public async Task A_git_that_never_answers_ends_at_its_bound_and_its_caller_reads_unknown()
    {
        (int, string, string)? answer = null;
        string? kind = null;

        await RunsAsync(
            (arguments, _, _) => Task.FromResult(arguments[0] switch
            {
                "merge-base" => new ChildRun(true, null, "", "", TimedOut: true),
                "rev-list" => new ChildRun(true, 0, "1\n", "", false),
                _ => new ChildRun(true, 1, "", "", false),
            }),
            async () =>
            {
                answer = await WorkingTree.GitAsync("repo", ["merge-base", "a", "b"], CancellationToken.None);
                kind = (await LandedProofs.ProveAsync(
                    "repo", "tip", "main", [new LandedProofs.LineForm("main", "refs/heads/main")], CancellationToken.None)).Kind;
            });

        Assert.Equal(-1, answer!.Value.Item1);
        Assert.Equal($"git did not answer within {GitBounds.Question.TotalSeconds:0} seconds, and was stopped.", answer.Value.Item3);
        Assert.Equal(LandedKind.Unknown, kind);
    }

    /// <summary>A caller that stops waiting stops its git: the token reaches the runner, which ends git's tree on it.</summary>
    [Fact]
    public async Task A_cancelled_caller_ends_its_git()
    {
        using var cancel = new CancellationTokenSource();
        var handed = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = false;

        await RunsAsync(
            async (_, bound, ct) =>
            {
                handed.TrySetResult(ct);
                try
                {
                    // A git that never answers, ended only by its bound or its caller.
                    await Task.Delay(bound, ct);
                    return new ChildRun(true, null, "", "", TimedOut: true);
                }
                catch (OperationCanceledException)
                {
                    ended = true;
                    throw;
                }
            },
            async () =>
            {
                var git = WorkingTree.GitAsync("repo", ["status", "--porcelain"], cancel.Token);
                await handed.Task.WaitAsync(Poll.Patience);
                await cancel.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => git.WaitAsync(Poll.Patience));
            });

        Assert.True(ended, "the git ran on past its caller's cancellation");
    }

    /// <summary>Runs <paramref name="body"/> with git's run stood in for by <paramref name="run"/>, the seam closed after.</summary>
    private static async Task RunsAsync(Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ChildRun>> run, Func<Task> body)
    {
        WorkingTree.Runs.Value = run;
        try
        {
            await body();
        }
        finally
        {
            WorkingTree.Runs.Value = null;
        }
    }
}
