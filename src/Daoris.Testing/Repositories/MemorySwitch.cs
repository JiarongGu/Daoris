using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>
/// One switch of a memory repository's checkout, made as git makes it and never forced: refused where a tracked file the target changes
/// holds work, or an untracked or ignored (when kept) file stands where the target puts one; else the files become the target's.
/// </summary>
internal sealed class MemorySwitch(MemoryHistory history, IReadOnlyList<MemoryTree> trees)
{
    internal GitDone Make(MemoryTree tree, GitSwitch to)
    {
        var now = tree.Branch is { } branch ? history.Commit(history.Refs.GetValueOrDefault($"refs/heads/{branch}")) : history.Commit(tree.Detached);
        MemoryCommit? target;
        if (to.Branch is null)
        {
            target = to.Start is null ? now : history.Resolve(to.Start, now?.Id);
            if (target is null) return GitDone.Not($"invalid reference: {to.Start ?? "HEAD"}");
        }
        else if (to.Creates)
        {
            if (history.Refs.ContainsKey($"refs/heads/{to.Branch}")) return GitDone.Not($"a branch named '{to.Branch}' already exists");
            target = to.Start is null ? now : history.Resolve(to.Start, now?.Id);
            if (target is null) return GitDone.Not($"invalid reference: {to.Start ?? "HEAD"}");
        }
        else
        {
            target = history.Commit(history.Refs.GetValueOrDefault($"refs/heads/{to.Branch}"));
            if (target is null) return GitDone.Not($"invalid reference: {to.Branch}");
            if (trees.FirstOrDefault(each => each != tree && each.Branch == to.Branch) is { } other) return GitDone.Not($"'{to.Branch}' is already used by worktree at '{other.Path}'");
        }

        var held = now?.Tree ?? new Dictionary<string, MemoryBlob>();
        if (Blocked(tree, held, target.Tree, to.KeepIgnored) is { } path)
        {
            return GitDone.Not($"Your local changes to the following files would be overwritten by checkout: {path}");
        }

        Place(tree, held, target);

        if (to.Branch is { } name)
        {
            if (to.Creates) history.Refs[$"refs/heads/{name}"] = target.Id;
            (tree.Branch, tree.Detached) = (name, null);
        }
        else (tree.Branch, tree.Detached) = (null, target.Id);

        return GitDone.Done;
    }

    /// <summary>Makes the files of <paramref name="tree"/> the target's: what the old commit held and the target does not goes, the rest is written.</summary>
    internal static void Place(MemoryTree tree, IReadOnlyDictionary<string, MemoryBlob> held, MemoryCommit target)
    {
        foreach (var gone in held.Keys.Where(each => !target.Tree.ContainsKey(each))) { tree.Files.Remove(gone); tree.Submodules.Remove(gone); }
        foreach (var (file, blob) in target.Tree)
        {
            if (blob.Mode == MemoryBlob.Submodule) tree.Submodules.Add(file);
            else tree.Files[file] = blob.Content;
        }
    }

    internal static string? Blocked(MemoryTree tree, IReadOnlyDictionary<string, MemoryBlob> held, IReadOnlyDictionary<string, MemoryBlob> target, bool keepIgnored)
    {
        foreach (var path in held.Keys.Concat(target.Keys).Distinct())
        {
            held.TryGetValue(path, out var was);
            target.TryGetValue(path, out var will);
            if (was?.Content == will?.Content && was?.Mode == will?.Mode) continue;
            tree.Files.TryGetValue(path, out var there);
            if (was is not null)
            {
                if (was.Mode == MemoryBlob.File && there != was.Content) return path;
            }
            else if (there is not null && (keepIgnored || MemoryIgnores.Top(tree.Files, path) is null)) return path;
        }

        return null;
    }
}
