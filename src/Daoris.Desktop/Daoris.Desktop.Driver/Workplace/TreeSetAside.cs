using System.Globalization;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;
using static Daoris.Driver.SetAsideMarks;
using static Daoris.Driver.SetAsideReads;

namespace Daoris.Driver;

/// <summary>
/// What the look's own tidy decides of one session branch and does to its tree (out of
/// <c>SessionTrees.Auto.cs</c>): which branches it takes without a press, its last guards right before one goes, the tree's folder
/// moved aside rather than deleted, and the later purge of what moved. Over the trees' context alone: the clean-up's sweep
/// (<see cref="TreeSweeps"/>) takes it for its by-itself path, and the look's pass (<see cref="AutoTidies"/>) takes the sweep.
/// </summary>
internal sealed class TreeSetAside(TreeContext context)
{
    /// <summary>
    /// Whether the look takes this session branch without a press: every kind the press clears
    /// (<see cref="SweepItem.Removable"/>: empty, landed by ancestry or by content, carried), with every guard read. With no tree
    /// it goes; with a tree, only one of this home's on a branch that moved since it grew (<see cref="SweepItem.Worked"/>). A tree
    /// whose branch never moved is a conversation's place, which words to its session go on in: removed, they could not,
    /// so it stays the press's, whatever its kind. A tree a run owns (<see cref="GrownBranch.Run"/>) is that run's to end (<see cref="TreeReleases"/>). A tree holding only ignored paths goes: by the owner's decision a finished session's
    /// own scratch goes with its tree, named in its record; uncommitted and untracked paths keep
    /// it, as the judgement's <c>dirty</c>.
    /// A landed branch goes only once its work is on the line (<see cref="SweepItem.OnLine"/>): landed on another branch of the
    /// person's alone, its pull request may still be open and its session asked to change it in its own tree, so the look waits
    /// for the merge it began from, and the press may take it before by the person's choice.
    /// </summary>
    internal bool GoesByItself(SweepItem item) =>
        item is { Removable: true, Unread: false }
        && (item.Kind != SweepKind.Landed || item.OnLine)
        && (item.Tree is null || (context.Holds(item.Tree) && item.Worked && context.Grown.Of(item.Repository, item.Branch)?.Run is null));

    /// <summary>The folder under the trees home the look's own tidy moves a tree's folder aside into.</summary>
    internal const string SetAsideFolder = ".tidied";

    /// <summary>How a folder moved aside is named after its tree: the tree's name, then the moment of the move, in UTC.</summary>
    internal const string SetAsideStamp = "yyyyMMdd'T'HHmmssfff'Z'";

    /// <summary>Where the look's own tidy keeps the folders it moved aside: <c>trees/.tidied/&lt;workspace&gt;/&lt;repository&gt;/</c>.</summary>
    internal string SetAsideRoot => Path.Combine(context.TreesRoot, SetAsideFolder);

