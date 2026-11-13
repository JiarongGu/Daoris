using System.Diagnostics;
using System.Runtime.CompilerServices;
using Daoris.Ports.Git;

namespace Daoris.Driver;

/// <summary>
/// What Daoris decides about each git the git adapter runs, as a root hands it (<see cref="IGitPolicy"/>): which program, by the tools'
/// choice, the home's entries and what a lazy fetch needs (<see cref="GitRuns.GitStart"/>, <see cref="GitLazyFetch"/>); the reads the
/// git library answers without a start (<see cref="GitLibraryReads"/>); the place the scheduler issues (<see cref="GitScheduler"/>);
/// and the count and timing each start is kept in (<see cref="GitLaunch"/>). The adapter runs what this resolves and decides none of it.
/// </summary>
public sealed class GitRunPolicy : IGitPolicy
{
    /// <summary>The one instance a root hands.</summary>
    public static GitRunPolicy Shared { get; } = new();

    // The start as the scheduler classifies it: kept beside the answer so a placed run reads the start it was resolved from.
    private static readonly ConditionalWeakTable<GitStart, ProcessStartInfo> s_infos = new();

    private GitRunPolicy()
    {
    }

    public string? InUse(string? home) => GitRuns.GitInUse(home);

    public async Task<GitAnswer?> AnswerInProcessAsync(Ports.Git.GitAsk ask, CancellationToken ct)
    {
        // A read with variables, an input or a bound of its own is always git's.
        if (ask.Environment is not null || ask.Input is not null || ask.Bound != GitBounds.Of(ask.Arguments)) return null;
        return await GitLibraryReads.AnswerAsync(ask.Folder, ask.Arguments, ct).ConfigureAwait(false) is { } answered
            ? new GitAnswer(answered.Code, answered.Stdout, "")
            : null;
    }

    public async Task<GitStart> StartAsync(Ports.Git.GitAsk ask, GitRunKind kind, CancellationToken ct)
    {
        var bound = ask.Bound ?? GitBounds.Of(ask.Arguments);
        var (info, refusal) = GitRuns.GitStart(ask.Folder, ask.Arguments, ask.Home);
        if (info is null) return new GitStart(null, [], new Dictionary<string, string?>(), bound, refusal);
        foreach (var (name, value) in ask.Environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        await GitLazyFetch.PrepareAsync(info, ask.Arguments, ct).ConfigureAwait(false);
        var start = new GitStart(info.FileName, [.. info.ArgumentList], EnvironmentOf(info), bound);
        s_infos.Add(start, info);
        return start;
    }

    public Task<T> LaunchAsync<T>(Ports.Git.GitAsk ask, GitRunKind kind, Func<Task<T>> run) =>
        GitLaunch.LaunchAsync(GitLaunch.KindOf(ask.Arguments), ask.Arguments, GitLaunch.Budget, RunnerOf(kind), run);

    public Task<T> PlaceAsync<T>(Ports.Git.GitAsk ask, GitStart start, GitRunKind kind, Func<CancellationToken, Task<T>> run, CancellationToken ct)
    {
        var info = s_infos.TryGetValue(start, out var resolved) ? resolved : throw new InvalidOperationException("the git was placed without being resolved by this policy");
        var runner = RunnerOf(kind);
        var placed = GitStartAsks.For(info, ask.Arguments, runner, start.Bound, shares: ask.Input is null && kind == GitRunKind.Whole, kind == GitRunKind.Streamed ? null : ask.Input);
        // An object read's question is its input: two of them start the same command, which a count tells apart by it.
        return GitScheduler.Shared.RunAsync(placed, token =>
        {
            using (kind == GitRunKind.Bytes && ask.Input is not null ? GitStartInputs.Placing(ask.Input) : null) return run(token);
        }, ct);
    }

    public void Unended(int id, string reason) => ChildProcess.Told(id, reason);

    private static GitRunner RunnerOf(GitRunKind kind) => kind switch
    {
        GitRunKind.Streamed => GitRunner.Streamed,
        GitRunKind.Bytes => GitRunner.Bytes,
        _ => GitRunner.Whole,
    };

    /// <summary>The variables the start takes beside the host's: each one it sets or changes, and each of the host's it removes (null).</summary>
    private static Dictionary<string, string?> EnvironmentOf(ProcessStartInfo info)
    {
        var diff = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in info.Environment)
        {
            if (Environment.GetEnvironmentVariable(name) != value) diff[name] = value;
        }

        foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (!info.Environment.ContainsKey(name)) diff[name] = null;
        }

        return diff;
    }
}
