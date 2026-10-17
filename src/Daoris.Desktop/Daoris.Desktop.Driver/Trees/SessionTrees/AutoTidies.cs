using System.Text.Json;
using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// What the look's own tidy did in one repository (AUTOTIDY1, AUTOTIDY2a): each session branch judged, removed or kept, or why
/// none was judged this look.
/// </summary>
/// <param name="Results">
/// Each session branch judged: those the look would not take, kept as judged; then what the clean-up's press path did with each
/// it would, in its own words (<see cref="SessionTrees.SweepAsync"/>).
/// </param>
/// <param name="Held">Why nothing was judged in the repository this look (<see cref="TidyKept"/>); null where the press's path ran.</param>
/// <param name="Detail">git's or the service's own words beside <paramref name="Held"/>.</param>
public sealed record TidyPass(IReadOnlyList<SweepResult> Results, string? Held = null, string? Detail = null)
{
    /// <summary>The repository looked at, as the registry names it.</summary>
    public string Repository { get; init; } = "";

    /// <summary>Its workspace, as <see cref="RemoteTarget.Workspace"/> reads the registry's row.</summary>
    public string Workspace { get; init; } = "";

    /// <summary>Which <see cref="TidyKept.Unread"/> held it (<see cref="TidyCause"/>); null for any other reason.</summary>
    public string? Cause { get; init; }

    /// <summary>
    /// What the clean-up's press path did with each branch a landing made that the look took (AUTOTIDY2b, D102's group), after
    /// the session branches; those it would not take are the press's, and are not listed.
    /// </summary>
    public IReadOnlyList<LandedResult> Landed { get; init; } = [];

    /// <summary>
    /// What the look did with the folders under the repository's trees that are no tree of its own (AUTOTIDY2c, OPENCUT1), before
    /// its session branches: each moved aside, or kept and why (<see cref="TreeStrays"/>).
    /// </summary>
    public IReadOnlyList<StrayResult> Strays { get; init; } = [];

    /// <summary>The recovery refs the look removed past their retention, each with the branch or tag that holds its commit (CLEANUP1).</summary>
    public IReadOnlyList<RecoveryGone> Recovered { get; init; } = [];
}

/// <summary>
/// Why the look's own tidy left a session branch, or a repository's, where it would have gone (AUTOTIDY1): the machine log's
/// <c>branch.kept</c> codes. What it holds (uncommitted work, commits no branch of the person's holds, in use) is no code: it is
/// the press's.
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

/// <summary>
/// Which <see cref="TidyKept.Unread"/> it was (AUTOTIDY1a2): the machine log's <c>branch.kept</c> <c>cause</c>, a code so the page can
/// word why without the sentence, whose path stays the console's (D47 §4). Other reasons carry none.
/// </summary>
public static class TidyCause
{
    /// <summary>git could not say where the checkout's top is.</summary>
    public const string Toplevel = "toplevel";

    /// <summary>git could not list the session branches.</summary>
    public const string Refs = "refs";

    /// <summary>git could not list the repository's working trees.</summary>
    public const string List = "list";

    /// <summary>A branch's folder here, with no tree git lists for it.</summary>
    public const string Folder = "folder";

    /// <summary>git could not read a tree's index.</summary>
    public const string Index = "index";

    /// <summary>A tree's folders could not be read.</summary>
    public const string Folders = "folders";

    /// <summary>A whole repository's pass threw (AUTOTIDY1a3); the exception is neither logged nor typed, its message may hold a path.</summary>
    public const string Pass = "pass";

    /// <summary>The judgement could not read an item: git gave no answer for its tree's contents, its tip or its unlanded log (AUTOTIDY1a3).</summary>
    public const string Judged = "judged";
}

