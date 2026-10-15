using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// What the look's own tidy decides of one session branch and does to its tree (AUTOTIDY1; PART10l-ii, out of
/// <c>SessionTrees.Auto.cs</c>): which branches it takes without a press, its last guards right before one goes, the tree's folder
/// moved aside rather than deleted, and the later purge of what was moved. Over the trees' context alone: the clean-up's sweep
/// (<see cref="TreeSweeps"/>) takes it for its by-itself path, and the look's pass (<see cref="AutoTidies"/>) takes the sweep.
/// </summary>
internal sealed class TreeSetAside(TreeContext context)
{
    /// <summary>
    /// Whether the look takes this session branch without a press (AUTOTIDY1, AUTOTIDY2a): every kind the press clears
    /// (<see cref="SweepItem.Removable"/>: empty, landed by ancestry or by content, carried), with every guard read. With no tree
    /// it goes; with a tree, only one of this home's on a branch that moved since it grew (<see cref="SweepItem.Worked"/>). A tree
    /// whose branch never moved is a conversation's place, which words to its session go on in (D137): removed, they could not,
    /// so it stays the press's, whatever its kind. A tree holding only ignored paths goes, since the look moves its folder aside
    /// for fourteen days rather than deleting it; uncommitted and untracked paths keep it, as the judgement's <c>dirty</c>.
    /// A landed branch goes only once its work is on the line (<see cref="SweepItem.OnLine"/>): landed on another branch of the
    /// person's alone, its pull request may still be open and its session asked to change it in its own tree, so the look waits
    /// for the merge D88 began from, and the press may take it before by the person's choice.
    /// </summary>
    internal bool GoesByItself(SweepItem item) =>
        item is { Removable: true, Unread: false }
        && (item.Kind != SweepKind.Landed || item.OnLine)
        && (item.Tree is null || (context.Holds(item.Tree) && item.Worked));

    /// <summary>The folder under the trees home the look's own tidy moves a tree's folder aside into (AUTOTIDY1).</summary>
    internal const string SetAsideFolder = ".tidied";

    /// <summary>How a folder moved aside is named after its tree: the tree's name, then the moment it was moved, in UTC.</summary>
    private const string SetAsideStamp = "yyyyMMdd'T'HHmmssfff'Z'";

    /// <summary>Where the look's own tidy keeps the folders it moved aside: <c>trees/.tidied/&lt;workspace&gt;/&lt;repository&gt;/</c>.</summary>
    internal string SetAsideRoot => Path.Combine(context.TreesRoot, SetAsideFolder);

