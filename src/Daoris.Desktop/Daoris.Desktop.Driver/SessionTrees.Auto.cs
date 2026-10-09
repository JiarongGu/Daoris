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

    /// <summary>The press's own removal did not happen: git would not remove its tree (a locked tree), or it moved.</summary>
    public const string Refused = "refused";
}

public sealed partial class SessionTrees
{
    /// <summary>
    /// Whether the look takes this session branch without a press (AUTOTIDY1): the clean-up's <c>empty</c> kind alone, nothing
    /// beyond the line, with no tree or a tree of this home's that holds nothing at all, an ignored path the checkout also holds
    /// included, and every guard read. Every other kind stays the press's, as it was: a squash merge's carried or content-held
    /// branch is <c>landed</c> or <c>carried</c>, never <c>empty</c>.
    /// </summary>
    public bool GoesByItself(SweepItem item) =>
        item is { Kind: SweepKind.Empty, Unread: false, IgnoredShared: 0, HeldBy: null, CarriedBy: null }
        && (item.Tree is null || Holds(item.Tree));

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

        var results = await SweepAsync([(repository, space, root)], busy, candidates, ct, GoesByItself).ConfigureAwait(false);
        // The record of where session branches grew from drops what is gone (LAND3), as the press's does.
        if (results.Any(result => result.Removed)) await ForgetGoneAsync(root, repository, ct).ConfigureAwait(false);
        return Pass(results);
    }
}
