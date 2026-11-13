namespace Daoris.Driver;

/// <summary>
/// git's reads the rule file and its analysis make, each read-only: what a commit tracks and holds, from git's objects; what the
/// working tree ignores at its top; the linked worktrees; and the tracked lines that name a path. None takes git's optional locks,
/// so none writes the index.
/// </summary>
internal static class FileGit
{
    /// <summary>Every path git tracks at <paramref name="commit"/>.</summary>
    internal static async Task<IReadOnlyList<string>> TrackedAsync(string root, string commit, CancellationToken ct) =>
        await MachinePorts.Repository.EntriesAsync(root, commit, [], true, ct).ConfigureAwait(false) is { } entries
            ? [.. entries.Select(entry => entry.Path)]
            : throw new DriverException($"git ls-tree in {root} failed");

    /// <summary>A tracked file's text at <paramref name="commit"/>, or null where the commit holds none.</summary>
    internal static Task<string?> ShowAsync(string root, string commit, string path, CancellationToken ct) => MachinePorts.Repository.TextAtAsync(root, commit, path, ct);

    /// <summary>The paths that differ between two commits, by name alone; null where git cannot say (a commit it no longer holds).</summary>
    internal static async Task<IReadOnlyList<string>?> ChangedAsync(string root, string from, string to, CancellationToken ct)
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "diff", "--name-only", "-z", "--no-renames", from, to, "--"], ct).ConfigureAwait(false);
        return code == 0 ? Split(stdout) : null;
    }

    /// <summary>The paths the working tree ignores, at their tops and never descending, a folder ending in <c>/</c>.</summary>
    internal static Task<IReadOnlyList<string>> IgnoredTopsAsync(string root, CancellationToken ct) => MachinePorts.Repository.IgnoredTopsAsync(root, ct);

    /// <summary>The checkout's <c>core.ignorecase</c>: whether git matches its ignore patterns without regard to case.</summary>
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
        string root, string commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs, CancellationToken ct)
    {
        if (names.Count == 0) return [];
        List<string> arguments = ["--no-optional-locks", "grep", "-I", "-F", "-z", "--no-color"];
        foreach (var name in names) arguments.AddRange(["-e", name]);
        arguments.Add(commit);
        arguments.Add("--");
        arguments.AddRange(pathspecs);
        var (code, stdout, stderr) = await WorkingTree.GitAsync(root, arguments, ct).ConfigureAwait(false);
        if (code > 1) throw new DriverException($"git grep in {root} failed: {TreeGit.Failure(stderr)}");
        var prefix = commit + ":";
        return [.. stdout.ReplaceLineEndings("\n").Split('\n').Select(line => line.Split('\0', 2)).Where(parts => parts.Length == 2)
            .Select(parts => (parts[0].StartsWith(prefix, StringComparison.Ordinal) ? parts[0][prefix.Length..] : parts[0], parts[1]))];
    }

    /// <summary>The commit <paramref name="revision"/> names.</summary>
    internal static async Task<string> CommitAsync(string root, string revision, CancellationToken ct) =>
        (await Ask(root, ["rev-parse", "--verify", "--end-of-options", revision + "^{commit}"], ct).ConfigureAwait(false)).Trim();

    private static async Task<string> Ask(string root, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(root, ["--no-optional-locks", .. arguments], ct).ConfigureAwait(false);
        return code == 0 ? stdout : throw new DriverException($"git {arguments[0]} in {root} failed: {TreeGit.Failure(stderr)}");
    }

    private static IReadOnlyList<string> Split(string listed) => listed.Split('\0', StringSplitOptions.RemoveEmptyEntries);
}