/// <summary>
/// The look's own tidy of one repository (AUTOTIDY1; PART10l-ii, out of <c>SessionTrees.Auto.cs</c>): the pass that judges the
/// repository's session branches and the branches its landings made, holds it, and hands what may go to the clean-up's press path.
/// Over the trees' context, the follow's ledger, the replay's record tidying, the sweep, which takes the look's guards and
/// set-aside itself, the set-aside's judgement of what goes (<see cref="TreeSetAside"/>), and the landed group's half of the
/// clean-up (<see cref="LandedCleanups"/>, AUTOTIDY2b).
/// </summary>
internal sealed class AutoTidies(
    TreeContext context, TreeFollows follows, BranchReplays replays, TreeSweeps sweeps, TreeSetAside setAside, LandedCleanups cleanups,
    EndKeeping ends)
{
    /// <summary>
    /// The look's own tidy of one repository (AUTOTIDY1, D88's note; AUTOTIDY2a; FABRIC-END4a): each session branch whose kept
    /// <see cref="HeldFact"/> holds at its tip, judged as the press's list judges it against the line read here with no fetch,
    /// and those <see cref="TreeSetAside.GoesByItself"/> takes removed
    /// by the clean-up's own press (<see cref="TreeSweeps"/>), judged again right before each goes, and deleted only while it is
    /// the commit judged. Then the branches the repository's landings made and recorded (AUTOTIDY2b, D102's group), each the
    /// press would take that no session branch the look leaves leans on, by the press's own half
    /// (<see cref="LandedCleanups.CleanLandedAsync"/>): their proof is the line's, or a completed pull request's kept answer.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Held alone, the sessions asked inside the hold</b>: a fresh tree a start just opened is an empty branch with
    /// a clean tree until its record opens, so the repository's trees are held as bringing them up to date holds them
    /// (<see cref="TreeLock"/>), and a start that holds them leaves every branch for another look.</para>
    ///
    /// <para><b>Judged before the hold</b> (AUTOTIDY2a): every held candidate is judged first, the sessions asked for it too so
    /// no tree a session works in is read, and the hold is taken only where one may go. A start waits on it only while those few
    /// are judged again and removed, and a look with nothing to take never holds the repository.</para>
    ///
    /// <para><b>The checkout must be a repository of its own</b>: git walks up from a folder that is not one, and would answer
    /// for the repository above it (FIX-LOG).</para>
    /// </remarks>
    /// <param name="inUse">The trees sessions still running or waiting name, asked once the repository is held.</param>
    internal async Task<TidyPass> TidyEmptyAsync(
        string root, string repository, string? workspace, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse,
        CancellationToken ct = default)
    {
        // FABRIC-END4a: the pace's pass and link 2's tries tidy one repository at a time in a process, so neither meets the other's
        // hold and says a start held it.
        var alone = Tidying.GetOrAdd($"{Path.GetFullPath(context.Home)}|{repository}", _ => new SemaphoreSlim(1, 1));
        await alone.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await TidyAloneAsync(root, repository, workspace, inUse, ct).ConfigureAwait(false);
        }
        finally
        {
            alone.Release();
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Tidying = new(StringComparer.OrdinalIgnoreCase);

    // CLEANUP1: when each repository's recovery refs were last held to their retention (home|repository). In memory: a restart asks once.
    // Retention counts in days, so a pace's pass and link 2's tries need not ask git again within one.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> Retained = new(StringComparer.OrdinalIgnoreCase);

    private bool RetentionDue(string repository)
    {
        var key = $"{Path.GetFullPath(context.Home)}|{repository}";
        var now = context.Clock();
        if (Retained.TryGetValue(key, out var last) && now - last < TimeSpan.FromDays(1)) return false;
        Retained[key] = now;
        return true;
    }

    // FABRIC-END4a review: each recorded branch the keeper could not prove and the press's judgement kept, at the tip and line commit it
    // was judged at, and when (home|repository|branch). In memory: a restart judges each once again. 🔴 Never for good (second review): a
    // busy, dirty or checked-out tree, or a landing checked out inside another, can clear with neither commit moving, so an entry older than
    // the `refs` deadline is judged again.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Tip, string? Line, DateTimeOffset At)> Judged = new(StringComparer.OrdinalIgnoreCase);

    private async Task<TidyPass> TidyAloneAsync(
        string root, string repository, string? workspace, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse, CancellationToken ct)
    {
        var space = RemoteTarget.Workspace(workspace);
        IReadOnlyList<RecoveryGone> recovered = [];
        TidyPass Pass(IReadOnlyList<SweepResult> results, string? held = null, string? detail = null, string? cause = null) =>
            new(results, held, detail) { Repository = repository, Workspace = space, Cause = cause, Recovered = recovered };

        // A main checkout's `.git` is a folder; a linked worktree's is a file, and a folder inside a repository has none. Asked
        // before any git, so a root that cannot hold session branches costs nothing.
        if (!Directory.Exists(Path.Combine(root, ".git"))) return Pass([]);
        var (topCode, toplevel, topErr) = await WorkingTree.GitAsync(root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (topCode != 0) return Pass([], TidyKept.Unread, FirstLine(topErr), TidyCause.Toplevel);
        if (!SamePath(toplevel.Trim(), root)) return Pass([]);

        // Where each tree is now (TREEFOLLOW1), written on the ledger before anything is judged, so what the look removes never
        // takes with it where a tree's work is.
        await follows.LearnTreesAsync(space, repository, ct).ConfigureAwait(false);
        // The ledger forgets an entry with no tree and no branch standing at each look, not only after a removal (TREEFOLLOW1): a
        // branch deleted by hand left one behind. A list git cannot give forgets nothing; not cancelled, as the removal's is not.
        await replays.ForgetGoneAsync(root, repository, CancellationToken.None).ConfigureAwait(false);

        // The line here, read with no fetch: the local branch, else origin's copy as this checkout last saw it. One look for the
        // pass (UIFREEZE1): the judgement before the hold and the press's path inside it ask git the checkout's line once.
        using var look = context.OneLook();
        // The refs read once for the judgement before the hold (PROC1d-ii); the press's path inside it reads its own again.
        var refs = RefSnapshot.Take(root);
        if (HomeLease.HeldHere(context.Home))
        {
            // CLEANUP1: a landing's branch the person removed is said on git's word, once (the person's end of it), and a recovery ref past
            // its retention that a branch or a tag holds goes, at most once a day for each repository in a process.
            await cleanups.MarkAbsentAsync(root, repository, ct, refs).ConfigureAwait(false);
            if (RetentionDue(repository)) recovered = await RecoveryRetention.TidyAsync(root, repository, context.Clock(), ct).ConfigureAwait(false);
        }

        var line = (await context.LineAsync(root, repository, space, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct, refs).ConfigureAwait(false);
        if (against is null) return Pass([]);
        // Every session branch (AUTOTIDY2a): the press takes landed, content-held and carried ones the line need not hold, so the
        // look no longer asks git for those the line holds alone. Where the refs could not be read at once, git's own words say why.
        var sessions = await refs.BranchesAsync(TreeFollows.SessionPrefix, ct).ConfigureAwait(false);
        if (sessions is null)
        {
            var (code, output, err) = await WorkingTree.GitAsync(
                root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
            if (code != 0) return Pass([], TidyKept.Unread, FirstLine(err), TidyCause.Refs);
            sessions = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // The branches the repository's landings made and recorded (AUTOTIDY2b, D102's group), which the look takes after them.
        if (sessions.Count == 0 && LandedProofs.EntriesOf(context.Recorded.All(), repository).Count == 0) return Pass([]);
        // FABRIC-END2: what the line holds is kept on the entries before anything goes, so an ended entry outlives its branch as a
        // trace. The end judgement's own writer, in the lease's process only.
        await ends.KeepAsync(root, repository, space, line, ct, refs).ConfigureAwait(false);

        // FABRIC-END4a (the design §C3, §C6 link 2): the candidates are the units whose kept fact says the line holds them at the tip
        // they stand at now, read from the records the keeper just wrote, never every branch judged again each pace. Nothing goes
        // before *held*. The press's judgement below, again inside the hold and once more right before each goes, still decides,
        // so a fact contradicted since keeps its branch (canary 8).
        var held = new List<string>();
        foreach (var grown in context.Grown.All().Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                     && sessions.Contains(each.Branch, StringComparer.Ordinal)))
        {
            if (await HeldHereAsync(grown.Branch, grown.Held).ConfigureAwait(false)) held.Add(grown.Branch);
        }

        var entries = new List<LandedBranch>();
        foreach (var entry in LandedProofs.EntriesOf(context.Recorded.All(), repository))
        {
            if (await HeldHereAsync(entry.Branch, entry.Held).ConfigureAwait(false)) entries.Add(entry);
        }

        // FABRIC-END4a review: a recorded branch the keeper could not prove (an older squash with no unit, a landing inside another)
        // may still be proved by the press's own judgement, as the old tidy proved it (SquashedHolderAsync, Inside). In the keeper's
        // process alone, each is judged once per tip and line commit: an unchanged one is not judged again each pass.
        var lineAt = await LineCommitAsync().ConfigureAwait(false);
        var began = context.Clock();
        var unproven = new Dictionary<string, string>(StringComparer.Ordinal);
        if (HomeLease.HeldHere(context.Home))
        {
            foreach (var grown in context.Grown.All().Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)
                         && sessions.Contains(each.Branch, StringComparer.Ordinal) && !held.Contains(each.Branch, StringComparer.Ordinal)))
            {
                if (await UnjudgedAsync(grown.Branch).ConfigureAwait(false) is { } tip) unproven[grown.Branch] = tip;
            }

            foreach (var entry in LandedProofs.EntriesOf(context.Recorded.All(), repository).Where(each => !entries.Contains(each)))
            {
                if (await UnjudgedAsync(entry.Branch).ConfigureAwait(false) is not { } tip) continue;
                unproven[entry.Branch] = tip;
                entries.Add(entry);
            }
        }

        held.AddRange(unproven.Keys.Where(branch => sessions.Contains(branch, StringComparer.Ordinal)));
        var branches = held.Select(branch => $"{repository}:{branch}").ToHashSet(StringComparer.Ordinal);
        if (branches.Count == 0 && entries.Count == 0) return Pass([]);

        // A list of the working trees git could not give is never "no tree here": nothing in the repository is judged this look.
        if (await context.Worktrees.ListAsync(root, ct, refs).ConfigureAwait(false) is null) return Pass([], TidyKept.Unread, ListUnread, TidyCause.List);

        // Judged before the hold, as the press's list judges them, with the sessions asked so no tree a session works in is read:
        // what stays is said from here, and only what may go holds the repository.
        var (before, unsaid) = await BusyAsync().ConfigureAwait(false);
        if (before is null) return Pass([], TidyKept.Sessions, unsaid);
        // AUTOTIDY2b: the landings' branches first, as their own proof finds them before any session branch leans on them: one whose
        // work the line holds holds a session branch's work there too, so that session branch goes first, as the press takes it.
        var landed = entries.Count == 0 ? [] : await cleanups.JudgeLandedAsync(root, repository, space, entries, [], stale: null, ct, refs).ConfigureAwait(false);
        // FABRIC-END4a, fourth review: each with what its own proof rested on, which a session branch's removal through it rests on too.
        var onLine = landed.Where(each => each.Line is not null)
            .Select(each => new LineLanding(each.Item.Branch, each.Tip, each.Line!) { Rests = each.Rests }).ToList();
        var plan = branches.Count == 0 ? []
            : await sweeps.SweepPlanAsync([(repository, space, root)], before, ct, branches, byItself: true, onLine).ConfigureAwait(false);
        // What the press's judgement kept of the unproven is remembered at its tip and the line's commit, and not judged again until either moves.
        foreach (var item in plan.Where(item => !setAside.GoesByItself(item) && unproven.ContainsKey(item.Branch)))
        {
            Judged[Key(item.Branch)] = (unproven[item.Branch], lineAt, began);
        }

        foreach (var each in landed.Where(each => !each.Item.Removable && unproven.ContainsKey(each.Item.Branch)))
        {
            Judged[Key(each.Item.Branch)] = (unproven[each.Item.Branch], lineAt, began);
        }

        var stays = plan.Where(item => !setAside.GoesByItself(item)).Select(item => new SweepResult(item, false, "kept")).ToList();
        var candidates = plan.Where(setAside.GoesByItself).Select(item => $"{repository}:{item.Branch}").ToHashSet(StringComparer.Ordinal);
        // Then each landing's branch the press would take, unless a session branch the look leaves leans on it (D102: those go first).
        // FABRIC-END4a: every session branch the look leaves leans, the ones it did not judge because the line holds none of them too.
        var leaning = plan.Where(item => !setAside.GoesByItself(item)).Select(item => item.Branch)
            .Concat(sessions.Where(branch => !held.Contains(branch, StringComparer.Ordinal))).ToList();
        var forms = await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false);
        var landings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var each in landed.Where(each => each.Item.Removable))
        {
            if (await LandedCleanups.LeanerAsync(root, each.Tip, leaning, forms, ct).ConfigureAwait(false) is null) landings.Add($"{repository}:{each.Item.Branch}");
        }

        if (candidates.Count == 0 && landings.Count == 0) return Pass(stays);

        using var hold = TreeLock.TryReplaying(context.Home, space, repository);
        if (hold is null) return Pass([], TidyKept.Starting);
        var (busy, unsaidNow) = await BusyAsync().ConfigureAwait(false);
        if (busy is null) return Pass([], TidyKept.Sessions, unsaidNow);

        var results = candidates.Count == 0 ? []
            : await sweeps.SweepAsync([(repository, space, root)], busy, candidates, byItself: true, ct, onLine).ConfigureAwait(false);
        // The record of where session branches grew from drops what is gone (LAND3), as the press's does: after a removal, so not
        // cancelled either.
        if (results.Any(result => result.Removed))
        {
            // FABRIC-END4a review: a removed branch whose tree moved onto a branch of its own is a trace at once, never left standing.
            context.Grown.Gone(repository, [.. results.Where(result => result.Removed).Select(result => result.Item.Branch)]);
            await replays.ForgetGoneAsync(root, repository, CancellationToken.None).ConfigureAwait(false);
        }
        // The landings' branches after the session branches, by the press's own half (AUTOTIDY2b): each judged again against the
        // session branches still standing, and again right before it goes, deleted only at the commit judged.
        var gone = landings.Count == 0 ? []
            : await cleanups.CleanLandedAsync([(repository, space, root)], landings, ct).ConfigureAwait(false);
        return Pass([.. stays, .. results]) with { Landed = gone };

        string Key(string branch) => $"{Path.GetFullPath(context.Home)}|{repository}|{branch}";

        // The line's commit as the refs read it: its local branch, else origin's copy.
        async Task<string?> LineCommitAsync() => line is null ? null
            : await TreeRefs.CommitOfAsync(root, $"refs/heads/{line}", ct, refs).ConfigureAwait(false)
              ?? await TreeRefs.CommitOfAsync(root, $"refs/remotes/origin/{line}", ct, refs).ConfigureAwait(false);

        // A recorded branch's tip, where the press's judgement has not yet kept it at that tip and this line commit; null where it has.
        async Task<string?> UnjudgedAsync(string branch)
        {
            if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{branch}", ct, refs).ConfigureAwait(false) is not { } tip) return null;
            return Judged.TryGetValue(Key(branch), out var seen) && (seen.Tip, seen.Line) == (tip, lineAt) && began - seen.At < RefWatch.Deadline ? null : tip;
        }

        // Whether the kept fact holds at the branch's tip now, by the line the cycle names: read from the refs already taken, so no
        // git starts for it. Where it was cleared or never written, the line is not known to hold it, and nothing goes (canary 15).
        async Task<bool> HeldHereAsync(string branch, HeldFact? fact) =>
            fact is not null && string.Equals(fact.Line, line, StringComparison.Ordinal)
            && string.Equals(await TreeRefs.CommitOfAsync(root, $"refs/heads/{branch}", ct, refs).ConfigureAwait(false), fact.Tip, StringComparison.Ordinal);

        // The trees sessions running or waiting name; none where the service did not say, with its words.
        async Task<(IReadOnlySet<string>? Busy, string? Why)> BusyAsync()
        {
            try
            {
                return (await inUse(ct).ConfigureAwait(false), null);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or JsonException)
            {
                return (null, error.Message);
            }
        }
    }
}
