using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the look's own tidy did in one repository (AUTOTIDY1): each empty session branch the press's path judged, removed or
/// kept, or why none was judged this look.
/// </summary>
/// <param name="Results">What the clean-up's press did with each candidate, in its own words (<see cref="SessionTrees.SweepAsync"/>).</param>
/// <param name="Held">Why nothing was judged in the repository this look (<see cref="TidyKept"/>); null where the press's path ran.</param>
/// <param name="Detail">git's or the service's own words beside <paramref name="Held"/>.</param>
public sealed record TidyPass(IReadOnlyList<SweepResult> Results, string? Held = null, string? Detail = null)
{
    /// <summary>The repository looked at, as the registry names it.</summary>
    public string Repository { get; init; } = "";

    /// <summary>Its workspace, as <see cref="RemoteTarget.Workspace"/> reads the registry's row.</summary>
    public string Workspace { get; init; } = "";
}

/// <summary>
/// Why the look's own tidy left an empty session branch, or a repository's, where it would have gone (AUTOTIDY1): the
/// machine log's <c>branch.kept</c> codes. What it holds (dirty, in use, a build's output) is no code: it is the press's.
/// </summary>
public static class TidyKept
{
    /// <summary>git could not answer: a guard, the branches' list, or whether the checkout is a repository of its own.</summary>
    public const string Unread = "unread";

    /// <summary>A session was starting in one of the repository's trees, which holds them (<see cref="TreeLock"/>).</summary>
    public const string Starting = "starting";

    /// <summary>The service did not say which sessions run or wait, or which repositories are here.</summary>
    public const string Sessions = "sessions";

    /// <summary>The removal did not happen: its tree is locked, git would not let its record go, or the branch moved.</summary>
    public const string Refused = "refused";

    /// <summary>Something on this machine holds its tree's folder open, so the folder could not be moved aside.</summary>
    public const string Busy = "busy";

    /// <summary>A tracked path in its tree is marked <c>assume-unchanged</c> or <c>skip-worktree</c>, which hides its changes.</summary>
    public const string Hidden = "hidden";

    /// <summary>Its tree holds another repository, which the tree's own status does not see.</summary>
    public const string Nested = "nested";

    /// <summary>Its tree is reached through a link under the trees home, so it is somewhere else.</summary>
    public const string Linked = "linked";
}

public sealed partial class SessionTrees
{
    /// <summary>
    /// Whether the look takes this session branch without a press (AUTOTIDY1): the clean-up's <c>empty</c> kind alone, nothing
    /// beyond the line, and every guard read. With no tree it goes; with a tree, only one of this home's that holds nothing at
    /// all, an ignored path the checkout also holds included, on a branch whose work the line took (<see cref="SweepItem.Worked"/>).
    /// A tree whose branch never moved is a conversation's place, which words to its session go on in (D137): removed, they
    /// could not, so it stays the press's. Every other kind stays the press's, as it was: a squash merge's carried or
    /// content-held branch is <c>landed</c> or <c>carried</c>, never <c>empty</c>.
    /// </summary>
    public bool GoesByItself(SweepItem item) =>
        item is { Kind: SweepKind.Empty, Unread: false, IgnoredShared: 0, HeldBy: null, CarriedBy: null }
        && (item.Tree is null || (Holds(item.Tree) && item.Worked));

