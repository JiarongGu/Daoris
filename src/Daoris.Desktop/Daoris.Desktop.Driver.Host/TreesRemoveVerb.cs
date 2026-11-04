using static Daoris.Driver.Host.TreesConsole;

namespace Daoris.Driver.Host;

/// <summary>`daoris tree remove`: a tree by its path, a session's branch by the session or the branch, or a landing's branch withdrawn on a reason.</summary>
internal static class TreesRemoveVerb
{
    /// <summary>
    /// A tree by its path, as it always was; or a session's branch by the session or the branch (LAND3): a failed or superseded attempt's,
    /// which no landing's tidy and no clean-up takes since its commits are on no branch of the person's. Its tree goes with it where it
    /// is still here. --force means it, except over a tree a session still running or waiting holds, which the driver's one discard
    /// keeps for both doors (LAND3c).
    /// </summary>
    public static async Task<int> RunAsync(string[] args, string named, string home, SessionTrees trees)
    {
        var force = args.Contains("--force");
        // LANDRETIRE1: a branch a landing recorded (`quest/…` holds a separator, so this is asked before the path is) is withdrawn on a
        // reason, never removed as a session's is: the driver's one withdrawal, the call the screen's WITHDRAW_LANDING_BRANCH ends in.
        if (new LandedBranchWithdrawal(trees, home).Owners(named, Option(args, "--repository")).Count > 0)
        {
            if (force) return Usage($"`{named}` is a branch a landing made, which is withdrawn with --why and never forced: its commits stay on a recovery ref");
            if (Option(args, "--why") is null && !args.Contains("--plan"))
            {
                return Usage($"`{named}` is a branch a landing made; say why the line will never hold its work with --why \"…\" (--plan says what it would do)");
            }

            return await WithdrawLandingAsync(trees, home, named, Option(args, "--repository"), Option(args, "--why") ?? "", args.Contains("--plan"))
                .ConfigureAwait(false);
        }

        // TREELOCK2: the ledger is asked where a service is there to ask; alone, the removal judges from the files, as before.
        using var service = ServiceOrNone();
        var removal = LooksLikePath(named)
            ? await trees.RemoveAsync(named, force, service: service).ConfigureAwait(false)
            : await RemoveSessionBranchAsync(trees, named, Option(args, "--repository"), force).ConfigureAwait(false);
        Told(home, LooksLikePath(named) ? PressesTold.DiscardTree : PressesTold.DiscardBranch);
        // A refusal that preserves work is policy doing its job, not a tool error. The words are the routes' too (RPC1b3).
        var said = TreesWords.Remove(removal);
        said.Print(Console.Out, Console.Error);
        return said.Code;
    }

    /// <summary>
    /// A session branch the person removes by its door (LAND3): named by the branch (<c>daoris/s-…</c> or <c>s-…</c>) or by the
    /// session, its tree with it while it is here. The driver's one discard, the call the screen's <c>DISCARD_SESSION_BRANCH</c>
    /// ends in (LAND3c), so a branch a live session's tree holds is kept here as there. The service answers the sessions in use
    /// and the registry's checkout.
    /// </summary>
    private static async Task<TreeRemoval> RemoveSessionBranchAsync(SessionTrees trees, string named, string? repository, bool force)
    {
        using var service = ServiceClient.FromEnvironment();
        return await new SessionBranchDiscard(trees).DiscardNamedAsync(service, named, repository, force).ConfigureAwait(false);
    }

    /// <summary>
    /// A landing's branch the line will never hold, withdrawn on the person's reason (LANDRETIRE1): its commits kept on a recovery ref and its
    /// record saying why. The driver's one withdrawal, which the screen's <c>WITHDRAW_LANDING_BRANCH</c> ends in as well; <paramref name="plan"/>
    /// says what it would do and changes nothing. Told to the open windows once it has acted, whatever it answered.
    /// </summary>
    private static async Task<int> WithdrawLandingAsync(SessionTrees trees, string home, string named, string? repository, string why, bool plan)
    {
        using var service = ServiceClient.FromEnvironment();
        var withdrawal = await new LandedBranchWithdrawal(trees, home).WithdrawNamedAsync(service, named, repository, why, plan).ConfigureAwait(false);
        if (!plan) Told(home, PressesTold.WithdrawLanding);
        var said = TreesWords.Withdraw(withdrawal);
        said.Print(Console.Out, Console.Error);
        return said.Code;
    }
}
