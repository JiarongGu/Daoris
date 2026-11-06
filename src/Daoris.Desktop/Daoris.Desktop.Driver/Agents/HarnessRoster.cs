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
public sealed class HarnessRoster(ProviderRegistry adapters, string? settingsPath = null)
{
    // What each adapter's binary answered when last asked (ROSTER1): whether it is there, its version, why not. Asked of the
    // binary alone, once a process, and again at a press or where the binary may have changed.
    internal readonly System.Collections.Concurrent.ConcurrentDictionary<string, Binary> _binaries =
        new(StringComparer.OrdinalIgnoreCase);

    internal sealed record Binary(bool Present, string? Version, string? Problem);

    // Who each reading this process made said is signed in, with the organisation and the plan beside them (ACCOUNTID1), by
    // owner and account (its moment beside it): read fresh and written nowhere (D66 §3), so a reading kept from before this
    // process says its state and when, and who once read again.
    internal readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTimeOffset At, LoginRead Said)> _who =
        new(StringComparer.OrdinalIgnoreCase);

    // Accounts a provider refused (AGT3b), by owner and profile — "" for the tool's own home — with
    // the sentence that says so. Held in memory until a person looks again; see `Refuse`.
    internal readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Reason, Noted? Note)> _refused =
        new(StringComparer.OrdinalIgnoreCase);

    internal static string AccountKey(string owner, string? profile) => $"{owner}/{profile ?? ""}";

    internal IStartLedger? _startLedger;

    /// <summary>
    /// The ledger this process's starts are written to (<c>StaffFor</c>, STAFFPOLICY1c): held here, beside the roster, because
    /// <see cref="Release"/> gives a refused start's reservation back with its account count. Set once, by the roles above this layer.
    /// </summary>
    public IStartLedger StartLedger(Func<IStartLedger> make)
    {
        lock (_load) return _startLedger ??= make();
    }

    /// <summary>The ledger when one was made; null before any start asked for it.</summary>
    public IStartLedger? StartLedgerMade => _startLedger;

    /// <inheritdoc cref="RosterCooling.Refuse"/>
    public void Refuse(string adapter, string? profile, string reason, Noted? note = null) => Cooling.Refuse(adapter, profile, reason, note);

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

    internal string? CoolingAgent(ISessionAdapter resolved) => Cooling.CoolingAgent(resolved);

    /// <inheritdoc cref="RosterCooling.LimitsOf"/>
    public LimitWords? LimitsOf(string adapter) => Cooling.LimitsOf(adapter);

    /// <inheritdoc cref="RosterCooling.SignInOf"/>
    public SignInWords? SignInOf(string adapter) => Cooling.SignInOf(adapter);

    /// <inheritdoc cref="RosterCooling.SignedOut"/>
    public void SignedOut(string adapter, string? profile) => Cooling.SignedOut(adapter, profile);

    // The accounts a refusal of a signed-in reading owes one fresh reading (TOOL6h), by owner and account: taken by the next walk
    // that reaches the account, then spent. Any other reading of the account settles it (`Keep`).
    internal readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _owed = new(StringComparer.OrdinalIgnoreCase);

    // The signed-in readings a fresh reading made (TOOL6h), by owner and account, with the time each was kept: a refusal of one
    // owes none.
    internal readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _fresh =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc cref="RosterCooling.WindowsOf"/>
    public WindowWords? WindowsOf(string adapter) => Cooling.WindowsOf(adapter);

    /// <inheritdoc cref="RosterCooling.Said"/>
    public IReadOnlyList<WindowReading>? Said(string adapter, string? profile, JsonElement info, string? session)
        => Cooling.Said(adapter, profile, info, session);

    internal void History(string owner, string account, string door, IReadOnlyList<WindowSeen> windows, string source, int reader, DateTimeOffset now, string? session, string? plan = null)
        => Cooling.History(owner, account, door, windows, source, reader, now, session, plan);

    /// <inheritdoc cref="RosterCooling.CoolingOf"/>
    public CoolingEntry? CoolingOf(string adapter, string? profile) => Cooling.CoolingOf(adapter, profile);

    /// <inheritdoc cref="RosterCooling.CoolingsOf"/>
    public IReadOnlyList<CoolingEntry> CoolingsOf(string adapter) => Cooling.CoolingsOf(adapter);

    /// <inheritdoc cref="RosterCooling.Limited"/>
    public (CoolingEntry Entry, LimitSeen Seen)? Limited(string adapter, string? profile, string? failure, string session, TimeSpan? coolOff = null, LimitSeen? byCode = null)
        => Cooling.Limited(adapter, profile, failure, session, coolOff, byCode);

    /// <inheritdoc cref="RosterCooling.Ready"/>
    public bool Ready(string adapter, string? profile) => Cooling.Ready(adapter, profile);

    /// <inheritdoc cref="RosterCooling.NewWait"/>
    internal bool NewWait(string agent, string? account, DateTimeOffset? until, IReadOnlyList<string>? signedOut = null)
        => Cooling.NewWait(agent, account, until, signedOut);

    /// <summary>The adapters this roster answers for — the build's, plus whatever plugins declare (D64).</summary>
    public ProviderRegistry Adapters => adapters;

    /// <summary>
    /// Answer for a different set from now on — the built-in adapters plus the plugins a tick just
    /// read. What each binary answered is keyed by harness name and survives: a harness that was there
    /// before is the same harness, and one that just arrived has simply not been asked yet.
    /// </summary>
    public void Use(ProviderRegistry live) => adapters = live;

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
    /// The agents whose accounts this roster answers for (POOLSTRAY1, <see cref="ProviderRegistry.Agents"/>): what every write of the wiring
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
    public static bool Structured(ISessionAdapter adapter) => adapter.Abilities().Structured;

    /// <inheritdoc cref="RosterReport.ReportAsync"/>
    public Task<HarnessReport?> ReportAsync(string adapter, DriverConfig config, bool refresh = false, CancellationToken ct = default, string? account = null)
        => Reporting.ReportAsync(adapter, config, refresh, ct, account);

    /// <inheritdoc cref="RosterReport.BinaryChangedAsync"/>
    public Task BinaryChangedAsync(string adapter, DriverConfig config, CancellationToken ct = default)
        => Reporting.BinaryChangedAsync(adapter, config, ct);

    internal void Keep(string owner, string? account, LoginRead said, DateTimeOffset at) => Cooling.Keep(owner, account, said, at);

    /// <inheritdoc cref="RosterCooling.Removed"/>
    public void Removed(string agent, string account) => Cooling.Removed(agent, account);

    /// <inheritdoc cref="RosterCooling.LookedAgain"/>
    public void LookedAgain() => Cooling.LookedAgain();

    internal void EndOwnCooling(string owner) => Cooling.EndOwnCooling(owner);

    /// <summary>
    /// How long the agent's word that an account is signed out is believed when nothing says it changed (TOOL6g): a
    /// backstop for a sign-in no door of Daoris's made. Before TOOL6g, a start held on a signed-out account asked every
    /// account's status, and the tool's own sign-in's, at every look.
    /// </summary>
    public static readonly TimeSpan SignedOutAskedAgain = TimeSpan.FromHours(1);

    internal bool AskAgain(string owner, string account, DateTimeOffset? read) => Reporting.AskAgain(owner, account, read);

    internal Task<HarnessProbe.LoginAnswer> AskOneAsync(string adapter, HarnessToolchain toolchain, string owner, string account, DriverConfig config, CancellationToken ct)
        => Reporting.AskOneAsync(adapter, toolchain, owner, account, config, ct);

    /// <inheritdoc cref="RosterReport.Busy"/>
    internal bool Busy(string owner, string? account) => Reporting.Busy(owner, account);

    /// <inheritdoc cref="RosterReport.LoginOfAsync"/>
    public Task<LoginRead> LoginOfAsync(string adapter, DriverConfig config, string profile, CancellationToken ct = default)
        => Reporting.LoginOfAsync(adapter, config, profile, ct);

    /// <inheritdoc cref="RosterReport.AccountToolchain"/>
    public HarnessToolchain? AccountToolchain(string adapter) => Reporting.AccountToolchain(adapter);

    /// <summary>
    /// The variable a key for this adapter's accounts is handed to the tool in, or null where its accounts take no key from Daoris: the
    /// one reader, the window's key actions and the terminal's <c>agent key</c> alike, through the registry's account agent.
    /// </summary>
    public string? KeyVariableOf(string adapter) =>
        Adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase)
            ? Adapters.AccountAgentOf(new DoorId(Adapters.Resolve(adapter).Name))?.Accounts.KeyVariable is { Length: > 0 } variable ? variable : null
            : null;

    /// <summary>Whether this adapter's agent carries a window a week at a time once it passes (<c>WeekFixed</c>), read from its account agent's pool through the registry.</summary>
    public bool WeekFixedOf(string adapter) =>
        Adapters.Names.Contains(adapter, StringComparer.OrdinalIgnoreCase)
        && Adapters.AccountAgentOf(new DoorId(Adapters.Resolve(adapter).Name))?.Accounts.WeekFixed == true;

    /// <inheritdoc cref="RosterReport.AccountAgentOf"/>
    public (string Name, HarnessToolchain Toolchain)? AccountAgentOf(string adapter) => Reporting.AccountAgentOf(adapter);

    /// <inheritdoc cref="RosterReport.SignInCommand"/>
    public HarnessCommand? SignInCommand(string adapter, DriverConfig config) => Reporting.SignInCommand(adapter, config);

    internal (string Name, HarnessToolchain Toolchain) AccountAgent(string name, HarnessToolchain toolchain)
        => Reporting.AccountAgent(name, toolchain);

    internal string ReportingDoor(string name, HarnessToolchain toolchain) => Reporting.ReportingDoor(name, toolchain);

    /// <inheritdoc cref="RosterReport.RosterAsync"/>
    public Task<IReadOnlyList<HarnessReport>> RosterAsync(DriverConfig config, bool refresh = false, CancellationToken ct = default)
        => Reporting.RosterAsync(config, refresh, ct);

    /// <inheritdoc cref="RosterSelection.SelectAsync"/>
    public Task<HarnessSelection> SelectAsync(string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind = StartKind.Driven, CancellationToken ct = default, string? model = null, IReadOnlyCollection<string>? passed = null, long except = 0, bool counts = true)
        => Selecting.SelectAsync(adapter, config, workspace, chosen, kind, ct, model, passed, except, counts);

    internal long Selected => Selecting.Selected;

    /// <inheritdoc cref="RosterSelection.SelectHandedAsync"/>
    public Task<(HarnessSelection Selection, string? Passed)> SelectHandedAsync(string adapter, DriverConfig config, string? workspace, string? account, StartKind kind, CancellationToken ct = default, string? model = null, IReadOnlyCollection<string>? passed = null, long except = 0, bool counts = true)
        => Selecting.SelectHandedAsync(adapter, config, workspace, account, kind, ct, model, passed, except, counts);

    /// <inheritdoc cref="RosterSelection.ResumeAsync"/>
    public Task<ResumeChoice> ResumeAsync(string adapter, DriverConfig config, string? workspace, string? account, StartKind kind, bool newSession = false, LineKind line = default, CancellationToken ct = default, bool limited = false, string? model = null, bool picked = false)
        => Selecting.ResumeAsync(adapter, config, workspace, account, kind, newSession, line, ct, limited, model, picked);

    /// <inheritdoc cref="RosterSelection.CarriedOnAsync"/>
    public Task<(HarnessSelection Selection, string? Passed)> CarriedOnAsync(ResumeChoice resume, string adapter, DriverConfig config, string? workspace, string? account, CancellationToken ct = default, string? model = null)
        => Selecting.CarriedOnAsync(resume, adapter, config, workspace, account, ct, model);

    internal Task<HarnessSelection> ChooseAsync(string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind, bool counts, CancellationToken ct, bool own = false, string? model = null, bool asks = true)
        => Selecting.ChooseAsync(adapter, config, workspace, chosen, kind, counts, ct, own, model, asks);

    internal RotationScope Walked(RotationScope scope, string owner, HarnessToolchain toolchain) => Selecting.Walked(scope, owner, toolchain);

    internal static HarnessSelection NoAccount(string owner, IReadOnlyList<string> signedOut, IReadOnlyDictionary<string, string> names)
        => RosterSelection.NoAccount(owner, signedOut, names);

    internal Task<HarnessSelection> WalkAsync(string adapter, DriverConfig config, string? workspace, string? chosen, StartKind kind, bool counts, bool ownHome, CancellationToken ct, OfferAsk? offer = null, bool asks = true)
        => Walking.WalkAsync(adapter, config, workspace, chosen, kind, counts, ownHome, ct, offer, asks);

    /// <inheritdoc cref="RosterNext.LastRead"/>
    public AgentLastRead? LastRead(string adapter, DriverConfig config, string? workspace = null, StartKind kind = StartKind.Driven)
        => Upcoming.LastRead(adapter, config, workspace, kind);

    internal (List<string?> Order, IReadOnlyDictionary<string, AccountFacts> Facts) OrderOf(string owner, RotationScope scope, StartKind kind, string? picked, bool ownHome, DateTimeOffset now, long except = 0)
        => Walking.OrderOf(owner, scope, kind, picked, ownHome, now, except);

    /// <inheritdoc cref="RosterNext.Next"/>
    public NextStart Next(string agent, StartKind kind = StartKind.Driven) => Upcoming.Next(agent, kind);

    /// <inheritdoc cref="RosterNext.SetUpAgents"/>
    public IReadOnlyList<string> SetUpAgents() => Upcoming.SetUpAgents();

    internal static AccountFacts? FactOf(IReadOnlyDictionary<string, AccountFacts> facts, string? account) => RosterNext.FactOf(facts, account);

    internal static bool SaidOut(AgentReads reads, string account) => RosterNext.SaidOut(reads, account);

    internal AccountState Before(string owner, string? account, DateTimeOffset now, IReadOnlyDictionary<string, string>? names = null, AccountFacts? fact = null)
        => Upcoming.Before(owner, account, now, names, fact);

    internal static readonly IReadOnlyDictionary<string, AccountFacts> NoFacts =
        new Dictionary<string, AccountFacts>(StringComparer.OrdinalIgnoreCase);

    internal readonly object _load = new();

    // The last look's records, each with its adapter's owner (AGT7), and the starts chosen since, numbered as chosen.
    internal IReadOnlyList<(string Owner, SessionStarted Session)> _looked = [];
    internal readonly List<(long Number, string Owner, string Account)> _chosen = [];

    /// <inheritdoc cref="RosterStarts.Mark"/>
    public long Mark() => Starts.Mark();

    /// <inheritdoc cref="RosterStarts.Look"/>
    public void Look(IReadOnlyList<SessionStarted> sessions, long mark) => Starts.Look(sessions, mark);

    internal long Chose(string owner, string account) => Starts.Chose(owner, account);

    internal void Unchose(long number) => Starts.Unchose(number);

    /// <inheritdoc cref="RosterStarts.Release"/>
    public void Release(long number, string? session = null) => Starts.Release(number, session);

    internal Dictionary<string, AccountFacts> Facts(string owner, IReadOnlyList<string> list, DateTimeOffset now, long except = 0)
        => Upcoming.Facts(owner, list, now, except);

    internal Task<IReadOnlyList<string>> ReadyAsync(string adapter, HarnessToolchain toolchain, DriverConfig config, string? picked, DateTimeOffset now, CancellationToken ct)
        => Upcoming.ReadyAsync(adapter, toolchain, config, picked, now, ct);

    /// <inheritdoc cref="RosterNext.WiringAsync"/>
    public Task<StartWiring> WiringAsync(string adapter, DriverConfig config, string? workspace, CancellationToken ct = default)
        => Upcoming.WiringAsync(adapter, config, workspace, ct);

    /// <summary>
    /// How an account's windows are asked in a test (CODEXUSE1): by owner and account, in place of the agent's server, asked
    /// under the same lock and the same holds, since every real question spawns the agent, which the fast half may not. The
    /// tool's own sign-in is asked as <see cref="AccountWindows.Own"/> (CODEXUSE3).
    /// </summary>
    internal Func<string, string, CancellationToken, Task<IReadOnlyList<WindowReading>?>>? AskingUsage { get; init; }

    // When a limit was last said of each account in this process, by owner and account (USAGEPROBE1): a cool-off the person
    // ended early leaves none in the file to say it.
    internal readonly ConcurrentDictionary<string, DateTimeOffset> _limitSaid = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc cref="RosterUsage.UsageOf"/>
    public UsageQuestion? UsageOf(string adapter) => Usage.UsageOf(adapter);

    /// <inheritdoc cref="RosterUsage.Speaks"/>
    public bool Speaks(string adapter) => Usage.Speaks(adapter);

    internal Task FreshenAsync(string adapter, string owner, IReadOnlyList<string> list, DriverConfig config, CancellationToken ct, RotationUse? use = null)
        => Usage.FreshenAsync(adapter, owner, list, config, ct, use);

    internal ProbeWhy? Owed(string owner, string account, RotationUse use, DateTimeOffset now, IReadOnlyList<CoolingEntry> offered)
        => Usage.Owed(owner, account, use, now, offered);

    /// <inheritdoc cref="RosterUsage.ProjectionOf"/>
    public AccountProjection? ProjectionOf(string owner, string account, AccountSaid? said, DateTimeOffset now)
        => Usage.ProjectionOf(owner, account, said, now);

    internal Dictionary<string, AccountFacts> Projected(string owner, IReadOnlyList<string> accounts, IReadOnlyDictionary<string, AccountFacts> facts, DateTimeOffset now)
        => Usage.Projected(owner, accounts, facts, now);

    /// <inheritdoc cref="RosterUsage.ReadUsageAsync"/>
    public Task ReadUsageAsync(string adapter, string owner, IReadOnlyList<string> accounts, DriverConfig config, CancellationToken ct = default)
        => Usage.ReadUsageAsync(adapter, owner, accounts, config, ct);

    /// <inheritdoc cref="RosterUsage.SwitchCandidates"/>
    public IReadOnlyList<SwitchCandidate> SwitchCandidates(string owner, IReadOnlyList<string> list, (string Agent, string? Version, string Model)? offer = null)
        => Usage.SwitchCandidates(owner, list, offer);

    internal Task<IReadOnlyList<WindowReading>?> AskUsageAsync(string adapter, string owner, string account, DriverConfig config, CancellationToken ct)
        => Usage.AskUsageAsync(adapter, owner, account, config, ct);

    internal Task PressUsageAsync(string adapter, string owner, DriverConfig config, CancellationToken ct)
        => Usage.PressUsageAsync(adapter, owner, config, ct);

    private RosterCooling? _cooling;
    private RosterCooling Cooling => LazyInitializer.EnsureInitialized(ref _cooling, () => new RosterCooling(this));
    private RosterReport? _reporting;
    private RosterReport Reporting => LazyInitializer.EnsureInitialized(ref _reporting, () => new RosterReport(this));
    private RosterSelection? _selecting;
    private RosterSelection Selecting => LazyInitializer.EnsureInitialized(ref _selecting, () => new RosterSelection(this));
    private RosterWalk? _walking;
    private RosterWalk Walking => LazyInitializer.EnsureInitialized(ref _walking, () => new RosterWalk(this));
    private RosterNext? _upcoming;
    private RosterNext Upcoming => LazyInitializer.EnsureInitialized(ref _upcoming, () => new RosterNext(this));
    private RosterStarts? _starts;
    private RosterStarts Starts => LazyInitializer.EnsureInitialized(ref _starts, () => new RosterStarts(this));
    private RosterUsage? _usage;
    private RosterUsage Usage => LazyInitializer.EnsureInitialized(ref _usage, () => new RosterUsage(this));
}
