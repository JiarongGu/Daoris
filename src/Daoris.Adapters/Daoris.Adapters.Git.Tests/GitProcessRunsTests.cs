using System.Diagnostics;
using Daoris.Adapters.Machine;
using Daoris.Ports.Git;

namespace Daoris.Adapters.Git.Tests;

/// <summary>What only real git shows of the adapter: a run ended past its bound and at its caller's cancellation, and a policy's refusal.</summary>
public sealed class GitProcessRunsTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static GitProcessRuns Runs(IGitPolicy? policy = null) =>
        new(new MachineProcessHost(new StartPlaces(4, 3), (_, _) => null), policy ?? new PlainGitPolicy());

    // A git alias that runs the system's ping for a minute: a git that answers nothing until it is ended.
    private static GitAsk Naps(TimeSpan? bound) =>
        new(AppContext.BaseDirectory, ["-c", "alias.nap=!ping -n 60 127.0.0.1", "nap"], GitWait.Read, Writes: false, Bound: bound);

    [Fact]
    public async Task A_run_past_its_bound_is_ended_and_says_it_did_not_answer()
    {
        var clock = Stopwatch.StartNew();

        var answer = await Runs().RunAsync(Naps(TimeSpan.FromSeconds(1)), CancellationToken.None).WaitAsync(Patience);

        Assert.Null(answer.Exit);
        Assert.Contains("did not answer within 1 seconds", answer.Error);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task A_run_whose_caller_cancels_is_ended_and_throws_the_cancellation()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Runs().RunAsync(Naps(TimeSpan.FromMinutes(5)), cancel.Token).WaitAsync(Patience));
    }

    [Fact]
    public async Task A_read_past_its_bound_answers_none()
    {
        var exit = await Runs().ReadAsync(Naps(TimeSpan.FromSeconds(1)), _ => true, CancellationToken.None).WaitAsync(Patience);

        Assert.Null(exit);
    }

    [Fact]
    public async Task A_git_the_policy_refuses_is_answered_in_the_refusals_words_and_starts_nothing()
    {
        var answer = await Runs(new RefusingPolicy()).RunAsync(Naps(null), CancellationToken.None);

        Assert.Null(answer.Exit);
        Assert.Equal("the git chosen cannot run", answer.Error);
    }

    private sealed class RefusingPolicy : IGitPolicy
    {
        private readonly PlainGitPolicy _plain = new();

        public Task<GitAnswer?> AnswerInProcessAsync(GitAsk ask, CancellationToken ct) => _plain.AnswerInProcessAsync(ask, ct);

        public Task<GitRunSpec> StartAsync(GitAsk ask, GitRunKind kind, CancellationToken ct) =>
            Task.FromResult(new GitRunSpec(null, [], new Dictionary<string, string?>(), TimeSpan.FromMinutes(1), "the git chosen cannot run"));

        public Task<T> LaunchAsync<T>(GitAsk ask, GitRunKind kind, Func<Task<T>> run) => run();

        public Task<T> PlaceAsync<T>(GitAsk ask, GitRunSpec start, GitRunKind kind, Func<CancellationToken, Task<T>> run, CancellationToken ct) =>
            throw new InvalidOperationException("a refused git is placed nowhere");

        public void Unended(int id, string reason)
        {
        }

        public string? InUse(string? home) => null;
    }
}