    /// <summary>
    /// Every place the set-aside folders lie: <c>.tidied</c> under each place of the trees
    /// entry (<see cref="TreeContext.TreesRoots"/>), the new first. A folder moves into <see cref="SetAsideRoot"/> alone; the folders set
    /// aside before the home's layout moved stay at the old place, where the purge, the slim, the press, the floor, the look and the card
    /// read them too.
    /// </summary>
    internal IReadOnlyList<string> SetAsideRoots =>
        [.. context.TreesRoots.Select(root => Path.Combine(root, SetAsideFolder)).Append(SetAsideRoot).Distinct(PathComparer)];

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// The look's last guards, asked after the press's judgement and its judgement again, right before anything
    /// moves: each keeps the branch for the press, saying why. Null where none keeps it.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A branch git lists no tree for, whose folder of this home's is here: git dropped a record it could not read.</item>
    /// <item>A tree reached through a link under the trees home, which is somewhere else whatever its path says.</item>
    /// <item>A tree a person locked (<c>git worktree lock</c>).</item>
    /// <item>A tracked path marked <c>assume-unchanged</c> or <c>skip-worktree</c>: an edit to it reads as clean to git's status.</item>
    /// <item>Another repository in the tree, a <c>.git</c> file or folder below its top, which the tree's own status skips: read
    /// from the folders themselves, not through git. One under a path git ignores goes aside with the tree.</item>
    /// </list>
    /// What cannot be read keeps it too.
    /// <para>Handed the package folders judged before the hold (<see cref="TreeSlims.JudgeAsync"/>), it checks them
    /// again by the reads it already makes, its index read and its one walk, and puts those that still hold in <paramref name="confirmed"/>
    /// (<see cref="TreeSlims.Confirmed"/>): what the manifest may name. None where a guard keeps the branch.</para>
    /// </remarks>
    internal async Task<(string Code, string Sentence, string? Cause)?> LastLookAsync(
        string root, SweepItem item, CancellationToken ct, IReadOnlyList<string>? judged = null, List<string>? confirmed = null)
    {
        if (item.Tree is not { } tree)
        {
            // The folder here is a tree git lists, on the branch its agent made; it is not this branch's, so nothing of
            // it is read as unreadable, and it stays untouched.
            if (item.TreeOn is not null) return null;
            var folder = context.TreeAt(item.Workspace, item.Repository, item.Branch[TreeFollows.SessionPrefix.Length..]);
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
            (nested, links) = await NestedRepositoriesAsync(tree, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (TidyKept.Unread, $"its tree's folders could not be read: {error.Message}", TidyCause.Folders);
        }

        // A repository under an ignored path (a test's scratch under `_fixtures/`) is ignored content like any
        // other, and goes with the folder as the tree's own scratch does; only one git would not ignore keeps the tree.
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
            // The judgement checked again by these same reads, the index's and the walk's, so the hold gains no git of its own.
            var indexed = listed.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(entry => entry.Length > 2).Select(entry => entry[2..]).ToHashSet(StringComparer.Ordinal);
            confirmed.AddRange(TreeSlims.Confirmed(judged, indexed, nested, links));
        }

        return null;
    }

    /// <summary>
    /// The look's removal of a tree: never a delete. Its folder is moved aside, in one rename on the same volume,
    /// into <see cref="SetAsideRoot"/>, then git lets go of its record of that tree alone (<c>git worktree remove</c> of a tree
    /// whose folder is gone deletes nothing but the record). A write that lands after the last look lands in the folder moved
    /// aside, so nothing is lost.
    /// </summary>
    /// <remarks>
    /// On Windows a folder whose files something holds open cannot be renamed: the tree is in use, and stays (<c>busy</c>), for
    /// a later look. Where git will not let go of the record, the folder is put back. Called with a token nothing cancels.
    /// <para>Handed the package folders the last look confirmed, it chooses the folder's name first and writes its
    /// manifest beside where it will go, <c>judged</c>, before the move, then <c>moved</c> once git let go of the tree; a move refused or a
    /// folder put back takes the manifest with it, so only a tree git no longer knows is ever slimmed (<see cref="SlimManifest"/>).</para>
    /// <para>Handed a finished tree's facts, it writes the schema-2 manifest whatever the slim found,
    /// and a manifest it cannot write keeps the tree: nothing moves, and the caller drops the tip's ref.</para>
    /// </remarks>
    internal Task<(bool Gone, string? To, string? Code, string Sentence)> MoveAsideAsync(
        string root, SweepItem item, CancellationToken ct, IReadOnlyList<string>? slim = null, FinishedTree? finished = null) =>
        MoveAsideAsync(root, item.Tree!, item.Workspace, item.Repository, ct, slim, item.Branch, finished);

