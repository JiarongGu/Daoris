using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Whether a checkout is a linked worktree (D51): a twin of the CLI's <c>linkedWorktreeMain</c> (<c>connect.ts</c>), which
/// refuses to register one. Git marks a linked worktree itself: its <c>.git</c> is a FILE naming the main repository's
/// <c>.git/worktrees/&lt;name&gt;</c>. A submodule wears a <c>.git</c> file too, pointing at <c>.git/modules/&lt;name&gt;</c>,
/// and is a repository of its own, so only the worktree marker counts. No spawn.
/// </summary>
public static partial class LinkedWorktree
{
    /// <summary>The main tree <paramref name="root"/> is a linked worktree of, or null for a main tree, a submodule or no repository.</summary>
    public static string? MainOf(string root)
    {
        try
        {
            var marker = Path.Combine(root, ".git");
            if (!File.Exists(marker)) return null;

            var pointed = GitDir().Match(File.ReadAllText(marker)) is { Success: true } match ? match.Groups[1].Value.Trim() : "";
            if (pointed.Length == 0) return null;

            var gitdir = Path.IsPathRooted(pointed) ? pointed : Path.GetFullPath(Path.Combine(root, pointed));
            // <main>/.git/worktrees/<name>, in either separator: git writes forward slashes on every platform.
            var parts = gitdir.Replace('\\', '/').Split('/');
            var at = Array.LastIndexOf(parts, "worktrees");
            if (at < 2 || parts[at - 1] != ".git") return null;
            return string.Join('/', parts[..(at - 1)]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // An unreadable marker is not this reader's to diagnose: git itself refuses next, in better words.
            return null;
        }
    }

    [GeneratedRegex(@"^gitdir:\s*(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex GitDir();
}
