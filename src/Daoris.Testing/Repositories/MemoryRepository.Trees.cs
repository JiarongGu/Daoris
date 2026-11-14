using Daoris.Ports.Repositories;

namespace Daoris.Testing;

public sealed partial class MemoryRepository
{
    private readonly List<GitHeldRun> _held = [];

    /// <summary>The held gits an act asked this repository, in the order it asked them.</summary>
    public IReadOnlyList<GitHeldRun> HeldRuns => _held;

    /// <summary>
    /// Answers an act's held git in memory instead of starting it: <c>update-ref --stdin</c> is a ref transaction, made as git makes it, and
    /// any other git is refused with the words git gives for a command it was never asked, so a test finds what it must teach this repository.
    /// </summary>
    public Task<GitHeldAnswer> RunHeldAsync(GitHeldRun run, GitHeldStart start)
    {
        lock (_held) _held.Add(run);
        if (Find(run.Folder) is null) return Task.FromResult(new GitHeldAnswer(128, "", $"fatal: not a git repository: {run.Folder}", false, ""));
        if (run.Arguments is not ["update-ref", "--stdin"]) return Task.FromResult(new GitHeldAnswer(1, "", $"memory repository does not answer `git {string.Join(' ', run.Arguments)}`", false, ""));
        if (MemoryUpdateRefs.Read(run.Input ?? "") is not { } transaction) return Task.FromResult(new GitHeldAnswer(128, "", "fatal: update-ref: unknown command", false, ""));
        var made = MemoryRefTransaction.Run(_history, transaction, CaseIgnored);
        return Task.FromResult(new GitHeldAnswer(made.Done ? 0 : 128, "", made.Done ? "" : $"fatal: {made.Said}", false, ""));
    }

    public Task<GitLayout?> LayoutAsync(string folder, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult<GitLayout?>(null);
        var common = Path.Combine(Root, ".git");
        var own = tree == _trees[0] ? common : Path.Combine(common, "worktrees", Path.GetFileName(tree.Path));
        return Task.FromResult<GitLayout?>(new GitLayout(own, common));
    }
}