    /// <summary>
    /// The look's own tidy of one repository (AUTOTIDY1, D88's note): every session branch whose tip the line holds, read from
    /// the line here with no fetch, is judged and removed by the clean-up's own press (<see cref="SweepAsync"/>), taking only
    /// what <see cref="GoesByItself"/> takes, judged again right before it goes, and deleted only while it is the commit judged.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Held alone, the sessions asked inside the hold</b>: a fresh tree a start just opened is an empty branch with
    /// a clean tree until its record opens, so the repository's trees are held as bringing them up to date holds them
    /// (<see cref="TreeLock"/>), and a start that holds them leaves every branch for another look.</para>
    ///
    /// <para><b>The checkout must be a repository of its own</b>: git walks up from a folder that is not one, and would answer
    /// for the repository above it (FIX-LOG).</para>
    /// </remarks>
    /// <param name="inUse">The trees sessions still running or waiting name, asked once the repository is held.</param>
    public async Task<TidyPass> TidyEmptyAsync(
        string root, string repository, string? workspace, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse,
        CancellationToken ct = default)
    {
        var space = RemoteTarget.Workspace(workspace);
        TidyPass Pass(IReadOnlyList<SweepResult> results, string? held = null, string? detail = null) =>
            new(results, held, detail) { Repository = repository, Workspace = space };

        // A main checkout's `.git` is a folder; a linked worktree's is a file, and a folder inside a repository has none. Asked
        // before any git, so a root that cannot hold session branches costs nothing.
        if (!Directory.Exists(Path.Combine(root, ".git"))) return Pass([]);
        var (topCode, toplevel, topErr) = await WorkingTree.GitAsync(root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (topCode != 0) return Pass([], TidyKept.Unread, FirstLine(topErr));
        if (!SamePath(toplevel.Trim(), root)) return Pass([]);

        // The line here, read with no fetch: the local branch, else origin's copy as this checkout last saw it.
        var line = (await LineAsync(root, repository, space, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return Pass([]);
        var (code, refs, err) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(refname:short)", $"--merged={against}", "refs/heads/daoris/"], ct).ConfigureAwait(false);
        if (code != 0) return Pass([], TidyKept.Unread, FirstLine(err));
        var candidates = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(branch => $"{repository}:{branch}")
            .ToHashSet(StringComparer.Ordinal);
        if (candidates.Count == 0) return Pass([]);

        // A list of the working trees git could not give is never "no tree here": nothing in the repository is judged this look.
        if (await ListWorktreesAsync(root, ct).ConfigureAwait(false) is null) return Pass([], TidyKept.Unread, ListUnread);

        using var hold = TreeLock.TryReplaying(home, space, repository);
        if (hold is null) return Pass([], TidyKept.Starting);
        IReadOnlySet<string> busy;
        try
        {
            busy = await inUse(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException)
        {
            return Pass([], TidyKept.Sessions, error.Message);
        }

        var results = await SweepAsync([(repository, space, root)], busy, candidates, byItself: true, ct).ConfigureAwait(false);
        // The record of where session branches grew from drops what is gone (LAND3), as the press's does: after a removal, so not
        // cancelled either.
        if (results.Any(result => result.Removed)) await ForgetGoneAsync(root, repository, CancellationToken.None).ConfigureAwait(false);
        return Pass(results);
    }

    /// <summary>The folder under the trees home the look's own tidy moves a tree's folder aside into (AUTOTIDY1).</summary>
    internal const string SetAsideFolder = ".tidied";

    /// <summary>How a folder moved aside is named after its tree: the tree's name, then the moment it was moved, in UTC.</summary>
    private const string SetAsideStamp = "yyyyMMdd'T'HHmmssfff'Z'";

    /// <summary>Where the look's own tidy keeps the folders it moved aside: <c>trees/.tidied/&lt;workspace&gt;/&lt;repository&gt;/</c>.</summary>
    public string SetAsideRoot => Path.Combine(TreesRoot, SetAsideFolder);

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
    private async Task<(string Code, string Sentence)?> LastLookAsync(string root, SweepItem item, CancellationToken ct)
    {
        if (item.Tree is not { } tree)
        {
            var folder = Path.Combine(TreesRoot, item.Workspace, item.Repository, item.Branch[SessionPrefix.Length..]);
            return Directory.Exists(folder) ? (TidyKept.Unread, $"its folder at {folder} is here, and git lists no tree for it") : null;
        }

        if (LinkBelowTreesRoot(tree) is not null) return (TidyKept.Linked, "its tree is reached through a link under the trees home");
        if (await ListWorktreesAsync(root, ct).ConfigureAwait(false) is not { } entries) return (TidyKept.Unread, ListUnread);
        if (entries.FirstOrDefault(entry => SamePath(entry.Path, tree)) is { Locked: true } locked)
        {
            return (TidyKept.Refused, "its tree is locked" + (locked.LockReason is { Length: > 0 } reason ? $" ({reason})" : "")
                + "; `git worktree unlock` lets it go");
        }

        if (!Directory.Exists(tree)) return null;

        var (code, listed, err) = await WorkingTree.GitAsync(tree, ["ls-files", "-v", "-z"], ct).ConfigureAwait(false);
        if (code != 0) return (TidyKept.Unread, $"git could not read its tree's index: {FirstLine(err)}");
        // `ls-files -v` tags an assume-unchanged entry in lower case, and a skip-worktree one `S`.
        var hidden = listed.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => entry.Length > 2 && (char.IsLower(entry[0]) || entry[0] == 'S'))
            .Select(entry => entry[2..])
            .ToList();
        if (hidden.Count > 0)
        {
            return (TidyKept.Hidden, $"{hidden.Count} tracked path(s) in its tree are marked assume-unchanged or skip-worktree, which "
                + $"hides their changes from git: {string.Join(", ", hidden.Take(3))}" + (hidden.Count > 3 ? $" and {hidden.Count - 3} more" : ""));
        }

        try
        {
            return NestedRepository(tree) is { } nested ? (TidyKept.Nested, $"its tree holds another repository at {nested}") : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (TidyKept.Unread, $"its tree's folders could not be read: {error.Message}");
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
    private async Task<(bool Gone, string? To, string? Code, string Sentence)> MoveAsideAsync(string root, SweepItem item, CancellationToken ct)
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
                if (LinkBelowTreesRoot(Path.GetDirectoryName(to)!) is not null)
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
