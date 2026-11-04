using System.Diagnostics;
using System.Text.Json;
using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// The loop D45 exists for: watch, plan, spawn, observe. One tick, a look, fetches a snapshot, plans it (pure), starts every
/// planned start and returns once each has opened its record or come to nothing. The sessions run on beside the looks that
/// follow, and each one's ending joins the next look's report (D115 §3.1). Under the watch (D181 point 3) a look waits for a
/// start only until something else wants the loop, and leaves the work none of its parts waits on running beside it.
/// </summary>
/// <remarks>
/// <para><b>The driver never writes quest state.</b> It opens and advances SESSION records through the service's ledger; the
/// spawned session claims and closes its own quest through its own connector (D46 §3), so a failure here leaves the quest
/// where it was.</para>
/// <para><b>Everything here is observation.</b> A session's end is concluded from the exit code and the quest's state
/// (<see cref="Observation"/>), never from the session's word.</para>
/// </remarks>
public sealed class Driver(
    ServiceClient service, DriverConfig config, AdapterSet adapters, string home,
    SessionProcesses? processes = null, RemoteSyncSet? sync = null, SessionOutput? output = null,
    HarnessRoster? harnesses = null,
    // Null where nobody keeps the usage record: the headless rehearsal driver and every test that does not care.
    SessionUsage? usage = null,
    // The plugins that speak (D64), shared across ticks since their processes outlive one.
    HookSet? hooks = null,
    // Shared so the shell hears each event as it is written; null keeps the record under the home with nobody watching.
    SessionEvents? events = null,
    // Daoris's own browser (D78); null where no shell carries one, and a server that drives it is then not handed.
    IInAppBrowser? browser = null,
    // The sessions still running, shared by the watch across the driver it builds for each look; null keeps this driver's own.
    RunningSessions? running = null)
{
    /// <summary>A test's in-process stand-in for <see cref="RunAsync"/>: every real run spawns git and a harness, which the fast half may not.</summary>
    internal Func<Consideration, Action, CancellationToken, Task<StartRun>>? Runner { get; init; }

    /// <summary>Told each answer the course gave a driven path as it is decided: a test's, no person's.</summary>
    internal Action<LineCourse>? Decided { get; set; }

    /// <summary>
    /// The driven carry-on's move record: the one store the limit move writes, handed the lease holder's scope, or a shared loop's
    /// lines alone. Made on first use, since the watch builds a driver for each look; a test hands one of its own.
    /// </summary>
    internal ConversationMoves Moves
    {
        get => _moves ??= new ConversationMoves(
            home, service, Context.Events, output,
            scope: Share ? MoveTakeUpScope.Shared(session => Context.Runs.Live.Values.Contains(session)) : MoveTakeUpScope.LeaseHolder);
        init => _moves = value;
    }

    private ConversationMoves? _moves;

    /// <summary>Whether this is a <c>--share</c> loop beside the lease holder, which takes up only the moves of the lines it runs.</summary>
    public bool Share { get; init; }

    /// <summary>The watch's first look of a loop: every dead loop's driven move of this scope taken up through <see cref="Moves"/>. Never faults.</summary>
    internal async Task TakeUpMovesAsync(CancellationToken ct) => await Moves.LookAsync(ct).ConfigureAwait(false);

    /// <summary>A test's stand-in for stopping a running session; null stops it through <see cref="SessionProcesses"/>.</summary>
    internal Func<string, Noted, bool>? Stops { get => Context.Stops; init => Context.Stops = value; }

    public Func<string, string, CancellationToken, Task<HandedOver>>? HandsOver { get => Context.HandsOver; init => Context.HandsOver = value; }

    public IChatSendBacks? SendsBack { get => Context.SendsBack; init => Context.SendsBack = value; }

    public Func<string, ContinueReason, CancellationToken, Task<StartedFrom>>? GoesOnLost { get => Context.GoesOnLost; init => Context.GoesOnLost = value; }

    internal Func<CancellationToken, Task<IReadOnlyList<HandOverDoor>>>? HandOverDoors { get => Context.HandOverDoors; init => Context.HandOverDoors = value; }

    /// <summary>A test's stand-in for the world a look's workspace plans read: every real press reads a line with git and asks the tools their version.</summary>
    internal Func<IWorkspaceSetupWorld>? SetupPlans { get; init; }

    /// <summary>The machine log, where a failure nothing else awaits is written with its place; null where nothing keeps one.</summary>
    public MachineLog? Log { get => Context.Log; init => Context.Log = value; }

    /// <summary>What each quest's and ask's walk last passed, so <c>actor.passed</c> is written when a pass is new, not at every look.</summary>
    internal PassMemory Passed { get; } = new();

    public Action? Noticed { get => Context.Noticed; init => Context.Noticed = value; }

    internal LookSaid Lines => Context.Said;

    /// <summary>Where this driver's looks time their stages: the watch's, shared across its looks, else its own.</summary>
    public LookStages Stages
    {
        get => LazyInitializer.EnsureInitialized(ref _stages, () => new LookStages(Log));
        init => _stages = value;
    }

    private LookStages? _stages;

    /// <summary>The review's showing in Daoris's browser, shared across looks as the browser is; null where no shell carries one.</summary>
    public ReviewDesk? Reviews { get; init; }

    /// <summary>
    /// The driver's file, read again at a running driven session's turn's end: the look's config is the file as that look read it,
    /// and a session outlives its look. The watch's; null for a one-look run and a gate's.
    /// </summary>
    public string? ConfigPath { get; init; }

    internal static string BroughtForwardTold(ForwardTake forward) =>
        "its take of the quest lost: another machine's take, or the person's reopen of the work it grew from, reached the remote first, and "
        + "this machine's driver stopped it with its work kept here. Since then the person brought that work forward, and "
        + (forward.Standing == "held"
            ? "the remote confirmed this machine's fresh take, so the quest is yours."
            : "this machine's fresh take is not yet confirmed by the remote, which may still refuse it: until then the quest is yours, and if "
              + "it is refused, this session is stopped as the one before it was.");

    /// <summary>Why a person's line is refused for a driven session, and where its work goes instead; the bridge carries it verbatim.</summary>
    internal static string TakesNoMessages(QuestView quest) =>
        $"the session driven for quest #{quest.Id} takes no messages: it was handed its whole quest at once "
        + "and works it in one turn. What it does lands on the quest — its take, its close and its "
        + "commits — and stopping it is the one move that reaches it.";

    /// <summary>A park's question the disk would not let go at a run's opening or conclusion is logged: its park could show a question its run did not ask.</summary>
    internal void Questions(string sessionId, QuestionSettle settled)
    {
        if (settled == QuestionSettle.Stayed) Log?.Warn("question.stayed", ("session", sessionId));
    }

    internal static string? JoinNotices(string? first, string? second) =>
        (first, second) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{first}\n{second}",
            ({ Length: > 0 }, _) => first,
            _ => second,
        };

    // The sync beside working sessions runs at the watch loop's own cadence, so one choice sets both.
    private readonly TimeSpan _syncBeside = TimeSpan.FromSeconds(Math.Max(1, config.PollSeconds));

    /// <summary>Which part of the look <see cref="TickAsync"/> runs: the whole look unless the watch hands a wake's <see cref="LookPart.Plan"/>.</summary>
    public LookPart Part { get; init; } = LookPart.Look;

    /// <summary>The look's passes, registered by their modules, ordered once per process and built once per driver on its look doors.</summary>
    internal PartSet<ILookPass> LookPasses =>
        LazyInitializer.EnsureInitialized(ref _lookPasses, () => Parts.Build(LookOrder.Value, type => (ILookPass)Activator.CreateInstance(type, LookWiring)!));

    private PartSet<ILookPass>? _lookPasses;

    private static readonly Lazy<IReadOnlyList<PartDeclaration>> LookOrder = new(() => Parts.Declared<LookPassAttribute>(typeof(Driver).Assembly));

    /// <summary>A driven session's run's stages, registered by their modules, ordered once per process and built once per driver on its run doors.</summary>
    internal PartSet<IRunStage> RunStages =>
        LazyInitializer.EnsureInitialized(ref _runStages, () => Parts.Build(RunOrder.Value, type => (IRunStage)Activator.CreateInstance(type, RunWiring)!));

    private PartSet<IRunStage>? _runStages;

    private static readonly Lazy<IReadOnlyList<PartDeclaration>> RunOrder = new(() => Parts.Declared<RunStageAttribute>(typeof(Driver).Assembly));

    /// <summary>What starts a prepared session and holds it to its conclusion, for every kind of run this driver starts.</summary>
    internal SessionHold Holds => LazyInitializer.EnsureInitialized(ref _holds, () => new SessionHold(RunWiring));

    private SessionHold? _holds;

    private RunDoors? _runWiring;
    private LookDoors? _lookWiring;

    private RunDoors RunWiring => LazyInitializer.EnsureInitialized(ref _runWiring, () => new RunDoors(this));

    private LookDoors LookWiring => LazyInitializer.EnsureInitialized(ref _lookWiring, () => new LookDoors(this));

    /// <summary>Told a run a start found bound already, to a named workflow, whose chain has come back here; null tells nobody.</summary>
    internal Action<string>? RunReturned { get; init; }

    /// <summary>
    /// What completes once something else wants the loop: a nudge, an ending, the <c>look</c> topic's deadline. Handed by the
    /// watch, whose looks then leave beside them the work no part of the look waits on. Null, a one-look run's and a gate's,
    /// runs each in the look.
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

    /// <summary>The clock a driven session's transient retry waits on: the machine's, and a held one in a test.</summary>
    public TimeProvider RetryClock { get; init; } = TimeProvider.System;

    /// <summary>What the loop's classes share: the constructor's objects, the sessions and trees, the per-look adapters, servers and catalogue, and the settables.</summary>
    internal LoopContext Context { get; } = new(service, config, home, adapters, processes, harnesses, events, output, usage, running, hooks, browser);

    public RunningSessions Running => Context.Runs;

    /// <summary>The build's own adapters, which each tick's plugins are laid over.</summary>
    internal AdapterSet Built { get; } = adapters;

    internal RemoteSyncSet? Sync { get; } = sync;

    private DriverOpinions? _opinions;
    private OpinionDeliverer? _deliverer;
    private OpinionGateLook? _gate;

    private DriverOpinions Opinions => LazyInitializer.EnsureInitialized(ref _opinions, () => new DriverOpinions(Context, this));

    /// <summary>The second opinion's delivery, which the go-on also asks to read the answers and to send findings to the person.</summary>
    internal OpinionDeliverer Deliverer => LazyInitializer.EnsureInitialized(ref _deliverer, () => new OpinionDeliverer(Context, Opinions));

    internal OpinionGateLook Gate => LazyInitializer.EnsureInitialized(ref _gate, () => new OpinionGateLook(Context, Opinions, Deliverer));

    private DriverContinuations? _continuations;
    private DriverHandOvers? _handOvers;
    private DriverHelpers? _helpers;
    private DriverIntakes? _intakes;
    private DriverPartners? _partners;
    private DriverBrainstorms? _brainstorms;
    private DriverMeetings? _meetings;

    internal DriverContinuations GoOns => LazyInitializer.EnsureInitialized(ref _continuations, () => new DriverContinuations(Context, this));

    internal DriverHandOvers HandOvers =>
        LazyInitializer.EnsureInitialized(ref _handOvers, () => new DriverHandOvers(Context, () => Moves) { Decided = course => Decided?.Invoke(course) });

    internal DriverHelpers Helpers => LazyInitializer.EnsureInitialized(ref _helpers, () => new DriverHelpers(Context, this));

    internal DriverPartners Partners => LazyInitializer.EnsureInitialized(ref _partners, () => new DriverPartners(Context, this));

    internal DriverIntakes Intakes => LazyInitializer.EnsureInitialized(ref _intakes, () => new DriverIntakes(Context, this));

    internal DriverBrainstorms Brainstorms => LazyInitializer.EnsureInitialized(ref _brainstorms, () => new DriverBrainstorms(Context, this));

    internal DriverMeetings Meetings => LazyInitializer.EnsureInitialized(ref _meetings, () => new DriverMeetings(Context, Brainstorms));

    internal Task<int> PiecesAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Partners.PiecesAsync(snapshot, taken, events, ct);

    // The settables below are the tests' stand-ins for what a real pass spawns (git, an agent) or reads from the machine, written through to the context.
    internal Func<HelperPassAsk, ReviewerChoice, CancellationToken, Task<HelperPassRun>>? HelperPasses
    {
        get => Context.HelperPasses;
        init => Context.HelperPasses = value;
    }

    internal Func<string, CancellationToken, Task<(string? Tip, string? Root)>>? HelperTrees { get => Context.HelperTrees; init => Context.HelperTrees = value; }

    internal Func<string, CancellationToken, Task<HelperCaptureRead>>? HelperCaptures { get => Context.HelperCaptures; init => Context.HelperCaptures = value; }

    internal Func<HelperAsk, HelperRule, PriorSession, CancellationToken, Task<ReviewerChoice>>? HelperChoices
    {
        get => Context.HelperChoices;
        init => Context.HelperChoices = value;
    }

    internal IReadOnlyList<PartnerMeasurement>? PartnerMeasured { get => Context.PartnerMeasured; init => Context.PartnerMeasured = value; }

    internal Func<PartnerAsk, PriorSession, CancellationToken, Task<ReviewerChoice>>? PartnerChoices
    {
        get => Context.PartnerChoices;
        init => Context.PartnerChoices = value;
    }

    internal Func<PartnerPassAsk, ReviewerChoice, CancellationToken, Task<PartnerPassRun>>? PartnerPasses
    {
        get => Context.PartnerPasses;
        init => Context.PartnerPasses = value;
    }

    internal Func<PartnerAsk, PartnerState, CancellationToken, Task<PieceKept>>? PartnerKeeps { get => Context.PartnerKeeps; init => Context.PartnerKeeps = value; }

    internal Func<string, CancellationToken, Task<string?>>? PartnerTrees { get => Context.PartnerTrees; init => Context.PartnerTrees = value; }

    internal Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? BrainstormTurns { get => Context.BrainstormTurns; init => Context.BrainstormTurns = value; }

    internal Func<string, string, CancellationToken, Task<BrainstormSearch>>? BrainstormSearches
    {
        get => Context.BrainstormSearches;
        init => Context.BrainstormSearches = value;
    }

    internal Func<string, CancellationToken, Task<(string? Root, string? Revision)>>? BrainstormCheckouts
    {
        get => Context.BrainstormCheckouts;
        init => Context.BrainstormCheckouts = value;
    }

    internal Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? MeetingTurns { get => Context.MeetingTurns; init => Context.MeetingTurns = value; }

    internal Func<OpinionPassAsk, ReviewerChoice, CancellationToken, Task<OpinionPassRun>>? Passes { get => Context.Passes; init => Context.Passes = value; }

    internal WorkingTree.GitRead OpinionGit { get => Context.OpinionGit; init => Context.OpinionGit = value; }

    internal TimeSpan? BrainstormMinute { get => Context.BrainstormMinute; init => Context.BrainstormMinute = value; }

    internal TimeProvider BrainstormClock { get => Context.BrainstormClock; init => Context.BrainstormClock = value; }

    /// <summary>The environment this driver reads its variables from (D63's seam): this process's, unless a test hands in its own scratch.</summary>
    internal Func<string, string?> Variable { get; init; } = Environment.GetEnvironmentVariable;

    internal Task<int> BrainstormsAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Brainstorms.BrainstormsAsync(snapshot, taken, events, ct);

    public Task<RoomMeasure> MeasureRoomAsync(string agent, bool spend, CancellationToken ct) => Brainstorms.MeasureRoomAsync(agent, spend, ct);

    internal Task<int> MeetingsAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Meetings.MeetingsAsync(snapshot, taken, events, ct);

    internal Task<int> HelpersAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Helpers.HelpersAsync(snapshot, taken, events, ct);

    /// <summary>
    /// Whether a driven run that ends now holding its quest waits on its own helper: a question its run asked counts from the later of
    /// <paramref name="openedAt"/> and the words it was last handed, as its park binds one.
    /// </summary>
    internal Task<bool> WaitsOnOwnHelperAsync(string session, QuestView quest, DateTimeOffset openedAt, CancellationToken ct) =>
        Helpers.WaitsOnOwnHelperAsync(
            session, quest, Context.Events.LastHandedAt(session) is { } handed && handed > openedAt ? handed : openedAt, ct);

    public Task<OpinionPassRun> PassAsync(OpinionPassAsk ask, ReviewerChoice choice, CancellationToken ct = default) =>
        Opinions.PassAsync(ask, choice, ct);

    public Task<OpinionDelivery> DeliverAsync(string opinion, OpinionPassAsk asked, CancellationToken ct = default) =>
        Deliverer.DeliverAsync(opinion, asked, ct);

    internal Task<(IReadOnlyDictionary<string, OpinionSit> Sits, int Passes)> OpinionsAsync(
        Snapshot snapshot, List<string> events, CancellationToken ct, List<OpinionJudged>? judged = null) =>
        Gate.OpinionsAsync(snapshot, events, ct, judged);

    /// <summary>
    /// A start's bind of its run before its take (D190's binding moment): Workflows' use case over the loop's service, home, choices
    /// and plugins. A binding it made is kept for the next look's bind to say.
    /// </summary>
    internal async Task<WorkflowBind> BindAtStartAsync(QuestView quest, string? workspace, CancellationToken ct)
    {
        var bound = await WorkflowRunStart.AtStartAsync(
                new WorkflowStarted(quest, workspace), new StartWorld(service), home, config, WorkflowSteps.PluginsOf(Context.Catalog),
                () => DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        if (bound.Came == WorkflowBindCame.Bound) Context.Runs.Bound(bound);
        return bound;
    }

    /// <summary>
    /// The role a quest's run names for its work step, which its start walks: its kept binding's, or where the run is bound nowhere
    /// yet, the binding its bind would make now, since the walk comes before the bind; null where it names none.
    /// </summary>
    internal async Task<string?> StepRoleAsync(QuestView quest, string? workspace, CancellationToken ct) =>
        (await WorkflowRunStart.PlannedAsync(
                new WorkflowStarted(quest, workspace), new StartWorld(service), home, config, WorkflowSteps.PluginsOf(Context.Catalog),
                () => DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false))?.RoleOf(NodeKinds.Claimed)?.Role;

    /// <summary>A new start's walk over Build's effective order (D205 §4.1), choice by choice, through the quest's start plan.</summary>
    internal async Task<(HarnessSelection Selection, ActorEffective? Chosen)> WalkBuildAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct) =>
        (await WalkBuildPassesAsync(start, registry, ct).ConfigureAwait(false)) is var walked ? (walked.Selection, walked.Chosen) : default;

    /// <summary>
    /// <see cref="WalkBuildAsync"/> with each choice it passed and why. For the start's re-ask at the open, <paramref name="refused"/>
    /// is passed as if known before the spawn and <paramref name="counts"/> false takes no count of its own.
    /// </summary>
    /// <param name="role">The role the run's work step names, walked in place of the work's; null for the work's.</param>
    internal Task<SettledSelection> WalkBuildPassesAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct, bool counts = true,
        (ActorEffective Entry, string? Account)? refused = null, long except = 0, string? role = null) =>
        Walk.Plan(start, registry, role).ResolveAsync(ct, counts, refused, except);

    /// <summary>
    /// The start's re-ask where its agent refused the entry's model at the open: the walk asked again as if the refusal had been
    /// known before the spawn. Its answer is a later entry on the same agent and account; anything else, a walk that throws
    /// included, is null.
    /// </summary>
    internal async Task<(ActorAsked Asked, string Choice)?> ReaskAsync(
        Consideration start, IReadOnlyList<RepoView> registry, ActorEffective refused, HarnessSelection selection, CancellationToken ct,
        string? role = null)
    {
        try
        {
            return await Walk.Plan(start, registry, role).ReaskAsync(refused, selection, ct).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>The work a quest's start is: design where its publisher or the intake marked its weight so, else build.</summary>
    internal static string StartWork(QuestView quest) =>
        string.Equals(quest.Weight, ParticipantWork.Design, StringComparison.Ordinal) ? ParticipantWork.Design : ParticipantWork.Build;

    /// <summary>Where a quest's start is, by which a scoped role is found: its circle and the repository it is asked of.</summary>
    internal static RolePlace PlaceOf(Consideration start) => new(start.Workspace, start.Quest.To);

    internal QuestWalk Walk => LazyInitializer.EnsureInitialized(ref _walk, () => new QuestWalk(home, config, Context, Passed));

    private QuestWalk? _walk;

    /// <summary>
    /// One look: the sync, the plan, and every planned start begun. Returns once each start has opened its session record or
    /// come to nothing, never waiting for a session to end (D115 §3.1).
    /// </summary>
    /// <param name="failed">
    /// Handed what the look had said when it fails or is closed, when there is any, so a stop it wrote and failed after is not
    /// lost; the failure then goes on as it was. Never called for a look that returns.
    /// </param>
    public async Task<TickReport> TickAsync(CancellationToken ct = default, Action<TickReport>? failed = null)
    {
        var events = new List<string>();

        // What ended, structurally: a parked intake this look ends, and every session that ended since the last look.
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

    /// <summary>The quests whose start resumed their limited record's own conversation, by quest, the record as the value; read once by the look's news.</summary>
    internal System.Collections.Concurrent.ConcurrentDictionary<string, string> LimitResumed { get; } = new(StringComparer.Ordinal);

    /// <summary>A driven record that just concluded: a done whose rule reads it is owed a second opinion, which a later look asks. Never a reason to fail the run.</summary>
    internal async Task ConcludedForOpinionAsync(
        string sessionId, QuestView quest, string? status, string state, string workTree, string? workspace, CancellationToken ct)
    {
        var process = status == "Done"
            ? await WorkflowProcesses.ConcludedAsync(home, config, new ServiceReviewWorld(service), quest, workTree, workspace, ct).ConfigureAwait(false)
            : null;
        OpinionLook.Concluded(home, config, sessionId, quest, status, state, workTree, process: process);
    }

    public Task<IReadOnlyList<TickReport>> RunUntilIdleAsync(CancellationToken ct = default, Action<TickReport>? said = null) =>
        Looping.UntilIdleAsync(ct, said);

    public Task<TickReport> RunOnceAsync(CancellationToken ct = default, Action<TickReport>? said = null) => Looping.OnceAsync(ct, said);

    private LoopRuns Looping =>
        LazyInitializer.EnsureInitialized(ref _looping, () => new LoopRuns(LookWiring, (ct, failed) => TickAsync(ct, failed), _syncBeside, () => LookPasses));

    private LoopRuns? _looping;

    /// <summary>One quest's start, run through the stages of each phase in turn; a stage may hold the start, which ends the run there.</summary>
    /// <returns>
    /// The console line, whether a session actually opened, what it ended as and whose decision that was, and, for a start held at
    /// spawn, the hold's own sentence. A hold or a refusal ends nothing, and a refusal means somebody else got there first.
    /// </returns>
    /// <param name="registry">The registry as this tick's snapshot answered it: what the session may reach across (D107).</param>
    /// <param name="onOpened">Called once the session's record is open, and never for a start that held or was refused.</param>
    /// <param name="fresh">The tree this start opens, let go on each way out that starts nothing, on no token.</param>
    /// <param name="movedOff">The account the first run of this start was refused for its sign-in; a walk that gives it again holds.</param>
    internal async Task<StartRun> RunAsync(
        Consideration start, IReadOnlyList<RepoView> registry, List<TrustHold> untrusted, Action onOpened, FreshTree fresh, CancellationToken ct,
        string? movedOff = null, (string Agent, string Model)? movedOffModel = null, string? movedFrom = null)
    {
        // The state lets go of the repository's trees and the carry-on's move record with the run, on every way out.
        await using var state = new RunState();
        var run = new RunRound(start, registry, untrusted, onOpened, movedOff, movedOffModel, movedFrom, fresh, state);

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
    /// The record of a run whose wire broke after a stop made first: ended as that stop's conclusion would have ended it, never
    /// interrupted, and shared by every run that catches a failure after <c>HoldAsync</c>. Best-effort, as the failure's own write is.
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
    /// The spawn, prepared for either kind of session: the adapter's process, the toolchain's environment on it, and for a dsh home
    /// Daoris made, its profile written. Shared by a quest's session and an ask's intake, so neither can forget a line the other carries.
    /// </summary>
    /// <returns>The process to start, and what this harness is doing that Daoris could not govern.</returns>
    /// <param name="prepared">The process a resume prepared, given the same environment; null prepares a start.</param>
    internal (ProcessStartInfo Info, string? Notice) Prepare(
        ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo? prepared = null)
    {
        target = TargetPrompt.ForStart(target, adapter);
        // A standing block past what the first prompt carries goes through its file, written before the spawn reads it.
        if (prepared is null) StandingBlocks.Hand(target);
        var info = prepared ?? adapter.Prepare(target with { Home = target.Home ?? DaorisHome.Resolve(Variable) }, config.Commands.GetValueOrDefault(adapter.Name));
        Equip(adapter, info, selection);

        // Only a dsh home Daoris made is written to; anywhere else the notice says what happens, and a home holding somebody
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
    /// What every session's process carries on its environment: its toolchain's account, binary and pin, then its tools at the
    /// start by the adapter's own switch, so its connector's tools come with its first request. Applied here rather than inside
    /// each adapter's spawn so no adapter can forget it; the switch is set last, so an account's own variables cannot defer the tools again.
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
    /// Whether the rules this session would be handed let it call the connector's <paramref name="tool"/> unasked (D73): the same
    /// composition <see cref="HandRules"/> hands over. False for a harness that takes no rules, which then depends on the repository's own list.
    /// </summary>
    internal bool HandedConnector(ISessionAdapter adapter, string? workspace, string? repository, string tool) =>
        adapter.TakesSettings
        && PermissionRules.AllowsConnector(
            PermissionRules.Compose(PermissionRules.Load(home), workspace, repository), tool);

    internal (string? File, object? Meta, HandedSection Handed) HandRules(
        ISessionAdapter adapter, ProcessStartInfo info, string sessionId, string? workspace, string? repository,
        string tree, string? kept, IReadOnlyList<string>? job = null, AcrossReach? across = null, string? said = null,
        bool opinion = false, bool helper = false, IReadOnlyList<string>? plugins = null, string? brainstorm = null,
        IReadOnlyList<string>? continued = null, HeavyHanded? heavy = null, bool partner = false) =>
        SessionRules.Hand(
            home, adapter, info, sessionId, workspace, repository, tree, kept, job, across, said, opinion, helper, plugins, brainstorm,
            continued, heavy, partner);

    /// <summary>The protocol-door posture a brainstorm's pass asks; null for every other session, and for a pass whose harness a rule narrows.</summary>
    internal static string? PassPosture(SessionTarget target, ISessionAdapter adapter) =>
        target.Brainstorm is not null ? BrainstormPermissions.Posture(adapter) : null;

    internal (RuleLists Rules, IReadOnlyList<string> HardDeny, bool Guarded) PartnerHanded(string? workspace, string? repository) =>
        SessionRules.PartnerHanded(home, workspace, repository);

    internal static HandedSection RulesHanded(bool takes, PermissionFile file, RuleLists handed, string? workspace, string? repository) =>
        SessionRules.Handed(takes, file, handed, workspace, repository);

    private AccountEndings Accounts => LazyInitializer.EnsureInitialized(ref _accounts, () => new AccountEndings(home, config, service, Context));

    private AccountEndings? _accounts;

    internal (SessionConclusion Conclusion, bool Limit) AccountLimited(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, string sessionId,
        AcpUsage? used, LimitSeen? byCode = null) =>
        Accounts.Limited(conclusion, adapter, selection, turnFailed, sessionId, used, byCode);

    internal Action<JsonElement> Said(ISessionAdapter adapter, HarnessSelection selection, string sessionId) =>
        Accounts.Said(adapter, selection, sessionId);

    internal SessionConclusion AccountRefused(SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, HarnessEnding ended) =>
        Accounts.Refused(conclusion, adapter, selection, ended);

    internal Noted RefuseAccount(ISessionAdapter adapter, HarnessSelection selection, string owner) => Accounts.Refuse(adapter, selection, owner);

    internal SessionConclusion ModelRefused(
        SessionConclusion conclusion, string agent, HarnessSelection selection, string? model, HarnessEnding ended, string sessionId) =>
        Accounts.ModelRefused(conclusion, agent, selection, model, ended, sessionId);

    internal SessionConclusion AccountSignedOut(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, HarnessEnding? ended = null) =>
        Accounts.SignedOut(conclusion, adapter, selection, turnFailed, ended);

    /// <summary>
    /// What a further part of a session is handed of the parts before it: each one's record, its tree's name where the tree stands
    /// and its branch now, or the branch its work landed on where the tree is gone; and how its own tree stands to theirs. Only the
    /// newest the instruction names are read from git; a running part's note is its opening, so none is said.
    /// </summary>
    /// <param name="joinedIn">The tree it went into, standing; null where it did not.</param>
    /// <param name="entryTree">The tree the planner found free, gone or not; null where it found none.</param>
    internal async Task<SessionParts> PartsAsync(
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

    private SessionWatching Watching => LazyInitializer.EnsureInitialized(ref _watching, () => new SessionWatching(home, config, Context, output));

    private SessionWatching? _watching;

    internal TimeSpan CaptureBound
    {
        get => Watching.CaptureBound;
        set => Watching.CaptureBound = value;
    }

    internal Task<bool> CaptureWithinAsync(Task capture, string sessionId) => Watching.CaptureWithinAsync(capture, sessionId);

    /// <summary>Exit code, or null when a pass's own minutes ran out and killed it, bounded by the tests' backstop where no minutes are given.</summary>
    internal Task<int?> WaitAsync(
        Process process, CancellationToken ct, int? minutes = null, Action<int>? timingOut = null, CancellationToken quiet = default,
        Action? quietEnding = null) =>
        SessionWaits.ExitAsync(process, minutes ?? config.TestBound, ct, timingOut, quiet, quietEnding);

    internal IReadOnlyList<string> LookIn(LookSaid said) => Watching.Look(said);

    /// <summary>The watcher: every running driven session's, shared by every look's driver through the record.</summary>
    internal SessionWatch Watch => SessionWatch.Of(Context.Events);

    /// <summary>A capture's stream, stamping its session heard at each line read; a session the watcher does not watch is stamped nothing.</summary>
    internal TextReader Hearing(TextReader reader, string sessionId) => Watch.Hearing(reader, sessionId);

    /// <summary>
    /// How often a driven session that waits on its own background work reads its quest again (D83's note): every fourth tick of the
    /// driver's own, so a waiting session costs a quarter of a tick and its run ends within a minute of its quest's close.
    /// </summary>
    internal TimeSpan BackgroundLook => TimeSpan.FromSeconds(4 * Math.Max(1, config.PollSeconds));

    /// <summary>
    /// The knowledge server this session is offered, located per run because a machine can gain the host between ticks, carrying
    /// who the session is, where its rules live, and an intake's ask when it is one.
    /// </summary>
    /// <param name="proc">The lineage token of the process this entry is composed for, on the protocol door alone.</param>
    internal AcpMcpServer? Connector(string sessionId, IReadOnlyDictionary<string, string>? scope, string? proc = null) =>
        KnowledgeConnector.Offer(
            Variable, AppContext.BaseDirectory,
            KnowledgeConnector.SessionScope(home, sessionId, scope, proc, KnowledgeConnector.KeptRole(Context.Conversations.RoleOf, sessionId)));

    /// <summary>A process's lineage token: issued, its <c>start</c> written to the session's lineage, and set anew on the spawn's environment, all before the spawn.</summary>
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
    /// Loop's wait on a bracketed process: the exit it read told to the token set, which may not watch that process; where the wait
    /// throws as the loop closes, its kill having read the exit, the process's end written unclean before the throw leaves the run.
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

    /// <summary>A bracketed process's drain: a capture that faults past the exit writes its end, unclean, before the fault leaves.</summary>
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

    /// <summary>
    /// Why the graph named for a session's take did not bind it, or null: a line's extra word is never worth failing the end of a
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
    /// A quest's target with the person's words on the ask its session's line is on, read for this start: every start on the ask is
    /// handed them, since a carry-on's own record holds only the session before it. A line on no ask is handed none, and words the
    /// service did not answer leave today's instruction, saying so (<see cref="AskWords.ReadAsync"/>).
    /// </summary>
    internal static async Task<SessionTarget> WithAskWordsAsync(SessionTarget target, string session, ServiceClient service, CancellationToken ct) =>
        target with { Words = await AskWords.ReadOfSessionAsync(service, session, ct).ConfigureAwait(false) };

    /// <summary>The work's session language for the quest's repository, from the config this tick holds: the repository's, else its workspace's, else none, and never the window's.</summary>
    internal static SessionTarget WithLanguage(SessionTarget target, DriverConfig config, string? workspace) =>
        target with { Language = SessionLanguages.Resolve(config, target.Repository, workspace) };

    /// <summary>The go-aheads its repository's access setup asks for, from the setup this start resolved once: under <c>never</c>, the instruction asks only what the person's words demand.</summary>
    internal static SessionTarget WithAccess(SessionTarget target, ResolvedAccess access) =>
        target with { NoGoAheads = access.Value(AccessRules.GoAheads) == AccessRules.Never };

    /// <summary>The part a carry-on goes on from, as its record says it ran: its harness, that harness's maker where this build has it, and the models its door reported.</summary>
    internal EarlierMaker? LeftBy(PriorSession prior)
    {
        if (prior.Adapter is not { Length: > 0 } ran) return null;
        string? maker;
        try { maker = Context.Adapters.Resolve(ran).Toolchain?.Maker; }
        catch (DriverException) { maker = null; }
        return new EarlierMaker(ran, maker, prior.Ran?.Models ?? []);
    }

    /// <summary>
    /// A driven move to a new part said as it happens (D189 point 8): a coded line on the part it left and on the new part, each on
    /// its console too, with the context the part it left stood at and the brief's characters; and <c>session.moved</c> in the
    /// machine log. Every driven start that continues another says it, whatever moved it.
    /// </summary>
    internal void SayMoved(PriorSession prior, string sessionId, string agent, string? account, string why, ComposedBrief brief)
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
    /// The session a quest's start continues, for its record's <c>from</c> link: the session the quest was handed over from, with the
    /// hand-over's why and asker; else a session a limit cut off, carried on onto another account, Daoris's own move; else the
    /// curve's move. Every other carry-on names its link too, so the brief follows the line: <c>retry</c> where its resume fell back,
    /// else <c>carry-on</c>, the person's where their press released or brought it forward. A first start, a resume of its own record
    /// and a fresh start carry nothing on and say none. The link is the course's move, its why and who asked.
    /// </summary>
    /// <param name="retried">Its record's resume was tried and fell back: a driven record's fallback.</param>
    /// <param name="pressed">The person's press carried it on: <i>Try again</i> after their stop, or a bring-forward.</param>
    /// <param name="handed">The quest's hand-over as held, or null.</param>
    /// <param name="carried">The record it carries on, where it carries one on, or null.</param>
    /// <param name="profile">The account it opens on.</param>
    /// <param name="agent">The agent it opens on, where a limit's move may have gone to another one; null compares accounts alone.</param>
    /// <param name="walked">
    /// The reason of the walk's first step, where the walk chose the account (<see cref="StepSaid.Why"/>). An account over its
    /// curve passed, or the most room under it, is the usage curve's move; a cooling or refused account passed, a limit reached
    /// and the rest are not.
    /// </param>
    internal static ContinuedFrom? ContinuesFrom(
        HandedQuest? handed, PriorSession? carried, string? profile, string? walked = null, bool retried = false, bool pressed = false,
        string? agent = null, Action<LineCourse>? decided = null, CoursePolicy? policy = null) =>
        carried is null ? null : DrivenCourse.From(handed, carried, profile, walked, retried, pressed, agent, decided, policy);
}
