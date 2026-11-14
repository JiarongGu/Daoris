using Daoris.Ports.Repositories;

namespace Daoris.Testing;

public sealed partial class MemoryRepository
{
    private readonly List<GitHeldRun> _held = [];

    /// <summary>The held gits an act asked this repository, in the order it asked them.</summary>
    public IReadOnlyList<GitHeldRun> HeldRuns => _held;

    /// <summary>
    /// Answers an act's held git in memory instead of starting it: <c>update-ref --stdin</c> is a ref transaction, made as git makes it, and
    /// any other git is refused with words naming it, so a test finds what it must teach this repository.
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

    public async Task<int?> UncommittedAsync(string folder, CancellationToken ct)
    {
        if (Find(folder) is null) return null;
        var status = await StatusAsync(folder, ignored: false, ct).ConfigureAwait(false);
        // Without `--untracked-files=all`, an untracked folder is one path.
        return status.Select(each => each.Value == "??" && each.Key.IndexOf('/', StringComparison.Ordinal) is var cut and >= 0 ? each.Key[..(cut + 1)] : each.Key).Distinct().Count();
    }

    public async Task<GitTreeStatus?> TreeStatusAsync(string folder, CancellationToken ct) =>
        Find(folder) is { } tree && await UncommittedAsync(folder, ct).ConfigureAwait(false) is { } changed ? new GitTreeStatus(changed, HeadOf(tree)?.Id, tree.Branch) : null;

    public Task<GitTreePlace> PlaceAsync(string folder, CancellationToken ct) =>
        Task.FromResult(Find(folder) is { } tree
            ? new GitTreePlace(0, Path.Combine(Root, ".git").Replace('\\', '/'), "", tree.Branch ?? "HEAD", true, tree.Path.Replace('\\', '/'))
            : new GitTreePlace(128, "", "fatal: not a git repository (or any of the parent directories): .git", "", false, ""));

    public async Task<GitDone> RemoveTreeAsync(string folder, string path, bool force, CancellationToken ct)
    {
        if (Linked(folder, path, out var why) is not { } tree) return GitDone.Not(why);
        if (!force && tree.Locked) return GitDone.Not($"cannot remove a locked working tree, lock reason: {tree.Reason}");
        if (!force && (await StatusAsync(path, ignored: false, ct).ConfigureAwait(false)).Count > 0) return GitDone.Not($"'{path}' contains modified or untracked files, use --force to delete it");
        Drop(tree);
        return GitDone.Done;
    }

    /// <summary>Called as an unlock of a working tree begins, with its path and the reason it is locked under: where a test cuts an open off.</summary>
    public Action<string, string?>? Unlocking { get; set; }

    /// <summary>What an unlock answers instead of unlocking, for a path; null lets it unlock.</summary>
    public Func<string, GitDone?>? UnlockFault { get; set; }

    /// <summary>Whether adding a working tree makes its folder on the disk and removing one deletes it, as git does; off, no folder is touched.</summary>
    public bool Materializes { get; set; }

    /// <summary>A linked tree's folder as git leaves it: the folder, and in it the <c>.git</c> file that names where its git directory stands.</summary>
    private void Materialize(MemoryTree tree)
    {
        Directory.CreateDirectory(tree.Path);
        File.WriteAllText(Path.Combine(tree.Path, ".git"), $"gitdir: {Path.Combine(Root, ".git", "worktrees", Path.GetFileName(tree.Path)).Replace('\\', '/')}\n");
    }

    private void Drop(MemoryTree tree)
    {
        _trees.Remove(tree);
        if (Materializes && Directory.Exists(tree.Path)) Directory.Delete(tree.Path, recursive: true);
    }

    public Task<IReadOnlyList<string>?> UnlandedAsync(string folder, string revision, string? except, CancellationToken ct)
    {
        if (Find(folder) is not { } tree || _history.Resolve(revision, HeadOf(tree)?.Id) is not { } top) return Task.FromResult<IReadOnlyList<string>?>(null);
        var unheld = _history.Reach(top);
        foreach (var (name, commit) in _history.Refs)
        {
            var held = name.StartsWith("refs/heads/", StringComparison.Ordinal) ? !name.StartsWith("refs/heads/daoris/", StringComparison.Ordinal) && name != $"refs/heads/{except}"
                : name.StartsWith("refs/remotes/", StringComparison.Ordinal) && !name["refs/remotes/".Length..].Contains("/daoris/", StringComparison.Ordinal);
            if (held && _history.Commit(commit) is { } holder) unheld.ExceptWith(_history.Reach(holder));
        }

        return Task.FromResult<IReadOnlyList<string>?>([.. unheld.Select(id => _history.Commit(id)!).OrderByDescending(each => each.Order).Select(each => $"{each.Id[..7]} {each.Message}")]);
    }

    public async Task<GitFreshTop> FreshTopAsync(string folder, CancellationToken ct) =>
        await TopOfAsync(folder, ct).ConfigureAwait(false) is { } top ? new GitFreshTop(true, top.Replace('\\', '/'), "") : new GitFreshTop(false, "", "fatal: not a git repository");

    public Task<IReadOnlyList<GitRefFact>?> RefFactsAsync(string folder, CancellationToken ct)
    {
        if (Find(folder) is null) return Task.FromResult<IReadOnlyList<GitRefFact>?>(null);
        var facts = _history.Refs.Where(each => each.Key.StartsWith("refs/heads/", StringComparison.Ordinal) || each.Key.StartsWith("refs/remotes/origin/", StringComparison.Ordinal))
            .OrderBy(each => each.Key, StringComparer.Ordinal)
            .Select(each => new GitRefFact(each.Key, each.Value, "commit", "", "", TreeIdOf(_history.Commit(each.Value)!))).ToList();
        return Task.FromResult<IReadOnlyList<GitRefFact>?>(facts);
    }

    public Task<IReadOnlyList<GitRef>?> HoldingRefsAsync(string folder, string commit, CancellationToken ct) => RefsAsync(folder, ["refs"], commit, null, ct);

    /// <summary>A stable id for what a commit's tree holds, so two commits of one content name one tree as git's do.</summary>
    private static string TreeIdOf(MemoryCommit commit)
    {
        var text = string.Join('\n', commit.Tree.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Value.Mode} {each.Key} {each.Value.Content}"));
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }

    public Task<GitLayout?> LayoutAsync(string folder, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult<GitLayout?>(null);
        var common = Path.Combine(Root, ".git");
        var own = tree == _trees[0] ? common : Path.Combine(common, "worktrees", Path.GetFileName(tree.Path));
        return Task.FromResult<GitLayout?>(new GitLayout(own, common));
    }
}