    /// <summary>
    /// The look's last guards (AUTOTIDY1), asked after the press's judgement and its judgement again, right before anything
    /// moves: each keeps the branch for the press, saying why. Null where none keeps it.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A branch git lists no tree for, whose folder of this home's is here: git dropped a record it could not read.</item>
    /// <item>A tree reached through a link under the trees home, which is somewhere else whatever its path says.</item>
    /// <item>A tree a person locked (<c>git worktree lock</c>).</item>
    /// <item>A tracked path marked <c>assume-unchanged</c> or <c>skip-worktree</c>: an edit to it reads as clean to git's status.</item>
    /// <item>Another repository in the tree, a <c>.git</c> file or folder below its top, which the tree's own status skips: read
    /// from the folders themselves, not through git.</item>
    /// </list>
    /// What cannot be read keeps it too.
    /// </remarks>
    internal async Task<(string Code, string Sentence, string? Cause)?> LastLookAsync(string root, SweepItem item, CancellationToken ct)
    {
        if (item.Tree is not { } tree)
        {
            // TREEFOLLOW1: the folder here is a tree git lists, on the branch its agent made; it is not this branch's, so nothing of
            // it is read as unreadable, and it stays untouched.
            if (item.TreeOn is not null) return null;
            var folder = Path.Combine(context.TreesRoot, item.Workspace, item.Repository, item.Branch[TreeFollows.SessionPrefix.Length..]);
            return Directory.Exists(folder) ? (TidyKept.Unread, $"its folder at {folder} is here, and git lists no tree for it", TidyCause.Folder) : null;
        }

        if (context.Worktrees.LinkBelowTreesRoot(tree) is not null) return (TidyKept.Linked, "its tree is reached through a link under the trees home", null);
        if (await context.Worktrees.ListAsync(root, ct).ConfigureAwait(false) is not { } entries) return (TidyKept.Unread, ListUnread, TidyCause.List);
        if (entries.FirstOrDefault(entry => SamePath(entry.Path, tree)) is { Locked: true } locked)
        {
            return (TidyKept.Refused, "its tree is locked" + (locked.LockReason is { Length: > 0 } reason ? $" ({reason})" : "")
                + "; `git worktree unlock` lets it go", null);
        }

        if (!Directory.Exists(tree)) return null;

        var (code, listed, err) = await WorkingTree.GitAsync(tree, ["ls-files", "-v", "-z"], ct).ConfigureAwait(false);
        if (code != 0) return (TidyKept.Unread, $"git could not read its tree's index: {FirstLine(err)}", TidyCause.Index);
        // `ls-files -v` tags an assume-unchanged entry in lower case, and a skip-worktree one `S`.
        var hidden = listed.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => entry.Length > 2 && (char.IsLower(entry[0]) || entry[0] == 'S'))
            .Select(entry => entry[2..])
            .ToList();
        if (hidden.Count > 0)
        {
            return (TidyKept.Hidden, $"{hidden.Count} tracked path(s) in its tree are marked assume-unchanged or skip-worktree, which "
                + $"hides their changes from git: {string.Join(", ", hidden.Take(3))}" + (hidden.Count > 3 ? $" and {hidden.Count - 3} more" : ""), null);
        }

