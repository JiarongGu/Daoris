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
    internal const string SetAsideStamp = "yyyyMMdd'T'HHmmssfff'Z'";

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
    internal Task<(bool Gone, string? To, string? Code, string Sentence)> MoveAsideAsync(string root, SweepItem item, CancellationToken ct) =>
        MoveAsideAsync(root, item.Tree!, item.Workspace, item.Repository, ct);

    /// <inheritdoc cref="MoveAsideAsync(string, SweepItem, CancellationToken)"/>
    internal async Task<(bool Gone, string? To, string? Code, string Sentence)> MoveAsideAsync(
        string root, string treePath, string workspace, string repository, CancellationToken ct)
    {
        var tree = Path.TrimEndingDirectorySeparator(Path.GetFullPath(treePath));
        string? to = null;
        if (Directory.Exists(tree))
        {
            var moved = MoveFolderAside(tree, workspace, repository, "its tree", "its folder");
            if (moved.Code is { } kept) return (false, null, kept, moved.Sentence);
            to = moved.To;
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
    /// A folder under the trees home moved aside, in one rename on the same volume, into <see cref="SetAsideRoot"/> under its
    /// workspace and repository, named for the moment it moved (AUTOTIDY1; AUTOTIDY2c, a folder that is no tree): where it went,
    /// or why it stays, as the machine log's code and a clause. A refused rename is a folder something uses (<c>busy</c>).
    /// </summary>
    /// <param name="what">What something is using, in the clause: <c>its tree</c>, or the folder itself.</param>
    /// <param name="whose">What could not be moved, in the clause: <c>its folder</c>, or the folder itself.</param>
    internal (string? To, string? Code, string Sentence) MoveFolderAside(
        string folder, string workspace, string repository, string what = "it", string whose = "it")
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        var to = Path.Combine(SetAsideRoot, workspace, repository, $"{name}-{DateTimeOffset.UtcNow.ToString(SetAsideStamp, CultureInfo.InvariantCulture)}");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            if (context.Worktrees.LinkBelowTreesRoot(Path.GetDirectoryName(to)!) is not null)
            {
                return (null, TidyKept.Linked, "the folder it would be moved aside into is reached through a link under the trees home");
            }

            Directory.Move(folder, to);
            return (to, null, "");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, TidyKept.Busy,
                $"something on this machine is using {what}, so {whose} could not be moved aside ({error.Message.Trim()}); a later look tries again");
        }
    }

    /// <summary>When a folder moved aside was moved, read from its name (<c>&lt;name&gt;-&lt;moment&gt;</c>); null where its name says none.</summary>
    internal static DateTimeOffset? MovedAt(string name)
    {
        var dash = name.LastIndexOf('-');
        return dash >= 0 && DateTimeOffset.TryParseExact(
            name[(dash + 1)..], SetAsideStamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moved)
            ? moved
            : null;
    }

    /// <summary>
    /// The folders the look moved aside that have waited <paramref name="after"/> (AUTOTIDY1): each deleted where nothing in it
    /// was written after it was moved, and kept, saying why, where something was, or where it holds a link outside a build folder,
    /// is reached through one, or cannot be read.
    /// </summary>
    /// <remarks>
    /// <para>A path past Windows' 260 characters, as a tree's own <c>node_modules</c> holds one, is read and deleted as any other
    /// (AUTOTIDY2b, proven over a deep tree): .NET hands Windows such a path in its extended form, as git's <c>core.longpaths</c>
    /// does for the tree's own record.</para>
    ///
    /// <para><b>Links</b> (TIDIEDLINK1, D88's note): a link under a build folder (<see cref="TreeScratch.UnderBuildFolder"/>), as
    /// npm's workspaces and pnpm make under <c>node_modules</c>, goes with its folder as a link, its own entry alone; what it points
    /// at stays wherever it is. Nothing here ever reads, clears or deletes through a link (<see cref="DeleteNeverFollowing"/>).</para>
    /// </remarks>
    /// <param name="now">What time it is, against the moment in each folder's name.</param>
    internal IReadOnlyList<SetAside> PurgeSetAside(DateTimeOffset now, TimeSpan after)
    {
        var results = new List<SetAside>();
        if (!Directory.Exists(SetAsideRoot)) return results;
        foreach (var workspace in Directory.EnumerateDirectories(SetAsideRoot))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var folder in Directory.EnumerateDirectories(repository))
        {
            if (MovedAt(Path.GetFileName(folder)) is not { } moved || now - moved < after) continue;
            (string Code, string Sentence)? kept;
            try
            {
                // A link on the way to it, the folder itself included, makes it somewhere else: never walked into (TIDIEDLINK1).
                kept = context.Worktrees.LinkBelowTreesRoot(folder) is { } link
                    ? (SetAsideKept.Linked, $"it is reached through a link, at {link}")
                    : WrittenSince(folder, moved);
                if (kept is null) DeleteNeverFollowing(folder);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                kept = (SetAsideKept.Unread, $"it could not be read or deleted ({error.Message.Trim()})");
            }

            results.Add(new SetAside(folder, moved, kept is null, kept?.Sentence, kept?.Code));
        }

        return results;
    }

    /// <summary>
    /// Why a folder moved aside is kept: something in it written after <paramref name="moved"/>, or a link in it outside a build
    /// folder; null where neither. A link under a build folder is a package manager's, which goes as a link (TIDIEDLINK1): its own
    /// write is read, never what it points at, and it is never walked into.
    /// </summary>
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
                var link = entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
                if (link && !TreeScratch.UnderBuildFolder(named)) return (SetAsideKept.Linked, $"it holds a link, at {named}");
                if (entry.LastWriteTimeUtc > moved.UtcDateTime)
                {
                    return (SetAsideKept.Written, $"something in it was written after it was moved aside: {named}");
                }

                if (entry is DirectoryInfo directory && !link) pending.Push(directory.FullName);
            }
        }

        return null;
    }

    /// <summary>
    /// <paramref name="folder"/> deleted with everything in it, never following a link (TIDIEDLINK1): a link goes as a link, its own
    /// entry alone, so what it points at stays wherever it is; a read-only file, as git's objects are, is made writable first, and
    /// only a file that is one. A folder or file that cannot be deleted throws, and what was deleted before it stays deleted.
    /// </summary>
    private static void DeleteNeverFollowing(string folder)
    {
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options).ToList())
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                // Windows takes a junction or a folder's link off with the folder call, which removes the link and never reaches
                // into it; elsewhere a link to a folder is a file to unlink.
                if (entry is DirectoryInfo && OperatingSystem.IsWindows()) Directory.Delete(entry.FullName, recursive: false);
                else File.Delete(entry.FullName);
            }
            else if (entry is DirectoryInfo inner)
            {
                DeleteNeverFollowing(inner.FullName);
            }
            else
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReadOnly)) entry.Attributes &= ~FileAttributes.ReadOnly;
                entry.Delete();
            }
        }

        Directory.Delete(folder, recursive: false);
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

    /// <summary>
    /// It holds a link outside a build folder, or is reached through one, which a delete is never asked to follow; a link under a
    /// build folder goes with it as a link (TIDIEDLINK1).
    /// </summary>
    public const string Linked = "linked";

    /// <summary>It could not be read, or deleted.</summary>
    public const string Unread = "unread";
}
