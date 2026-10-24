using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What this machine's harnesses are, and which account a spawn runs as (D49 §4) — <b>one judgement
/// for both doors</b>, driven and chat, for the same reason <see cref="Planner"/> is pure and the
/// service has one ledger: two copies of "may this start" drift, and the drift is invisible.
/// </summary>
/// <remarks>
/// <para><b>An account is read only on a press, one at a time, or as the result of something that ran</b> (ROSTER1, D150
/// §5.3): never at the application's start, at a look, or when a view opens. A status question may refresh the account's
/// token, and probing on its own is what signed the owner's accounts out (TOOL6g). So the report is what was last read
/// (<see cref="AccountReads"/>, kept under the home, so a restart starts from it), each account with when, and the accounts
/// are the directories on disk now. Its binary is asked once a process, never against an account's home: whether it is
/// there and its version. A yes is trusted and a no is asked again: an absent agent's binary at once, and a signed-out
/// account on its own once its word is <see cref="SignedOutAskedAgain"/> old or a sign-in marked it (TOOL6g), since a start
/// held on it is tried at every look, or once after a refusal of a reading that said signed in (TOOL6h). An account's windows,
/// where its agent's server is asked them, are read at a press and at a start's walk too (CODEXUSE1, Harnesses.Usage.cs).</para>
///
/// <para><b>The wiring file is re-read, never held.</b> Same rule as `driver.json`: the file is the
/// truth and the surfaces are editors over it (D50), so a profile default changed from a terminal
/// takes effect on the next spawn without restarting anything.</para>
/// </remarks>
public sealed class HarnessRoster(AdapterSet adapters, string? settingsPath = null)
{
    // What each adapter's binary answered when last asked (ROSTER1): whether it is there, its version, why not. Asked of the
    // binary alone, once a process, and again at a press or where the binary may have changed.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Binary> _binaries =
        new(StringComparer.OrdinalIgnoreCase);

    private sealed record Binary(bool Present, string? Version, string? Problem);

    // Who each reading this process made said is signed in, with the organisation and the plan beside them (ACCOUNTID1), by
    // owner and account (its moment beside it): read fresh and written nowhere (D66 §3), so a reading kept from before this
    // process says its state and when, and who once read again.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTimeOffset At, LoginRead Said)> _who =
        new(StringComparer.OrdinalIgnoreCase);

    // Accounts a provider refused (AGT3b), by owner and profile — "" for the tool's own home — with
    // the sentence that says so. Held in memory until a person looks again; see `Refuse`.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Reason, Noted? Note)> _refused =
        new(StringComparer.OrdinalIgnoreCase);

    private static string AccountKey(string owner, string? profile) => $"{owner}/{profile ?? ""}";

