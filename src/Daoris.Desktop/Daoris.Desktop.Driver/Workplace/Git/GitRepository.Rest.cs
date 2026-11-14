using Daoris.Ports.Repositories;

namespace Daoris.Driver;

public sealed partial class GitRepository
{
    public async Task<bool> AvailableAsync(CancellationToken ct) =>
        (await RunGitAsync(AppContext.BaseDirectory, ["--version"], ct).ConfigureAwait(false)).Code == 0;

    public async Task<GitDone> InitAsync(string folder, string line, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var init = Done(await RunGitAsync(folder, ["init", "-q"], ct).ConfigureAwait(false));
        return init.Ok ? Done(await RunGitAsync(folder, ["symbolic-ref", "HEAD", $"refs/heads/{line}"], ct).ConfigureAwait(false)) : init;
    }

    public async Task<GitDone> CommitEmptyAsync(string folder, string name, string email, string message, CancellationToken ct) =>
        Done(await RunGitAsync(folder, [.. GitAuthor.Arguments((name, email)), "-c", "commit.gpgsign=false", "commit", "--allow-empty", "--no-verify", "-q", "-m", message], ct).ConfigureAwait(false));
}
