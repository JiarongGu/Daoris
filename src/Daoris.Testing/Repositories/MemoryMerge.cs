using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>A merge begun in a checkout and not yet committed: the commit merged in, the tree it makes, and the paths still conflicted.</summary>
internal sealed record MemoryMerging(MemoryCommit Tip, IReadOnlyDictionary<string, MemoryBlob> Result, HashSet<string> Conflicts);

/// <summary>
/// A three-way merge made as whole files: a path the two sides changed to different contents is a conflict, its working file holding both
/// sides between markers; every other path takes the side that changed it. A merge never begins over work its files would overwrite.
/// </summary>
internal sealed class MemoryMerge(MemoryHistory history)
{
    internal static string Markers(string ours, string theirs) => $"<<<<<<< HEAD\n{ours}=======\n{theirs}>>>>>>> merged\n";

    /// <summary>The merge's files, or the first path of work that stands in its way; conflicts left as <paramref name="conflicts"/>.</summary>
    internal (Dictionary<string, MemoryBlob> Result, HashSet<string> Conflicts) Combine(MemoryCommit head, MemoryCommit tip)
    {
        var from = history.Base(head, tip)?.Tree ?? new Dictionary<string, MemoryBlob>();
        var result = new Dictionary<string, MemoryBlob>(head.Tree, StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in head.Tree.Keys.Concat(tip.Tree.Keys).Concat(from.Keys).Distinct(StringComparer.Ordinal))
        {
            head.Tree.TryGetValue(path, out var ours);
            tip.Tree.TryGetValue(path, out var theirs);
            from.TryGetValue(path, out var origin);
            if (Same(ours, theirs) || Same(theirs, origin)) continue;
            if (Same(ours, origin))
            {
                if (theirs is null) result.Remove(path);
                else result[path] = theirs;
            }
            else conflicts.Add(path);
        }

        return (result, conflicts);
    }

    internal GitMerged Begin(MemoryTree tree, MemoryCommit head, MemoryCommit tip, GitMergeAsk ask, Dictionary<MemoryTree, MemoryMerging> merging, Action<MemoryTree, IReadOnlyList<string>, Dictionary<string, MemoryBlob>, string?> commit)
    {
        var reach = history.Reach(head);
        if (reach.Contains(tip.Id)) return new GitMerged(true, "", [], false);
        if (ask.Kind == GitMergeKind.FastForwardOnly)
        {
            var moved = history.Reach(tip).Contains(head.Id) ? new MemoryAdvance(history).FastForward(tree, head, tip) : GitDone.Not("fatal: Not possible to fast-forward, aborting.");
            return new GitMerged(moved.Ok, moved.Error, [], false);
        }

        var (result, conflicts) = Combine(head, tip);
        if (MemorySwitch.Blocked(tree, head.Tree, result, keepIgnored: false) is { } path)
        {
            return new GitMerged(false, $"error: Your local changes to the following files would be overwritten by merge: {path}", [], false);
        }

        foreach (var gone in head.Tree.Keys.Where(each => !result.ContainsKey(each))) { tree.Files.Remove(gone); tree.Submodules.Remove(gone); }
        foreach (var (file, blob) in result)
        {
            if (blob.Mode == MemoryBlob.Submodule) tree.Submodules.Add(file);
            else tree.Files[file] = blob.Content;
        }

        foreach (var conflict in conflicts)
        {
            head.Tree.TryGetValue(conflict, out var ours);
            tip.Tree.TryGetValue(conflict, out var theirs);
            tree.Files[conflict] = ours is not null && theirs is not null ? Markers(ours.Content, theirs.Content) : (ours ?? theirs)!.Content;
        }

        if (conflicts.Count > 0)
        {
            merging[tree] = new MemoryMerging(tip, result, conflicts);
            return new GitMerged(false, $"CONFLICT (content): Merge conflict in {conflicts.Order(StringComparer.Ordinal).First()}\nAutomatic merge failed; fix conflicts and then commit the result.", [.. conflicts.Order(StringComparer.Ordinal)], true);
        }

        if (ask.Kind == GitMergeKind.Stage)
        {
            merging[tree] = new MemoryMerging(tip, result, conflicts);
            return new GitMerged(true, "", [], true);
        }

        commit(tree, [head.Id, tip.Id], result, ask.Message);
        return new GitMerged(true, "", [], false);
    }

    private static bool Same(MemoryBlob? one, MemoryBlob? other) => one?.Mode == other?.Mode && one?.Content == other?.Content;
}
