using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The working trees git lists, and where a tree stands below the trees home (AUTOTIDY1). A list git could not give is never an
/// empty one: "no tree here" lets a branch go, and a branch checked out somewhere would then go from under it.
/// </summary>
internal sealed class TreeWorktrees(TreeContext context)
{
    /// <summary>What a kept branch says when git could not list the working trees.</summary>
    internal const string ListUnread = "git could not list the repository's working trees";

    /// <summary>
    /// Every working tree git lists for the checkout at <paramref name="root"/>, or null where git could not list them. Where the
    /// judgement holds <paramref name="refs"/>, the list is the one it read for them (PROC1d-iii), so a judgement lists once.
    /// </summary>
    internal static Task<IReadOnlyList<WorktreeEntry>?> EntriesAsync(string root, CancellationToken ct, RefSnapshot? refs = null) =>
        refs?.WorktreesAsync(ct) ?? ListedAsync(root, ct);

    /// <summary>The one start: <c>worktree list --porcelain</c>, as git printed it, or null where git could not list them.</summary>
    internal static async Task<string?> PorcelainAsync(string root, CancellationToken ct)
    {
        var (code, porcelain, _) = await WorkingTree.GitAsync(root, ["worktree", "list", "--porcelain"], ct).ConfigureAwait(false);
        return code == 0 ? porcelain : null;
    }

    private static async Task<IReadOnlyList<WorktreeEntry>?> ListedAsync(string root, CancellationToken ct) =>
        await PorcelainAsync(root, ct).ConfigureAwait(false) is { } porcelain ? Parse(porcelain) : null;

    /// <summary>The working trees <c>worktree list --porcelain</c> printed: each one's path, branch and lock.</summary>
    internal static IReadOnlyList<WorktreeEntry> Parse(string porcelain)
    {
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

    /// <summary>The same list, by this instance, which a test may make fail, whatever the judgement's <paramref name="refs"/> hold.</summary>
    internal Task<IReadOnlyList<WorktreeEntry>?> ListAsync(string root, CancellationToken ct, RefSnapshot? refs = null) =>
        context.ListFails?.Invoke(root) == true ? Task.FromResult<IReadOnlyList<WorktreeEntry>?>(null) : EntriesAsync(root, ct, refs);

    /// <summary>Which tree each branch is checked out in, or null where git could not list them: each caller keeps what it judged.</summary>
    internal async Task<Dictionary<string, string>?> ReadAsync(string root, CancellationToken ct, RefSnapshot? refs = null) =>
        await ListAsync(root, ct, refs).ConfigureAwait(false) is { } entries ? ByBranch(entries) : null;

    /// <summary>Which tree each branch is checked out in; a list git could not give is the door's refusal, never an empty list.</summary>
    internal static async Task<Dictionary<string, string>> WorktreesAsync(string root, CancellationToken ct, RefSnapshot? refs = null) =>
        await EntriesAsync(root, ct, refs).ConfigureAwait(false) is { } entries
            ? ByBranch(entries)
            : throw new DriverException($"git could not list the working trees of the checkout at {root}, so nothing there was judged.");

    internal static Dictionary<string, string> ByBranch(IEnumerable<WorktreeEntry> entries)
    {
        var trees = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Branch is { } branch) trees[branch] = entry.Path;
        }

        return trees;
    }

    /// <summary>
    /// The first link on the way from the trees home down to <paramref name="tree"/>, the tree's own folder included (AUTOTIDY1):
    /// the ownership test reads the path's words, and a link below the trees home makes a tree somewhere else. Null where there
    /// is none; a folder that cannot be read counts as one.
    /// </summary>
    internal string? LinkBelowTreesRoot(string tree)
    {
        var home = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.RootOf(tree) ?? context.TreesRoot));
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
