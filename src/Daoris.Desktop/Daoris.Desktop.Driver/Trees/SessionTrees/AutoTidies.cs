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
/// repository's session branches, holds it, and hands what may go to the clean-up's press path. Over the trees' context, the
/// follow's ledger, the replay's record tidying, the sweep, which takes the look's guards and set-aside itself, and the set-aside's
/// judgement of what goes (<see cref="TreeSetAside"/>).
/// </summary>
internal sealed class AutoTidies(TreeContext context, TreeFollows follows, BranchReplays replays, TreeSweeps sweeps, TreeSetAside setAside)
{
    /// <summary>
    /// The look's own tidy of one repository (AUTOTIDY1, D88's note; AUTOTIDY2a): every session branch, judged as the press's
    /// list judges it against the line read here with no fetch, and those <see cref="TreeSetAside.GoesByItself"/> takes removed
    /// by the clean-up's own press (<see cref="TreeSweeps"/>), judged again right before each goes, and deleted only while it is
    /// the commit judged.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Held alone, the sessions asked inside the hold</b>: a fresh tree a start just opened is an empty branch with
    /// a clean tree until its record opens, so the repository's trees are held as bringing them up to date holds them
    /// (<see cref="TreeLock"/>), and a start that holds them leaves every branch for another look.</para>
    ///
    /// <para><b>Judged before the hold</b> (AUTOTIDY2a): every session branch is judged first, the sessions asked for it too so
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
        var space = RemoteTarget.Workspace(workspace);
        TidyPass Pass(IReadOnlyList<SweepResult> results, string? held = null, string? detail = null, string? cause = null) =>
            new(results, held, detail) { Repository = repository, Workspace = space, Cause = cause };

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
        var line = (await context.LineAsync(root, repository, space, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return Pass([]);
        // Every session branch (AUTOTIDY2a): the press takes landed, content-held and carried ones the line need not hold, so the
        // look no longer asks git for those the line holds alone.
        var (code, refs, err) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
        if (code != 0) return Pass([], TidyKept.Unread, FirstLine(err), TidyCause.Refs);
        var branches = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(branch => $"{repository}:{branch}")
            .ToHashSet(StringComparer.Ordinal);
        if (branches.Count == 0) return Pass([]);

        // A list of the working trees git could not give is never "no tree here": nothing in the repository is judged this look.
        if (await context.Worktrees.ListAsync(root, ct).ConfigureAwait(false) is null) return Pass([], TidyKept.Unread, ListUnread, TidyCause.List);

        // Judged before the hold, as the press's list judges them, with the sessions asked so no tree a session works in is read:
        // what stays is said from here, and only what may go holds the repository.
        var (before, unsaid) = await BusyAsync().ConfigureAwait(false);
        if (before is null) return Pass([], TidyKept.Sessions, unsaid);
        var plan = await sweeps.SweepPlanAsync([(repository, space, root)], before, ct, branches, byItself: true).ConfigureAwait(false);
        var stays = plan.Where(item => !setAside.GoesByItself(item)).Select(item => new SweepResult(item, false, "kept")).ToList();
        var candidates = plan.Where(setAside.GoesByItself).Select(item => $"{repository}:{item.Branch}").ToHashSet(StringComparer.Ordinal);
        if (candidates.Count == 0) return Pass(stays);

        using var hold = TreeLock.TryReplaying(context.Home, space, repository);
        if (hold is null) return Pass([], TidyKept.Starting);
        var (busy, unsaidNow) = await BusyAsync().ConfigureAwait(false);
        if (busy is null) return Pass([], TidyKept.Sessions, unsaidNow);

        var results = await sweeps.SweepAsync([(repository, space, root)], busy, candidates, byItself: true, ct).ConfigureAwait(false);
        // The record of where session branches grew from drops what is gone (LAND3), as the press's does: after a removal, so not
        // cancelled either.
        if (results.Any(result => result.Removed)) await replays.ForgetGoneAsync(root, repository, CancellationToken.None).ConfigureAwait(false);
        return Pass([.. stays, .. results]);

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
