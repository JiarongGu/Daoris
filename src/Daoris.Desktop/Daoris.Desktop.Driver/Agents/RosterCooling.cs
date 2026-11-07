using System.Collections.Concurrent;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the roster knows of a cool-off, a refusal and a signed-out account: the words each adapter says them in, and the holds kept and cleared.
/// </summary>
internal sealed class RosterCooling(HarnessRoster roster)
{
    /// <summary>
    /// Hold further starts on an account its provider refused, with the sentence that says
    /// why and what fixes it.
    /// </summary>
    /// <remarks>
    /// 🔴 Measured: a refused key costs a session over three minutes of the tool's silent retries
    /// before it fails, and every further session on that account would do the same. It is the
    /// ACCOUNT's, so it holds both doors onto it. It clears when a person looks again: any
    /// account action, or the roster's own refresh. A new sign-in or key is exactly such an action.
    /// </remarks>
    /// <param name="note">
    /// The reason by code, its English the reason to the byte: a hold on this account says it by code. Without it the
    /// hold keeps the reason's words.
    /// </param>
    public void Refuse(string adapter, string? profile, string reason, Noted? note = null)
    {
        var owner = roster.Adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase) ? roster.Adapters.OwnerOfAgent(adapter).Value : adapter;
        roster._refused[HarnessRoster.AccountKey(owner, profile)] = (reason, note is { } coded && coded.Note == reason ? coded : null);
    }

    // The waits already written to the machine log, by account, with the cool-off each is for and the accounts it passed
    // not signed in: a wait is written once, however many looks it lasts, and a new cool-off on the account,
    // or another account signed out or in, is a new wait.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _waited =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whose accounts a cool-off on this adapter belongs to: its owner, as its accounts are; null for an adapter
    /// that declares no toolchain, which has no accounts and reads no limit.
    /// </summary>
    internal string? CoolingAgent(AgentDoor resolved) => roster.Adapters.AgentNamed(resolved.Id.Value) is null ? null : roster.Adapters.OwnerOfAgent(resolved.Id.Value).Value;

    /// <summary>
    /// The words this adapter says an account's limit in: its own entry, else its owner's, since a
    /// door's limits are its owner's as its accounts are. Null reads every failure as a failure.
    /// </summary>
    public LimitWords? LimitsOf(string adapter) => Held(adapter) is { } door ? roster.Adapters.LimitsOf(door) : null;

    /// <summary>The registry's door for a name this set carries, else null (an unknown name reads no words).</summary>
    private DoorId? Held(string adapter) =>
        roster.Adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase) ? new DoorId(roster.Adapters.Door(adapter).Id.Value) : null;

    /// <summary>
    /// The words this adapter's agent refuses a start for its sign-in in: its own entry, else its owner's, since a
    /// door's refusals are its owner's as its accounts are. Null reads every failure as a failure.
    /// </summary>
    public SignInWords? SignInOf(string adapter) => Held(adapter) is { } door ? roster.Adapters.SignInOf(door) : null;

    /// <summary>
    /// A start on this adapter as <paramref name="profile"/> is refused for its sign-in: that account,
    /// or with <paramref name="profile"/> null the tool's own sign-in, reads signed out from now, kept as a reading is
    /// (<see cref="AccountReads"/>), so the next start walks past it as it walks past any account read signed out and a
    /// restart starts from it. Its owner's, so every door onto the account reads it.
    /// </summary>
    /// <remarks>
    /// <para>Cleared as a reading signed out is: a sign-in or a key through either door marks the account, and the next start
    /// asks it again (<see cref="AskAgain"/>); a sign-in on the screen reads it as it ends; a press reads it; and the hour's
    /// backstop. A reading not written costs the next start one more refusal, never this one's record.</para>
    /// <para>🔴 <b>A refusal of an account last read signed in owes it one fresh reading</b>: the refusal contradicts
    /// the agent's own last word, so the next start that walks to the account asks it once, whatever the mark and the hour say,
    /// and its answer is kept and honoured; a sign-in the person made since, at a door of Daoris's or not, runs it again. One:
    /// the refusal of a signed-in reading that fresh reading itself made owes none, so an agent that says signed in while it
    /// refuses every start costs one more start, never one per look. Never the tool's own sign-in, which only a person's press
    /// asks. Held in memory, as <see cref="Refuse"/> is: after a restart the reading waits for the mark or the hour.</para>
    /// </remarks>
    public void SignedOut(string adapter, string? profile)
    {
        if (CoolingAgent(roster.Adapters.Door(adapter)) is not { } owner) return;
        var before = profile is null ? null : AccountReads.Of(roster.Home, owner).Accounts.GetValueOrDefault(profile);
        Keep(owner, profile, new LoginRead(LoginState.Out, null), roster.Clock());
        if (profile is not null && before is { Login: LoginState.In }
            && !(roster._fresh.TryGetValue(HarnessRoster.AccountKey(owner, profile), out var made) && made == before.At))
        {
            roster._owed[HarnessRoster.AccountKey(owner, profile)] = true;
        }
    }

    /// <summary>
    /// How this adapter says an account's windows: its own entry, else its owner's, since a door's readings
    /// are its owner's as its accounts are. Null says nothing.
    /// </summary>
    public WindowWords? WindowsOf(string adapter) => Held(adapter) is { } door ? roster.Adapters.WindowsOf(door) : null;

    /// <summary>
    /// What a session's door said about the windows of the account it runs as: read by the adapter's
    /// table and kept in <c>windows.json</c>, the newest reading of each window replacing the older, as the door carries it.
    /// Only a named account's, since the tool's own sign-in is never in a list. Null where nothing is kept.
    /// </summary>
    /// <param name="info">The frame's object, apart from the agent's words: never the transcript.</param>
    /// <remarks>
    /// Every window the frame says is kept in the usage history first, the scoped weeks the walk
    /// does not read among them, at the moment <c>windows.json</c> keeps.
    /// </remarks>
    public IReadOnlyList<WindowReading>? Said(string adapter, string? profile, JsonElement info, string? session)
    {
        var resolved = roster.Adapters.Door(adapter);
        if (profile is null || CoolingAgent(resolved) is not { } agent || WindowsOf(resolved.Id.Value) is not { } words) return null;

        var now = roster.Clock();
        History(agent, profile, resolved.Id.Value, AccountReadings.Seen(info, words), UsageHistory.SourceFrame, AccountReadings.HistoryReader, now, session);
        var readings = AccountReadings.Read(info, words);
        if (readings.Count == 0) return null;
        AccountWindows.Said(roster.Home, agent, profile, readings, now, session);
        return readings;
    }

    /// <summary>
    /// A reader's windows kept in the usage history, before <c>windows.json</c> keeps the newest of
    /// each: with the plan as last read, the answer's own where it named one, else the one this process's last status question
    /// of the account read (held in memory alone); never who signed in or the organisation. Never throws.
    /// </summary>
    /// <param name="account">The account, or <see cref="AccountWindows.Own"/> for the tool's own sign-in.</param>
    internal void History(
        string owner, string account, string door, IReadOnlyList<WindowSeen> windows, string source, int reader, DateTimeOffset now,
        string? session, string? plan = null)
    {
        if (windows.Count == 0) return;
        var planFrom = plan is null ? null : source;
        if (plan is null && roster._who.TryGetValue(HarnessRoster.AccountKey(owner, account), out var who) && who.Said is { Login: LoginState.In, Plan: { } read })
        {
            plan = read;
            planFrom = UsageHistory.PlanFromStatus;
        }

        UsageHistory.Kept(roster.Home, new ReadingFrom(owner, account, door, source, reader, now, session, plan, planFrom), windows, now);
        ModelsSpent(owner, account, door, windows, now, session);
    }

    /// <summary>
    /// A reading that puts a model family's week at its limit cools the account for that family until the week's reset, as its agent
    /// said it: the walk reads only the whole plan's windows, so the family's limit is kept as a cool-off of account plus
    /// model. A cool-off the file will not take is held in memory, as a limit's is.
    /// </summary>
    private void ModelsSpent(string owner, string account, string door, IReadOnlyList<WindowSeen> windows, DateTimeOffset now, string? session)
    {
        foreach (var (family, reset) in ModelLimits.Spent(windows, WindowsOf(door)?.Models, now))
        {
            AccountCooling.Keep(
                roster.Home,
                new CoolingEntry(owner, account == AccountWindows.Own ? null : account, reset, Stated: true, AccountWindows.Weekly, now, session, Model: family),
                now);
        }
    }

    /// <summary>
    /// The account's cool-off now, or null when it is ready, a start on this adapter as <paramref name="profile"/> would read it.
    /// </summary>
    /// <param name="model">The model the start asks: a model family's cool-off holds only a start of that family; null is account default.</param>
    public CoolingEntry? CoolingOf(string adapter, string? profile, string? model = null) =>
        CoolingAgent(roster.Adapters.Door(adapter)) is { } agent ? AccountCooling.Of(roster.Home, agent, profile, roster.Clock(), model) : null;

    /// <summary>
    /// Every account of the adapter's owner cooling now for a start of <paramref name="model"/>, earliest ready first:
    /// what a conversation held by its limit computes its wait from. None for an adapter that has no accounts.
    /// </summary>
    public IReadOnlyList<CoolingEntry> CoolingsOf(string adapter, string? model = null) =>
        CoolingAgent(roster.Adapters.Door(adapter)) is { } agent
            ? [.. AccountCooling.Holding(roster.Home, agent, roster.Clock(), model).OrderBy(entry => entry.Until)]
            : [];

    /// <summary>
    /// The door's failure, read for an account's limit: where the adapter's table recognises it, the
    /// account the session ran as cools until the reset the agent named, and what is read is returned. Null is a
    /// failure, and nothing is written.
    /// </summary>
    /// <param name="failure">What the door carried apart from the agent's words; never the transcript.</param>
    /// <param name="coolOff">
    /// How long the account cools when the agent named no time this reads: the machine's <c>cooloff</c>
    /// (<see cref="DriverConfig.CoolOff"/>), or <see cref="AccountLimits.DefaultCoolOff"/> when none is handed over.
    /// </param>
    /// <param name="byCode">
    /// A limit the reader read by its code or kind with no sentence its table reads:
    /// cooled for the unstated default from now, <c>Stated: false</c>, where the sentence reads none. Null reads the sentence alone.
    /// </param>
    public (CoolingEntry Entry, LimitSeen Seen)? Limited(
        string adapter, string? profile, string? failure, string session, TimeSpan? coolOff = null, LimitSeen? byCode = null)
    {
        var resolved = roster.Adapters.Door(adapter);
        if (CoolingAgent(resolved) is not { } agent) return null;

        var now = roster.Clock();
        var read = AccountLimits.Read(LimitsOf(resolved.Id.Value), failure, now, roster.Zone, coolOff ?? AccountLimits.DefaultCoolOff)
                   ?? (byCode is { Stated: false } coded ? coded with { Until = now + (coolOff ?? AccountLimits.DefaultCoolOff) } : null);
        return read is { } seen ? Cool(agent, profile, seen, now, session, resolved.Id.Value) : null;
    }

    /// <summary>
    /// <see cref="Limited"/>'s trusted entry: a bound kind's limit, read whole by the agent layer's
    /// ingress under its bounds, cools its account with the same writes, reading no sentence. Null where the door cools no account.
    /// </summary>
    public (CoolingEntry Entry, LimitSeen Seen)? LimitedTrusted(string adapter, string? profile, LimitSeen seen, string session) =>
        CoolingAgent(roster.Adapters.Door(adapter)) is { } agent ? Cool(agent, profile, seen, roster.Clock(), session, adapter) : null;

    /// <summary>
    /// The writes a limit makes: the cooling entry, the account's set point, and a named weekly reset kept for the weeks after. A limit
    /// whose words name a model family the door's windows declare cools the account for that family alone, and tells
    /// nothing of the account's week.
    /// </summary>
    private (CoolingEntry Entry, LimitSeen Seen) Cool(string agent, string? profile, LimitSeen seen, DateTimeOffset now, string session, string door)
    {
        var model = ModelLimits.In(seen, WindowsOf(door)?.Models);
        var entry = new CoolingEntry(agent, profile, seen.Until, seen.Stated, seen.Window, now, session, seen.AssumedZone, seen.NotBelieved, model);
        // A cool-off the file will not take is held in memory, never lost to a strike.
        AccountCooling.Keep(roster.Home, entry, now);
        // A limit's sentence is a set point: the account's maker is asked once it is ready again, its reading older.
        if (profile is not null) roster._limitSaid[HarnessRoster.AccountKey(agent, profile)] = now;

        // A weekly reset the agent named is that account's for the weeks after: a start spends a
        // week about to lapse first. Only a named account's, since the tool's own sign-in is never in a list.
        if (profile is not null && model is null && seen.Stated && IsWeekly(seen))
        {
            try
            {
                AccountWindows.Told(roster.Home, agent, profile, seen.Until, now, session);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A week not kept costs a start's ranking, never the limit's cool-off.
            }
        }

        return (entry, seen);
    }

    /// <summary>
    /// Whether a limit's reset is its account's weekly one: the window its reset named, or, where it names none, what is
    /// hit (<i>You've hit your weekly limit · resets …</i>).
    /// </summary>
    private static bool IsWeekly(LimitSeen seen) =>
        string.Equals(seen.Window ?? seen.Hit, AccountWindows.Weekly, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// End an account's cool-off early: the person's <i>Try now</i>, since they may know the limit is raised.
    /// True when it is cooling; one that is not is told so, and nothing changes.
    /// </summary>
    public bool Ready(string adapter, string? profile) =>
        CoolingAgent(roster.Adapters.Door(adapter)) is { } agent && AccountCooling.End(roster.Home, agent, profile, roster.Clock());

    /// <summary>
    /// Whether this wait is new — an account, the cool-off it is waiting out (none where it waits for a person), and
    /// the accounts it passed not signed in — so it is written once.
    /// </summary>
    internal bool NewWait(string agent, string? account, DateTimeOffset? until, IReadOnlyList<string>? signedOut = null)
    {
        var key = HarnessRoster.AccountKey(agent, account);
        var now = $"{until?.UtcTicks}|{string.Join(",", (signedOut ?? []).Order(StringComparer.OrdinalIgnoreCase)).ToUpperInvariant()}";
        var said = _waited.TryGetValue(key, out var was) && was == now;
        _waited[key] = now;
        return !said;
    }

    /// <summary>
    /// A reading of one account, or with <paramref name="account"/> null the tool's own: kept under the home, so a
    /// restart starts from it; who, the organisation and the plan, in memory alone. A reading not
    /// written costs a restart's knowledge, never the answer.
    /// </summary>
    internal void Keep(string owner, string? account, LoginRead said, DateTimeOffset at)
    {
        var login = said.Login;
        roster._who[HarnessRoster.AccountKey(owner, account)] = (at, login == LoginState.In ? said : new LoginRead(login, null));
        // A newer word on the account settles a fresh reading a refusal owed it.
        roster._owed.TryRemove(HarnessRoster.AccountKey(owner, account), out _);
        // Its usage history's binding epoch: a sign-out marks it, and another plan as read begins the next.
        UsageHistory.Login(roster.Home, owner, account ?? AccountWindows.Own, login, login == LoginState.In ? said.Plan : null, at);
        try
        {
            AccountReads.Keep(roster.Home, owner, account, login, at);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The answer stands for its caller; the next press writes it again.
        }
    }

    /// <summary>
    /// An account removed from this machine: its reading goes with it, so one made later under its name starts never
    /// read.
    /// </summary>
    /// <param name="agent">Whose accounts: the accounts' owner.</param>
    public void Removed(string agent, string account)
    {
        roster._who.TryRemove(HarnessRoster.AccountKey(agent, account), out _);
        roster._owed.TryRemove(HarnessRoster.AccountKey(agent, account), out _);
        roster._fresh.TryRemove(HarnessRoster.AccountKey(agent, account), out _);
        // One made later under its name is another binding in the usage history.
        UsageHistory.Unbound(roster.Home, agent, account, roster.Clock());
        try
        {
            AccountReads.Forget(roster.Home, agent, account);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A reading not forgotten is replaced by the next one of an account made under its name.
        }
    }

    /// <summary>
    /// A person looked again: a refused account is let through, and the tool's own home's cool-off ends,
    /// since a sign-in there happens at their own terminal. What an account action and the roster's refresh have always
    /// done, now without asking any account.
    /// </summary>
    public void LookedAgain()
    {
        roster._refused.Clear();
        try
        {
            AccountCooling.EndOwnHomes(roster.Home, roster.Clock());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A cool-off that could not be ended costs a start's wait, never the roster.
        }
    }

    /// <summary>
    /// The agent's *Read again*: <paramref name="owner"/>'s own home's cool-off ends, nothing asked,
    /// as <see cref="LookedAgain"/> ends every agent's.
    /// </summary>
    internal void EndOwnCooling(string owner)
    {
        try
        {
            AccountCooling.End(roster.Home, owner, account: null, roster.Clock());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A cool-off that could not be ended costs a start's wait, never the press.
        }
    }
}
