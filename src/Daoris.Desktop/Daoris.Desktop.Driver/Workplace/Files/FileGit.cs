namespace Daoris.Driver;

/// <summary>The reads the rule file and its analysis make of git, each through the repository port and none writing the index.</summary>
internal static class FileGit
{
    internal static async Task<IReadOnlyList<string>> TrackedAsync(string root, string commit, CancellationToken ct) =>
        await MachinePorts.Repository.EntriesAsync(root, commit, [], true, ct).ConfigureAwait(false) is { } entries
            ? [.. entries.Select(entry => entry.Path)]
            : throw new DriverException($"git ls-tree in {root} failed");

    internal static Task<string?> ShowAsync(string root, string commit, string path, CancellationToken ct) => MachinePorts.Repository.TextAtAsync(root, commit, path, ct);

    internal static Task<IReadOnlyList<string>?> ChangedAsync(string root, string from, string to, CancellationToken ct) => MachinePorts.Repository.ChangedAsync(root, from, to, ct);

    internal static Task<IReadOnlyList<string>> IgnoredTopsAsync(string root, CancellationToken ct) => MachinePorts.Repository.IgnoredTopsAsync(root, ct);

    internal static Task<bool> IgnoresCaseAsync(string root, CancellationToken ct) => MachinePorts.Repository.IgnoresCaseAsync(root, ct);

    internal static async Task<IReadOnlyList<string>> WorktreesAsync(string root, CancellationToken ct) =>
        await MachinePorts.Repository.WorktreesAsync(root, ct).ConfigureAwait(false) is { } trees
            ? [.. trees.Select(tree => tree.Path)]
            : throw new DriverException($"git worktree in {root} failed");

    /// <summary>Each tracked line at <paramref name="commit"/> that holds one of <paramref name="names"/> in the files <paramref name="pathspecs"/> name, as (file, line).</summary>
    internal static async Task<IReadOnlyList<(string File, string Line)>> LinesAsync(
        string root, string commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs, CancellationToken ct) =>
        [.. (await MachinePorts.Repository.GrepAsync(root, commit, names, pathspecs, ct).ConfigureAwait(false)).Select(each => (each.Path, each.Line))];

    internal static async Task<string> CommitAsync(string root, string revision, CancellationToken ct) =>
        await MachinePorts.Repository.CommitOfAsync(root, revision, ct).ConfigureAwait(false) ?? throw new DriverException($"git rev-parse in {root} failed: `{revision}` names no commit");
}
