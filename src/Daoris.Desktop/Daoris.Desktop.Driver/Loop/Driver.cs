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
    RunningSessions? running = null) : ILandingWiring, IPluginWiring, ISetupWiring, ILookStagesWiring, IRunWiring, IStartsWiring
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

    SessionHold IRunWiring.Holds => Holds;

    Func<string, string?> IRunWiring.Variable => Variable;

    (string Role, long Revision)? IRunWiring.RoleTaken(Consideration start, string? named) => RoleTaken(start, named);

    (ProcessStartInfo Info, string? Notice) IRunWiring.Prepare(ISessionAdapter adapter, SessionTarget target, HarnessSelection selection) =>
        Prepare(adapter, target, selection);

    (string? File, object? Meta, HandedSection Handed) IRunWiring.HandRules(
        ISessionAdapter adapter, ProcessStartInfo info, string sessionId, string? workspace, string? repository, string tree, string? kept,
        AcrossReach? across, IReadOnlyList<string>? plugins, IReadOnlyList<string>? continued, HeavyHanded? heavy) =>
        HandRules(adapter, info, sessionId, workspace, repository, tree, kept, across: across, plugins: plugins, continued: continued, heavy: heavy);

    EarlierMaker? IRunWiring.LeftBy(PriorSession prior) => LeftBy(prior);

    Task<SessionParts> IRunWiring.PartsAsync(
        EntryParts entry, string? joinedIn, string? entryTree, TreeOpened? opened, string repository, CancellationToken ct) =>
        PartsAsync(entry, joinedIn, entryTree, opened, repository, ct);

    void IRunWiring.SayMoved(PriorSession prior, string sessionId, string agent, string? account, string why, ComposedBrief brief) =>
        SayMoved(prior, sessionId, agent, account, why, brief);

    Task<(ActorAsked Asked, string Choice)?> IRunWiring.ReaskAsync(
        Consideration start, IReadOnlyList<RepoView> registry, ActorEffective refused, HarnessSelection selection, CancellationToken ct, string? role) =>
        ReaskAsync(start, registry, refused, selection, ct, role);

    Task<(ActorAsked Asked, string Choice)?> IRunWiring.ReaskHandedAsync(
        Consideration start, string agent, HarnessSelection selection, ModelRefusal refusal, HashSet<string> refused, CancellationToken ct, string? role) =>
        ReaskHandedAsync(start, agent, selection, refusal, refused, ct, role);

    Action<JsonElement> IRunWiring.Said(ISessionAdapter adapter, HarnessSelection selection, string sessionId) => Said(adapter, selection, sessionId);

    Task<bool> IRunWiring.WaitsOnOwnHelperAsync(string session, QuestView quest, DateTimeOffset openedAt, CancellationToken ct) =>
        WaitsOnOwnHelperAsync(session, quest, openedAt, ct);

    Task IRunWiring.ConcludedForOpinionAsync(
        string sessionId, QuestView quest, string? status, string state, string workTree, string? workspace, CancellationToken ct) =>
        ConcludedForOpinionAsync(sessionId, quest, status, state, workTree, workspace, ct);

    SessionConclusion IRunWiring.AccountRefused(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, HarnessEnding ended) =>
        AccountRefused(conclusion, adapter, selection, ended);

    SessionConclusion IRunWiring.ModelRefused(
        SessionConclusion conclusion, string agent, HarnessSelection selection, string? model, HarnessEnding ended, string sessionId) =>
        ModelRefused(conclusion, agent, selection, model, ended, sessionId);

    SessionConclusion IRunWiring.AccountSignedOut(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, HarnessEnding? ended) =>
        AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);

    (SessionConclusion Conclusion, bool Limit) IRunWiring.AccountLimited(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, string sessionId,
        AcpUsage? used, LimitSeen? byCode) =>
        AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used, byCode);

    Task<string?> IRunWiring.HandOverAtTurnEndAsync(string session, string quest, CancellationToken ct) => HandOvers.AtTurnEndAsync(session, quest, ct);

    StartReservation IRunWiring.MovingReservation(Consideration start) => ClaimedStart.Moving(config, start);

    Task<SessionConclusion> IRunWiring.EndStoppedAsync(string sessionId, SessionStop stop) => EndStoppedAsync(sessionId, stop);

    ConversationMoves IRunWiring.Moves => Moves;

    Action<LineCourse>? IRunWiring.Decided => Decided;

    void IRunWiring.Questions(string sessionId, QuestionSettle settled) => Questions(sessionId, settled);

    string? IRunWiring.DesignRoleName(Consideration start, string? named) => DesignRoleName(start, named);

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

    SessionWire ISetupWiring.Door() => StartDoor.Of(home, config, Context.Adapters);

    Func<CancellationToken, Task>? IStartsWiring.Wanted => Wanted;

    Func<Consideration, Action, CancellationToken, Task<StartRun>>? IStartsWiring.Runner => Runner;

    Task<StartRun> IStartsWiring.RunAsync(
        Consideration start, IReadOnlyList<RepoView> registry, List<TrustHold> untrusted, Action onOpened, FreshTree fresh, CancellationToken ct,
        string? movedOff, (string Agent, string Model)? movedOffModel, string? movedFrom) =>
        RunAsync(start, registry, untrusted, onOpened, fresh, ct, movedOff, movedOffModel, movedFrom);

    Task<StartRun> IStartsWiring.RunIntakeAsync(AskView ask, List<TrustHold> untrusted, Action onOpened, CancellationToken ct) =>
        RunIntakeAsync(ask, untrusted, onOpened, ct);

    RoleCatalog? IStartsWiring.CatalogueForReport() => CatalogueForReport();

    System.Collections.Concurrent.ConcurrentDictionary<string, string> IStartsWiring.LimitResumed => LimitResumed;

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
        var round = new LookRound(Part, events, new LookState(concluded));
        foreach (var phase in new[] { LookPhase.Before, LookPhase.Local, LookPhase.Read, LookPhase.Planned, LookPhase.Begun, LookPhase.After })
        {
            foreach (var pass in LookPasses.In(phase)) await pass.RunAsync(round, ct).ConfigureAwait(false);
        }

        var state = round.State;
        var outcome = state.Outcome;
        return new TickReport(Considerations.Blocked(state.Plan, outcome.Held, outcome.SignedOut), events, outcome.Progressed, state.Snapshot.Active, concluded)
        {
            Untrusted = outcome.Untrusted,
            Waits = outcome.Waits,
            JudgedOpinions = state.JudgedOpinions,
        };
    }

    /// <summary>
    /// The quests whose start resumed their limited record's own conversation (LIMITRESUME1b), by quest, the record as the value: set as
    /// the start keeps Daoris's word, taken off where its own conversation refused, and read once by the look's news.
    /// </summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<string, string> LimitResumed { get; } = new(StringComparer.Ordinal);

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
        // The state lets go of the repository's trees and the carry-on's move record with the run, on every way out.
        await using var state = new RunState();
        var run = new RunRound(start, registry, untrusted, onOpened, movedOff, movedOffModel, movedFrom, fresh, state);

        // Each phase's stages at its anchor, in the order the marks make: a stage may hold the start, which ends the run there.
        foreach (var phase in new[] { RunPhase.Choose, RunPhase.Grown, RunPhase.Placed, RunPhase.Cleared, RunPhase.Opened })
        {
            foreach (var stage in RunStages.In(phase))
            {
                if (await stage.RunAsync(run, ct).ConfigureAwait(false) is { } held) return held;
            }
        }

        // The record handed to its session, which runs to its conclusion however it ends.
        foreach (var stage in RunStages.In(RunPhase.Handed))
        {
            if (await stage.RunAsync(run, ct).ConfigureAwait(false) is { } ended) return ended;
        }

        throw new InvalidOperationException("no stage handed the opened record to its session");
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
    internal static async Task<string?> TookBindOrNullAsync(ServiceClient service, string sessionId, CancellationToken ct)
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
