using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The set-aside kind's one removal (KEEP1a, D195 points 1 and 3; the cleanup design §2.1, §3.1 and §5): the purge at the policy's days,
/// then the slims due, within the pass's budget. The look's Keeping pass calls it holding the home's run lock (<see cref="KeepingRun"/>);
/// a press's run and a terminal's call it the same way (KEEP1e, <see cref="SpaceFree"/>), so the three share one set of guards. Outside every repository's
/// hold: it touches no git and no live tree.
/// </summary>
/// <remarks>
/// <para><b>The slim</b>, of each manifest at <see cref="SlimStates.Moved"/> whose folder moved an hour or more ago, where the policy is
/// not <c>whole</c>: (1) nothing written in the folder since the move, by the purge's own read, which passes over what the slim removed;
/// a write keeps it whole for its days, each path <c>kept</c>, <c>written</c>; (2) each path checked again right before it goes: its
/// words (<see cref="TreeSlims.Refusal"/>), lying under the folder by whole segments, no link on the way from the trees home to it, it
/// included, and still a folder; (3) removed never following a link (<see cref="TreeSetAside.DeleteNeverFollowing"/>), written
/// <c>removing</c> before and <c>removed</c> with when it ended after. A stop midway leaves it <c>removing</c>, taken up from (1).</para>
/// <para><b>Bounded</b> (Codex 3.2): removals stop once the budget is spent or a stop is asked, between paths and never inside one; a path
/// whose removal failed for the same reason as at its last try waits a day.</para>
/// </remarks>
internal sealed class SetAsideKeeping(TreeContext context, TreeSetAside setAside)
{
    /// <summary>How long a pass removes for, at most (§3.1).</summary>
    internal static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(60);

    /// <summary>How long a path whose removal failed the same way twice waits before it is tried again (§3.1).</summary>
    internal static readonly TimeSpan Again = TimeSpan.FromDays(1);

    /// <summary>One run of the set-aside kind's removal at <paramref name="now"/>, by <paramref name="policy"/>.</summary>
    /// <param name="purge">The folders past their days a press listed (KEEP1e); null purges every one.</param>
    /// <param name="slim">The folders whose slim is due a press listed (KEEP1e); null slims every one.</param>
    internal SetAsideRun Run(
        KeepPolicy policy, DateTimeOffset now, TimeSpan budget, CancellationToken ct, Func<string, bool>? purge = null, Func<string, bool>? slim = null)
    {
        var clock = Stopwatch.StartNew();
        bool Spent() => clock.Elapsed >= budget || ct.IsCancellationRequested;
        // KEEPSIZE1b (the keep-size design §3.4): one purge, the hour's and the days', each folder by its class.
        var purged = setAside.PurgeSetAside(now, policy.SetAside.Days, policy.SetAside.Whole, Spent, out var stopped, purge);
        var slims = new List<SlimResult>();
        if (!stopped && !policy.SetAside.Whole) stopped = SlimDue(now, Spent, slims, slim);
        return new(purged, slims) { Stopped = stopped };
    }

    /// <summary>
    /// What the press lists of the set-aside folders (KEEP1e, the cleanup design §3.3), read and never written: each folder with when it
    /// was moved and when its days end; whether the purge takes it now (past its days, nothing written in it since), whether its slim is
    /// due, and whether it holds files of its own (something written in it after the move, or a link in it, which the purge keeps it for).
    /// </summary>
    internal IReadOnlyList<SetAsideListed> List(KeepPolicy policy, DateTimeOffset now)
    {
        var listed = new List<SetAsideListed>();
        // KEEPSIZE1a: every place of the trees entry, the folders set aside before the home's layout moved included.
        foreach (var root in setAside.SetAsideRoots.Where(Directory.Exists))
        foreach (var workspace in SetAsideEligibility.Workspaces(root))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var folder in Directory.EnumerateDirectories(repository))
        {
            if (TreeSetAside.MovedAt(Path.GetFileName(folder)) is not { } moved) continue;
            // KEEPSIZE1b (§3.4, §5): its class, and its threshold for a write, from its manifest.
            var judged = SetAsideEligibility.Of(folder, moved, policy.SetAside.Days, policy.SetAside.Whole);
            var kept = setAside.Kept(folder, judged.Threshold);
            var past = judged.Due is { } due && now >= due;
            var slim = !past && !policy.SetAside.Whole && kept is null && SlimDueOf(folder, now);
            listed.Add(new(folder, Path.GetFileName(workspace), Path.GetFileName(repository), moved, judged.Due ?? moved + policy.SetAside.Days, past, slim, kept?.Code)
            {
                Class = judged.Class,
                Ref = judged.Manifest?.Finished?.Ref,
                Why = judged.Manifest?.Why,
            });
        }

