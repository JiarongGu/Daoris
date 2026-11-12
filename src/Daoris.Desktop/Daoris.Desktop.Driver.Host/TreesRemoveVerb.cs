using static Daoris.Driver.Host.TreesConsole;

namespace Daoris.Driver.Host;

/// <summary>`daoris tree remove`: a tree by its path, a session's branch by the session or the branch, or a landing's branch withdrawn on a reason.</summary>
internal static class TreesRemoveVerb
{
    /// <summary>
    /// A tree by its path; or a session's branch by the session or the branch: a failed or superseded attempt's,
    /// which no landing's tidy and no clean-up takes since its commits are on no branch of the person's. Its tree goes with it where it
    /// is still here. --force means it, except over a tree a session still running or waiting holds, which the driver's one discard
    /// keeps for both doors.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, string named, string home, SessionTrees trees)
    {
        var force = args.Contains("--force");
        // A branch a landing recorded (`quest/…` holds a separator, so this is asked before the path is) is withdrawn on a
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

        // A session's branch is the running Daoris's DISCARD_SESSION_BRANCH where one answers, confirmed in its window or run under the
        // family a person granted; alone where none runs.
        if (!LooksLikePath(named))
        {
            var repository = Option(args, "--repository");
            return await InstanceDoor.PressAsync(TreesRoutes.DiscardBranch, new { named, repository, force },
                () => DiscardAloneAsync(trees, home, named, repository, force),
                answer => TreesSaid.ReadWords(Console.Out, Console.Error)(answer) ?? 2, InstanceHost.World(home, "trees")).ConfigureAwait(false);
        }

        // The ledger is asked where a service is there to ask; alone, the removal judges from the files.
        using var service = ServiceOrNone();
        var removal = await trees.RemoveAsync(named, force, service: service).ConfigureAwait(false);
        Told(home, PressesTold.DiscardTree);
        // A refusal that preserves work is policy doing its job, not a tool error. The words are the routes' too.
        var said = TreesWords.Remove(removal);
        said.Print(Console.Out, Console.Error);
        return said.Code;
    }

    /// <summary>
    /// A session branch the person removes by its door, where no Daoris runs: named by the branch (<c>daoris/s-…</c> or <c>s-…</c>)
    /// or by the session, its tree with it while it is here. The driver's one discard, the call <c>DISCARD_SESSION_BRANCH</c> ends in,
    /// so a branch a live session's tree holds is kept here as there. The service answers the sessions in use and the registry's checkout.
    /// </summary>
    private static async Task<int> DiscardAloneAsync(SessionTrees trees, string home, string named, string? repository, bool force)
    {
        using var service = ServiceClient.FromEnvironment();
        var removal = await new SessionBranchDiscard(trees).DiscardNamedAsync(service, named, repository, force).ConfigureAwait(false);
        Told(home, PressesTold.DiscardBranch);
        var said = TreesWords.Remove(removal);
        said.Print(Console.Out, Console.Error);
        return said.Code;
    }

    /// <summary>
    /// A landing's branch the line will never hold, withdrawn on the person's reason: its commits kept on a recovery ref and its
    /// record saying why. The driver's one withdrawal, which the screen's <c>WITHDRAW_LANDING_BRANCH</c> ends in as well; <paramref name="plan"/>
    /// says what it would do and changes nothing. Told to the open windows once it has acted, whatever it answered.
    /// </summary>
    private static async Task<int> WithdrawLandingAsync(SessionTrees trees, string home, string named, string? repository, string why, bool plan)
    {
        var door = new LandedBranchWithdrawal(trees, home);
        async Task<int> AloneAsync()
        {
            using var service = ServiceClient.FromEnvironment();
            var withdrawal = await door.WithdrawNamedAsync(service, named, repository, why, plan).ConfigureAwait(false);
            if (!plan) Told(home, PressesTold.WithdrawLanding);
            var said = TreesWords.Withdraw(withdrawal);
            said.Print(Console.Out, Console.Error);
            return said.Code;
        }

        // Beside a running Daoris the act is its own route, sent as a terminal press, never refused for the desktop running; a name two
        // repositories made, or none, is answered where it is judged, alone.
        if (plan || door.Owners(named, repository) is not [var owner]) return await AloneAsync().ConfigureAwait(false);
        return await InstanceDoor.PressAsync(TreesRoutes.Withdraw, new { repository = owner, branch = named, why, plan },
            AloneAsync, answer => TreesSaid.ReadWords(Console.Out, Console.Error)(answer) ?? 2, InstanceHost.World(home, "trees")).ConfigureAwait(false);
    }
}
