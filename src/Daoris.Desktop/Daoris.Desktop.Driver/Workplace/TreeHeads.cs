namespace Daoris.Driver;

/// <summary>
/// The branch each of a home's trees has checked out, read with one git start per repository: <c>worktree list --porcelain</c> names
/// every linked tree of a repository with its branch, where asking each tree on its own costs a start per tree, a third of a second
/// each on a loaded machine, on every look. A tree git does not list, or whose repository cannot be told from its files, is asked on
/// its own.
/// </summary>
internal static class TreeHeads
{
    /// <summary>
    /// What <c>rev-parse --abbrev-ref HEAD</c> answers in each of <paramref name="trees"/>: its exit code and its branch, <c>HEAD</c> for a
    /// detached one.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<string, (int Code, string Branch)>> ReadAsync(IReadOnlyList<string> trees, CancellationToken ct)
    {
        var answers = new Dictionary<string, (int, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var repository in trees.GroupBy(CommonDir, StringComparer.OrdinalIgnoreCase))
        {
            var listed = repository.Key.Length == 0 ? null : await TreeWorktrees.EntriesAsync(repository.First(), ct).ConfigureAwait(false);
            foreach (var tree in repository)
            {
                var entry = listed?.FirstOrDefault(each => Same(each.Path, tree));
                answers[tree] = entry is not null
                    ? (0, entry.Branch ?? "HEAD")
                    : await AskedAsync(tree, ct).ConfigureAwait(false);
            }
        }

        return answers;
    }

    private static async Task<(int Code, string Branch)> AskedAsync(string tree, CancellationToken ct)
    {
        try
        {
            return (0, await MachinePorts.Repository.BranchAsync(tree, ct).ConfigureAwait(false) ?? "HEAD");
        }
        catch (Exception error) when (error is DriverException or InvalidOperationException)
        {
            return (128, "");
        }
    }

    /// <summary>The repository a linked tree belongs to, from its <c>.git</c> link and its git dir's <c>commondir</c>; empty where the files do not say.</summary>
    private static string CommonDir(string tree)
    {
        try
        {
            var link = Path.Combine(tree, ".git");
            if (!File.Exists(link)) return "";
            var text = File.ReadAllText(link).Trim();
            if (!text.StartsWith("gitdir:", StringComparison.Ordinal)) return "";
            var gitDir = Path.GetFullPath(Path.Combine(tree, text["gitdir:".Length..].Trim()));
            var common = Path.Combine(gitDir, "commondir");
            return File.Exists(common) ? Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(common).Trim())) : "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "";
        }
    }

    private static bool Same(string listed, string tree) =>
        string.Equals(Path.TrimEndingDirectorySeparator(listed), Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree)), StringComparison.OrdinalIgnoreCase);
}
