using System.Diagnostics;
using System.Text.Json;
using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// The loop D45 exists for: watch → plan → spawn → observe. One tick — a look — fetches a snapshot,
/// plans it (pure), starts every planned start, and returns once each has opened its record or come to
/// nothing. The sessions run on beside the looks that follow, and each one's ending joins the next
/// look's report (DEV3, D115 §3.1): a quest published while a long session runs starts at the next look,
/// not after that session. Under the watch (PARALLEL1b, D181 point 3) a look waits for a start only until
/// something else wants the loop, the start reserved and running on beside it, and leaves beside it the
/// work none of its parts waits on: the remote sync, the plugins' word on an ending, a packet frozen.
/// </summary>
/// <remarks>
/// <para><b>The driver never writes quest state.</b> It opens and advances SESSION records through the
/// service's ledger; the spawned session claims and closes its own quest through its own connector
/// (D46 §3). A failure here therefore leaves the quest exactly where it was.</para>
///
/// <para><b>Everything here is observation.</b> The session's end is concluded from the exit code and
/// the quest's state (<see cref="Observation"/>); the evidence is what git says landed. Nothing is
/// taken from the session's word, because outside sessions have no word to give.</para>
/// </remarks>
public sealed class Driver(
    ServiceClient service, DriverConfig config, AdapterSet adapters, string home,
    SessionProcesses? processes = null, RemoteSyncSet? sync = null, SessionOutput? output = null,
    HarnessRoster? harnesses = null,
    // What sessions consumed (TOOL3). Null where nobody is keeping the record — the family
    // rehearsal's headless driver, and every test that does not care.
    SessionUsage? usage = null,
    // The plugins that speak (D64), shared across ticks like the process registry: their processes
    // outlive a tick, and the tick asks them at its points. Null where no plugin is read at all,
    // which is every test that does not care and nothing that runs on a machine.
    HookSet? hooks = null,
    // Where a session's structure is kept (D76 §2), shared so the shell hears each event as it is
    // written. Null means this driver keeps the record itself, under the home, with nobody watching —
    // the headless host's case: the record still exists for a surface opened later.
    SessionEvents? events = null,
    // Daoris's own browser (D78), asked for by a plugin server that drives it. Null where no shell
    // carries one — the headless host, every gate — and such a server is then not handed.
    IInAppBrowser? browser = null,
    // The sessions still running (DEV3), shared by the watch across the driver it builds for each look,
    // since a session outlives the look that started it. Null keeps this driver's own, which is what a
    // one-look run and a gate's run-until-idle want.
    RunningSessions? running = null) : ILandingWiring, IPluginWiring, ISetupWiring, ILookStagesWiring, IRunWiring
{
    /// <summary>
    /// How one quest's start is run, from its holds to its record's conclusion: <see cref="RunAsync"/>, unless a
    /// test hands in an in-process stand-in (DEV3). Every real run spawns git and a harness, which the suite's fast
    /// half may not, and what a look does with its starts is the scheduling that half has to hold. The action is
    /// the run's to call once its session record is open.
    /// </summary>
    internal Func<Consideration, Action, CancellationToken, Task<StartRun>>? Runner { get; init; }

    /// <summary>
    /// Told each answer the course gave a driven path as it is decided (FOUNDATION1i, <see cref="DrivenCourse"/>): a test's, no person's.
    /// </summary>
    internal Action<LineCourse>? Decided { get; set; }

    /// <summary>The course's answer for a start's resume of the words kept on its record, which the account its record ran on may hold.</summary>
    private LineCourse DecideResume(PriorSession record, string questStatus, ResumeChoice resume)
    {
        var course = DrivenCourse.GoOn(record, questStatus, null, false, resume.Answer);
        Decided?.Invoke(course);
        return course;
    }

    /// <summary>
    /// The limit resume's step 1, decided by the course (path F): the record's own account the roster found ready resumes its own
    /// conversation, a move that still stands to the record as its destination holds it, read first (round 8, O7-F11).
    /// </summary>
    private async Task<LineCourse> DecideLimitOwnAsync(PriorSession limited, ResumeChoice own, CancellationToken ct)
    {
        LineAnswer? answer = own is { Own: NextHold.Ready, Selection.Allowed: true } ? new LineAnswer.Resume()
            : own.Answer is LineAnswer.Resume ? null : own.Answer;
        var standing = await DrivenCourse.StandingAsync(Moves.Resolve, limited.Session, ct).ConfigureAwait(false);
        var course = DrivenCourse.LimitOwn(limited, answer, standing);
        Decided?.Invoke(course);
        return course;
    }

    /// <summary>
    /// The move record the driven carry-on keeps (FOUNDATION1f-iv part a; the foundation design §3.3, §13.4): the one store the live limit move
    /// writes, handed the lease holder's scope (executor <c>driven</c>), or a shared loop's, only the lines it runs (SHAREMOVES1, <see cref="Share"/>).
    /// Made on first use, since the watch builds a driver for each look and the store is opened for each use; a test hands one of its own run.
    /// </summary>
    internal ConversationMoves Moves
    {
        get => _moves ??= new ConversationMoves(
            home, service, Context.Events, output,
            scope: Share ? MoveTakeUpScope.Shared(session => Context.Runs.Live.Values.Contains(session)) : MoveTakeUpScope.LeaseHolder);
        init => _moves = value;
    }

    private ConversationMoves? _moves;

    /// <summary>
    /// Whether this is a <c>--share</c> loop running beside the lease holder (SHAREMOVES1, the foundation design §3.3): its holder takes up only
    /// the moves of the lines this loop runs, the sessions its <see cref="RunningSessions"/> holds as the take-up reads them, and the lease
    /// holder's look takes up the rest. False, the default, is the lease holder's own: every dead run's driven move.
    /// </summary>
    public bool Share { get; init; }

    /// <summary>
    /// The watch's first look of a loop (FOUNDATION1f-iv part b; the foundation design §3.3, §19.1): every dead loop's driven move of this
    /// loop's scope taken up through <see cref="Moves"/> (the lease holder's, or a shared loop's own lines), and ended by its phase, so a line a
    /// dead loop left a move standing for is carried on by this loop and a word it sent and never took is taken, none sent again. The look
    /// reading each hold, not the shell's come-up, since a come-up's say of another shell on the home is the shell's own chat run's. Never faults.
    /// </summary>
    internal async Task TakeUpMovesAsync(CancellationToken ct) => await Moves.LookAsync(ct).ConfigureAwait(false);

    /// <summary>
    /// How a running session is stopped for a take that lost (D68 §5): its process ended through <see cref="SessionProcesses"/>,
    /// unless a test hands in its stand-in run's stop beside <see cref="Runner"/> (DEV3b). True when the session was running here.
    /// </summary>
    internal Func<string, Noted, bool>? Stops
    {
        get => Context.Stops;
        init => Context.Stops = value;
    }

    /// <summary>How a conversation an agent asked to hand over is handed to the shell's chat runner (MIXAGENT1c): <see cref="LoopContext.HandsOver"/>.</summary>
    public Func<string, string, CancellationToken, Task<HandedOver>>? HandsOver
    {
        get => Context.HandsOver;
        init => Context.HandsOver = value;
    }

    /// <summary>How a chat run's findings reach its conversation through the shell's chat runner (CHATFLOW1e): <see cref="LoopContext.SendsBack"/>.</summary>
    public IChatSendBacks? SendsBack
    {
        get => Context.SendsBack;
        init => Context.SendsBack = value;
    }

    /// <summary>How a closed quest's lost session goes on as a conversation through the shell's chat runner (SESSCTX1c part B): <see cref="LoopContext.GoesOnLost"/>.</summary>
    public Func<string, ContinueReason, CancellationToken, Task<StartedFrom>>? GoesOnLost
    {
        get => Context.GoesOnLost;
        init => Context.GoesOnLost = value;
    }

    /// <summary>The roster's doors a hand-over's order reads, a test's stand-in (MIXAGENT1c): <see cref="LoopContext.HandOverDoors"/>.</summary>
    internal Func<CancellationToken, Task<IReadOnlyList<HandOverDoor>>>? HandOverDoors
    {
        get => Context.HandOverDoors;
        init => Context.HandOverDoors = value;
    }

    /// <summary>
    /// The world a look's workspace plans read (WSSETUP6): <see cref="WorkspaceSetupWorld"/> over this driver's client, unless
    /// a test hands in a stand-in, since every real press reads a line with git and asks the tools their version.
    /// </summary>
    internal Func<IWorkspaceSetupWorld>? SetupPlans { get; init; }

    /// <summary>
    /// The machine log (LOG1a, D94), where a failure nothing else awaits is written with its place: a run's capture once the
    /// run was ended before awaiting it (ANSWER2). Null where nothing keeps one, as in a gate's driver; the console says it still.
    /// </summary>
    public MachineLog? Log
    {
        get => Context.Log;
        init => Context.Log = value;
    }

    /// <summary>What each quest's and ask's walk last passed, so <c>actor.passed</c> is written when a pass is new, not at every look (LOGCOLLECTION1).</summary>
    internal PassMemory Passed { get; } = new();

    /// <summary>Told after a look's news is kept as a notice (NOTIFY2h): <see cref="LoopContext.Noticed"/>.</summary>
    public Action? Noticed
    {
        get => Context.Noticed;
        init => Context.Noticed = value;
    }

    /// <summary>The look's lines, classed where each is made (NOTIFY2h): <see cref="LoopContext.Said"/>.</summary>
    internal LookSaid Lines => Context.Said;

    /// <summary>
    /// Where this driver's looks time their stages (LOOKWAIT1, D181 point 6): the watch's, shared across its looks and read into each
    /// <c>watch.looked</c>; else this driver's own, which still names a stage that hangs in <see cref="Log"/>.
    /// </summary>
    public LookStages Stages
    {
        get => LazyInitializer.EnsureInitialized(ref _stages, () => new LookStages(Log));
        init => _stages = value;
    }

    private LookStages? _stages;

    /// <summary>
    /// The review's showing in Daoris's browser (REVIEWENV1d, D154 point 5), shared across looks as the browser is: a local
    /// set-up step's tab opened before its session, its build served there once its set-up is posted, and let go at each look
    /// once the person answered it. Null where no shell carries a browser, the headless host and every gate, which show nothing.
    /// </summary>
    public ReviewDesk? Reviews { get; init; }

    /// <summary>
    /// The driver's file, read again at a running driven session's turn's end (ACCESSSWAP1b, D183's note): the look's config is the file
    /// as that look read it, and a session outlives its look, so a change the page's <c>SET_ACCESS</c> or the terminal's
    /// <c>daoris place access</c> wrote since is seen only here. The watch's; null where nothing names it, a one-look run's and a gate's,
    /// and a session there goes on under its start's setup until its next start.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>
    /// What a carry-on of a stood-down session whose lost take the person brought forward is told of the session before it (SYNCMERGE1b,
    /// D178 point 3), in place of that record's stop, which said its work would double someone else's: the quest is this machine's again,
    /// and, while the fresh take is only here, that it may still lose (D178 point 8).
    /// </summary>
    internal static string BroughtForwardTold(ForwardTake forward) =>
        "its take of the quest lost: another machine's take, or the person's reopen of the work it grew from, reached the remote first, and "
        + "this machine's driver stopped it with its work kept here. Since then the person brought that work forward, and "
        + (forward.Standing == "held"
            ? "the remote confirmed this machine's fresh take, so the quest is yours."
            : "this machine's fresh take is not yet confirmed by the remote, which may still refuse it: until then the quest is yours, and if "
              + "it is refused, this session is stopped as the one before it was.");

    /// <summary>
    /// Why a person's line is refused for a driven session (INT4i), and where its work goes instead —
    /// the sentence the bridge carries verbatim, so a caller is told the truth rather than "it ended".
    /// </summary>
    internal static string TakesNoMessages(QuestView quest) =>
        $"the session driven for quest #{quest.Id} takes no messages: it was handed its whole quest at once "
        + "and works it in one turn. What it does lands on the quest — its take, its close and its "
        + "commits — and stopping it is the one move that reaches it.";

    /// <summary>
    /// A park's question the disk would not let go at a run's opening or conclusion (PARKVIEW1; Fable's read, Q1), logged as
    /// <c>question.stayed</c>: its park could show a question its run did not ask. Every other outcome says nothing.
    /// </summary>
    internal void Questions(string sessionId, QuestionSettle settled)
    {
        if (settled == QuestionSettle.Stayed) Log?.Warn("question.stayed", ("session", sessionId));
    }

    /// <summary>Two lines a transcript opens with, either of which may be absent.</summary>
    internal static string? JoinNotices(string? first, string? second) =>
        (first, second) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{first}\n{second}",
            ({ Length: > 0 }, _) => first,
            _ => second,
        };

    // How often the sync runs BESIDE sessions still working (D68 §6): the watch loop's own cadence, so
    // a session no longer holds the sync back for its whole run, and one choice sets both.
    private readonly TimeSpan _syncBeside = TimeSpan.FromSeconds(Math.Max(1, config.PollSeconds));

    /// <summary>
    /// Which part of the look <see cref="TickAsync"/> runs (FABRIC-EVENT3a, the fabric events design §A3, §C6 link 5): the whole look
    /// unless the watch hands a wake's <see cref="LookPart.Plan"/>, which leaves the remote sync to the <c>look</c> topic's deadline.
    /// </summary>
    public LookPart Part { get; init; } = LookPart.Look;

    /// <summary>
    /// The look's passes, registered by their modules (<see cref="LookPassAttribute"/>), ordered once per process and built once per
    /// driver with this driver as their wiring; each phase's run at its anchor in <see cref="LookAsync"/>.
    /// </summary>
    internal PartSet<ILookPass> LookPasses =>
        LazyInitializer.EnsureInitialized(ref _lookPasses, () => Parts.Build(LookOrder.Value, type => (ILookPass)Activator.CreateInstance(type, this)!));

    private PartSet<ILookPass>? _lookPasses;

    private static readonly Lazy<IReadOnlyList<PartDeclaration>> LookOrder = new(() => Parts.Declared<LookPassAttribute>(typeof(Driver).Assembly));

    /// <summary>
    /// A driven session's run's stages, registered by their modules (<see cref="RunStageAttribute"/>), ordered once per process and built
    /// once per driver with this driver as their wiring; each phase's run at its anchor in <see cref="RunAsync"/>.
    /// </summary>
    internal PartSet<IRunStage> RunStages =>
        LazyInitializer.EnsureInitialized(ref _runStages, () => Parts.Build(RunOrder.Value, type => (IRunStage)Activator.CreateInstance(type, this)!));

    private PartSet<IRunStage>? _runStages;

    /// <summary>What starts a prepared session and holds it to its conclusion, for every kind of run this driver starts.</summary>
    internal SessionHold Holds => LazyInitializer.EnsureInitialized(ref _holds, () => new SessionHold(this));

    private SessionHold? _holds;

    private static readonly Lazy<IReadOnlyList<PartDeclaration>> RunOrder = new(() => Parts.Declared<RunStageAttribute>(typeof(Driver).Assembly));

    LoopContext IRunWiring.Context => Context;

    DriverContinuations IRunWiring.GoOns => GoOns;

    SessionWatch IRunWiring.Watch => Watch;

    string? IRunWiring.ConfigPath => ConfigPath;

    TimeProvider IRunWiring.RetryClock => RetryClock;

    ITransportHost IRunWiring.Transport => new DriverTransportHost(this);

    TimeSpan IRunWiring.BackgroundLook => BackgroundLook;

    Task<string?> IRunWiring.StepRoleAsync(QuestView quest, string? workspace, CancellationToken ct) => StepRoleAsync(quest, workspace, ct);

    RoleCatalog? IRunWiring.CatalogueForReport() => CatalogueForReport();

    IReadOnlyList<ActorEffective> IRunWiring.QuestOrder(Consideration start, IReadOnlyList<string>? fence, string? handed, string? role) =>
        QuestOrder(start, fence, handed, role);

    Task<SettledSelection> IRunWiring.WalkBuildPassesAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct, bool counts, (ActorEffective Entry, string? Account)? refused,
        long except, string? role) => WalkBuildPassesAsync(start, registry, ct, counts, refused, except, role);

    LineCourse IRunWiring.DecideResume(PriorSession record, string questStatus, ResumeChoice resume) => DecideResume(record, questStatus, resume);

    Task<LineCourse> IRunWiring.DecideLimitOwnAsync(PriorSession limited, ResumeChoice own, CancellationToken ct) => DecideLimitOwnAsync(limited, own, ct);

    Task<WorkflowBind> IRunWiring.BindAtStartAsync(QuestView quest, string? workspace, CancellationToken ct) => BindAtStartAsync(quest, workspace, ct);

    Action<string>? IRunWiring.RunReturned => RunReturned;

    ReviewDesk? IRunWiring.Reviews => Reviews;

    void IRunWiring.CountMove(string line, string role, string condition, ActorEffective from, string? to) => CountMove(line, role, condition, from, to);

    System.Collections.Concurrent.ConcurrentDictionary<string, string> IRunWiring.LimitResumed => LimitResumed;

    bool IRunWiring.HandedConnector(ISessionAdapter adapter, string? workspace, string? repository, string tool) =>
        HandedConnector(adapter, workspace, repository, tool);

    AcpMcpServer? IRunWiring.Connector(string sessionId, IReadOnlyDictionary<string, string>? scope, string? proc) => Connector(sessionId, scope, proc);

    string ILookWiring.Home => home;

    DriverConfig ILookWiring.Config => config;

    ServiceClient ILookWiring.Service => service;

    RemoteSyncSet? ILookWiring.Sync => sync;

    RunningSessions ILookWiring.Runs => Context.Runs;

    SessionProcesses ILookWiring.Processes => Context.Processes;

    LookSaid ILookWiring.Lines => Lines;

    bool ILookWiring.Beside => Context.Beside;

    Func<string, Noted, bool>? ILookWiring.Stops => Stops;

    HarnessRoster ILookWiring.Harnesses => Context.Harnesses;

    QuestionSupervisorDecisions ILookWiring.Supervisor => Context.Supervisor;

    GoOnMarks ILookWiring.Marks => Context.Marks;

    LoopContext ILookStagesWiring.Context => Context;

    ReviewDesk? ILookStagesWiring.Reviews => Reviews;

    DriverHandOvers ILookStagesWiring.HandOvers => HandOvers;

    OpinionGateLook ILookStagesWiring.Gate => Gate;

    DriverHelpers ILookStagesWiring.Helpers => Helpers;

    DriverPartners ILookStagesWiring.Partners => Partners;

    DriverBrainstorms ILookStagesWiring.Brainstorms => Brainstorms;

    DriverMeetings ILookStagesWiring.Meetings => Meetings;

    DriverIntakes ILookStagesWiring.Intakes => Intakes;

    LookSync ILookStagesWiring.SyncPass => LookPasses.Of<LookSync>();

    SessionOutput? ILandingWiring.Output => output;

    HookSet? ILandingWiring.Hooks => Context.Hooks;

    SessionEvents ILookWiring.Events => Context.Events;

    IReadOnlyList<string> ILookWiring.LookIn(LookSaid said) => LookIn(said);

    AdapterSet IPluginWiring.Built => _built;

    HookSet? IPluginWiring.Hooks => Context.Hooks;

    void IPluginWiring.Adopt(PluginCatalog catalog)
    {
        Context.Adapters = _built.WithPlugins(catalog);
        Context.Servers = catalog.Servers;
        Context.Catalog = catalog;
        Context.Harnesses.Use(Context.Adapters);
        // One set of Daoris's accounts (POOL1a, D197): the wiring folded at the come-up, and again at a next look a held lock refused.
        Context.Harnesses.Pool(Log);
    }

    Func<IWorkspaceSetupWorld>? ISetupWiring.SetupPlans => SetupPlans;

    SessionWire ISetupWiring.Door() => Door();

    /// <summary>
    /// Told a run a start found bound already, to a named workflow (FABRIC-CUTOVER1c; the cutover design §3): where it is a graph run whose record
    /// holds its end, its chain has come back here, and the loop's runtime is asked for a pass that judges it. Null tells nobody.
    /// </summary>
    internal Action<string>? RunReturned { get; init; }

    /// <summary>
    /// What completes once something else wants the loop (PARALLEL1b, D181 point 3): a nudge, an ending, the <c>look</c> topic's
    /// deadline. Handed by the watch, whose looks then leave beside them the work no part of the look waits on (the remote sync and the
    /// lost-claim stop after it, the sync after an ending), and wait for a start that has not yet opened its record only until this
    /// completes. Null, a one-look run's and a gate's run-until-idle, runs each in the look as before, since each reads one look whole.
    /// </summary>
    internal Func<CancellationToken, Task>? Wanted
    {
        get => _wanted;
        init
        {
            _wanted = value;
            Context.Beside = value is not null;
        }
    }

    private readonly Func<CancellationToken, Task>? _wanted;

    /// <summary>
    /// What the loop's classes share (PART10m, D166): the constructor's objects, the sessions and trees, the per-look adapters,
    /// servers and catalogue, and the settables. Built once, here; the classes below hold it, and the ones that run through this
    /// driver's machinery hold the driver as well.
    /// </summary>
    /// <summary>
    /// The clock a driven session's transient retry waits on (AGENTERRORS1a, D210 §5.1): the machine's, and a held one in a test
    /// (<c>.claude/knowledge/held-clocks.md</c>), so its 1, 2, 4, 8 and 16 minutes are the test's to spend.
    /// </summary>
    public TimeProvider RetryClock { get; init; } = TimeProvider.System;

    internal LoopContext Context { get; } = new(service, config, home, adapters, processes, harnesses, events, output, usage, running, hooks, browser);

    /// <summary>The sessions still running, which outlive the look that started them (DEV3).</summary>
    public RunningSessions Running => Context.Runs;

    // The build's own adapters, which each tick's plugins are laid over — held as a field so the
    // primary constructor's parameter is captured in one place only (CS9124).
    private readonly AdapterSet _built = adapters;

    // The second opinion's classes (PART10m): the pass runs through this driver's machinery, the delivery passes, the gate delivers.
    private DriverOpinions? _opinions;
    private OpinionDeliverer? _deliverer;
    private OpinionGateLook? _gate;

    private DriverOpinions Opinions => LazyInitializer.EnsureInitialized(ref _opinions, () => new DriverOpinions(Context, this));

    /// <summary>The second opinion's delivery, which the go-on also asks to read the answers and to send findings to the person.</summary>
    internal OpinionDeliverer Deliverer => LazyInitializer.EnsureInitialized(ref _deliverer, () => new OpinionDeliverer(Context, Opinions));

    private OpinionGateLook Gate => LazyInitializer.EnsureInitialized(ref _gate, () => new OpinionGateLook(Context, Opinions, Deliverer));

    // The go-on, hand-over, helper and intake classes (PART10n): each runs through this driver's machinery or the context alone.
    private DriverContinuations? _continuations;
    private DriverHandOvers? _handOvers;
    private DriverHelpers? _helpers;
    private DriverIntakes? _intakes;

    private DriverContinuations GoOns => LazyInitializer.EnsureInitialized(ref _continuations, () => new DriverContinuations(Context, this));

    private DriverHandOvers HandOvers => LazyInitializer.EnsureInitialized(ref _handOvers, () => new DriverHandOvers(Context, () => Moves) { Decided = course => Decided?.Invoke(course) });

    private DriverHelpers Helpers => LazyInitializer.EnsureInitialized(ref _helpers, () => new DriverHelpers(Context, this));

    private DriverPartners? _partners;

    private DriverPartners Partners => LazyInitializer.EnsureInitialized(ref _partners, () => new DriverPartners(Context, this));

    /// <summary>The pieces on file at a look (COAGENT1b): <see cref="DriverPartners.PiecesAsync"/>.</summary>
    internal Task<int> PiecesAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Partners.PiecesAsync(snapshot, taken, events, ct);

    private DriverIntakes Intakes => LazyInitializer.EnsureInitialized(ref _intakes, () => new DriverIntakes(Context, this));

    /// <summary>
    /// How a helper's pass is run: <see cref="DriverHelpers.HelperPassAsync"/>, unless a test hands in a stand-in, since a real pass
    /// spawns git and an agent, which the suite's fast half may not (MOD8). Written through to the context.
    /// </summary>
    internal Func<HelperPassAsk, ReviewerChoice, CancellationToken, Task<HelperPassRun>>? HelperPasses
    {
        get => Context.HelperPasses;
        init => Context.HelperPasses = value;
    }

    /// <summary>
    /// How an asker's tree is read, its last commit and its repository's registered checkout: git, unless a test hands in a
    /// stand-in, since a real read spawns git, which the suite's fast half may not (MOD8). Written through to the context.
    /// </summary>
    internal Func<string, CancellationToken, Task<(string? Tip, string? Root)>>? HelperTrees
    {
        get => Context.HelperTrees;
        init => Context.HelperTrees = value;
    }

    /// <summary>
    /// How an asker's work in progress is captured for its helper (COAGENT1a): <see cref="HelperCapture.TakeAsync"/>, which spawns
    /// git, unless a test hands in a stand-in (MOD8). Written through to the context.
    /// </summary>
    internal Func<string, CancellationToken, Task<HelperCaptureRead>>? HelperCaptures
    {
        get => Context.HelperCaptures;
        init => Context.HelperCaptures = value;
    }

    /// <summary>
    /// How a helper's agent is chosen: <see cref="HelperChoice.ChooseAsync"/>, whose account walk reads this machine's installs and
    /// sign-ins, unless a test hands in a choice (the walk itself is <see cref="ReviewerChoice"/>'s, held by its own suite). Written
    /// through to the context.
    /// </summary>
    internal Func<HelperAsk, HelperRule, PriorSession, CancellationToken, Task<ReviewerChoice>>? HelperChoices
    {
        get => Context.HelperChoices;
        init => Context.HelperChoices = value;
    }

    /// <summary>The measurements a test's stand-in agents are admitted by (COAGENT1b), never a production start's. Written through to the context.</summary>
    internal IReadOnlyList<PartnerMeasurement>? PartnerMeasured
    {
        get => Context.PartnerMeasured;
        init => Context.PartnerMeasured = value;
    }

    /// <summary>How a piece's agent is chosen and admitted (COAGENT1b): <see cref="PartnerChoice.ChooseAsync"/>, unless a test hands in a choice. Written through to the context.</summary>
    internal Func<PartnerAsk, PriorSession, CancellationToken, Task<ReviewerChoice>>? PartnerChoices
    {
        get => Context.PartnerChoices;
        init => Context.PartnerChoices = value;
    }

    /// <summary>How a partner's pass is run (COAGENT1b): <see cref="DriverPartners.PartnerPassAsync"/>, unless a test hands in a stand-in.</summary>
    internal Func<PartnerPassAsk, ReviewerChoice, CancellationToken, Task<PartnerPassRun>>? PartnerPasses
    {
        get => Context.PartnerPasses;
        init => Context.PartnerPasses = value;
    }

    /// <summary>How a piece is kept and imported (COAGENT1b): <see cref="PieceKeeper.KeepAsync"/>, unless a test hands in a stand-in.</summary>
    internal Func<PartnerAsk, PartnerState, CancellationToken, Task<PieceKept>>? PartnerKeeps
    {
        get => Context.PartnerKeeps;
        init => Context.PartnerKeeps = value;
    }

    /// <summary>How a lead's repository's checkout is read (COAGENT1b), unless a test hands in a stand-in.</summary>
    internal Func<string, CancellationToken, Task<string?>>? PartnerTrees
    {
        get => Context.PartnerTrees;
        init => Context.PartnerTrees = value;
    }

    /// <summary>How a brainstorm's turn is run (BRAINSTORM1a): <see cref="DriverBrainstorms.TurnAsync"/>, unless a test hands in a stand-in.</summary>
    internal Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? BrainstormTurns
    {
        get => Context.BrainstormTurns;
        init => Context.BrainstormTurns = value;
    }

    /// <summary>How a brainstorm's packet searches the knowledge service, unless a test hands in a stand-in.</summary>
    internal Func<string, string, CancellationToken, Task<BrainstormSearch>>? BrainstormSearches
    {
        get => Context.BrainstormSearches;
        init => Context.BrainstormSearches = value;
    }

    /// <summary>How a brainstorm's repository is read, its checkout and its commit: git, unless a test hands in a stand-in (MOD8).</summary>
    internal Func<string, CancellationToken, Task<(string? Root, string? Revision)>>? BrainstormCheckouts
    {
        get => Context.BrainstormCheckouts;
        init => Context.BrainstormCheckouts = value;
    }

    /// <summary>
    /// The environment this driver reads its variables from (ENVSEAM1, D63's seam): the connector's host, home and store, the
    /// remotes, and the home a start's tools are read from. This process's, unless a test hands in its own, so a test that names
    /// its own scratch sets nothing every class beside it shares.
    /// </summary>
    internal Func<string, string?> Variable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>How long a brainstorm's minute lasts (BRAINSTORM1b): a minute, unless a test shortens it.</summary>
    internal TimeSpan? BrainstormMinute
    {
        get => Context.BrainstormMinute;
        init => Context.BrainstormMinute = value;
    }

    /// <summary>The clock for brainstorm deadlines and cancellation timers; the system's unless a test holds it.</summary>
    internal TimeProvider BrainstormClock
    {
        get => Context.BrainstormClock;
        init => Context.BrainstormClock = value;
    }

    private DriverBrainstorms? _brainstorms;

    private DriverBrainstorms Brainstorms => LazyInitializer.EnsureInitialized(ref _brainstorms, () => new DriverBrainstorms(Context, this));

    /// <summary>The brainstorms on file at a look (BRAINSTORM1a, D176): <see cref="DriverBrainstorms.BrainstormsAsync"/>.</summary>
    internal Task<int> BrainstormsAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Brainstorms.BrainstormsAsync(snapshot, taken, events, ct);

    /// <summary>
    /// A room measurement of an agent's pass (BRAINSTORM1c, <see cref="DriverBrainstorms.MeasureRoomAsync"/>): the walk's account alone where
    /// <paramref name="spend"/> is false, else one probe pass on it, kept as evidence. The terminal's <c>brainstorm measure</c> is its press.
    /// </summary>
    public Task<RoomMeasure> MeasureRoomAsync(string agent, bool spend, CancellationToken ct) => Brainstorms.MeasureRoomAsync(agent, spend, ct);

    /// <summary>How a meeting's turn is run (AGENTROOM1a): refused with no stand-in, since no tool carries its message yet.</summary>
    internal Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? MeetingTurns
    {
        get => Context.MeetingTurns;
        init => Context.MeetingTurns = value;
    }

    private DriverMeetings? _meetings;

    private DriverMeetings Meetings => LazyInitializer.EnsureInitialized(ref _meetings, () => new DriverMeetings(Context, Brainstorms));

    /// <summary>The meetings on file at a look (AGENTROOM1a, D177): <see cref="DriverMeetings.MeetingsAsync"/>.</summary>
    internal Task<int> MeetingsAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Meetings.MeetingsAsync(snapshot, taken, events, ct);

    /// <summary>The asks for a helper at a look (MIXAGENT2a-ii): <see cref="DriverHelpers.HelpersAsync"/>.</summary>
    internal Task<int> HelpersAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Helpers.HelpersAsync(snapshot, taken, events, ct);

    /// <summary>
    /// Whether a driven run that ends now holding its quest waits on its own helper (HELPERWAIT1; <see cref="DriverHelpers.WaitsOnOwnHelperAsync"/>):
    /// a question its run asked counts from the later of <paramref name="openedAt"/> and the words it was last handed, as its park binds one.
    /// </summary>
    internal Task<bool> WaitsOnOwnHelperAsync(string session, QuestView quest, DateTimeOffset openedAt, CancellationToken ct) =>
        Helpers.WaitsOnOwnHelperAsync(
            session, quest, Context.Events.LastHandedAt(session) is { } handed && handed > openedAt ? handed : openedAt, ct);

    /// <summary>One intake run in its room (D65 §1b): <see cref="DriverIntakes.RunIntakeAsync"/>.</summary>
    private Task<StartRun> RunIntakeAsync(AskView ask, List<TrustHold> untrusted, Action onOpened, CancellationToken ct) =>
        Intakes.RunIntakeAsync(ask, untrusted, onOpened, ct);

    /// <summary>
    /// How a pass is run: <see cref="PassAsync"/>, unless a test hands in a stand-in, since a real pass spawns git and an agent,
    /// which the suite's fast half may not (MOD8). The recheck is run through it (XAGENT1e). Written through to the context.
    /// </summary>
    internal Func<OpinionPassAsk, ReviewerChoice, CancellationToken, Task<OpinionPassRun>>? Passes
    {
        get => Context.Passes;
        init => Context.Passes = value;
    }

    /// <summary>
    /// How a fix's commit is read from git when the working session's turn ends (XAGENT1e, design §6.4): the review's seam,
    /// <see cref="WorkingTree.ReadGitAsync"/> unless a test hands in its stand-in. Written through to the context.
    /// </summary>
    internal WorkingTree.GitRead OpinionGit
    {
        get => Context.OpinionGit;
        init => Context.OpinionGit = value;
    }

    /// <summary>
    /// Read <paramref name="ask"/>'s work with the reviewer <paramref name="choice"/> names (XAGENT1d): <see cref="DriverOpinions.PassAsync"/>.
    /// </summary>
    public Task<OpinionPassRun> PassAsync(OpinionPassAsk ask, ReviewerChoice choice, CancellationToken ct = default) =>
        Opinions.PassAsync(ask, choice, ct);

    /// <summary>Deliver <paramref name="opinion"/> as far as the facts let it go now (XAGENT1e): <see cref="OpinionDeliverer.DeliverAsync"/>.</summary>
    public Task<OpinionDelivery> DeliverAsync(string opinion, OpinionPassAsk asked, CancellationToken ct = default) =>
        Deliverer.DeliverAsync(opinion, asked, ct);

    /// <summary>The second opinions owed at a look (XAGENT1f): <see cref="OpinionGateLook.OpinionsAsync"/>.</summary>
    internal Task<(IReadOnlyDictionary<string, OpinionSit> Sits, int Passes)> OpinionsAsync(
        Snapshot snapshot, List<string> events, CancellationToken ct, List<OpinionJudged>? judged = null) =>
        Gate.OpinionsAsync(snapshot, events, ct, judged);

    /// <summary>
    /// A start's bind of its run before its take (FABRIC-CUTOVER1g, D190's binding moment; the cutover design §8): Workflows' use case
    /// (<see cref="WorkflowRunStart.AtStartAsync(WorkflowStarted, IWorkflowStartWorld, string, DriverConfig, IReadOnlyList{WorkflowPlugin}, Func{DateTimeOffset}, CancellationToken)"/>)
    /// over the loop's service, home, choices and plugins. A binding it made is kept for the next look's bind to say.
    /// </summary>
    private async Task<WorkflowBind> BindAtStartAsync(QuestView quest, string? workspace, CancellationToken ct)
    {
        var bound = await WorkflowRunStart.AtStartAsync(
                new WorkflowStarted(quest, workspace), new StartWorld(service), home, config, WorkflowSteps.PluginsOf(Context.Catalog),
                () => DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        if (bound.Came == WorkflowBindCame.Bound) Context.Runs.Bound(bound);
        return bound;
    }

    /// <summary>
    /// The role a quest's run names for its work step (ROLESTEP1b), which its start walks: its kept binding's, or where the run is bound
    /// nowhere yet, the binding its bind would make now (<see cref="WorkflowRunStart.PlannedAsync"/>), since the walk comes before the bind;
    /// null where it names none. The bind's own binding is held against it once made (<see cref="RunAsync"/>).
    /// </summary>
    internal async Task<string?> StepRoleAsync(QuestView quest, string? workspace, CancellationToken ct) =>
        (await WorkflowRunStart.PlannedAsync(
                new WorkflowStarted(quest, workspace), new StartWorld(service), home, config, WorkflowSteps.PluginsOf(Context.Catalog),
                () => DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false))?.RoleOf(NodeKinds.Claimed)?.Role;

    /// <summary>
    /// The door this machine's starts ride — the protocol door where any entry of Build's order rides it, else the first entry's wire
    /// (MODELROLE1c, D205 §4.1's <c>no-connector</c>: the planner asks whether any entry can drive a repository that has not adopted
    /// Daoris, and the start passes each entry that cannot), read against this tick's adapters so a plugin's harness counts. An adapter
    /// nobody knows is the pipe: the stricter door plans nothing more than it should, and the spawn names the unknown adapter itself. An
    /// order that does not read is the pipe too, as the start that reads it holds.
    /// </summary>
    private SessionWire Door()
    {
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = RoleCatalog.ReadHome(home).Order(ParticipantWork.Build, new RolePlace(), config);
        }
        catch (DriverException)
        {
            order = [];
        }

        SessionWire? first = null;
        foreach (var entry in order)
        {
            try
            {
                var wire = Context.Adapters.Resolve(entry.Agent).Wire;
                if (wire == SessionWire.Acp) return wire;
                first ??= wire;
            }
            catch (DriverException)
            {
                first ??= SessionWire.Pipe;
            }
        }

        return first ?? SessionWire.Pipe;
    }

    /// <summary>
    /// A new start's walk over Build's effective order (MODELROLE1c, D205 §4.1), through the quest's start plan (FOUNDATION1j,
    /// <see cref="StartPlan"/>): choice by choice, each choice's accounts by the existing walk before the next. An entry that cannot run
    /// here at all is passed by its code before any account is asked: <c>unavailable</c> (no such agent), <c>no-posture</c> (its harness
    /// has no mode for the repository's permission prompts, judged by the start's own <see cref="DrivenAccess"/>) and <c>no-connector</c>
    /// (the pipe door, for a repository that has not adopted Daoris). The first entry whose walk allows a start is taken; with none, the
    /// start holds on the wait's sentence, the soonest cool-off beside it. An order of one entry is today's walk exactly, its own hold or
    /// refusal unchanged.
    /// </summary>
    internal async Task<(HarnessSelection Selection, ActorEffective? Chosen)> WalkBuildAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct) =>
        (await WalkBuildPassesAsync(start, registry, ct).ConfigureAwait(false)) is var walked ? (walked.Selection, walked.Chosen) : default;

    /// <summary>
    /// <see cref="WalkBuildAsync"/>, with each choice it passed and why (D205 §4.6's opening note), and for the start's re-ask at the open
    /// (§3.3, MODELROLE1c2): <paramref name="refused"/>, the entry and account its agent refused, passed as if known before the spawn, and
    /// <paramref name="counts"/> false, so the re-ask takes no count of its own and ranks without the start's own (<paramref name="except"/>).
    /// Each entry's accounts are judged by their offer readings for the entry's model (§4.1's model parameter).
    /// </summary>
    /// <param name="role">The role the run's work step names (<see cref="StepRoleAsync"/>), walked in place of the work's; null for the work's.</param>
    internal Task<SettledSelection> WalkBuildPassesAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct, bool counts = true,
        (ActorEffective Entry, string? Account)? refused = null, long except = 0, string? role = null) =>
        BuildPlan(start, registry, role).ResolveAsync(ct, counts, refused, except);

    /// <summary>
    /// The start's re-ask where its agent refused the entry's model at the open (D205 §3.3, MODELROLE1c2): the walk asked again as if the
    /// refusal had been known before the spawn, taking no count of its own. Its answer is a later entry to set on the same session where it
    /// names the same agent on the same account; anything else is null, and the opened record ends <c>choice-not-offered</c>, the next look
    /// starting what the walk then answers. A walk that throws answers null, as it always has.
    /// </summary>
    internal async Task<(ActorAsked Asked, string Choice)?> ReaskAsync(
        Consideration start, IReadOnlyList<RepoView> registry, ActorEffective refused, HarnessSelection selection, CancellationToken ct,
        string? role = null)
    {
        try
        {
            return await BuildPlan(start, registry, role).ReaskAsync(refused, selection, ct).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>The catalogue for the look's report of a start, which never faults the look: a store that will not read says the seed's, and the start itself holds.</summary>
    private RoleCatalog? CatalogueForReport()
    {
        try
        {
            return RoleCatalog.ReadHome(home);
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The role a quest's start took and its record's revision (FOUNDATION1x), for its binding: the one its run's work step names where it
    /// names one (ROLESTEP1b), else the work's; null where none takes the work or the store will not read.
    /// </summary>
    internal (string Role, long Revision)? RoleTaken(Consideration start, string? named)
    {
        try
        {
            var catalogue = RoleCatalog.ReadHome(home);
            return Found(catalogue, StartWork(start.Quest), start, named).Role is { } role ? (role.Id, catalogue.RevisionOf(role.Id)) : null;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>The name of the role a design quest's start took, as its opening says it; null where the catalogue will not read.</summary>
    private string? DesignRoleName(Consideration start, string? named)
    {
        try
        {
            return Found(RoleCatalog.ReadHome(home), ParticipantWork.Design, start, named).Role is { } role
                ? ActorChoices.Ids.Contains(role.Id, StringComparer.Ordinal) ? ActorWords.Name(role.Id) : role.Name.In("en")
                : null;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>The role a start of <paramref name="work"/> takes: the one its step names (ROLESTEP1b), else the work's at its place.</summary>
    private static RoleFound Found(RoleCatalog catalogue, string work, Consideration start, string? named) =>
        named is { Length: > 0 } ? catalogue.Named(named, work, PlaceOf(start)) : catalogue.RoleFor(work, PlaceOf(start));

    /// <summary>The work a quest's start is: design where its publisher or the intake marked its weight so (MODELROLE1l), else build.</summary>
    internal static string StartWork(QuestView quest) =>
        string.Equals(quest.Weight, ParticipantWork.Design, StringComparison.Ordinal) ? ParticipantWork.Design : ParticipantWork.Build;

    /// <summary>Where a quest's start is, by which a scoped role is found: its circle and the repository it is asked of.</summary>
    internal static RolePlace PlaceOf(Consideration start) => new(start.Workspace, start.Quest.To);

    /// <summary>
    /// The order the role taking a quest's work walks (FOUNDATION1x), with the fence, the pick or the hand-over's agent a caller names: the
    /// role its run's work step names where it names one (ROLESTEP1b), held by its own sentence where that role cannot take it.
    /// </summary>
    private IReadOnlyList<ActorEffective> QuestOrder(Consideration start, IReadOnlyList<string>? fence = null, string? handed = null, string? role = null) =>
        role is { Length: > 0 }
            ? RoleCatalog.ReadHome(home).Order(role, StartWork(start.Quest), PlaceOf(start), config, fence, handed: handed)
            : RoleCatalog.ReadHome(home).Order(StartWork(start.Quest), PlaceOf(start), config, fence, handed: handed);

    /// <summary>
    /// A quest's start plan (FOUNDATION1j): Build's order for its work, its passes remembered and said under the quest, its gate the quest's
    /// own: an agent whose harness has no mode for the repository's permission prompts, and a pipe door where the repository has not adopted
    /// Daoris, each passed before any account is asked. The role its run's work step names is walked in place of the work's (ROLESTEP1b).
    /// </summary>
    private StartPlan BuildPlan(Consideration start, IReadOnlyList<RepoView> registry, string? role = null)
    {
        var quest = start.Quest;
        var adopted = registry.FirstOrDefault(repo => string.Equals(repo.Repository, quest.To, StringComparison.OrdinalIgnoreCase))?.Adopted ?? true;
        var access = AccessRules.Resolve(config, quest.To, start.Workspace);
        (string Code, string Why)? Gate(ActorEffective entry, ISessionAdapter adapter)
        {
            if (DrivenAccess.For(access, quest.To, adapter).Refusal is { } unposed) return (StartPasses.NoPosture, unposed);
            return !adopted && adapter.Wire != SessionWire.Acp
                ? (StartPasses.NoConnector, $"`{entry.Agent}` cannot take a quest here: `{quest.To}` has not adopted Daoris")
                : null;
        }

        return new StartPlan(
            StartWork(quest), config, Context.Harnesses, Context.Adapters, Passed, catalogue: RoleCatalog.ReadHome(home), place: PlaceOf(start), role: role)
        {
            Workspace = start.Workspace,
            Kind = StartKind.Driven,
            Subject = $"quest:{quest.Id}",
            Named = ("quest", quest.Id),
            Log = Log,
            Gate = Gate,
            Staff = Context.Harnesses.StaffFor(home),
            // The choices this quest's line moved off in the last 30 minutes are passed.
            LineFacts = StaffCourse.LineFacts(Context.Harnesses.StaffFor(home), quest, start.Workspace, role),
        };
    }

    /// <summary>
    /// A part the role's policy moved to another choice (STAFFPOLICY1e2): the record keeps its <c>chosen</c> (moved, with the condition and the
    /// choice left), the machine log writes <c>actor.moved</c>, and the opening note says it. A service that will not answer costs the record this, never the run.
    /// </summary>
    private async Task SayPolicyMoveAsync(
        ServiceClient service, string sessionId, string role, string line, StaffMoving moving, PriorSession left, string? to)
    {
        var staff = Context.Harnesses.StaffFor(home);
        var read = staff.Moves.Of(line);
        var (policy, said) = StaffCourse.MoveSaid(staff, role, line, left, moving.Condition, read.Moves);
        ActorMoves.Write(Log, role, line, left.Choice, to, moving.Condition, StaffCourse.ActionOf(policy, moving.Condition), read.Moves);
        await service.TryKeepActorAsync(
                sessionId, Log, chosen: new ActorChosen(policy?.Rule ?? StaffRules.Order, StaffBy.Moved, moving.Condition, left.Choice))
            .ConfigureAwait(false);
        RotatedOpening.SayMovedChoice(Context.Events, sessionId, StaffCourse.ChoiceOf(left)?.Said ?? left.Adapter ?? "", said);
    }

    /// <summary>
    /// A move to another choice made inside one start (a model its agent refused, a sign-in that walked to another agent), counted against the
    /// line's bound of moves in 24 hours (STAFFPOLICY1e2, design §11 point 5). It names no part, so no later carry-on is taken for a move's own.
    /// </summary>
    private void CountMove(string line, string role, string condition, ActorEffective from, string? to)
    {
        try
        {
            Context.Harnesses.StaffFor(home).Moves.Record(line, role, null, condition, StaffActions.Next, StaffKey.Of(from).Text, to);
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            Log?.Write("warn", "actor.moves-unkept", [("line", line), ("why", error.Message)]);
        }
    }

    /// <summary>
    /// The re-ask of a handed quest's start whose agent refused the model it asked at the open (PRESSMODEL1, D205 §3.3): Build's later
    /// entries for the handed <paramref name="agent"/> in order, then its own default with no model asked, the first that the same account
    /// offers, set on the same session. Models already refused here (<paramref name="refused"/>) are never asked again, so the re-ask ends.
    /// Null where none can run on the account, and the opened record ends <c>choice-not-offered</c>.
    /// </summary>
    internal async Task<(ActorAsked Asked, string Choice)?> ReaskHandedAsync(
        Consideration start, string agent, HarnessSelection selection, ModelRefusal refusal, HashSet<string> refused, CancellationToken ct,
        string? role = null)
    {
        refused.Add(refusal.Value);
        (ActorAsked Asked, string Choice)? answer = null;
        try
        {
            var candidates = QuestOrder(start, fence: [agent], role: role).ToList();
            if (!candidates.Any(entry => entry.Model is null)) candidates.Add(new ActorEffective(agent, null, null, "rule-default"));
            // The plan's one loop over today's candidates (FOUNDATION1j part v): a handed quest names its agent and its account, so the ask
            // stays the handed one on the account the refusal named, and a model already refused is held, neither asked nor passed.
            var walked = await StartPlan.LoopAsync(
                candidates, entry => entry,
                async entry =>
                {
                    var (picked, _) = await Context.Harnesses
                        .SelectHandedAsync(agent, config, start.Workspace, selection.Profile, StartKind.Driven, ct, entry.Model, null, selection.Number, counts: false)
                        .ConfigureAwait(false);
                    return picked.Allowed && string.Equals(picked.Profile, selection.Profile, StringComparison.OrdinalIgnoreCase)
                        ? EntryTry.Runs(picked)
                        : EntryTry.Passed(StartPasses.Held, picked.Refusal, picked);
                },
                holds: (entry, _) => entry.Model is { } model && refused.Contains(model)).ConfigureAwait(false);
            if (walked.Taken is { } taken) answer = (taken.Asked, taken.From);
        }
        catch (DriverException)
        {
            return null;
        }

        Log?.Write("info", "actor.reasked", [("actor", "build"), ("quest", start.Quest.Id), ("refused", refusal.Value), ("answer", answer?.Choice), ("same", answer is not null)]);
        return answer;
    }

    /// <summary>
    /// One look: the sync, the plan, and every planned start begun. Returns once each start has opened its
    /// session record or come to nothing, never waiting for a session to end (DEV3, D115 §3.1): the sessions
    /// run on, and what ended since the last look joins this one's report.
    /// </summary>
    /// <remarks>
    /// A <see cref="LookPart.Plan"/> (FABRIC-EVENT3a) is the same look without the remote sync at its head and the lost-claim stop
    /// after it; everything else it runs as the whole look does, the sync after an ending it reports included (the sync design §8).
    /// Where the watch hands <see cref="Wanted"/> (PARALLEL1b), the syncs run beside the look and a start still preparing when
    /// something else wants the loop runs on beside it, reserved, what it came to said by a later look.
    /// </remarks>
    /// <param name="failed">
    /// Handed what the look had said when it fails or is closed (DEV3c): its lines and what ended, which its report would have
    /// carried, when there is any; the failure then goes on as it was. A look that stopped a session and then failed lost the
    /// stop's line, the record written and the line not. Never called for a look that returns.
    /// </param>
    public async Task<TickReport> TickAsync(CancellationToken ct = default, Action<TickReport>? failed = null)
    {
        var events = new List<string>();

        // What ended, structurally — the half of the report a watcher can act on (SURF5b): a parked intake
        // this look ends, and every session that ended since the last look (DEV3).
        var concluded = new List<SessionEnded>();

        try
        {
            return await LookAsync(events, concluded, ct).ConfigureAwait(false);
        }
        catch when (failed is not null)
        {
            List<string> lines;
            lock (events) lines = [.. events];
            if (lines.Count > 0 || concluded.Count > 0) failed(new TickReport([], lines, Progressed: false, Concluded: [.. concluded]));
            throw;
        }
    }

    /// <summary><see cref="TickAsync"/>'s look, saying into <paramref name="events"/> and <paramref name="concluded"/> as it goes.</summary>
    private async Task<TickReport> LookAsync(List<string> events, List<SessionEnded> concluded, CancellationToken ct)
    {
        // LIMITRESUME1b (the limit-resume design §3.1): a cool-off the file would not take is written again once per look, held in memory
        // meanwhile, so the account stays passed until its reset with no strike spent on a full disk.
        AccountCooling.WriteUnkept(home, Context.Harnesses.Clock());

        // DRAINHOLD1: what an update's drain held is said again if a later drain holds it, once this look does not drain.
        if (!config.Draining) Context.Runs.DrainLifted();

        // FOUNDATION1 z-iii (D212; the design §13.5, §13.6): the conversations the sessions' binding files keep move into the desktop store at
        // the first look the start's confirmation is settled, and a file an older build wrote during a downgrade at the look that finds it, so
        // a resume reads its id from the binding. A look with no such file asks nothing of the store.
        Context.Conversations.ImportFiles();

        // FOUNDATION1 z-v: the notices' file moves into the store the same way, and stays as the backup.
        Notices.ImportFile(home);

        // The look's registered passes run at their phase's anchor, in their order; the code that has not moved into a pass runs
        // between the anchors.
        var round = new LookRound(Part, events, new LookState(concluded));
        foreach (var pass in LookPasses.In(LookPhase.Before)) await pass.RunAsync(round, ct).ConfigureAwait(false);

        foreach (var pass in LookPasses.In(LookPhase.Local)) await pass.RunAsync(round, ct).ConfigureAwait(false);

        foreach (var pass in LookPasses.In(LookPhase.Read)) await pass.RunAsync(round, ct).ConfigureAwait(false);

        // What the passes left in the state, which the stages not yet passes read and the snapshot they go on shaping.
        var state = round.State;
        var snapshot = state.Snapshot;
        var passes = state.Passes;
        var judgedOpinions = state.JudgedOpinions;

        // Whether this loop carries Daoris's browser decides whether a local set-up step can be shown here (REVIEWENV1c).
        IReadOnlyList<Consideration> plan;
        using (Stages.Begin("plan"))
        {
            snapshot = snapshot with { Starting = [.. state.ReservedBefore.Union(Context.Runs.Reserved)] };
            plan = Planner.Plan(
                snapshot, passes > 0 ? config with { Cap = Math.Max(0, config.Cap - passes) } : config, Door(), window: Context.Browser is not null,
                roles: Context.Roles);
        }

        var progressed = false;

        // What held at spawn, by quest — the plan said Start and the spawn said no. Folded back into
        // the report's considerations, so "why is this sitting" is answered for the holds a real
        // machine actually hits, not only the ones the planner can see.
        var heldAt = new Dictionary<string, string>(StringComparer.Ordinal);
        // The accounts each held quest's start passed not signed in (TOOL6g), said beside its hold.
        var signedOutAt = new Dictionary<string, SignedOutAccounts>(StringComparer.Ordinal);

        // The holds that are the harness's trust (D73), as facts: the screen offers exactly these.
        var untrusted = new List<TrustHold>();

        // Each start's line of work on an ask, whose channel its instruction is handed what waited on (SESSONE2b).
        var starts = plan.Where(c => c.Verdict == StartVerdict.Start)
            .Select(c => c with { Channel = ChannelEntry.ForQuest(c.Quest, snapshot) })
            .ToList();

        state.Snapshot = snapshot;
        state.Starts = starts;
        foreach (var pass in LookPasses.In(LookPhase.Planned)) await pass.RunAsync(round, ct).ConfigureAwait(false);
        var intakes = state.Intakes;

        // 🔴 One run per record the person's words wait on (ANSWER2, D131 §3): a run that took a record up moves it to working a
        // moment after the look planned it (before its spawn since SAIDBACK1e), so a look in between still reads it waiting and
        // plans it again. A second run's move to working was refused (working → working) and failed the session the first was resuming.
        // Claimed here for the run's whole life, so a later look begins nothing on it, and says why. After the intakes are
        // counted, so the slot the record holds is counted as the plan counted it.
        var goingOn = new Dictionary<string, string>(StringComparer.Ordinal);
        var claimed = new List<Consideration>();
        foreach (var start in starts)
        {
            if (start is { GoesOn: true, Resumes: { } record })
            {
                if (!Context.Runs.TryGoOn(record.Session))
                {
                    events.Add(Lines.Progress("going-on", $"going on  session {record.Session} (#{start.Quest.Id} → {start.Quest.To}): "
                        + "a run of this machine's already goes on with the person's words in it."));
                    continue;
                }

                goingOn[start.Quest.Id] = record.Session;
            }

            claimed.Add(start);
        }

        // Every start begun at once, as before: sessions in different repositories, or in trees of their own, run together. 🔴 PARALLEL1b
        // (D181 point 3): each is reserved first, its quest or ask, the tree it goes back into and its place under the cap, so a look
        // that runs before its record opens neither plans it again nor gives its place (Snapshot.Starting). The look then waits for
        // each until it has opened its record or come to nothing (DEV3), so a hold, a refusal and an error before the spawn are still
        // this look's to say; but under the watch only until something else wants the loop (Wanted): a start still preparing (git
        // under a scan, an account's probe, a plugin slow to answer) runs on beside the look, reserved, and what it comes to joins a
        // later look's report. The rest of each run is the running set's.
        var beginning = new List<(StartBegun Started, LookStages.StageSpan Span, StartReservation Reserved, Task<StartCame> Came)>();
        foreach (var start in claimed)
        {
            // The agent it runs on: a handed quest's own (MIXAGENT1a-ii), which the held-wait report names its cooling by.
            var started = new StartBegun(
                start.Quest.Id, null, start.Quest.To, start.Workspace,
                StartPlan.AgentFor(
                    StartWork(start.Quest), config, start.Resumes, new HandedQuests(home).Read(start.Quest.Id)?.Agent, CatalogueForReport(), PlaceOf(start)), start);
            Begin(started, new StartReservation(start.Quest.Id, null, start.Quest.To, TreeGoneBackInto(start)), (own, opened) => ClaimedAsync(start, own, opened));
        }

        foreach (var ask in intakes)
        {
            var started = new StartBegun(null, ask.Id, $"ask #{ask.Id}", ask.Workspace, StartPlan.AgentFor(ParticipantWork.Intake, config, catalogue: CatalogueForReport(), place: new RolePlace(ask.Workspace)));
            Begin(started, new StartReservation(null, ask.Id, $"ask #{ask.Id}", null), (own, opened) => RunIntakeAsync(ask, own, opened, ct));
        }

        var begun = await SettledAsync(beginning).ConfigureAwait(false);

        // What the starts an earlier look let go of came to since (PARALLEL1b), said first, as that look would have said it; this
        // look's own outcome for the same quest is said after it, and stands.
        var carried = Context.Runs.TakeCame();
        IReadOnlyList<StartCame> outcomes = [.. carried, .. begun];
        foreach (var came in outcomes)
        {
            untrusted.AddRange(came.Untrusted);
            if (came.Came is null)
            {
                // Null is a session opened, and running on: its ending joins a later look's report.
                progressed = true;
                if (came.Started.Quest is { } opened)
                {
                    heldAt.Remove(opened);
                    signedOutAt.Remove(opened);
                }

                // LIMITRESUME1a (D206 point 1): a line a limit cut off says that it went on, once, as its own news, not its end's.
                // LIMITRESUME1b: or, its own account ready again, that it resumed its own conversation.
                if (LimitedLine(came.Started) is { } wentOn)
                {
                    var (code, line) = LimitResumed.TryRemove(wentOn.Quest!, out _)
                        ? (LookSaid.LimitResumed, LimitResumedLine)
                        : (LookSaid.LimitWentOn, LimitWentOnLine);
                    events.Add(Lines.News("start", wentOn with { Code = code }, $"start  #{wentOn.Quest}: {line}"));
                }

                continue;
            }

            // A start held on its accounts waits on the person (its row); one held otherwise is tried at a later look (NOTIFY2h). A line a
            // limit cut off, held on cooling accounts, says it waits as its own news, once (LIMITRESUME1a), the band saying the time.
            if (came.Came.Cooling is not null && LimitedLine(came.Started) is { } limitWaits)
            {
                events.Add(Lines.News("start", limitWaits, came.Came.Line));
            }
            else
            {
                var startClass = came.Came is { Cooling: not null } or { SignedOut: not null } ? LookClass.Wait
                    : came.Came is { Held: not null, Ended: null } ? LookClass.Retry
                    : LookClass.Progress;
                events.Add(Lines.Of(startClass, "start", came.Came.Line));
            }

            progressed |= came.Came.Opened;
            if (came.Came.Ended is not null) concluded.Add(came.Came.Ended);
            if (came.Started.Quest is { } quest && came.Came.Held is not null) heldAt[quest] = came.Came.Held;
            if (came.Started.Quest is { } passed && came.Came.SignedOut is not null) signedOutAt[passed] = came.Came.SignedOut;
        }

        var waits = Waits(outcomes);

        state.OpenedStarts = outcomes
            .Where(each => each.Opened && each.Started.Start is not null)
            .Select(each => (Consideration)each.Started.Start!)
            .DistinctBy(start => start.Quest.Id, StringComparer.Ordinal)
            .ToList();
        foreach (var pass in LookPasses.In(LookPhase.Begun)) await pass.RunAsync(round, ct).ConfigureAwait(false);

        foreach (var pass in LookPasses.In(LookPhase.After)) await pass.RunAsync(round, ct).ConfigureAwait(false);

        return new TickReport(Considerations.Blocked(plan, heldAt, signedOutAt), events, progressed, snapshot.Active, concluded)
        {
            Untrusted = untrusted,
            Waits = waits,
            JudgedOpinions = judgedOpinions,
        };

        // PARALLEL1b: one start reserved, then begun. The planner holds a reserved quest, ask and tree, so a reservation refused here is
        // a start outside the look's plan reaching the same work first: said, and nothing begun twice.
        void Begin(StartBegun started, StartReservation reserved, Func<List<TrustHold>, Action, Task<StartRun>> start)
        {
            if (!Context.Runs.TryReserve(reserved))
            {
                events.Add(Lines.Progress(
                    "starting", $"starting  {started.Where}: a start of this machine's is already opening its session, so none is begun beside it."));
                if (started.Quest is { } quest && goingOn.TryGetValue(quest, out var session)) Context.Runs.WentOn(session);
                return;
            }

            var span = Stages.Begin("start", started.Quest is { } named ? $"#{named}" : $"ask#{started.Ask}");
            beginning.Add((started, span, reserved, BeginAsync(started, span, reserved, start)));
        }

        // Each start's preparation, from its holds to its record open or to what it came to (LOOKWAIT1), timed with its quest or ask. Its
        // reservation is let go the moment its record opens, when the ledger holds it, or once it came to nothing (PARALLEL1b). Run as
        // its span's own work, so its git waiting for a place is said as its queue (LOOKWAIT1b); the look that holds the span is not.
        async Task<StartCame> BeginAsync(StartBegun started, LookStages.StageSpan span, StartReservation reserved, Func<List<TrustHold>, Action, Task<StartRun>> start)
        {
            var own = new List<TrustHold>();
            try
            {
                var came = await span.RunAsync(() => Context.Runs.StartAsync(opened => start(own, () =>
                {
                    Context.Runs.Release(reserved);
                    opened();
                }))).ConfigureAwait(false);
                return new StartCame(started, came, own);
            }
            finally
            {
                Context.Runs.Release(reserved);
                span.Dispose();
            }
        }

        // The starts, each to its record open or to what it came to (DEV3). PARALLEL1b: under the watch only until something else wants
        // the loop (Wanted); each still preparing then runs on beside the look, reserved, and what it comes to is carried to a later
        // look's report. A start that threw before its record opened fails the look, as it always did.
        async Task<IReadOnlyList<StartCame>> SettledAsync(List<(StartBegun Started, LookStages.StageSpan Span, StartReservation Reserved, Task<StartCame> Came)> starting)
        {
            if (starting.Count == 0) return [];
            var all = Task.WhenAll(starting.Select(each => each.Came));
            if (Wanted is not { } wanted) return await all.ConfigureAwait(false);

            using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var first = await Task.WhenAny(all, wanted(waiting.Token)).ConfigureAwait(false);
                await waiting.CancelAsync().ConfigureAwait(false);
                if (first == all) return await all.ConfigureAwait(false);
            }

            // Partitioned once: a start that settles after this is carried, never said twice.
            var settled = new List<Task<StartCame>>();
            foreach (var each in starting)
            {
                if (each.Came.IsCompleted)
                {
                    settled.Add(each.Came);
                    continue;
                }

                each.Span.Aside();
                Context.Runs.Carry(each.Started, each.Came);
            }

            var came = new List<StartCame>();
            foreach (var task in settled) came.Add(await task.ConfigureAwait(false));
            return came;
        }

        // The tree a start goes back into, where its repository opens trees (PARALLEL1b): a further part's, a resume's or a carry-on's.
        // A first start grows its own, and in a repository whose sessions run in its root, the root is held by its repository.
        string? TreeGoneBackInto(Consideration start) =>
            config.OpensOwnTree(start.Quest.To) ? start.Entry?.Into?.Tree ?? start.Resumes?.Tree : null;

        // The run, and its claim on the record its words wait on let go once it has ended, however it ended (ANSWER2). The plugins' say
        // first, BEFORE a start costs anything (D64 §4): the first hold in catalogue order is the quest's reason for sitting, exactly as a
        // dirty tree or a missing binary would be, and a plugin that could not decide holds too, naming itself. PARALLEL1b: each start
        // asks on its own, at once with the others, so one plugin slow to answer for one quest holds that start alone.
        async Task<StartRun> ClaimedAsync(Consideration start, List<TrustHold> own, Action opened)
        {
            try
            {
                if (Context.Hooks is { } considering)
                {
                    var decision = await Stages.TimeAsync("consider", () => considering.ConsiderAsync(start, ct), $"#{start.Quest.Id}")
                        .ConfigureAwait(false);
                    if (!decision.Allowed)
                    {
                        return new StartRun($"held  #{start.Quest.Id} → {start.Quest.To}: {decision.Reason}", Opened: false, Held: decision.Reason);
                    }
                }

                // QUEUEDTREE1: the tree the start opens goes on the driver's close before its record's open is asked, on no token.
                var fresh = new FreshTree(path => Context.Trees.RemoveAsync(path));
                var run = await fresh.UntilOpenAsync(
                        () => Runner is { } runner ? runner(start, opened, ct) : RunAsync(start, snapshot.Repositories, own, opened, fresh, ct), ct)
                    .ConfigureAwait(false);

                // LIMITPICK2: a start refused for its account's sign-in goes on under the next account the walk gives, once, in this start;
                // where the walk gives none (or the same), the first run's end stands as it was.
                if (run.SignedOutOn is { } signedOut)
                {
                    var holding = MovingReservation(start);
                    try
                    {
                        var again = new FreshTree(path => Context.Trees.RemoveAsync(path));
                        var moved = await again.UntilOpenAsync(
                                () => RunAsync(start, snapshot.Repositories, own, () => Context.Runs.Release(holding), again, ct, movedOff: signedOut, movedFrom: run.Ended?.Adapter), ct)
                            .ConfigureAwait(false);
                        if (moved.Opened) return moved;
                    }
                    finally
                    {
                        Context.Runs.Release(holding);
                    }
                }

                // MODELREFUSE1: the same, for a model its agent refused; a walk that gives nothing runnable leaves the first run's end as it was.
                if (run.ModelRefusedOn is { } refusedOn)
                {
                    var holding = MovingReservation(start);
                    try
                    {
                        var again = new FreshTree(path => Context.Trees.RemoveAsync(path));
                        var moved = await again.UntilOpenAsync(
                                () => RunAsync(start, snapshot.Repositories, own, () => Context.Runs.Release(holding), again, ct, movedOffModel: refusedOn), ct)
                            .ConfigureAwait(false);
                        if (moved.Opened) return moved;
                    }
                    finally
                    {
                        Context.Runs.Release(holding);
                    }
                }

                return run;
            }
            finally
            {
                if (goingOn.TryGetValue(start.Quest.Id, out var session)) Context.Runs.WentOn(session);
            }
        }
    }

    /// <summary>The line said where a line a limit cut off went on (LIMITRESUME1a): a carry-on is a new part handed the brief.</summary>
    internal const string LimitWentOnLine = "work an account's limit cut off went on in a new session, handed Daoris's brief.";

    /// <summary>The line said where a line a limit cut off resumed its own conversation on its own account (LIMITRESUME1b).</summary>
    internal const string LimitResumedLine = "work an account's limit cut off resumed in its own conversation, its account ready again.";

    /// <summary>
    /// The quests whose start resumed their limited record's own conversation (LIMITRESUME1b), by quest, the record as the value: set as
    /// the start keeps Daoris's word, taken off where its own conversation refused, and read once by the look's news.
    /// </summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<string, string> LimitResumed { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The line's own news for a start of a quest whose last part an account's limit ended (LIMITRESUME1a, D206 point 1; the limit-resume
    /// design §3.1), coded <see cref="LookSaid.LimitWaits"/> and keyed by that part, so each limited part says it waits once and went on
    /// once; null for any other start. A start the person's words make is theirs, never said back (R3), so it is none.
    /// </summary>
    internal static LookNews? LimitedLine(StartBegun started) =>
        started is { Quest: { } quest, Start: Consideration { GoesOn: false, Resumes: { Limit: true } prior } }
            ? new LookNews(LookSaid.LimitWaits, prior.Session, prior.Session, quest, started.Workspace)
            : null;

    /// <summary>
    /// The starts this look held on a cooling account, one wait per account (TOOL4d, D125 §4), each written to the
    /// machine log the first look it appears in and never again until a new cool-off; then the starts it held on accounts not
    /// signed in with none cooling, one wait per agent with no time (UX6d1), written once per set of accounts.
    /// </summary>
    private IReadOnlyList<AccountWait> Waits(IEnumerable<StartCame> begun)
    {
        var waits = begun
            .Where(each => each.Came?.Cooling is not null)
            .GroupBy(each => $"{each.Came!.Cooling!.Agent}/{each.Came.Cooling.Account ?? ""}", StringComparer.OrdinalIgnoreCase)
            .Select(account =>
            {
                var held = account.ToList();
                var cooling = held[0].Came!.Cooling!;
                var workspaces = held.Select(each => each.Started.Workspace).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                // The person's name for the account, read as the look says the wait (ACCT2b): the page says it, the log the id.
                var names = AccountNames.Of(Context.Harnesses.Home, cooling.Agent);
                // The agent the start asked, as its plan answered it, where its hold says one (FOUNDATION1j part ii); else the one it began on.
                return new AccountWait(
                    held[0].Came!.Agent ?? held[0].Started.Adapter, cooling.Agent, cooling.Account, workspaces.Count == 1 ? workspaces[0] : null,
                    cooling.Until, cooling.Stated, held[0].Came!.Held ?? CoolingWords.Hold(cooling, Context.Harnesses.Zone, names))
                {
                    Quests = [.. held.Select(each => each.Started.Quest).OfType<string>()],
                    Asks = [.. held.Select(each => each.Started.Ask).OfType<string>()],
                    Repositories = [.. held.Select(each => each.Started.Where).Distinct(StringComparer.OrdinalIgnoreCase)],
                    SignedOut = SignedOutOf(held),
                    Name = cooling.Account is { } id && names.TryGetValue(id, out var name) ? name : null,
                };
            })
            .ToList();

        foreach (var wait in waits.Where(wait => Context.Harnesses.NewWait(wait.Agent, wait.Account, wait.Until, wait.SignedOut)))
        {
            service.AccountSaid(AccountLine.Waiting(
                wait.Adapter, wait.Account, wait.Workspace, wait.Until, wait.Quests.Count + wait.Asks.Count, wait.SignedOut));
        }

        // A start held on accounts not signed in with none cooling waits for a person, not a time (TOOL6g): a wait with no time
        // and no account, naming the accounts a sign-in frees, its quests and its asks' intakes among what it holds (UX6d1). The
        // tick hands it to the page as it hands a cool-off's, since a consideration names quests alone, and an intake held so
        // was the log's line and nothing else. One per agent, as the line is.
        var signedOut = begun
            .Where(each => each.Came is { Cooling: null, SignedOut: not null })
            .GroupBy(each => each.Came!.SignedOut!.Agent, StringComparer.OrdinalIgnoreCase)
            .Select(agent =>
            {
                var held = agent.ToList();
                var workspaces = held.Select(each => each.Started.Workspace).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return new AccountWait(
                    held[0].Came!.Agent ?? held[0].Started.Adapter, held[0].Came!.SignedOut!.Agent, null, workspaces.Count == 1 ? workspaces[0] : null,
                    Until: null, Stated: false, held[0].Came!.Held ?? "")
                {
                    Quests = [.. held.Select(each => each.Started.Quest).OfType<string>()],
                    Asks = [.. held.Select(each => each.Started.Ask).OfType<string>()],
                    Repositories = [.. held.Select(each => each.Started.Where).Distinct(StringComparer.OrdinalIgnoreCase)],
                    SignedOut = SignedOutOf(held),
                    NoAccount = held.Any(each => each.Came!.SignedOut!.NoAccount),
                };
            })
            .ToList();

        // The line is written with no time, once per agent and set of accounts. Kept apart from a cool-off of the tool's own
        // sign-in, whose account is null too: no profile name has a colon.
        foreach (var wait in signedOut.Where(wait => Context.Harnesses.NewWait(wait.Agent, ":signed-out", null, wait.SignedOut)))
        {
            service.AccountSaid(AccountLine.Waiting(
                wait.Adapter, null, wait.Workspace, until: null, wait.Quests.Count + wait.Asks.Count, wait.SignedOut));
        }

        return [.. waits, .. signedOut];

        static IReadOnlyList<string> SignedOutOf(IEnumerable<StartCame> held) =>
            [.. held.SelectMany(each => each.Came?.SignedOut?.Accounts ?? []).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// A driven record that just concluded: a done whose rule reads it is owed a second opinion, which a later look asks (XAGENT1f, the
    /// second-agent design §2.1). Never a reason to fail the run: the record has concluded.
    /// </summary>
    internal async Task ConcludedForOpinionAsync(
        string sessionId, QuestView quest, string? status, string state, string workTree, string? workspace, CancellationToken ct)
    {
        var process = status == "Done"
            ? await WorkflowProcesses.ConcludedAsync(home, config, new ServiceReviewWorld(service), quest, workTree, workspace, ct).ConfigureAwait(false)
            : null;
        OpinionLook.Concluded(home, config, sessionId, quest, status, state, workTree, process: process);
    }

    /// <summary>
    /// Look until nothing runs and a look starts nothing — the deterministic mode a gate drives (D115 §3.1):
    /// a queue that is empty, held, or waiting on the person, with no session of this driver's still working
    /// and no ending left unreported.
    /// </summary>
    /// <remarks>
    /// A look no longer waits for its sessions, so between looks this waits for the next ending or the
    /// watch's pace, whichever is first — the sync beside the sessions and the lost-claim stop ride every
    /// look, as they ride the watch's (D68 §5, §6). A failure or a close lets go only once every session has
    /// written how it ended, as a look that waited for its sessions did (REV3), and has said what ended (DEV3a).
    /// </remarks>
    /// <param name="said">
    /// Handed each look's report as the look ends (DEV3a), a look that failed or was closed what it had said before (DEV3c), and
    /// before a failure or a close lets go, what ended while it waited: what the headless host prints, so a look's lines reach
    /// the person whatever ends the run after it.
    /// </param>
    public async Task<IReadOnlyList<TickReport>> RunUntilIdleAsync(CancellationToken ct = default, Action<TickReport>? said = null)
    {
        var reports = new List<TickReport>();
        try
        {
            while (true)
            {
                var report = await TickAsync(ct, failed: said).ConfigureAwait(false);
                reports.Add(report);
                said?.Invoke(report);
                if (!report.Progressed && Context.Runs.Idle) return reports;

                await Context.Runs.NextAsync(_syncBeside, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            }
        }
        catch
        {
            await LetGoAsync(said).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// One look, then every session it started to its end — the headless host's <c>--once</c>, and a test that
    /// reads one start whole. The endings join the look's own report, as they did when a look waited for its
    /// sessions.
    /// </summary>
    /// <remarks>
    /// 🔴 It never lets go before what its look started has ended (DEV3a): each session's ending written to the service and
    /// to the machine log, and said. A pass beside the sessions that fails is a line, and the next pass tries again; a run that
    /// left there let the process end with its sessions still working, and a lost-claim stop already made went unsaid.
    /// </remarks>
    /// <param name="said">
    /// Handed the look's report as the look ends, each pass beside its sessions that said anything as that pass ends (DEV3b),
    /// then what ended (DEV3a), which together are the report returned: what the headless host prints, so a line reaches the
    /// person as it is made, whatever ends the run after it. A look that fails hands it what it had said before (DEV3c).
    /// </param>
    public async Task<TickReport> RunOnceAsync(CancellationToken ct = default, Action<TickReport>? said = null)
    {
        TickReport look;
        try
        {
            look = await TickAsync(ct, failed: said).ConfigureAwait(false);
        }
        catch
        {
            // A look that failed after opening a session still waits for it, as a look always did: its record
            // is written before the failure is said.
            await LetGoAsync(said).ConfigureAwait(false);
            throw;
        }

        said?.Invoke(look);
        var settled = await SettleAsync(ct, said).ConfigureAwait(false);
        return look with
        {
            Events = [.. look.Events, .. settled.Events],
            Concluded = [.. look.Concluded, .. settled.Concluded],
        };
    }

    /// <summary>
    /// What a run that failed or was closed does before it lets go (REV3, DEV3a): it waits until every session it started has
    /// written how it ended, then hands <paramref name="said"/> what ended, so the endings are said rather than lost with the
    /// process. Nothing is asked of the service: it may be what failed.
    /// </summary>
    private async Task LetGoAsync(Action<TickReport>? said)
    {
        await Context.Runs.SettledAsync().ConfigureAwait(false);
        if (said is null) return;

        var ended = Context.Runs.Drain();
        if (ended.Count == 0) return;
        said(new TickReport([], [.. ended.Select(run => Lines.Ran(run.Look, run.Ended is { ByPerson: true }, run.Line))], Progressed: false, Concluded: [.. ended.Select(run => run.Ended).OfType<SessionEnded>()]));
    }

    /// <summary>
    /// Wait until nothing this driver started runs, then report what ended. While they work, the sync runs
    /// beside them at the watch's pace, each pass followed by the lost-claim stop (D68 §5, §6).
    /// </summary>
    /// <remarks>
    /// A pass that fails is said and the next tries again (DEV3a), as a look's sync is, since leaving here would leave the
    /// sessions with nothing watching. Only the close leaves, and what was gathered and what ended is handed to
    /// <paramref name="said"/> before it does.
    /// </remarks>
    /// <param name="said">
    /// Handed each pass that said anything as the pass ends (DEV3b), then what ended: together, the report returned.
    /// </param>
    private async Task<TickReport> SettleAsync(CancellationToken ct, Action<TickReport>? said = null)
    {
        var events = new List<string>();
        var concluded = new List<SessionEnded>();
        // What is gathered and not yet said. 🔴 Each pass is said as it ends (DEV3b): kept for the run's end, a lost-claim stop
        // the family rehearsal's run had made went with the process, ended from outside before its sessions had all ended.
        var pending = new List<string>();
        void Say(IReadOnlyList<SessionEnded> ended)
        {
            List<string> lines;
            lock (pending)
            {
                lines = [.. pending];
                pending.Clear();
            }

            if (lines.Count == 0 && ended.Count == 0) return;
            events.AddRange(lines);
            said?.Invoke(new TickReport([], lines, Progressed: false, Concluded: ended));
        }

        var settled = Context.Runs.SettledAsync();
        try
        {
            // SESSWATCH1a (the session-watch design §2): woken at the pace whether or not a remote syncs, so the watcher judges the
            // sessions it waits for, and a --quiet-end ends one nobody watches; with no remote, the watcher's look alone.
            while (!settled.IsCompleted && !ct.IsCancellationRequested)
            {
                using var pace = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var first = await Task.WhenAny(settled, Task.Delay(_syncBeside, pace.Token)).ConfigureAwait(false);
                await pace.CancelAsync().ConfigureAwait(false);
                if (first == settled || ct.IsCancellationRequested) break;

                try
                {
                    lock (pending) pending.AddRange(LookIn(Lines));
                    if (sync is not null)
                    {
                        await LookPasses.Of<LookSync>().OnceAsync(pending, ct).ConfigureAwait(false);
                        await LookPasses.Of<LostClaimStop>().StopAsync(pending, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // 🔴 Closing (REV3): the sessions are ending on this same token, and each writes how it ended.
                    // Leaving before them left them unrecorded when the process exited.
                    break;
                }
                catch (Exception error)
                {
                    lock (pending) pending.Add(Lines.Retry("sync", $"sync  a pass beside the running sessions failed, and the next tries again: {error.Message}"));
                }

                Say([]);
            }

            await settled.ConfigureAwait(false);

            try
            {
                await LookPasses.Of<EndingsLook>().EndAsync(pending, concluded, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                // What ended is drained into the report before the pass after it runs, so a failed pass loses none of it.
                lock (pending) pending.Add(Lines.Retry("sync", $"sync  the pass after the sessions ended failed, and the next look tries again: {error.Message}"));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Say(concluded);
            throw;
        }

        Say(concluded);
        return new TickReport([], events, Progressed: false, Concluded: concluded);
    }

    /// <summary>The reservation that keeps a look from planning a quest again between a start's refused end and its move to another account (LIMITPICK2).</summary>
    private StartReservation MovingReservation(Consideration start) =>
        new(start.Quest.Id, null, start.Quest.To, config.OpensOwnTree(start.Quest.To) ? start.Entry?.Into?.Tree : null);

    /// <returns>
    /// The console line, whether a session actually opened, — when one ended here — what it ended
    /// as and whose decision that was (SURF5b), and — when the start was HELD at spawn — the hold's
    /// own sentence, which the report carries as the quest's verdict. A hold or a refusal ends
    /// nothing, so what ended is null: there is no session to have ended. A refusal is not a
    /// hold either: somebody else got there first, and the quest is theirs rather than sitting.
    /// </returns>
    /// <param name="registry">The registry as this tick's snapshot answered it: what the session may reach across (D107).</param>
    /// <param name="onOpened">Called once the session's record is open, and never for a start that held or was refused (DEV3).</param>
    /// <param name="fresh">The tree this start opens, let go on each way out that starts nothing, on no token (QUEUEDTREE1).</param>
    /// <param name="movedOff">The account the first run of this start was refused for its sign-in (LIMITPICK2); a walk that gives it again holds.</param>
    private async Task<StartRun> RunAsync(
        Consideration start, IReadOnlyList<RepoView> registry, List<TrustHold> untrusted, Action onOpened, FreshTree fresh, CancellationToken ct,
        string? movedOff = null, (string Agent, string Model)? movedOffModel = null, string? movedFrom = null)
    {
        var quest = start.Quest;
        var root = start.Root!;

        // The agent, a clean checkout, the account and the access setup, each of which may hold the start before anything is grown,
        // locked or opened.
        using var state = new RunState();
        var run = new RunRound(start, registry, untrusted, onOpened, movedOff, fresh, state);
        foreach (var stage in RunStages.In(RunPhase.Choose))
        {
            if (await stage.RunAsync(run, ct).ConfigureAwait(false) is { } held) return held;
        }

        var (agent, handedQuest, stepRole, policyRole, handedFrom) =
            (run.State.Agent, run.State.HandedQuest, run.State.StepRole, run.State.PolicyRole, run.State.HandedFrom);
        var (selection, chosen, buildPasses, passedAccount) = (run.State.Selection, run.State.Chosen, run.State.BuildPasses, run.State.PassedAccount);
        var (limitWalks, policyMove, resume, limitResume) = (run.State.LimitWalks, run.State.PolicyMove, run.State.Resume, run.State.LimitResume);
        var (access, permissions) = (run.State.Access, run.State.Permissions);
        StartRun Hold(string why) => run.Hold(why);
        string? CarriedModel(PriorSession? record) => run.State.CarriedModel(record);
        var refusedHanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The session's own tree, found or grown before the record; the repository's trees stay held in the state until the record is open.
        foreach (var stage in RunStages.In(RunPhase.Grown))
        {
            if (await stage.RunAsync(run, ct).ConfigureAwait(false) is { } held) return held;
        }

        var workTree = run.State.WorkTree;
        foreach (var stage in RunStages.In(RunPhase.Placed))
        {
            if (await stage.RunAsync(run, ct).ConfigureAwait(false) is { } held) return held;
        }

        var (runBound, setUp) = (run.State.Bound, run.State.SetUp);
        var (entryTree, priorStands, entryStands, opened, resumedIn) = (run.State.Tree.EntryTree, run.State.Tree.PriorStands, run.State.Tree.EntryStands, run.State.Tree.Opened, run.State.Tree.ResumedIn);
        var (joinedIn, resuming, carryingOn) = (run.State.Tree.JoinedIn, run.State.Tree.Resuming, run.State.Tree.CarryingOn);
        var (answered, handsOn, inSync, oneLook, starting) = (run.State.Tree.Answered, run.State.Tree.HandsOn, run.State.Tree.InSync, run.State.Tree.OneLook, run.State.Starting);

        // What may still hold the start before the record: the tree's trust by the harness, and the record the words wait on.
        foreach (var stage in RunStages.In(RunPhase.Cleared))
        {
            if (await stage.RunAsync(run, ct).ConfigureAwait(false) is { } held) return held;
        }

        // The record the person's words wait on, continued, or its words handed on below in a new session saying why. It may move the
        // start, the account it walked to and the passed account.
        var goneOn = run.State.GoneOn;
        start = goneOn.Start;
        var (fellBack, answerKept, written, switched) = (goneOn.FellBack, goneOn.AnswerKept, goneOn.Written, goneOn.Switched);
        (selection, passedAccount) = (run.State.Selection, run.State.PassedAccount);

        // A resume that was tried let go of the starting hold (LEFT2) when the park held the tree; the carry-on's open below
        // needs it again, until its own record holds the tree.
        using var restarting = answerKept ? TreeLock.TryStarting(home, root) : null;
        if (answerKept && restarting is null) return Hold(HeldWords.Holding(quest.To, TreeLock.HolderOf(home, root)).Why);

        // Where the tree stands BEFORE anything runs in it (SURF6). Read here rather than after the
        // spawn so it is genuinely the base: between this line and the process starting, the only
        // thing that touches the tree is the process. It goes onto the record because the review
        // reads it back on a later launch, long after this variable is gone — which is what makes
        // the range a fact rather than something reconstructed from the evidence string.
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);

        // The session this start continues, why and who asked (MIXAGENT1a-iii, D163 point 4): the one the quest was handed over
        // from, or one a limit cut off when this runs on another account; named on the record as it opens. SESSCTX1b: every other
        // carry-on names its link too, `retry` where its resume fell back, so the brief follows the line.
        var continues = ContinuesFrom(
            handedQuest, carryingOn ? start.Resumes : null, selection.Profile, selection.Steps?.FirstOrDefault()?.Why,
            retried: fellBack is not null,
            pressed: start.BroughtForward is not null || (start.Resumes?.PersonStopped == true && !handedFrom),
            agent: limitWalks ? agent : null, decided: Decided,
            // STAFFPOLICY1e: the move the role's policy made at that end, which the link names (`transient`).
            policy: policyMove is { } policied ? new CoursePolicy(new StaffAction(policied.Condition, StaffActions.Next, null, null, false, false), DateTimeOffset.UtcNow) : null);

        // 🔴 FOUNDATION1d-ii part 5 (the foundation design §3.4 rule 3): a carry-on that hands the new session the words of the record it goes on
        // from, its fallback's or a resume's asker's, opens it through the keyed door, which moves them in the open's own step, the words named and
        // any of the person's kept there since they were read, so no word said between this start's read and its open stays behind on a record the
        // line has left. Against a host whose `opens` mark is below 2 the key goes with no words to move, or none goes, and they are taken as before.
        var handing = fellBack is not null ? written : handsOn;
        var moves = handing is null
            ? null
            : MovedWords.Asked(
                handing.Session, handing.Waiting.Where(DriverContinuations.Hands(resumed: fellBack is null, switched)).Select(word => word.Id),
                handing.Waiting.Select(word => word.Id));

        // 🔴 FOUNDATION1f-iv part a (the foundation design §3.3, §13.4 steps 1 and 3): a carry-on keeps its move record, decided before its open
        // and asked under the key the open is sent with, so a crash between the old part's end and the new part's words is taken up and no word
        // is sent twice. One the quest's hand-over starts (FOUNDATION1f-v part c) goes on from the hand-over's own move, decided at the ask and
        // left at `old-ended` by the look's take: one move from ask to delivery, not two. A move already standing for the session it goes on
        // from that is not that one, or a store that will not take the decision, holds the start for the next look: nothing is opened and
        // nothing is written. The record is let go on every way out below, ended by its phase unless it is delivered.
        CarryOnRecord? carry = null;
        var openKey = MoveWords.DrivenKey();
        if (CarryOnRecord.Decision(continues, agent, selection.Profile) is { } decision)
        {
            var begun = await Moves.BeginAsync(decision, ct).ConfigureAwait(false);
            if (CarryOnRecord.Of(Moves, begun, handedFrom, agent) is not { } decided)
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                return Hold(begun.Refused is { } unwritten
                    ? $"its move to a new part could not be written, so it waits: {unwritten}"
                    : $"a move of `{decision.Left}` to a new part is still standing, so `#{quest.Id}` waits for it to end.");
            }

            carry = decided;
            if ((carry.OldEnded() ?? carry.Opening(openKey)) is { } unasked)
            {
                await carry.EndAsync().ConfigureAwait(false);
                await fresh.LetGoAsync().ConfigureAwait(false);
                return Hold($"its move to a new part could not be written, so it waits: {unasked}");
            }
        }

        await using var recording = carry;

        // The ledger judges the open — the same door any other client would use. A refusal here is
        // an answer (someone else got there first), not an error. Once asked, the ledger may hold the
        // tree even where a close cuts its answer off, so a close from here leaves it (QUEUEDTREE1).
        fresh.Asked();
        // The tree this spawn will hold (D51) — its own where the repository opted in, the registered root otherwise. Stated rather than left to
        // the service to infer, because this side is the one that knows where it is about to run a process. Whether this is a set-up, for the
        // machine log alone, so a set-up's cost can be read back (WSSETUP11).
        // FOUNDATION1f-iv part a: a carry-on that keeps its record is opened through the keyed door whether or not it hands a word, under the
        // key its record names; it then asks no words to move where it hands none.
        var keyed = moves is null && carry is null
            ? null
            : await service
                .OpenSessionMovingAsync(
                    quest.Id, agent, selection.Version, selection.Profile, workTree, before, continues, moves, ct,
                    setup: SetupQuests.IsSetup(quest.Title), access: access.Values, opens: carry is null ? null : openKey)
                .ConfigureAwait(false);
        var (sessionId, message) = keyed is not null
            ? (keyed.SessionId, keyed.Message)
            : await service
                .OpenSessionAsync(
                    quest.Id, agent, selection.Version, selection.Profile, workTree, before, ct,
                    setup: SetupQuests.IsSetup(quest.Title), from: continues, access: access.Values)
                .ConfigureAwait(false);
        if (sessionId is null)
        {
            // A fresh tree for a session that never opened has no work in it, so the clean removal
            // path takes it — and if anything landed there in the meantime, the refusal keeps it.
            await fresh.LetGoAsync().ConfigureAwait(false);

            // The words of a record the person wrote to are the start's own to say (TICKSHOWN1, D137): carried as the hold's
            // sentence, the consideration reads Blocked and the record's row says its words wait and why, not only going on.
            // Any other start's refusal stays in the events: its quest's row has no words waiting to show it on.
            return new StartRun(
                $"refused  #{quest.Id} → {quest.To}: {message}", false, null, start.GoesOn ? $"the service refused the open: {message}" : null);
        }

        // FOUNDATION1f-iv part a (§13.4 steps 2 to 4): the open's answer with every word it moved, by its id on the new session, then
        // `delivering` and the prompt's mark before the take. A write the store refuses stops the take of the moved words, which wait on the new
        // session for its own go-on, never attempted; the record is then ended by its phase below.
        var fenced = carry is null ? null : await carry.FenceAsync(sessionId, keyed?.Moved ?? []).ConfigureAwait(false);

        // TREELOCK1 (D51's note): its own tree locked in its name while it holds it, so no tool that prunes merged branches takes it;
        // asked before the starting hold goes, so the look's let-go of unheld locks sees it held. Never a reason to hold the start.
        var locksTree = Context.Trees.Holds(workTree);
        if (locksTree && await SessionLocks.HoldAsync(workTree, sessionId, ct).ConfigureAwait(false) is { } unlocked)
        {
            Log?.Warn("tree.unlocked", ("session", sessionId), ("why", unlocked));
        }

        // The record is open, so the ledger holds the tree and a replay's own look sees it in use (LEFT2).
        starting?.Dispose();
        restarting?.Dispose();
        // Marked as this process's start until its spawn's marker replaces it, so a sweep ends it only if this driver goes (QUEUED1).
        Context.Processes.MarkOpening(sessionId);
        onOpened();
        var openedAt = DateTimeOffset.UtcNow;
        // PARKVIEW1 (the park's ask design §2): no question stands while a run of its record works; one the disk keeps is logged.
        Questions(sessionId, new QuestionFiles(home).Clear(sessionId));

        // Which account it opened on and why (TOOL4f, D125 §3.6; TOOL6b, D130 §16.4): its record's first line names the step
        // that chose it, and, for a carry-on, the cut-off session and the turn a limit refused; a start moved off where its
        // scope begins is `account.rotated` in the log.
        var prior = carryingOn ? start.Resumes : null;
        RotatedOpening.Say(
            service, Context.Events, sessionId, agent, selection,
            prior is null
                ? null
                : new RotatedOpening.Carried(
                    prior.Session,
                    prior.Limit ? Context.Events.TurnsEnded(prior.Session) + 1 : null,
                    prior.Limit ? RotatedOpening.ContextOf(usage, Context.Events, prior.Session) : null));
        // Where a later choice of Build's ran, its actor line beside it, naming each choice passed (D205 §4.6 item 1, MODELROLE1c2).
        if (movedOff is not null) RotatedOpening.SayMovedOff(Context.Events, sessionId, AccountNames.Said(AccountNames.Of(home, agent), movedOff));
        if (movedOffModel is { } refusedModel) RotatedOpening.SayMovedOffModel(Context.Events, sessionId, refusedModel.Agent, refusedModel.Model);
        // STAFFPOLICY1e2: a model its agent refused, or a sign-in that walked to another agent, is a move to another choice, counted against `most`.
        var movedRole = policyRole;
        if (movedOffModel is { } countedModel)
        {
            CountMove(quest.Id, movedRole, StaffConditions.ModelRefused, new ActorEffective(countedModel.Agent, countedModel.Model, null, ""), chosen?.From);
        }
        else if (movedOff is not null && movedFrom is { } wasAgent && !string.Equals(wasAgent, agent, StringComparison.OrdinalIgnoreCase))
        {
            CountMove(quest.Id, movedRole, StaffConditions.SignedOut, new ActorEffective(wasAgent, null, null, ""), chosen?.From);
        }
        if (chosen is not null) RotatedOpening.SayActor(Context.Events, sessionId, "build", chosen, buildPasses);
        await RotatedOpening.SayStaffAsync(Context.Events, Context.Harnesses.Staff(), service, Log, sessionId, selection, "build").ConfigureAwait(false);
        // STAFFPOLICY1e2: a part the role's policy moved here says so, on the record and the opening note, after the start's own why.
        if (policyMove is { } movedByPolicy && start.Resumes is { } leftPart)
        {
            await SayPolicyMoveAsync(service, sessionId, policyRole, quest.Id, movedByPolicy, leftPart, chosen?.From).ConfigureAwait(false);
        }
        else
        {
            // A yes releases one move at the start that follows it: a start that moved nothing closes it, so it releases no later one.
            Context.Harnesses.StaffFor(home).Moves.SpendApproval(quest.Id, policyRole);
        }
        // A quest marked design says whose order it ran on: the role taking design, Build's while none does (FOUNDATION1x, MODELROLE1l).
        if (start.Resumes is null && StartWork(quest) == ParticipantWork.Design && DesignRoleName(start, stepRole) is { } designer)
        {
            RotatedOpening.SayDesign(Context.Events, sessionId, designer);
        }

        // Its setup in one line, after the account's (AUTONOMY1a), and the mode it got on its harness (AUTONOMY1b), after the change
        // from the session it goes on from where its permission prompts moved since: a start under today's behaviour going on from none
        // that ran otherwise notes nothing, so its transcript reads as it always did; the record keeps the setup either way. Uncoded until
        // the page's note catalogues word it (AUTONOMY1d).
        if (DrivenAccess.StartNote(access, permissions, start.Resumes) is { } accessNote)
        {
            Context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = accessNote }, say: null);
        }

        // The stop made of it before a close cut its conclusion off, told as its run lets go (FLAKE3).
        SessionStop? stoppedFirst = null;
        // TREELOCK1: whether its record still holds its tree as the run ends, waiting on the person or going on at the next look.
        var holdsTree = false;
        try
        {
            // The words a session could not go on with went to this one (MSG1b, D137 §2.2): off that record by their ids, kept
            // on the ask by the service, so this start's instruction reads them among the person's words; and said in that
            // session's conversation, where the person wrote them. FOUNDATION1d-ii part 5: where the open moved them, off this one by theirs,
            // which the ask keeps alike, each shown here under its id and its files copied here first; never by the ids the record was read with.
            var movedBy = MovedWords.Moving(keyed) ? keyed : null;
            IReadOnlyList<SaidWordView>? movedOn = null;
            // FOUNDATION1f-iv part b: the record's delivery is written where each door's write of the first prompt returns, the moved words'
            // `sent` first and their take after it, so it goes with the take only where the fence was written; where the store refused it,
            // the moved words are not taken here and wait on the new session.
            var delivering = fenced is null ? carry : null;
            if (fenced is not null && movedBy is not null)
            {
                output?.Append(sessionId, $"— the move's record could not be written ({fenced}), so the words moved to this session wait on it.");
            }
            else if (fellBack is not null)
            {
                movedOn = await GoOns.HandedOnAsync(written!, sessionId, fellBack, ct, switched: switched, opened: movedBy, delivery: delivering)
                    .ConfigureAwait(false);
            }
            // SAYHELD1 (A): and those said to the session that asked while its quest waited go to its resume, the same way.
            else if (handsOn is not null)
            {
                movedOn = await GoOns.HandedOnAsync(
                        handsOn, sessionId, ContinueWhy.Of(ContinueWhy.Resumed), ct, resumed: true, opened: movedBy, delivery: delivering)
                    .ConfigureAwait(false);
            }

            // The record that was not fenced is ended by its phase here, not at the end of a run that lasts as long as its session does. One that
            // was fenced goes on to its door's write, which is where it delivers: the moved words' `sent` and their take, or, for a carry-on that
            // moved no word, an older host's, the prompt's outcome alone; a run that never writes ends it by its phase as it is let go.
            if (carry is not null && delivering is null) await carry.EndAsync().ConfigureAwait(false);

            var adapter = Context.Adapters.Resolve(agent);
            // What it may read and write outside its tree (D107): the rules it is handed, and the sentence
            // every harness's instruction carries.
            var across = AcrossRules.Reach(config, registry, quest.To, start.Workspace);
            // What the cut-off session's line left in Daoris's own record (TOOL4f, D125 §3.5, as SESSCTX1b widens it, D189 point 4):
            // Daoris's brief of the whole line, every word it took and each turn's outcome, the tree read in the tree this start
            // works in, which every carry-on is handed, since a cut-off for any reason loses the same thread. And whether it ran on
            // another account that is now cooling, which only a limit makes so.
            // The note its agent left where it asked to hand over (MIXAGENT1c), the brief's own section as that agent's claim.
            var handedNote = carryingOn && handedFrom ? handedQuest?.Note : null;
            var brief = prior is null
                ? null
                : await ContextBriefs.ComposeAsync(
                    service, Context.Events, home, prior.Session, continues?.Why ?? HandOverWhy.CarryOn, conversation: false, note: handedNote,
                    config: config, tree: workTree, ct: ct).ConfigureAwait(false);
            if (prior is not null && continues is not null && brief is not null) SayMoved(prior, sessionId, agent, selection.Profile, continues.Why, brief);
            // CONTEXTDRIVE1b (§3 point 2): a start with no brief carries its line's course where any count is not zero, a quest that failed here
            // before among them; a carry-on's brief carries it among its own.
            var course = brief is null ? ContextBrief.Counted((await LineCountsRead.FactsAsync(service, home, sessionId, ct).ConfigureAwait(false)).Counts) : null;
            var elsewhere = prior is not null && AccountIdentity.OnAnotherAccount(prior, selection.Profile, limitWalks ? agent : null);
            // The person's words on the ask the quest was asked by (DRIFT1b, D133 §2), read for this start whatever kind it is;
            // their standing answer for its repository (KNOWUSE1b), and the work's session language (LANG1c), from this
            // machine's config.
            // The servers the plugins hand this session (D65 §1f), handed before its instruction is composed, which tells it the
            // browser it was handed (BRWHINT1b, D78). With Daoris's own browser brought up for a server that drives it, or that server
            // left out and the transcript told why.
            var (servers, browserNoted, drivesBrowser, browserDrivers) =
                await InAppBrowserServers.HandNotedAsync(Context.Servers, Context.Browser, ct).ConfigureAwait(false);
            var browserNotice = browserNoted?.Note;
            // What waited on the ask's channel for this part's line of work (SESSONE2b, D167 point 7): read for its instruction, never
            // holding the start, and its entry's mark moved once the record works, not before, so a start that fails hands it on.
            var channelDoors = new ServiceChannelDoors(service);
            var channelHeld = start.Channel is { } line ? await ChannelReach.HandAsync(channelDoors, line, sessionId, ct).ConfigureAwait(false) : null;
            // FABRIC-CLOSE1l (CLOSE1f, CLOSE1g; the fabric events design §E5): a quest whose work cycle holds `quest.close` names its graph for
            // the take as its first session opens, so the take binds it in its own write, and a bound quest's session says `ready`, never
            // `done`. A circle synced through a remote, by the map sync reads, binds only where the host judges the remote's handshake and
            // every machine of it declared it knows a bound quest (FABRIC-CLOSE1i). No closure graph: nothing asked, nothing said.
            // FABRIC-CLOSE1o: named for this session, whose take alone binds it, the service judging it again at the take. FABRIC-CUTOVER1g:
            // the graph its run is bound to, as the bind above kept it: Current's work cycle under Current, none under a named version.
            var closure = await ClosureBinding.AtStartAsync(
                service, config, quest, start.Workspace ?? RemoteTarget.DefaultWorkspace,
                RemoteTarget.Effective(Variable, home).ContainsKey(RemoteTarget.Workspace(start.Workspace)), home, ct,
                session: sessionId, run: runBound.Binding)
                .ConfigureAwait(false);
            // The process its work follows (WORKFLOW1f): read for the landing a session in a tree of its own is told, and for the checks its
            // run takes wherever its repository sets them, since a named workflow's run takes its version's, never Current's rule; and
            // wherever its run is bound to a named workflow, whose version's `work`, or a graph's `claimed`, may set them
            // (FABRIC-PIPELINE1b2, 1b3). Read from the binding the bind above came to, so the first session takes its version's, and
            // neither the service's quests nor the run's file is read again (FABRIC-PIPELINE1b3).
            var process = Context.Trees.Holds(workTree) || SessionChecks.For(config, quest.To) is not null || runBound.Binding is { IsCurrent: false }
                ? WorkflowProcesses.Bound(home, config, quest.To, start.Workspace, runBound.Run, runBound.Binding)
                : null;
            // FABRIC-PIPELINE1b (D192 point 5): what its run's checks tell and hand it, its words composed from the declaration, its deny
            // rules where its agent takes Daoris's rules; and those checks kept by its id for `run-heavy`, which reads them by the session
            // its tree's lock names, let go as its run ends, a graph version's `callMinutes` beside them for its long-call watch (1b3).
            var checks = SessionChecks.Of(config, quest.To, process);
            var heavy = HeavyTold.Hand(workTree, home, quest.To, checks, adapter.TakesSettings);
            var checksKept = SessionChecks.Hold(home, sessionId, checks, LongCalls.MinutesOf(process));
            using var checksHeld = new Disposer(() => SessionChecks.LetGo(checksKept));
            var asking = await WithAskWordsAsync(SessionTarget.ForQuest(quest, workTree, service.BaseUrl), sessionId, service, ct).ConfigureAwait(false);
            // FOUNDATION1f-iv part b: a carry-on's moved words are taken once its first prompt is written, and the take is what keeps the
            // person's among them on the ask this instruction reads, so the instruction is handed them here, as the take will have kept them.
            if (delivering is not null && movedBy is not null && movedOn is { Count: > 0 } && asking.Words is AskWords askRead)
            {
                asking = asking with { Words = askRead.WithMoved(movedOn, sessionId, quest.Id) };
            }

            var target = WithAccess(WithLanguage(asking, config, start.Workspace), access) with
            {
                // CONTEXTONE1c (D230 point 3): its standing block, its tree's facts with its repository's routing and the person's choices
                // on this machine, the memory a carry-on's line read, and the file it goes through where the whole first prompt cannot
                // carry it.
                Block = StandingBlocks.OnMachine(asking.Block ?? new StandingFacts { Repository = quest.To }, ParticipantWork.Build, config, registry, start.Workspace) with
                {
                    Checkouts = across.Reads.Count > 0,
                    Memory = brief?.Memory ?? [],
                    File = StandingBlocks.FileOf(home, sessionId),
                },
                // The mode its repository's permission prompts name on its harness (AUTONOMY1b), on either door.
                Access = permissions,
                // MODELROLE1c (D205 §3.2): what the entry the walk took names, carried on every run; a continuation keeps its record's,
                // and its choice with it (FOUNDATION1j part ii, §3.5(a)), so a part carried on from Build's second choice says it is that one.
                Asked = chosen?.Asked ?? (start.Resumes is { } carried ? carried.Asked : ActorAsked.Before(adapter.Name)),
                Choice = chosen?.From ?? start.Resumes?.Choice,
                Browser = InAppBrowserServers.Told(browserDrivers),
                ReadsAcross = across.Reads,
                WritesAcross = across.Writes,
                // Named on its spawn as on its connector, so what it publishes says it did (SESS1).
                Session = sessionId,
                Ready = closure?.Bound == true,
                Answered = answered,
                // What cut the last session off, and what it left uncommitted (D80) — read by the
                // driver, because the session may not run `git status` itself. An open quest's start, handed the words its
                // session could not go on with (MSG1b), takes it as any first start does: it is not yet anyone's.
                // SYNCMERGE1b: a stand-down brought forward is told the quest is this machine's again, never its stop's words that its
                // work would double someone else's.
                CutOff = carryingOn && quest.Status != "Open"
                    ? start.BroughtForward is { } forward ? BroughtForwardTold(forward) : start.Resumes!.Note ?? "it ended before closing the quest."
                    : null,
                // RETRYWORK1: an open quest carried on in the tree its failed session committed work in is told that record's words, to
                // take the quest and finish the work rather than build it again.
                Retried = carryingOn && start.CarriesWork is > 0 ? start.Resumes!.Note ?? "it ended before closing the quest." : null,
                // A further part gone into its session's tree is told what the part before it left uncommitted there (SESSONE1d).
                InFlight = carryingOn || joinedIn is not null ? await WorkingTree.UncommittedAsync(workTree, ct: ct).ConfigureAwait(false) : [],
                // MAKERTRAIL2: the harness the part before it ran on and the models its door reported, so a carry-on of another maker
                // credits what that part left uncommitted.
                LeftBy = carryingOn ? LeftBy(start.Resumes!) : null,
                // What its start did against the line in the tree it goes on in (LANDSYNC2).
                InSync = inSync?.Told,
                // Its repository's remotes Daoris holds, so it does not try them (GITAUTH1a): read after its start's own fetch above. And,
                // where the remote helper is laid out for it, what `git remote -v` shows under the rewrite (GITAUTH1b, the design §5.3);
                // or, on a git older than the doors need, that its remote acts are not Daoris's (GITAUTH1c, §4.1).
                Git = GitHolds.Told(
                    GitHolds.OfRepository(home, quest.To, RemoteTarget.Workspace(start.Workspace)),
                    rewritten: GitWiring.DoorPath(home, GitWiring.RemoteHelper) is not null,
                    origin: GitWiring.DoorPath(home, GitWiring.RemoteHelper) is null
                        ? null
                        : await GitRemotes.AddressOf(workTree, "origin", ct: ct).ConfigureAwait(false),
                    older: await GitWiring.OlderAsync(home, ct).ConfigureAwait(false)),
                // What its tree's open copied of the checkout's dependencies, so it skips the `npm ci` that would delete them (TREEDEPS1).
                // And what a tree made again copied the same way (TREELOCAL2), where the session goes on in one.
                Dependencies = opened?.Dependencies ?? (priorStands?.Told, entryStands?.Told) switch
                {
                    ({ } remadePrior, { } remadeEntry) => $"{remadePrior} {remadeEntry}",
                    ({ } remadePrior, _) => remadePrior,
                    (_, { } remadeEntry) => remadeEntry,
                    _ => null,
                },
                // Its repository's heavy gates, once at its end through `run-heavy`, where its PATH finds the verb (HEAVYRUN1b1). Looked up
                // under the quest's repository, the name DAORIS_REPOSITORY hands `run-heavy`, not the checkout's folder (HEAVYRUN1b2a2).
                // Under its run's checks, the words composed from them (FABRIC-PIPELINE1b): `light`'s light gates, and its heavy gates as
                // the verification's.
                Heavy = heavy.Words,
                GrewFrom = opened?.GrewFrom,
                // The parts before it on its session's line, and how its tree stands to theirs (SESSONE1d).
                Parts = start.Entry is { } entry ? await PartsAsync(entry, joinedIn, entryTree, opened, quest.To, ct).ConfigureAwait(false) : null,
                // What waited on the ask's channel for its line of work (SESSONE2b, design §2.3), its mark moved once it works.
                Channel = channelHeld,
                // How its work will land (WSR1, D87), for a session in a tree of its own — the only kind
                // the review's press reaches. A chain's step is told its chain's branch, which its done moves on (LAND2c). Under a
                // named workflow its run's bound version's landing step says it (WORKFLOW1f); a run is bound before its take
                // (FABRIC-CUTOVER1g), so its first session is told its version's too.
                LandsOn = Context.Trees.Holds(workTree)
                    ? await Context.Trees.PlanAsync(
                        workTree, await LandsAsAsync(sessionId, quest, id => service.FindQuestAsync(id, ct), ct).ConfigureAwait(false), ct,
                        process!)
                        .ConfigureAwait(false)
                    : null,
                // How it commits (PROCESS1d-i, D164's amendment point 4): the rule standing for its repository, told with the landing
                // to a session in a tree of its own on every form, so its agent and the repository's own rules do not stop each other;
                // the person's checkout is told nothing. Under a named workflow the rule applies as everywhere.
                Commits = Context.Trees.Holds(workTree) ? CommitRules.Resolve(config, quest.To, start.Workspace) : null,
                // The person's answer, when the session before parked to ask them (STANDDOWN2).
                PersonSaid = carryingOn ? SaidWordView.PersonsJoined(start.Resumes!.Waiting) : null,
                // SAYHELD1 (A): a resume's, the words said to the session that asked while it waited, each under its id.
                // LIMITSWITCH3: and a carry-on a cooling account's switch made, Daoris's own words it carries (the person's come by the ask).
                // FOUNDATION1d-ii part 5: the words the open moved here, where it moved them, as this record holds them.
                Handed = handsOn is not null
                    ? [.. (movedOn ?? handsOn.Waiting).Where(DriverContinuations.HandedToResume)]
                    : switched ? [.. (movedOn ?? written!.Waiting).Where(word => word.Daoris && DaorisWords.NewPartCarries(word))] : [],
                // The person's stop, which they released with Try again (SESSUX1b): told so, never that it was cut off.
                Released = carryingOn && start.Resumes!.PersonStopped && !handedFrom,
                // The quest was handed over from that session (MIXAGENT1a-ii): told so, with the agent it ran on.
                BroughtForward = carryingOn && quest.Status != "Open" && start.BroughtForward is not null,
                HandedOver = carryingOn && handedFrom ? start.Resumes!.Adapter ?? "another agent" : null,
                Brief = brief,
                Course = course,
                // MIXAGENT1c: the note its agent left where it asked to hand over, told as that agent's claim; the brief carries it.
                HandedNote = handedNote,
                // Another agent's accounts are not this agent's: a hand-over says no account was cooling for it to be told of.
                AccountChanged = elsewhere && !handedFrom && prior!.Limit
                    && LineAccount.OwnHold(Context.Harnesses.CoolingOf(agent, prior.Profile)) == NextHold.Cooling,
                // A set-up step is told its environment as the rule declares it now (REVIEWENV1c, design §2.2), and its account
                // says where that rule was set (REVIEWENV1c2).
                SetUp = setUp,
                SetUpFrom = setUp is null ? null : ReviewRules.Resolve(config, quest.To, start.Workspace)?.Source,
            };
            oneLook.Dispose();
            var (info, harnessNotice) = Prepare(adapter, target, selection);

            // The creating sentence, on the record while the session runs (D51 rule 4): where it grew from and what a fresh
            // tree does not hold. The conclusion's note replaces it — by then the record's Tree field and the evidence say the rest.
            var startingNote = StartingNote(
                opened, quest, answered, resumedIn, carryingOn, start.Resumes, written, elsewhere, fellBack,
                handedOver: carryingOn && handedFrom, walked: carryingOn && handedFrom && passedAccount is not null,
                joinedSession: joinedIn is not null ? start.Entry?.Into?.Session : null, broughtForward: start.BroughtForward is not null,
                carriesWork: carryingOn && start.CarriesWork is > 0);
            await service.AdvanceAsync(sessionId, "starting", note: startingNote?.Note, ct: ct, parts: startingNote?.Parts).ConfigureAwait(false);

            // The answer beneath the question it answers (STANDDOWN2): the session that asked ends with
            // the person's words in its own record, where they read the question. A resume that was tried already put
            // them there (ANSWER1a).
            // Words to a session that had ended are in its conversation already, where the person wrote them (MSG1b).
            if (carryingOn && !answerKept && SaidWordView.PersonsJoined(start.Resumes!.Waiting) is { Length: > 0 } answeredWith && written is { Parked: true })
            {
                Context.Events.Keep(start.Resumes.Session, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = answeredWith }, say: null);
            }

            var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            // The protocol door carries the servers on the wire below; a pipe-door harness that takes a file at spawn is handed
            // one under Daoris's home, and the file goes when the session does.
            Context.Hooks?.Log.Served(Context.Catalog, sessionId, servers);
            var handed = SpawnServers.Hand(adapter, info, home, sessionId, servers);
            // HELPPLUG1b (D170 point 4): the skills plugins hand sessions, composed under the home, gone with the session.
            var (skills, skillPlugins) = SpawnSkills.Hand(adapter, info, home, sessionId, Context.Catalog, PluginServerTo.Sessions);
            using var skilled = new Disposer(() => SpawnSkills.Remove(skills));

            // What this session may do (PERM1, D72): the rules composed for its circle and repository,
            // handed over as the harness's own settings tier.
            // SESSCTX1b: a carry-on reads the kept folders of its line's parts, whose files its brief names (the design §3.1 item 5).
            // CONTEXTONE1c: and the folder its standing block went through, where it went through one.
            var blockFolder = TargetPrompt.Filed(TargetPrompt.ForStart(target, adapter)) ? StandingBlocks.FolderOf(home, sessionId) : null;
            var rules = HandRules(
                adapter, info, sessionId, start.Workspace, quest.To, workTree, target.AttachmentsDirectory, across: across, plugins: skillPlugins,
                continued: blockFolder is null ? brief?.Kept : [.. brief?.Kept ?? [], blockFolder], heavy: heavy);

            Context.Runs.Live[quest.Id] = sessionId;
            using var live = new Disposer(() => Context.Runs.Live.TryRemove(quest.Id, out var _));

            return await Holds.HoldAsync(
                "build", adapter, info, target, sessionId, transcript, workTree, JoinNotices(harnessNotice, browserNotice), rules, handed,
                // FOUNDATION1x: `actor` stays the seeded id; the role the start took and its revision go on the binding. A continuation keeps its record's.
                taken: start.Resumes is null ? RoleTaken(start, stepRole) : null,
                // 🔴 A driven session takes no person's line, on either door (INT4i) — INT4h's rule for
                // an intake, for the same reason: it was handed its whole quest at once, the pipe door
                // gives it no stdin, and the protocol door's stdin carries the driver's own frames.
                refusesInput: TakesNoMessages(quest),
                ct: ct,
                preamble: browserNotice,
                handedServers: servers,
                drivesBrowser: drivesBrowser,
                said: Said(adapter, selection, sessionId),
                // A native run holds what the person says while it works, and goes on with it in its own conversation (MSG1b).
                goesOnAs: selection,
                stopMade: stop => stoppedFirst = stop,
                // The part works: what it was handed from the ask's channel is acknowledged by its id (SESSONE2b, COWORK1d).
                working: channelHeld is null ? null : () => _ = ChannelReach.HandedAsync(channelDoors, channelHeld, CancellationToken.None),
                // BGWAIT1: a turn that ends on the session's own background work, holding its quest as a park would, is not
                // a question. Read from the quest as the conclusion reads it, by the conclusion's own rule.
                waitsOnBackground: WaitsOnBackground(service, quest, sessionId, resumed: start.Resumes is not null),
                // LANDBACK1 part 2: a turn that ends with the quest done and the tree uncommitted is carried on, not concluded.
                carriesOn: CarriesOn(service, quest, workTree, resumed: start.Resumes is not null, () => Context.Processes.StopMade(sessionId) is not null),
                // FOUNDATION1f-iv part b: where the first prompt's write returns, the carry-on's moved words are `sent`, then taken.
                written: delivering is null ? null : delivering.Written,
                // MODELROLE1c2 (D205 §3.3): its agent's refusal of the entry's model at the open is re-asked; a new start's walk may answer a
                // later entry on the same agent and account, set on this session. A continuation keeps its asked choice, so its record ends.
                // PRESSMODEL1: a handed quest's start asks its named agent's own choice (Build's entry for it, else its default), so a
                // refusal of that entry's model is re-asked on that agent's later entries and its default, on the same account.
                offers: new OpenOffers(
                    selection.Version, selection.Profile,
                    chosen is { } walkedEntry && handedQuest is null && (start.Resumes is null || limitWalks)
                        ? _ => ReaskAsync(start, registry, walkedEntry, selection, ct, stepRole)
                        : chosen is not null && handedQuest is not null
                            ? refusal => ReaskHandedAsync(start, agent, selection, refusal, refusedHanded, ct, stepRole)
                            : _ => Task.FromResult<(ActorAsked Asked, string Choice)?>(null)),
                // AGENTERRORS1a (D210 §5.1, §5.5): a driven quest session is tried again in the same conversation on a transient failure,
                // asking before each try whether its quest still wants a turn, against the quest as this start was planned (finding 5).
                transientWanted: retryCt => TransientRetries.WantedAsync(service, quest.Id, sessionId, quest.Status, quest.Awaits, retryCt),
                conclude: async (exitCode, used, turnFailed, ended) =>
                {
                    if (used is not null)
                    {
                        usage?.Record(new UsageEntry(
                            sessionId, quest.To, adapter.Name, selection.Profile,
                            used.Used, used.Size, DateTimeOffset.UtcNow));
                    }

                    // The person's stop outranks the observation: a killed session leaves the same
                    // signals as a crashed one, and only this flag knows whose decision the end was.
                    var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
                    var status = after?.Status ?? "Open";
                    var stop = Context.Processes.StopMade(sessionId);
                    // HELPERWAIT1: a clean end holding its quest, on its agent's own open helper ask and no question of the person's, waits on that helper.
                    var awaitsHelper = stop is null && exitCode == 0 && status == "Taken"
                                       && await WaitsOnOwnHelperAsync(sessionId, quest, openedAt, ct).ConfigureAwait(false);
                    var conclusion = stop is not null
                        // The driver's own stop, for a take that lost (D68 §5), stands it down: the quest was someone
                        // else's, and the reason says who. The person's, a pause's naming the pause (PAUSE1b, design
                        // §4.1), is theirs, never interrupted. One reading, which a close cutting this off records too (FLAKE3).
                        ? stop.Conclusion
                        : exitCode is int code
                            // What it waited on before and after (D79): a NEW question is this session
                            // asking and waiting, which is a good ending, not a stand-down.
                            ? Observation.Conclude(
                                code, status, quest.Awaits, after?.Awaits, turnFailed, resumed: start.Resumes is not null,
                                // Whose take it was, and what it last said (STANDDOWN2): a session
                                // holding the quest it took and stopping is waiting on the person.
                                took: status == "Taken" && await service.TookAsync(sessionId, ct).ConfigureAwait(false),
                                lastWords: SessionLastWords.ParkedWords(Context.Events, sessionId, transcript),
                                // FABRIC-CLOSE1c2: the person's reopen of the work it grew from holds its quest as it ends. FABRIC-CLOSE1c16:
                                // or they reopened it and went on with the step while it ran, so it ends as the reopen stopped it, and is
                                // carried on with its branch brought up to date.
                                reopenedFrom: Planner.ReopenedFrom(quest, after),
                                // BGWAIT1c: the native door's harness ended its own background work with its turn.
                                backgroundCut: ended.BackgroundCut,
                                // FABRIC-CLOSE1h5: a closure graph's submission is its done, so its session is due, never parked.
                                submitted: QuestResults.Submitted(after),
                                awaitsHelper: awaitsHelper,
                                // APIREFUSAL1: its provider refused its turn, so it failed, and never parks on the person.
                                providerRefused: ended.ProviderRefused)
                            // BGWAIT2: a done this run reached outranks the timeout, as it outranks a messy exit.
                            // SESSWATCH1a: the watcher's end (a headless --quiet-end) reads as its own cause, never as a bound's.
                            : Watch.EndedMinutes(sessionId) is { } quietMinutes
                                ? Observation.AfterQuiet(quietMinutes, quest.Status, status)
                                : Observation.AfterTimeout(config.TestBound ?? 0, quest.Status, status);
                    // CODEXPERM1: a park whose turn its agent cut asked the person nothing, and says so.
                    if (stop is null) conclusion = Observation.Cut(conclusion, ended.Cut);

                    // ACCESSSWAP1b: a run Daoris ended at its turn's end for a changed setup is neither a park nor a failure, and goes on.
                    var swappedOn = stop is null && ended.SetupChanged is not null;
                    if (swappedOn) conclusion = Observation.SetupChanged(conclusion, ended.SetupChanged!);

                    // MIXAGENT1c (D191): its agent's ask to hand over, where the person's rule or their yes lets it go, is taken at this
                    // turn's end and never mid-turn: the quest is held on the next agent with the agent's note, written before the record
                    // says it stopped, as the person's own hand-over stops it. Not over the person's stop, nor a run ended for a changed setup.
                    if (stop is null && !swappedOn && exitCode is int && status == "Taken"
                        && await HandOvers.AtTurnEndAsync(sessionId, quest.Id, ct).ConfigureAwait(false) is { } handedTo)
                    {
                        conclusion = SessionConclusion.Of("stopped", HandOverNotes.QuestHandedOver(handedTo));
                    }

                    // MODELROLE1c2 (D205 §3.3, §3.5(c)): its agent refused the choice at the open, before any prompt, so it ends
                    // `choice-not-offered`: no strike, its quest planned again, no Ended notice, the reading kept before it ended.
                    var notOffered = stop is null && ended.NotOffered is not null;
                    if (notOffered) conclusion = Observation.ChoiceNotOffered(adapter.Name, ended.NotOffered!);

                    // AGENTERRORS1a (D210 §5.1): past its transient bound a failed end is `ended.transient`, no strike and no cool-off, its quest
                    // held for the person; and Daoris's go-on comes off the record by its key where its take-off did not land (R3-O5).
                    if (stop is null && ended.Transient?.Episode is { Exhausted: true } exhausted && conclusion.State == "failed")
                    {
                        conclusion = Observation.Transient(exhausted);
                    }

                    if (ended.Transient?.Opened == true) await TransientRetries.GoOnOffAsync(service, sessionId, ct).ConfigureAwait(false);

                    conclusion = AccountRefused(conclusion, adapter, selection, ended);
                    var beforeSignIn = conclusion;
                    conclusion = AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
                    // LIMITPICK2: the account this start was refused for, where it has a profile another can stand in for.
                    var signedOutOn = !ReferenceEquals(beforeSignIn, conclusion) && stop is null && movedOff is null && start.Resumes is null
                        ? selection.Profile : null;
                    // The quest is reserved again before this record fails, so a look between this end and the next run's open plans nothing
                    // for it; the caller lets it go when that run has opened or come to nothing. Not reserved, it is not moved.
                    if (signedOutOn is not null && !Context.Runs.TryReserve(MovingReservation(start))) signedOutOn = null;
                    // MODELROLE1c3 (D205 §3.3's pipe exception): the pipe door refused the model its choice named on a silent turn, so the
                    // account's reading is kept and the part ends with its line, no strike; the next look starts what the walk then answers.
                    var refusedModel = ModelRefused(conclusion, adapter.Name, selection, target.Asked?.NamedModel, ended, sessionId);
                    var modelRefused = !ReferenceEquals(refusedModel, conclusion);
                    conclusion = refusedModel;
                    // MODELREFUSE1: a model its agent refused, at the open or on a silent pipe turn, moves the same start to the walk's next
                    // answer as an account's sign-in does, under the same reservation; the reading it kept makes the walk pass it.
                    (string Agent, string Model)? modelRefusedOn = null;
                    if ((notOffered || modelRefused) && stop is null && movedOff is null && movedOffModel is null && start.Resumes is null
                        && (ended.NotOffered?.Value ?? target.Asked?.NamedModel) is { Length: > 0 } refusedValue
                        && Context.Runs.TryReserve(MovingReservation(start)))
                    {
                        modelRefusedOn = (adapter.Name, refusedValue);
                    }

                    (conclusion, var limited) = AccountLimited(
                        conclusion, adapter, selection, turnFailed, sessionId, used,
                        // A limit read by its code on the door, with no sentence (§3.1): cooled for D125's unstated hour.
                        byCode: ended.Transient?.Last is { Kind: AgentErrorKind.Limit, Limit: { } coded } ? coded : null);

                    var commits = await WorkingTree.CommitsSinceAsync(workTree, before, ct).ConfigureAwait(false);
                    // EVID1b (D144 §3): a done that waits on its evidence is read here, after the quest and before the record,
                    // at the tree's HEAD now, and the verdict goes to the evidence door and beneath the commits on the record.
                    var evidence = await EvidenceCheck.AtEndAsync(
                            service, after, new EvidenceAt(workTree, EvidenceCodes.SessionEnd) { Base = before, Session = sessionId },
                            commits, ct)
                        .ConfigureAwait(false);
                    // PARKVIEW1 (the park's ask design §2): its question kept only for the park this run ends in, and only where its last
                    // turn asked it; settled before the record moves, so a parked record never shows another run's question.
                    Questions(sessionId, new QuestionFiles(home).SettleRun(sessionId, conclusion.State, openedAt, Context.Events));
                    await service.AdvanceAsync(
                        sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct, limit: limited,
                        parts: conclusion.Parts).ConfigureAwait(false);
                    // D205 §4.1 (round 4, R3-B1): its process has stopped and its record left Running, so its one count is given back, both
                    // halves, before the next look selects what the walk now answers.
                    if (notOffered) Context.Harnesses.Release(selection.Number, sessionId);

                    // REVIEWENV1c (design §2.6): a set-up step's said set-ups posted with the commit Daoris reads from its tree now.
                    // REVIEWENV1d: and its build served to its tab from then until the person's verdict.
                    await ReviewSetUps.PostAsync(service, config, Context.Events, after, sessionId, workTree, start.Workspace, openedAt, ct, Reviews)
                        .ConfigureAwait(false);

                    await ConcludedForOpinionAsync(sessionId, quest, QuestResults.Status(after), conclusion.State, workTree, start.Workspace, ct)
                        .ConfigureAwait(false);

                    // The tree stays, whole — nothing merges itself and nothing deletes itself (D51
                    // rules 6–7): the person merges from the root and discards from a surface that
                    // refuses to destroy work. The line names it so a terminal watcher knows where.
                    var where = opened is not null ? $" [own tree: {opened.Path}]"
                        : resumedIn is not null ? $" [own tree, resumed: {resumedIn}]"
                        : "";
                    // HELPERWAIT1: a session waiting on its own helper goes on in this tree with the answer, so it keeps it, as a park does.
                    var waitsOnHelper = HelperWaits.Ended(conclusion.Parts);
                    holdsTree = SessionStates.IsParked(conclusion.State) || swappedOn || waitsOnHelper;
                    // FABRIC-CLOSE1o: a take its start named a graph for that went unbound (an expired bind, a remote since) is said
                    // here once, beside its take's own answer, since its next start reads it unbound and names nothing.
                    return new StartRun(
                        $"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}){where}: {conclusion.Note}"
                        + ClosureBinding.UnboundAtEnd(
                            closure, after,
                            // CLOSETAKE1: why, read off the session's record only where there is something to say.
                            closure is { Bound: true } && after is { Status: "Taken" or "Done", Closure: null or "" }
                                ? await TookBindOrNullAsync(service, sessionId, ct).ConfigureAwait(false)
                                : null),
                        true,
                        // 🔴 The stop flag IS the "whose decision was this" answer (SURF5b) — the same
                        // one that outranks the observation above. Read once, used for both. A park is
                        // not an ending (STANDDOWN2): the record waits on the person, and the attention
                        // watch says so from the active list, as it does for a parked intake. Nor is a run ended for a changed setup,
                        // whose record goes on at the next look (ACCESSSWAP1b), nor one waiting on its own helper (HELPERWAIT1).
                        SessionStates.IsParked(conclusion.State) || swappedOn || waitsOnHelper
                            ? null
                            : new SessionEnded(
                                sessionId, quest.To, conclusion.State,
                                ByPerson: stop is { ByPerson: true }, conclusion.Note,
                                Quest: quest.Id, Adapter: adapter.Name, Account: selection.Profile)
                            {
                                Limit = limited,
                                NotOffered = notOffered || modelRefused,
                                Reason = SessionEnded.ReasonOf(conclusion.Parts),
                            })
                    {
                        SignedOutOn = signedOutOn,
                        ModelRefusedOn = modelRefusedOn,
                    };
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The person is closing the driver. WaitAsync already ended the process tree, so nothing
            // is orphaned — and the record must say so rather than sit at "working" forever. The write
            // rides an unbound token: the cancelled one would refuse the very report it caused.
            // Interrupted (D104): closing the driver is not a decision about this quest, so a take this
            // session held is carried on at the next start, as a cut-off is.
            // 🔴 Unless a stop came first (FLAKE3): the driver's for a take that lost, or the person's, whose process was still
            // ending, or its conclusion still asking the service, when the close came. That stop decided the end, so the record
            // says it, as its conclusion would have, and is never interrupted.
            var (closed, interrupted) = Observation.Closed(stoppedFirst);
            try
            {
                await service.AdvanceAsync(
                    sessionId, closed.State,
                    stoppedFirst is null && Context.Processes.Close.Build is { } build ? ShellClose.Closed(build) : closed.AsNoted(),
                    ct: CancellationToken.None, interrupted: interrupted).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            if (stoppedFirst is { } first)
            {
                return new StartRun(
                    $"{closed.State}  session {sessionId} (#{quest.Id} → {quest.To}): {closed.Note}",
                    true,
                    new SessionEnded(sessionId, quest.To, closed.State, ByPerson: first.ByPerson, closed.Note, Quest: quest.Id, Adapter: agent));
            }

            // The person is closing the driver, so this ending is theirs — no interruption is owed
            // for something they are in the middle of doing (design §4).
            return new StartRun(
                $"stopped  session {sessionId} (#{quest.Id} → {quest.To}): the driver was stopped.",
                true,
                new SessionEnded(sessionId, quest.To, "stopped", ByPerson: true, Quest: quest.Id, Adapter: agent));
        }
        catch (Exception) when (stoppedFirst is not null)
        {
            // A wire that broke after a stop (the process killed under a handshake's write) is that stop's end, not a failure: the
            // stop decided it, as its conclusion would have, and the person's is never interrupted (D104, FLAKE3).
            var stopped = await EndStoppedAsync(sessionId, stoppedFirst).ConfigureAwait(false);

            return new StartRun(
                $"{stopped.State}  session {sessionId} (#{quest.Id} → {quest.To}): {stopped.Note}",
                true,
                new SessionEnded(sessionId, quest.To, stopped.State, ByPerson: stoppedFirst!.ByPerson, stopped.Note, Quest: quest.Id, Adapter: agent));
        }
        catch (Exception error)
        {
            // Everything but the shutdown above — which includes a client TIMEOUT: HttpClient's is an
            // OperationCanceledException with nobody having cancelled, and filtering it out let it
            // escape the tick and leave the record at `starting` (REV3).
            // The record must say what the driver saw, even when what it saw was its own failure —
            // an abandoned "starting" row reads as a session that never ends.
            try
            {
                await service.AdvanceAsync(
                    sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The terminal write is best-effort by construction: the first failure is the report,
                // and a host that is gone or slow here must not turn that report into a second throw.
            }

            return new StartRun(
                $"failed  session {sessionId} (#{quest.Id} → {quest.To}): {error.Message}",
                true,
                new SessionEnded(sessionId, quest.To, "failed", ByPerson: false, error.Message, Quest: quest.Id, Adapter: agent));
        }
        finally
        {
            // However this ended, the stream is over. The buffer stays readable so whoever was
            // watching can see how it finished; the transcript remains the durable copy either way.
            // In the finally rather than the happy path, because a session that FAILED is the one
            // whose last lines someone most wants to read.
            output?.Close(sessionId);

            // TREELOCK1: the session lets go of its tree however it ended, before any clean-up of Daoris's removes it; on no token,
            // since a close is one of the ways. A record that still holds it keeps the lock.
            if (locksTree && !holdsTree) await SessionLocks.LetGoAsync(workTree, sessionId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The record of a run whose wire broke after a stop made first (STOPRACE1, FLAKE3): ended as that stop's conclusion would
    /// have ended it, never interrupted, and shared by every run that catches a failure after <c>HoldAsync</c>. Best-effort, as
    /// the failure's own write is.
    /// </summary>
    internal async Task<SessionConclusion> EndStoppedAsync(string sessionId, SessionStop stop)
    {
        var (stopped, _) = Observation.Closed(stop);
        try
        {
            await service.AdvanceAsync(sessionId, stopped.State, stopped.AsNoted(), ct: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The first failure is the report; a host that is gone here must not turn the stop into a second throw.
        }

        return stopped;
    }

    /// <summary>
    /// The spawn, prepared for either kind of session: the adapter's process, the toolchain's
    /// environment on it, and — for a dsh home Daoris made — its profile written. Shared by a quest's
    /// session and an ask's intake, so neither can forget a line the other carries.
    /// </summary>
    /// <returns>The process to start, and what this harness is doing that Daoris could not govern.</returns>
    /// <param name="prepared">The process a resume prepared (ANSWER1a), given the same environment; null prepares a start.</param>
    internal (ProcessStartInfo Info, string? Notice) Prepare(
        ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo? prepared = null)
    {
        target = TargetPrompt.ForStart(target, adapter);
        // CONTEXTONE1c: a standing block past what the first prompt carries goes through its file, written before the spawn reads it.
        if (prepared is null) StandingBlocks.Hand(target);
        var info = prepared ?? adapter.Prepare(target with { Home = target.Home ?? DaorisHome.Resolve(Variable) }, config.Commands.GetValueOrDefault(adapter.Name));
        Equip(adapter, info, selection);

        // What daoris writes into a dsh home it owns (ACP3): the two rows that send session
        // material off this machine, off — and its own skills reachable. Only where daoris made
        // the directory; the notice is what happens everywhere else, and a home holding somebody
        // else's patch layer is reported rather than overwritten.
        var harnessNotice = DshProfile.NoticeFor(adapter.Name, selection.ProfileHome);
        if (harnessNotice is null && adapter is DshAdapter && selection.ProfileHome is { Length: > 0 } dshHome)
        {
            try
            {
                DshProfile.Write(dshHome);
            }
            catch (DriverException refused)
            {
                harnessNotice = $"— {refused.Message}";
            }
        }

        return (info, harnessNotice);
    }

    /// <summary>
    /// What every session's process carries on its environment, a driven one's and a conversation's alike: its toolchain's account,
    /// binary and pin (D49 §4), then its tools at the start by the adapter's own switch (HELP5; CONTEXTONE1d, D230 point 3), so its
    /// connector's tools come with its first request rather than behind a search step. Applied by the driver rather than inside each
    /// adapter's spawn, so one line governs every door and kind and no adapter can forget it; the switch is set last, so an account's
    /// own variables cannot defer the tools again.
    /// </summary>
    internal static void Equip(ISessionAdapter adapter, ProcessStartInfo info, HarnessSelection selection)
    {
        if (adapter.Toolchain is { } toolchain)
        {
            HarnessProbe.Apply(
                info, toolchain, selection.ProfileHome, selection.Binary, selection.ClaudeExecutable,
                selection.Environment);
        }

        foreach (var (name, value) in adapter.ToolsUpFront) info.Environment[name] = value;
    }

    /// <inheritdoc cref="DownloadKeeping.KeepHandedAsync"/>
    internal Task KeepHandedAsync(string sessionId, ProcessStartInfo info, CancellationToken ct) =>
        DownloadKeeping.KeepHandedAsync(service, home, sessionId, info, ct);

    /// <summary>
    /// Whether the rules this session would be handed let it call the connector's <paramref name="tool"/>
    /// unasked (D73) — the same composition <see cref="HandRules"/> hands over. False for a harness that
    /// takes no rules at all, which then depends on the repository's own list and so on its trust.
    /// </summary>
    internal bool HandedConnector(ISessionAdapter adapter, string? workspace, string? repository, string tool) =>
        adapter.TakesSettings
        && PermissionRules.AllowsConnector(
            PermissionRules.Compose(PermissionRules.Load(home), workspace, repository), tool);

    /// <summary>
    /// The rules one session may run under (PERM1, D72), composed from this machine's file and handed
    /// over the harness's own way: a flag on the pipe door, <c>session/new</c>'s <c>_meta</c> on the
    /// protocol door. Shared by a quest's session and an ask's intake, so neither forgets it.
    /// </summary>
    /// <param name="tree">The tree the session works in — its own, which the tree guard holds it to (PERM3).</param>
    /// <param name="kept">
    /// The folder THIS session's quest or ask keeps its files in, or null when none is on this machine.
    /// It lies outside the tree, so the session is handed a read of it and of nothing else under the home
    /// (INT4j): a read there would otherwise be asked, and every ask is refused (D52). Only a read — the
    /// tree guard still refuses a write anywhere outside the tree, and says nothing about reads.
    /// </param>
    /// <param name="across">
    /// What it may reach outside its tree (D107): a read of each checkout it may read, an edit in each declared
    /// target, and a refusal everywhere else — beside the person's rules, whose denies still win.
    /// </param>
    /// <param name="said">
    /// The folder THIS session keeps the files the person said with their words in (MSG1d3, D137 §2.4), where a resumed run
    /// is handed them; null for none. A read of it, as of <paramref name="kept"/>, and of nothing else under the home.
    /// </param>
    /// <param name="opinion">
    /// A reviewer's session (XAGENT1d, D155 point 5): the composition laid under the opinion's rules
    /// (<see cref="OpinionPermissions.Shape"/>), and the classifier told it changes nothing.
    /// </param>
    /// <returns>
    /// The file, which goes when the session does, what the protocol door carries, and what was handed, for the instruction's
    /// account (CONTEXT1), which outlives the file.
    /// </returns>
    /// <param name="plugins">
    /// The harness plugins composed for it (HELPPLUG1b, <see cref="SpawnSkills"/>), which the protocol door carries beside its rules;
    /// the pipe door was handed them by its own flag already.
    /// </param>
    /// <param name="brainstorm">
    /// A brainstorm's pass (BRAINSTORM1a2, D176 points 1 and 2): its one result tool, and the composition laid under the
    /// <c>brainstorm</c> posture (<see cref="BrainstormPermissions.Shape"/>), its room the <paramref name="tree"/>; null for any other session.
    /// </param>
    /// <param name="continued">
    /// The kept folders of the parts a carry-on's line continues (SESSCTX1b, the session-context design §3.1 item 5): its brief names
    /// their files, so it may read exactly those folders too. None for every other start.
    /// </param>
    /// <param name="heavy">
    /// What its run's checks hand it (FABRIC-PIPELINE1b, <see cref="HeavyTold.Hand"/>): under <c>light</c>, each heavy gate's declared
    /// command denied exactly beside the person's rules, and auto mode's classifier told the heavy gates are the verification's. None for
    /// a session that works on no quest's repository, and nothing where the harness takes no rules.
    /// </param>
    internal (string? File, object? Meta, HandedSection Handed) HandRules(
        ISessionAdapter adapter, ProcessStartInfo info, string sessionId, string? workspace, string? repository,
        string tree, string? kept, IReadOnlyList<string>? job = null, AcrossReach? across = null, string? said = null,
        bool opinion = false, bool helper = false, IReadOnlyList<string>? plugins = null, string? brainstorm = null,
        IReadOnlyList<string>? continued = null, HeavyHanded? heavy = null, bool partner = false)
    {
        // A harness a Claude Code rule means nothing to is handed nothing, and no file is written.
        if (!adapter.TakesSettings) return (null, null, RulesHanded(takes: false, PermissionFile.Empty, RuleLists.Empty, workspace, repository));

        var rules = PermissionRules.Load(home);
        var composed = PermissionRules.Compose(rules, workspace, repository);
        // GITAUTH1f (§6.8): `no-push`'s forms follow the session's git access, and the classifier is told as git access says it.
        var gitWrite = GitAccessKeys.Resolve(home, repository, workspace).Of(GitAccessKeys.Write).Value;
        composed = GitAccessPolicy.Compose(composed, gitWrite);
        var pushTold = GitAccessPolicy.HardDeny(PermissionRules.HardDeny(rules), gitWrite);
        var folders = new[] { kept, said }.Concat(continued ?? []).OfType<string>().Where(folder => folder.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var folder in folders)
        {
            composed = composed with { Allow = [.. composed.Allow, PermissionRules.ReadRule(folder)] };
        }

        if (across is not null) composed = composed.Joined(AcrossRules.Rules(across, [tree, kept ?? "", said ?? "", .. folders]));

        // 🔴 What the session's job needs, beside the person's rules (FG5): an intake's room names its tools
        // in its own settings file, and over the protocol door only what is handed at spawn counts. The
        // first real intake was refused `WebFetch` that way. A deny the person wrote still wins.
        if (job is { Count: > 0 })
        {
            composed = composed with { Allow = [.. composed.Allow, .. job.Where(rule => !composed.Allow.Contains(rule))] };
        }

        // A helper's session (MIXAGENT2a-ii) is the reviewer's shape with its own one tool.
        if (opinion || helper) composed = OpinionPermissions.Shape(composed, helper);

        // A partner's session (COAGENT1b) writes its copy and calls its reading tools and its answer: `PartnerPermissions`, the server's
        // refusals by role beside it.
        if (partner) composed = PartnerPermissions.Shape(composed);

        // A brainstorm's pass (BRAINSTORM1a2) reads its room and gives its one answer: the `brainstorm` posture, never the reviewer's.
        if (brainstorm is not null) composed = BrainstormPermissions.Shape(composed, tree, brainstorm);

        // FABRIC-PIPELINE1b (the pipeline design §3.2's third layer): under a light pipeline each heavy gate's declared command, exactly,
        // beside `no-push`, and the classifier told; a deny the person wrote still stands beside them.
        if (heavy is { Deny.Count: > 0 }) composed = composed with { Deny = [.. composed.Deny.Union(heavy.Deny, StringComparer.Ordinal)] };
        IReadOnlyList<string> heavyTold = heavy?.HardDeny is { } sentence ? [sentence] : [];

        var file = SpawnSettings.Write(
            home, sessionId, composed,
            brainstorm is not null ? [.. pushTold, BrainstormPermissions.HardDeny]
            : partner ? [.. pushTold, PartnerPermissions.HardDeny]
            : helper ? [.. pushTold, OpinionPermissions.HelperHardDeny]
            : opinion ? [.. pushTold, OpinionPermissions.HardDeny] : [.. pushTold, .. heavyTold],
            // A partner is held to its copy whatever the person's defaults say (COAGENT1b, the co-agent design §7.4).
            PermissionRules.GuardsTree(rules) || partner ? TreeGuard.For(home, tree, across?.Writes.Select(target => target.Path)) : null,
            // Which agent made each call, and each subagent's start and stop (FABRIC-C1a): the part's children bound by its lineage.
            ChildLineage.HookFor(home, sessionId));
        var handed = RulesHanded(takes: true, rules, composed, workspace, repository);
        if (adapter.Wire == SessionWire.Pipe)
        {
            adapter.HandSettings(info, file);
            return (file, null, handed);
        }

        return (file, adapter.AcpSessionMeta(file, plugins ?? []), handed);
    }

    /// <summary>
    /// The protocol-door posture a brainstorm's pass asks (BRAINSTORM1c, <see cref="BrainstormPermissions.Posture"/>): its harness's mode that
    /// changes nothing where it takes no rules; null for every other session, and for a pass whose harness a rule narrows.
    /// </summary>
    internal static string? PassPosture(SessionTarget target, ISessionAdapter adapter) =>
        target.Brainstorm is not null ? BrainstormPermissions.Posture(adapter) : null;

    /// <summary>
    /// What a partner's start hands its agent beside the instruction (COAGENT1b, the co-agent design §3.3): the rules
    /// <see cref="HandRules"/> writes for it with <c>partner</c>, its classifier's hard denials and its tree guard, which always holds it.
    /// What its effective configuration's digest is taken over (<see cref="PartnerConfigurations"/>).
    /// </summary>
    internal (RuleLists Rules, IReadOnlyList<string> HardDeny, bool Guarded) PartnerHanded(string? workspace, string? repository)
    {
        var rules = PermissionRules.Load(home);
        return (PartnerPermissions.Shape(PermissionRules.Compose(rules, workspace, repository)),
            [.. PermissionRules.HardDeny(rules), PartnerPermissions.HardDeny], true);
    }

    /// <summary>
    /// The permission rules a session was handed beside its instruction (CONTEXT1, D143 point 1), kept in its instruction's
    /// account: how many in each list, the scopes they were composed for, the hard denials and the tree guard. The rules file
    /// goes when the run does (TRACE1), and the account keeps what it held by count past that.
    /// </summary>
    /// <param name="takes">Whether the session's agent takes Daoris's rules at all (<see cref="ISessionAdapter.TakesSettings"/>).</param>
    /// <param name="handed">The rules as handed: the composition, with any read of the quest's files, reach across and job's own.</param>
    internal static HandedSection RulesHanded(bool takes, PermissionFile file, RuleLists handed, string? workspace, string? repository)
    {
        const string Name = HandedSections.Rules;
        const string Source = HandedSources.Permissions;
        if (!takes)
        {
            return new HandedSection(Name, Source, "the permission rules: none handed, since its agent takes no rules from Daoris")
            {
                Shown = 0,
                None = HandedNones.Agent,
            };
        }

        var count = handed.Allow.Count + handed.Ask.Count + handed.Deny.Count;
        var hard = PermissionRules.HardDeny(file).Count;
        var guard = PermissionRules.GuardsTree(file);
        if (count == 0 && hard == 0 && !guard)
        {
            return new HandedSection(Name, Source, "the permission rules: none handed, since none were composed for it")
            {
                Shown = 0,
                None = HandedNones.Empty,
            };
        }

        var scope = $"workspace `{RemoteTarget.Workspace(workspace)}`" + (repository is { Length: > 0 } named ? $" and repository `{named}`" : "");
        var beside = new List<string>();
        if (hard > 0) beside.Add(InstructionAccounting.Plural(hard, "hard denial"));
        if (guard) beside.Add("the tree guard");
        return new HandedSection(
            Name, Source,
            $"the permission rules: {count} handed beside it ({handed.Allow.Count} allow, {handed.Ask.Count} ask, {handed.Deny.Count} deny), "
            + $"composed from this machine's rules for {scope}" + (beside.Count > 0 ? "; " + InstructionAccounting.Joined(beside) : ""))
        {
            Shown = count,
            Cuts = file.Problem is null
                ? null
                : [new HandedCut(HandedCuts.RulesUnread, "this machine's rules file could not be read, so only the defaults were handed")],
        };
    }

    /// <summary>
    /// 🔴 A turn the door refused for an account's limit (TOOL4d, D125 §2, §5.2), read from the door's failure by the
    /// adapter's table and never from the transcript: the account the session ran as cools until the reset the agent
    /// named, the record says <c>limit</c> — which is never a strike — and its note says until when, naming no account.
    /// </summary>
    /// <remarks>
    /// <para>Only a <c>failed</c> conclusion: a refused turn after the work reached its close or its wait ended as that, the
    /// person's stop is theirs, and a timeout carries no door failure. A failure no table recognises is a failure as
    /// today, and the strikes bound it.</para>
    /// <para>🔴 <b>The note carries the facts, never the agent's sentence</b> (AGT3c): the sentence names its reset in the zone
    /// of the machine it was said on, and in English, and the note travels to every machine. The cooling line carries the reset
    /// as a moment each reader words in its own zone and language; the sentence stays in the transcript and the conversation,
    /// the raw view, which stay here.</para>
    /// </remarks>
    /// <param name="turnFailed">What the protocol door said refusing the turn (ACPEND1), or null.</param>
    /// <param name="used">The context the door reported, for the log; null where it reported none.</param>
    /// <param name="byCode">A limit the reader read by its code with no sentence (AGENTERRORS1a, §3.1): cooled for D125's hour.</param>
    internal (SessionConclusion Conclusion, bool Limit) AccountLimited(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, string sessionId,
        AcpUsage? used, LimitSeen? byCode = null)
    {
        if (conclusion.State != "failed" || turnFailed is not { Length: > 0 }) return (conclusion, false);

        (CoolingEntry Entry, LimitSeen Seen)? limited;
        // The cool-offs that ended lately, read before this one replaces its account's (LIMITRESUME1b): a reset that did not lift it.
        var now = Context.Harnesses.Clock();
        IReadOnlyList<CoolingEntry> ended;
        try { ended = AccountCooling.Offered(home, now); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { ended = []; }

        try
        {
            limited = Context.Harnesses.Limited(adapter.Name, selection.Profile, turnFailed, sessionId, config.CoolOff, byCode);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The limit could not be read for a disk that would not answer: the record still says the agent's words. A cool-off the
            // file would not take is no such case: the roster holds it in memory, and the end is limited (LIMITRESUME1b).
            return (conclusion, false);
        }

        if (limited is not { } read) return (conclusion, false);

        var (entry, seen) = read;
        service.AccountSaid(AccountLine.Limited(sessionId, adapter.Name, selection.Profile, seen, Context.Events.TurnsEnded(sessionId) + 1, used?.Used));
        // LIMITRESUME1b (the design §3.1, round 3's Codex 10): kept in memory only, said once in the machine log for this entry.
        if (AccountCooling.Unwritten(home, entry.Agent, entry.Account)) service.AccountSaid(AccountLine.CoolingUnkept(sessionId, adapter.Name, selection.Profile, seen));
        var noted = conclusion.Unsaid(turnFailed).Then(" ", CoolingWords.NoteOf(entry, Context.Harnesses.Zone));

        // 🔴 LIMITRESUME1b (the design §3.3's bound): an attempt refused with the same kind of limit within the margin after its account's
        // cool-off ended met a reset that did not lift it (an admin's spend limit can outlast the window the sentence names). Said once,
        // on this end, and in the machine log; it still waits for the next cool-off and never parks.
        if (LimitResume.NotLifted(ended, entry, now) is { } notLifted)
        {
            noted = noted.Then(" ", notLifted);
            service.AccountSaid(AccountLine.NotLifted(sessionId, adapter.Name, selection.Profile, seen));
        }

        return (noted, true);
    }

    /// <summary>
    /// What a session's door says about the windows of the account it runs as (TOOL6c, D130 §5.2), kept by the roster as the
    /// door carries it, for the next start's walk. A reading not kept costs a start's ranking, never the session.
    /// </summary>
    /// <remarks>
    /// Asked once by every start that holds a session (a driven one, an intake, a carry-on, an opinion's or a helper's pass), where
    /// its account is chosen: so the session's turns are bound to that account in the usage history here (USAGECAP1a2).
    /// </remarks>
    internal Action<JsonElement> Said(ISessionAdapter adapter, HarnessSelection selection, string sessionId)
    {
        UsageTurns.Bound(home, Context.Events, sessionId, TurnBinding.Of(adapter, selection.Profile));
        return info =>
        {
            try
            {
                Context.Harnesses.Said(adapter.Name, selection.Profile, info, sessionId);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Nothing kept is nothing said: the walk reads the account as unknown, as it did before the door spoke.
            }
        };
    }

    /// <summary>
    /// 🔴 A credential its provider refused is read from what the harness itself said of its ending (AGT3b, AGT3c), and the
    /// account is held so no further session sits through the same minutes of retries.
    /// </summary>
    /// <remarks>
    /// <para>Never from words its agent could write (AGT3c, D125 point 1's rule for a limit): an agent whose output ends quoting
    /// the phrase, reading a log or testing an error path, would hold an account its provider never refused. A door that
    /// carries only text gives nothing but its transcript, so there the exit, which only the harness sets, must say it failed
    /// (<see cref="HarnessEnding.Transcript"/>).</para>
    /// <para>And it reads signed out (ROSTER1b, D150 §5.3), kept as a reading is, so the hold outlives this process and the page
    /// says the account's state; its line is one no strike counts (<see cref="NoteCodes.AccountsOwn"/>).</para>
    /// </remarks>
    /// <param name="ended">What the harness said of its ending, by its door (<see cref="Ending"/>).</param>
    internal SessionConclusion AccountRefused(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, HarnessEnding ended)
    {
        if (conclusion.State != "failed" || adapter.Toolchain is not { Refused: { Length: > 0 } refusedWords } refusing
            || !Observation.Refused(ended, refusedWords))
        {
            return conclusion;
        }

        return conclusion.Then(" ", RefuseAccount(adapter, selection, refusing.Owner(adapter.Name)));
    }

    /// <summary>
    /// The account a refused credential holds (AGT3b) and reads signed out (ROSTER1b), named by <paramref name="owner"/>; its line for the
    /// record. What <see cref="AccountRefused"/> applies, and a resume's open refused for access applies as the reader read it (Codex's
    /// owed read of D210, finding 6).
    /// </summary>
    internal Noted RefuseAccount(ISessionAdapter adapter, HarnessSelection selection, string owner)
    {
        // The hold a later start says names the account by the person's name, read now (ACCT2b); the note, which travels and
        // whose cleaner knows only the record's own id, keeps the id.
        var named = selection.Profile is { } profile ? AccountNames.Said(AccountNames.Of(Context.Harnesses.Home, owner), profile) : null;
        var hold = RefusedAccounts.RefusedHold(owner, selection.Profile, named);
        Context.Harnesses.Refuse(adapter.Name, selection.Profile, hold.Note, hold);
        Context.Harnesses.SignedOut(adapter.Name, selection.Profile);
        return RefusedAccounts.RefusedNote(owner, selection.Profile);
    }

    /// <summary>
    /// 🔴 A start its agent refused for its sign-in (ROSTER1b, D150 §5.3), read from the door's failure by the adapter's table
    /// (<see cref="SignInRefusals"/>) and never from the transcript: the account it ran as reads signed out, kept under the home
    /// with its time as a reading is, so the next start walks past it as it walks past any account read signed out (TOOL6g),
    /// and a start held on it names it and its sign-in. The note says why it ended, by a line no strike counts
    /// (<see cref="NoteCodes.AccountsOwn"/>): the fault is the account's, never the quest's.
    /// </summary>
    /// <remarks>
    /// <para>Seen on the install (2026-10-04): every account read <i>never read</i> after an update, so the rotation started a
    /// quest on an account nobody had signed in to, four times in two minutes, and the third failure parked it.</para>
    /// <para>The tool's own sign-in is never asked by a walk (TOOL6g), so a reading alone would start it into the same refusal at
    /// every look: it is held as AGT3b holds a refused one, until a person looks again.</para>
    /// </remarks>
    /// <param name="turnFailed">What the protocol door said refusing the call (ACPEND1), or null.</param>
    /// <summary>
    /// 🔴 The pipe door's refusal of the model its actor asked (D205 §3.3's pipe exception, §3.5(a), MODELROLE1c3), read from row a's
    /// measured words on a silent turn (<see cref="HarnessEnding.ModelRefused"/>): the one refusal after a prompt was handed that is no
    /// failure of the quest's. The account's <c>refused</c> reading for that model is kept, so the next start's walk passes the account
    /// without spawning, and the part ends with the account's line, which no strike counts (<see cref="NoteCodes.AccountsOwn"/>), naming
    /// the agent and the model but no account, since the note travels.
    /// </summary>
    /// <param name="model">The model its actor's choice named (<see cref="ActorAsked.NamedModel"/>); null keeps the conclusion, since a
    /// model the person's own command or environment named is no choice a reading passes.</param>
    internal SessionConclusion ModelRefused(
        SessionConclusion conclusion, string agent, HarnessSelection selection, string? model, HarnessEnding ended, string sessionId)
    {
        if (conclusion.State != "failed" || !ended.ModelRefused || model is not { Length: > 0 }) return conclusion;

        if (AgentOffers.Refused(home, agent, selection.Version, selection.Profile, model, DateTimeOffset.UtcNow) is { } unkept)
        {
            Log?.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
        }

        Log?.Write("info", "actor.refused", [("session", sessionId), ("agent", agent), ("model", model), ("code", AgentOffers.RefusedCode)]);
        return conclusion.Then(" ", Noted.Of(
            NoteCodes.AccountRefusedModel,
            $"`{agent}` refused `{model}` on the account it ran on, so that account is passed for that model until the agent offers it again. "
            + "It is not counted as a failed session.",
            ("agent", agent), ("model", model)));
    }

    /// <param name="ended">The run's ending: a refusal its door's own code reads as access is the account's sign-in, whatever its words (LIMITPICK1).</param>
    internal SessionConclusion AccountSignedOut(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, HarnessEnding? ended = null) =>
        conclusion.State == "failed"
        && RefusedAccounts.SignInRefused(Context.Harnesses, adapter, selection.Profile, turnFailed, AccessByCode(adapter, ended)) is { } line
            ? conclusion.Then(" ", line)
            : conclusion;

    /// <summary>
    /// Whether the refusal a run ended on reads access by a code or kind its door declares (D210 point 5): the one reader's, its words read
    /// apart, since a credential its provider refused by its words is AGT3b's (<see cref="AccountRefused"/>).
    /// </summary>
    private bool AccessByCode(ISessionAdapter adapter, HarnessEnding? ended) =>
        ended?.Refusal is { } refused
        && AgentErrors.Read(Context.Adapters, adapter, DoorFailure.OfPrompt(refused), Context.Harnesses.Clock(), Context.Harnesses.Zone)
            is { Kind: AgentErrorKind.Access } read
        && !read.By.Contains(":words:", StringComparison.Ordinal);

    /// <summary>
    /// What a start's landing is named for, as its session is told it (WSR5, LAND2c): a chain's step lands on its chain's branch,
    /// named for the chain's first quest, so it is told that branch, which its done moves on. A quest that follows nothing is its
    /// own; a service that cannot walk the chain leaves the step its own name, since a start never waits on a sentence.
    /// </summary>
    /// <param name="find">One quest as the service answers it, closed ones included.</param>
    internal static async Task<LandingSubject> LandsAsAsync(
        string session, QuestView quest, Func<string, Task<QuestView?>> find, CancellationToken ct)
    {
        var own = LandingSubject.Of(session, quest);
        if (quest.Parent is not { Length: > 0 }) return own;
        try
        {
            var chain = await LandingRules.SubjectAsync(session, quest.Id, find, opening: null).ConfigureAwait(false);
            // A service that no longer answers for the step itself names it with no title: its own is truer.
            return chain.Title is null ? own : chain;
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return own;
        }
    }

    /// <summary>
    /// What a further part of a session is handed of the parts before it (SESSONE1d): each one's record, its tree's name where the
    /// tree stands and its branch now, or the branch its work landed on where the tree is gone; and how its own tree stands to
    /// theirs. Only the newest the instruction names are read from git; a running part's note is its opening, so none is said.
    /// </summary>
    /// <param name="joinedIn">The tree it went into, standing; null where it did not.</param>
    /// <param name="entryTree">The tree the planner found free, gone or not; null where it found none.</param>
    private async Task<SessionParts> PartsAsync(
        EntryParts entry, string? joinedIn, string? entryTree, TreeOpened? opened, string repository, CancellationToken ct)
    {
        var earlier = new List<EarlierPart>();
        foreach (var part in entry.Parts)
        {
            var named = earlier.Count < TargetPrompt.PartsShown;
            var tree = named && part.Tree is { Length: > 0 } path && Directory.Exists(path) ? path : null;
            var branch = tree is not null
                ? await Context.Trees.BranchNowAsync(tree, ct).ConfigureAwait(false) ?? $"daoris/{TreeName(tree)}"
                : named && Context.Trees.Recorded.Landing(part.Session) is { GoneAt: null } landing
                        && string.Equals(landing.Repository, repository, StringComparison.OrdinalIgnoreCase)
                    ? landing.Branch
                    : null;
            earlier.Add(new EarlierPart(part.Session, part.Quest, part.State)
            {
                Note = part.Running ? null : part.Note,
                Tree = tree is null ? null : TreeName(tree),
                Branch = branch,
            });
        }

        var regrown = entryTree is not null && joinedIn is null;
        return new SessionParts(earlier)
        {
            Into = joinedIn is not null ? entry.Into?.Session : null,
            HeldBy = entry.HeldBy,
            Gone = regrown ? entry.Into?.Session : null,
            GrewFrom = regrown ? opened?.GrewFrom : null,
        };

        static string TreeName(string tree) => Path.GetFileName(tree.TrimEnd('/', '\\'));
    }

    /// <summary>
    /// The creating sentence a start's record carries while it runs (D51 rule 4), each line with its code (LANG1a, the language
    /// design §4 rows 25–36): its tree's opening; a resume once its question closed, and in which tree; or a carry-on, why,
    /// whether on another account, in which tree, and why the person's words did not go on in their own session. Null where
    /// a start in the root says nothing.
    /// </summary>
    /// <param name="resumes">The record it carries on or resumes after, or null.</param>
    /// <param name="written">The record the person's words were written to, as the look read it, or null.</param>
    /// <param name="elsewhere">Whether it runs on another account than the record it carries on.</param>
    /// <param name="handedOver">The quest was handed over from <paramref name="resumes"/> (MIXAGENT1a-ii): said by the agent it came from.</param>
    /// <param name="walked">The account the hand-over named could not run it, and another account of the named agent did.</param>
    /// <param name="joinedSession">The earlier part's session whose free tree this start went into (SESSONE1g), or null.</param>
    /// <param name="broughtForward">It carries on a stand-down whose lost take the person brought forward (SYNCMERGE1b): said as that.</param>
    /// <param name="carriesWork">It carries an open quest on in the tree its failed session committed work in (RETRYWORK1): said as that.</param>
    internal static Noted? StartingNote(
        TreeOpened? opened, QuestView quest, QuestView? answered, string? resumedIn, bool carryingOn,
        PriorSession? resumes, PriorSession? written, bool elsewhere, ContinueReason? fellBack, bool handedOver = false, bool walked = false,
        string? joinedSession = null, bool broughtForward = false, bool carriesWork = false)
    {
        if (opened is not null) return opened.Opening ?? Noted.From(opened.Sentence, null);

        if (answered is not null)
        {
            var resumed = Noted.Of(
                NoteCodes.StartedResumesAnswered, $"resumes `#{quest.Id}` now that `#{answered.Id}` is answered",
                ("quest", quest.Id), ("answered", answered.Id));
            return resumedIn is null
                ? resumed.Then(".")
                : resumed.Then(", ", Noted.Of(
                    NoteCodes.StartedInAskingTree, $"in the tree session `{resumes!.Session}` asked from.", ("session", resumes.Session)));
        }

        // A further part gone into a free tree of its entry (SESSONE1g, D167): said, as a start in the root is not. A part whose
        // tree was removed grows again, and its opening (`started.tree`) already says whose tree went.
        if (!carryingOn)
        {
            return joinedSession is null
                ? null
                : Noted.Of(
                    NoteCodes.StartedJoined,
                    $"goes into the tree of session `{joinedSession}`, which holds the earlier part of this work.",
                    ("session", joinedSession));
        }

        var session = resumes!.Session;
        (string Name, object? Value)[] facts = [("quest", quest.Id), ("session", session)];
        var carried = resumes.PersonSaid
            ? (written is { Parked: false, Said: not null }
                // Words to a session that had ended (MSG1b): no answer, and an open quest is started.
                ? quest.Status == "Open"
                    ? Noted.Of(NoteCodes.StartedWordsOpen, $"starts `#{quest.Id}` with your words to session `{session}`", facts)
                    : Noted.Of(NoteCodes.StartedWordsTaken, $"carries `#{quest.Id}` on with your words to session `{session}`", facts)
                : Noted.Of(NoteCodes.StartedAnswer, $"carries `#{quest.Id}` on with your answer to session `{session}`", facts))
            : handedOver
                // The agent it came from, as its record names it; never an account's name or a path (the note travels).
                ? Noted.Of(
                    NoteCodes.StartedHandedOver,
                    $"carries `#{quest.Id}` on: session `{session}` was handed over from {resumes.Adapter ?? "its agent"}",
                    [.. facts, ("agent", resumes.Adapter ?? "its agent")])
            : broughtForward
                ? Noted.Of(
                    NoteCodes.StartedBroughtForward,
                    $"carries `#{quest.Id}` on: you brought the work of session `{session}` forward after its take lost", facts)
            // STEWARDROLE1b (D209 §5.1): the steward's stop released, said as the steward's in the resumed brief, never as the person's.
            : resumes.StewardStopped
                ? Noted.Of(NoteCodes.StartedReleasedSteward, $"carries `#{quest.Id}` on: the steward stopped session `{session}`, and it was released", facts)
            : resumes.PersonStopped
                ? Noted.Of(NoteCodes.StartedReleased, $"carries `#{quest.Id}` on: you stopped session `{session}`, and released it", facts)
            // RETRYWORK1: an open quest carried on in the tree its failed session committed work in before it took the quest.
            : carriesWork
                ? Noted.Of(NoteCodes.StartedCarriedWork, $"carries `#{quest.Id}` on with the work session `{session}` committed before it failed", facts)
                : Noted.Of(NoteCodes.StartedCutOff, $"carries `#{quest.Id}` on after session `{session}` was cut off", facts);

        // 🔴 Which account, never (TOOL4f, D125 §3.6): the note travels, and its scrubber elides only the record's own
        // account, so another account's name here would reach a teammate.
        if (handedOver && walked)
        {
            carried = carried.Then(", ", Noted.Of(NoteCodes.StartedHandedWalked, "the account it named could not run it, so another of its agent's did"));
        }
        else if (elsewhere && !handedOver)
        {
            carried = carried.Then(", ", Noted.Of(NoteCodes.StartedOtherAccount, "on another account"));
        }
        carried = resumedIn is null ? carried.Then(".") : carried.Then(", ", Noted.Of(NoteCodes.StartedSameTree, "in the tree it worked in."));

        // Why the answer did not go on in its own conversation (ANSWER1a, D131 §2).
        return fellBack is null ? carried : carried.Then(" ", Continuations.CarriedOn(fellBack, resumes.Adapter));
    }

    /// <summary>
    /// The judgement a turn that ends on the session's own background work is asked (BGWAIT1, BGWAIT1b), one for a start and a
    /// resumed run: the quest read again, by the conclusion's own rule. Null for a resumed run whose quest was closed as the look
    /// planned it, which never waits (D83, D105 §3, D137 §2.3). A start asks the ledger whether it took its quest; a resumed
    /// run carried on a take of this machine's, so it asks nothing.
    /// </summary>
    internal static Func<CancellationToken, Task<bool>>? WaitsOnBackground(
        ServiceClient service, QuestView planned, string sessionId, bool resumed)
    {
        if (resumed && planned.Status is not ("Open" or "Taken")) return null;

        // DONEEND1: read again at every look while the session waits. A take once read stays this session's (its record keeps it), so
        // the sessions are read only until it is.
        var took = false;
        return async token =>
        {
            var now = await service.FindQuestAsync(planned.Id, token).ConfigureAwait(false);
            var status = now?.Status ?? "Open";
            took = took || (!resumed && status == "Taken" && await service.TookAsync(sessionId, token).ConfigureAwait(false));
            return Observation.Parks(status, planned.Awaits, now?.Awaits, resumed, took: took && status == "Taken");
        };
    }

    /// <summary>How many times a went-on run is carried on (LANDBACK1 part 3): the person's words were its turn, so one more.</summary>
    internal const int WentOnCarryBound = 1;

    /// <summary>
    /// The look a turn's end takes of a quest that may have closed with uncommitted work in its tree (LANDBACK1 part 2, TURNWAIT1): the
    /// quest read again, and, where it is done or submitted and the person has not stopped the session, its tree (<see cref="TurnCarries"/>).
    /// A resumed run whose quest was closed as the look planned it (a went-on run, part 3) is carried on once, not <see cref="TurnCarry.Bound"/>
    /// times, and not at all where it is an Accept's own carry-on (null: the Accept's count bounds it). A parked quest (a question for the
    /// person), an ask-and-wait (D79), a stand-down and a failed exit are none of them done or submitted, so none is carried on.
    /// </summary>
    /// <param name="stopped">Whether the person (or the driver) stopped the session, read at each look and again where the words are written.</param>
    /// <param name="accepted">Whether the session's kept Accept (<c>CarriedAccepts</c>) is the cause of this resume, read as the run is built.</param>
    internal static Func<CancellationToken, Task<TurnCarry>>? CarriesOn(
        ServiceClient service, QuestView planned, string tree, bool resumed, Func<bool> stopped, bool accepted = false)
    {
        // A went-on run (a resume of a quest closed as the look planned it, D83) is carried on once, a turn's end twice; an Accept's carry-on
        // is the Accept's, bounded by its own count, so the run reads null for it and the session is never carried on by two counters.
        var wentOn = resumed && planned.Status is not ("Open" or "Taken");
        if (wentOn && accepted) return null;
        var limit = wentOn ? WentOnCarryBound : TurnCarry.Bound;
        return async token =>
        {
            if (stopped()) return TurnCarry.None;
            var now = await service.FindQuestAsync(planned.Id, token).ConfigureAwait(false);
            return await TurnCarries.LookAsync(now is { Status: "Done" } || QuestResults.Submitted(now), stopped, tree, token, limit).ConfigureAwait(false);
        };
    }

    /// <summary>Where a run's capture failed once the run was ended before awaiting it (ANSWER2): the machine log's <c>where</c>.</summary>
    internal const string AbandonedRun = "a session's run, ended as its record would not move to working";

    /// <summary>
    /// How long a given-up run's capture is waited for before it is left to finish on its own, still observed: its process was
    /// just ended, so its output closes at once, and a capture still open after this is stuck, not slow.
    /// </summary>
    internal static readonly TimeSpan AbandonedWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A run's capture once the run gave up before awaiting it (ANSWER2), as when its record will not move to working: the
    /// process ended, then the capture observed, and a failure there written with its reason, to the machine log as an
    /// <c>error</c> naming its place and to the session's console. Left alone it reached the finalizer, an unobserved task's
    /// AggregateException whose only place was <i>an unobserved task</i>, beside each session the double move failed.
    /// </summary>
    /// <remarks>
    /// Waited for within <see cref="AbandonedWait"/>, as the ordinary path awaits a capture once its process exited; past it the
    /// run goes on to say how it ended, and the capture is still observed whenever it finishes. Never throws.
    /// </remarks>
    /// <param name="exitRead">
    /// Told once the ended process's exit is read within <see cref="AbandonedWait"/>, before the capture is waited for
    /// (COWORKVIEW1a): where its <c>end</c> is written. Never told past the bound.
    /// </param>
    internal static async Task AbandonedAsync(
        Process? process, Task capture, string sessionId, MachineLog? log, SessionOutput? output, TimeSpan? within = null, Action? exitRead = null)
    {
        if (process is not null)
        {
            SessionProcesses.EndIfRunning(process);
            if (exitRead is not null && await ExitReadAsync(process, AbandonedWait).ConfigureAwait(false)) exitRead();
        }

        var observed = capture.ContinueWith(
            done =>
            {
                if (done.Exception is not { } failed) return;
                var error = failed.InnerExceptions.Count == 1 ? failed.InnerExceptions[0] : failed;
                log?.Failed(AbandonedRun, error);
                output?.Append(
                    sessionId,
                    $"— the run ended here, since its record would not move, failed as its output was read: {error.GetType().Name}: {error.Message}");
            },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            await observed.WaitAsync(within ?? AbandonedWait).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Still observed: the continuation above writes its failure whenever the capture ends.
        }
    }

    /// <summary>
    /// How long a run's capture is waited for once its root exited, G (SESSWATCH1a, D200, the session-watch design §1.5): the door's
    /// drain and <c>EndedLingeringAsync</c>'s ten seconds fit inside it. Settable for a test, never read from <c>driver.json</c>.
    /// </summary>
    internal TimeSpan CaptureBound { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 🔴 State E (SESSWATCH1a, D200, the session-watch design §2): a run whose root has exited is concluded by what it said, within
    /// <see cref="CaptureBound"/>. Past it with its capture still held open, by something its agent started holding the harness's
    /// output, everything left in the run's job is ended, said as a note line (<c>ended.gone</c>), and the capture drains; one still
    /// open after <see cref="AbandonedWait"/> is abandoned, still observed. Never a cut-off: the caller concludes by the exit, so a
    /// session that finished its turn and left a dev server holding its pipe (ORPHAN1's case) ends as it finished.
    /// </summary>
    /// <returns>Whether the capture finished, so what it read may be awaited; false where it was abandoned.</returns>
    internal async Task<bool> CaptureWithinAsync(Task capture, string sessionId)
    {
        if (await Task.WhenAny(capture, Task.Delay(CaptureBound)).ConfigureAwait(false) == capture)
        {
            await capture.ConfigureAwait(false);
            return true;
        }

        Context.Processes.JobOf(sessionId)?.Terminate();
        var gone = Observation.Gone;
        output?.Append(sessionId, $"— {gone.Note}");
        Context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = gone.Note, Parts = gone.Parts }, say: null);
        Log?.Info("session.gone", ("session", sessionId), ("bound", (int)CaptureBound.TotalSeconds));
        if (await Task.WhenAny(capture, Task.Delay(AbandonedWait)).ConfigureAwait(false) == capture)
        {
            await capture.ConfigureAwait(false);
            return true;
        }

        await AbandonedAsync(null, capture, sessionId, Log, output, TimeSpan.Zero).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Exit code, or null when a pass's own minutes ran out and killed it. Either way the tree dies with it — a pass's bound
    /// AND a driver shutdown both end the process, because an orphaned agent session working a quest
    /// nobody is observing is the one thing worse than a failed one.
    /// </summary>
    /// <remarks>
    /// 🔴 No working session is ended on time (SESSWATCH1a, D200): a driven session, an answered park's resumed run, a native go-on and
    /// an intake wait for their exit with no bound, and the watcher looks in on one gone quiet. Only a pass with minutes of its own (a
    /// helper's, a reviewer's, a partner's, a brainstorm's turn) passes them, and the tests' one backstop (<see cref="DriverConfig.TestBound"/>)
    /// rides the same parameter.
    /// </remarks>
    /// <param name="minutes">A pass's own minutes (XAGENT1d); null bounds nothing but the tests' backstop.</param>
    /// <param name="timingOut">
    /// Told the bound, in minutes, when the bound ends the process, before it is killed (BGWAIT2): what lets the protocol door
    /// say the stream's end that follows as the bound's. Never told for a shutdown.
    /// </param>
    /// <param name="quiet">
    /// The watcher's end of the run (SESSWATCH1a): a headless <c>--quiet-end</c>, its own cause, never a registry stop. Its exit is null
    /// as a bound's is, and the conclusion reads the watcher's minutes.
    /// </param>
    /// <param name="quietEnding">Told before the watcher's kill, as <paramref name="timingOut"/> is before a bound's (BGWAIT2).</param>
    internal async Task<int?> WaitAsync(
        Process process, CancellationToken ct, int? minutes = null, Action<int>? timingOut = null, CancellationToken quiet = default,
        Action? quietEnding = null)
    {
        var bound = minutes ?? config.TestBound;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, quiet);
        if (bound is { } within) timeout.CancelAfter(TimeSpan.FromMinutes(within));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            // Said before the kill, since the stream's end can reach the door's reader before anything after it here runs.
            if (!ct.IsCancellationRequested)
            {
                if (quiet.IsCancellationRequested) quietEnding?.Invoke();
                else timingOut?.Invoke(bound ?? 0);
            }
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (ct.IsCancellationRequested) throw; // shutdown: the caller records "stopped"
            return null; // timeout: the caller records "failed"
        }
    }

    /// <summary>
    /// The watcher's look (SESSWATCH1a, D200, the session-watch design §2): its steps this look, each said as a line, which is the
    /// headless host's row, since it has no <i>What needs you</i>; the window's row is <c>NeedsAPerson</c>'s.
    /// </summary>
    /// <param name="said">The look's classing, each step's line classed as it is made (NOTIFY2h2).</param>
    internal IReadOnlyList<string> LookIn(LookSaid said)
    {
        var watch = Watch;
        watch.Log = Log;
        var lines = new LookLines(said, "watch");
        var steps = watch.Look(
            id => Context.Processes.JobOf(id) is { } job ? new JobRead(job.Holds, job.Cpu) : null, TimeSpan.FromSeconds(config.PollSeconds),
            (id, tree) => HeavyWaitOf(id, tree));
        foreach (var step in steps)
        {
            var who = step.Kind == SessionWatch.Intake ? $"intake {step.Session}" : $"session {step.Session}";
            switch (step)
            {
                case { Step: SessionWatch.Marked }:
                    lines.Progress($"watch  {who} is quiet: nothing heard from it for {step.Minutes} minutes, and nothing it runs uses the machine; "
                        + "Daoris looked in and ended nothing.");
                    break;
                case { Step: SessionWatch.Brought }:
                    var row = watch.RowOf(step.Session);
                    lines.Wait($"watch  {who} has said nothing for {step.Minutes} minutes"
                        + (row is { Busy: true } ? ", while something it runs keeps the machine busy" : ", and nothing it runs uses the machine")
                        + ": it needs you. Daoris ends nothing; stop it or leave it running from Daoris's window, or end this run with Ctrl-C."
                        + (watch.EndAfter is null ? " A headless run nobody watches takes --quiet-end <minutes>." : ""));
                    break;
                case { Step: SessionWatch.Ended }:
                    lines.Progress($"watch  {who} said nothing for {step.Minutes} minutes: --quiet-end ends its run, and its work goes on in the same tree.");
                    break;
                case { Outcome: "heard" or "busy" or "moving" or "waits" }:
                    lines.Progress($"watch  {who} is no longer quiet ({step.Outcome}).");
                    break;
            }
        }

        return lines.Said;
    }

    /// <summary>
    /// Whether a session waits for its repository's heavy-run slot (HEAVYSLOT1, the session-watch design §1.3), from the slot's records:
    /// the holder's session, or null for a run from a terminal; null where it waits for none, or its tree is in no checkout.
    /// </summary>
    private HeavyWait? HeavyWaitOf(string session, string? tree)
    {
        // HEAVYQUEUE1: a run asking for a place in the machine's admission waits for the machine, as one waiting for the slot waits for a
        // run: it names no session to help, and is brought to the person at three times the bound all the same.
        if (MachineAdmission.Look(home).Waits.Any(wait => string.Equals(wait.Session, session, StringComparison.OrdinalIgnoreCase))) return new HeavyWait(null);
        if (tree is null || HeavySlot.KeyOf(tree) is not { } key) return null;
        var look = HeavySlot.Look(home, key);
        return look.Waiters.Any(waiter => string.Equals(waiter.Session, session, StringComparison.OrdinalIgnoreCase))
            ? new HeavyWait(look.Holder?.Session)
            : null;
    }

    /// <summary>The watcher (SESSWATCH1a): every running driven session's, shared by every look's driver through the record.</summary>
    internal SessionWatch Watch => SessionWatch.Of(Context.Events);

    /// <summary>
    /// A capture's stream, stamping its session heard at each line read (SESSWATCH1a, the session-watch design §1.2): every line and
    /// frame, a subagent's and stderr's among them, the time alone. A session the watcher does not watch is stamped nothing.
    /// </summary>
    internal TextReader Hearing(TextReader reader, string sessionId) => Watch.Hearing(reader, sessionId);

    /// <summary>
    /// How long an agent whose session ended with its own background work still running is given to exit once its stdin closed,
    /// before its tree is ended (DONEEND1). Its session has said all it will; the work is what can hold it.
    /// </summary>
    internal static readonly TimeSpan LingerBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often a driven session that waits on its own background work reads its quest again (DONEEND1, D83's note): every fourth
    /// tick of the driver's own, a minute at its default. Each look reads what a tick reads (the board, and the sessions until this
    /// session's take is read), so a waiting session costs a quarter of a tick, and its run ends within a minute of its quest's close.
    /// </summary>
    internal TimeSpan BackgroundLook => TimeSpan.FromSeconds(4 * Math.Max(1, config.PollSeconds));

    /// <summary>
    /// Wait up to <paramref name="bound"/> for an agent to exit; past it, end its process tree, saying so to the run first (DONEEND1).
    /// True where its tree was ended here.
    /// </summary>
    internal static async Task<bool> EndedLingeringAsync(Process process, TimeSpan bound, AcpEnding? ending)
    {
        using var within = new CancellationTokenSource(bound);
        try
        {
            await process.WaitForExitAsync(within.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            ending?.Lingering();
            SessionProcesses.EndIfRunning(process);
            return true;
        }
    }

    /// <summary>
    /// The knowledge server this session is offered (ACP4) — located per run, because a machine can
    /// gain the host between ticks — carrying who the session is, where its rules live, and an intake's
    /// ask when it is one.
    /// </summary>
    /// <param name="proc">The lineage token of the process this entry is composed for, on the protocol door alone.</param>
    internal AcpMcpServer? Connector(string sessionId, IReadOnlyDictionary<string, string>? scope, string? proc = null) =>
        KnowledgeConnector.Offer(
            Variable, AppContext.BaseDirectory,
            KnowledgeConnector.SessionScope(home, sessionId, scope, proc, KnowledgeConnector.KeptRole(Context.Conversations.RoleOf, sessionId)));

    /// <summary>
    /// A process's lineage token (COWORKVIEW1a): issued, its <c>start</c> written to the session's lineage, and set anew on the
    /// spawn's environment, all before the spawn.
    /// </summary>
    /// <param name="moved">The token its run's move issued (a resume, a chat's go-on), whose <c>start</c> is written already.</param>
    internal static string Bracketed(string home, string sessionId, ProcessStartInfo info, string? moved = null)
    {
        var proc = moved ?? ChildLineage.Opened(home, sessionId);
        info.Environment[ChildMarks.LineageProcVariable] = proc;
        return proc;
    }

    /// <summary>The spawn under <paramref name="proc"/>: its process handed the token, or the token let go where it made none.</summary>
    internal static async Task<Process?> Spawned(string proc, Func<Task<Process?>> spawn)
    {
        var tokens = LineageTokens.Current;
        Process? process;
        try
        {
            process = await spawn().ConfigureAwait(false);
        }
        catch
        {
            tokens.Unspawned(proc);
            throw;
        }

        if (process is null) tokens.Unspawned(proc);
        else tokens.Hand(proc, process);
        return process;
    }

    /// <summary>A process's end once its exit was read and its capture drained: clean, or killed, stopped or failed.</summary>
    internal void Ended(string sessionId, string proc, int? exitCode) =>
        ChildLineage.End(home, sessionId, clean: exitCode == 0 && Context.Processes.StopMade(sessionId) is null, DateTimeOffset.UtcNow, proc);

    /// <summary>
    /// Loop's wait on a bracketed process (COWORKVIEW1a): the exit it read told to the token set, which may not watch that process;
    /// where the wait throws as the loop closes, its kill having read the exit, the process's end written unclean before the throw
    /// leaves the run.
    /// </summary>
    internal async Task<int?> WaitedAsync(string sessionId, string proc, Task<int?> wait, CancellationToken ct)
    {
        try
        {
            var exitCode = await wait.ConfigureAwait(false);
            LineageTokens.Current.Exited(proc);
            return exitCode;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ChildLineage.Exited(home, sessionId, proc, clean: false);
            throw;
        }
    }

    /// <summary>A bracketed process's drain (COWORKVIEW1a): a capture that faults past the exit writes its end, unclean, before the fault leaves.</summary>
    internal async Task<bool> DrainedAsync(string sessionId, string proc, Task capture)
    {
        try
        {
            return await CaptureWithinAsync(capture, sessionId).ConfigureAwait(false);
        }
        catch
        {
            ChildLineage.Exited(home, sessionId, proc, clean: false);
            throw;
        }
    }

    /// <summary>Whether <paramref name="process"/>'s exit is read within <paramref name="within"/>; one that cannot say has not exited.</summary>
    internal static async Task<bool> ExitReadAsync(Process process, TimeSpan within)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(within).ConfigureAwait(false);
            return true;
        }
        catch (Exception error) when (error is TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Why the graph named for a session's take did not bind it (CLOSETAKE1), or null: a line's extra word is never worth failing the end of a
    /// run for, so a service that did not answer says nothing more than the line already did.
    /// </summary>
    private static async Task<string?> TookBindOrNullAsync(ServiceClient service, string sessionId, CancellationToken ct)
    {
        try
        {
            return await service.TookBindAsync(sessionId, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// A quest's target with the person's words on the ask its session's line is on (DRIFT1b, D133 §2; COMMS1c), read from the
    /// ask for this start: every start on the ask is handed them, a first start, a resume and a carry-on on any account alike,
    /// since a carry-on's own record holds only the session before it. A line on no ask is handed none, and words the service
    /// did not answer leave today's instruction, saying so (<see cref="AskWords.ReadAsync"/>).
    /// </summary>
    internal static async Task<SessionTarget> WithAskWordsAsync(SessionTarget target, string session, ServiceClient service, CancellationToken ct) =>
        target with { Words = await AskWords.ReadOfSessionAsync(service, session, ct).ConfigureAwait(false) };

    /// <summary>
    /// The work's session language for the quest's repository (LANG1c, D142 point 7), read from the config this tick holds
    /// for every start, whatever kind: the repository's, else its workspace's, else none, and never the window's.
    /// </summary>
    internal static SessionTarget WithLanguage(SessionTarget target, DriverConfig config, string? workspace) =>
        target with { Language = SessionLanguages.Resolve(config, target.Repository, workspace) };

    /// <summary>
    /// The go-aheads its repository's access setup asks for (AUTONOMY1c, D184 point 3), from the setup this start resolved once and
    /// keeps on its record: under <c>never</c>, full access among them, the instruction asks only what the person's words demand.
    /// </summary>
    internal static SessionTarget WithAccess(SessionTarget target, ResolvedAccess access) =>
        target with { NoGoAheads = access.Value(AccessRules.GoAheads) == AccessRules.Never };

    /// <summary>
    /// The part a carry-on goes on from, as its record says it ran (MAKERTRAIL2): its harness, that harness's maker where this build
    /// has it, and the models its door reported (MODELROLE1b). A record that names no harness is none.
    /// </summary>
    private EarlierMaker? LeftBy(PriorSession prior)
    {
        if (prior.Adapter is not { Length: > 0 } ran) return null;
        string? maker;
        try { maker = Context.Adapters.Resolve(ran).Toolchain?.Maker; }
        catch (DriverException) { maker = null; }
        return new EarlierMaker(ran, maker, prior.Ran?.Models ?? []);
    }

    /// <summary>
    /// A driven move to a new part said as it happens (SESSCTX1c, D189 point 8; the session-context design §7): a coded line on the
    /// part it left and on the new part, each on its console too, with the context the part it left stood at (its high-water, H)
    /// and the brief's characters (B); and <c>session.moved</c> in the machine log. Every driven start that continues another says it,
    /// whatever moved it, since each is a new part handed the brief.
    /// </summary>
    private void SayMoved(PriorSession prior, string sessionId, string agent, string? account, string why, ComposedBrief brief)
    {
        var context = RotatedOpening.ContextOf(usage, Context.Events, prior.Session);
        var ranOn = prior.Adapter ?? agent;
        var on = HandOverNotes.MovedOn(sessionId, agent, account, why, context, brief.Text.Length);
        var from = HandOverNotes.MovedFrom(prior.Session, ranOn, prior.Profile, why, context, brief.Text.Length);
        foreach (var (session, line) in new[] { (prior.Session, on), (sessionId, from) })
        {
            output?.Append(session, line.Note);
            Context.Events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        }

        service.AccountSaid(Continuations.Moved(sessionId, prior.Session, ranOn, agent, prior.Profile, account, why, briefed: true));
    }

    /// <summary>
    /// The session a quest's start continues, for its record's <c>from</c> link (MIXAGENT1a-iii, D163 point 4): the session the
    /// quest was handed over from, with the hand-over's why and asker (MIXAGENT1a-ii); else a session a limit cut off, carried on
    /// onto another account (TOOL4f), Daoris's own move; else the curve's move. SESSCTX1b (D189 point 4): every other carry-on
    /// names its link too, so the brief follows the line: <c>retry</c> where its resume fell back (D131 point 3), else
    /// <c>carry-on</c>, the person's where their press released or brought it forward. A first start, a resume of its own record
    /// and a fresh start carry nothing on and say none.
    /// </summary>
    /// <param name="retried">Its record's resume was tried and fell back (D131 point 3): a driven record's fallback.</param>
    /// <param name="pressed">The person's press carried it on: <i>Try again</i> after their stop, or a bring-forward.</param>
    /// <param name="handed">The quest's hand-over as held, or null.</param>
    /// <param name="carried">The record it carries on, where it carries one on, or null.</param>
    /// <param name="profile">The account it opens on.</param>
    /// <param name="agent">The agent it opens on, where a limit's move may have gone to another one (WALKAGENT1); null compares accounts alone.</param>
    /// <param name="walked">
    /// The reason of the walk's first step, where the walk chose the account (<see cref="StepSaid.Why"/>). An account over its
    /// curve passed, or the most room under it, is the usage curve's move (SWITCH1c-iii, the switch design §4); a cooling or
    /// refused account passed, a limit reached and the rest are not.
    /// </param>
    internal static ContinuedFrom? ContinuesFrom(
        HandedQuest? handed, PriorSession? carried, string? profile, string? walked = null, bool retried = false, bool pressed = false,
        string? agent = null, Action<LineCourse>? decided = null, CoursePolicy? policy = null) =>
        // FOUNDATION1i path 3: the link is the course's move, its why and who asked, over the readings this has always made.
        carried is null ? null : DrivenCourse.From(handed, carried, profile, walked, retried, pressed, agent, decided, policy);
}
