namespace Daoris.Driver;

/// <summary>
/// An account's sign-in renewed once before sessions start or resume on it together, and a sign-in its agent cleared read at once.
/// Both read the stored sign-in's shape alone (<see cref="IStoredSignIn"/>), never a token.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Why</b>: a sign-in's refresh token rotates, single-use. Sessions on one account that start together each find the
/// access token expired and each refresh it with the one token they read; the first wins, the others are refused, and the agent
/// clears the account's stored sign-in, its refresh token still good for weeks (2026-11-16, at an install's restart). Renewed once
/// first, under the account's lock (<see cref="ProbeLock"/>), the sessions find a fresh token and none of them refreshes.</para>
/// <para><b>When</b>: a start's walk, once it has counted the account, takes the account's lock to wait out a question in flight
/// (<c>RosterWalk.WaitedOutAsync</c>); holding it, it renews where the store's access token expires within <see cref="Margin"/>.
/// A start beside it waits on the same lock and finds the token fresh. Not where a session of Daoris's already runs on the
/// account, whose own process renews the token it holds: there a start reads the store as it stands, and its session may refresh at
/// the moment the running one does, which no lock of Daoris's reaches (the area's rule 33 says so). The renewal is the agent's own status question, which refreshes an expired
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
    private static bool Running(HarnessRoster roster, string owner, string account) => SessionsOn(roster, owner, account).Running.Count > 0;

    /// <summary>
    /// The sessions of Daoris's running on the account by the last look (their ids), and how many starts were chosen on it that the look
    /// has not read yet: whose processes may refresh its token now.
    /// </summary>
    internal static (IReadOnlyList<string> Running, int Starting) SessionsOn(HarnessRoster roster, string owner, string account)
    {
        lock (roster._load)
        {
            var running = roster._looked
                .Where(each => each.Session.Running
                               && string.Equals(each.Owner, owner, StringComparison.OrdinalIgnoreCase)
                               && string.Equals(each.Session.Profile, account, StringComparison.OrdinalIgnoreCase))
                .Select(each => each.Session.Id ?? "?")
                .ToList();
            var starting = roster._chosen.Count(each => string.Equals(each.Owner, owner, StringComparison.OrdinalIgnoreCase)
                                                        && string.Equals(each.Account, account, StringComparison.OrdinalIgnoreCase));
            return (running, starting);
        }
    }
}

/// <summary>
/// Starts on one account whose stored access token is still about to expire, after any renewal, paced one at a time: a session of
/// such a start reads the store as it starts and refreshes the token it read, so sessions reopened together after a restart, all
/// holding the one token, spend one single-use refresh token several times. The first start goes at once; each next
/// goes <see cref="HarnessRoster.RefreshStagger"/> after the one before, or as soon as the store is no longer expiring (the earlier
/// session refreshed it), and none goes while a write of the store is in flight.
/// </summary>
internal sealed class RefreshPace
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _next = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How long to wait before looking again at <paramref name="slot"/>, never more than <paramref name="poll"/>; null once it has come.</summary>
    internal static TimeSpan? Pause(long slot, long now, TimeSpan poll) =>
        slot - now is > 0 and var left ? TimeSpan.FromMilliseconds(Math.Min(left, poll.TotalMilliseconds)) : null;

    /// <summary>Wait this start's turn on the account.</summary>
    public async Task WaitAsync(HarnessRoster roster, AccountAgent asker, string owner, string account, CancellationToken ct)
    {
        if (asker.Accounts.StoredSignIn is not { } store) return;
        var home = HarnessSettings.ProfileHome(roster.Home, owner, account);
        bool Risky() => store.Read(home) is { Cleared: false } shape && shape.Expiring(roster.Clock(), SignInRefresh.Margin);

        if (roster.RefreshStagger > TimeSpan.Zero && Risky())
        {
            long slot;
            lock (_gate)
            {
                var key = HarnessRoster.AccountKey(owner, account);
                slot = Math.Max(Environment.TickCount64, _next.GetValueOrDefault(key));
                _next[key] = slot + (long)roster.RefreshStagger.TotalMilliseconds;
            }

            while (Pause(slot, Environment.TickCount64, roster.RefreshPoll) is { } pause && Risky())
            {
                await roster.Delay(pause, ct).ConfigureAwait(false);
            }
        }

        // A write of the store in flight is waited out, within the walk's patience: its rename is the moment the token changes.
        var until = Environment.TickCount64 + (long)roster.LockPatience.TotalMilliseconds;
        while (StoredLeftovers.InFlight(store, home, roster.Clock().UtcDateTime) && Environment.TickCount64 < until)
        {
            await roster.Delay(roster.RefreshPoll, ct).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// The stored sign-ins, read at each look and at each write the credentials watch sees (<see cref="CredentialWrites"/>): a cleared
/// store reads its account signed out, kept as a reading is (<see cref="AccountReads"/>), so the walk passes it, its page says so
/// through its sign-in state (<see cref="SignInStates"/>) and, after a signed-in reading, a notice tells it (<see cref="SignOutNotices"/>).
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
            foreach (var account in HarnessSettings.Profiles(roster.Home, name))
            {
                if (store.Read(HarnessSettings.ProfileHome(roster.Home, name, account)) is not { Cleared: true } shape) continue;
                if (MarkOut(roster, name, account, shape, by: null)) kept++;
            }
        }

        return kept;
    }

    /// <summary>A cleared store reads the account signed out unless it does, at the store's write unless a reading came after it; whether it marked.</summary>
    internal static bool MarkOut(HarnessRoster roster, string owner, string account, StoredSignInShape shape, string? by)
    {
        var read = AccountReads.Of(roster.Home, owner).Accounts.GetValueOrDefault(account);
        if (read is { Login: LoginState.Out }) return false;
        var at = read is null || shape.Written > read.At ? shape.Written : roster.Clock();
        roster.Keep(owner, account, new LoginRead(LoginState.Out, null), at, by);
        return true;
    }

    /// <summary>A store filled after the watch saw it cleared is a sign-in: the account read signed out reads signed in; whether it marked.</summary>
    internal static bool MarkIn(HarnessRoster roster, string owner, string account)
    {
        var read = AccountReads.Of(roster.Home, owner).Accounts.GetValueOrDefault(account);
        if (read is not { Login: LoginState.Out }) return false;
        roster.Keep(owner, account, new LoginRead(LoginState.In, null), roster.Clock());
        return true;
    }
}
