using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// An account's usage: its windows asked and kept, what an account owes, and the accounts a switch could move to.
/// </summary>
internal sealed class RosterUsage(HarnessRoster roster)
{
    // When each account's windows were last asked, by owner and account, answered or not: one that could not be had waits
    // as long as a reading stands before it is asked again.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _usageAsked = new(StringComparer.OrdinalIgnoreCase);

    // The usage history's last week in memory, read on as it grows (USAGEPROBE1): what each start's projection reads.
    private UsageLedger? _ledger;

    private UsageLedger Ledger => LazyInitializer.EnsureInitialized(ref _ledger, () => new UsageLedger(roster.Home));

    /// <summary>
    /// How this adapter's accounts' windows are asked (CODEXUSE1): its own question, else its owner's, since a door's readings
    /// are its owner's as its accounts are (AGT7). Null asks nothing.
    /// </summary>
    public IUsageRead? UsageOf(string adapter) =>
        roster.Adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase)
            ? roster.Adapters.UsageOf(roster.Adapters.Door(adapter).Id)
            : null;

    /// <summary>
    /// Whether this adapter's accounts say what they have left (D130 §6): its door carries the agent's frame
    /// (<see cref="WindowsOf"/>), or its agent's server is asked (<see cref="UsageOf"/>). What switching before the limit waits
    /// for.
    /// </summary>
    public bool Speaks(string adapter) => roster.WindowsOf(adapter) is not null || UsageOf(adapter) is not null;

    /// <summary>
    /// The accounts of a list asked their windows at once, each under its own lock, before the walk reads what each said: each it
    /// could take whose last reading is older than its question's floor (<see cref="CodexUsage.Fresh"/>, or Claude's
    /// <see cref="ClaudeUsage.Floor"/>, CODEXUSE1), and, for a start's walk (<paramref name="use"/>), only where a set point owes
    /// it a question (USAGEPROBE1, <see cref="UsageProjection.Owed"/>).
    /// </summary>
    /// <param name="use">The start's scope's use, whose curve the projections are measured against; null asks past the floor alone.</param>
    internal async Task FreshenAsync(
        string adapter, string owner, IReadOnlyList<string> list, DriverConfig config, CancellationToken ct, RotationUse? use = null)
    {
        if (UsageOf(adapter) is not { } question) return;

        var now = roster.Clock();
        IReadOnlyList<string> present;
        AgentReads reads;
        try
        {
            present = HarnessSettings.Profiles(roster.Home, owner);
            reads = AccountReads.Of(roster.Home, owner);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return;
        }

        IReadOnlyList<CoolingEntry> offered = use is null ? [] : Offered(now);
        var asking = list
            .Where(account => present.Contains(account, StringComparer.OrdinalIgnoreCase))
            .Where(account => roster.Before(owner, account, now).IsReady && !HarnessRoster.SaidOut(reads, account))
            .Where(account => !Recent(owner, account, now, question.Fresh))
            .Where(account => use is null || Owed(owner, account, use, now, offered) is not null)
            .Select(account => AskUsageAsync(adapter, owner, account, config, ct))
            .ToList();
        await Task.WhenAll(asking).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether a start owes this account's maker a question now, and why (USAGEPROBE1); null where the walk ranks on what it
    /// holds. A limit's sentence counts from this process's own hearing or from a cool-off ended within a day.
    /// </summary>
    internal ProbeWhy? Owed(string owner, string account, RotationUse use, DateTimeOffset now, IReadOnlyList<CoolingEntry> offered)
    {
        var said = SaidOf(owner, account, now);
        DateTimeOffset? limit = roster._limitSaid.TryGetValue(HarnessRoster.AccountKey(owner, account), out var heard) ? heard : null;
        foreach (var ended in offered)
        {
            if (string.Equals(ended.Agent, owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ended.Account, account, StringComparison.OrdinalIgnoreCase)
                && (limit is null || ended.Seen > limit))
            {
                limit = ended.Seen;
            }
        }

        return UsageProjection.Owed(said, ProjectionOf(owner, account, said, now), use, now, limit);
    }

    /// <summary>The cool-offs that ended within a day, as the file still holds them; none where it does not read.</summary>
    private IReadOnlyList<CoolingEntry> Offered(DateTimeOffset now)
    {
        try
        {
            return AccountCooling.Offered(roster.Home, now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// An account's windows projected to now (USAGEPROBE1): its last reading plus Daoris's own turns on it since, by its measured
    /// ratio, read from the usage history in memory; null where it said no number.
    /// </summary>
    public AccountProjection? ProjectionOf(string owner, string account, AccountSaid? said, DateTimeOffset now)
    {
        if (said is null) return null;
        var (readings, turns) = Ledger.Of(owner, account, now);
        return UsageProjection.Of(said, readings, turns, now);
    }

    /// <summary>
    /// The walk's facts with each account's projection beside its reading (USAGEPROBE1), for the start's line alone, which says
    /// it as projected: the walk ranked on the readings.
    /// </summary>
    internal Dictionary<string, AccountFacts> Projected(
        string owner, IReadOnlyList<string> accounts, IReadOnlyDictionary<string, AccountFacts> facts, DateTimeOffset now)
    {
        var projected = new Dictionary<string, AccountFacts>(facts, StringComparer.OrdinalIgnoreCase);
        foreach (var account in accounts)
        {
            if (projected.GetValueOrDefault(account) is { Said: { } said } known)
            {
                projected[account] = known with { Projected = ProjectionOf(owner, account, said, now) };
            }
        }

        return projected;
    }

    /// <summary>
    /// The read at a running session's turn's end (SWITCH1c-ii; the switch design §1.2), where the curve's decision asks for it
    /// (<see cref="SwitchChoice.ReadOthers"/>): what a start's walk does for its list (<see cref="FreshenAsync"/>), for these
    /// accounts. Each present and ready (not cooling, not refused, not read signed out) whose last reading is older than its
    /// question's floor is asked, under its own lock; never a key, nor one a session of Daoris's runs on. A reading not had keeps
    /// the older one; absent is never zero (D57). Nothing for an agent whose accounts are asked nothing.
    /// </summary>
    /// <param name="adapter">The session's adapter, whose question, or its owner's, is asked.</param>
    /// <param name="owner">Whose accounts: the accounts' owner (AGT7).</param>
    public Task ReadUsageAsync(
        string adapter, string owner, IReadOnlyList<string> accounts, DriverConfig config, CancellationToken ct = default) =>
        FreshenAsync(adapter, owner, accounts, config, ct);

    /// <summary>
    /// The accounts of a scope's list a running session could move to (SWITCH1c-ii; the switch design §2), in the list's order:
    /// what each last said of its windows, and whether it is ready by the walk's own judgements, before any probe: not cooling,
    /// not refused, not read signed out, and on this machine. Reads files, asks nothing.
    /// </summary>
    /// <param name="owner">Whose accounts: the accounts' owner (AGT7).</param>
    /// <param name="offer">
    /// The conversation's agent, its version and the model its record asks (D205 §3.5(a), MODELROLE1d2): a candidate whose offer reading
    /// lacks or refused that model is not ready, as a start's walk passes it. Null judges no reading, as for a record that asks no model.
    /// </param>
    public IReadOnlyList<SwitchCandidate> SwitchCandidates(
        string owner, IReadOnlyList<string> list, (string Agent, string? Version, string Model)? offer = null)
    {
        var now = roster.Clock();
        IReadOnlyList<string> present;
        AgentReads reads;
        try
        {
            present = HarnessSettings.Profiles(roster.Home, owner);
            reads = AccountReads.Of(roster.Home, owner);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            // Accounts not read are none ready: nothing is moved to on a guess.
            return [.. list.Select(account => new SwitchCandidate(account, null, Ready: false))];
        }

        var floor = FloorOf(owner);
        return [.. list.Select(account => new SwitchCandidate(
            account,
            SaidOf(owner, account, now),
            Ready: present.Contains(account, StringComparer.OrdinalIgnoreCase) && roster.Before(owner, account, now).IsReady && !HarnessRoster.SaidOut(reads, account)
                && (offer is not { } asked || AgentOffers.Offers(roster.Home, asked.Agent, asked.Version, account, now).Session.Judge(asked.Model) is null),
            floor))];
    }

    /// <summary>
    /// How long a reading of the owner's accounts stands: its provider's question's floor (s-iii: Codex's five minutes, Claude's one),
    /// or null where the owner names no door this build resolves or its agent asks nothing of its server.
    /// </summary>
    private TimeSpan? FloorOf(string owner)
    {
        try
        {
            return UsageOf(owner)?.Fresh;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>What an account last said of its windows, or null where nothing was or the file does not read.</summary>
    private AccountSaid? SaidOf(string owner, string account, DateTimeOffset now)
    {
        try
        {
            return AccountWindows.SaidOf(roster.Home, owner, account, now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether an account was asked, or said, within <paramref name="fresh"/> (the question's: <see cref="CodexUsage.Fresh"/>,
    /// or Claude's <see cref="ClaudeUsage.Floor"/>): kept from a restart too.
    /// </summary>
    private bool Recent(string owner, string account, DateTimeOffset now, TimeSpan fresh)
    {
        if (_usageAsked.TryGetValue(HarnessRoster.AccountKey(owner, account), out var asked) && now - asked < fresh) return true;
        try
        {
            return AccountWindows.SaidOf(roster.Home, owner, account, now) is { } said && now - said.Seen < fresh;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ask one account's windows of its agent's server, and keep what it answered (CODEXUSE1): null where nothing was asked or
    /// nothing could be had, and then nothing is kept. Never throws but for the caller's own stop.
    /// </summary>
    /// <param name="account">
    /// The account of Daoris's whose home its server runs in: never none, so the tool's own sign-in is never read (POOL1a3, D197
    /// point 3, amending CODEXUSE3).
    /// </param>
    internal async Task<IReadOnlyList<WindowReading>?> AskUsageAsync(
        string adapter, string owner, string account, DriverConfig config, CancellationToken ct)
    {
        if (UsageOf(adapter) is not { } question) return null;
        try
        {
            // An account that is a key is billed by the key, not a plan's windows (AGT3): nothing to read.
            if (HarnessKeys.Of(roster.Home, owner, account) is not null) return null;
            _usageAsked[HarnessRoster.AccountKey(owner, account)] = roster.Clock();

            // One question per account at a time, and none while a session of Daoris's runs on it (TOOL6g).
            await using var held = await ProbeLock.TakeAsync(ProbeLock.PathOf(roster.Home, owner, account), ct: ct).ConfigureAwait(false);
            if (held is null || roster.Busy(owner, account)) return null;

            IReadOnlyList<WindowReading>? readings;
            if (roster.AskingUsage is { } standIn)
            {
                readings = await standIn(owner, account, ct).ConfigureAwait(false);
            }
            else
            {
                if (UsageCommand(question, config) is not { } command) return null;
                var toolchain = roster.AccountAgentOf(adapter) ?? roster.AgentOf(adapter)!;
                // 🔴 Its server is started in the account's home, never with none, where the agent would answer for the
                // person's own sign-in and might refresh their token (D197 point 3).
                var profileHome = HarnessSettings.ProfileHome(roster.Home, owner, account);
                readings = await question.AskAsync(
                        new UsageAsk(command.Run, toolchain, profileHome, command.Managed, roster.Clock()) { Profile = account }, ct)
                    .ConfigureAwait(false);
            }

            // Every window it said kept in the usage history first (USAGECAP1a1): the reader's own parse where it answered one,
            // else the walk's readings as they are (a stand-in's, reader 0).
            var now = roster.Clock();
            var key = account;
            var door = roster.Adapters.Door(adapter).Id.Value;
            if (readings is UsageAnswer answer)
            {
                roster.History(owner, key, door, answer.Seen, answer.Source, answer.Reader, now, session: null, answer.Plan);
            }
            else if (readings is { Count: > 0 })
            {
                roster.History(
                    owner, key, door,
                    [.. readings.Select(reading => new WindowSeen(
                        reading.Window, reading.Window, null, null, reading.Used, null, reading.Reset, UsageHistory.ResetSaid))],
                    question.Source, reader: 0, now, session: null);
            }

            if (readings is not { Count: > 0 }) return null;
            AccountWindows.Said(roster.Home, owner, key, readings, now, session: null, source: AccountWindows.SourceRead);
            return readings;
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            // A reading not had, or not kept, costs a start's ranking, never the start: the account reads as unknown.
            return null;
        }
    }

    /// <summary>
    /// The server's command, as every spawn resolves its binary (D57): <c>driver.json</c>'s command for the question's harness,
    /// then its managed pin, then <c>PATH</c>. Null where it is pinned with nothing installed at the pin: nobody to ask.
    /// </summary>
    private (IReadOnlyList<string> Run, string? Managed)? UsageCommand(IUsageQuestion question, DriverConfig config)
    {
        var arguments = question.Command.Skip(1).ToList();
        if (config.Commands.GetValueOrDefault(question.Harness) is { Count: > 0 } named) return ([.. named, .. arguments], null);
        if (roster.Settings.ResolveVersion(question.Harness, workspace: null, chosen: null) is not { } pinned) return (question.Command, null);
        return HarnessSettings.ManagedBinary(roster.Home, question.Harness, pinned, [question.Command[0]]) is { } managed
            ? ([managed, .. arguments], managed)
            : null;
    }

    /// <summary>
    /// A press (ROSTER1): each of the agent's accounts asked, one at a time per account, whatever its last reading's age; never
    /// the tool's own sign-in (POOL1a3, D197 point 3, amending CODEXUSE3).
    /// </summary>
    internal async Task PressUsageAsync(string adapter, string owner, DriverConfig config, CancellationToken ct)
    {
        if (UsageOf(adapter) is null) return;
        IReadOnlyList<string> accounts;
        try
        {
            accounts = HarnessSettings.Profiles(roster.Home, owner);
        }
        catch (DriverException)
        {
            accounts = [];
        }

        await Task.WhenAll(accounts.Select(account => AskUsageAsync(adapter, owner, account, config, ct))).ConfigureAwait(false);
    }
}