        try
        {
            return NestedRepository(tree) is { } nested ? (TidyKept.Nested, $"its tree holds another repository at {nested}", null) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (TidyKept.Unread, $"its tree's folders could not be read: {error.Message}", TidyCause.Folders);
        }
    }

    /// <summary>
    /// The first folder below the tree's top that holds a <c>.git</c> file or folder, as a path from the tree's root with forward
    /// slashes; null where none does. Read from the folders, hidden ones included, never following a link; the tree's own
    /// <c>.git</c> at its top is passed over, and never read into. A folder that cannot be read throws.
    /// </summary>
    private static string? NestedRepository(string tree)
    {
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var pending = new Stack<string>();
        pending.Push(top);
        while (pending.TryPop(out var folder))
        {
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options))
            {
                if (string.Equals(entry.Name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(folder, top, StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetRelativePath(top, folder).Replace('\\', '/');
                    }

                    continue;
                }

                if (entry is DirectoryInfo directory && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    pending.Push(directory.FullName);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The look's removal of a tree (AUTOTIDY1): never a delete. Its folder is moved aside, in one rename on the same volume,
    /// into <see cref="SetAsideRoot"/>, then git lets go of its record of that tree alone (<c>git worktree remove</c> of a tree
    /// whose folder is gone deletes nothing but the record). A write that lands after the last look lands in the folder moved
    /// aside, so nothing is lost.
    /// </summary>
    /// <remarks>
    /// On Windows a folder whose files something holds open cannot be renamed: the tree is in use, and stays (<c>busy</c>), for
    /// a later look. Where git will not let go of the record, the folder is put back. Called with a token nothing cancels.
    /// </remarks>
    internal async Task<(bool Gone, string? To, string? Code, string Sentence)> MoveAsideAsync(string root, SweepItem item, CancellationToken ct)
    {
        var tree = Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Tree!));
        string? to = null;
        if (Directory.Exists(tree))
        {
            var name = Path.GetFileName(tree);
            to = Path.Combine(SetAsideRoot, item.Workspace, item.Repository, $"{name}-{DateTimeOffset.UtcNow.ToString(SetAsideStamp, CultureInfo.InvariantCulture)}");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (context.Worktrees.LinkBelowTreesRoot(Path.GetDirectoryName(to)!) is not null)
                {
                    return (false, null, TidyKept.Linked, "the folder it would be moved aside into is reached through a link under the trees home");
                }

                Directory.Move(tree, to);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return (false, null, TidyKept.Busy,
                    $"something on this machine is using its tree, so its folder could not be moved aside ({error.Message.Trim()}); a later look tries again");
            }
        }

        var (code, _, err) = await WorkingTree.GitAsync(root, ["worktree", "remove", tree], ct).ConfigureAwait(false);
        if (code == 0) return (true, to, null, "");

        var why = FirstLine(err);
        if (to is null) return (false, null, TidyKept.Refused, $"git would not let go of its tree: {why}");
        try
        {
            Directory.Move(to, tree);
            return (false, null, TidyKept.Refused, $"git would not let go of its tree ({why}), so its folder was put back");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (false, to, TidyKept.Refused,
                $"git would not let go of its tree ({why}), and its folder, moved aside to {to}, could not be put back ({error.Message.Trim()})");
        }
    }

    /// <summary>
    /// The folders the look moved aside that have waited <paramref name="after"/> (AUTOTIDY1): each deleted where nothing in it
    /// was written after it was moved, and kept, saying why, where something was, or where it holds a link or cannot be read.
    /// </summary>
    /// <param name="now">What time it is, against the moment in each folder's name.</param>
    internal IReadOnlyList<SetAside> PurgeSetAside(DateTimeOffset now, TimeSpan after)
    {
        var results = new List<SetAside>();
        if (!Directory.Exists(SetAsideRoot)) return results;
        foreach (var workspace in Directory.EnumerateDirectories(SetAsideRoot))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var folder in Directory.EnumerateDirectories(repository))
        {
            var name = Path.GetFileName(folder);
            var dash = name.LastIndexOf('-');
            if (dash < 0 || !DateTimeOffset.TryParseExact(
                    name[(dash + 1)..], SetAsideStamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moved))
            {
                continue;
            }

            if (now - moved < after) continue;
            (string Code, string Sentence)? kept;
            try
            {
                kept = WrittenSince(folder, moved);
                if (kept is null)
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                kept = (SetAsideKept.Unread, $"it could not be read or deleted ({error.Message.Trim()})");
            }

            results.Add(new SetAside(folder, moved, kept is null, kept?.Sentence, kept?.Code));
        }

        return results;
    }

    /// <summary>Why a folder moved aside is kept: something in it written after <paramref name="moved"/>, or a link in it; null where neither.</summary>
    private static (string Code, string Sentence)? WrittenSince(string folder, DateTimeOffset moved)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var at))
        {
            foreach (var entry in new DirectoryInfo(at).EnumerateFileSystemInfos("*", options))
            {
                var named = Path.GetRelativePath(folder, entry.FullName).Replace('\\', '/');
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) return (SetAsideKept.Linked, $"it holds a link, at {named}");
                if (entry.LastWriteTimeUtc > moved.UtcDateTime)
                {
                    return (SetAsideKept.Written, $"something in it was written after it was moved aside: {named}");
                }

                if (entry is DirectoryInfo directory) pending.Push(directory.FullName);
            }
        }

        return null;
    }
}

/// <summary>A folder the look moved aside, met by a later look's purge (AUTOTIDY1): deleted, or kept and why.</summary>
/// <param name="Moved">When it was moved aside, read from its name.</param>
/// <param name="Kept">Kept: why, in a clause.</param>
/// <param name="Code">Kept: why, as the machine log's code (<see cref="SetAsideKept"/>).</param>
internal sealed record SetAside(string Folder, DateTimeOffset Moved, bool Purged, string? Kept, string? Code = null);

/// <summary>Why a folder moved aside outlives its wait (AUTOTIDY1): the machine log's <c>tidied.kept</c> codes.</summary>
public static class SetAsideKept
{
    /// <summary>Something in it was written after it was moved aside: a late write it was moved to keep.</summary>
    public const string Written = "written";

    /// <summary>It holds a link, which a delete is never asked to follow.</summary>
    public const string Linked = "linked";

    /// <summary>It could not be read, or deleted.</summary>
    public const string Unread = "unread";
}
