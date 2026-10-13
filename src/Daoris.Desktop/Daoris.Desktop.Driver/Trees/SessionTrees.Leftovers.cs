namespace Daoris.Driver;

/// <summary>
/// A tree git let go of whose folder stayed on disk (found in the first real post-merge run, 2026-09-30).
/// </summary>
/// <remarks>
/// <para><c>git worktree remove</c> deletes a tree's files, then its registration, and carries on past a folder that
/// will not go. On Windows, something still holding the folder open — a terminal or a process whose working folder it
/// was — leaves it registered nowhere and on disk, empty, and git's answer is a failure though the tree is gone. The
/// removal is then said plainly: the tree is removed, its folder is left, and why; and the clean-up tries the empty
/// folder again once nothing holds it.</para>
///
/// <para>🔴 <b>Only an empty folder is ever deleted here</b>, and only under the trees home. One that still holds
/// anything is named and left: whatever is in it, git did not know it as the tree's.</para>
/// </remarks>
public sealed partial class SessionTrees
{
    /// <summary>Whether git still counts <paramref name="tree"/> as one of this repository's working trees.</summary>
    private static async Task<bool?> RegisteredAsync(string root, string tree, CancellationToken ct)
    {
        var (code, porcelain, _) = await WorkingTree.GitAsync(root, ["worktree", "list", "--porcelain"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        return porcelain.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
            .Any(line => SamePath(line["worktree ".Length..], tree));
    }

    /// <summary>
    /// After git failed to remove a tree: whether git let go of it all the same, and then what became of its folder —
    /// a sentence where it stays, null where it went after all.
    /// </summary>
    private static async Task<(bool LetGo, string? Left)> LetGoAsync(string root, string tree, CancellationToken ct)
    {
        if (await RegisteredAsync(root, tree, ct).ConfigureAwait(false) is not false) return (false, null);
        return (true, EmptyFolderGone(tree) is { } why ? $"Its folder at {tree} is left behind: {why}" : null);
    }

    /// <summary>
    /// Delete a leftover folder if it is empty: null where it is gone, else why it stays — the words the removal and the
    /// clean-up both put in their own sentence.
    /// </summary>
    private static string? EmptyFolderGone(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return null;
            if (Directory.EnumerateFileSystemEntries(folder).Any())
            {
                return "it still holds files git did not know as the tree's, so it is left for you to look at.";
            }

            Directory.Delete(folder, recursive: false);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"it is empty, and something on this machine still holds it open ({error.Message}). The clean-up removes it once "
                + "nothing does.";
        }
    }

    /// <summary>
    /// The clean-up's retry (the first real post-merge run): every empty folder where a tree was, under the trees home,
    /// deleted — and a sentence for each one removed or still held.
    /// </summary>
    private IReadOnlyList<string> EmptyFoldersGone()
    {
        var said = new List<string>();
        if (!Directory.Exists(TreesRoot)) return said;
        try
        {
            foreach (var workspace in Directory.EnumerateDirectories(TreesRoot))
            foreach (var repository in Directory.EnumerateDirectories(workspace))
            foreach (var tree in Directory.EnumerateDirectories(repository))
            {
                bool empty;
                try { empty = !Directory.EnumerateFileSystemEntries(tree).Any(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { continue; }
                if (!empty) continue;
                said.Add(EmptyFolderGone(tree) is { } why
                    ? $"the empty folder a tree left at {tree} stays: {why}"
                    : $"removed the empty folder a tree left at {tree}.");
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            said.Add($"Daoris could not look under {TreesRoot} for folders trees left behind ({error.Message}).");
        }

        return said;
    }
}