        return listed;
    }

    /// <summary>Whether <paramref name="folder"/>'s manifest reads at <c>moved</c>, an hour after its move, with a path left to take.</summary>
    private static bool SlimDueOf(string folder, DateTimeOffset now) =>
        SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest is { Moved: { } moved } manifest && Slimmable(manifest)
        && now - moved >= KeepKinds.SetAsideSettle
        && manifest.Paths.Any(path => !path.Final && !Waits(path, now));

    /// <summary>
    /// A folder set aside removed before its date by the person's word (KEEP1e, the cleanup design §3.3), whole and with its manifest,
    /// judged again right before it goes: one reached through a link, or that cannot be read, is kept; one that holds files of its own now
    /// where the list said it held none changed since the list, and is kept. Never following a link inside it.
    /// </summary>
    /// <param name="listedOwn">Whether the list the person read said it holds files of its own.</param>
    /// <param name="listedClass">The class the list said it was (KEEPSIZE1b); one that reads another now is kept, <c>changed</c>.</param>
    internal (long Bytes, string? Kept) RemoveEarly(string folder, bool listedOwn, string? listedClass = null)
    {
        if (!Directory.Exists(folder)) return (0, SpaceKept.Gone);
        if (TreeSetAside.MovedAt(Path.GetFileName(folder)) is not { } moved) return (0, SpaceKept.Refused);
        if (context.Worktrees.LinkBelowTreesRoot(folder) is not null) return (0, SetAsideKept.Linked);
        // KEEPSIZE1b (§3.4): a finished folder's threshold is its manifest's `read`; a class changed since the list keeps it.
        var judged = SetAsideEligibility.Of(folder, moved, TimeSpan.Zero, whole: true);
        if (listedClass is not null && listedClass != judged.Class) return (0, SpaceKept.Changed);
        var kept = setAside.Kept(folder, judged.Threshold);
        if (kept is { Code: SetAsideKept.Unread }) return (0, SetAsideKept.Unread);
        if (kept is not null && !listedOwn) return (0, SpaceKept.Changed);
        // Renamed into `.going` first, then deleted (§3.4): a refused rename keeps it whole.
        var (bytes, refused) = TreeSetAside.Remove(folder);
        return refused is null ? (bytes, null) : (0, refused.Value.Code == TidyKept.Busy ? SpaceKept.InUse : SpaceKept.Failed);
    }

    /// <summary>The slims due: each manifest at <c>moved</c> an hour after its move; true where the budget or a stop ended the pass.</summary>
    private bool SlimDue(DateTimeOffset now, Func<bool> spent, List<SlimResult> slims, Func<string, bool>? only)
    {
        // KEEPSIZE1a: every place; a folder at the old place set aside before KEEP1a has no manifest, so it is never slimmed.
        foreach (var root in setAside.SetAsideRoots.Where(Directory.Exists))
        foreach (var workspace in SetAsideEligibility.Workspaces(root))
        foreach (var repository in Directory.EnumerateDirectories(workspace))
        foreach (var file in Directory.EnumerateFiles(repository, "*" + SlimManifest.Suffix).ToList())
        {
            var folder = SlimManifest.FolderOf(file)!;
            if (only is not null && !only(folder)) continue;
            if (!Directory.Exists(folder))
            {
                // Its folder went at its days, or by the person's hand: the manifest goes with it.
                Drop(file);
                continue;
            }

            // A manifest that does not read is unknown: its folder stays whole, and the purge reads it whole.
            if (SlimManifest.Read(file).Manifest is not { } manifest) continue;
            // KEEPSIZE1b (§3.5): a finished tree's manifest at `judged` holds its record's inputs, so it is never dropped; its folder stays whole.
            if (manifest.State == SlimStates.Judged && manifest.Finished is not null) continue;
            if (manifest.State == SlimStates.Judged)
            {
                // Left between the move and its `moved`: what was authorized is unknown, so the folder is kept whole for its days.
                Drop(file);
                continue;
            }

            if (!Slimmable(manifest) || manifest.Moved is not { } moved || now - moved < KeepKinds.SetAsideSettle) continue;
            if (manifest.Paths.All(path => path.Final || Waits(path, now))) continue;
            if (spent()) return true;
            var (result, stopped) = Slim(folder, file, manifest, now, spent);
            if (result is not null) slims.Add(result);
            if (stopped) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the slim may take a manifest's paths: a schema-1 one at <c>moved</c>, as built; a finished tree's at any step after its move,
    /// since its folder waits its days or its record and the slim stays for those (the keep-size design §3.6).
    /// </summary>
    private static bool Slimmable(SlimManifest manifest) =>
        manifest.Finished is null ? manifest.State == SlimStates.Moved
            : manifest.State is SlimStates.Moved or SlimStates.Refused or SlimStates.Deleted or SlimStates.Recorded or SlimStates.Unmarked;

    private static bool Waits(SlimPath path, DateTimeOffset now) => path.Tried is { } tried && now - tried < Again;

    /// <summary>One folder's slim, steps 1 to 3; what it did, and whether the budget or a stop ended it.</summary>
    private (SlimResult? Result, bool Stopped) Slim(string folder, string file, SlimManifest manifest, DateTimeOffset now, Func<bool> spent)
    {
        if (TreeSetAside.MovedAt(Path.GetFileName(folder)) is not { } named) return (null, false);
        var paths = manifest.Paths.ToList();
        var removed = new List<string>();
        var kept = new List<SlimPath>();
        var failed = new List<SlimPath>();
        long bytes = 0;

        // Step 1: nothing written in the folder since the move, by the purge's own read, which knows the manifest.
        if (setAside.Kept(folder, named) is { } written)
        {
            if (written.Code == SetAsideKept.Unread) return (new(folder, [], 0, [], [], Ended: false) { Unread = written.Sentence }, false);
            // A path a stopped slim left `removing` is kept too: the folder waits its days whole, read as the purge reads it.
            var why = written.Code == SetAsideKept.Linked ? SlimKept.Linked : SlimKept.Written;
            for (var at = 0; at < paths.Count; at++)
            {
                if (paths[at].Final) continue;
                paths[at] = paths[at] with { State = SlimStates.Kept, Why = why };
                kept.Add(paths[at]);
            }

            if (!Save(file, manifest with { Paths = paths })) return (null, false);
            return (new(folder, [], 0, kept, [], paths.All(path => path.Final)) { Said = written.Sentence }, false);
        }

        var stopped = false;
        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var same = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var at = 0; at < paths.Count; at++)
        {
            var path = paths[at];
            if (path.Final || Waits(path, now)) continue;
            if (spent())
            {
                stopped = true;
                break;
            }

            // Step 2: its words, whole segments under the folder, no link on the way, still a folder.
            var full = Path.GetFullPath(Path.Combine(top, path.Path.Replace('/', Path.DirectorySeparatorChar)));
            string? keep = TreeSlims.Refusal(path.Path) is not null || !full.StartsWith(top + Path.DirectorySeparatorChar, same)
                ? SlimKept.Refused
                : context.Worktrees.LinkBelowTreesRoot(full) is not null ? SlimKept.Linked
                : !Directory.Exists(full) ? SlimKept.Gone
                : null;
            if (keep is not null)
            {
                // A path whose removal began and is gone now went by this slim, when unknown: the purge reads its folder above against the move.
                paths[at] = keep == SlimKept.Gone && path.State == SlimStates.Removing
                    ? path with { State = SlimStates.Removed, Why = null, Ended = null }
                    : path with { State = SlimStates.Kept, Why = keep };
                if (paths[at].State == SlimStates.Kept) kept.Add(paths[at]);
                if (!Save(file, manifest with { Paths = paths })) return (null, false);
                continue;
            }

            // Step 3: written `removing` before, so a stop midway is taken up and the purge passes over it; never following a link.
            paths[at] = path with { State = SlimStates.Removing };
            if (!Save(file, manifest with { Paths = paths })) return (null, false);
            try
            {
                bytes += TreeSetAside.DeleteNeverFollowing(full);
                paths[at] = paths[at] with { State = SlimStates.Removed, Why = null, Tried = null, Ended = DateTimeOffset.UtcNow };
                removed.Add(path.Path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // What it removed before the failure stays removed; the path stays `removing` and is tried again, a day later where it
                // failed as it did last time.
                var why = error.Message.Trim();
                paths[at] = paths[at] with { Why = why, Tried = path.Why == why ? now : null };
                failed.Add(paths[at]);
            }

            if (!Save(file, manifest with { Paths = paths })) return (null, false);
        }

        return (new(folder, removed, bytes, kept, failed, paths.All(each => each.Final)), stopped);
    }

    /// <summary>The manifest written; false where it could not be, so nothing more is removed of its folder this pass.</summary>
    private static bool Save(string file, SlimManifest manifest)
    {
        try
        {
            manifest.Write(file);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Drop(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left, it is read again at the next pass, and judged the same way.
        }
    }
}

/// <summary>A folder set aside as the press lists it (<see cref="SetAsideKeeping.List"/>).</summary>
/// <param name="Goes">When its days end, and the purge takes it.</param>
/// <param name="Past">Its days have ended.</param>
/// <param name="Slim">Its slim is due: an hour after its move, a package folder left to take.</param>
/// <param name="Kept">Why the purge keeps it, <see cref="SetAsideKept"/>'s code; null where nothing in it keeps it.</param>
internal sealed record SetAsideListed(
    string Folder, string Workspace, string Repository, DateTimeOffset Moved, DateTimeOffset Goes, bool Past, bool Slim, string? Kept)
{
    /// <summary>Its class (KEEPSIZE1b, <see cref="SetAsideClass"/>), which the press lists and checks again before it removes.</summary>
    public string Class { get; init; } = SetAsideClass.Unmarked;

    /// <summary>A finished tree's recovery ref at its tip, where one holds it; null where the line does, or none is known.</summary>
    public string? Ref { get; init; }

    /// <summary>Why it waits, as its manifest says: the last post's or check's words.</summary>
    public string? Why { get; init; }

    /// <summary>It holds files of its own: something written in it after its move, or a link in it.</summary>
    public bool Own => Kept is SetAsideKept.Written or SetAsideKept.Linked;

    /// <summary>Its id in a press: <c>&lt;workspace&gt;/&lt;repository&gt;/&lt;folder&gt;</c>, as the card's walk names it.</summary>
    public string Id => $"{Workspace}/{Repository}/{Path.GetFileName(Folder)}";
}

/// <summary>One run of the set-aside kind's removal: the folders the purge met and the slims it made.</summary>
/// <param name="Purged">Each folder past its days, deleted or kept and why.</param>
/// <param name="Slims">Each folder whose slim did something this pass.</param>
internal sealed record SetAsideRun(IReadOnlyList<SetAside> Purged, IReadOnlyList<SlimResult> Slims)
{
    /// <summary>The budget was spent, or a stop asked, before everything due was done: the next pass takes up the rest.</summary>
    public bool Stopped { get; init; }
}

/// <summary>What one pass's slim did of one folder set aside (the cleanup design §5).</summary>
/// <param name="Removed">The package folders it removed, in git's spelling.</param>
/// <param name="Bytes">The bytes they held.</param>
/// <param name="Kept">The paths it kept for the folder's days, each with why.</param>
/// <param name="Failed">The paths whose removal failed, each with git's or the system's words; tried again later.</param>
/// <param name="Ended">Every path of the manifest is now removed or kept.</param>
internal sealed record SlimResult(
    string Folder, IReadOnlyList<string> Removed, long Bytes, IReadOnlyList<SlimPath> Kept, IReadOnlyList<SlimPath> Failed, bool Ended)
{
    /// <summary>Step 1 kept it whole: the purge's clause saying why.</summary>
    public string? Said { get; init; }

    /// <summary>Its folder could not be read: why, in a clause; nothing of it was touched.</summary>
    public string? Unread { get; init; }
}
