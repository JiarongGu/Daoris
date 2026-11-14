using Daoris.Ports.Git;
using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>
/// A repository kept in memory behind <see cref="IGitRepository"/>: commits, refs, and checkouts whose files a test writes with
/// <see cref="Write"/>. It answers as git does for what the contract holds it to (<see cref="RepositoryContract"/>), so a family's
/// unit test runs on it without a process. Every act changes only memory; no folder is read or written.
/// </summary>
public sealed class MemoryRepository : IGitRepository
{
    private readonly MemoryHistory _history = new();
    private readonly List<MemoryTree> _trees = [];

    /// <summary>A repository whose first checkout is <paramref name="root"/>, on an unborn <paramref name="branch"/>.</summary>
    public MemoryRepository(string root, string branch = "main") => _trees.Add(new MemoryTree(Full(root), branch, null));

    public string Root => _trees[0].Path;

    // ——— What a test sets up.

    /// <summary>Writes a file in the checkout at <paramref name="folder"/>.</summary>
    public void Write(string folder, string path, string content) => Tree(folder).Files[path] = content;

    /// <summary>Deletes a file from the checkout.</summary>
    public void Remove(string folder, string path) => Tree(folder).Files.Remove(path);

    /// <summary>Marks a path assume-unchanged, as <c>update-index --assume-unchanged</c> does.</summary>
    public void Hide(string folder, string path) => Tree(folder).Hidden.Add(path);

    /// <summary>Makes a path a submodule in the checkout, which the next commit tracks.</summary>
    public void Submodule(string folder, string path) => Tree(folder).Submodules.Add(path);

    /// <summary>Makes the folder of a checkout gone, which a prune then forgets.</summary>
    public void Vanish(string folder) => Tree(folder).Gone = true;

    /// <summary>The file at <paramref name="path"/> in the checkout, or null where it has none.</summary>
    public string? Read(string folder, string path) => Tree(folder).Files.GetValueOrDefault(path);

    /// <summary>The ref's commit, or null where it names none.</summary>
    public string? Ref(string name) => _history.Refs.GetValueOrDefault(name);

    // ——— The port.

    public Task<string?> HeadAsync(string folder, CancellationToken ct) => Task.FromResult(Find(folder) is { } tree ? HeadOf(tree)?.Id : null);

    public Task<string?> BranchAsync(string folder, CancellationToken ct) =>
        Find(folder) is { } tree ? Task.FromResult(tree.Branch) : Task.FromException<string?>(new InvalidOperationException($"{folder} is not a git repository"));

    public Task<string?> CommitOfAsync(string folder, string revision, CancellationToken ct) =>
        Task.FromResult(Find(folder) is { } tree && revision.Length > 0 && revision[0] != '-' ? _history.Resolve(revision, HeadOf(tree)?.Id)?.Id : null);

    public Task<string?> MergeBaseAsync(string folder, string first, string second, CancellationToken ct) =>
        Task.FromResult(Pair(folder, first, second) is var (one, two) ? _history.Base(one, two)?.Id : null);

    public Task<bool?> IsAncestorAsync(string folder, string ancestor, string descendant, CancellationToken ct) =>
        Task.FromResult(Pair(folder, ancestor, descendant) is var (one, two) ? (bool?)_history.Reach(two).Contains(one.Id) : null);

    public Task<int?> CountAsync(string folder, string tip, IReadOnlyList<string> exclude, CancellationToken ct)
    {
        if (Find(folder) is not { } tree || _history.Resolve(tip, HeadOf(tree)?.Id) is not { } top) return Task.FromResult<int?>(null);
        var reach = _history.Reach(top);
        foreach (var each in exclude)
        {
            if (_history.Resolve(each, HeadOf(tree)?.Id) is not { } excluded) return Task.FromResult<int?>(null);
            reach.ExceptWith(_history.Reach(excluded));
        }

        return Task.FromResult<int?>(reach.Count);
    }

