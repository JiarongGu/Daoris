namespace Daoris.Driver;

/// <summary>
/// The person's doors for withdrawing a landing's branch the line will never hold (LANDRETIRE1; D50: one act, two doors): the window's
/// <c>WITHDRAW_LANDING_BRANCH</c> names the branch and its repository (<see cref="WithdrawAsync"/>); the terminal's
/// <c>daoris-driver trees remove &lt;landing branch&gt; [--repository &lt;name&gt;] --why "…" [--plan]</c> names the branch alone
/// (<see cref="WithdrawNamedAsync"/>), and both end in <see cref="SessionTrees.WithdrawLandedAsync"/>, which judges and acts.
/// Mirrors <see cref="SessionBranchDiscard"/>.
/// </summary>
/// <remarks>
/// <para><b>Only the process holding the home's lease acts</b> (<see cref="RecoveryDropping.WithoutHome"/>, D102's RECOVERYDROP1 note): the
/// desktop's own press while it runs, or a verb acting alone under the lease it took; a verb beside a running desktop is refused and says
/// so. A plan changes nothing and needs none.</para>
///
/// <para>The service is asked for the repository's checkout, where git deletes the branch. A refusal is an answer, never thrown.</para>
/// </remarks>
public sealed class LandedBranchWithdrawal(SessionTrees trees, string home)
{
    /// <summary>
    /// The repositories of this machine's standing landings that made a branch named <paramref name="named"/>, in one repository where
    /// <paramref name="repository"/> says which: none for a name no landing made, which the terminal's <c>remove</c> then reads as it always did.
    /// </summary>
    public IReadOnlyList<string> Owners(string named, string? repository) =>
        [.. trees.Recorded.All()
            .Where(entry => string.Equals(entry.Branch, named, StringComparison.Ordinal)
                            && (repository is null || string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => entry.Repository).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The branch by its name in its repository, the window's door and where the terminal's ends: refused where this process does not hold the
    /// home, or where its repository has no checkout here, else <see cref="SessionTrees.WithdrawLandedAsync"/> in that checkout.
    /// </summary>
    public async Task<LandedWithdrawal> WithdrawAsync(
        ServiceClient service, string repository, string branch, string why, bool plan, CancellationToken ct = default)
    {
        if (!plan && RecoveryDropping.WithoutHome(home) is { } refused) return new(false, refused);
        var root = (await service.RegistryAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Root;
        return string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)
            ? new(false, $"`{repository}` has no checkout here, so its branch `{branch}` cannot be withdrawn from this machine.")
            : await trees.WithdrawLandedAsync(root, repository, branch, why, plan, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The terminal's door: a branch named alone, in the one repository whose landing made it. A name two repositories' landings made is
    /// refused until <c>--repository</c> says which.
    /// </summary>
    public async Task<LandedWithdrawal> WithdrawNamedAsync(
        ServiceClient service, string named, string? repository, string why, bool plan, CancellationToken ct = default)
    {
        var owners = Owners(named, repository);
        return owners.Count switch
        {
            0 => new(false, $"no landing of this machine's made `{named}`"
                + (repository is null ? "" : $" in `{repository}`") + ", so Daoris withdraws nothing."),
            1 => await WithdrawAsync(service, owners[0], named, why, plan, ct).ConfigureAwait(false),
            _ => new(false, TreesWords.NamesSeveral(named, owners)),
        };
    }
}