    /// <inheritdoc cref="MoveAsideAsync(string, SweepItem, CancellationToken, IReadOnlyList{string}, FinishedTree)"/>
    internal async Task<(bool Gone, string? To, string? Code, string Sentence)> MoveAsideAsync(
        string root, string treePath, string workspace, string repository, CancellationToken ct, IReadOnlyList<string>? slim = null, string branch = "",
        FinishedTree? finished = null)
    {
        var tree = Path.TrimEndingDirectorySeparator(Path.GetFullPath(treePath));
        string? to = null;
        string? manifest = null;
        var shares = TreeFilesRecord.Read(context.Home, tree)?.Shares ?? [];
        if (Directory.Exists(tree))
        {
            var named = AsideName(tree, workspace, repository);
            if (finished is not null)
            {
                manifest = Judged(named, repository, branch, slim ?? [], finished);
                if (manifest is null)
                {
                    return (false, null, TidyKept.Unread,
                        "Daoris could not write the manifest that keeps its record's facts beside where its folder would go, so it is kept");
                }
            }
            else if (slim is { Count: > 0 })
            {
                manifest = Judged(named, repository, branch, slim);
            }

            SetAsideFaults.Current?.BeforeMove?.Invoke(tree);
            var moved = MoveFolderAside(tree, workspace, repository, "its tree", "its folder", named);
            if (moved.Code is { } kept)
            {
                Forget(manifest);
                return (false, null, kept, moved.Sentence);
            }

            to = moved.To;

            // 🔴 What the tree holds read again in the moved folder, while git still knows it, so a change
            // made between the judgement's read and the move (a tracked file deleted at the top, which leaves no later time on anything that
            // stays) keeps the tree: put back, as git's refusal below puts it back.
            if (finished is not null && await UncommittedAsync(to!, ct).ConfigureAwait(false) is { } dirty)
            {
                return PutBackWithShares(to!, tree, manifest, TidyKept.Refused, dirty, shares);
            }

            // A set-aside folder holding a share's link would let a hand's recursive delete of it reach the shared place, so the links come
            // off once the tree is known to go, in the moved folder, and a tree put back has them again.
            if (TreeShares.Unlink(context.Home, tree, to) is { } linked) return PutBackWithShares(to!, tree, manifest, TidyKept.Busy, linked, shares);
        }

        var (code, _, err) = SetAsideFaults.Current?.RefuseLetGo == true
            ? (1, "", "refused by the test's fault")
            : await WorkingTree.GitAsync(root, ["worktree", "remove", tree], ct).ConfigureAwait(false);
        if (code == 0)
        {
            MarkMoved(manifest);
            TreeFilesRecord.Forget(context.Home, tree);
            if (SetAsideFaults.Current?.StopAfterMove == true) throw new SetAsideStop();
            return (true, to, null, "");
        }

        var why = FirstLine(err);
        if (to is null)
        {
            Forget(manifest);
            return (false, null, TidyKept.Refused, $"git would not let go of its tree: {why}");
        }

        return PutBackWithShares(to, tree, manifest, TidyKept.Refused, $"git would not let go of its tree ({why})", shares);
    }

    private (bool Gone, string? To, string? Code, string Sentence) PutBackWithShares(
        string to, string tree, string? manifest, string code, string why, IReadOnlyList<TreeShare> shares)
    {
        var put = PutBack(to, tree, manifest, code, why);
        if (put.To is null) TreeShares.Relink(context.Home, tree, shares);
        return put;
    }

