namespace Daoris.Driver;

/// <summary>One working tree as git lists it (AUTOTIDY1): where it is, the branch it has out, and whether it is locked.</summary>
/// <param name="Branch">The branch it has out, without <c>refs/heads/</c>; null for a detached one.</param>
/// <param name="LockReason">Why it is locked, as the person said it to <c>git worktree lock</c>; null where none was given.</param>
internal sealed record WorktreeEntry(string Path, string? Branch, bool Locked, string? LockReason);

/// <summary>
/// The working trees git lists, and how a tree's path is compared (AUTOTIDY1). A list git could not give is never an empty one:
/// "no tree here" lets a branch go, and a branch checked out somewhere would then go from under it.
/// </summary>
public sealed partial class SessionTrees
{
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>The test seam (AUTOTIDY1): true for a checkout whose working trees git could not list. Production sets none.</summary>
    internal Func<string, bool>? ListFails { get; init; }

    /// <summary>Every working tree git lists for the checkout at <paramref name="root"/>, or null where git could not list them.</summary>
    private static async Task<IReadOnlyList<WorktreeEntry>?> WorktreeEntriesAsync(string root, CancellationToken ct)
    {
        var (code, porcelain, _) = await WorkingTree.GitAsync(root, ["worktree", "list", "--porcelain"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var entries = new List<WorktreeEntry>();
        string? path = null;
        string? branch = null;
        var locked = false;
        string? reason = null;
        foreach (var raw in porcelain.Split('\n').Append(""))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (path is not null) entries.Add(new WorktreeEntry(Path.GetFullPath(path), branch, locked, reason));
                (path, branch, locked, reason) = (null, null, false, null);
            }
            else if (line.StartsWith("worktree ", StringComparison.Ordinal)) path = line["worktree ".Length..];
            else if (line.StartsWith("branch refs/heads/", StringComparison.Ordinal)) branch = line["branch refs/heads/".Length..];
            else if (line == "locked" || line.StartsWith("locked ", StringComparison.Ordinal))
            {
                locked = true;
                reason = line.Length > "locked ".Length ? line["locked ".Length..] : null;
            }
        }

        return entries;
    }

    /// <summary>The same list, by this instance, which a test may make fail.</summary>
    private Task<IReadOnlyList<WorktreeEntry>?> ListWorktreesAsync(string root, CancellationToken ct) =>
        ListFails?.Invoke(root) == true ? Task.FromResult<IReadOnlyList<WorktreeEntry>?>(null) : WorktreeEntriesAsync(root, ct);

    /// <summary>Which tree each branch is checked out in, or null where git could not list them: each caller keeps what it judged.</summary>
    private async Task<Dictionary<string, string>?> ReadWorktreesAsync(string root, CancellationToken ct) =>
        await ListWorktreesAsync(root, ct).ConfigureAwait(false) is { } entries ? ByBranch(entries) : null;

    /// <summary>Which tree each branch is checked out in; a list git could not give is the door's refusal, never an empty list.</summary>
    private static async Task<Dictionary<string, string>> WorktreesAsync(string root, CancellationToken ct) =>
        await WorktreeEntriesAsync(root, ct).ConfigureAwait(false) is { } entries
            ? ByBranch(entries)
            : throw new DriverException($"git could not list the working trees of the checkout at {root}, so nothing there was judged.");

    /// <summary>What a kept branch says when git could not list the working trees.</summary>
    internal const string ListUnread = "git could not list the repository's working trees";

    private static Dictionary<string, string> ByBranch(IEnumerable<WorktreeEntry> entries)
    {
        var trees = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Branch is { } branch) trees[branch] = entry.Path;
        }

        return trees;
    }

    /// <summary>
    /// A path as it is on disk (AUTOTIDY1): full, with each link on the way resolved, so a tree a session names through a link
    /// and the same tree git names where it is compare as one. Where a link cannot be read, the path is kept as written.
    /// </summary>
    private static string Normal(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.GetPathRoot(full) ?? "";
        var at = root;
        foreach (var part in full[root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            at = Path.Combine(at, part);
            try
            {
                var info = new DirectoryInfo(at);
                if (info.Exists && info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                {
                    at = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Kept as written: a link that cannot be read is compared by its name.
            }
        }

        return Path.TrimEndingDirectorySeparator(at);
    }

    /// <summary>
    /// The first link on the way from the trees home down to <paramref name="tree"/>, the tree's own folder included (AUTOTIDY1):
    /// the ownership test reads the path's words, and a link below the trees home makes a tree somewhere else. Null where there
    /// is none; a folder that cannot be read counts as one.
    /// </summary>
    private string? LinkBelowTreesRoot(string tree)
    {
        var home = Path.TrimEndingDirectorySeparator(Path.GetFullPath(TreesRoot));
        var below = Path.GetRelativePath(home, Path.GetFullPath(tree));
        var at = home;
        foreach (var part in below.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            at = Path.Combine(at, part);
            try
            {
                var info = new DirectoryInfo(at);
                if (!info.Exists) return null;
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return at;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return at;
            }
        }

        return null;
    }
}
