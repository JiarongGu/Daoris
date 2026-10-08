using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The driver's watch loop, in the shell's process — the same library the headless host runs, one
/// driver with two doors (D46 §7). In-process because the shell is where the tick reports become
/// something a person sees, and where session process control lives. The loop mechanics are
/// <see cref="DriverWatch"/>'s; this class owns only what the shell adds — bringing the host up, and
/// turning reports into event-bus notifications.
/// </summary>
/// <param name="home">
/// What establishing the install's home did (D63), when this shell is an install and something is
/// worth saying — state moved in, the variable set for the person's account, or the account's home
/// overridden by the install's own (D105). Null otherwise.
/// </param>
public sealed class DriverLoop(
    IEventBus eventBus, HostSupervisor supervisor, string serviceUrl, HomeEstablished? home = null,
    // Daoris's own browser (D78): the shell's window, asked for by a plugin server that drives it.
    IInAppBrowser? browser = null,
    // The shell's machine log (LOG1b, D94): what the person runs and how long it takes. Null writes none.
    MachineLog? log = null,
    // The account's own DAORIS_HOME, which a terminal reads (LEFT2); the user environment's by default, and a
    // test's stand-in so a test never reads the machine's.
    Func<string?>? account = null) : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource<bool> _hostReady = new();
    private DriverWatch? _watch;
    private Task? _loop;

    /// <summary>Completes when the HTTP host answers (or provably will not) — what navigation waits on.</summary>
    public Task<bool> HostReady => _hostReady.Task;

    /// <summary>
    /// The shell's machine log (D94), or null where none was handed in — for a route that writes what the person
    /// did through it, as the preview's opening is (LEFT2).
    /// </summary>
    public MachineLog? Log => log;

    /// <summary>The live processes, shared across ticks — how "stop that session" reaches its target.</summary>
    /// <remarks>
    /// Marked under the home's <c>sessions/</c>, so a terminal's driver sharing the home can tell this
    /// loop's live sessions from orphans, and this loop can tell its (2026-09-25).
    /// </remarks>
    public SessionProcesses Processes { get; } = new(Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(DriverConfig.ResolvePath()))!, "sessions"));

    /// <summary>
    /// What sessions are saying, as they say it (D49 §2) — shared across ticks for the same reason the
    /// processes are, and readable only through this shell: output is transcript-class material and
    /// never leaves the machine (D47 §4).
    /// </summary>
    public SessionOutput Output { get; } = Announcing(new SessionOutput(), eventBus);

    /// <summary>
    /// A stream opening or ending (CONSOLE2c) becomes the page's <c>SESSION_STREAMS</c> event. It names
    /// the session and nothing else: the page asks for the list, so a missed event costs nothing.
    /// </summary>
    private static SessionOutput Announcing(SessionOutput output, IEventBus bus)
    {
        output.Streamed += session => _ = bus.EmitAsync("DAORIS", "SESSION_STREAMS", new { Session = session });
        return output;
    }

    /// <summary>
    /// What sessions did, as typed events (D76 §2) — the conversation the page renders, kept under the
    /// home beside the transcripts so it outlives the console's window and a restart. Machine-local by
    /// the same rule, and reachable only over this bridge.
    /// </summary>
    public SessionEvents Events { get; } = new(Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(DriverConfig.ResolvePath()))!, "sessions"));

    /// <summary>
    /// What sessions consumed (TOOL3/D57 §4) — machine-local by the same rule as the console and the
    /// profile name it records, and reachable only over this bridge. Its home is the Daoris home,
    /// which is the directory `driver.json` sits in.
    /// </summary>
    public SessionUsage Usage { get; } = new(
        Path.GetDirectoryName(Path.GetFullPath(DriverConfig.ResolvePath()))!);

    /// <summary>
    /// Conversations, once the loop is up (D49 §3) — null before the host answers, because a chat
    /// needs the service that holds its record. The control surface says so rather than failing
    /// obscurely: "not yet" is a state a person can wait out.
    /// </summary>
    public ChatRunner? Chat { get; private set; }

    /// <summary>
    /// The machine as Ask Daoris's room says it (ASKHIST1): handed by the module that writes the room on <c>START_HELP</c>, and
    /// handed on to the conversations as they come up, so an earlier conversation going on finds the room as an open would.
    /// </summary>
    public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp { get; set; }

    /// <summary>
    /// The service this loop drives, once the host answers — null before, like <see cref="Chat"/>
    /// and for the same reason.
    /// </summary>
    /// <remarks>
    /// <b>Exposed so the person's own moves go through the same door the loop's do</b> (D52 §4). A
    /// parked session is cleared by a judgement rather than by an observation, and the record and
    /// the process must move together: this machine lets the process go, then advances the record.
    /// A page that advanced the record by itself would leave a `completed` record beside a process
    /// still running here, which is the one lie the observed lifecycle exists to prevent.
    /// </remarks>
    public ServiceClient? Service { get; private set; }

    /// <summary>Where this loop reads the person's choices — what the control surface edits.</summary>
    public string ConfigPath { get; } = DriverConfig.ResolvePath();

    /// <summary>The Daoris home (D63): the directory every machine-local file lives in.</summary>
    public string Home => DriverConfig.HomeOf(ConfigPath);

    /// <summary>
    /// What establishing the home did on this start, when it is worth a person's attention — state
    /// moved in from a profile directory, the account's environment gaining the variable, or the
    /// account's home set aside for this install's own (D105), which lasts as long as the variable
    /// names the other folder, so it is said on every such start. Carried
    /// in the state as well as raised once, because the page subscribes only after the host answers,
    /// and a one-time sentence raised before that is a sentence nobody read.
    /// </summary>
    public string? HomeNotice => home is { Worth: true } ? home.Notice : null;

    /// <summary>
    /// How <see cref="Home"/> stands to the account's DAORIS_HOME (LEFT2), a <see cref="Desktop.HomeAccount"/> value: the
    /// same folder, overridden by this install's own, or named for this start alone. Settings → Driver's home hint
    /// reads it to say whether a terminal's daoris reads this folder, where it read the notice's English before —
    /// and a start named for itself alone has no notice at all.
    /// </summary>
    public string HomeAccount => InstallHome.AccountOf(Home, home, (account ?? InstallHome.AccountVariable)());

    /// <summary>
    /// The host the shell adopted is serving a page that is not this install's (case study 4d), or
    /// null. Carried in the state for the same reason the home's notice is: it was a toast, and a
    /// toast raised before the page subscribed — which is exactly when adoption is decided — reached
    /// nobody. The one surface item the second deployment left unfiled.
    /// </summary>
    public string? HostNotice => supervisor.Notice;

    /// <summary>
    /// This machine's harnesses and the accounts they run as (D49 §4) — the roster surface reads it,
    /// and both spawn doors ask it the same question before starting anything.
    /// </summary>
    /// <remarks>
    /// Constructed eagerly, unlike <see cref="Chat"/>: detection needs no service, so a person can see
    /// what is installed and log a profile in while the host is still coming up — which is exactly the
    /// moment a machine being set up has the question.
    /// </remarks>
    public HarnessRoster Harnesses { get; } = new(WithPlugins(AdapterSet.Built()));

    /// <summary>
    /// The build's adapters plus whatever the home's plugins declare (D64) — read at construction so
    /// the roster a person opens before the loop's first tick already knows a declared harness; every
    /// tick re-reads the catalogue and hands the roster the live set.
    /// </summary>
    private static AdapterSet WithPlugins(AdapterSet built) =>
        built.WithPlugins(PluginCatalog.Load(
            Path.GetDirectoryName(Path.GetFullPath(DriverConfig.ResolvePath()))!, built.Names));

    /// <summary>The plugins that speak (D64): their processes live with this loop and stop with it.</summary>
    private HookSet? _hooks;

    /// <summary>
    /// The loop's own record of each plugin's health (PLUGUI1d, D119 §2): fed by its hook set and by every landing and
    /// hand-off a route runs in this process, and read for the Plugins view, which a terminal reads from the log instead.
    /// </summary>
    public PluginHealth Health { get; } = new();

    /// <summary>Which plugins have a hook process up right now, by id — what the Plugins card shows as running.</summary>
    public IReadOnlyList<string> RunningPlugins => _hooks?.Running ?? [];

    /// <summary>Stop one plugin's hook process now — before its folder is removed (REV3).</summary>
    public Task<bool> StopPluginAsync(string id) => _hooks?.StopAsync(id) ?? Task.FromResult(false);

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown.</summary>
    public void Nudge()
    {
        Interlocked.Increment(ref _nudges);
        _watch?.Nudge();
    }

    private long _nudges;

    /// <summary>How many times a route asked the loop to look now: what a test holds "the loop is nudged" by (MSG1d).</summary>
    public long Nudges => Interlocked.Read(ref _nudges);

    /// <summary>
    /// Whether an update drains this loop (UPDATE1, D139 §2): the install's <see cref="InstallUpdater.Draining"/>, handed in
    /// once both exist, and asked by the watch at every look. Null, as in a workspace build, holds nothing.
    /// </summary>
    public Func<bool>? Draining { get; set; }

    /// <summary>
    /// What an update waits on (D139 §2): the driven sessions this loop runs — a quest's, a resume, a carry-on, an intake —
    /// and the conversations whose turn is in flight. A conversation between turns, a parked session and a terminal are idle.
    /// </summary>
    public UpdateWork Work() => new(
        _watch?.Running.Running ?? 0,
        Chat is { } chat ? Processes.Running.Count(chat.Taking) : 0);

    /// <summary>
    /// A session's console lines, as the page's one event for them. The shape is the page's contract,
    /// so one writer builds it: the relay's batches and a harness action's lines alike (REV3 CLEAN1).
    /// </summary>
    /// <param name="live">
    /// Whether the console still runs as the batch goes out: a session's last words are written after it
    /// closed, and a batch the page read as live re-marked a finished session so (CONSOLE3c).
    /// </param>
    internal static Task EmitOutput(IEventBus bus, string session, IEnumerable<ConsoleLine> lines, bool live = true) =>
        bus.EmitAsync("DAORIS", "SESSION_OUTPUT", new
        {
            Session = session,
            Lines = lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
            Live = live,
        });

    /// <summary>
    /// One quest as the tick hands it to the page: its id, its repository, the verdict and the driver's sentence, which
    /// the page translates by the verdict and never by its words (UX5 U27). One writer, since the shape is the page's
    /// contract.
    /// </summary>
    /// <remarks>
    /// <b>Whose stop holds it</b> (SESSUX1d, D126 §3.3): a <c>Stopped</c> verdict names the session stopped, so the page
    /// can say the sentence in the reader's language and name the session as a fact rather than reading it out of the
    /// driver's English. Null for every other verdict, which the bridge leaves out. The stop's tree stays here.
    /// <para><b>How many failed, and since when</b> (SESSUX1i, D126 §4.6): an <c>Exhausted</c> verdict, with its park read,
    /// carries the number the planner parked it at, so the page says why in the reader's language, and when its last
    /// session ended, which *What needs you* counts its wait from. Null for every other verdict and a park not read yet.
    /// The last session and its note stay here, as the stop's tree does.</para>
    /// <para><b>Which account it waits for</b> (TOOL4g, D125 §4): a quest held at spawn because every account its start may use
    /// is cooling waits for an account, not parked and with no Retry. The wait names whose account, which (null for the tool's
    /// own sign-in), until when, and whether the agent named the time, so the page says it in the reader's language. Null where
    /// no wait of the look holds this quest on a cooling account: a wait on signed-out accounts alone has no time (UX6d1), and
    /// its quest says its accounts by <c>SignedOut</c>. A profile name rides this bridge only (D47 §4).</para>
    /// <para><b>Which accounts it passed not signed in</b> (TOOL6g): beside a wait or with none cooling, whose and which, so the
    /// page names each with its sign-in. Null where it passed none.</para>
    /// <para><b>Whose take holds it</b> (CARRY2c): a <c>TakenElsewhere</c> verdict names the machine that took the quest and its
    /// session where a teammate's record named them, or that it was taken here after the session ended, and the session here
    /// not carried on. Null for every other verdict.</para>
    /// </remarks>
    /// <param name="park">The quest's park as the loop last read it (<see cref="QuestParkReader"/>), or null.</param>
    /// <param name="wait">The look's wait on a cooling account, where it holds this quest (<see cref="TickReport.Waits"/>), or null.</param>
    public static object TickConsideration(Consideration consideration, QuestPark? park = null, AccountWait? wait = null)
    {
        var parked = consideration.Verdict == StartVerdict.Exhausted
                     && park is not null
                     && string.Equals(park.Quest, consideration.Quest.Id, StringComparison.OrdinalIgnoreCase)
            ? park
            : null;
        // A wait on signed-out accounts has no reset (UX6d1): its quest waits for a sign-in, which `SignedOut` says, never a time.
        var waiting = wait is { Until: not null } && wait.Quests.Contains(consideration.Quest.Id, StringComparer.OrdinalIgnoreCase)
            ? wait
            : null;
        return new
        {
            Quest = consideration.Quest.Id,
            Repository = consideration.Quest.To,
            Verdict = consideration.Verdict.ToString(),
            consideration.Reason,
            HeldBy = consideration.HeldBy?.Session,
            // Whose pause holds it (PAUSE1b, D132 §2.3): the scope and the id, so the page says it from facts. Null otherwise.
            PausedBy = consideration.PausedBy is { } pause ? new { Scope = pause.Word, pause.Id } : null,
            parked?.Strikes,
            parked?.Since,
            // Each account with the name the person gave it, read when the look said it (ACCT2b): the page says the name, null
            // where there is none, and the id stays the fact a sign-in takes.
            WaitsFor = waiting is null ? null : new { waiting.Agent, waiting.Account, waiting.Name, waiting.Until, waiting.Stated },
            // The accounts its start passed not signed in (TOOL6g), whose: the page says each with its sign-in. Null otherwise.
            SignedOut = consideration.SignedOut is { } passed ? new { passed.Agent, passed.Accounts, passed.Names } : null,
            // Held by an update's drain (UPDATE1, D139 §2): a fact the page says in the reader's language. Null otherwise.
            ForUpdate = InstallUpdate.IsHeldForUpdate(consideration) ? true : (bool?)null,
            // Whose take holds it, where it is not this machine's (CARRY2c): the machine and its session where a record named
            // them, a take made here, and the session here not carried on, so the page says it from facts. Null otherwise.
            TakenBy = consideration.TakenBy is { } taken ? new { taken.Machine, taken.Session, taken.Here, taken.Last } : null,
        };
    }

    /// <summary>
    /// One wait as the tick hands it to the page (UX6d, D150 §6.2): starts held because every account they may use is
    /// cooling, one per account (<see cref="TickReport.Waits"/>), so *What needs you* lists a start waiting for accounts.
    /// </summary>
    /// <remarks>
    /// <para><b>An ask's intake is among them.</b> A consideration names a quest alone, so a held intake reached the page
    /// only as the driver's toast and its log line, and the band could not say it waited (the TOOL6g note, TOOL4m's row).
    /// The asks are carried by id; where each would have run (<c>ask #id</c>) stays here, since the page names an intake by
    /// its ask.</para>
    /// <para><b>And a start held on signed-out accounts with none cooling</b> (UX6d1): one per agent, its <c>until</c> and its
    /// <c>account</c> null and <c>stated</c> false, its <c>signedOut</c> the accounts a sign-in frees. It waits for a person,
    /// not a time.</para>
    /// <para><b>Facts, never the driver's English</b>: whose account, which and the name the person gave it (ACCT2b), the
    /// workspace where the held starts share one, until when and whether the agent named the time, what it holds, and the
    /// accounts the starts passed signed out (TOOL6g). The page says them in the reader's language. The adapter stays too:
    /// the page names the agent, never a door.</para>
    /// <para>🔴 <b>Nothing is asked to build it</b> (D150 §6.3): the look's own wait, read from the cool-offs and the refusals
    /// its starts met (D125 §4), so no row on the page starts a process to find out. A profile name rides this bridge only
    /// (D47 §4).</para>
    /// </remarks>
    public static object TickWait(AccountWait wait) => new
    {
        wait.Agent,
        wait.Account,
        wait.Name,
        wait.Workspace,
        wait.Until,
        wait.Stated,
        wait.Quests,
        wait.Asks,
        wait.SignedOut,
    };

    /// <summary>
    /// What the waits SAY, as one string (UX6d), equal when the same accounts hold the same starts until the same time,
    /// whatever the order. The tick is forwarded when it changes, as it is for the considerations' signature: a wait that
    /// begins or ends for an intake alone moves no consideration. A wait on signed-out accounts signs with no time (UX6d1).
    /// </summary>
    public static string WaitsSignature(IEnumerable<AccountWait> waits) =>
        string.Join("\n", waits
            .Select(wait => string.Join("\t",
                wait.Agent, wait.Account ?? "", wait.Workspace ?? "", wait.Until?.ToUnixTimeSeconds(), wait.Stated,
                string.Join(",", wait.Quests), string.Join(",", wait.Asks), string.Join(",", wait.SignedOut)))
            .OrderBy(line => line, StringComparer.Ordinal));

    /// <summary>
    /// What the last tick held for the harness's trust (D73) — the only grants the screen may confirm.
    /// </summary>
    public TrustHolds Trust { get; } = new();

    /// <summary>
    /// What the last tick parked by its strikes (DRV6) — the verdict the quest drawer shows its Retry by, and what
    /// Ask Daoris's <c>retry</c> is judged against and its room lists (HELP10).
    /// </summary>
    public ParkedQuests Parked { get; } = new();

    /// <summary>
    /// Everything the last tick considered (SESSUX1a): the planner's verdicts Sessions' groups read, so a session's
    /// *parked* or *awaiting reply* is what the loop decided. Null before the first tick, when a fresh plan is read.
    /// </summary>
    public LastLook Look { get; } = new();

    /// <summary>The loop's syncs, once it is up — the ones its tick runs, and the ones *Sync now* runs.</summary>
    private RemoteSyncSet? _sync;

    /// <summary>
    /// One circle's pass now (SYNC6b): the tick's own pass through the loop's own sync set, so the
    /// screen's *Sync now* and `daoris-driver sync` run the same thing (D50). Null before the loop is up.
    /// </summary>
    /// <exception cref="DriverException">The circle has no remote on this machine.</exception>
    public Task<SyncReport>? SyncNowAsync(string workspace, CancellationToken ct) =>
        _sync?.RunOnceAsync(workspace, ct);

    public void Start()
    {
        _loop = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        var ct = _stopping.Token;

        // Whatever happens in here, HostReady must complete — the splash awaits it, and a supervisor
        // fault that skipped the TrySetResult would leave a permanently dark window, the exact failure
        // the form's own fallback was written to prevent.
        var up = false;
        string? trouble = null;
        try
        {
            up = await supervisor.EnsureAsync(ct).ConfigureAwait(false);
            trouble = supervisor.Trouble;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            trouble = error.Message;
        }
        finally
        {
            _hostReady.TrySetResult(up);
        }

        if (!up)
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = trouble })
                .ConfigureAwait(false);
            return;
        }

        // The host is up and it is somebody else's, serving a page that is not this install's (case
        // study 4d). Said once, on the channel a person has to act on — like a parked session, it is
        // waiting on them — and the loop goes on, because the host does work.
        if (supervisor.Notice is { } notice)
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = notice })
                .ConfigureAwait(false);
        }

        // The home's own news (D63) on the same channel, for the same reason: a `~/.daoris` that just
        // moved into the install, or a variable just set for the account, is something the person acts
        // on once — a terminal opened before it was set does not see it. And an account's home this
        // install set aside for its own (D105): a terminal still reads the other folder.
        if (home is { Worth: true })
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = home.Notice })
                .ConfigureAwait(false);
        }

        var homeDirectory = Home;
        var key = Environment.GetEnvironmentVariable(ServiceClient.KeyVariable);
        using var service = new ServiceClient(serviceUrl, key);

        // Every session this loop and its conversations open, move and hear, timed into the machine log
        // (LOG1b) — declared after the client and before the chat runner, so it hears the runner's
        // closing moves and lets go before the client does.
        using var sessionLog = log is null ? null : new SessionLog(log, service, Events);

        // The machine's remotes — one per workspace that has one (D47 §9, D48 §5). The syncs ride the
        // tick, in the shell exactly as in the headless host. Absence is silent and local.
        using var sync = RemoteSyncSet.FromEnvironment(service.BaseUrl, key);
        _sync = sync;

        // Live console lines become IPC events, BATCHED by the library's relay (D49 §2). The shell's
        // half is only what a batch becomes: the page asks for the backlog once over `TAIL_SESSION`
        // and takes everything after it from here.
        using var console = new ConsoleRelay(Output, (session, lines) => EmitOutput(eventBus, session, lines, Output.IsLive(session)));

        // The conversation's events, the same way (D76, CONV1): the page reads the history once over
        // `SESSION_HISTORY` and takes everything after it from here, asking for a gap it notices.
        using var conversation = new EventRelay(Events, (session, events) =>
            eventBus.EmitAsync("DAORIS", "SESSION_EVENTS", new { Session = session, Events = events.ToArray() }));

        // Conversations share everything the loop has — the same service, the same process registry
        // (so one lock and one "stop" reach both kinds), the same console buffer.
        // …and the same harness roster, so one probe serves both doors and a login the person just
        // did is seen by whichever of them asks next.
        // 🔴 Declared AFTER the client, so it is disposed BEFORE it: disposing the runner ends each
        // conversation and writes its record through this client, and the language's reverse order is
        // what guarantees the client is still there. Stopped after the client, a chat open at close was
        // recorded nowhere and read `working` forever (2026-09-25).
        // …and the same usage record, so a conversation's end and a driven session's are one writer (USAGE1).
        // …and the same log, where each plugin server a conversation is handed or withheld is a line (PLUGUI1e).
        using var chat = new ChatRunner(
            service, Harnesses.Adapters, homeDirectory, Processes, Output, Harnesses, Events, Usage, browser,
            plugins: new PluginLog(log, Health));
        // A conversation's model and effort each time they change (AGT6b, D98), in the shape `SESSION_OPTIONS`
        // answers with, so a page that asked once follows them. Machine-local, like the queue.
        chat.OptionsChanged += (session, options) =>
            _ = eventBus.EmitAsync("DAORIS", "SESSION_OPTIONS_CHANGED", DriverModule.OptionsAnswer(session, options));
        await ComeUpAsync(service, chat).ConfigureAwait(false);

        // Every loop on the home watches the requests a terminal's `sessions stop|finish|decline` writes (SESSUX1g, D126
        // §7.1): one for a session this registry runs, a conversation or a driven session, is acted on as the screen's
        // route would act. Before the home's lock, so a conversation is reached while a terminal's loop drives the home.
        await using var requests = new SessionRequestWatch(homeDirectory, Processes, () => Service)
        {
            // A terminal's `sessions say` (MSG1e2) is judged as the box's words are: a conversation the window runs hears it,
            // kept words are shown at once and the loop nudged, and words said as a session winds up wait here.
            Say = Words.HoldAsync,
        };

        // A plugin's word goes to the console under `plugin:<id>` (D49 §2, D64 §4) — the same buffer
        // a session's lines and a harness action's lines land in, readable only over this bridge.
        // What the set does with each plugin goes to the machine log without its words, and into the loop's
        // record of its health (PLUGUI1d).
        _hooks = new HookSet(homeDirectory, Output, log: log, health: Health);

        // What is worth interrupting the person for (SURF5b). The judgement is the library's, so the
        // headless host reaches the same answer; the shell's half is only what an event BECOMES.
        // One live driver per home (DRV8a, D104): the loop waits for the home's lock while a headless loop
        // holds it, and everything above — the host, the page, the conversations — carries on meanwhile.
        using var held = await HoldHomeAsync(ct).ConfigureAwait(false);
        if (held is null) return;

        var attention = new AttentionWatch();
        // A quest's park is said with its last session's facts, and Overview's row waits from its end (SESSUX1i).
        var parks = new QuestParkReader();
        string? lastConsidered = null;
        var lastWaited = "";
        string? lastAsked = null;
        string? lastActive = null;
        string? lastRegistered = null;
        var failures = new TickErrors();

        _watch = new DriverWatch(service, ConfigPath, homeDirectory, Processes, sync, Output, Harnesses, Usage, _hooks, Events, browser)
        {
            // Asked at every look, so an update staged or put off between looks holds or frees the next one (UPDATE1).
            Draining = () => Draining?.Invoke() == true,
            // Where a run's failure nothing else awaits is written with its place, not left to the finalizer (ANSWER2).
            Log = log,
        };
        await _watch.RunAsync(
            async (report, ticked) =>
            {
                failures.Ran();

                // Observed either way, so turning notifications back on does not then announce
                // everything that happened while they were off — the switch is about being TOLD.
                var read = await parks.LookAsync(
                    report, ticked.ForgivenAt, token => SessionRecords.ReadAsync(service.BaseUrl, key, ct: token), ct)
                    .ConfigureAwait(false);
                var attend = attention.Observe(report, read);
                if (ticked.Notify)
                {
                    foreach (var item in attend)
                    {
                        await eventBus.EmitAsync("DAORIS", "SESSION_ATTENTION", new
                        {
                            Kind = item.Kind.ToString(),
                            item.Session,
                            item.Repository,
                            item.State,
                            item.Headline,
                            item.Detail,
                            // The quest a park names (SESSUX1i); null for a session's own news.
                            item.Quest,
                        }).ConfigureAwait(false);
                    }
                }

                // Forwarded when the tick has something to say: it did something, or what it says
                // about the sitting quests CHANGED. The considerations are a standing answer the
                // Overview renders — "why is this sitting" (D46 §3) — so a change has to reach the
                // page, and a stable set must not: every tick the page receives refetches four
                // queries, and a machine with one held quest would otherwise send one every poll.
                var considered = Considerations.Signature(report.Considerations);
                // …or the parks were read again (SESSUX1i): a park read a look after its verdict, its first read refused,
                // still has to reach the row its wait and its number are said on.
                var changed = considered != lastConsidered || read is not null;
                lastConsidered = considered;
                // …or what the waits say (UX6d): an intake held on its accounts, or let go, moves no consideration.
                var waited = WaitsSignature(report.Waits);
                changed |= waited != lastWaited;
                lastWaited = waited;

                // What this tick held for trust, kept for the screen's grant to be checked against
                // (D73) — replaced whole, so a folder the driver stopped holding cannot be granted.
                Trust.Record(report.Untrusted);

                // And what it parked by its strikes (HELP10), replaced whole the same way, so Ask Daoris proposes a
                // retry only of a quest the drawer would offer Retry on.
                Parked.Record(report.Considerations);

                // And all of it, whole, for the session list's groups (SESSUX1a): parked and awaiting reply are its verdicts;
                // with the cool-offs it held starts on, which a record whose words one holds names (MSG1f2).
                Look.Record(report.Considerations, report.Waits);

                // 🔴 And the asks (INT4d): the attention band reads them beside the sessions, and an
                // ask made by the other door — a terminal, a teammate's sync — moves nothing above,
                // so a quiet tick never told the page and the band missed it until a reload. Read
                // here because the tick reads asks only when an intake is named. A host that answers
                // no ask door changes nothing.
                var asked = await AskedAsync(service, ct).ConfigureAwait(false);
                if (asked is not null && asked != lastAsked)
                {
                    changed = true;
                    lastAsked = asked;
                }

                // 🔴 And the sessions (UX5 U13), for the same reason: a conversation started or ended
                // from the main window moves nothing this tick reports, so the monitor and a detached
                // window, which hear of sessions only by a tick, never showed it. Seen on the window: a
                // chat opened in `game`, and a minute later the monitor still did not have it.
                var active = await ActiveAsync(service, ct).ConfigureAwait(false);
                if (active is not null && active != lastActive)
                {
                    changed = true;
                    lastActive = active;
                }

                // 🔴 And the registry (FG4): a folder imported from a terminal, a retire, a re-wire —
                // none moves anything above, and the page said *no workspace yet* over 29 repositories
                // just registered, until it was reloaded.
                var registered = await RegisteredAsync(service, ct).ConfigureAwait(false);
                if (registered is not null && registered != lastRegistered)
                {
                    changed = true;
                    lastRegistered = registered;
                }

                if (report.PlannedAnything || report.Events.Count > 0 || changed)
                {
                    await eventBus.EmitAsync("DAORIS", "DRIVER_TICK", new
                    {
                        Events = report.Events,
                        Considered = report.Considerations
                            .Select(consideration => TickConsideration(
                                consideration,
                                parks.Latest.FirstOrDefault(park =>
                                    string.Equals(park.Quest, consideration.Quest.Id, StringComparison.OrdinalIgnoreCase)),
                                // The wait holding it on a cooling account, if one does (TOOL4g, D125 §4).
                                report.Waits.FirstOrDefault(wait =>
                                    wait.Quests.Contains(consideration.Quest.Id, StringComparer.OrdinalIgnoreCase))))
                            .ToArray(),
                        // The trust holds as facts (D73): machine-local paths, so over this bridge only.
                        // A hold's `quest` or `ask`, whichever it is not, is left out by the bridge.
                        Untrusted = report.Untrusted.Select(hold => new
                        {
                            hold.Folder,
                            hold.TrustFile,
                            hold.Quest,
                            hold.Ask,
                        }).ToArray(),
                        // The starts held on cooling accounts, one per account, an ask's intake among them (UX6d): what
                        // *What needs you* says waits for an account, from the look's own wait and nothing asked.
                        Waits = report.Waits.Select(TickWait).ToArray(),
                    }).ConfigureAwait(false);
                }
            },
            // An unattended loop outlives its service's restarts — say so, wait, look again. Said once
            // until a tick runs again, and a failure it can name is named (UX5 U30).
            onError: error => failures.Failed(error) is { } said
                ? eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { said.Code, said.Message })
                : Task.CompletedTask,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The loop's service, handed to the routes, and the page told (LOOK2a): until now every route that reads the
    /// service refused *still coming up*, and from now it answers.
    /// </summary>
    /// <remarks>
    /// <para><b>The page asks again on this, not on a tick.</b> A page opened on a screen that reads the service asks at
    /// once, is refused, and until LOOK2a kept that refusal until something else asked again: the first tick, which can
    /// wait on a remote's sync or on another driver's lock (DRV8a). Settings → Workspace said no repository had a line
    /// until *Bring up to date* happened to ask again. <c>DRIVER_READY</c> is said once, the moment the refusals stop.</para>
    ///
    /// <para>The service holds its registry the moment it answers at all (its store is read on each ask), so the
    /// service being handed over is the whole of being ready. Called by the loop once its host answers; public so the
    /// tests hold what the page is told.</para>
    ///
    /// <para><b>Its conversations come up with it</b> (D49 §3): a chat needs the service that holds its record, so the
    /// runner is handed over here, and the routes and a terminal's words (MSG1e2) reach it from now. Two things only the
    /// loop does for them (MSG1c2): a chat the runner takes up by itself announces its end, since no route that started it
    /// is there to hear it; and the words kept on an ended chat while no shell ran are taken up now, in the background, so
    /// no look waits on them.</para>
    /// </remarks>
    /// <param name="chat">The loop's conversations, built over <paramref name="service"/>; null keeps none.</param>
    public async Task ComeUpAsync(ServiceClient service, ChatRunner? chat = null)
    {
        // Words held while a session winds up are tried the moment its record moves here (MSG1d), and what a word said now
        // would do is told again as a record moves or a later session of its quest opens (MSG1f2).
        service.Moved += Words.OnMoved;
        service.Opened += Words.OnOpened;
        Words.Reached += (session, reach) =>
            _ = eventBus.EmitAsync("DAORIS", "SESSION_QUEUED", DriverModule.QueueAnswer(this, session, reach));

        // What a person told a driven session, waiting for its turn to end (SESS3), told as the queue is — and `Listening`
        // false once it stops taking any, so the page takes its box away. An open door's reach rides it (MSG1f2); one that
        // closed leaves the session winding up, which only its record answers for, so that is told once it has.
        Processes.HeldChanged += (session, queue) =>
        {
            var reach = queue.Taking ? Words.ReachHere(session) : null;
            _ = eventBus.EmitAsync("DAORIS", "SESSION_QUEUED", new
            {
                Session = session,
                Queued = queue.Queued.Select(message => new { message.Text, Files = Array.Empty<string>() }).ToArray(),
                queue.Taking,
                // An open inbox always has a turn in hand; a closed one tells Idle.
                Listening = queue.Taking,
                Reach = DriverModule.ReachOf(reach),
            });
            if (reach is null) Words.Tell(session);
        };

        if (chat is not null)
        {
            // Where a conversation's turns stand, as it moves (CONV4a): whether one is in flight, and what is waiting, which
            // is in no record until it is sent. Each change is the whole state, so a missed one costs nothing. What a word said
            // now would do rides it where the conversation runs here (MSG1f2); one that ended is told once its record answers.
            chat.QueueChanged += (session, queue) =>
            {
                var reach = Words.ReachHere(session);
                _ = eventBus.EmitAsync("DAORIS", "SESSION_QUEUED", new
                {
                    Session = session,
                    // The words and the names of their files — never where the files are kept.
                    Queued = queue.Queued.Select(message => new { message.Text, Files = message.Files.Select(file => file.Name).ToArray() }).ToArray(),
                    queue.Taking,
                    // Whether what waits, waits for the door to open rather than for a turn (HELP4).
                    queue.Opening,
                    // When its last turn ended here (RAIL2): the chat's last move, which its record never
                    // says. Machine-local, like everything this event carries.
                    LastTurn = queue.LastTurnEnded,
                    Reach = DriverModule.ReachOf(reach),
                });
                if (reach is null) Words.Tell(session);
            };
            chat.TakenUpEnded += (session, state) => _ = Ended(eventBus, session, state);
            // An Ask Daoris conversation going on writes its room as an open does (ASKHIST1), from the module's reading.
            chat.DescribeHelp = ct => DescribeHelp is { } describe ? describe(ct) : Task.FromResult<HelpMachine?>(null);
            Chat = chat;
        }

        Service = service;
        await eventBus.EmitAsync("DAORIS", "DRIVER_READY", new { Ready = true }).ConfigureAwait(false);

        if (chat is not null)
        {
            var stopping = _stopping.Token;
            _ = Task.Run(() => chat.TakeUpAsync(stopping), stopping);
        }
    }

    /// <summary>
    /// A conversation's end, as the page's one event for it (D49 §3): the session and the state its record took. One writer,
    /// since the shape is the page's contract: the routes that start a conversation hand it to their runs, and a chat the
    /// runner took up by itself is announced by it (MSG1c2).
    /// </summary>
    internal static Task Ended(IEventBus bus, string session, string state) =>
        bus.EmitAsync("DAORIS", "SESSION_ENDED", new { Session = session, State = state });

    /// <summary>
    /// What the person's words to a session do, whatever its state (MSG1d, D137 §2): the one judge <c>SESSION_INPUT</c> and
    /// <c>SESSION_QUEUE</c> answer by, and where words said as a session winds up wait for its record to end.
    /// </summary>
    public SessionWords Words => LazyInitializer.EnsureInitialized(ref _words, () => new SessionWords(this));

    private SessionWords? _words;

    /// <summary>How often a loop waiting on another driver's lock looks again (DRV8a).</summary>
    public TimeSpan HoldRetry { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// This home's driver lock, taken for the desktop's loop (DRV8a, D104). While another live driver
    /// holds it — a terminal's `daoris-driver drive` — the loop waits, and says so once, on the channel a
    /// person acts on; it takes the lock the moment that one lets go. Null when the shell closes first.
    /// </summary>
    /// <remarks>
    /// Waited for rather than refused, as a terminal's loop is: the desktop is where a person drives from,
    /// and nothing else it carries races anyone for a quest.
    /// </remarks>
    public async Task<DriverLock?> HoldHomeAsync(CancellationToken ct)
    {
        var told = false;
        while (true)
        {
            DriverLock? held;
            string? said;
            try
            {
                held = DriverLock.TryAcquire(Home, DriverKind.Desktop, out var holder);
                said = holder is null
                    ? null
                    : $"The driver loop is waiting: this home is already driven by {holder.Named}. It starts "
                      + "here the moment that one stops.";
            }
            catch (DriverException error)
            {
                held = null;
                said = error.Message;
            }

            if (held is not null) return held;
            if (!told && said is not null)
            {
                await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = said }).ConfigureAwait(false);
                told = true;
            }

            try
            {
                await Task.Delay(HoldRetry, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }

    /// <summary>What the asks say this tick, or null when the host could not answer — never a change.</summary>
    private static async Task<string?> AskedAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return Asks.Signature(await service.AsksAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>What the registry holds, or null when the host could not answer — never a change.</summary>
    private static async Task<string?> RegisteredAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return Repositories.Signature(await service.RegistryAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Which sessions are active and how, or null when the host could not answer — never a change.</summary>
    private static async Task<string?> ActiveAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return ActiveSessions.Signature(await service.ActiveSessionsAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Stop, bounded: an in-flight session is ended and recorded `stopped` by the driver itself, and so is
    /// a conversation — the loop's run ends by disposing its chat runner, inside its client's scope.
    /// </summary>
    public void Stop()
    {
        _stopping.Cancel();
        _words?.Dispose();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(15));
        }
        catch (AggregateException)
        {
            // Cancellation surfacing as it should; the record writes ride an unbound token inside.
        }

        // The plugins' processes go with the loop (D64 §4, rule 3): registrations are effects, and
        // the effect ends here. Bounded per plugin by the set itself; a plugin that will not leave is
        // ended rather than waited for.
        var hooks = Interlocked.Exchange(ref _hooks, null);
        if (hooks is not null)
        {
            try
            {
                hooks.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(10));
            }
            catch (AggregateException)
            {
                // A plugin that failed while leaving is a plugin that has left.
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _stopping.Dispose();
    }
}

/// <param name="Code">A failure the page words from its catalogue, or null when the words are the driver's.</param>
/// <param name="Message">The driver's own sentence, which a page that does not know the code shows.</param>
public sealed record TickError(string? Code, string Message);

/// <summary>
/// What a failed tick tells the page (UX5 U30): said once until a tick runs again, and a failure the
/// driver can name is named.
/// </summary>
/// <remarks>
/// <para>🔴 With the machine's service stopped, the shell toasted the .NET socket's own words on every
/// poll, <i>No connection could be made because the target machine actively refused it.
/// (127.0.0.1:5188)</i>, beside the page's own sentence for the same fact. A refused connection is
/// <c>SERVICE_UNREACHABLE</c>, the code the page's own requests use, so both doors say one sentence
/// in the reader's language. A tick report is a log and a toast is an interruption (D62), so the same
/// failure again is not news.</para>
///
/// <para>Called by the watch loop alone, one tick at a time, so it holds no lock.</para>
/// </remarks>
public sealed class TickErrors
{
    private string? _last;

    /// <summary>The failure to tell the page, or null when it is the one already told.</summary>
    public TickError? Failed(Exception error)
    {
        var said = error is HttpRequestException { StatusCode: null }
            ? new TickError("SERVICE_UNREACHABLE", "the service is not answering — the driver looks again in a few seconds.")
            : new TickError(null, error.Message);
        if (said.Message == _last) return null;
        _last = said.Message;
        return said;
    }

    /// <summary>A tick ran, so the next failure is news again.</summary>
    public void Ran() => _last = null;
}