    /// <summary>
    /// Hold further starts on an account its provider refused (AGT3b), with the sentence that says
    /// why and what fixes it.
    /// </summary>
    /// <remarks>
    /// 🔴 Measured: a refused key costs a session over three minutes of the tool's silent retries
    /// before it fails, and every further session on that account would do the same. It is the
    /// ACCOUNT's, so it holds both doors onto it (AGT7). It clears when a person looks again: any
    /// account action, or the roster's own refresh. A new sign-in or key is exactly such an action.
    /// </remarks>
    /// <param name="note">
    /// The reason by code (ROSTERREFUSE2), its English the reason to the byte: a hold on this account says it by code. Without it the
    /// hold keeps the reason's words.
    /// </param>
    public void Refuse(string adapter, string? profile, string reason, Noted? note = null)
    {
        var owner = adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase)
            ? Toolchain(adapter)?.Owner(adapters.Resolve(adapter).Name) ?? adapter
            : adapter;
        _refused[AccountKey(owner, profile)] = (reason, note is { } coded && coded.Note == reason ? coded : null);
    }

    // The waits already written to the machine log, by account, with the cool-off each was for and the accounts it passed
    // not signed in (TOOL4d, TOOL6g): a wait is written once, however many looks it lasts, and a new cool-off on the account,
    // or another account signed out or in, is a new wait.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _waited =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What time it is, for reading and writing cool-offs (TOOL4d); the system's, unless a test's.</summary>
    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>
    /// How a browser sign-in's port is tried before it starts (CODEXLOGIN1): taken for a moment on this machine
    /// (<see cref="SignInPorts.Hold"/>), unless a test's, since the ports Codex waits on are this machine's and a test that
    /// held them would race the person's own sign-in. Settable, as the loop builds its roster itself.
    /// </summary>
    public Func<int, PortHold> Ports { get; set; } = SignInPorts.Hold;

    /// <summary>
    /// How one account's status is asked on its own (TOOL6h): by owner and account, the agent's own status question under the
    /// account's lock, unless a test hands in an in-process stand-in, since every real question spawns the agent, which the
    /// suite's fast half may not. A stand-in's answer is a reading, kept as the agent's would be.
    /// </summary>
    internal Func<string, string, CancellationToken, Task<LoginState>>? Asking { get; init; }

    /// <summary>
    /// How long a start's walk waits for an account's lock (TOOL6g), held by a question in flight, before it leaves that account
    /// unknown for the walk (PARALLEL1c): its own status question's wait, and a counted start's wait to count the account it chose,
    /// which then counts nothing there. <see cref="ProbeLock.Patience"/>, unless a test's.
    /// </summary>
    internal TimeSpan LockPatience { get; init; } = ProbeLock.Patience;

    /// <summary>
    /// This machine's zone (D125 §2.1): it stands in for a zone a limit's sentence names that is not an IANA name, and
    /// every cool-off is said in it, with the zone named.
    /// </summary>
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;

    /// <summary>
    /// Whose accounts a cool-off on this adapter belongs to: its owner, as its accounts are (AGT7); null for an adapter
    /// that declares no toolchain, which has no accounts and reads no limit.
    /// </summary>
    private static string? CoolingAgent(ISessionAdapter resolved) => resolved.Toolchain?.Owner(resolved.Name);

    /// <summary>
    /// The words this adapter says an account's limit in (TOOL4a, D125 §1.3): its own entry, else its owner's, since a
    /// door's limits are its owner's as its accounts are (AGT7). Null reads every failure as a failure.
    /// </summary>
    public LimitWords? LimitsOf(string adapter)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain?.Limits is { } own) return own;
        return CoolingAgent(resolved) is { } owner
               && !string.Equals(owner, resolved.Name, StringComparison.OrdinalIgnoreCase)
               && adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? adapters.Resolve(owner).Toolchain?.Limits
            : null;
    }

    /// <summary>
    /// The words this adapter's agent refuses a start for its sign-in in (ROSTER1b): its own entry, else its owner's, since a
    /// door's refusals are its owner's as its accounts are (AGT7). Null reads every failure as a failure.
    /// </summary>
    public SignInWords? SignInOf(string adapter)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain?.SignIn is { } own) return own;
        return CoolingAgent(resolved) is { } owner
               && !string.Equals(owner, resolved.Name, StringComparison.OrdinalIgnoreCase)
               && adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? adapters.Resolve(owner).Toolchain?.SignIn
            : null;
    }

    /// <summary>
    /// A start on this adapter as <paramref name="profile"/> was refused for its sign-in (ROSTER1b, D150 §5.3): that account,
    /// or with <paramref name="profile"/> null the tool's own sign-in, reads signed out from now, kept as a reading is
    /// (<see cref="AccountReads"/>), so the next start walks past it as it walks past any account read signed out (TOOL6g) and a
    /// restart starts from it. Its owner's (AGT7), so every door onto the account reads it.
    /// </summary>
    /// <remarks>
    /// <para>Cleared as a reading signed out is: a sign-in or a key through either door marks the account, and the next start
    /// asks it again (<see cref="AskAgain"/>); a sign-in on the screen reads it as it ends; a press reads it; and the hour's
    /// backstop. A reading not written costs the next start one more refusal, never this one's record.</para>
    /// <para>🔴 <b>A refusal of an account last read signed in owes it one fresh reading</b> (TOOL6h): the refusal contradicts
    /// the agent's own last word, so the next start that walks to the account asks it once, whatever the mark and the hour say,
    /// and its answer is kept and honoured; a sign-in the person made since, at a door of Daoris's or not, runs it again. One:
    /// the refusal of a signed-in reading that fresh reading itself made owes none, so an agent that says signed in while it
    /// refuses every start costs one more start, never one per look. Never the tool's own sign-in, which only a person's press
    /// asks (TOOL6g). Held in memory, as <see cref="Refuse"/> is: after a restart the reading waits for the mark or the hour.</para>
    /// </remarks>
    public void SignedOut(string adapter, string? profile)
    {
        if (CoolingAgent(adapters.Resolve(adapter)) is not { } owner) return;
        var before = profile is null ? null : AccountReads.Of(Home, owner).Accounts.GetValueOrDefault(profile);
        Keep(owner, profile, new LoginRead(LoginState.Out, null), Clock());
        if (profile is not null && before is { Login: LoginState.In }
            && !(_fresh.TryGetValue(AccountKey(owner, profile), out var made) && made == before.At))
        {
            _owed[AccountKey(owner, profile)] = true;
        }
    }

    // The accounts a refusal of a signed-in reading owes one fresh reading (TOOL6h), by owner and account: taken by the next walk
    // that reaches the account, then spent. Any other reading of the account settles it (`Keep`).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _owed = new(StringComparer.OrdinalIgnoreCase);

    // The signed-in readings a fresh reading made (TOOL6h), by owner and account, with the time each was kept: a refusal of one
    // owes none.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _fresh =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How this adapter says an account's windows (TOOL6c, D130 §5.2): its own entry, else its owner's, since a door's readings
    /// are its owner's as its accounts are (AGT7). Null says nothing.
    /// </summary>
    public WindowWords? WindowsOf(string adapter)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain?.Windows is { } own) return own;
        return CoolingAgent(resolved) is { } owner
               && !string.Equals(owner, resolved.Name, StringComparison.OrdinalIgnoreCase)
               && adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? adapters.Resolve(owner).Toolchain?.Windows
            : null;
    }

    /// <summary>
    /// What a session's door said about the windows of the account it runs as (TOOL6c, D130 §5.2): read by the adapter's
    /// table and kept in <c>windows.json</c>, the newest reading of each window replacing the older, as the door carries it.
    /// Only a named account's, since the tool's own sign-in is never in a list (D125 §3.7). Null where nothing was kept.
    /// </summary>
    /// <param name="info">The frame's object, apart from the agent's words: never the transcript (D125 §1.4).</param>
    /// <remarks>
    /// Every window the frame says is kept in the usage history first (USAGECAP1a1, D179 point 2), the scoped weeks the walk
    /// does not read among them, at the moment <c>windows.json</c> keeps.
    /// </remarks>
    public IReadOnlyList<WindowReading>? Said(string adapter, string? profile, JsonElement info, string? session)
    {
        var resolved = adapters.Resolve(adapter);
        if (profile is null || CoolingAgent(resolved) is not { } agent || WindowsOf(resolved.Name) is not { } words) return null;

        var now = Clock();
        History(agent, profile, resolved.Name, AccountReadings.Seen(info, words), UsageHistory.SourceFrame, AccountReadings.HistoryReader, now, session);
        var readings = AccountReadings.Read(info, words);
        if (readings.Count == 0) return null;
        AccountWindows.Said(Home, agent, profile, readings, now, session);
        return readings;
    }

    /// <summary>
    /// A reader's windows kept in the usage history (USAGECAP1a1, D179 point 2), before <c>windows.json</c> keeps the newest of
    /// each: with the plan as last read, the answer's own where it named one, else the one this process's last status question
    /// of the account read (ACCOUNTID1, held in memory alone); never who signed in or the organisation. Never throws.
    /// </summary>
    /// <param name="account">The account, or <see cref="AccountWindows.Own"/> for the tool's own sign-in.</param>
    private void History(
        string owner, string account, string door, IReadOnlyList<WindowSeen> windows, string source, int reader, DateTimeOffset now,
        string? session, string? plan = null)
    {
        if (windows.Count == 0) return;
        var planFrom = plan is null ? null : source;
        if (plan is null && _who.TryGetValue(AccountKey(owner, account), out var who) && who.Said is { Login: LoginState.In, Plan: { } read })
        {
            plan = read;
            planFrom = UsageHistory.PlanFromStatus;
        }

        UsageHistory.Kept(Home, new ReadingFrom(owner, account, door, source, reader, now, session, plan, planFrom), windows, now);
    }

    /// <summary>The account's cool-off now, or null when it is ready, a start on this adapter as <paramref name="profile"/> would read it.</summary>
    public CoolingEntry? CoolingOf(string adapter, string? profile) =>
        CoolingAgent(adapters.Resolve(adapter)) is { } agent ? AccountCooling.Of(Home, agent, profile, Clock()) : null;

    /// <summary>
    /// Every account of the adapter's owner cooling now, earliest ready first (LIMITRESUME1c, D206 §3.2): what a conversation held by
    /// its limit computes its wait from. None for an adapter that has no accounts.
    /// </summary>
    public IReadOnlyList<CoolingEntry> CoolingsOf(string adapter) =>
        CoolingAgent(adapters.Resolve(adapter)) is { } agent
            ? [.. AccountCooling.Read(Home, Clock())
                .Where(entry => string.Equals(entry.Agent, agent, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Until)]
            : [];

    /// <summary>
    /// The door's failure, read for an account's limit (TOOL4d, D125 §1, §2): where the adapter's table recognises it, the
    /// account the session ran as cools until the reset the agent named, and what was read is returned. Null is a
    /// failure as today, and nothing is written.
    /// </summary>
    /// <param name="failure">What the door carried apart from the agent's words; never the transcript (D125 §1.4).</param>
    /// <param name="coolOff">
    /// How long the account cools when the agent named no time this reads: the machine's <c>cooloff</c>
    /// (<see cref="DriverConfig.CoolOff"/>, TOOL4e), or <see cref="AccountLimits.DefaultCoolOff"/> when none is handed over.
    /// </param>
    /// <param name="byCode">
    /// A limit the reader read by its code or kind with no sentence its table reads (AGENTERRORS1a, the agent-errors design §3.1, §5.2):
    /// cooled for D125's unstated default from now, <c>Stated: false</c>, where the sentence reads none. Null reads the sentence alone.
    /// </param>
    public (CoolingEntry Entry, LimitSeen Seen)? Limited(
        string adapter, string? profile, string? failure, string session, TimeSpan? coolOff = null, LimitSeen? byCode = null)
    {
        var resolved = adapters.Resolve(adapter);
        if (CoolingAgent(resolved) is not { } agent) return null;

        var now = Clock();
        var read = AccountLimits.Read(LimitsOf(resolved.Name), failure, now, Zone, coolOff ?? AccountLimits.DefaultCoolOff)
                   ?? (byCode is { Stated: false } coded ? coded with { Until = now + (coolOff ?? AccountLimits.DefaultCoolOff) } : null);
        if (read is not { } seen) return null;

        var entry = new CoolingEntry(agent, profile, seen.Until, seen.Stated, seen.Window, now, session, seen.AssumedZone, seen.NotBelieved);
        // LIMITRESUME1b (D206; D125 point 3 amended): a cool-off the file will not take is held in memory, never lost to a strike.
        AccountCooling.Keep(Home, entry, now);
        // A limit's sentence is a set point: the account's maker is asked once it is ready again, its reading older (USAGEPROBE1).
        if (profile is not null) _limitSaid[AccountKey(agent, profile)] = now;

        // A weekly reset the agent named is that account's for the weeks after (TOOL6b, D130 §16.3 step 4): a start spends a
        // week about to lapse first. Only a named account's, since the tool's own sign-in is never in a list (D125 §3.7).
        if (profile is not null && seen.Stated && IsWeekly(seen))
        {
            try
            {
                AccountWindows.Told(Home, agent, profile, seen.Until, now, session);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A week not kept costs a start's ranking, never the limit's cool-off.
            }
        }

        return (entry, seen);
    }

    /// <summary>
    /// Whether a limit's reset is its account's weekly one: the window its reset named, or, where it named none, what was
    /// hit (<i>You've hit your weekly limit · resets …</i>).
    /// </summary>
    private static bool IsWeekly(LimitSeen seen) =>
        string.Equals(seen.Window ?? seen.Hit, AccountWindows.Weekly, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// End an account's cool-off early: the person's <i>Try now</i>, since they may know the limit was raised (D125 §2.3).
    /// True when it was cooling; one that was not is told so, and nothing changes.
    /// </summary>
    public bool Ready(string adapter, string? profile) =>
        CoolingAgent(adapters.Resolve(adapter)) is { } agent && AccountCooling.End(Home, agent, profile, Clock());

    /// <summary>
    /// Whether this wait is new — an account, the cool-off it is waiting out (none where it waits for a person, TOOL6g), and
    /// the accounts it passed not signed in — so it is written once.
    /// </summary>
    internal bool NewWait(string agent, string? account, DateTimeOffset? until, IReadOnlyList<string>? signedOut = null)
    {
        var key = AccountKey(agent, account);
        var now = $"{until?.UtcTicks}|{string.Join(",", (signedOut ?? []).Order(StringComparer.OrdinalIgnoreCase)).ToUpperInvariant()}";
        var said = _waited.TryGetValue(key, out var was) && was == now;
        _waited[key] = now;
        return !said;
    }

    /// <summary>The adapters this roster answers for — the build's, plus whatever plugins declare (D64).</summary>
    public AdapterSet Adapters => adapters;

    /// <summary>
    /// Answer for a different set from now on — the built-in adapters plus the plugins a tick just
    /// read. What each binary answered is keyed by harness name and survives: a harness that was there
    /// before is the same harness, and one that just arrived has simply not been asked yet.
    /// </summary>
    public void Use(AdapterSet live) => adapters = live;

    /// <summary>Where the wiring lives. The profile tree sits beside it.</summary>
    public string SettingsPath { get; } = settingsPath ?? HarnessSettings.ResolvePath();

    /// <summary>The directory profiles live under — the Daoris home (D63), which is the directory the wiring file sits in.</summary>
    public string Home => HarnessSettings.HomeOf(SettingsPath);

    /// <summary>
    /// The person's harness wiring as every reader takes it (POOL1a2, the account-pool design §2.4): the file on disk right now,
    /// projected to one set of Daoris's accounts per agent (<see cref="HarnessSettings.LoadSet"/>), so a file an older build wrote
    /// meanwhile is read as the set too and no start reads a workspace's scope. The come-up folds the file itself (<see cref="Pool"/>),
    /// and every later write folds first.
    /// </summary>
    public HarnessSettings Settings => HarnessSettings.LoadSet(SettingsPath, Agents);

    /// <summary>
    /// The agents whose accounts this roster answers for (POOLSTRAY1, <see cref="AdapterSet.Agents"/>): what every write of the wiring
    /// through it hands <see cref="HarnessSettings.Change"/>, so a folder under the account homes none of them names is never folded.
    /// </summary>
    public IReadOnlyList<string> Agents => adapters.Agents;

    private bool _pooled;

    /// <summary>
    /// The come-up's fold (the account-pool design §6.2): <see cref="HarnessSettings.FoldNow"/> at the first look, and at each next look
    /// while a held lock refused it, until one lands; nothing waits on it, since every reader reads the projection meanwhile.
    /// </summary>
    public void Pool(MachineLog? log)
    {
        if (_pooled) return;
        _pooled = HarnessSettings.FoldNow(SettingsPath, log, agents: Agents);
    }

    /// <summary>The harnesses this build knows, whether or not they are installed.</summary>
    public IReadOnlyList<string> Known => adapters.Names;

    /// <summary>This harness's toolchain, or null for an adapter that declares none.</summary>
    public HarnessToolchain? Toolchain(string adapter) => adapters.Resolve(adapter).Toolchain;

    /// <summary>
    /// Which door this adapter holds a session over (D53) — what a surface calls the *tool's* way in
    /// rather than a tool of its own.
    /// </summary>
    /// <remarks>
    /// 🔴 Added for the roster surface after the owner read it as a catalogue of five tools
    /// (2026-09-22: *"`*-acp` really confusing of the scope of this project"*). It is not five tools;
    /// it is three, two of which can be reached two ways. The page cannot say so without knowing
    /// which entry is a door and which is a tool, and <see cref="HarnessToolchain.AccountOf"/>
    /// already answers the second half — this answers the first.
    /// </remarks>
    public SessionWire Wire(string adapter) => adapters.Resolve(adapter).Wire;

    /// <summary>
    /// Whether this harness's door carries a conversation's STRUCTURE (D76 §1): the protocol door, or a
    /// native door whose adapter reads the harness's own structured output (CONV3a).
    /// </summary>
    /// <remarks>
    /// The page reads it to know what an empty record means — nothing said yet on a structured door, a
    /// conversation that lives in the console on a text one — rather than guessing from the emptiness,
    /// which told a fresh chat on a structured door that its door carries only text (CONV3b).
    /// </remarks>
    public bool Structured(string adapter) => Structured(adapters.Resolve(adapter));

    /// <inheritdoc cref="Structured(string)"/>
    public static bool Structured(ISessionAdapter adapter) =>
        adapter.Wire == SessionWire.Acp || adapter.StructuredOutput() is not null;

    /// <summary>
    /// One harness as this machine has it: what was last read of each of its accounts and its own sign-in, with when, and
    /// its binary. Without a press nothing is asked of an account (ROSTER1, D150 §5.3); <paramref name="refresh"/> is the
    /// person's press, reading every account one at a time, or with <paramref name="account"/> that account alone.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>No account is asked to answer a look</b>, at a cold cache either: a start, a panel, the page's roster and a
    /// restart read <see cref="AccountReads"/>, so the first roster read after the application starts asks nothing, where it
    /// asked every named account (UX6e). The binary is asked its version once a process, which is a question of the binary
    /// and never of an account's home (§5.3).</para>
    /// <para>🔴 <b>One press of a harness at a time</b> (TOOL6g): a caller that finds one in flight takes its answer. Each
    /// account inside it is asked under its <see cref="ProbeLock"/>, and one a session of Daoris's runs on is not asked and
    /// keeps its word and its time. A press is a person looking again, so it lets a refused account of that agent through
    /// (AGT3b), and ends the tool's own home's cool-off (D125 §3.7) without asking it.</para>
    /// <para>🔴 <b>The tool's own sign-in is never asked</b> (POOL1a3, D197 point 3): it is the person's, and the agent may
    /// refresh their token to answer, beside their own terminal's. Its kept reading is said as it was.</para>
    /// </remarks>
    /// <param name="account">With <paramref name="refresh"/>, the one account a press reads: an account's own *Read again*.</param>
    public async Task<HarnessReport?> ReportAsync(
        string adapter, DriverConfig config, bool refresh = false, CancellationToken ct = default, string? account = null)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain is not { } toolchain) return null;
        var name = resolved.Name;
        var owner = toolchain.Owner(name);

        if (refresh && account is not null)
        {
            var named = HarnessSettings.Profiles(Home, owner).FirstOrDefault(each => string.Equals(each, account, StringComparison.OrdinalIgnoreCase))
                ?? throw new DriverException(
                    $"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                    + (HarnessSettings.Profiles(Home, owner) is { Count: > 0 } accounts ? string.Join(", ", accounts) : "(none)"));
            _refused.TryRemove(AccountKey(owner, named), out _);
            // Asked the way its owner asks when this build carries the owner (AGT7): a door declares no login question.
            var asker = AccountAgent(name, toolchain);
            await AskOneAsync(asker.Name, asker.Toolchain, owner, named, config, ct).ConfigureAwait(false);
            // And its windows, where its agent's server is asked them (CODEXUSE1).
            await AskUsageAsync(name, owner, named, config, ct).ConfigureAwait(false);
        }
        else if (refresh)
        {
            foreach (var refused in _refused.Keys.Where(key => key.StartsWith(owner + "/", StringComparison.OrdinalIgnoreCase)))
            {
                _refused.TryRemove(refused, out _);
            }

            // The own home's cool-off ends at the agent's *Read again* as at the roster's (D197's acceptance note): a sign-in
            // there happens at the person's terminal, which Daoris does not see, and the cool-off's line sends them here.
            EndOwnCooling(owner);
            await Fly($"{name}/press", () => PressAsync(name, toolchain, owner, config), ct).ConfigureAwait(false);
        }

        return Composed(name, toolchain, await BinaryOfAsync(name, toolchain, config, again: false, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Ask an adapter's binary again whether it is there and its version, and no account (ROSTER1): what an install, an
    /// update or a pin changes, and what a start held on an absent agent asks before it refuses.
    /// </summary>
    public async Task BinaryChangedAsync(string adapter, DriverConfig config, CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain is not { } toolchain) return;
        await BinaryOfAsync(resolved.Name, toolchain, config, again: true, ct).ConfigureAwait(false);
    }

    /// <summary>The person's press on one agent: every account of Daoris's asked one at a time, each answer kept; never the own sign-in.</summary>
    private async Task PressAsync(string name, HarnessToolchain toolchain, string owner, DriverConfig config)
    {
        var prior = Composed(name, toolchain, new Binary(true, null, null));
        // 🔴 A door onto an agent this build carries no door of (CODEXACCT1: `codex-acp`, `codex`) asks its binary as itself,
        // and its accounts as that agent asks them, with the agent's own binary: no press of the agent's would, and the door
        // has no status question.
        var holder = HolderOf(name, toolchain);
        var report = await HarnessProbe.ProbeAsync(
            name, toolchain, config.Commands.GetValueOrDefault(name), Settings, Home, CancellationToken.None,
            busy: account => Busy(owner, account), prior: prior,
            asks: holder is null ? null : _ => false, clock: Clock,
            answered: (account, said, at) => Keep(owner, account, said, at)).ConfigureAwait(false);
        _binaries[name] = new Binary(report.Present, report.Version, report.Problem);
        if (holder is { } agent)
        {
            await HarnessProbe.ProbeAsync(
                agent.Name, agent.Toolchain, config.Commands.GetValueOrDefault(agent.Name), Settings, Home, CancellationToken.None,
                busy: account => Busy(owner, account), prior: prior, clock: Clock,
                answered: (account, said, at) => Keep(owner, account, said, at)).ConfigureAwait(false);
        }
        // And each account's windows, where its agent's server is asked them (CODEXUSE1); the own sign-in's never (D197 point 3).
        await PressUsageAsync(name, owner, config, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>What the binary last answered, asked once a process, or again where <paramref name="again"/>; never an account.</summary>
    private async Task<Binary> BinaryOfAsync(string name, HarnessToolchain toolchain, DriverConfig config, bool again, CancellationToken ct)
    {
        if (!again && _binaries.TryGetValue(name, out var known)) return known;

        await Fly($"{name}/binary", async () =>
        {
            var report = await HarnessProbe.ProbeAsync(
                name, toolchain, config.Commands.GetValueOrDefault(name), Settings, Home, CancellationToken.None,
                asks: _ => false).ConfigureAwait(false);
            _binaries[name] = new Binary(report.Present, report.Version, report.Problem);
        }, ct).ConfigureAwait(false);
        return _binaries.TryGetValue(name, out var asked) ? asked : new Binary(false, null, null);
    }

    /// <summary>
    /// One harness's report from what is known (ROSTER1): the accounts on disk now, each with what was last read of it and
    /// when, the tool's own sign-in likewise, and the binary's last answer. Nothing is asked.
    /// </summary>
    private HarnessReport Composed(string name, HarnessToolchain toolchain, Binary binary)
    {
        var owner = toolchain.Owner(name);
        var reads = AccountReads.Of(Home, owner);
        var names = AccountNames.Of(Home, owner);
        var profiles = HarnessSettings.Profiles(Home, owner).Select(account =>
        {
            var read = reads.Accounts.GetValueOrDefault(account);
            var held = HarnessKeys.Of(Home, owner, account);
            var who = WhoOf(owner, account, read);
            return new ProfileReport(
                account, HarnessSettings.ProfileHome(Home, owner, account), read?.Login ?? LoginState.Unknown,
                who?.Account, held is null ? null : HarnessKeys.Handle(held), read?.At,
                names.GetValueOrDefault(account), who?.Org, who?.Plan);
        }).ToList();

        // HELPSTUCK1: the folders beside the accounts that are no account, each with whether Daoris kept a sign-in of it while it
        // was listed as one, so a sign-in in a lock's folder is said rather than lost.
        var strays = HarnessSettings.Strays(Home, owner).Select(stray => new StrayFolder(
            stray,
            reads.Accounts.GetValueOrDefault(stray)?.Login == LoginState.In
            || ProbeLock.SignedIn(Home, owner, stray) is not null
            || HarnessKeys.Of(Home, owner, stray) is not null)).ToList();

        var own = WhoOf(owner, null, reads.Own);
        return new HarnessReport(
            name,
            binary.Present,
            binary.Present ? binary.Version : null,
            binary.Present ? null : binary.Problem,
            toolchain.ProfileVariable,
            // The order's first, where the machine's default was (POOL1a2, the account-pool design §2.2).
            Settings.FirstOf(owner),
            profiles,
            reads.Own?.Login ?? LoginState.Unknown,
            own?.Account,
            reads.Own?.At,
            own?.Org,
            own?.Plan)
        {
            Strays = strays,
        };
    }

    /// <summary>
    /// Who the reading this process made of an account said is signed in, with the organisation and the plan beside them
    /// (ACCOUNTID1), where that reading is the one kept and said yes; null otherwise.
    /// </summary>
    private LoginRead? WhoOf(string owner, string? account, AccountRead? read) =>
        read is not null && _who.TryGetValue(AccountKey(owner, account), out var said) && Second(said.At) == read.At
        && said.Said.Login == LoginState.In
            ? said.Said
            : null;

    private static DateTimeOffset Second(DateTimeOffset at) =>
        new(at.UtcTicks - (at.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

    /// <summary>
    /// A reading of one account, or with <paramref name="account"/> null the tool's own (ROSTER1): kept under the home, so a
    /// restart starts from it; who, the organisation and the plan, in memory alone (D66 §3, ACCOUNTID1). A reading not
    /// written costs a restart's knowledge, never the answer.
    /// </summary>
    private void Keep(string owner, string? account, LoginRead said, DateTimeOffset at)
    {
        var login = said.Login;
        _who[AccountKey(owner, account)] = (at, login == LoginState.In ? said : new LoginRead(login, null));
        // A newer word on the account settles a fresh reading a refusal owed it (TOOL6h).
        _owed.TryRemove(AccountKey(owner, account), out _);
        // Its usage history's binding epoch (USAGECAP1a1): a sign-out marks it, and another plan as read begins the next.
        UsageHistory.Login(Home, owner, account ?? AccountWindows.Own, login, login == LoginState.In ? said.Plan : null, at);
        try
        {
            AccountReads.Keep(Home, owner, account, login, at);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The answer stands for its caller; the next press writes it again.
        }
    }

    /// <summary>
    /// An account removed from this machine (D66 §3): its reading goes with it, so one made later under its name starts never
    /// read (ROSTER1).
    /// </summary>
    /// <param name="agent">Whose accounts: the accounts' owner (AGT7).</param>
    public void Removed(string agent, string account)
    {
        _who.TryRemove(AccountKey(agent, account), out _);
        _owed.TryRemove(AccountKey(agent, account), out _);
        _fresh.TryRemove(AccountKey(agent, account), out _);
        // One made later under its name is another binding in the usage history (USAGECAP1a1).
        UsageHistory.Unbound(Home, agent, account, Clock());
        try
        {
            AccountReads.Forget(Home, agent, account);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A reading not forgotten is replaced by the next one of an account made under its name.
        }
    }

    /// <summary>
    /// A person looked again (AGT3b, D125 §3.7): a refused account is let through, and the tool's own home's cool-off ends,
    /// since a sign-in there happens at their own terminal. What an account action and the roster's refresh have always
    /// done, now without asking any account (ROSTER1).
    /// </summary>
    public void LookedAgain()
    {
        _refused.Clear();
        try
        {
            AccountCooling.EndOwnHomes(Home, Clock());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A cool-off that could not be ended costs a start's wait, never the roster.
        }
    }

    /// <summary>
    /// The agent's *Read again* (POOL1a3, D197's acceptance note): <paramref name="owner"/>'s own home's cool-off ends, nothing asked,
    /// as <see cref="LookedAgain"/> ends every agent's.
    /// </summary>
    private void EndOwnCooling(string owner)
    {
        try
        {
            AccountCooling.End(Home, owner, account: null, Clock());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A cool-off that could not be ended costs a start's wait, never the press.
        }
    }

    /// <summary>
    /// Run <paramref name="work"/> unless one under <paramref name="flight"/> is in flight, whose end is then awaited instead
    /// (TOOL6g): its own task, never a caller's token, so a caller that stops waiting leaves the others their answer.
    /// </summary>
    private async Task Fly(string flight, Func<Task> work, CancellationToken ct)
    {
        Task running;
        lock (_probing)
        {
            if (!_probing.TryGetValue(flight, out running!))
            {
                running = Task.Run(work, CancellationToken.None);
                _probing[flight] = running;
                var started = running;
                _ = started.ContinueWith(_ =>
                {
                    lock (_probing)
                    {
                        if (_probing.TryGetValue(flight, out var current) && current == started) _probing.Remove(flight);
                    }
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        await running.WaitAsync(ct).ConfigureAwait(false);
    }

    // What is in flight for each harness, by name and kind — a press, with or without the own sign-in, or its binary
    // (TOOL6g): one at a time, its answer shared by every caller that came. Two flights never ask one account at once: each
    // account has its lock.
    private readonly Dictionary<string, Task> _probing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How long the agent's word that an account is signed out is believed when nothing says it changed (TOOL6g): a
    /// backstop for a sign-in no door of Daoris's made. Before TOOL6g, a start held on a signed-out account asked every
    /// account's status, and the tool's own sign-in's, at every look.
    /// </summary>
    public static readonly TimeSpan SignedOutAskedAgain = TimeSpan.FromHours(1);

    // When one account's status was last asked on its own (TOOL6g), by owner and account.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _askedAt =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the agent's word that an account is signed out is asked again, that account alone (TOOL6g): once per sign-out.
    /// The word is the later of its last reading (<paramref name="read"/>, kept under the home, ROSTER1) and its own last
    /// question; it stands until a sign-in or a key into the account at the terminal marks it
    /// (<see cref="ProbeLock.SignedInPathOf"/>) after the word, or until the word is <see cref="SignedOutAskedAgain"/> old.
    /// A sign-in on the screen reads that account as it ends, and a press reads it.
    /// </summary>
    private bool AskAgain(string owner, string account, DateTimeOffset? read)
    {
        var said = read ?? DateTimeOffset.MinValue;
        if (_askedAt.TryGetValue(AccountKey(owner, account), out var alone) && alone > said) said = alone;
        return ProbeLock.SignedIn(Home, owner, account) is { } marked && marked > said
               || Clock() - said >= SignedOutAskedAgain;
    }

    /// <summary>
    /// Ask one account's status, under its lock, with its key where it is a key (AGT3), and keep the answer under the home
    /// (ROSTER1), so the next start, the screen's next start (<see cref="Next"/>) and a restart read it too: a start asking a
    /// signed-out account again (TOOL6g), and an account's own *Read again*. One a session of Daoris's runs on is not asked,
    /// and keeps its word and its time. Its lock is waited for <see cref="LockPatience"/>: one not had is said
    /// (<see cref="HarnessProbe.LoginAnswer.Locked"/>), so a walk leaves the account unknown for itself (PARALLEL1c).
    /// </summary>
    private async Task<HarnessProbe.LoginAnswer> AskOneAsync(
        string adapter, HarnessToolchain toolchain, string owner, string account, DriverConfig config, CancellationToken ct)
    {
        var key = toolchain.KeyVariable is { Length: > 0 } variable && HarnessKeys.Of(Home, owner, account) is { } held
            ? new Dictionary<string, string> { [variable] = held }
            : null;
        var before = AccountReads.Of(Home, owner).Accounts.GetValueOrDefault(account);
        var answer = Asking is { } standIn
            ? new HarnessProbe.LoginAnswer(await standIn(owner, account, ct).ConfigureAwait(false), null, true)
            : await HarnessProbe.AskAccountAsync(
                adapter, toolchain, config.Commands.GetValueOrDefault(adapter), Settings, Home,
                HarnessSettings.ProfileHome(Home, owner, account), ct, ProbeLock.PathOf(Home, owner, account), key,
                busy: () => Busy(owner, account), kept: new LoginRead(before?.Login ?? LoginState.Unknown, null),
                patience: LockPatience).ConfigureAwait(false);
        // A mark this question answered is spent, whatever the file's clock says against the roster's.
        var asked = Clock();
        if (ProbeLock.SignedIn(Home, owner, account) is { } marked && marked > asked) asked = marked;
        _askedAt[AccountKey(owner, account)] = asked;

        if (answer.Asked) Keep(owner, account, answer.Said, Clock());
        return answer;
    }

    /// <summary>
    /// Whether a session of Daoris's runs on an account of <paramref name="owner"/>'s now (TOOL6g): a running record the last
    /// look read, or a start chosen on it since; <paramref name="account"/> null is the tool's own sign-in. Its own process
    /// may refresh the account's token, so no probe asks it then.
    /// </summary>
    internal bool Busy(string owner, string? account)
    {
        lock (_load)
        {
            return _looked.Any(each => each.Session.Running
                                       && string.Equals(each.Owner, owner, StringComparison.OrdinalIgnoreCase)
                                       && string.Equals(each.Session.Profile ?? "", account ?? "", StringComparison.OrdinalIgnoreCase))
                   || (account is not null && _chosen.Any(start =>
                       string.Equals(start.Owner, owner, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(start.Account, account, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// What the harness says about ONE of its profiles — the question a sign-in asks as it ends
    /// (D66 §3), answered without probing every other account to learn it, and kept as that account's reading (ROSTER1):
    /// who signed in, with the organisation and the plan beside them (ACCOUNTID1).
    /// </summary>
    public async Task<LoginRead> LoginOfAsync(
        string adapter, DriverConfig config, string profile, CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain is not { } toolchain) return new LoginRead(LoginState.Unknown, null);

        // A door's account is its owner's (AGT7), asked the way the owner asks when this build has it.
        var owner = toolchain.Owner(resolved.Name);
        var asker = AccountAgent(resolved.Name, toolchain);
        var answer = await HarnessProbe.AskAccountAsync(
            asker.Name, asker.Toolchain, config.Commands.GetValueOrDefault(asker.Name), Settings, Home,
            HarnessSettings.ProfileHome(Home, owner, profile), ct, ProbeLock.PathOf(Home, owner, profile), key: null).ConfigureAwait(false);
        if (answer.Asked) Keep(owner, profile, answer.Said, Clock());
        return answer.Said;
    }

    /// <summary>The toolchain that answers for this adapter's accounts (AGT7) — its owner's, when carried or declared.</summary>
    public HarnessToolchain? AccountToolchain(string adapter) => AccountAgentOf(adapter)?.Toolchain;

    /// <summary>
    /// The agent that answers for this adapter's accounts, by name and toolchain (AGT7, CODEXACCT1): what a sign-in on the
    /// door runs, with that agent's configured command, and what its accounts are asked with. Null for an adapter that
    /// declares no toolchain.
    /// </summary>
    public (string Name, HarnessToolchain Toolchain)? AccountAgentOf(string adapter) =>
        Toolchain(adapter) is { } toolchain ? AccountAgent(adapters.Resolve(adapter).Name, toolchain) : null;

    /// <summary>
    /// What a sign-in on this door runs as (CODEXACCT1): its account agent's binary, resolved as that agent's status question
    /// resolves it (<see cref="HarnessProbe.CommandOf"/>, D57 rule 4): the command named for it, its pin, else <c>PATH</c>'s.
    /// The install's only <c>codex</c> is its pin, so a sign-in on <c>PATH</c>'s could not start there. Null for an
    /// adapter that declares no toolchain.
    /// </summary>
    public HarnessCommand? SignInCommand(string adapter, DriverConfig config) =>
        AccountAgentOf(adapter) is { } agent
            ? HarnessProbe.CommandOf(agent.Name, agent.Toolchain, config.Commands.GetValueOrDefault(agent.Name), Settings, Home)
            : null;

    /// <summary>
    /// Which agent answers for a door's ACCOUNTS (AGT7): its owner when this build carries a door of it — the owner has the
    /// login question and the key variable — else the owner as the set declares it where it carries no door of it
    /// (<see cref="AdapterSet.Holder"/>, CODEXACCT1: <c>codex</c> for <c>codex-acp</c>), else the door itself.
    /// </summary>
    /// <remarks>
    /// 🔴 A declared owner is no adapter: its name is never handed to <see cref="ReportAsync"/> or a press, which resolve
    /// adapters. <see cref="HolderOf"/> says which agent that is.
    /// </remarks>
    private (string Name, HarnessToolchain Toolchain) AccountAgent(string name, HarnessToolchain toolchain)
    {
        var owner = toolchain.Owner(name);
        if (string.Equals(owner, name, StringComparison.OrdinalIgnoreCase)) return (name, toolchain);
        if (adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase) && adapters.Resolve(owner).Toolchain is { } ownerToolchain)
        {
            return (adapters.Resolve(owner).Name, ownerToolchain);
        }

        return adapters.Holder(owner) is { } holder ? (owner, holder) : (name, toolchain);
    }

    /// <summary>
    /// The agent a door asks its accounts through where this build carries no door of it (CODEXACCT1), or null where the
    /// door answers for its accounts itself or its owner's own door does: what a press on the door asks with, since no
    /// press of the owner's would.
    /// </summary>
    private (string Name, HarnessToolchain Toolchain)? HolderOf(string name, HarnessToolchain toolchain)
    {
        var agent = AccountAgent(name, toolchain);
        return string.Equals(agent.Name, name, StringComparison.OrdinalIgnoreCase)
               || adapters.Names.Contains(agent.Name, StringComparer.OrdinalIgnoreCase)
            ? null
            : agent;
    }

    /// <summary>The adapter whose report says a door's accounts (AGT7): its owner's door where this build carries one, else itself.</summary>
    private string ReportingDoor(string name, HarnessToolchain toolchain) =>
        AccountAgent(name, toolchain).Name is var agent && adapters.Names.Contains(agent, StringComparer.OrdinalIgnoreCase) ? agent : name;

    /// <summary>Every harness this build knows, probed — what a roster surface renders.</summary>
    /// <remarks>
    /// 🔴 <b>A door with nothing to run on this machine is not on the roster.</b> The stub's toolchain
    /// has no binary of its own — the "binary" is whatever `driver.json` names, which is what lets
    /// the rehearsal gate the roster with no model in it — and a deployed application names nothing,
    /// so it listed an agent tool called <c>stub</c> with an install button and nothing to install.
    /// Structural rather than a name: name a command for it and it is a real door again. The spawn
    /// path is untouched — <see cref="SelectAsync"/> still answers for any adapter it is asked about.
    /// </remarks>
    public async Task<IReadOnlyList<HarnessReport>> RosterAsync(
        DriverConfig config, bool refresh = false, CancellationToken ct = default)
    {
        // A person looking again is asking to try again (AGT3b): a refused account is let through. And the tool's own
        // home's cool-off ends (D125 §3.7): it means whoever was signed in there when the limit came, and a sign-in at
        // the person's own terminal since is not something Daoris sees. A named account's reset stands.
        if (refresh) LookedAgain();

        var reports = new List<HarnessReport>();
        foreach (var name in Known)
        {
            if (adapters.Resolve(name).Toolchain is { Binary.Count: 0 }
                && config.Commands.GetValueOrDefault(name) is not { Count: > 0 })
            {
                continue;
            }

            // The tool's own sign-in is never asked, at a press either (POOL1a3, D197 point 3; Fable's F2): it is theirs, and
            // their terminal and their other sessions run on it.
            if (await ReportAsync(name, config, refresh, ct).ConfigureAwait(false) is { } report)
            {
                reports.Add(report);
            }
        }

        return reports;
    }

    /// <summary>
    /// May a session spawn on this harness, and as which account? The two refusals mirror each other
    /// deliberately — a missing binary and a logged-out profile are the same kind of answer, and each
    /// <b>names the action that fixes it</b> rather than failing bare.
    /// </summary>
    /// <remarks>
    /// <para><b>One set</b> (POOL1a2, D197 points 1 and 2; the account-pool design §2.4): every workspace's start reads the agent's
    /// set (<see cref="HarnessSettings.Set"/>), its whole order begun at the order's first. A pick is the one account, never moved;
    /// an agent with no account of Daoris's holds its start for a sign-in.</para>
    /// <para><b>The walk</b>: <see cref="AccountRotation.Order"/>, by <c>use</c> — the goal's (the default) or D125's —
    /// and the first account still ready runs: not cooling, not refused, not signed out. It says which step chose it
    /// (<see cref="HarnessSelection.Choice"/>) and, where it ran somewhere other than where its scope begins, why
    /// (<see cref="HarnessSelection.Rotated"/>). When no account is ready the start waits: before any probe while each is
    /// cooling or refused, with one sentence naming the first reset.</para>
    /// <para><b>Counted as chosen</b>: the goal reads Daoris's sessions as of the driver's last look (<see cref="Look"/>)
    /// and every start chosen since, so starts in one look spread. A start's choice and its count are made one at a time,
    /// under a gate no question is asked under (PARALLEL1c, D181 point 3): a question on one account never holds a start on
    /// another, and an account whose lock stays held past its patience is left unknown for that walk, never counted.</para>
    /// </remarks>
    /// <param name="workspace">The repository's circle, for its pinned version (D49 §4); every workspace reads the one set.</param>
    /// <param name="chosen">The person's pick for this session, when they made one.</param>
    /// <param name="kind">Driven work or a conversation: driven work never starts on the scope's kept account (§4.6).</param>
    /// <param name="model">
    /// The model the start asks (D205 §4.1, MODELROLE1c2): each account's offer reading is judged for it (<see cref="AgentOffers"/>), and
    /// one that does not offer it or refused it is passed for this start, never cooled; null for account default, which no reading passes.
    /// </param>
    /// <param name="passed">The accounts this start already passed for this model (§4.1's per-start set): never a cool-off, never stored.</param>
    /// <param name="except">The start's own count (§4.1's re-ask), left out of the accounts' facts so its own load does not bias the order.</param>
    public Task<HarnessSelection> SelectAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen,
        StartKind kind = StartKind.Driven, CancellationToken ct = default, string? model = null,
        IReadOnlyCollection<string>? passed = null, long except = 0, bool counts = true) =>
        WalkAsync(adapter, config, workspace, chosen, kind, counts, ownHome: false, ct, new OfferAsk(model, passed, except));

    /// <summary>
    /// A handed quest's start (MIXAGENT1a-ii, D163 point 1): the account the hand-over named is asked for first, as a person's pick
    /// is. Where it cannot run (cooling, refused, signed out, gone), the walk goes on within the SAME agent's accounts, as for any
    /// start, and never back to the agent the quest was handed from; the answer says which account it passed, so the start's note can.
    /// </summary>
    /// <param name="account">The account the hand-over named, or null for the walk alone.</param>
    /// <returns>The selection, and the account named that could not run it, or null where it did or none was named.</returns>
    /// <param name="model">The model the start asks, its accounts judged by their readings for it (<see cref="SelectAsync"/>).</param>
    /// <param name="passed">The accounts this start already passed for that model.</param>
    public async Task<(HarnessSelection Selection, string? Passed)> SelectHandedAsync(
        string adapter, DriverConfig config, string? workspace, string? account, StartKind kind, CancellationToken ct = default,
        string? model = null, IReadOnlyCollection<string>? passed = null, long except = 0, bool counts = true)
    {
        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        if (named is null) return (await SelectAsync(adapter, config, workspace, chosen: null, kind, ct, model, passed, except, counts).ConfigureAwait(false), null);

        var asked = await SelectAsync(adapter, config, workspace, chosen: named, kind, ct, model, passed, except, counts).ConfigureAwait(false);
        if (asked.Allowed) return (asked, null);

        return (await SelectAsync(adapter, config, workspace, chosen: null, kind, ct, model, passed, except, counts).ConfigureAwait(false), named);
    }

    /// <summary>
    /// The account a resume runs on (MSG1g, D137 §2.2's account paragraph): its record's own, asked for by name as a chat's
    /// picker names one, since its conversation lives in that account's configuration home (D131 §1); never the walk's choice
    /// for a start. Judged by <see cref="ResumeAccount.Judge"/> with what this roster knows of the account before any probe.
    /// </summary>
    /// <remarks>
    /// <para><b>Ready</b>, it is selected as a pick is, so a sign-in the agent last said is gone is learned from that reading
    /// (ROSTER1). <b>Cooling</b>, the resume is held with its cool-off, nothing counted, so a look says the wait once and a screen
    /// shows it waiting for an account (TOOL4g), unless the person chose a new session (<paramref name="newSession"/>).
    /// <b>Unable to run there at all</b> (gone, off the list, kept for conversations, refused, signed out), or left by the
    /// person's choice, the walk picks as for any start (D130 §16.3), which passes the account by itself; where
    /// <paramref name="walks"/> is false, as for a conversation that nothing carries on by itself, nothing is walked or
    /// counted, and the answer is a refusal saying what held it.</para>
    /// </remarks>
    /// <param name="account">The account the record ran on, or null for the tool's own sign-in.</param>
    /// <param name="newSession">The person chose to go on in a new session rather than wait for a cooling account.</param>
    /// <param name="walks">Whether an account that cannot carry the words is replaced by the walk's pick.</param>
    /// <param name="limited">
    /// The record an account's limit ended (LIMITRESUME1b, D206; the limit-resume design §3.3): its account, ready, has its maker's reading
    /// asked first as the walk asks a just-reset account, a reading still out held as cooling; and, cooling, its words never wait for it
    /// (<see cref="ResumeChoice.Limited"/>), since they go where the line goes.
    /// </param>
    /// <param name="model">
    /// The record's asked model (D205 §3.5(a), MODELROLE1c2): an own account whose reading refuses it cannot carry the words, which go to
    /// a new record on another account of the same choice, the own account passed, as an own account signed out does.
    /// </param>
    public async Task<ResumeChoice> ResumeAsync(
        string adapter, DriverConfig config, string? workspace, string? account, StartKind kind, bool newSession = false,
        bool walks = true, CancellationToken ct = default, bool limited = false, string? model = null)
    {
        var resolved = adapters.Resolve(adapter);
        if (resolved.Toolchain is not { } toolchain)
        {
            // An adapter with no accounts has nothing to ask for: it spawns as it always did.
            return new ResumeChoice(await SelectAsync(adapter, config, workspace, chosen: null, kind, ct).ConfigureAwait(false), NextHold.Ready);
        }

        // The own account's reading judged for the asked model before anything is asked of it (D205 §3.5(a)): one that refuses it is an
        // account that cannot carry the words, never ended or cooled.
        var unoffered = !string.IsNullOrWhiteSpace(account) && model is not null
            && AgentOffers.Judge(AgentOffers.Read(Home), resolved.Name, (await ReportAsync(resolved.Name, config, refresh: false, ct).ConfigureAwait(false))?.Version,
                account.Trim(), model, Clock()) is not null;

        var owner = toolchain.Owner(resolved.Name);
        var named = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        // A record on a Daoris account is judged against the set a start walks (POOL1a2, the account-pool design §2.4), whatever its
        // workspace; one that began in the tool's own home is judged against no scope (F1, ResumeAccount.Judge).
        var settled = Settings.Set(owner);
        var scope = named is null ? settled : Walked(settled, owner, toolchain);
        IReadOnlyCollection<string> present;
        try
        {
            present = named is null ? [] : HarnessSettings.Profiles(Home, owner);
        }
        catch (DriverException)
        {
            present = [];
        }

        var names = AccountNames.Of(Home, owner);
        var before = Before(owner, named, Clock(), names);
        var own = ResumeAccount.Judge(named, scope, kind, before, present);
        // LIMITRESUME1b (the design §3.3 step 1, round 2's B2): a limited record's own account, ready by its cool-off, is asked its maker's
        // reading first, where its door carries none of its windows, and judged again on what that left; where its door carries them, the
        // attempt is the probe. USAGEONE1's skip of a resume's probe stays for the person's words.
        if (limited && named is not null && own == NextHold.Ready)
        {
            await FreshenAsync(adapter, owner, [named], config, ct).ConfigureAwait(false);
            before = Before(owner, named, Clock(), names);
            own = ResumeAccount.Judge(named, scope, kind, before, present);
        }

        if (own == NextHold.Cooling && !newSession && !limited)
        {
            // CONVNOTE2g: the conversation's line by code beside the hold's English, composed here, where the names it says are read.
            var cooling = before.Cooling!;
            return new ResumeChoice(new HarnessSelection(ResumeWords.Waits(cooling, Zone, names)) { Cooling = cooling }, own)
            {
                Waiting = ResumeWords.Waiting(cooling, Zone, names, ResumeWords.NotYet),
            };
        }

        if (own != NextHold.Ready) return await ElsewhereAsync(own).ConfigureAwait(false);
        if (unoffered) return await ElsewhereAsync(NextHold.Refused, notOffered: true).ConfigureAwait(false);

        // Ready before any probe: asked for by name, as a pick is; a record that began in the tool's own home goes on there, as a
        // harness keeps a conversation in the home it began in (D175 point 3), where every new start takes a Daoris account.
        var asked = await ChooseAsync(adapter, config, workspace, chosen: named, kind, counts: true, ct, own: named is null).ConfigureAwait(false);
        return asked is { Allowed: false, NotReady: AccountReadiness.SignedOut }
            ? await ElsewhereAsync(NextHold.SignedOut).ConfigureAwait(false)
            : new ResumeChoice(asked, NextHold.Ready);

        // Where the own account does not offer the model, the walk takes the same choice's other accounts with the own one passed (§3.5(a)).
        async Task<ResumeChoice> ElsewhereAsync(NextHold held, bool notOffered = false) => new(
            walks
                ? await SelectAsync(adapter, config, workspace, chosen: null, kind, ct, model, notOffered && named is not null ? [named] : null)
                    .ConfigureAwait(false)
                : new HarnessSelection(notOffered
                    ? $"Its own account `{AccountNames.Said(names, named!)}` does not offer `{model}`."
                    : ResumeWords.Line(held, newSession)?.Note ?? "Its own account cannot carry it.") { Cooling = before.Cooling },
            held)
        {
            NewSession = newSession && held == NextHold.Cooling,
            Limited = limited,
            NotOffered = notOffered,
        };
    }

    /// <summary>
    /// The account a resume's words take where they are carried to a NEW record, its own conversation unable to go on (OWNSPACE1a,
    /// D175 point 3): the resume's own where that is a Daoris account; where it was the tool's own home, the walk's, since only
    /// the record that began there goes on there. A new record is a new start, so it takes the walk as any start does (D130
    /// §16.2), and holds where the walk holds.
    /// </summary>
    /// <param name="account">The account a hand-over named, which the walk asks for first, or null.</param>
    /// <returns>The selection, and the account named that could not run it, as <see cref="SelectHandedAsync"/> answers.</returns>
    public async Task<(HarnessSelection Selection, string? Passed)> CarriedOnAsync(
        ResumeChoice resume, string adapter, DriverConfig config, string? workspace, string? account, CancellationToken ct = default,
        string? model = null) =>
        // The carry-on walks the asked choice's accounts, each judged by its reading for the record's model (D205 §3.5(a)).
        resume.Selection is { Allowed: true, Profile: null }
            ? await SelectHandedAsync(adapter, config, workspace, account, StartKind.Driven, ct, model).ConfigureAwait(false)
            : (resume.Selection, null);

    /// <summary>
    /// <see cref="SelectAsync"/>, or with <paramref name="counts"/> false the same answer for a panel, which starts nothing
    /// and so is never counted as a start chosen.
    /// </summary>
    /// <param name="own">A resume of a record that began in the tool's own home, which goes on there (D175 point 3).</param>
    private Task<HarnessSelection> ChooseAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind, bool counts, CancellationToken ct,
        bool own = false, string? model = null) =>
        // No gate here (PARALLEL1c): the walk takes its own only to read again and count, never across a question.
        WalkAsync(adapter, config, workspace, chosen, kind, counts, own, ct, new OfferAsk(model, null, 0));

    /// <summary>
    /// What a walk judges each account's offer reading for (D205 §4.1, round 4, Opus N12; MODELROLE1c2): the model the start asks, the
    /// accounts this start already passed for it, and its own count, left out of the facts so a re-ask ranks without its own load.
    /// </summary>
    private sealed record OfferAsk(string? Model, IReadOnlyCollection<string>? Passed, long Except)
    {
        public static OfferAsk None { get; } = new(null, null, 0);
    }

    /// <summary>
    /// The scope a start walks (OWNSPACE1a, D175 points 1-2 and its OWNSPACE1a note): <paramref name="scope"/>, unless it names no
    /// account and the agent's own home is the person's. Then the agent's Daoris accounts on this machine are the start's: one is
    /// the scope's one account, more its list, walked by the rotation's own order and allowance rules; none leaves it naming
    /// nothing, which a start holds on (<see cref="RotationWords.NoAccountOf"/>), never running in the person's own tool home.
    /// </summary>
    private RotationScope Walked(RotationScope scope, string owner, HarnessToolchain toolchain)
    {
        if (scope.List.Count > 0 || scope.Begins is not null || toolchain.StartsInOwnHome) return scope;

        IReadOnlyList<string> mine;
        try
        {
            mine = HarnessSettings.Profiles(Home, owner);
        }
        catch (DriverException)
        {
            mine = [];
        }

        return mine.Count switch
        {
            0 => scope,
            1 => scope with { Begins = mine[0] },
            _ => Settings.Over(owner, mine),
        };
    }

    /// <summary>
    /// The hold of a start with no Daoris account of the agent's signed in (OWNSPACE1a, D175 point 2): one sentence, and a wait
    /// for a sign-in naming the accounts it passed signed out, none where the agent has none, so a look reports it as a wait and
    /// says it once (D125 point 6).
    /// </summary>
    private static HarnessSelection NoAccount(string owner, IReadOnlyList<string> signedOut, IReadOnlyDictionary<string, string> names)
    {
        var note = RotationWords.NoAccountOf(owner);
        return new HarnessSelection(note.Note) { SignedOut = SignedOutAccounts.Of(owner, signedOut, names) with { NoAccount = true }, RefusalNote = note };
    }

    private async Task<HarnessSelection> WalkAsync(
        string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind, bool counts, bool ownHome,
        CancellationToken ct, OfferAsk? offer = null)
    {
        offer ??= OfferAsk.None;
        var resolved = adapters.Resolve(adapter);

        // An adapter that declares no toolchain has nothing to check: it spawns exactly as it did
        // before this existed. Purely additive, which is what lets a new adapter arrive without
        // answering questions about installers it may not have.
        if (resolved.Toolchain is not { } toolchain) return new HarnessSelection(Refusal: null);

        var settings = Settings;
        // 🔴 A door runs as its OWNER's accounts (AGT7): the owner's default, the owner's directory,
        // the owner's login question and key. The pin below stays the door's own — a door is a
        // different package at a different version (ACP2).
        var owner = toolchain.Owner(resolved.Name);
        // The agent's one set, whatever the workspace (POOL1a2, the account-pool design §2.4): no start reads a workspace's scope.
        var named = settings.Set(owner);
        var picked = string.IsNullOrWhiteSpace(chosen) ? null : chosen.Trim();
        // Where the person named no account, the agent's Daoris accounts are the start's (OWNSPACE1a), and none signed in holds it.
        var ours = picked is null && !ownHome && named.List.Count == 0 && named.Begins is null && !toolchain.StartsInOwnHome;
        var scope = ours ? Walked(named, owner, toolchain) : named;

        // Where the agent's door carries none of its windows, its own server is asked them first, for each account of the list
        // the start could take that a set point owes a question (CODEXUSE1, USAGEPROBE1): never read, unread for the interval,
        // projected near its curve, or a limit or a credit said of it. A start, never a look or a panel, which count nothing.
        // Each under its own account's lock and under no gate of the walk's (PARALLEL1c), so another start never waits on them.
        // Only where there is a choice for a reading to change (USAGEONE1): a set of one, and a resume held to its own account,
        // run where they run whatever they would say, so the hour's backstop and a person's look read them instead.
        if (counts && picked is null && !ownHome && scope.List.Count > 1)
        {
            await FreshenAsync(resolved.Name, owner, scope.List, config, ct, scope.Use).ConfigureAwait(false);
        }

        // The first look, before any question: read again under the gate once the questions are asked (PARALLEL1c).
        var now = Clock();
        var (order, _) = OrderOf(owner, scope, kind, picked, ownHome, now, offer.Except);

        // What a hold says names each account by the person's name, read now, as it is said (ACCT2b); the selection's facts
        // and the record's opening line keep the ids.
        var names = AccountNames.Of(Home, owner);

        // 🔴 No Daoris account to run on (OWNSPACE1a, D175 point 2): the start waits for the person's sign-in, before anything is
        // probed or spawned, and never runs in the person's own tool home, which a scope naming nothing once meant (D125 point 5).
        if (ours && order is [null]) return NoAccount(owner, [], names);
        var states = order.Select(account => Before(owner, account, now, names)).ToList();

        // 🔴 A cooling account is held FIRST, by a file read, before any probe (TOOL4d, D125 §3.3, §4): a start on a
        // spent account is refused at once and spends nothing, and three of them parked a quest on 1 October whose only
        // fault was its account. An account its provider already refused is not spent again (AGT3b). So when no account
        // the start may use is ready on either count, nothing is spawned or probed: the start waits.
        if (!states.Any(state => state.IsReady))
        {
            // A pick is the person's (§3.3): refused, never rotated, naming what they could pick instead.
            if (picked is null || states[0].Cooling is not { } pick) return Unready(owner, states, picked is null ? scope : null, kind, now, names);

            var refusal = RotationWords.PickedOf(
                pick, await ReadyAsync(resolved.Name, toolchain, config, pick.Account, now, ct).ConfigureAwait(false), Zone, names);
            return new HarnessSelection(refusal.Note) { Cooling = pick, RefusalNote = refusal };
        }

        // Which binary this spawn runs (TOOL2/D57): the explicit command, then the managed pin, then
        // PATH. An explicit `commands` entry is the person naming exactly what to run and has the
        // last word — a pin quietly replacing it would be a standing choice overruling a specific one.
        string? managed = null;
        if (config.Commands.GetValueOrDefault(resolved.Name) is not { Count: > 0 }
            && settings.ResolveVersion(resolved.Name, workspace, chosen: null) is { } pinned)
        {
            managed = HarnessSettings.ManagedBinary(Home, resolved.Name, pinned, toolchain.Binary);
            if (managed is null)
            {
                // 🔴 Refused, never a silent fall back to PATH. Running a different tool than the one
                // that was pinned — and recording its version as though it were the pinned one —
                // is the failure this whole shape exists to prevent.
                var pinRefusal = $"`{resolved.Name}` is pinned to {pinned} on this machine, and nothing is "
                    + $"installed at that version — `daoris agent pin {resolved.Name} {pinned}` "
                    + $"installs it, and `daoris agent unpin {resolved.Name}` goes back to PATH. "
                    + "Daoris will not quietly run a different version than the one you asked for.";
                return new HarnessSelection(pinRefusal)
                {
                    Absent = AgentAbsence.PinNotInstalled,
                    RefusalNote = Noted.Of(NoteCodes.HeldPinNotInstalled, pinRefusal, ("agent", resolved.Name), ("version", pinned)),
                };
            }
        }

        var report = await ReportAsync(resolved.Name, config, refresh: false, ct).ConfigureAwait(false);
        if (report is { Present: false })
        {
            // Asked again before refusing: the cached answer may predate the install the last refusal
            // asked for. Its binary alone, since an install is no question of an account (ROSTER1).
            await BinaryChangedAsync(resolved.Name, config, ct).ConfigureAwait(false);
            report = await ReportAsync(resolved.Name, config, refresh: false, ct).ConfigureAwait(false);
        }

        if (report is { Present: false })
        {
            var installs = toolchain.Install is { Count: > 0 };
            var own = string.Join(' ', toolchain.Command(config.Commands.GetValueOrDefault(resolved.Name)));
            var install = installs
                ? $"`daoris agent install {resolved.Name}` installs it"
                : $"install it with its own tooling ({own})";

            const string never = "Daoris never installs an agent unasked: a tool that changed under a running loop is a moving target.";
            var absent = $"`{resolved.Name}` is not installed on this machine, so there is nothing to spawn — {install}. {never}";
            return new HarnessSelection(absent)
            {
                Absent = AgentAbsence.NotInstalled,
                // An agent with its own tooling names that command, which may hold a path: it is the program's words between the
                // lead and the line that says Daoris never installs, never a value (ROSTERREFUSE2).
                RefusalNote = installs
                    ? Noted.Of(NoteCodes.HeldNotInstalled, absent, ("agent", resolved.Name))
                    : Noted.Of(NoteCodes.HeldNotInstalledOwn,
                            $"`{resolved.Name}` is not installed on this machine, so there is nothing to spawn — install it with its own tooling (",
                            ("agent", resolved.Name))
                        .Then("", Noted.Said(own, NoteBy.Program)).Then("). ", Noted.Of(NoteCodes.HeldNeverInstalls, never)),
            };
        }

        // The ACP adapter's own seam (ACP2): it runs the Agent SDK, which needs to be told which
        // `claude` to use. Read from the PIPE door's pin, because that is where the CLI lives.
        var claude = resolved.Wire == SessionWire.Acp
            ? HarnessSettings.ManagedBinary(
                Home, "claude-code", settings.ResolveVersion("claude-code", workspace, null), ["claude"])
            : null;

        if (order is [null])
        {
            return new HarnessSelection(null, null, null, report?.Version, managed, claude);
        }

        // The account's state is its owner's answer when this build carries the owner — a door
        // declares no login question of its own, which is what `accountOf` says.
        var asker = AccountAgent(resolved.Name, toolchain);

        // 🔴 The walk (TOOL4f, D125 §3.3), with no question asked under its gate (PARALLEL1c, D181 point 3). A pass reads again
        // the order, each account's state and what the agent last said of each, and chooses the first still ready, counting it
        // where the start counts, under the gate alone, so starts made together each see the others' counts. A question the walk
        // owes an account, a wait for the chosen account's lock, and every other question are outside it: a question that does
        // not answer on one account holds no start on another.
        var walk = new WalkMemory();
        // Each account's offer reading, judged for the model this start asks (D205 §3.3, MODELROLE1c2): an account passed already, or
        // whose reading of this version, under a day old, lacks or refused it, is passed for this start, never cooled.
        var readings = offer.Model is null ? [] : AgentOffers.Read(Home);
        string? Offers(string account) =>
            offer.Passed?.Contains(account, StringComparer.OrdinalIgnoreCase) == true
                ? AgentOffers.NotOffered
                : AgentOffers.Judge(readings, resolved.Name, report?.Version, account, offer.Model, Clock());
        WalkPass pass;
        while (true)
        {
            if (counts)
            {
                lock (_walking) pass = Pass(owner, scope, kind, picked, ours, counts, names, walk, Offers, offer.Except);
            }
            else
            {
                pass = Pass(owner, scope, kind, picked, ours, counts, names, walk, Offers, offer.Except);
            }

            if (pass.Held is { } hold) return hold;
            if (pass.Ask is { } asking)
            {
                // Asked again, as the install above is, since the person may have signed in since (TOOL6g): that account
                // alone, and only once a sign-in marked it or the word is `SignedOutAskedAgain` old, or once after a refusal
                // its last reading said signed in (TOOL6h). Before, a start held on a signed-out account asked every account's
                // status at every look. An account never read is unknown, and a start runs on it as on any unknown (SES3): a
                // look asks nothing (ROSTER1). One whose lock another question kept past its patience is unknown for this walk
                // alone, and passed (PARALLEL1c).
                HarnessProbe.LoginAnswer answer;
                try
                {
                    answer = await AskOneAsync(asker.Name, asker.Toolchain, owner, asking, config, ct).ConfigureAwait(false);
                }
                finally
                {
                    _asking.TryRemove(AccountKey(owner, asking), out _);
                }

                if (answer.Locked) walk.Unknown.Add(asking);
                else walk.Said[asking] = answer.Login;
                if (pass.Owed && AccountReads.Of(Home, owner).Accounts.GetValueOrDefault(asking) is { Login: LoginState.In } fresh)
                {
                    _fresh[AccountKey(owner, asking)] = fresh.At;
                }

                continue;
            }

            // Counted already, and busy from then on (TOOL6g): a status question another caller is asking the account now is
            // waited out, and no question begun after asks it while the session's own process may refresh its token. Where its
            // lock stays held past its patience, the count is taken back and the account passed for this walk (PARALLEL1c).
            if (counts && !await WaitedOutAsync(owner, pass.States[pass.At].Account!, pass.Number, ct).ConfigureAwait(false))
            {
                walk.Unknown.Add(pass.States[pass.At].Account!);
                continue;
            }

            break;
        }

        // What the pass that chose it read: the start's line says the walk as it was when the account was counted.
        (states, order, now) = (pass.States, pass.Order, pass.Now);
        var (at, facts) = (pass.At, pass.Facts);
        var runs = states[at].Account!;
        var home = HarnessSettings.ProfileHome(Home, owner, runs);

        // An account that is a key is handed its key through the tool's own variable (AGT3) — the
        // owner's variable, for a door, since the door runs the owner's tool.
        var key = asker.Toolchain.KeyVariable is { Length: > 0 } variable
            && HarnessKeys.Of(Home, owner, runs) is { } held
                ? new Dictionary<string, string> { [variable] = held }
                : null;

        var selection = new HarnessSelection(null, runs, home, report?.Version, managed, claude, key) { Number = pass.Number };
        if (picked is not null || scope.List.Count == 0 || scope.Begins is not { } begins) return selection;

        // Said by whoever opens the record (TOOL4f, §3.6; TOOL6b, §16.4): its first line and `account.rotated`, with what each
        // account of the list last said about its windows (TOOL6c).
        var choice = AccountRotation.Chose(scope, kind, states, at, facts, now);
        var passed = states.FirstOrDefault(state => string.Equals(state.Account, begins, StringComparison.OrdinalIgnoreCase));
        // Each step by code beside its English (UX7d-1): the choice's clause joins them, and a rotation's is the first alone.
        var steps = RotationWords.Steps(choice, owner, runs, scope, null, passed, facts, kind, now, Zone);
        // Over its curve is the log's `near`, the word for an account passed (SWITCH1b-ii).
        string? Standing(string account) => AccountReadings.Standing(facts.GetValueOrDefault(account)?.Said, scope.Use, now);
        var walked = scope.List.Where(account => order.Contains(account, StringComparer.OrdinalIgnoreCase)).ToList();
        return selection with
        {
            Choice = scope.Use.Use == "goal" && order.Count > 1 ? new AccountChoice(choice.Step, RotationWords.Text(steps)) : null,
            Steps = steps,
            // A start's line says what was projected (USAGEPROBE1); a panel, which a person waits on, reads no history for it.
            AccountsSaid = RotationWords.AccountsSaid(walked, counts ? Projected(owner, walked, facts, now) : facts, scope.Use, now),
            Rotated = string.Equals(runs, begins, StringComparison.OrdinalIgnoreCase)
                ? null
                : new RotatedStart(begins, passed?.Cooling, steps[0].Text)
                {
                    Step = choice.Step,
                    // The set's, for every start now (the account-pool design §2.4); a line written before keeps the workspace it named.
                    Scope = null,
                    FromSaid = Standing(begins),
                    ToSaid = Standing(runs),
                },
        };
    }

    /// <summary>
    /// The accounts a start may use, in the order it tries them (TOOL6b, D130 §16.3; D125 §3.3 under `order`), and what the walk
    /// knows of each. A pick, a scope that names one account, and the tool's own sign-in are the one account, and nothing more
    /// is read.
    /// </summary>
    private (List<string?> Order, IReadOnlyDictionary<string, AccountFacts> Facts) OrderOf(
        string owner, RotationScope scope, StartKind kind, string? picked, bool ownHome, DateTimeOffset now, long except = 0)
    {
        if (picked is not null) return ([picked], NoFacts);
        if (ownHome || scope.List.Count == 0) return ([ownHome ? null : scope.Begins], NoFacts);

        var facts = Facts(owner, scope.List, now, except);
        return ([.. AccountRotation.Order(scope, kind, HarnessSettings.Profiles(Home, owner), facts, now)], facts);
    }

    /// <summary>
    /// What one start's walk learned across its passes (PARALLEL1c): what its own status questions answered, by account, and the
    /// accounts left unknown for it, whose lock another question kept past its patience.
    /// </summary>
    private sealed class WalkMemory
    {
        public Dictionary<string, LoginState> Said { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Unknown { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What one pass of a start's walk found (PARALLEL1c): a hold to answer with; an account to ask first, then pass again
    /// (<see cref="Owed"/> where TOOL6h's one fresh reading is what asks it); or the account chosen, at <see cref="At"/> of the
    /// states it read, counted as start <see cref="Number"/> where the start counts.
    /// </summary>
    private sealed record WalkPass(HarnessSelection? Held = null, string? Ask = null, bool Owed = false)
    {
        public List<AccountState> States { get; init; } = [];

        public List<string?> Order { get; init; } = [];

        public IReadOnlyDictionary<string, AccountFacts> Facts { get; init; } = NoFacts;

        public DateTimeOffset Now { get; init; }

        public int At { get; init; } = -1;

        public long Number { get; init; }
    }

    /// <summary>
    /// The hold of a start whose every ready account's reading does not offer its model (D205 §3.3, MODELROLE1c2): one sentence naming
    /// each account by the person's name, and the soonest cool-off of the rest beside it, so the actor's walk passes the choice
    /// <c>not-offered</c> and a pick is refused with its reason (§3.5(d)).
    /// </summary>
    private static HarnessSelection NotOfferedHold(
        string owner, IReadOnlyList<string> unoffered, IReadOnlyList<AccountState> states, IReadOnlyDictionary<string, string> names)
    {
        var said = string.Join(", ", unoffered.Select(account => $"`{AccountNames.Said(names, account)}`"));
        var soonest = states.Select(state => state.Cooling).OfType<CoolingEntry>().OrderBy(cooling => cooling.Until).FirstOrDefault();
        var verb = unoffered.Count == 1 ? "account {0} does" : "accounts {0} do";
        return new HarnessSelection($"the `{owner}` {string.Format(System.Globalization.CultureInfo.InvariantCulture, verb, said)} not offer the model asked.")
        {
            NotOffered = [.. unoffered],
            Cooling = soonest,
        };
    }

    /// <summary>
    /// One pass of a start's walk (TOOL4f, D125 §3.3), asking nothing (PARALLEL1c): the order and each account's state read again,
    /// with what this walk's own questions answered and every other account's last reading (ROSTER1), and the first account still
    /// ready chosen, and counted where the start counts. A counted start's pass runs under the walk's gate, so it reads and counts
    /// as one; it reads files and asks no agent.
    /// </summary>
    /// <remarks>
    /// An account read signed out that is owed a question (TOOL6g's mark or hour, TOOL6h's fresh reading) is handed back to be
    /// asked outside the gate, claimed so no other walk asks it beside this one; one another walk is asking now is passed as its
    /// last reading says, never waited on. An account unknown for this walk is passed; where nothing else is chosen, the start is
    /// held on it, for the next start to try again.
    /// </remarks>
    private WalkPass Pass(
        string owner, RotationScope scope, StartKind kind, string? picked, bool ours, bool counts,
        IReadOnlyDictionary<string, string> names, WalkMemory walk, Func<string, string?>? offers = null, long except = 0)
    {
        var now = Clock();
        var (order, facts) = OrderOf(owner, scope, kind, picked, ownHome: false, now, except);
        var states = order.Select(account => Before(owner, account, now, names)).ToList();
        // What the agent last said of each account (ROSTER1), as a report says it: an account not on this machine said nothing.
        var present = HarnessSettings.Profiles(Home, owner);
        var reads = AccountReads.Of(Home, owner);

        var at = -1;
        List<string> passed = [];
        List<string> unoffered = [];
        for (var i = 0; i < states.Count && at < 0; i++)
        {
            if (!states[i].IsReady) continue;
            var account = states[i].Account!;
            // D205 §3.3 (MODELROLE1c2): an account whose reading does not offer the model asked is passed for this start, before any
            // question is asked of it: not a cool-off, never counted.
            if (offers?.Invoke(account) is not null)
            {
                unoffered.Add(account);
                continue;
            }

            if (walk.Unknown.Contains(account))
            {
                passed.Add(account);
                states[i] = states[i] with { Locked = true };
                continue;
            }

            var read = present.FirstOrDefault(name => string.Equals(name, account, StringComparison.OrdinalIgnoreCase)) is { } listed
                ? reads.Accounts.GetValueOrDefault(listed)
                : null;
            var asked = walk.Said.TryGetValue(account, out var answered);
            var login = asked ? answered : read?.Login ?? LoginState.Unknown;
            var key = AccountKey(owner, account);
            if (login == LoginState.Out && !asked && _asking.TryAdd(key, true))
            {
                // The one fresh reading a refusal of a signed-in reading owes it (TOOL6h), spent here whatever it answers.
                var owed = _owed.TryRemove(key, out _);
                if (owed || AskAgain(owner, account, read?.At)) return new WalkPass(Ask: account, Owed: owed);
                _asking.TryRemove(key, out _);
            }

            if (login == LoginState.Out)
            {
                var profile = ShellWord.Of(account, ShellWord.Account);
                var refusal = $"the `{owner}` account `{AccountNames.Said(names, account)}` is not signed in, so a session would have "
                    + $"nothing to run as — `daoris agent login {owner} --profile {profile}` runs "
                    + "the agent's own sign-in into it. Daoris manages the directory and the name; the "
                    + "credential stays in the agent's own store.";
                states[i] = states[i] with
                {
                    Readiness = AccountReadiness.SignedOut,
                    Refusal = refusal,
                    RefusalNote = Noted.Of(NoteCodes.HeldSignedOut, refusal, ("owner", owner), ("account", account), ("profile", profile)),
                };
                continue;
            }

            at = i;
        }

        if (at < 0)
        {
            // An account passed for its lock comes free by itself: the start is held for the next to try, never waiting for a time.
            if (passed.Count > 0) return new WalkPass(Held: Locked(owner, passed, names));

            // Every account still ready refuses the model (D205 §3.3, §3.5(d)): the start holds with the reason, the soonest cool-off of
            // the rest beside it, for the actor's walk to take its next choice.
            if (unoffered.Count > 0 && !states.Any(state => state.IsReady && state.Readiness != AccountReadiness.SignedOut
                    && !unoffered.Contains(state.Account!, StringComparer.OrdinalIgnoreCase)))
            {
                return new WalkPass(Held: NotOfferedHold(owner, unoffered, states, names));
            }

            // Every Daoris account the person never named said signed out: none is signed in, so the same sentence (OWNSPACE1a).
            return new WalkPass(Held: ours && states.All(state => state.Readiness == AccountReadiness.SignedOut)
                ? NoAccount(owner, RotationWords.SignedOut(states), names)
                : Unready(owner, states, picked is null ? scope : null, kind, now, names));
        }

        return new WalkPass
        {
            States = states, Order = order, Facts = facts, Now = now, At = at,
            // Counted from here on as one of this account's sessions, so the next start chosen before the next look spreads.
            Number = counts ? Chose(owner, states[at].Account!) : 0,
        };
    }

    /// <summary>
    /// A counted start waits out a question in flight on the account it chose (TOOL6g), counted already, so no question begun
    /// since asks it. False, the count taken back, where the account's lock stays held past <see cref="LockPatience"/>
    /// (PARALLEL1c): a start beside a question in flight could spend its single-use refresh token twice, which signs it out.
    /// </summary>
    private async Task<bool> WaitedOutAsync(string owner, string account, long number, CancellationToken ct)
    {
        IAsyncDisposable? held;
        try
        {
            held = await ProbeLock.TakeAsync(ProbeLock.PathOf(Home, owner, account), LockPatience, ct).ConfigureAwait(false);
        }
        catch
        {
            Unchose(number);
            throw;
        }

        if (held is null)
        {
            Unchose(number);
            return false;
        }

        await held.DisposeAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// The hold of a start whose every account it could run on stayed locked by another question past the walk's patience
    /// (PARALLEL1c): nothing counted, nothing run beside the question, and no wait for a time, so the next start tries again. Its
    /// words alone, a refusal no held note carries by code (<see cref="HarnessSelection.RefusalNote"/>).
    /// </summary>
    private static HarnessSelection Locked(string owner, IReadOnlyList<string> accounts, IReadOnlyDictionary<string, string> names)
    {
        var one = accounts.Count == 1;
        var said = string.Join(", ", accounts.Select(account => $"`{AccountNames.Said(names, account)}`"));
        var words = $"the `{owner}` account{(one ? "" : "s")} {said} {(one ? "is" : "are")} being asked about by another question that held "
            + $"{(one ? "it" : "them")} longer than a start waits, so nothing starts on {(one ? "it" : "them")} now: a start beside that "
            + "question could sign the account out. The next start tries again.";
        return new HarnessSelection(words)
        {
            RefusalNote = Noted.Of(NoteCodes.HeldLocked, words, ("owner", owner), ("accounts", (IReadOnlyList<string>)[.. accounts])),
        };
    }

    /// <summary>
    /// Which account the next start on <paramref name="agent"/>'s accounts would take, in any workspace since every one reads the
    /// set (POOL1a2, D197 point 1), the step that chose it, and what holds each other account (TOOL6e, D130 §3–§4): what the agent's
    /// page in Agents says.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The walk's own pieces, never a second judgement</b>, as <see cref="WiringAsync"/> holds: the one scope
    /// <see cref="SelectAsync"/> reads, what it knows of each account, its order, and each account as it finds it before any
    /// probe (cooling, refused), with the agent's word on who is signed in as its last reading left it (ROSTER1). Nothing is
    /// probed, spawned or counted as a start chosen, so the page may ask at every tick; a sign-in the last reading did not see
    /// is said by a press, or by the start that asks again.
    /// </remarks>
    /// <param name="agent">Whose accounts: the accounts' owner (AGT7), a door's being its owner's.</param>
    /// <param name="kind">Driven work, which drops the kept account, or a conversation (§4.6).</param>
    public NextStart Next(string agent, StartKind kind = StartKind.Driven)
    {
        // The set a start walks (POOL1a2): its whole order; with no account, a wait.
        var scope = Settings.Set(agent);
        var toolchain = OwnersToolchain(agent);
        if (toolchain is not null) scope = Walked(scope, agent, toolchain);
        if (toolchain is { StartsInOwnHome: false } && scope.List.Count == 0 && scope.Begins is null) return new NextStart(null, NextReason.NoAccount, []);

        var now = Clock();
        IReadOnlyList<string> present;
        try
        {
            present = HarnessSettings.Profiles(Home, agent);
        }
        catch (DriverException)
        {
            present = [];
        }

        var facts = scope.List.Count == 0 ? NoFacts : Facts(agent, scope.List, now);
        IReadOnlyList<string?> order = scope.List.Count == 0 ? [scope.Begins] : [.. AccountRotation.Order(scope, kind, present, facts, now)];
        var reads = AccountReads.Of(Home, agent);
        var tried = order.Select(account => Before(agent, account, now) switch
        {
            { IsReady: true, Account: { } named } ready when SaidOut(reads, named) => ready with { Readiness = AccountReadiness.SignedOut },
            var state => state,
        }).ToList();
        return AccountRotation.Next(scope, kind, tried, present, facts, now);
    }

    /// <summary>
    /// The toolchain of the accounts' owner (AGT7), which is no adapter's name where only its doors are (`codex` behind
    /// `codex-acp`): the adapter of that name, else the first door whose owner it is, else the holder this build declares for it
    /// (CODEXACCT1); null where none declares one. What <see cref="Next"/> reads whether a start may run in its own home by
    /// (OWNSPACE1a), since the page asks it by the owner.
    /// </summary>
    private HarnessToolchain? OwnersToolchain(string owner)
    {
        HarnessToolchain? door = null;
        foreach (var name in adapters.Names)
        {
            HarnessToolchain? toolchain;
            try
            {
                toolchain = adapters.Resolve(name).Toolchain;
            }
            catch (DriverException)
            {
                continue;
            }

            if (toolchain is null || !string.Equals(toolchain.Owner(name), owner, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(name, owner, StringComparison.OrdinalIgnoreCase)) return toolchain;
            door ??= toolchain;
        }

        return door ?? adapters.Holder(owner);
    }

    /// <summary>Whether the last reading of an owner's account said it is signed out (ROSTER1): kept under the home, never asked.</summary>
    private static bool SaidOut(AgentReads reads, string account) =>
        reads.Accounts.GetValueOrDefault(account)?.Login == LoginState.Out;

    /// <summary>
    /// What a start on this account would meet before any probe (TOOL4f, D125 §3.3): its cool-off, a file read, then a
    /// refusal its provider gave (AGT3b), held in memory; otherwise ready, until the agent says it is signed out.
    /// </summary>
    /// <param name="names">The owner's names by id, which a cooling account's hold names it by (ACCT2b); null says the id.</param>
    private AccountState Before(string owner, string? account, DateTimeOffset now, IReadOnlyDictionary<string, string>? names = null)
    {
        if (AccountCooling.Of(Home, owner, account, now) is { } cooling)
        {
            var hold = CoolingWords.HoldOf(cooling, Zone, names);
            return new AccountState(account, AccountReadiness.Cooling, cooling, hold.Note) { RefusalNote = hold };
        }

        return _refused.TryGetValue(AccountKey(owner, account), out var refused)
            ? new AccountState(account, AccountReadiness.Refused, Refusal: refused.Reason) { RefusalNote = refused.Note }
            : new AccountState(account, AccountReadiness.Ready);
    }

    /// <summary>
    /// No account the start may use is ready (TOOL4f, D125 §4). One account is held as it always was: its own sentence,
    /// and its cool-off where that is why. Over an order, the start waits for the first reset when one is cooling, said
    /// with every account and why; with none cooling, nothing will come ready by itself, so the default's own refusal,
    /// which names the fix, is the answer.
    /// </summary>
    /// <remarks>
    /// A wait for a time also says what the person could do (TOOL6b, D130 §3.3, §4.6), where it applies: a driven start
    /// whose kept account is ready says it is kept for conversations. No account is outside the set (POOL1a2, D197 point 1), so
    /// none is named as one a list could add. Read from the cool-offs and the refusals alone, as a waiting look starts no process.
    /// </remarks>
    /// <param name="scope">The start's scope, or null for a pick, which says nothing more.</param>
    /// <param name="names">The owner's names by id, which the sentence says each account by (ACCT2b), and its facts carry.</param>
    private HarnessSelection Unready(
        string owner, IReadOnlyList<AccountState> states, RotationScope? scope, StartKind kind, DateTimeOffset now,
        IReadOnlyDictionary<string, string> names)
    {
        var first = states.Where(state => state.Cooling is not null).Select(state => state.Cooling!).MinBy(cooling => cooling.Until);
        // TOOL6g: the accounts passed as not signed in are facts beside the sentence, and over a list with nothing cooling
        // each is named with its sign-in, where the first account's refusal alone named only its own.
        var signedOut = RotationWords.SignedOut(states) is { Count: > 0 } passed ? SignedOutAccounts.Of(owner, passed, names) : null;
        var held = states.Count == 1 || (first is null && signedOut is null)
            ? new HarnessSelection(states[0].Refusal)
            {
                Cooling = states[0].Cooling, NotReady = states[0].Readiness, SignedOut = signedOut, RefusalNote = states[0].RefusalNote,
            }
            : first is null
                ? new HarnessSelection(RotationWords.NoneReady(owner, states, names))
                {
                    SignedOut = signedOut, RefusalNote = RotationWords.NoneReadyOf(owner, states, names),
                }
                : new HarnessSelection(RotationWords.Wait(owner, states, Zone, names))
                {
                    Cooling = first, SignedOut = signedOut, RefusalNote = RotationWords.WaitOf(owner, states, Zone, names),
                };
        if (held.Cooling is null || scope?.Begins is null) return held;

        // What is said after the refusal is parts of their own after its code (ROSTERREFUSE2); where the refusal has none, the
        // sentence keeps its words.
        var sentence = held.Refusal!;
        var note = held.RefusalNote;
        if (kind == StartKind.Driven
            && scope.Use.Keep is { } kept
            && scope.List.Contains(kept, StringComparer.OrdinalIgnoreCase)
            && !states.Any(state => string.Equals(state.Account, kept, StringComparison.OrdinalIgnoreCase))
            && Before(owner, kept, now).IsReady)
        {
            var aside = RotationWords.KeptAsideOf(kept, names);
            sentence += " " + aside.Note;
            note = note?.Then(" ", aside);
        }

        // No account outside the set to name (POOL1a2, D197 point 1): every account of Daoris's is in the order the start walked.
        return held with { Refusal = sentence, RefusalNote = note };
    }

    // ——— What the goal's walk counts (TOOL6b, D130 §4.2, §16.2): Daoris's sessions as of the driver's last look, and the
    // starts chosen since. One roster serves the driver and the conversations alike, so both count.

    private static readonly IReadOnlyDictionary<string, AccountFacts> NoFacts =
        new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);

    // One choice and its count at a time, so starts begun together see each other's: a pass of a counted start's walk, which
    // reads files and counts, and is held across no question, binary or lock wait (PARALLEL1c, D181 point 3).
    private readonly object _walking = new();

    // The accounts a walk is asking its status now (PARALLEL1c), by owner and account: another walk passes such an account as its
    // last reading says, never waiting on that question nor asking it again beside it.
    private readonly ConcurrentDictionary<string, bool> _asking = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _load = new();

    // The last look's records, each with its adapter's owner (AGT7), and the starts chosen since, numbered as chosen.
    private IReadOnlyList<(string Owner, SessionStarted Session)> _looked = [];
    private readonly List<(long Number, string Owner, string Account)> _chosen = [];
    private long _numbered;

    /// <summary>
    /// The number of the last start chosen: taken by the driver before it reads a look's records, so <see cref="Look"/>
    /// keeps every start chosen while they were read.
    /// </summary>
    public long Mark()
    {
        lock (_load) return _numbered;
    }

    /// <summary>
    /// The driver's look read this machine's session records (TOOL6b): what the walk counts from now on, with the starts
    /// chosen after <paramref name="mark"/>. A look begins only once the last look's starts have opened their records, so
    /// every start chosen before it is one of them.
    /// </summary>
    public void Look(IReadOnlyList<SessionStarted> sessions, long mark)
    {
        var owned = sessions.Select(session => (OwnerOf(session.Adapter), session)).ToList();
        lock (_load)
        {
            _looked = owned;
            _chosen.RemoveAll(start => start.Number <= mark);
        }
    }

    /// <summary>A start chosen on an account: one of its sessions until the next look reads the records. Its number, to take it back.</summary>
    private long Chose(string owner, string account)
    {
        lock (_load)
        {
            _chosen.Add((++_numbered, owner, account));
            return _numbered;
        }
    }

    /// <summary>A start counted on an account that did not run there after all (PARALLEL1c): no longer one of its sessions.</summary>
    private void Unchose(long number)
    {
        lock (_load) _chosen.RemoveAll(start => start.Number == number);
    }

    /// <summary>
    /// A start's one count given back after its opened process has stopped and before any replacement is chosen (D205 §4.1, round 4,
    /// R3-B1, MODELROLE1c2): idempotent, both halves under the count's lock. The start's number is taken back (nothing once a look has
    /// covered it), and where <paramref name="session"/> is given, a look's reading of that record is made not running and out of the
    /// last started, so the refusing account is neither busy nor the start just made.
    /// </summary>
    /// <param name="number">The start's count, as its selection carries it; 0 for none.</param>
    /// <param name="session">The record it opened, or null where none opened.</param>
    public void Release(long number, string? session = null)
    {
        lock (_load)
        {
            if (number > 0) _chosen.RemoveAll(start => start.Number == number);
            if (session is null) return;
            _looked =
            [
                .. _looked.Select(each => string.Equals(each.Session.Id, session, StringComparison.Ordinal)
                    ? (each.Owner, each.Session with { Running = false, NotOffered = true })
                    : each),
            ];
        }
    }

    /// <summary>
    /// What the walk knows of each account of a list (§16.4), and what its agent last said about its windows where a door
    /// carried it (TOOL6c, §5.2).
    /// </summary>
    private Dictionary<string, AccountFacts> Facts(string owner, IReadOnlyList<string> list, DateTimeOffset now, long except = 0)
    {
        var facts = new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);
        lock (_load)
        {
            foreach (var account in list)
            {
                // A record ended `choice-not-offered` ran nothing (D205 §3.5(c)): neither running nor the account's last start.
                var sessions = _looked.Where(each => Same(each.Owner, owner) && Same(each.Session.Profile, account) && !each.Session.NotOffered)
                    .Select(each => each.Session).ToList();
                // A re-ask's own count left out (D205 §4.1): its account is ranked as if the start had not chosen it.
                var chosen = _chosen.Where(start => start.Number != except && Same(start.Owner, owner) && Same(start.Account, account)).ToList();
                facts[account] = new AccountFacts(
                    Running: sessions.Count(session => session.Running) + chosen.Count,
                    LastStarted: sessions.Select(session => session.Created).Where(created => created is not null).Max(),
                    Chosen: chosen.Count == 0 ? null : chosen.Max(start => start.Number));
            }
        }

        foreach (var account in list)
        {
            try
            {
                // A weekly reset a limit told is not read here (USAGERESET1): it says when, not how much would lapse, which
                // only the week's reading says.
                facts[account] = facts[account] with { Said = AccountWindows.SaidOf(Home, owner, account, now) };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A reading not read is nothing said, as one never told is.
            }
        }

        return facts;

        static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whose accounts a session on <paramref name="adapter"/> ran as (AGT7): its owner, or the adapter where it is unknown.</summary>
    private string OwnerOf(string adapter)
    {
        try
        {
            return adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase) && adapters.Resolve(adapter) is { } resolved
                ? resolved.Toolchain?.Owner(resolved.Name) ?? resolved.Name
                : adapter;
        }
        catch (DriverException)
        {
            return adapter;
        }
    }

    /// <summary>What a report says of one account: its last reading and when; null where the report lists none of that name.</summary>
    private static ProfileReport? ProfileOf(HarnessReport? report, string account) =>
        report?.Profiles.FirstOrDefault(p => string.Equals(p.Name, account, StringComparison.OrdinalIgnoreCase));

    /// <summary>What the agent last said about one account's sign-in, from a report; unknown where it says nothing.</summary>
    private static LoginState LoginOf(HarnessReport? report, string account) => ProfileOf(report, account)?.Login ?? LoginState.Unknown;

    /// <summary>
    /// The agent's other accounts a person could pick instead of a cooling one (D125 §3.3): not cooling, not refused, and
    /// not signed out, as the last reading of each says (ROSTER1). Nothing is asked of an account.
    /// </summary>
    private async Task<IReadOnlyList<string>> ReadyAsync(
        string adapter, HarnessToolchain toolchain, DriverConfig config, string? picked, DateTimeOffset now, CancellationToken ct)
    {
        var owner = toolchain.Owner(adapter);
        var others = HarnessSettings.Profiles(Home, owner)
            .Where(name => !string.Equals(name, picked, StringComparison.OrdinalIgnoreCase))
            .Where(name => Before(owner, name, now).IsReady)
            .ToList();
        if (others.Count == 0) return [];

        var report = await ReportAsync(ReportingDoor(adapter, toolchain), config, refresh: false, ct).ConfigureAwait(false);
        return report is { Present: false } ? [] : [.. others.Where(name => LoginOf(report, name) != LoginState.Out)];
    }

    /// <summary>
    /// What a start in this workspace would run on, and where each part came from (MAP1b, D67 §3).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Read through <see cref="SelectAsync"/>, never beside it.</b> The design's rule is that the
    /// picture cannot disagree with the loop, and a second resolution is exactly how it would: the
    /// account is the selection's — its scope is the agent's one set (<see cref="HarnessSettings.Set"/>, POOL1a2), which
    /// <see cref="SelectAsync"/> reads — and whether the start happens, and at which version, is the
    /// selection itself, asked without being counted as a start chosen. Nothing from the selection's
    /// machine-local half (the home, the binary, the key) is kept.
    /// </remarks>
    public async Task<StartWiring> WiringAsync(
        string adapter, DriverConfig config, string? workspace, CancellationToken ct = default)
    {
        var resolved = adapters.Resolve(adapter);
        var toolchain = resolved.Toolchain;
        var owner = toolchain?.Owner(resolved.Name) ?? resolved.Name;
        var settings = Settings;

        // The start's one set (POOL1a2, the account-pool design §2.4): where it begins, the order's first, read as the machine's
        // and never a workspace's (`StartWiring.ProfileFrom`); unset where the agent has no account.
        var scope = toolchain is null ? null : settings.Set(owner);
        var profileFrom = scope?.Begins is null ? ChoiceFrom.Unset : ChoiceFrom.Machine;
        // The same precedence `SelectAsync` applies: a command `driver.json` names has the last word.
        var commanded = config.Commands.GetValueOrDefault(resolved.Name) is { Count: > 0 };
        var (pinned, versionFrom) = commanded || toolchain is null
            ? (null, ChoiceFrom.Unset)
            : settings.ResolveVersionFrom(resolved.Name, workspace, chosen: null);

        // Asked as a start would ask, and counted as none: a panel starts nothing.
        var selection = await ChooseAsync(adapter, config, workspace, chosen: null, StartKind.Driven, counts: false, ct).ConfigureAwait(false);

        // The account a start would take (TOOL4f, TOOL6b): the walk's, with where the scope begins beside it when that is
        // another, so the panel never shows an account a start would not take; where begins, when the start is held.
        return new StartWiring(
            resolved.Name, owner, selection.Profile ?? scope?.Begins, profileFrom,
            selection.Version ?? pinned, versionFrom, commanded, selection.Refusal)
        {
            RotatedFrom = selection.Rotated?.From,
        };
    }

    // The windows of an account asked of its agent's own server, where its door carries none (CODEXUSE1, D125's CODEXUSE1 note):
    // kept in windows.json as a door's frame is (AccountWindows.Said), so near, pace, a start's line of what each account said,
    // `daoris agent list` and the page read a Codex account as they read a Claude Code one.
    //  - When, as ROSTER1 reads an account: at a person's press, the agent's or the account's; and at a start's walk over a list,
    //    where switching before the limit and pace read it, for each account of the list the start could take whose last reading
    //    is older than CodexUsage.Fresh. Never at a look, a panel, a pick or the page's next start.
    //  - The tool's own sign-in only at a person's press for it (CODEXUSE3): its row's Read, the agent's Read again and the
    //    roster's read-all, as its status question is asked (TOOL6g), since it is the person's own and their terminal runs on it.
    //    Its server is started with no account's home, so it answers wherever the agent keeps its own sign-in (D63: that home is
    //    the agent's), and its reading is kept under AccountWindows.Own. A walk never asks it: it is in no list (D125 §3.7), so
    //    its reading moves no start.
    //  - How, as a status question is asked (TOOL6g): under the account's ProbeLock, the own sign-in's under its own, and not at
    //    all while a session of Daoris's runs on it, whose own process may refresh its token; at a walk never an account cooling,
    //    refused, read signed out, or that is a key, whose plan has no windows. A reading not had is unknown and keeps nothing (D57).

    /// <summary>
    /// How an account's windows are asked in a test (CODEXUSE1): by owner and account, in place of the agent's server, asked
    /// under the same lock and the same holds, since every real question spawns the agent, which the fast half may not. The
    /// tool's own sign-in is asked as <see cref="AccountWindows.Own"/> (CODEXUSE3).
    /// </summary>
    internal Func<string, string, CancellationToken, Task<IReadOnlyList<WindowReading>?>>? AskingUsage { get; init; }

    // When each account's windows were last asked, by owner and account, answered or not: one that could not be had waits
    // as long as a reading stands before it is asked again.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _usageAsked = new(StringComparer.OrdinalIgnoreCase);

    // When a limit was last said of each account in this process, by owner and account (USAGEPROBE1): a cool-off the person
    // ended early leaves none in the file to say it.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _limitSaid = new(StringComparer.OrdinalIgnoreCase);

    // The usage history's last week in memory, read on as it grows (USAGEPROBE1): what each start's projection reads.
    private UsageLedger? _ledger;

    private UsageLedger Ledger => LazyInitializer.EnsureInitialized(ref _ledger, () => new UsageLedger(Home));

    /// <summary>
    /// How this adapter's accounts' windows are asked (CODEXUSE1): its own question, else its owner's, since a door's readings
    /// are its owner's as its accounts are (AGT7). Null asks nothing.
    /// </summary>
    public UsageQuestion? UsageOf(string adapter)
    {
        var resolved = Adapters.Resolve(adapter);
        if (resolved.Toolchain?.Usage is { } own) return own;
        return CoolingAgent(resolved) is { } owner
               && !string.Equals(owner, resolved.Name, StringComparison.OrdinalIgnoreCase)
               && Adapters.Names.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? Adapters.Resolve(owner).Toolchain?.Usage
            : null;
    }

    /// <summary>
    /// Whether this adapter's accounts say what they have left (D130 §6): its door carries the agent's frame
    /// (<see cref="WindowsOf"/>), or its agent's server is asked (<see cref="UsageOf"/>). What switching before the limit waits
    /// for.
    /// </summary>
    public bool Speaks(string adapter) => WindowsOf(adapter) is not null || UsageOf(adapter) is not null;

    /// <summary>
    /// The accounts of a list asked their windows at once, each under its own lock, before the walk reads what each said: each it
    /// could take whose last reading is older than its question's floor (<see cref="CodexUsage.Fresh"/>, or Claude's
    /// <see cref="ClaudeUsage.Floor"/>, CODEXUSE1), and, for a start's walk (<paramref name="use"/>), only where a set point owes
    /// it a question (USAGEPROBE1, <see cref="UsageProjection.Owed"/>).
    /// </summary>
    /// <param name="use">The start's scope's use, whose curve the projections are measured against; null asks past the floor alone.</param>
    private async Task FreshenAsync(
        string adapter, string owner, IReadOnlyList<string> list, DriverConfig config, CancellationToken ct, RotationUse? use = null)
    {
        if (UsageOf(adapter) is not { } question) return;

        var now = Clock();
        IReadOnlyList<string> present;
        AgentReads reads;
        try
        {
            present = HarnessSettings.Profiles(Home, owner);
            reads = AccountReads.Of(Home, owner);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return;
        }

        IReadOnlyList<CoolingEntry> offered = use is null ? [] : Offered(now);
        var asking = list
            .Where(account => present.Contains(account, StringComparer.OrdinalIgnoreCase))
            .Where(account => Before(owner, account, now).IsReady && !SaidOut(reads, account))
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
    private ProbeWhy? Owed(string owner, string account, RotationUse use, DateTimeOffset now, IReadOnlyList<CoolingEntry> offered)
    {
        var said = SaidOf(owner, account, now);
        DateTimeOffset? limit = _limitSaid.TryGetValue(AccountKey(owner, account), out var heard) ? heard : null;
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
            return AccountCooling.Offered(Home, now);
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
    private Dictionary<string, AccountFacts> Projected(
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
    public IReadOnlyList<SwitchCandidate> SwitchCandidates(string owner, IReadOnlyList<string> list)
    {
        var now = Clock();
        IReadOnlyList<string> present;
        AgentReads reads;
        try
        {
            present = HarnessSettings.Profiles(Home, owner);
            reads = AccountReads.Of(Home, owner);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            // Accounts not read are none ready: nothing is moved to on a guess.
            return [.. list.Select(account => new SwitchCandidate(account, null, Ready: false))];
        }

        return [.. list.Select(account => new SwitchCandidate(
            account,
            SaidOf(owner, account, now),
            Ready: present.Contains(account, StringComparer.OrdinalIgnoreCase) && Before(owner, account, now).IsReady && !SaidOut(reads, account)))];
    }

    /// <summary>What an account last said of its windows, or null where nothing was or the file does not read.</summary>
    private AccountSaid? SaidOf(string owner, string account, DateTimeOffset now)
    {
        try
        {
            return AccountWindows.SaidOf(Home, owner, account, now);
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
        if (_usageAsked.TryGetValue(AccountKey(owner, account), out var asked) && now - asked < fresh) return true;
        try
        {
            return AccountWindows.SaidOf(Home, owner, account, now) is { } said && now - said.Seen < fresh;
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
    private async Task<IReadOnlyList<WindowReading>?> AskUsageAsync(
        string adapter, string owner, string account, DriverConfig config, CancellationToken ct)
    {
        if (UsageOf(adapter) is not { } question) return null;
        try
        {
            // An account that is a key is billed by the key, not a plan's windows (AGT3): nothing to read.
            if (HarnessKeys.Of(Home, owner, account) is not null) return null;
            _usageAsked[AccountKey(owner, account)] = Clock();

            // One question per account at a time, and none while a session of Daoris's runs on it (TOOL6g).
            await using var held = await ProbeLock.TakeAsync(ProbeLock.PathOf(Home, owner, account), ct: ct).ConfigureAwait(false);
            if (held is null || Busy(owner, account)) return null;

            IReadOnlyList<WindowReading>? readings;
            if (AskingUsage is { } standIn)
            {
                readings = await standIn(owner, account, ct).ConfigureAwait(false);
            }
            else
            {
                if (UsageCommand(question, config) is not { } command) return null;
                var toolchain = AccountToolchain(adapter) ?? Toolchain(adapter)!;
                // 🔴 Its server is started in the account's home, never with none, where the agent would answer for the
                // person's own sign-in and might refresh their token (D197 point 3).
                var profileHome = HarnessSettings.ProfileHome(Home, owner, account);
                if (question.Kind == UsageKind.Claude)
                {
                    // Run in an empty folder of the home's own, never the repository or OS temp: the CLI keys its project
                    // state by its working directory, and a read is no project's.
                    var folder = Path.Combine(profileHome, "usage-read");
                    Directory.CreateDirectory(folder);
                    var asking = CodexUsage.Prepare(command.Run, toolchain, profileHome, command.Managed, folder);
                    var at = Clock();
                    readings = await CodexUsage.ReadAsync(
                        asking, ClaudeUsage.Patience, ct,
                        (incoming, outgoing, patience, token) => ClaudeUsage.AskWithinAsync(incoming, outgoing, patience, at, token))
                        .ConfigureAwait(false);
                }
                else
                {
                    var info = CodexUsage.Prepare(command.Run, toolchain, profileHome, command.Managed);
                    readings = await CodexUsage.ReadAsync(info, CodexUsage.Patience, ct).ConfigureAwait(false);
                }
            }

            // Every window it said kept in the usage history first (USAGECAP1a1): the reader's own parse where it answered one,
            // else the walk's readings as they are (a stand-in's, reader 0).
            var now = Clock();
            var key = account;
            var door = Adapters.Resolve(adapter).Name;
            if (readings is UsageAnswer answer)
            {
                History(owner, key, door, answer.Seen, answer.Source, answer.Reader, now, session: null, answer.Plan);
            }
            else if (readings is { Count: > 0 })
            {
                History(
                    owner, key, door,
                    [.. readings.Select(reading => new WindowSeen(
                        reading.Window, reading.Window, null, null, reading.Used, null, reading.Reset, UsageHistory.ResetSaid))],
                    question.Kind == UsageKind.Claude ? ClaudeUsage.Source : CodexUsage.Method, reader: 0, now, session: null);
            }

            if (readings is not { Count: > 0 }) return null;
            AccountWindows.Said(Home, owner, key, readings, now, session: null, source: AccountWindows.SourceRead);
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
    private (IReadOnlyList<string> Run, string? Managed)? UsageCommand(UsageQuestion question, DriverConfig config)
    {
        var arguments = question.Command.Skip(1).ToList();
        if (config.Commands.GetValueOrDefault(question.Harness) is { Count: > 0 } named) return ([.. named, .. arguments], null);
        if (Settings.ResolveVersion(question.Harness, workspace: null, chosen: null) is not { } pinned) return (question.Command, null);
        return HarnessSettings.ManagedBinary(Home, question.Harness, pinned, [question.Command[0]]) is { } managed
            ? ([managed, .. arguments], managed)
            : null;
    }

    /// <summary>
    /// A press (ROSTER1): each of the agent's accounts asked, one at a time per account, whatever its last reading's age; never
    /// the tool's own sign-in (POOL1a3, D197 point 3, amending CODEXUSE3).
    /// </summary>
    private async Task PressUsageAsync(string adapter, string owner, DriverConfig config, CancellationToken ct)
    {
        if (UsageOf(adapter) is null) return;
        IReadOnlyList<string> accounts;
        try
        {
            accounts = HarnessSettings.Profiles(Home, owner);
        }
        catch (DriverException)
        {
            accounts = [];
        }

        await Task.WhenAll(accounts.Select(account => AskUsageAsync(adapter, owner, account, config, ct))).ConfigureAwait(false);
    }
}
