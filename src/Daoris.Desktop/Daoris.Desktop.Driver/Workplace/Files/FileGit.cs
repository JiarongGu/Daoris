namespace Daoris.Driver;

/// <summary>
/// git's reads the rule file and its analysis make, each read-only: what a commit tracks and holds, from git's objects; what the
/// working tree ignores at its top; the linked worktrees; and the tracked lines that name a path. None takes git's optional locks,
/// so none writes the index.
/// </summary>
internal static class FileGit
{
    internal static async Task<IReadOnlyList<string>> TrackedAsync(string root, string commit, CancellationToken ct) =>
        await MachinePorts.Repository.EntriesAsync(root, commit, [], true, ct).ConfigureAwait(false) is { } entries
            ? [.. entries.Select(entry => entry.Path)]
            : throw new DriverException($"git ls-tree in {root} failed");

    /// <summary>A tracked file's text at <paramref name="commit"/>, or null where the commit holds none.</summary>
    internal static Task<string?> ShowAsync(string root, string commit, string path, CancellationToken ct) => MachinePorts.Repository.TextAtAsync(root, commit, path, ct);

    /// <summary>The paths that differ between two commits, by name alone; null where git cannot say (a commit it no longer holds).</summary>
    internal static Task<IReadOnlyList<string>?> ChangedAsync(string root, string from, string to, CancellationToken ct) => MachinePorts.Repository.ChangedAsync(root, from, to, ct);

    internal static Task<IReadOnlyList<string>> IgnoredTopsAsync(string root, CancellationToken ct) => MachinePorts.Repository.IgnoredTopsAsync(root, ct);

    internal static Task<bool> IgnoresCaseAsync(string root, CancellationToken ct) => MachinePorts.Repository.IgnoresCaseAsync(root, ct);

    /// <summary>Each linked worktree git lists, by its full path.</summary>
    internal static async Task<IReadOnlyList<string>> WorktreesAsync(string root, CancellationToken ct) =>
        await MachinePorts.Repository.WorktreesAsync(root, ct).ConfigureAwait(false) is { } trees
            ? [.. trees.Select(tree => tree.Path)]
            : throw new DriverException($"git worktree in {root} failed");

    /// <summary>
    /// Each tracked line at <paramref name="commit"/> that holds one of <paramref name="names"/>, in one search over the files
    /// <paramref name="pathspecs"/> name, as (file, line).
    /// </summary>
    internal static async Task<IReadOnlyList<(string File, string Line)>> LinesAsync(
        string root, string commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs, CancellationToken ct) =>
        [.. (await MachinePorts.Repository.GrepAsync(root, commit, names, pathspecs, ct).ConfigureAwait(false)).Select(each => (each.Path, each.Line))];

    /// <summary>The commit <paramref name="revision"/> names.</summary>
    internal static async Task<string> CommitAsync(string root, string revision, CancellationToken ct) =>
        await MachinePorts.Repository.CommitOfAsync(root, revision, ct).ConfigureAwait(false) ?? throw new DriverException($"git rev-parse in {root} failed: `{revision}` names no commit");
}
