using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The driver's watch loop, in the shell's process — the same library the headless host runs, one
/// driver with two doors (D46 §7). In-process because the shell is where the tick reports become
/// something a person sees, and where session process control lives. The loop mechanics are
/// <see cref="DriverWatch"/>'s; this class owns only what the shell adds — bringing the host up, and
/// turning reports into event-bus notifications. Its one run is <see cref="LoopRun"/>'s, and what the
/// run brings up and these doors read is <see cref="LoopState"/>.
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
    Func<string?>? account = null,
    // Daoris's browser as a set-up step's review uses it (REVIEWENV1d): its tab, and its build served there until the person's
    // verdict. Null shows nothing, as a loop with no browser does.
    IReviewTabs? reviews = null) : IDisposable
{
    /// <summary>
    /// What the loop does when a conversation's start receipt is written (HELPWIREDLIVE1b, D183 point 4): the help scope is told with
    /// that session. Public for the modules tests to reach the subscription.
    /// </summary>
    public static Action<string> WiredKeptTold(IEventBus bus) =>
        session => StatePublisher.Publish(bus, StateScopes.Help, session);

    /// <summary>
    /// The review's showing in Daoris's browser (REVIEWENV1d, D154 point 5): one for the shell's life, handed to every look's
    /// driver, read by the strip's chip and pressed by its *Show it again*. Null where this loop carries no browser.
    /// </summary>
    public ReviewDesk? Reviews { get; } = reviews is null ? null : new ReviewDesk(reviews);

    /// <summary>
    /// The shell's word on Daoris's own browser for clearing its caches (KEEP1e, the cleanup design §3.2): its browser host, handed as the
    /// in-app browser is. Null where this loop carries no browser, which keeps the caches, as a terminal alone does.
    /// </summary>
    public IBrowserCaches? BrowserCaches => browser as IBrowserCaches;

    private readonly LoopState _state = new();
    private Task? _loop;

    /// <summary>Completes when the HTTP host answers (or provably will not) — what navigation waits on.</summary>
    public Task<bool> HostReady => _state.HostReady.Task;

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
    public SessionProcesses Processes { get; } = new(DaorisHome.Entry(HomeOfChoices(), HomeEntry.SessionPids));

    /// <summary>
    /// The home the person's choices are in, asked at construction as each part below asks it: the home whose <c>driver.json</c>
    /// the one resolution finds (HOMELAYOUT3: at its new place or its old, never the file's folder alone).
    /// </summary>
    private static string HomeOfChoices() => DriverConfig.HomeOf(DriverConfig.ResolvePath());

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
        output.Clock = () => DateTimeOffset.UtcNow;
        output.Streamed += session => _ = bus.EmitAsync("DAORIS", "SESSION_STREAMS", new { Session = session });
        return output;
    }

    /// <summary>
    /// What sessions did, as typed events (D76 §2) — the conversation the page renders, kept under the
    /// home beside the transcripts so it outlives the console's window and a restart. Machine-local by
    /// the same rule, and reachable only over this bridge. Watched from the first event for the call each
    /// session's agent is inside (WAITSAY1b), which <c>SESSION_GROUPS</c> answers whether or not a window watched it begin.
    /// </summary>
    public SessionEvents Events { get; } = RunningCalls.Watched(new SessionEvents(DaorisHome.Entry(HomeOfChoices(), HomeEntry.Sessions)));

    /// <summary>
    /// What sessions consumed (TOOL3/D57 §4) — machine-local by the same rule as the console and the
    /// profile name it records, and reachable only over this bridge. Its home is the Daoris home,
    /// which is the home `driver.json` is in.
    /// </summary>
    public SessionUsage Usage { get; } = new(HomeOfChoices());

    /// <summary>
    /// The presses under way (STATEHUB1c2, D169 point 7), the driver's to hold rather than a component's: the shell's
    /// <see cref="StatePublisher"/> writes it, and the <c>UNDER_WAY</c> route reads it.
    /// </summary>
    public PressesUnderWay UnderWay { get; } = new();

    /// <summary>
    /// The driver's state as every window holds it (PUSH1a, D182): one owner, judged from what <c>STATE</c> answers and pushed with
    /// its data when it moves, so no tick reads it again in any window. The <c>STATE_SLICE</c> route reads its snapshot.
    /// </summary>
    public StateSlice DriverSlice => LazyInitializer.EnsureInitialized(ref _driverSlice, () => DriverStateSlice.Of(this, eventBus));

    private StateSlice? _driverSlice;

    /// <summary>The slices this Daoris pushes (PUSH1a): the snapshot route finds them here, and <see cref="Start"/> runs them.</summary>
    public StateSlices Slices => LazyInitializer.EnsureInitialized(ref _slices, () => new StateSlices(
        DriverSlice, LinesStateSlice.Of(this, eventBus), SessionsStateSlice.Of(this, eventBus), CallsStateSlice.Of(this, eventBus),
        WorkStateSlice.Of(this, eventBus), GroupsStateSlice.Of(this, eventBus), SweepStateSlice.Of(this, eventBus),
        SyncStateSlice.Of(this, eventBus), QuestsStateSlice.Of(this, eventBus), AsksStateSlice.Of(this, eventBus),
        UsageStateSlice.Of(this, eventBus), AccountsStateSlice.Of(this, eventBus), AttentionStateSlice.Of(this, eventBus),
        Notices.Slice, ConsiderationsStateSlice.Of(this, eventBus), RulesStateSlice.Of(this, eventBus)));

    private StateSlices? _slices;

    /// <summary>
    /// The one judge of who is at work (COWORKVIEW1a): each landed composition registered, woken by the three slices, this process's
    /// lineage tokens and the lineage files' topic, telling <see cref="StateScopes.AtWork"/> naming no session.
    /// </summary>
    public AtWorkWatch AtWork => LazyInitializer.EnsureInitialized(ref _atWork, () => new AtWorkWatch(
        () => StatePublisher.Publish(eventBus, StateScopes.AtWork), Slices.Find(GroupsStateSlice.Name), Slices.Find(SessionsStateSlice.Name),
        Slices.Find(QuestsStateSlice.Name), LineageTokens.Shared, Slices.Find(DriverStateSlice.Name), HintStanding.Word, Slices.Find(AsksStateSlice.Name)));

    private AtWorkWatch? _atWork;

    /// <summary>
    /// The routing standing the at-work view's hint reads (COWORKVIEW1e-iii): the driver slice's drive and trees with the lanes the tick
    /// last heard from the registry. The first registry answer moves it, and the judge is told (<see cref="LookTold"/>).
    /// </summary>
    public AtWorkStanding HintStanding => LazyInitializer.EnsureInitialized(ref _hintStanding, () => new AtWorkStanding(() => Slices.Find(DriverStateSlice.Name)));

    private AtWorkStanding? _hintStanding;

    /// <summary>The rules' reading, shared by their slice and each look, which tells their scope only where it moved (FOUNDATION1s part B).</summary>
    public RulesReading RulesRead { get; } = new();

    /// <summary>The knowledge index's size as the service lists it, read at each look, which tells the repositories while it feeds.</summary>
    public IndexFeed IndexRead { get; } = new();

    /// <summary>
    /// <i>Done and failed</i> (NOTIFY2f, D196 point 6): the notices this process records and reads again, which the <c>NOTICES_READ</c>
    /// and <c>NOTICE_DISMISS</c> routes write through.
    /// </summary>
    public NoticesStateSlice Notices => LazyInitializer.EnsureInitialized(ref _notices, () => NoticesStateSlice.Of(this, eventBus));

    private NoticesStateSlice? _notices;

    /// <summary>
    /// Each move the change sources heard, as the watch tells the machine's facts (PARALLEL1a): its topic and subject, a ledger family
    /// the change stream named among them, so a slice judged from the service's answers hears a connector's write with no desktop
    /// dispatch (PUSH1d2). Never a press, which is no move.
    /// </summary>
    public event Action<string, string>? MoveHeard;

    /// <summary>What the watch tells of each move it heard: the machine's facts it moves woken, then whoever hears the moves.</summary>
    /// <param name="mine">A ref Daoris moved itself (OWNMOVES1): its repository's facts judged past their rest; the hearers as for any move.</param>
    public void HeardMove(string topic, string subject, bool mine = false)
    {
        HelpFacts.Wake(Facts, topic, subject, mine);
        MoveHeard?.Invoke(topic, subject);
    }

    /// <summary>
    /// Each sync pass's end, the tick's and <i>Sync now</i>'s (PUSH1a2): told by the loop's one set, so the slice of where each circle
    /// stands reads it again. A pass rewrites when it last tried, which the change stream does not carry.
    /// </summary>
    public event Action? Passed;

    /// <summary>What the loop's sync set tells at the end of each pass: whoever hears the passes.</summary>
    public void HeardPass() => Passed?.Invoke();

    /// <summary>The slices' workers, from <see cref="Start"/> until the stop.</summary>
    private Task? _pushing;

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
    /// How a piece's lead's tree is read at the person's ask (COAGENT1b, <c>SESSION_PARTNER_ASK</c>): its commit and uncommitted paths,
    /// git's, or a test's without a process (MOD8).
    /// </summary>
    public Func<string, CancellationToken, Task<PieceBase>> PieceBases { get; set; } = PartnerPersonAsk.ReadTreeAsync;

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

    /// <summary>
    /// The Daoris home (D63) every machine-local file lives in: the home <see cref="ConfigPath"/> is in, asked once, since it
    /// reads the install's marker (HOMELAYOUT3) and the path never changes.
    /// </summary>
    public string Home => _home ??= DriverConfig.HomeOf(ConfigPath);

    private string? _home;

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
    /// The host this window's supervisor started or adopted, and what its last probe heard (WIRING1b): This Daoris reads them
    /// rather than asking the service again on the page's open.
    /// </summary>
    public HostSupervisor Host => supervisor;

    /// <summary>The address this loop's service is at, as the window was handed it (<c>DAORIS_SERVICE_URL</c> or the default).</summary>
    public string ServiceUrl => serviceUrl;

    /// <summary>Whether this window set its own home on this start (D63's establishing): This Daoris says the variable's source is Daoris.</summary>
    public bool HomeSetByDaoris => home is not null;

    /// <summary>
    /// This machine's harnesses and the accounts they run as (D49 §4) — the roster surface reads it,
    /// and both spawn doors ask it the same question before starting anything.
    /// </summary>
    /// <remarks>
    /// Constructed eagerly, unlike <see cref="Chat"/>: detection needs no service, so a person can see
    /// what is installed and log a profile in while the host is still coming up — which is exactly the
    /// moment a machine being set up has the question.
    /// </remarks>
    public HarnessRoster Harnesses { get; } = new(WithPlugins(ProviderRegistry.Built()));

    /// <summary>
    /// The build's adapters plus whatever the home's plugins declare (D64) — read at construction so
    /// the roster a person opens before the loop's first tick already knows a declared harness; every
    /// tick re-reads the catalogue and hands the roster the live set.
    /// </summary>
    private static ProviderRegistry WithPlugins(ProviderRegistry built) =>
        built.WithPlugins(PluginCatalog.Load(HomeOfChoices(), built.Names));

    /// <summary>
    /// The loop's own record of each plugin's health (PLUGUI1d, D119 §2): fed by its hook set and by every landing and
    /// hand-off a route runs in this process, and read for the Plugins view, which a terminal reads from the log instead.
    /// </summary>
    public PluginHealth Health { get; } = new();

    /// <summary>Which plugins have a hook process up right now, by id — what the Plugins card shows as running.</summary>
    public IReadOnlyList<string> RunningPlugins => _state.Hooks?.Running ?? [];

    /// <summary>Stop one plugin's hook process now — before its folder is removed (REV3).</summary>
    public Task<bool> StopPluginAsync(string id) => _state.Hooks?.StopAsync(id) ?? Task.FromResult(false);

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown.</summary>
    public void Nudge()
    {
        Interlocked.Increment(ref _nudges);
        _state.Watch?.Nudge();
    }

    /// <summary>The quests the node runtime last read (FABRIC-CLOSE1q3), for the held list to judge a close against; null before one pass has read.</summary>
    public IReadOnlyDictionary<string, QuestView>? LastQuests => _state.Watch?.LastQuests;

    /// <summary>The runs held at a declined request (INTEGRATE1o part o3), for <i>What needs you</i> to judge; none before a pass has read.</summary>
    public IReadOnlyList<RequestHold> RequestHolds => _state.Watch?.RequestHolds ?? [];

    /// <summary>The runs waiting on a person's task (NODEKIT1 HUMAN), for <i>What needs you</i>; none before a pass has read.</summary>
    public IReadOnlyList<HumanTask> HumanTasks => _state.Watch?.HumanTasks ?? [];

    /// <summary>The landed branches whose integration waits for the owner's card (INTEGRATE1h), for <i>What needs you</i> and the Integration tab; none before a pass.</summary>
    public IReadOnlyList<IntegrationCardWait> CardWaits => _state.Watch?.CardWaits ?? [];

    /// <summary>The repositories whose integration declaration waits for the owner's acceptance (INTEGRATE1h, §5.8), for <i>What needs you</i>; none before a look.</summary>
    public IReadOnlyList<DeclarationOwed> DeclarationsOwed => _state.Watch?.DeclarationsOwed ?? [];

    /// <summary>
    /// Ask the loop's runtime for a whole pass, and wait for it within <paramref name="bound"/> (FABRIC-CUTOVER1d, <see cref="DriverWatch.PassWholeAsync"/>):
    /// a press held by a run's graph for a judgement not made yet. False past the bound, and where no loop runs.
    /// </summary>
    public Task<bool> PassWholeAsync(TimeSpan bound, CancellationToken ct) => _state.Watch?.PassWholeAsync(bound, ct) ?? Task.FromResult(false);

    /// <summary>Cancelled by the loop's stop alone: what work that outlives the window asking it runs under (a landing's press, LANDPRESS1).</summary>
    public CancellationToken Stopping => _state.Stopping.Token;

    /// <summary>Look now because a terminal's words for <paramref name="session"/> were kept: the requests topic's wake (FABRIC-EVENT2b).</summary>
    public void Said(string session)
    {
        Interlocked.Increment(ref _nudges);
        _state.Watch?.Said(session);
    }

    /// <summary>
    /// A main window has the person's focus (FABRIC-END6; the first one's and, since FABRIC-END6a, each later one's): the driver asks a
    /// plugin about each open pull request, within D148's minute.
    /// No look and no nudge; returns at once, so the window's thread waits on nothing.
    /// </summary>
    public void Focused() => _state.Focus.Focused();

    /// <summary>Whether a focus heard before the watch existed waits to be told to it (FABRIC-END6c), which a test reads.</summary>
    public bool FocusPending => _state.Focus.Pending;

    /// <summary>The machine's resume as the shell hears it (FABRIC-EVENT2d); null where the shell hears none, and the watch's overrun rule is all.</summary>
    public PowerResume? Power { get; set; }

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
        _state.Watch?.Running.Running ?? 0,
        Chat?.InFlight ?? 0);

    /// <summary>One quest as the tick hands it to the page (<see cref="TickShown.Quest"/>).</summary>
    public static object TickConsideration(Consideration consideration, QuestPark? park = null, AccountWait? wait = null) =>
        TickShown.Quest(consideration, park, wait);

    /// <summary>Which of a verdict's sentences the planner said, as the page receives it (<see cref="TickShown.Said"/>).</summary>
    public static object? SaidShown(SittingFact? said) => TickShown.Said(said);

    /// <summary>One wait as the tick hands it to the page (<see cref="TickShown.Wait"/>).</summary>
    public static object TickWait(AccountWait wait) => TickShown.Wait(wait);

    /// <summary>What a look's lines say, as one string (<see cref="TickSignatures.Lines"/>).</summary>
    public static string LinesSignature(IEnumerable<string> lines) => TickSignatures.Lines(lines);

    /// <summary>What the waits say, as one string (<see cref="TickSignatures.Waits"/>).</summary>
    public static string WaitsSignature(IEnumerable<AccountWait> waits) => TickSignatures.Waits(waits);

    /// <summary>What the kept second-opinion gates say that waits on the person, as one string (<see cref="TickSignatures.Judged"/>).</summary>
    public static string JudgedSignature(IEnumerable<OpinionJudged> judged) => TickSignatures.Judged(judged);

    /// <summary>What a look moved, as the scopes' own changes (<see cref="LookTold.Scopes"/>).</summary>
    public static StateScopes LookScopes(bool rulesMoved, bool registryMoved, bool indexMoved) =>
        LookTold.Scopes(rulesMoved, registryMoved, indexMoved);

    /// <summary>Whether the registry's signature a look read is a change, which wakes the machine's facts (<see cref="LookTold.RegistryHeard"/>).</summary>
    public static bool RegistryHeard(MachineFacts facts, string? registered, string? last) => LookTold.RegistryHeard(facts, registered, last);

    /// <summary>
    /// What the last tick held for the harness's trust (D73) — the only grants the screen may confirm.
    /// </summary>
    public TrustHolds Trust { get; } = new();

    /// <summary>
    /// What the last look considered of each open quest, as the tick hands it to the page (<see cref="TickConsideration"/>), kept whether
    /// or not the tick was forwarded, for <i>What needs you</i>'s judge (PUSH1d4) to read from memory. Null before the first look.
    /// </summary>
    public IReadOnlyList<object>? Considered => _considered;

    private volatile IReadOnlyList<object>? _considered;

    /// <summary>
    /// Keep what a look considered and ask the considerations' slice to judge it with what the look kept beside it (FOUNDATION1s, D182):
    /// the trust holds and the waits, pushed only where one moved.
    /// </summary>
    public void RecordConsidered(IReadOnlyList<object> sitting)
    {
        _considered = sitting;
        Slices.Find(ConsiderationsStateSlice.Name)?.Wake();
    }

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

    /// <summary>
    /// The last whole look at the sessions this shell took, with when (HELPSTART1): what Ask Daoris's room reads where it is
    /// recent, rather than judge every tree with git again.
    /// </summary>
    public SessionsLooked SessionsLooked { get; } = new();

    /// <summary>
    /// The supervisor's decisions, live in this instance's memory (SUPERVISOR1a-i, D208; the supervisor design §3.4): one for the shell's
    /// life, shared by the routes that keep and overrule a decision and every look of the loop, since both run in the window's process.
    /// Its admission is the pipe's judge (SUPERVISOR1a-ii), read at each ask, so a pipe served after the first look still answers; with
    /// no pipe, or while appointments are not offered, every act is refused. A test sets its own, with the admission it injects.
    /// </summary>
    public QuestionSupervisorDecisions Supervisor
    {
        get => _supervisor ??= new(Home, new PipeDecisionAdmission(this), log);
        set => _supervisor = value;
    }

    private QuestionSupervisorDecisions? _supervisor;

    /// <summary>
    /// Ask Daoris's conversation placed and not yet running (HELPSTART1): the press answers once it is placed, and the words said
    /// to it meanwhile wait here for its door.
    /// </summary>
    public HelpStarting HelpStarting { get; } = new();

    /// <summary>
    /// The machine's facts as the judges keep them (PARALLEL1a, D181 points 1-2): one for the shell's life, its one owner, judged on
    /// background tasks once the loop is up and woken by what the change sources hear, read as last judged by what a person
    /// presses. The module registers Ask Daoris's topics on it; a state hub's push reads it next (D182).
    /// </summary>
    public MachineFacts Facts { get; } = new() { Log = log };

    /// <summary>
    /// What waits at the service's own door for the person (NOTIFY2j, D196 point 4): one wait held from the loop's service, told to
    /// every window at each change, so no window polls. Read by the bridge's <c>SERVICE_CONFIRMATIONS</c>; none before it is told.
    /// </summary>
    public ServiceConfirmations ServiceConfirmations { get; } = new(eventBus, log);

    /// <summary>The memory changes that wait on the person (KNOWCENTRAL2e), read from the service on a poll of the loop's own; <i>What needs you</i> judges them.</summary>
    public MemoryWaits MemoryWaits { get; } = new(eventBus, log);

    /// <summary>
    /// The second-opinion gates the last tick judged (XAGENT1g3), replaced whole each tick: what <c>OPINION_WAITS</c> answers, as of
    /// that tick, at most one poll old. Null before the first.
    /// </summary>
    public LastOpinions Judged { get; } = new();

    /// <summary>
    /// One circle's pass now (SYNC6b): the tick's own pass through the loop's own sync set, so the
    /// screen's *Sync now* and `daoris remote sync` run the same thing (D50). Null before the loop is up.
    /// </summary>
    /// <exception cref="DriverException">The circle has no remote on this machine.</exception>
    public Task<SyncReport>? SyncNowAsync(string workspace, CancellationToken ct) =>
        _state.Sync?.RunOnceAsync(workspace, ct);

    public void Start()
    {
        _loop = Task.Run(() => new LoopRun(this, _state, eventBus, supervisor, serviceUrl, home, browser, log).RunAsync());
        // PUSH1a (D182): the slices judged from the start, whether or not a window is open, and before the service answers, so a
        // window's first snapshot is read from memory.
        _pushing = Task.Run(() => Slices.RunAsync(_state.Stopping.Token));
    }

    /// <summary>
    /// Leave the service's address in the home, naming this process (FINDDAORIS1, D171 point 1): a terminal outside Daoris's
    /// own panel is handed no variable, and a <c>daoris</c> the install carries reads where the service is from here.
    /// Once while it runs; <see cref="Stop"/> takes it away. Not written is not a failure: a terminal then says Daoris is not
    /// running, and the window runs on.
    /// </summary>
    public void Announce() => Address.Announce();

    /// <summary>The address this Daoris left in its home and the pipe named in it, under one lock with the stop's cancel.</summary>
    private LoopAddress Address => LazyInitializer.EnsureInitialized(ref _address, () => new LoopAddress(() => Home, serviceUrl, _state.Stopping));

    private LoopAddress? _address;

    /// <summary>
    /// The pipe a terminal reaches this Daoris on (RPC1a, D171 point 3): the shell hands its own in, the loop serves it once it
    /// holds the home and takes it down at its stop (<see cref="Stop"/>). None in a headless loop or a test that hands none.
    /// </summary>
    public InstancePipe? Pipe { get; set; }

    /// <summary>The start's timer (STARTFAST1), told when the host answers and the pipe is served; none in a headless loop or a test.</summary>
    public StartStages? Stages { get; set; }

    /// <summary>
    /// The longest the work the first screen does not need waits for the page's word that the screen has its data (STARTFAST1b, the
    /// start-path design §5c): the first look's line-following and tidy, and the machine's facts.
    /// </summary>
    public static TimeSpan FirstScreenBound { get; } = TimeSpan.FromSeconds(30);

    /// <summary>How long a stop lets the presses the pipe runs answer before it goes on (the terminal RPC design §6.3).</summary>
    public TimeSpan PipeDrain { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Name the pipe this Daoris now serves in its address (RPC1a, the terminal RPC design §6.3): written only once it is served,
    /// so a terminal that reads it finds it served; the one before it is taken away no more (<see cref="LoopAddress.AnnouncePipe"/>).
    /// </summary>
    public void AnnouncePipe(string pipe) => Address.AnnouncePipe(pipe);

    /// <summary>
    /// A conversation answered the person (NOTIFY1a, <see cref="ChatRunner.Answered"/>): told as the tick's attention is, while
    /// the machine's notifications are on. Public so the tests hold what the page and the shell are told.
    /// </summary>
    /// <remarks>
    /// The switch is read from the machine's choices at each answer, since a conversation's turn ends outside any look. One that
    /// does not read errs toward saying it, as the shell's notifier does. NOTIFY2g: the answer is kept as a notice before the switch's
    /// gate, its workspace the conversation's partition in the sessions slice, so an answer heard with the switch off is listed.
    /// </remarks>
    public Task AnsweredAsync(AttentionEvent item)
    {
        Notices.Answered(item with
        {
            Workspace = MainWindowScopes.WorkspaceOf(Slices.Find(SessionsStateSlice.Name)?.Snapshot(), null, item.Session, null),
        });
        return NotifyRule.Tells(item, Notifying()) ? AttentionTold.TellAsync(eventBus, item) : Task.CompletedTask;
    }

    /// <summary>
    /// Whether this machine's notify switch is on (Settings → Driver, <c>daoris settings notify</c>), read from its choices now: for an
    /// announcement made outside any look, a conversation's answer (NOTIFY1a) or a held tool call (NOTIFY2d). Choices that do not read
    /// err toward saying it, as the shell's notifier does.
    /// </summary>
    public bool Notifying() => NotifyRule.SwitchOn(ConfigPath);

    /// <summary>
    /// The loop's service, handed to the routes, and the page told <c>DRIVER_READY</c> (LOOK2a): until now every route that reads the
    /// service refused <i>still coming up</i>, and from now it answers, its conversations with it (<see cref="LoopComeUp"/>). Called by
    /// the loop once its host answers; public so the tests hold what the page is told.
    /// </summary>
    /// <param name="chat">The loop's conversations, built over <paramref name="service"/>; null keeps none.</param>
    public async Task ComeUpAsync(ServiceClient service, ChatRunner? chat = null)
    {
        LoopComeUp.HearService(this, eventBus, service);
        if (chat is not null)
        {
            LoopComeUp.HearChat(this, eventBus, chat);
            Chat = chat;
        }

        Service = service;
        await eventBus.EmitAsync("DAORIS", "DRIVER_READY", new { Ready = true }).ConfigureAwait(false);

        if (chat is not null)
        {
            var stopping = _state.Stopping.Token;
            _ = Task.Run(() => chat.TakeUpAsync(stopping), stopping);
        }

        // AGENTSPLACE1b2: a place's apply that a stop left uncertain is read by its postcondition, in the background.
        var counting = _state.Stopping.Token;
        Settling = Task.Run(() => LoopComeUp.SettlePlacesAsync(this, service, counting));
    }

    /// <summary>The come-up's reading of the receipts of places' applies (AGENTSPLACE1b2); complete where it ended or was not begun, so a test holds it.</summary>
    public Task Settling { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// A conversation's end, as the page's one event for it (D49 §3): the session and the state its record took. One writer,
    /// since the shape is the page's contract: the routes that start a conversation hand it to their runs, and a chat the
    /// runner took up by itself is announced by it (MSG1c2).
    /// </summary>
    public static Task Ended(IEventBus bus, string session, string state) => ConversationEnds.FromRouteAsync(bus, session, state);

    /// <summary>The event's type, spelled here alone: a slice that wakes on it names it by this (PUSH1a).</summary>
    public const string EndedType = "SESSION_ENDED";

    /// <summary>
    /// An end the journal told this process (FOUNDATION1 l-i): any part's, driven or a conversation's, with its reason and class, so a
    /// slice that follows ends wakes on it rather than on the conversation's route alone.
    /// </summary>
    public const string PartEndedType = "PART_ENDED";

    /// <summary>
    /// Whether this process's journal reader was last answered by a host that keeps the journal: false before its first answer, against a
    /// host from before it, and while the host refuses or drops the ask, when a reader of <see cref="PartEndedType"/> keeps its old surface.
    /// </summary>
    public bool HearsJournal => _state.HearsJournal;

    /// <summary>
    /// A conversation's start refused at its open went on in its replacement (MODELROLE1d2, D205 §3.5(b)): <c>{ from, to }</c>, the refused
    /// record and the one that took its words, which a window showing the first follows.
    /// </summary>
    public const string ReplacedType = "CHAT_REPLACED";

    /// <summary>
    /// What the person's words to a session do, whatever its state (MSG1d, D137 §2): the one judge <c>SESSION_INPUT</c> and
    /// <c>SESSION_QUEUE</c> answer by, and where words said as a session winds up wait for its record to end.
    /// </summary>
    public SessionWords Words => LazyInitializer.EnsureInitialized(ref _words, () => new SessionWords(this));

    private SessionWords? _words;

    /// <summary>How often a loop waiting on another holder of the home's lease looks again (DRV8a).</summary>
    public TimeSpan HoldRetry { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long the loop waits on an act's hold (a verb, a migration, a swap) before it says so (HOMELEASE1).</summary>
    public TimeSpan TellAfter { get; init; } = HomeLease.Wait;

    /// <summary>This home's lease, waited for by the loop for its life (<see cref="HomeHold"/>); null when the shell closes first.</summary>
    public Task<HomeLease?> HoldHomeAsync(CancellationToken ct) => HomeHold.WaitAsync(Home, HoldRetry, TellAfter, eventBus, log, ct);

    /// <summary>
    /// Stop, bounded: an in-flight session is ended and recorded `stopped` by the driver itself, and so is
    /// a conversation — the loop's run ends by disposing its chat runner, inside its client's scope.
    /// </summary>
    public void Stop()
    {
        // The pipe first (RPC1a, the terminal RPC design §6.3): every new hello is answered `stopping` from here, and the presses
        // it runs answer within the drain, while the address still names this Daoris, so no terminal finds the home without one
        // and acts alone beside a press still under way.
        var pipe = Pipe;
        if (pipe is not null)
        {
            pipe.BeginStopping();
            pipe.Drain(PipeDrain);
        }

        // Then the address, so a terminal stops finding a service this Daoris is about to let go (FINDDAORIS1), and the lease
        // after it, which the loop lets go as it ends.
        Address.Withdraw();

        pipe?.Dispose();

        _words?.Dispose();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(15));
            _pushing?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Cancellation surfacing as it should; the record writes ride an unbound token inside.
        }

        // The plugins' processes go with the loop (D64 §4, rule 3): registrations are effects, and
        // the effect ends here. Bounded per plugin by the set itself; a plugin that will not leave is
        // ended rather than waited for.
        var hooks = _state.TakeHooks();
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
        Facts.Dispose();
        _state.Stopping.Dispose();
    }
}
