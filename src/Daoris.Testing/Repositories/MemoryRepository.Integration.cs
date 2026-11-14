using Daoris.Ports.Repositories;

namespace Daoris.Testing;

public sealed partial class MemoryRepository
{
    private readonly Dictionary<MemoryTree, MemoryMerging> _merging = [];

    /// <summary>Settles a conflicted path of the merge begun in the checkout as a person does: the file written and added.</summary>
    public void Resolve(string folder, string path, string content)
    {
        var tree = Tree(folder);
        tree.Files[path] = content;
        _merging[tree].Conflicts.Remove(path);
    }

    public Task<GitMerged> MergeAsync(string folder, GitMergeAsk ask, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(new GitMerged(false, $"{folder} is not a git repository", [], false));
        if (_merging.ContainsKey(tree)) return Task.FromResult(new GitMerged(false, "fatal: You have not concluded your merge (MERGE_HEAD exists).", [], true));
        if (HeadOf(tree) is not { } head || _history.Resolve(ask.Commit, head.Id) is not { } tip)
        {
            return Task.FromResult(new GitMerged(false, $"merge: {ask.Commit} - not something we can merge", [], false));
        }

        return Task.FromResult(new MemoryMerge(_history).Begin(tree, head, tip, ask, _merging, Make));
    }

    public Task<GitDone> MergeAbortAsync(string folder, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        if (_merging.Remove(tree, out var merging) && HeadOf(tree) is { } head)
        {
            foreach (var added in merging.Result.Keys.Where(each => !head.Tree.ContainsKey(each))) tree.Files.Remove(added);
            new MemoryAdvance(_history).ResetHard(tree, head, head);
        }

        return Task.FromResult(GitDone.Done);
    }

    public Task<IReadOnlyList<string>> UnmergedAsync(string folder, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(Find(folder) is { } tree && _merging.TryGetValue(tree, out var merging) ? [.. merging.Conflicts.Order(StringComparer.Ordinal)] : []);

    public Task<GitDone> CommitStagedAsync(string folder, string message, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        if (!_merging.TryGetValue(tree, out var merging) || HeadOf(tree) is not { } head) return Task.FromResult(GitDone.Not("nothing to commit"));
        if (merging.Conflicts.Count > 0) return Task.FromResult(GitDone.Not("error: Committing is not possible because you have unmerged files."));
        var next = new Dictionary<string, MemoryBlob>(StringComparer.Ordinal);
        foreach (var (path, content) in tree.Files) if (merging.Result.ContainsKey(path) || MemoryIgnores.Top(tree.Files, path) is null) next[path] = new MemoryBlob(MemoryBlob.File, content);
        foreach (var (path, blob) in merging.Result.Where(each => each.Value.Mode == MemoryBlob.Submodule)) next[path] = blob;
        _merging.Remove(tree);
        Make(tree, [head.Id, merging.Tip.Id], next, message);
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> PutBackAsync(string folder, string commit, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        if (_history.Resolve(commit, HeadOf(tree)?.Id) is not { } target) return Task.FromResult(GitDone.Not($"fatal: invalid reference: {commit}"));
        MergeAbortAsync(folder, ct);
        var now = HeadOf(tree);
        var held = now?.Tree ?? new Dictionary<string, MemoryBlob>();
        foreach (var path in tree.Files.Keys.Where(each => !held.ContainsKey(each) && MemoryIgnores.Top(tree.Files, each) is null).ToList()) tree.Files.Remove(path);
        foreach (var (path, blob) in held) if (blob.Mode == MemoryBlob.File) tree.Files[path] = blob.Content;
        MemorySwitch.Place(tree, held, target);
        (tree.Branch, tree.Detached) = (null, target.Id);
        return Task.FromResult(GitDone.Done);
    }

    private void Make(MemoryTree tree, IReadOnlyList<string> parents, IReadOnlyDictionary<string, MemoryBlob> files, string? message)
    {
        var made = _history.Add(parents, files, message ?? $"Merge {parents[^1]}");
        if (tree.Branch is { } branch) _history.Refs[$"refs/heads/{branch}"] = made.Id;
        else tree.Detached = made.Id;
    }
}
