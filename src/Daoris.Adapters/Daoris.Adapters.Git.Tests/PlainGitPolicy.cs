using Daoris.Ports.Git;

namespace Daoris.Adapters.Git.Tests;

/// <summary>
/// The least policy a git adapter runs under: the git the system finds, the library answering nothing, no place to wait for and no
/// count, the bound the ask gives or two minutes.
/// </summary>
internal sealed class PlainGitPolicy : IGitPolicy
{
    public Task<GitAnswer?> AnswerInProcessAsync(GitAsk ask, CancellationToken ct) => Task.FromResult<GitAnswer?>(null);

    public Task<GitStart> StartAsync(GitAsk ask, GitRunKind kind, CancellationToken ct) =>
        Task.FromResult(new GitStart("git", ask.Arguments, new Dictionary<string, string?>(ask.Environment?.Select(each => KeyValuePair.Create(each.Key, (string?)each.Value)) ?? []),
            ask.Bound ?? TimeSpan.FromMinutes(2)));

    public Task<T> LaunchAsync<T>(GitAsk ask, GitRunKind kind, Func<Task<T>> run) => run();

    public Task<T> PlaceAsync<T>(GitAsk ask, GitStart start, GitRunKind kind, Func<CancellationToken, Task<T>> run, CancellationToken ct) => run(ct);

    public void Unended(int id, string reason)
    {
    }

    public string? InUse(string? home) => "git";
}