    /// <summary>
    /// A folder under the trees home moved aside, in one rename on the same volume, into <see cref="SetAsideRoot"/> under its
    /// workspace and repository, named for the moment it moved (a folder that is no tree): where it went,
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
    /// Where a folder moved aside now goes: <see cref="SetAsideRoot"/>, its workspace and repository, its name and this moment, in UTC.
    /// Chosen before the move, so a manifest can be written beside it first.
    /// </summary>
    internal string AsideName(string folder, string workspace, string repository) =>
        Path.Combine(SetAsideRoot, workspace, repository,
            $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))}-{DateTimeOffset.UtcNow.ToString(SetAsideStamp, CultureInfo.InvariantCulture)}");

    /// <summary>
    /// The manifest of a folder about to move, written <c>judged</c> beside where it goes; its path, or null where it
    /// is not written, so the folder moves whole. Never written where that folder is reached through a link, which the move
    /// refuses too.
    /// </summary>
    private string? Judged(string to, string repository, string branch, IReadOnlyList<string> slim, FinishedTree? finished = null)
    {
        try
        {
            var beside = Path.GetDirectoryName(to)!;
            Directory.CreateDirectory(beside);
            if (context.Worktrees.LinkBelowTreesRoot(beside) is not null) return null;
            var path = SlimManifest.PathFor(to);
            new SlimManifest(SlimStates.Judged, DateTimeOffset.UtcNow, null, repository, branch, [.. slim.Select(each => new SlimPath(each, SlimStates.Pending))])
                {
                    Finished = finished,
                }
                .Write(path);
            return path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The resume: a branch with no tree deleted by the look, whose folder is moved aside and stopped or refused, marks the manifest
    /// of that branch and tip <c>deleted</c>, at every place of the trees entry, so the folder reaches its record at the next pass.
    /// </summary>
    internal void MarkDeletedOf(string workspace, string repository, string branch, string tip, DateTimeOffset at)
    {
        foreach (var root in SetAsideRoots)
        {
            var folder = Path.Combine(root, workspace, repository);
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*" + SlimManifest.Suffix))
            {
                if (SlimManifest.Read(file).Manifest is { Finished: { } finished } manifest
                    && string.Equals(manifest.Branch, branch, StringComparison.Ordinal) && string.Equals(finished.Tip, tip, StringComparison.OrdinalIgnoreCase))
                {
                    Mark(file, SlimStates.Deleted, at);
                }
            }
        }
    }

    internal static void Mark(string path, string state, DateTimeOffset at) => SetAsideMarks.Mark(path, state, at);

    internal static void TryDelete(string path) => SetAsideMarks.TryDelete(path);

    internal static DateTimeOffset? MovedAt(string name) => SetAsideMarks.MovedAt(name);

    /// <summary>
    /// The folders the look moved aside that have waited <paramref name="after"/>: each deleted where nothing in it
    /// is written after it moved, and kept, saying why, where something is, or where it holds a link outside a build folder,
    /// is reached through one, or cannot be read.
    /// </summary>
    /// <remarks>
    /// <para>A path past Windows' 260 characters, as a tree's own <c>node_modules</c> holds one, is read and deleted as any other
    /// (proven over a deep tree): .NET hands Windows such a path in its extended form, as git's <c>core.longpaths</c>
    /// does for the tree's own record.</para>
    ///
    /// <para><b>Links</b>: a link under a build folder (<see cref="TreeScratch.UnderBuildFolder"/>), as
    /// npm's workspaces and pnpm make under <c>node_modules</c>, goes with its folder as a link, its own entry alone; what it points
    /// at stays wherever it is. Nothing here ever reads, clears or deletes through a link (<see cref="DeleteNeverFollowing"/>).</para>
    /// </remarks>
    /// <param name="now">What time it is, against the moment in each folder's name.</param>
    internal IReadOnlyList<SetAside> PurgeSetAside(DateTimeOffset now, TimeSpan after) => PurgeSetAside(now, after, () => false, out _);

    /// <summary>
    /// <inheritdoc cref="PurgeSetAside(DateTimeOffset, TimeSpan)"/> Within the Keeping pass's budget:
    /// once <paramref name="spent"/> says so, between folders and never inside one's delete, it stops, and the next pass takes up the
    /// rest; a folder whose delete failed for the same reason as at its last try waits a day. A folder's manifest goes with it. With no
    /// registry to run a holding check in, it takes no recorded folder by the hour (<see cref="PurgeSetAsideAsync"/>).
    /// </summary>
    /// <param name="only">The folders a press listed: only those are met, each judged again as the purge
    /// judges every folder; null meets every folder past its days. A folder not met keeps what <c>purge-failed.json</c> says of it.</param>
    internal IReadOnlyList<SetAside> PurgeSetAside(
        DateTimeOffset now, TimeSpan after, Func<bool> spent, out bool stopped, Func<string, bool>? only = null, bool whole = false)
    {
        // With no registry no step awaits git, so this completes as it is called.
        var (purged, ended) = PurgeSetAsideAsync(now, after, whole, spent, only, null, CancellationToken.None).GetAwaiter().GetResult();
        stopped = ended;
        return purged;
    }

    /// <summary>
    /// One purge for the hour and the days, each folder's moment and wait a function of its class
    /// (<see cref="SetAsideEligibility"/>): a recorded folder an hour after its record, under no <c>whole</c>; one awaiting its record, or whose
    /// manifest does not read, never; any other its days after the moment in its name. A finished folder's threshold for a write is its
    /// manifest's <c>read</c>, and its own top is read too. Each folder that goes is renamed into <c>.going</c> first, with a record of its
    /// removal (<see cref="GoingRecord"/>).
    /// <para>🔴 A recorded folder goes by the hour only on a holding check run right before its rename, in the root
    /// <paramref name="rootOf"/> gives: a check that fails for certain makes it <c>unmarked</c>, one
    /// that cannot run or read keeps it as it is, and with no <paramref name="rootOf"/> none is taken by the hour.</para>
    /// </summary>
    internal async Task<(IReadOnlyList<SetAside> Purged, bool Stopped)> PurgeSetAsideAsync(
        DateTimeOffset now, TimeSpan days, bool whole, Func<bool> spent, Func<string, bool>? only, Func<string, string, string?>? rootOf,
        CancellationToken ct)
    {
        var state = new PurgeState();
        var results = new List<SetAside>();
        // Every place of the trees entry, each with its own record of what it could not delete.
        foreach (var root in SetAsideRoots)
        {
            if (!Directory.Exists(root)) continue;
            // What a stop left in `.going` first, within the budget.
            await ResumeGoingAsync(root, rootOf, spent, state, results, ct).ConfigureAwait(false);
            await PurgeAtAsync(root, now, days, whole, spent, state, only, rootOf, results, ct).ConfigureAwait(false);
        }

        return (results, state.Stopped);
    }

    private sealed class PurgeState
    {
        public bool Stopped;
    }

    /// <summary>The purge of the folders under one place's <c>.tidied</c> (<paramref name="root"/>), into <paramref name="results"/>.</summary>
    private async Task PurgeAtAsync(
        string root, DateTimeOffset now, TimeSpan after, bool whole, Func<bool> spent, PurgeState state, Func<string, bool>? only,
        Func<string, string, string?>? rootOf, List<SetAside> results, CancellationToken ct)
    {
        var before = FailedEntries(root);
        var failed = new List<(string Folder, string Why, DateTimeOffset? Tried)>();
        foreach (var workspace in SetAsideEligibility.Workspaces(root))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var folder in Directory.EnumerateDirectories(repository).ToList())
        {
            if (MovedAt(Path.GetFileName(folder)) is not { } moved) continue;
            var judged = SetAsideEligibility.Of(folder, moved, after, whole);
            if (judged.Due is not { } due || now < due) continue;
            var last = before.GetValueOrDefault(Path.GetFullPath(folder));
            if (only is not null && !only(folder))
            {
                // Not this press's: what the last pass kept of it stays kept, as the pass said.
                if (last.Why is { } why) failed.Add((folder, why, last.Tried));
                continue;
            }

            if (last.Tried is { } tried && now - tried < SetAsideKeeping.Again)
            {
                // Failed the same way twice: kept as the last pass said, and tried again the next day.
                failed.Add((folder, last.Why!, tried));
                results.Add(new SetAside(folder, moved, false, last.Why, SetAsideKept.Unread) { Class = judged.Class, Hour = judged.Hour });
                continue;
            }

            if (state.Stopped || spent())
            {
                state.Stopped = true;
                if (last.Why is { } why) failed.Add((folder, why, last.Tried));
                continue;
            }

            var kept = Kept(folder, judged.Threshold, top: judged.Manifest?.Finished is not null);
            if (kept is null && judged.Hour)
            {
                kept = await HeldNowAsync(folder, judged.Manifest!, Path.GetFileName(workspace), rootOf, ct).ConfigureAwait(false);
            }

            long bytes = 0;
            if (kept is null)
            {
                (bytes, kept) = Remove(folder, judged.Hour ? GoingDoors.Hour : GoingDoors.Days, judged.Threshold, now, top: judged.Manifest?.Finished is not null);
                // A refused rename (`busy`) or a failed delete is kept where the inventory reads it, waiting a day after the same failure twice.
                // A write its read under `.going` found is said as the purge's own read says one, never a failure.
                if (kept is { } refused && refused.Code is not (SetAsideKept.Linked or SetAsideKept.Written)) failed.Add((folder, refused.Sentence, last.Why == refused.Sentence ? now : null));
            }

            results.Add(new SetAside(folder, moved, kept is null, kept?.Sentence, kept?.Code) { Bytes = bytes, Class = judged.Class, Hour = judged.Hour });
        }

        KeepFailed(root, failed);
    }

    /// <summary>
    /// A set-aside folder removed: renamed first into <c>.tidied/.going/</c>, so nothing is half deleted
    /// under the name a person opened, then deleted never following a link, and its manifest with it. A refused rename keeps it whole,
    /// <c>busy</c>.
    /// <para>🔴 Where it would go is reached through no link: a link on the way into <c>.going</c>, asked
    /// before the folders are made and again after, keeps the folder where it is, <c>linked</c>, as <see cref="MoveFolderAside"/> keeps a move.</para>
    /// <para>A record of the removal goes beside it in <c>.going</c> before the rename (its door, when, the
    /// threshold, and whether its delete began), and its manifest stays where it is until the delete ends, so a stop at any moment is
    /// resumed by <see cref="ResumeGoingAsync"/> with the folder's identity whole.</para>
    /// </summary>
    /// <param name="door">Which door removes it: <see cref="GoingDoors"/>.</param>
    /// <param name="top">A finished tree's folder, whose own top is read too (<see cref="Kept"/>).</param>
    internal (long Bytes, (string Code, string Sentence)? Kept) Remove(
        string folder, string door, DateTimeOffset threshold, DateTimeOffset now, bool top = false)
    {
        var repository = Path.GetDirectoryName(folder)!;
        var workspace = Path.GetDirectoryName(repository)!;
        var root = Path.GetDirectoryName(workspace)!;
        var going = Path.Combine(root, SetAsideEligibility.Going, Path.GetFileName(workspace), Path.GetFileName(repository), Path.GetFileName(folder));
        var into = Path.GetDirectoryName(going)!;
        var record = GoingRecord.PathFor(going);
        try
        {
            if (context.Worktrees.LinkBelowTreesRoot(into) is { } before) return (0, Linked(before));
            Directory.CreateDirectory(into);
            if (context.Worktrees.LinkBelowTreesRoot(into) is { } after) return (0, Linked(after));
            new GoingRecord(door, now, threshold, Started: false).Write(record);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (0, (SetAsideKept.Unread, $"it could not be read or deleted ({error.Message.Trim()})"));
        }

        try
        {
            SetAsideFaults.Current?.BeforeRename?.Invoke(folder);
            Directory.Move(folder, going);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            TryDelete(record);
            return (0, (TidyKept.Busy, $"it could not be read or deleted: something on this machine is using it, so it was kept whole ({error.Message.Trim()})"));
        }

        if (SetAsideFaults.Current?.StopAfterRename == true) throw new SetAsideStop();
        try
        {
            // The rules' doors read the folder again under `.going`, where nothing writes by its old name, so a
            // write between the purge's read and the rename puts it back whole; the press removes what its list disclosed and is chosen.
            if (door != GoingDoors.Press
                && SetAsideKeeps.WrittenSince(going, threshold, SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest, top) is { } late)
            {
                Directory.Move(going, folder);
                TryDelete(record);
                return (0, late);
            }

            new GoingRecord(door, now, threshold, Started: true).Write(record);
            var bytes = DeleteNeverFollowing(going);
            TryDelete(SlimManifest.PathFor(folder));
            TryDelete(record);
            return (bytes, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (0, (SetAsideKept.Unread, $"it could not be read or deleted ({error.Message.Trim()})"));
        }

        static (string, string) Linked(string link) =>
            (SetAsideKept.Linked, $"the folder it would be moved into to be removed is reached through a link, at {link}, so it is kept");
    }

    /// <summary>
    /// What a stop left under one place's <c>.tidied/.going</c>, each folder within the budget: one whose
    /// delete never began, or whose record does not read, goes back to its place whole; one whose delete began finishes only where no file
    /// in it is written since its removal began and, for the hour's door, its holding check passes now; else what remains goes back, the
    /// result says why, and a finished folder's manifest ends <c>unmarked</c> with that why. 🔴 A folder
    /// reached through a link on the way, <c>.going</c> or any level below it included, is never touched.
    /// </summary>
    private async Task ResumeGoingAsync(
        string root, Func<string, string, string?>? rootOf, Func<bool> spent, PurgeState state, List<SetAside> results, CancellationToken ct)
    {
        var going = Path.Combine(root, SetAsideEligibility.Going);
        if (!Directory.Exists(going)) return;
        List<string> folders;
        try
        {
            folders = [.. Directory.EnumerateDirectories(going).SelectMany(each => Directory.EnumerateDirectories(each)).SelectMany(each => Directory.EnumerateDirectories(each))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var folder in folders)
        {
            if (state.Stopped || spent())
            {
                state.Stopped = true;
                return;
            }

            if (context.Worktrees.LinkBelowTreesRoot(folder) is not null) continue;
            var name = Path.GetFileName(folder);
            var repository = Path.GetFileName(Path.GetDirectoryName(folder)!);
            var workspace = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(folder)!)!);
            var place = Path.Combine(root, workspace, repository, name);
            var moved = MovedAt(name) ?? DateTimeOffset.MinValue;
            var path = GoingRecord.PathFor(folder);
            var record = GoingRecord.Read(path);
            string? back;
            try
            {
                // A folder whose remains cannot be read is put back, never a purge that throws.
                back = record is null ? "its removal's record does not read, so it was put back"
                    : !record.Started ? "its removal stopped before its delete began, so it was put back whole"
                    : SetAsideKeeps.FileWrittenSince(folder, record.At) is { } late ? $"something in it was written after its removal began ({late}), so what remains was put back"
                    : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                back = $"what remains of it could not be read ({error.Message.Trim()}), so it was put back";
            }

            if (back is null && record!.Door == GoingDoors.Hour)
            {
                var manifest = SlimManifest.Read(SlimManifest.PathFor(place)).Manifest;
                back = manifest is null ? "its manifest does not read, so what remains was put back"
                    : await HeldNowAsync(place, manifest, workspace, rootOf, ct).ConfigureAwait(false) is { } unheld ? $"{unheld.Sentence}; what remains was put back"
                    : null;
            }

            if (back is not null)
            {
                try
                {
                    if (Directory.Exists(place)) throw new IOException($"{place} is there again");
                    Directory.Move(folder, place);
                    TryDelete(path);
                    // A finished folder whose delete began is no longer what is recorded, so whatever put it back,
                    // a late write or a check that did not pass or could not run, it ends unmarked, saying why, and waits its days.
                    if (record is { Started: true }) Unmark(place, back);
                    results.Add(new SetAside(place, moved, false, back, SetAsideKept.Returned));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    results.Add(new SetAside(place, moved, false, $"{back}, but it could not be ({error.Message.Trim()})", SetAsideKept.Unread));
                }

                continue;
            }

            try
            {
                var bytes = DeleteNeverFollowing(folder);
                TryDelete(SlimManifest.PathFor(place));
                TryDelete(path);
                results.Add(new SetAside(place, moved, true, null) { Bytes = bytes, Hour = record!.Door == GoingDoors.Hour });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Left for the next pass, its record still saying its delete began.
            }
        }
    }

    private readonly SetAsideKeeps keeps = new(context);

    internal (string Code, string Sentence)? Kept(string folder, DateTimeOffset moved, bool top = false) => keeps.Kept(folder, moved, top);

    internal static long DeleteNeverFollowing(string folder) => SetAsideDeleting.DeleteNeverFollowing(folder);

    /// <summary>
    /// Where the purge keeps the folders whose delete it could not finish (re-check), beside them: a delete refused after the
    /// read found nothing late (a file held open against it) shows in no read the inventory can repeat, so it is kept here, and the
    /// inventory reads it (<see cref="Failed"/>). Each pass writes what it met, so a folder a later pass deletes leaves it.
    /// </summary>
    internal string FailedPath => FailedPathOf(SetAsideRoot);

    /// <summary>
    /// The folders the last purge could not delete, by full path, each with the purge's reason (<see cref="FailedPath"/>); none where
    /// nothing is kept or it does not read.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Failed() =>
        SetAsideRoots.SelectMany(FailedEntries)
            .ToDictionary(each => each.Key, each => each.Value.Why!, StringComparer.OrdinalIgnoreCase);
}