    public Task<IReadOnlyList<GitRef>?> RefsAsync(string folder, IReadOnlyList<string> prefixes, string? contains, int? limit, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult<IReadOnlyList<GitRef>?>(null);
        var held = contains is null ? null : _history.Resolve(contains, HeadOf(tree)?.Id);
        if (contains is not null && held is null) return Task.FromResult<IReadOnlyList<GitRef>?>(null);
        var found = _history.Refs.Where(each => prefixes.Any(prefix => each.Key == prefix.TrimEnd('/') || each.Key.StartsWith(prefix.TrimEnd('/') + "/", StringComparison.Ordinal)))
            .Where(each => held is null || _history.Reach(_history.Commit(each.Value)!).Contains(held.Id))
            .OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => new GitRef(each.Key, each.Value)).Take(limit ?? int.MaxValue).ToList();
        return Task.FromResult<IReadOnlyList<GitRef>?>(found);
    }

    public Task<IReadOnlyList<GitTreeEntry>?> EntriesAsync(string folder, string commit, IReadOnlyList<string> within, bool recursive, CancellationToken ct)
    {
        if (Find(folder) is not { } tree || _history.Resolve(commit, HeadOf(tree)?.Id) is not { } found) return Task.FromResult<IReadOnlyList<GitTreeEntry>?>(null);
        var under = within.Count == 0 ? [""] : within;
        var entries = new SortedDictionary<string, GitTreeEntry>(StringComparer.Ordinal);
        foreach (var top in under)
        {
            var folderOf = top.EndsWith('/') || top.Length == 0 ? top : null;
            foreach (var (path, blob) in found.Tree)
            {
                if (folderOf is null && path != top && !path.StartsWith(top + "/", StringComparison.Ordinal)) continue;
                if (folderOf is not null && !path.StartsWith(folderOf, StringComparison.Ordinal)) continue;
                if (recursive || folderOf is null) entries[path] = new GitTreeEntry(blob.Mode, path);
                else
                {
                    var rest = path[folderOf.Length..];
                    var cut = rest.IndexOf('/', StringComparison.Ordinal);
                    if (cut < 0) entries[path] = new GitTreeEntry(blob.Mode, path);
                    else entries[folderOf + rest[..cut]] = new GitTreeEntry("040000", folderOf + rest[..cut]);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<GitTreeEntry>?>([.. entries.Values]);
    }

    public Task<string?> TextAtAsync(string folder, string commit, string path, CancellationToken ct) =>
        Task.FromResult(Find(folder) is { } tree && _history.Resolve(commit, HeadOf(tree)?.Id) is { } found && found.Tree.TryGetValue(path, out var blob) && blob.Mode == MemoryBlob.File ? blob.Content : null);

    /// <summary>Whether the repository matches its ignore patterns without regard to case, as git on a case-insensitive disk does.</summary>
    public bool CaseIgnored { get; set; }

    public Task<bool> IgnoresCaseAsync(string folder, CancellationToken ct) => Task.FromResult(CaseIgnored);

    public Task<IReadOnlyList<string>> IgnoredTopsAsync(string folder, CancellationToken ct) => IgnoredAsync(folder, [], ct);

    public async Task<IReadOnlyList<string>> IgnoredAsync(string folder, IReadOnlyList<string> within, CancellationToken ct) =>
        [.. (await StatusAsync(folder, ignored: true, ct).ConfigureAwait(false)).Where(each => each.Value == "!!" && Under(each.Key, within)).Select(each => each.Key).Order(StringComparer.Ordinal)];

    private static bool Under(string top, IReadOnlyList<string> within) =>
        within.Count == 0 || within.Any(path => top.TrimEnd('/') == path.TrimEnd('/') || top.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal));

    public Task<IReadOnlyList<GitWorktree>?> WorktreesAsync(string folder, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GitWorktree>?>(Find(folder) is null ? null : [.. _trees.Select(each => new GitWorktree(each.Path, each.Branch, each.Locked, each.Reason))]);

    public Task<IReadOnlyDictionary<string, string>> StatusAsync(string folder, bool ignored, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromException<IReadOnlyDictionary<string, string>>(new InvalidOperationException($"{folder} is not a git repository"));
        var head = HeadOf(tree)?.Tree ?? new Dictionary<string, MemoryBlob>();
        var status = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, content) in tree.Files)
        {
            if (head.TryGetValue(path, out var held)) { if (held.Content != content) status[path] = " M"; continue; }
            if (MemoryIgnores.Top(tree.Files, path) is { } top) { if (ignored) status[top] = "!!"; }
            else status[path] = "??";
        }

        foreach (var path in head.Keys.Where(path => !tree.Files.ContainsKey(path) && !tree.Submodules.Contains(path))) status[path] = " D";
        return Task.FromResult<IReadOnlyDictionary<string, string>>(status);
    }

    public Task<IReadOnlyList<string>?> HiddenAsync(string folder, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>?>(Find(folder) is { } tree ? [.. tree.Hidden.Order(StringComparer.Ordinal)] : null);

    public Task<GitDone> UpdateRefAsync(string folder, string name, string to, string? from, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        if (_history.Resolve(to, HeadOf(tree)?.Id) is not { } target) return Task.FromResult(GitDone.Not($"{to} is not a valid object"));
        if (from is not null && _history.Resolve(from, HeadOf(tree)?.Id)?.Id != _history.Refs.GetValueOrDefault(name)) return Task.FromResult(GitDone.Not($"cannot lock ref '{name}': is at a different commit than expected"));
        _history.Refs[name] = target.Id;
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> SwitchAsync(string folder, GitSwitch to, CancellationToken ct) =>
        Task.FromResult(Find(folder) is { } tree ? new MemorySwitch(_history, _trees).Make(tree, to) : GitDone.Not($"{folder} is not a git repository"));

    public Task<GitDone> CommitAllAsync(string folder, string message, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        var head = HeadOf(tree);
        var kept = head?.Tree ?? new Dictionary<string, MemoryBlob>();
        var next = new Dictionary<string, MemoryBlob>(StringComparer.Ordinal);
        foreach (var (path, content) in tree.Files) if (kept.ContainsKey(path) || MemoryIgnores.Top(tree.Files, path) is null) next[path] = new MemoryBlob(MemoryBlob.File, content);
        foreach (var path in tree.Submodules) next[path] = new MemoryBlob(MemoryBlob.Submodule, "");
        foreach (var path in kept.Where(each => tree.Submodules.Contains(each.Key)).Select(each => each.Key)) next[path] = kept[path];
        if (head is null ? next.Count == 0 : next.Count == kept.Count && next.All(each => kept.TryGetValue(each.Key, out var was) && was == each.Value))
        {
            return Task.FromResult(GitDone.Not("nothing to commit"));
        }

        var made = _history.Add(head is null ? [] : [head.Id], next, message);
        if (tree.Branch is { } branch) _history.Refs[$"refs/heads/{branch}"] = made.Id;
        else tree.Detached = made.Id;
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> LockAsync(string folder, string path, string reason, CancellationToken ct)
    {
        if (Linked(folder, path, out var why) is not { } tree) return Task.FromResult(GitDone.Not(why));
        if (tree.Locked) return Task.FromResult(GitDone.Not($"'{path}' is already locked" + (tree.Reason is { } said ? $", reason: {said}" : "")));
        (tree.Locked, tree.Reason) = (true, reason);
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> UnlockAsync(string folder, string path, CancellationToken ct)
    {
        if (Linked(folder, path, out var why) is not { } tree) return Task.FromResult(GitDone.Not(why));
        Unlocking?.Invoke(path, tree.Reason);
        ct.ThrowIfCancellationRequested();
        if (UnlockFault?.Invoke(path) is { } fault) return Task.FromResult(fault);
        if (!tree.Locked) return Task.FromResult(GitDone.Not($"'{path}' is not locked"));
        (tree.Locked, tree.Reason) = (false, null);
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> PruneAsync(string folder, CancellationToken ct)
    {
        if (Find(folder) is null) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        _trees.RemoveAll(each => each.Gone && !each.Locked && each != _trees[0]);
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> AddWorktreeAsync(string folder, GitWorktreeAdd add, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        if (_history.Resolve(add.Start, HeadOf(tree)?.Id) is not { } at) return Task.FromResult(GitDone.Not($"invalid reference: {add.Start}"));
        if (Find(add.Path) is not null) return Task.FromResult(GitDone.Not($"'{add.Path}' already exists"));
        if (add.Branch is { } branch && _history.Refs.ContainsKey($"refs/heads/{branch}")) return Task.FromResult(GitDone.Not($"a branch named '{branch}' already exists"));
        var added = new MemoryTree(Full(add.Path), add.Branch, add.Branch is null ? at.Id : null) { Locked = add.LockReason is not null, Reason = add.LockReason };
        foreach (var (file, blob) in at.Tree) if (blob.Mode == MemoryBlob.File) added.Files[file] = blob.Content; else added.Submodules.Add(file);
        if (add.Branch is { } made) _history.Refs[$"refs/heads/{made}"] = at.Id;
        _trees.Add(added);
        if (Materializes) Materialize(added);
        return Task.FromResult(GitDone.Done);
    }

    public Task<string?> TopOfAsync(string folder, CancellationToken ct)
    {
        var full = Full(folder);
        return Task.FromResult(_trees.Where(each => string.Equals(each.Path, full, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(each.Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).OrderByDescending(each => each.Path.Length).FirstOrDefault()?.Path);
    }

    public Task<GitDone> FastForwardAsync(string folder, string commit, CancellationToken ct) =>
        Task.FromResult(Find(folder) is { } tree && _history.Resolve(commit, HeadOf(tree)?.Id) is { } target
            ? new MemoryAdvance(_history).FastForward(tree, HeadOf(tree), target)
            : GitDone.Not($"{commit} is not something we can merge"));

    public Task<GitDone> ResetHardAsync(string folder, string commit, CancellationToken ct) =>
        Task.FromResult(Find(folder) is { } tree && _history.Resolve(commit, HeadOf(tree)?.Id) is { } target
            ? new MemoryAdvance(_history).ResetHard(tree, HeadOf(tree), target)
            : GitDone.Not($"ambiguous argument '{commit}': unknown revision"));

    public Task<string?> DefaultBranchAsync(string folder, CancellationToken ct) =>
        Task.FromResult(Find(folder) is null ? null : new[] { "main", "master" }.FirstOrDefault(name => _history.Refs.ContainsKey($"refs/heads/{name}")));

    public Task<GitDone> DeleteBranchAsync(string folder, string branch, CancellationToken ct)
    {
        if (Find(folder) is null) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        if (!_history.Refs.ContainsKey($"refs/heads/{branch}")) return Task.FromResult(GitDone.Not($"branch '{branch}' not found"));
        if (_trees.FirstOrDefault(each => each.Branch == branch) is { } holder) return Task.FromResult(GitDone.Not($"cannot delete branch '{branch}' used by worktree at '{holder.Path}'"));
        _history.Refs.Remove($"refs/heads/{branch}");
        return Task.FromResult(GitDone.Done);
    }

    public Task<IReadOnlyList<string>?> ChangedAsync(string folder, string from, string to, CancellationToken ct)
    {
        if (Pair(folder, from, to) is not var (one, two)) return Task.FromResult<IReadOnlyList<string>?>(null);
        return Task.FromResult<IReadOnlyList<string>?>([.. one.Tree.Keys.Union(two.Tree.Keys)
            .Where(path => one.Tree.GetValueOrDefault(path) != two.Tree.GetValueOrDefault(path)).Order(StringComparer.Ordinal)]);
    }

    public Task<IReadOnlyList<GitMatch>> GrepAsync(string folder, string commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs, CancellationToken ct) =>
        Find(folder) is { } tree && _history.Resolve(commit, HeadOf(tree)?.Id) is { } found
            ? Task.FromResult(names.Count == 0 ? (IReadOnlyList<GitMatch>)[] : MemoryGrep.Lines(found, names, pathspecs))
            : Task.FromException<IReadOnlyList<GitMatch>>(new InvalidOperationException($"{commit} names no commit in {folder}"));

    public Task<GitRefRun> TransactAsync(string folder, GitRefTransaction transaction, CancellationToken ct) =>
        Task.FromResult(Find(folder) is null ? new GitRefRun(false, $"{folder} is not a git repository") : MemoryRefTransaction.Run(_history, transaction, CaseIgnored));

    public Task<GitDone> RestoreAsync(string folder, IReadOnlyList<string> paths, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        var held = HeadOf(tree)?.Tree ?? new Dictionary<string, MemoryBlob>();
        var each = paths.Select(path => (path, found: held.Where(blob => blob.Key == path || blob.Key.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal)).ToList())).ToList();
        if (each.FirstOrDefault(one => one.found.Count == 0) is { path: { } missing }) return Task.FromResult(GitDone.Not($"pathspec '{missing}' did not match any file(s) known to git"));
        foreach (var blob in each.SelectMany(one => one.found)) if (blob.Value.Mode == MemoryBlob.File) tree.Files[blob.Key] = blob.Value.Content;
        return Task.FromResult(GitDone.Done);
    }

    /// <summary>The hooks run, in order, as (folder, hook, arguments): the fake runs none and records each ask.</summary>
    public List<(string Folder, string Hook, IReadOnlyList<string> Arguments)> HooksRun { get; } = [];

    public Task<GitDone> RunHookAsync(string folder, string hook, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        if (Find(folder) is null) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        HooksRun.Add((folder, hook, arguments));
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> RemoveWorktreeAsync(string folder, string path, CancellationToken ct)
    {
        if (Linked(folder, path, out var why) is not { } tree) return Task.FromResult(GitDone.Not(why));
        Drop(tree);
        return Task.FromResult(GitDone.Done);
    }

    public IGitRepository Held(GitRun run) => this;

    // ——— The model.

    private static string Full(string folder) => Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private MemoryTree? Find(string folder) => _trees.FirstOrDefault(each => string.Equals(each.Path, Full(folder), StringComparison.OrdinalIgnoreCase));

    private MemoryTree Tree(string folder) => Find(folder) ?? throw new InvalidOperationException($"{folder} is no checkout of this repository");

    private MemoryCommit? HeadOf(MemoryTree tree) => tree.Branch is { } branch ? _history.Commit(_history.Refs.GetValueOrDefault($"refs/heads/{branch}")) : _history.Commit(tree.Detached);

    private (MemoryCommit, MemoryCommit)? Pair(string folder, string first, string second) =>
        Find(folder) is { } tree && _history.Resolve(first, HeadOf(tree)?.Id) is { } one && _history.Resolve(second, HeadOf(tree)?.Id) is { } two ? (one, two) : null;

    private MemoryTree? Linked(string folder, string path, out string why)
    {
        why = $"{folder} is not a git repository";
        if (Find(folder) is null) return null;
        var found = Find(path);
        why = $"'{path}' is not a working tree";
        if (found is null) return null;
        why = "the main working tree cannot be locked, unlocked or removed";
        return found == _trees[0] ? null : found;
    }

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

    public Task<IReadOnlyList<string>?> RevListAsync(string folder, string tip, IReadOnlyList<string> exclude, CancellationToken ct)
    {
        if (Find(folder) is not { } tree || _history.Resolve(tip, HeadOf(tree)?.Id) is not { } top) return Task.FromResult<IReadOnlyList<string>?>(null);
        var reach = _history.Reach(top);
        foreach (var each in exclude)
        {
            if (_history.Resolve(each, HeadOf(tree)?.Id) is not { } excluded) return Task.FromResult<IReadOnlyList<string>?>(null);
            reach.ExceptWith(_history.Reach(excluded));
        }

        return Task.FromResult<IReadOnlyList<string>?>([.. reach.Select(id => _history.Commit(id)!).OrderByDescending(each => each.Order).Select(each => each.Id)]);
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

    /// <summary>The empty commits made, as (folder, name, email, message), in order.</summary>
    public List<(string Folder, string Name, string Email, string Message)> Empties { get; } = [];

    /// <summary>Words that refuse the next empty commit, as a hook's refusal does; null for none.</summary>
    public string? RefuseCommit { get; set; }

    /// <summary>Whether <paramref name="folder"/> is a checkout of this repository or lies within one.</summary>
    public bool Holds(string folder)
    {
        var full = Full(folder);
        return _trees.Any(each => string.Equals(each.Path, full, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(each.Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<GitStatusEntry>> StatusEntriesAsync(string folder, CancellationToken ct) =>
        [.. (await StatusAsync(folder, ignored: false, ct).ConfigureAwait(false)).Select(each => new GitStatusEntry(each.Key, each.Value, null))];

    public Task<bool> AvailableAsync(CancellationToken ct) => Task.FromResult(true);

    public Task<GitDone> InitAsync(string folder, string line, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a folder of this repository"));
        if (HeadOf(tree) is null) tree.Branch = line;
        return Task.FromResult(GitDone.Done);
    }

    public Task<GitDone> CommitEmptyAsync(string folder, string name, string email, string message, CancellationToken ct)
    {
        if (Find(folder) is not { } tree) return Task.FromResult(GitDone.Not($"{folder} is not a git repository"));
        if (RefuseCommit is { } refused) return Task.FromResult(GitDone.Not(refused));
        var head = HeadOf(tree);
        var made = _history.Add(head is null ? [] : [head.Id], head?.Tree ?? new Dictionary<string, MemoryBlob>(), message);
        if (tree.Branch is { } branch) _history.Refs[$"refs/heads/{branch}"] = made.Id;
        else tree.Detached = made.Id;
        Empties.Add((folder, name, email, message));
        return Task.FromResult(GitDone.Done);
    }
}
