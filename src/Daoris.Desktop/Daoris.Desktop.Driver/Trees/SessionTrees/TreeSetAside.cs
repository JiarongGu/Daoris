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
    /// from the folders themselves, not through git. One under a path git ignores goes aside with the tree (OVERVIEWNOISE1a).</item>
    /// </list>
    /// What cannot be read keeps it too.
    /// <para>KEEP1a (Fable F1.5): handed the package folders judged before the hold (<see cref="TreeSlims.JudgeAsync"/>), it checks them
    /// again by the reads it already makes, its index read and its one walk, and puts those that still hold in <paramref name="confirmed"/>
    /// (<see cref="TreeSlims.Confirmed"/>): what the manifest may name. None where a guard keeps the branch.</para>
    /// </remarks>
    internal async Task<(string Code, string Sentence, string? Cause)?> LastLookAsync(
        string root, SweepItem item, CancellationToken ct, IReadOnlyList<string>? judged = null, List<string>? confirmed = null)
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

        IReadOnlyList<string> nested;
        IReadOnlyList<string> links;
        try
        {
            (nested, links) = NestedRepositories(tree);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (TidyKept.Unread, $"its tree's folders could not be read: {error.Message}", TidyCause.Folders);
        }

        // OVERVIEWNOISE1a: a repository under an ignored path (a test's scratch under `_fixtures/`) is ignored content like any
        // other, and goes aside with the folder for fourteen days; only one git would not ignore keeps the tree.
        foreach (var path in nested)
        {
            var (ignoredCode, _, ignoredErr) = await WorkingTree.GitAsync(tree, ["check-ignore", "--quiet", "--", path + "/"], ct).ConfigureAwait(false);
            if (ignoredCode == 0) continue;
            return ignoredCode == 1
                ? (TidyKept.Nested, $"its tree holds another repository at {path}", null)
                : (TidyKept.Unread, $"git could not read whether its tree ignores {path}: {FirstLine(ignoredErr)}", TidyCause.Folders);
        }

        if (judged is { Count: > 0 } && confirmed is not null)
        {
            // KEEP1a: the judgement checked again by these same reads, the index's and the walk's, so the hold gains no git of its own.
            var indexed = listed.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(entry => entry.Length > 2).Select(entry => entry[2..]).ToHashSet(StringComparer.Ordinal);
            confirmed.AddRange(TreeSlims.Confirmed(judged, indexed, nested, links));
        }

        return null;
    }

    /// <summary>
    /// Every folder below the tree's top that holds a <c>.git</c> file or folder, as paths from the tree's root with forward
    /// slashes, none read into: what lies inside one is that repository's. Read from the folders, hidden ones included, never
    /// following a link; the tree's own <c>.git</c> at its top is passed over, and never read into. A folder that cannot be read throws.
    /// KEEP1a: the same walk names every link it meets, never followed, which the slim's judgement reads (Fable F1.5: one walk shared).
    /// </summary>
    private static (List<string> Nested, List<string> Links) NestedRepositories(string tree)
    {
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var found = new List<string>();
        var links = new List<string>();
        var pending = new Stack<string>();
        pending.Push(top);
        while (pending.TryPop(out var folder))
        {
            var below = new List<string>();
            var linked = new List<string>();
            var repository = false;
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options))
            {
                if (string.Equals(entry.Name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    repository = !string.Equals(folder, top, StringComparison.OrdinalIgnoreCase);
                    if (repository) break;
                    continue;
                }

                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    linked.Add(Path.GetRelativePath(top, entry.FullName).Replace('\\', '/'));
                }
                else if (entry is DirectoryInfo directory)
                {
                    below.Add(directory.FullName);
                }
            }

            if (repository)
            {
                found.Add(Path.GetRelativePath(top, folder).Replace('\\', '/'));
                continue;
            }

            links.AddRange(linked);
            foreach (var each in below) pending.Push(each);
        }

        return (found, links);
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
    /// <para>KEEP1a (Codex 2.4): handed the package folders the last look confirmed, it chooses the folder's name first and writes its
    /// manifest beside where it will go, <c>judged</c>, before the move, then <c>moved</c> once git let go of the tree; a move refused or a
    /// folder put back takes the manifest with it, so only a tree git no longer knows is ever slimmed (<see cref="SlimManifest"/>).</para>
    /// </remarks>
    internal Task<(bool Gone, string? To, string? Code, string Sentence)> MoveAsideAsync(
        string root, SweepItem item, CancellationToken ct, IReadOnlyList<string>? slim = null) =>
        MoveAsideAsync(root, item.Tree!, item.Workspace, item.Repository, ct, slim, item.Branch);

    /// <inheritdoc cref="MoveAsideAsync(string, SweepItem, CancellationToken, IReadOnlyList{string})"/>
    internal async Task<(bool Gone, string? To, string? Code, string Sentence)> MoveAsideAsync(
        string root, string treePath, string workspace, string repository, CancellationToken ct, IReadOnlyList<string>? slim = null, string branch = "")
    {
        var tree = Path.TrimEndingDirectorySeparator(Path.GetFullPath(treePath));
        string? to = null;
        string? manifest = null;
        if (Directory.Exists(tree))
        {
            var named = AsideName(tree, workspace, repository);
            if (slim is { Count: > 0 }) manifest = Judged(named, repository, branch, slim);
            var moved = MoveFolderAside(tree, workspace, repository, "its tree", "its folder", named);
            if (moved.Code is { } kept)
            {
                Forget(manifest);
                return (false, null, kept, moved.Sentence);
            }

            to = moved.To;
        }

        var (code, _, err) = await WorkingTree.GitAsync(root, ["worktree", "remove", tree], ct).ConfigureAwait(false);
        if (code == 0)
        {
            MarkMoved(manifest);
            return (true, to, null, "");
        }

        Forget(manifest);
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
    /// <param name="named">Where it goes, chosen before (<see cref="AsideName"/>); null chooses it now.</param>
    internal (string? To, string? Code, string Sentence) MoveFolderAside(
        string folder, string workspace, string repository, string what = "it", string whose = "it", string? named = null)
    {
        var to = named ?? AsideName(folder, workspace, repository);
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

    /// <summary>
    /// Where a folder moved aside now goes: <see cref="SetAsideRoot"/>, its workspace and repository, its name and this moment, in UTC
    /// (AUTOTIDY1). Chosen before the move, so a manifest can be written beside it first (KEEP1a).
    /// </summary>
    internal string AsideName(string folder, string workspace, string repository) =>
        Path.Combine(SetAsideRoot, workspace, repository,
            $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))}-{DateTimeOffset.UtcNow.ToString(SetAsideStamp, CultureInfo.InvariantCulture)}");

    /// <summary>
    /// The manifest of a folder about to move, written <c>judged</c> beside where it goes (KEEP1a, Codex 2.4); its path, or null where it
    /// was not written, so the folder moves whole as before. Never written where that folder is reached through a link, which the move
    /// refuses too.
    /// </summary>
    private string? Judged(string to, string repository, string branch, IReadOnlyList<string> slim)
    {
        try
        {
            var beside = Path.GetDirectoryName(to)!;
            Directory.CreateDirectory(beside);
            if (context.Worktrees.LinkBelowTreesRoot(beside) is not null) return null;
            var path = SlimManifest.PathFor(to);
            new SlimManifest(SlimStates.Judged, DateTimeOffset.UtcNow, null, repository, branch, [.. slim.Select(each => new SlimPath(each, SlimStates.Pending))])
                .Write(path);
            return path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The manifest marked <c>moved</c> once git let go of the tree; only one still at <c>judged</c>, never one made anew.</summary>
    private static void MarkMoved(string? path)
    {
        if (path is null || SlimManifest.Read(path).Manifest is not { State: SlimStates.Judged } judged) return;
        try
        {
            (judged with { State = SlimStates.Moved, Moved = DateTimeOffset.UtcNow }).Write(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left at `judged`, the next pass keeps the folder whole and drops it: the move stands, the slim does not happen.
        }
    }

    /// <summary>The manifest of a move refused or a folder put back, gone with the attempt (Codex 2.4).</summary>
    private static void Forget(string? path)
    {
        if (path is null) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left at `judged` beside no folder, the next pass drops it.
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
    internal IReadOnlyList<SetAside> PurgeSetAside(DateTimeOffset now, TimeSpan after) => PurgeSetAside(now, after, () => false, out _);

    /// <summary>
    /// <inheritdoc cref="PurgeSetAside(DateTimeOffset, TimeSpan)"/> Within the Keeping pass's budget (KEEP1a, the cleanup design §3.1):
    /// once <paramref name="spent"/> says so, between folders and never inside one's delete, it stops, and the next pass takes up the
    /// rest; a folder whose delete failed for the same reason as at its last try waits a day. A folder's manifest goes with it.
    /// </summary>
    internal IReadOnlyList<SetAside> PurgeSetAside(DateTimeOffset now, TimeSpan after, Func<bool> spent, out bool stopped)
    {
        stopped = false;
        var results = new List<SetAside>();
        if (!Directory.Exists(SetAsideRoot)) return results;
        var before = FailedEntries();
        var failed = new List<(string Folder, string Why, DateTimeOffset? Tried)>();
        foreach (var workspace in Directory.EnumerateDirectories(SetAsideRoot))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var folder in Directory.EnumerateDirectories(repository))
        {
            if (MovedAt(Path.GetFileName(folder)) is not { } moved || now - moved < after) continue;
            var last = before.GetValueOrDefault(Path.GetFullPath(folder));
            if (last.Tried is { } tried && now - tried < SetAsideKeeping.Again)
            {
                // Failed the same way twice: kept as the last pass said, and tried again the next day.
                failed.Add((folder, last.Why!, tried));
                results.Add(new SetAside(folder, moved, false, last.Why, SetAsideKept.Unread));
                continue;
            }

            if (stopped || spent())
            {
                stopped = true;
                if (last.Why is { } why) failed.Add((folder, why, last.Tried));
                continue;
            }

            var kept = Kept(folder, moved);
            long bytes = 0;
            if (kept is null)
            {
                try
                {
                    bytes = DeleteNeverFollowing(folder);
                    var manifest = SlimManifest.PathFor(folder);
                    if (File.Exists(manifest)) File.Delete(manifest);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    kept = (SetAsideKept.Unread, $"it could not be read or deleted ({error.Message.Trim()})");
                    failed.Add((folder, kept.Value.Sentence, last.Why == kept.Value.Sentence ? now : null));
                }
            }

            results.Add(new SetAside(folder, moved, kept is null, kept?.Sentence, kept?.Code) { Bytes = bytes });
        }

        KeepFailed(failed);
        return results;
    }

    /// <summary>
    /// Where the purge keeps the folders whose delete it could not finish (CLEANUP1, re-check), beside them: a delete refused after the
    /// read found nothing late (a file held open against it) shows in no read the inventory can repeat, so it is kept here, and the
    /// inventory reads it (<see cref="Failed"/>). Each pass writes what it met, so a folder a later pass deletes leaves it.
    /// </summary>
    internal string FailedPath => Path.Combine(SetAsideRoot, "purge-failed.json");

    /// <summary>
    /// The folders this pass could not delete, kept where the inventory reads them; written only where they changed, gone where none. A
    /// folder that failed the same way twice carries <c>tried</c>, when (KEEP1a: it waits a day from there), a field added after the rest.
    /// </summary>
    private void KeepFailed(IReadOnlyList<(string Folder, string Why, DateTimeOffset? Tried)> failed)
    {
        try
        {
            if (failed.Count == 0)
            {
                if (File.Exists(FailedPath)) File.Delete(FailedPath);
                return;
            }

            var text = JsonFile.Serialize(writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("folders");
                foreach (var (folder, why, tried) in failed.OrderBy(each => each.Folder, StringComparer.Ordinal))
                {
                    writer.WriteStartObject();
                    writer.WriteString("folder", Path.GetRelativePath(SetAsideRoot, folder).Replace('\\', '/'));
                    writer.WriteString("why", why);
                    if (tried is { } at) writer.WriteString("tried", at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
            if (File.Exists(FailedPath) && File.ReadAllText(FailedPath) == text) return;
            AtomicFile.WriteText(FailedPath, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The purge's own report still says each folder it kept; the inventory then reads it as before.
        }
    }

    /// <summary>
    /// The folders the last purge could not delete, by full path, each with the purge's reason (<see cref="FailedPath"/>); none where
    /// nothing is kept or it does not read.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Failed() =>
        FailedEntries().ToDictionary(each => each.Key, each => each.Value.Why!, StringComparer.OrdinalIgnoreCase);

    /// <summary><see cref="Failed"/>'s folders, each with when it last failed as before, where it did (KEEP1a's day's wait).</summary>
    private Dictionary<string, (string? Why, DateTimeOffset? Tried)> FailedEntries()
    {
        var found = new Dictionary<string, (string? Why, DateTimeOffset? Tried)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(FailedPath)) return found;
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(FailedPath));
            if (!document.RootElement.TryGetProperty("folders", out var folders) || folders.ValueKind != System.Text.Json.JsonValueKind.Array) return found;
            foreach (var each in folders.EnumerateArray())
            {
                if (each.ValueKind == System.Text.Json.JsonValueKind.Object
                    && each.TryGetProperty("folder", out var folder) && folder.GetString() is { Length: > 0 } named
                    && each.TryGetProperty("why", out var why) && why.GetString() is { Length: > 0 } said)
                {
                    var tried = each.TryGetProperty("tried", out var at) && at.ValueKind == System.Text.Json.JsonValueKind.String
                                && DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when)
                        ? when
                        : (DateTimeOffset?)null;
                    found[Path.GetFullPath(Path.Combine(SetAsideRoot, named))] = (said, tried);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            // A record that does not read claims nothing: each folder reads as its own read says.
        }

        return found;
    }

    /// <summary>
    /// Why the purge keeps a folder moved aside at <paramref name="moved"/>, read and never written: reached through a link, a link in it
    /// outside a build folder, something in it written after the move, or a folder that cannot be read; null where the purge would delete
    /// it. The purge's own read, and the inventory's (CLEANUP1, after review), so a folder its rule can no longer finish is said to the
    /// person rather than retained for ever: the comparison is with the move, so a later write keeps it at every pass.
    /// <para>KEEP1a (the cleanup design §5 step 4): the manifest beside it, where it reads, says what the slim removed, which the read
    /// passes over (<see cref="WrittenSince"/>). One that does not read says nothing, so the folder is read whole: kept more, never less.</para>
    /// </summary>
    internal (string Code, string Sentence)? Kept(string folder, DateTimeOffset moved)
    {
        try
        {
            // A link on the way to it, the folder itself included, makes it somewhere else: never walked into (TIDIEDLINK1).
            return context.Worktrees.LinkBelowTreesRoot(folder) is { } link
                ? (SetAsideKept.Linked, $"it is reached through a link, at {link}")
                : WrittenSince(folder, moved, SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (SetAsideKept.Unread, $"it could not be read or deleted ({error.Message.Trim()})");
        }
    }

    /// <summary>
    /// Why a folder moved aside is kept: something in it written after <paramref name="moved"/>, or a link in it outside a build
    /// folder; null where neither. A link under a build folder is a package manager's, which goes as a link (TIDIEDLINK1): its own
    /// write is read, never what it points at, and it is never walked into.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Each entry's creation time too</b> (KEEP1a, Fable F1.1, as <see cref="TreeStrays"/> reads one): a copy keeps its source's
    /// write time and takes a new creation time, so a file copied in after the move is a write.</para>
    /// <para>🔴 <b>What the slim removed</b> (the cleanup design §5 step 4, the steward's S1): removing a folder changes the time of the
    /// folder above it, which this read would take for a write after the move and keep for ever. So a path the manifest marks
    /// <c>removing</c> is passed over, never read into; the folder directly above a <c>removed</c> path has its own write time read
    /// against when that removal ended, and passed over while a path below it is still <c>removing</c>. Its creation time, and every
    /// other entry, are read against the move; a removed path there again is read as any entry, a write.</para>
    /// </remarks>
    private static (string Code, string Sentence)? WrittenSince(string folder, DateTimeOffset moved, SlimManifest? manifest = null)
    {
        var same = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var removing = new HashSet<string>(same);
        var passedOver = new HashSet<string>(same);
        var unknown = new HashSet<string>(same);
        var ended = new Dictionary<string, DateTime>(same);
        foreach (var path in manifest?.Paths ?? [])
        {
            var above = path.Path.LastIndexOf('/') is var slash and > 0 ? path.Path[..slash] : "";
            if (path.State == SlimStates.Removing)
            {
                removing.Add(path.Path);
                passedOver.Add(above);
            }
            else if (path.State == SlimStates.Removed && path.Ended is { } end)
            {
                ended[above] = ended.TryGetValue(above, out var was) && was > end.UtcDateTime ? was : end.UtcDateTime;
            }
            else if (path.State == SlimStates.Removed)
            {
                // A removal whose end is not known reads the folder above against the move: kept more, never less.
                unknown.Add(above);
            }
        }

        var after = moved.UtcDateTime;
        DateTime Since(string named) => !unknown.Contains(named) && ended.TryGetValue(named, out var end) && end > after ? end : after;
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var at))
        {
            foreach (var entry in new DirectoryInfo(at).EnumerateFileSystemInfos("*", options))
            {
                var named = Path.GetRelativePath(folder, entry.FullName).Replace('\\', '/');
                if (removing.Contains(named)) continue;
                var link = entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
                if (link && !TreeScratch.UnderBuildFolder(named)) return (SetAsideKept.Linked, $"it holds a link, at {named}");
                var written = entry.CreationTimeUtc > after || (!passedOver.Contains(named) && entry.LastWriteTimeUtc > Since(named));
                if (written) return (SetAsideKept.Written, $"something in it was written after it was moved aside: {named}");
                if (entry is DirectoryInfo directory && !link) pending.Push(directory.FullName);
            }
        }

        return null;
    }

    /// <summary>
    /// <paramref name="folder"/> deleted with everything in it, never following a link (TIDIEDLINK1): a link goes as a link, its own
    /// entry alone, so what it points at stays wherever it is; a read-only file, as git's objects are, is made writable first, and
    /// only a file that is one. A folder or file that cannot be deleted throws, and what was deleted before it stays deleted. A
    /// tree's open discards an unfinished copy of its checkout's dependencies with it too (TREEDEPS1). The bytes its files held.
    /// </summary>
    /// <remarks>
    /// 🔴 KEEP1a (Codex 2.5): it checks its own argument, and each folder again right before it reads it, and refuses one that is a link:
    /// handed a folder that is, or became, a junction, it would have read the junction's target. What still crosses is a folder replaced
    /// by a junction between that check and the read, under a trees home only Daoris writes (the cleanup design §10).
    /// </remarks>
    /// <exception cref="IOException">It is a link, or something in it could not be deleted.</exception>
    internal static long DeleteNeverFollowing(string folder)
    {
        // A folder that is not there reads every attribute set (-1): its enumeration below says it is missing, as it always did.
        var attributes = new DirectoryInfo(folder).Attributes;
        if ((int)attributes != -1 && attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"{folder} is a link, so nothing is deleted through it");
        }

        long bytes = 0;
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
                bytes += DeleteNeverFollowing(inner.FullName);
            }
            else
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReadOnly)) entry.Attributes &= ~FileAttributes.ReadOnly;
                bytes += ((FileInfo)entry).Length;
                entry.Delete();
            }
        }

        Directory.Delete(folder, recursive: false);
        return bytes;
    }
}

/// <summary>A folder the look moved aside, met by a later look's purge (AUTOTIDY1): deleted, or kept and why.</summary>
/// <param name="Moved">When it was moved aside, read from its name.</param>
/// <param name="Kept">Kept: why, in a clause.</param>
/// <param name="Code">Kept: why, as the machine log's code (<see cref="SetAsideKept"/>).</param>
internal sealed record SetAside(string Folder, DateTimeOffset Moved, bool Purged, string? Kept, string? Code = null)
{
    /// <summary>Purged: the bytes its files held (KEEP1a, <c>keeping.json</c>'s <c>last</c>).</summary>
    public long Bytes { get; init; }
}

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
