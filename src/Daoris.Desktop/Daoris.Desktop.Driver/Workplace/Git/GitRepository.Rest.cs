using Daoris.Ports.Repositories;

namespace Daoris.Driver;

public sealed partial class GitRepository
{
    public async Task<IReadOnlyList<GitStatusEntry>> StatusEntriesAsync(string folder, CancellationToken ct)
    {
        var (code, said, error) = await RunGitAsync(folder, ["--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all"], ct).ConfigureAwait(false);
        if (code != 0) throw new DriverException($"git could not read the tree's status: {error.Trim()}");
        var entries = said.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var listed = new List<GitStatusEntry>();
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            var state = entry[..2];
            string? from = null;
            if (state[0] is 'R' or 'C' && at + 1 < entries.Length) from = entries[++at];
            listed.Add(new GitStatusEntry(entry[3..], state, from));
        }

        return listed;
    }

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
