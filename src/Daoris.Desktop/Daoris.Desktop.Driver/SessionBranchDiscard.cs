namespace Daoris.Driver;

/// <summary>
/// Discarding a session branch on the person's word (LAND3, LAND3b, LAND3c): a failed or superseded attempt's, whose commits
/// no branch of the person's holds, so no landing's tidy and no clean-up takes it. Its tree goes with it where it is still
/// here. One act, two doors (D50): the screen's <c>DISCARD_SESSION_BRANCH</c> names the branch and its repository
/// (<see cref="DiscardAsync"/>); the terminal's <c>daoris-driver trees remove &lt;session|branch&gt; [--repository &lt;name&gt;]
/// [--force]</c> names the branch or the session (<see cref="DiscardNamedAsync"/>), and ends in the same call.
/// </summary>
/// <remarks>
/// <para><b>A branch whose tree a session still running or waiting holds is kept</b>, whatever the press says, as the clean-up
/// keeps it (D88): forced, <see cref="SessionTrees.RemoveBranchAsync"/> would take that tree with its branch. The tree's branch
/// is the one the layout names (<see cref="SessionTrees.BranchOfTree"/>), so the keep asks git nothing, and it is asked
/// before the checkout. LAND3b built the keep into the screen's door alone, and the terminal's took a live session's tree
/// forced; the act lives here so the two doors cannot part again (LAND3c).</para>
///
/// <para>The service is asked for the sessions running or waiting and for the registry's checkout, where git removes the
/// branch; the terminal's naming by a session asks it for that session's tree besides.</para>
/// </remarks>
public sealed class SessionBranchDiscard(SessionTrees trees)
{
    /// <summary>A branch kept because a session still running or waiting holds its tree: the sentence both doors say.</summary>
    public static string Held(string repository, string branch) =>
        $"a session still running or waiting holds the tree of `{branch}` in `{repository}`, so it is kept.";

    /// <summary>
    /// The branch by its name in its repository, the screen's door and where the terminal's ends: kept while a live session
    /// holds its tree, refused where its repository has no checkout here, else <see cref="SessionTrees.RemoveBranchAsync"/> in
    /// that checkout. Unforced that is D88's proof; a refusal is an answer, never thrown.
    /// </summary>
    public async Task<TreeRemoval> DiscardAsync(
        ServiceClient service, string repository, string branch, bool force, CancellationToken ct = default)
    {
        if (await HeldAsync(service, repository, branch, ct).ConfigureAwait(false)) return new(false, Held(repository, branch));

        var root = await CheckoutAsync(service, repository, ct).ConfigureAwait(false);
        return root is null
            ? new(false, $"`{repository}` has no checkout here, so its branch `{branch}` cannot be removed from this machine.")
            : await trees.RemoveBranchAsync(root, repository, branch, force, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The terminal's door (LAND3): a session branch named by the branch (<c>daoris/s-…</c>) or its tree's name (<c>s-…</c>),
    /// found in the record of where branches grew from, or by the session, whose record names its tree. A session whose tree
    /// is still here has that tree removed by its path, as <see cref="SessionTrees.RemoveAsync"/> always did, under the same
    /// keep; every other name ends in <see cref="DiscardAsync"/>.
    /// </summary>
    public async Task<TreeRemoval> DiscardNamedAsync(
        ServiceClient service, string named, string? repository, bool force, CancellationToken ct = default)
    {
        if (named.StartsWith(SessionTrees.SessionPrefix, StringComparison.Ordinal) || named.StartsWith("s-", StringComparison.Ordinal))
        {
            var found = trees.FindBranches(named, repository);
            var owners = found.Select(entry => entry.Repository).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (owners.Count > 1)
            {
                return new(false, $"`{named}` names a session branch in {string.Join(", ", owners.Select(r => $"`{r}`"))} — say which with "
                    + "`--repository <name>`.");
            }

            var owner = owners.FirstOrDefault() ?? repository;
            if (owner is null)
            {
                return new(false, $"`{named}` names no session branch this machine recorded — say which repository holds it with "
                    + "`--repository <name>`.");
            }

            var branch = found.FirstOrDefault()?.Branch
                         ?? (named.StartsWith(SessionTrees.SessionPrefix, StringComparison.Ordinal) ? named : SessionTrees.SessionPrefix + named);
            return await DiscardAsync(service, owner, branch, force, ct).ConfigureAwait(false);
        }

        var (tree, _) = await service.SessionGroundAsync(named, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tree))
        {
            return new(false, $"session `{named}` names no working tree on this machine, so it left no session branch here.");
        }

        var of = trees.BranchOfTree(tree);
        if (Directory.Exists(tree))
        {
            // A tree outside the trees home is refused by the removal itself, before git is asked.
            return of is { } here && await HeldAsync(service, here.Repository, here.Branch, ct).ConfigureAwait(false)
                ? new(false, Held(here.Repository, here.Branch))
                : await trees.RemoveAsync(tree, force, ct).ConfigureAwait(false);
        }

        return of is { } gone
            ? await DiscardAsync(service, gone.Repository, gone.Branch, force, ct).ConfigureAwait(false)
            : new(false, $"session `{named}`'s tree is not one this machine's trees home holds, so Daoris removes nothing for it.");
    }

    /// <summary>Whether a tree a session running or waiting names is this branch's, by the layout: the ledger as it says now.</summary>
    private async Task<bool> HeldAsync(ServiceClient service, string repository, string branch, CancellationToken ct) =>
        (await service.ActiveSessionsAsync(ct).ConfigureAwait(false))
            .Select(session => session.Tree).OfType<string>().Where(tree => tree.Length > 0)
            .Any(tree => trees.BranchOfTree(tree) is { } of
                         && string.Equals(of.Repository, repository, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(of.Branch, branch, StringComparison.Ordinal));

    /// <summary>The repository's checkout the registry names, where it is on this disk; null where it is not.</summary>
    private static async Task<string?> CheckoutAsync(ServiceClient service, string repository, CancellationToken ct)
    {
        var root = (await service.RegistryAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Root;
        return string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ? null : root;
    }
}
