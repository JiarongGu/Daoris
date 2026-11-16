namespace Daoris.Driver;

/// <summary>
/// An account's sign-in renewed once before sessions start or resume on it together, and a sign-in its agent cleared read at once
/// (CREDRACE1). Both read the stored sign-in's shape alone (<see cref="IStoredSignIn"/>), never a token.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Why</b>: a sign-in's refresh token rotates, single-use. Sessions on one account that start together each find the
/// access token expired and each refresh it with the one token they read; the first wins, the others are refused, and the agent
/// clears the account's stored sign-in, its refresh token still good for weeks (2026-11-16, at an install's restart). Renewed once
/// first, under the account's lock (<see cref="ProbeLock"/>), the sessions find a fresh token and none of them refreshes.</para>
/// <para><b>When</b>: a start's walk, once it has counted the account, takes the account's lock to wait out a question in flight
/// (<c>RosterWalk.WaitedOutAsync</c>); holding it, it renews where the store's access token expires within <see cref="Margin"/>.
/// A start beside it waits on the same lock and finds the token fresh. Not where a session of Daoris's already runs on the
/// account, whose own process renews the token it holds. The renewal is the agent's own status question, which refreshes an expired
/// token to answer, bounded by its patience; its answer is kept as the account's reading.</para>
/// </remarks>
internal static class SignInRefresh
{
    /// <summary>How close to its expiry an access token is renewed first: wider than an agent's own margin, so no session renews it.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Renew <paramref name="account"/>'s sign-in where its stored access token expires within <see cref="Margin"/>, the caller holding
    /// the account's lock. True where it is asked.
    /// </summary>
    /// <param name="asker">The agent that answers for the account (<c>HarnessRoster.AnswersFor</c>), whose store and question these are.</param>
    public static async Task<bool> OnceAsync(
        HarnessRoster roster, AccountAgent asker, string owner, string account, DriverConfig config, CancellationToken ct)
    {
        if (asker.Accounts.StoredSignIn is not { } store || Running(roster, owner, account)) return false;
        var home = HarnessSettings.ProfileHome(roster.Home, owner, account);
        if (store.Read(home) is not { Cleared: false } shape || !shape.Expiring(roster.Clock(), Margin)) return false;

        // The lock is the caller's: asked with none, as a question under it.
        var answer = roster.Asking is { } standIn
            ? new HarnessProbe.LoginAnswer(await standIn(owner, account, ct).ConfigureAwait(false), null, true)
            : await HarnessProbe.AskAccountAsync(
                asker.Name, asker, config.Commands.GetValueOrDefault(asker.Name), roster.Settings, roster.Home, home, ct,
                lockPath: null, key: null).ConfigureAwait(false);
        if (answer.Asked) roster.Keep(owner, account, answer.Said, roster.Clock());
        return true;
    }

    /// <summary>Whether a session of Daoris's runs on the account now, by the last look: a start chosen and not yet running is none.</summary>
    private static bool Running(HarnessRoster roster, string owner, string account)
    {
        lock (roster._load)
        {
            return roster._looked.Any(each => each.Session.Running
                                       && string.Equals(each.Owner, owner, StringComparison.OrdinalIgnoreCase)
                                       && string.Equals(each.Session.Profile, account, StringComparison.OrdinalIgnoreCase));
        }
    }
}

/// <summary>
/// The stored sign-ins read at each look: an account whose store its agent cleared since its last reading reads signed out from
/// then, kept as a reading is (<see cref="AccountReads"/>), so the walk passes it, its page says so through its sign-in state
/// (<see cref="SignInStates"/>) and, after a signed-in reading, a notice tells it (<see cref="SignOutNotices"/>).
/// </summary>
internal static class StoredSignIns
{
    /// <summary>Each cleared store not yet read signed out, kept so; how many.</summary>
    public static int Read(HarnessRoster roster)
    {
        var kept = 0;
        foreach (var name in roster.Adapters.Agents)
        {
            // A door's accounts are its owner's: each store read once, under its owner.
            if (roster.Adapters.AgentNamed(name) is not { Accounts.StoredSignIn: { } store }
                || !string.Equals(roster.Adapters.OwnerOfAgent(name).Value, name, StringComparison.OrdinalIgnoreCase)) continue;
            var reads = AccountReads.Of(roster.Home, name).Accounts;
            foreach (var account in HarnessSettings.Profiles(roster.Home, name))
            {
                if (store.Read(HarnessSettings.ProfileHome(roster.Home, name, account)) is not { Cleared: true } shape) continue;
                var read = reads.GetValueOrDefault(account);
                if (read is { Login: LoginState.Out }) continue;
                // At the store's write, unless a reading came after it, which an older moment never replaces.
                var at = read is null || shape.Written > read.At ? shape.Written : roster.Clock();
                roster.Keep(name, account, new LoginRead(LoginState.Out, null), at);
                kept++;
            }
        }

        return kept;
    }
}

/// <summary>The look's read of the stored sign-ins (<see cref="StoredSignIns"/>), before the sign-outs it may make are claimed.</summary>
[LookPass("stored-sign-ins", LookPhase.After, "plugin-commands")]
internal sealed class StoredSignInsLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var kept = StoredSignIns.Read(wiring.Harnesses);
        if (kept > 0) round.Events.Add($"{kept} account(s) read signed out: their agent cleared the stored sign-in.");
        return Task.CompletedTask;
    }
}
