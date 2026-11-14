using Daoris.Ports.Repositories;

namespace Daoris.Driver;

public sealed partial class GitRepository
{
    public async Task<GitMerged> MergeAsync(string folder, GitMergeAsk ask, CancellationToken ct)
    {
        IReadOnlyList<string> arguments = ask.Kind switch
        {
            GitMergeKind.FastForwardOnly => ["merge", "--ff-only", "--quiet", ask.Commit],
            GitMergeKind.Commit => ["merge", "--no-ff", "-m", ask.Message ?? $"Merge {ask.Commit}", ask.Commit],
            _ => ["merge", "--no-ff", "--no-commit", ask.Commit],
        };
        var (code, stdout, stderr) = await RunGitAsync(folder, arguments, ct).ConfigureAwait(false);
        if (code == 0)
        {
            var staged = ask.Kind == GitMergeKind.Stage;
            return new GitMerged(true, "", [], staged);
        }

        var pending = (await RunGitAsync(folder, ["rev-parse", "--quiet", "--verify", "MERGE_HEAD"], ct).ConfigureAwait(false)).Code == 0;
        return new GitMerged(false, stderr.Length > 0 ? stderr : stdout, pending ? await UnmergedAsync(folder, ct).ConfigureAwait(false) : [], pending);
    }

    public async Task<GitDone> MergeAbortAsync(string folder, CancellationToken ct)
    {
        if ((await RunGitAsync(folder, ["rev-parse", "--quiet", "--verify", "MERGE_HEAD"], ct).ConfigureAwait(false)).Code != 0) return GitDone.Done;
        return Done(await RunGitAsync(folder, ["merge", "--abort"], ct).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<string>> UnmergedAsync(string folder, CancellationToken ct)
    {
        var (code, stdout, _) = await RunGitAsync(folder, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", "--diff-filter=U"], ct).ConfigureAwait(false);
        return code != 0 ? [] : [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    public async Task<GitDone> CommitStagedAsync(string folder, string message, CancellationToken ct) =>
        Done(await RunGitAsync(folder, ["commit", "--quiet", "--no-verify", "-m", message], ct).ConfigureAwait(false));

    public async Task<GitDone> PutBackAsync(string folder, string commit, CancellationToken ct)
    {
        await RunGitAsync(folder, ["merge", "--abort"], ct).ConfigureAwait(false);
        foreach (var arguments in new[]
        {
            new[] { "reset", "--quiet", "--hard" }, new[] { "clean", "--quiet", "-fd" }, new[] { "checkout", "--quiet", "--force", "--detach", commit },
        })
        {
            var ran = await RunGitAsync(folder, arguments, ct).ConfigureAwait(false);
            if (ran.Code != 0) return GitDone.Not(ran.Stderr.Trim());
        }

        return GitDone.Done;
    }
}
