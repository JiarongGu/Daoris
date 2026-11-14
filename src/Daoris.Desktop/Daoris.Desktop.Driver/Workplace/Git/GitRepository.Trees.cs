using Daoris.Ports.Repositories;

namespace Daoris.Driver;

public sealed partial class GitRepository
{
    /// <summary>The act's own start: the process ring places it in the act's job and journals its pid, which no other git here does.</summary>
    public Task<GitHeldAnswer> RunHeldAsync(GitHeldRun run, GitHeldStart start) => start(run);

    public async Task<GitLayout?> LayoutAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["rev-parse", "--absolute-git-dir", "--git-common-dir"], ct).ConfigureAwait(false);
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return code != 0 || lines.Length < 2 ? null : new GitLayout(Path.GetFullPath(lines[0]), Path.GetFullPath(Path.Combine(folder, lines[1])));
    }
}
