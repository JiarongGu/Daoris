using Daoris.Driver;
using Daoris.Ports.Git;
using Daoris.Testing;
using GitAsk = Daoris.Ports.Git.GitAsk;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git a test chooses for its own flow, over the port the test root hands (<see cref="TestPorts.Git"/>): one that watches what is
/// asked, one that answers some asks itself, or one that stands in for every run. Each lasts until disposed, in the flow that chose it
/// and what that flow awaits, so classes running beside it never see it.
/// </summary>
internal static class GitFlow
{
    /// <summary>
    /// Set, it is told the arguments of each git this flow launches, the held ones and the stand-ins' too, as the launch's count keeps
    /// them, before git starts, until set again; a read the library answers launches nothing.
    /// </summary>
    internal static FlowChoice<Action<IReadOnlyList<string>>> Started { get; } = new(seen => GitLaunch.Count(start => seen(start.Arguments)));

    /// <summary>Set, it answers each git it gives a reply for (<see cref="Answer"/>) until set again.</summary>
    internal static FlowChoice<Func<IReadOnlyList<string>, (int Code, string Stdout, string Stderr)?>> Answered { get; } = new(Answer);

    /// <summary>Set, it stands in for every whole and streamed run (<see cref="Run"/>) until set again.</summary>
    internal static FlowChoice<Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ChildRun>>> Runs { get; } = new(Run);

    /// <summary>One choice of a flow's git, made by setting <see cref="Value"/> and ended by setting it again, in the flow that set it.</summary>
    internal sealed class FlowChoice<T>(Func<T, IDisposable> choose) where T : class
    {
        private readonly AsyncLocal<(T Value, IDisposable Chosen)?> _held = new();

        public T? Value
        {
            get => _held.Value?.Value;
            set
            {
                _held.Value?.Chosen.Dispose();
                _held.Value = value is null ? null : (value, choose(value));
            }
        }
    }


    /// <summary>Tells <paramref name="seen"/> the arguments of each git asked, every one still asked of the git beneath.</summary>
    internal static IDisposable Watch(Action<IReadOnlyList<string>> seen) =>
        FlowGit.Use(new ScriptedGit().Seen(ask => seen(ask.Arguments)).Beyond(TestPorts.Git.Current));

    /// <summary>Answers each git <paramref name="answer"/> gives a reply for, as git's code and streams; every other is asked of the git beneath.</summary>
    internal static IDisposable Answer(Func<IReadOnlyList<string>, (int Code, string Stdout, string Stderr)?> answer) =>
        FlowGit.Use(new ScriptedGit()
            .On(ask => answer(ask.Arguments) is var (code, stdout, stderr)
                ? Launched(ask.Arguments, GitRunner.Whole, () => Task.FromResult(new GitAnswer(code, stdout, stderr))).GetAwaiter().GetResult()
                : null)
            .Beyond(TestPorts.Git.Current));

    /// <summary>A stand-in's answer, counted and timed as the launch it stands in for, so a count of the flow's starts holds it.</summary>
    private static Task<T> Launched<T>(IReadOnlyList<string> arguments, GitRunner runner, Func<Task<T>> answer) =>
        GitLaunch.LaunchAsync(GitLaunch.KindOf(arguments), arguments, GitLaunch.Budget, runner, answer);

    /// <summary>
    /// Stands in for every whole and streamed run, handed the arguments, the bound the run is given and the caller's token, so a test
    /// sees the bound a kind is given and the token that reaches git; a byte read is still asked of the git beneath.
    /// </summary>
    internal static IDisposable Run(Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ChildRun>> run) =>
        FlowGit.Use(new StandIn(run, TestPorts.Git.Current));

    private sealed class StandIn(Func<IReadOnlyList<string>, TimeSpan, CancellationToken, Task<ChildRun>> run, IGit beneath) : IGit
    {
        public Task<GitAnswer> RunAsync(GitAsk ask, CancellationToken ct) => Launched(ask.Arguments, GitRunner.Whole, async () =>
        {
            var bound = ask.Bound ?? GitBounds.Of(ask.Arguments);
            return GitProcessRuns.Answer(await run(ask.Arguments, bound, ct).ConfigureAwait(false), bound);
        });

        public Task<GitBytes> ReadBytesAsync(GitAsk ask, int limit, CancellationToken ct) => beneath.ReadBytesAsync(ask, limit, ct);

        public Task<int?> ReadAsync(GitAsk ask, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct) => Launched(ask.Arguments, GitRunner.Streamed, async () =>
        {
            var ran = await run(ask.Arguments, ask.Bound ?? GitBounds.Of(ask.Arguments), ct).ConfigureAwait(false);
            if (!ran.Started || ran.TimedOut) return (int?)null;
            return take(ran.Stdout.AsMemory()) ? ran.ExitCode!.Value : 0;
        });

        public string? InUse(string? home) => beneath.InUse(home);
    }
}
