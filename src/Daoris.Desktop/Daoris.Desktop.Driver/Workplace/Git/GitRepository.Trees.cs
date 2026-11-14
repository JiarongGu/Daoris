using System.Globalization;
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

    public async Task<GitTreeStatus?> TreeStatusAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, TreeStatus.Ask, ct).ConfigureAwait(false);
        return code == 0 && TreeStatus.Parse(stdout) is { } read ? new GitTreeStatus(read.Changed, read.Head, read.Branch) : null;
    }

    public async Task<int?> UncommittedAsync(string folder, CancellationToken ct)
    {
        var (code, porcelain, _) = await RunGitAsync(folder, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        return code != 0 ? null : porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    /// <summary>One start for the repository, the working root and the branch where git gives all three; else the two asks one at a time, each with its own failure.</summary>
    public async Task<GitTreePlace> PlaceAsync(string folder, CancellationToken ct)
    {
        var (code, output, _) = await RunGitAsync(folder, ["rev-parse", "--path-format=absolute", "--git-common-dir", "--show-toplevel", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToArray();
        if (code == 0 && lines.Length == 3)
        {
            GitFacts.SameRepository(folder, lines[0]);
            return new GitTreePlace(0, lines[0], "", lines[2], true, lines[1]);
        }

        var (rootCode, commonDir, rootErr) = await RunGitAsync(folder, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (rootCode != 0) return new GitTreePlace(rootCode, commonDir, rootErr, "", false, "");
        GitFacts.SameRepository(folder, commonDir.Trim());
        var (_, branch, _) = await RunGitAsync(folder, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return new GitTreePlace(0, commonDir, rootErr, branch, false, "");
    }

    public async Task<IReadOnlyList<string>?> UnlandedAsync(string folder, string revision, string? except, CancellationToken ct)
    {
        var (code, log, _) = await RunGitAsync(
            folder,
            except is null
                ? ["log", "--oneline", revision, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes"]
                : ["log", "--oneline", revision, "--not", "--exclude=daoris/*", $"--exclude={except}", "--branches", "--exclude=*/daoris/*", "--remotes"],
            ct).ConfigureAwait(false);
        return code != 0 ? null : log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<IReadOnlyList<GitRefFact>?> RefFactsAsync(string folder, CancellationToken ct)
    {
        var (code, output, _) = await RunGitAsync(folder, ["for-each-ref", .. GitLibraryRefs.Listed], ct).ConfigureAwait(false);
        return code == 0 && RefSnapshot.Parse(output) is { } refs
            ? [.. refs.Entries.Select(each => new GitRefFact(each.Name, each.Object, each.Type, each.Upstream ?? "", each.UpstreamShort ?? "", each.Tree ?? ""))]
            : null;
    }

    public async Task<IReadOnlyList<Ports.Repositories.GitRef>?> HoldingRefsAsync(string folder, string commit, CancellationToken ct)
    {
        var (code, containing, _) = await RunGitAsync(folder, ["for-each-ref", "--contains", commit, $"--format={ContentHolds.UnaliasedFormat}"], ct).ConfigureAwait(false);
        return code != 0 ? null : [.. ContentHolds.Unaliased(containing).Select(pair => new Ports.Repositories.GitRef(pair.Ref, pair.Commit))];
    }

    public async Task<GitFreshTop> FreshTopAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(folder, ["rev-parse", "--show-toplevel"], new Dictionary<string, string>(), timeout: null, ct).ConfigureAwait(false);
        return new GitFreshTop(code == 0 && stdout.Trim().Length > 0, stdout, stderr);
    }

    public async Task<GitDone> RemoveTreeAsync(string folder, string path, bool force, CancellationToken ct) =>
        Done(await RunGitAsync(folder, force ? ["worktree", "remove", "--force", path] : ["worktree", "remove", path], ct).ConfigureAwait(false));
}
