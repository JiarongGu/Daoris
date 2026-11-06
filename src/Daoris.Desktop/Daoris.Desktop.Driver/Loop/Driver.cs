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
    ServiceClient service, DriverConfig config, ProviderRegistry adapters, string home,
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
    internal ProviderRegistry Built { get; } = adapters;

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

    internal Task<string?> StepRoleAsync(QuestView quest, string? workspace, CancellationToken ct) =>
        StartBinding.StepRoleAsync(Context, quest, workspace, ct);

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

    internal Task<SessionConclusion> EndStoppedAsync(string sessionId, SessionStop stop) => StoppedRecord.EndAsync(service, sessionId, stop);

    private SessionEquipment Equipment => LazyInitializer.EnsureInitialized(ref _equipment, () => new SessionEquipment(home, config, Variable));

    private SessionEquipment? _equipment;

    internal (ProcessStartInfo Info, string? Notice) Prepare(
        ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo? prepared = null) =>
        Equipment.Prepare(adapter, target, selection, prepared);

    internal bool HandedConnector(ISessionAdapter adapter, string? workspace, string? repository, string tool) =>
        Equipment.HandedConnector(adapter, workspace, repository, tool);

    internal (string? File, object? Meta, HandedSection Handed) HandRules(
        ISessionAdapter adapter, ProcessStartInfo info, string sessionId, string? workspace, string? repository,
        string tree, string? kept, IReadOnlyList<string>? job = null, AcrossReach? across = null, string? said = null,
        bool opinion = false, bool helper = false, IReadOnlyList<string>? plugins = null, string? brainstorm = null,
        IReadOnlyList<string>? continued = null, HeavyHanded? heavy = null, bool partner = false) =>
        SessionRules.Hand(
            home, adapter, info, sessionId, workspace, repository, tree, kept, job, across, said, opinion, helper, plugins, brainstorm,
            continued, heavy, partner);

    internal (RuleLists Rules, IReadOnlyList<string> HardDeny, bool Guarded) PartnerHanded(string? workspace, string? repository) =>
        SessionRules.PartnerHanded(home, workspace, repository);

    private AccountEndings Accounts => LazyInitializer.EnsureInitialized(ref _accounts, () => new AccountEndings(home, config, service, Context));

    private AccountEndings? _accounts;

    internal (SessionConclusion Conclusion, bool Limit) AccountLimited(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, string sessionId,
        AcpUsage? used, LimitSeen? byCode = null) =>
        Accounts.Limited(conclusion, adapter, selection, turnFailed, sessionId, used, byCode);

    internal Action<JsonElement> Said(ISessionAdapter adapter, HarnessSelection selection, string sessionId) =>
        Accounts.Said(adapter, selection, sessionId);

    internal NativeLimits NativeLimit(ISessionAdapter adapter) => Accounts.NativeLimit(adapter);

    internal SessionConclusion AccountRefused(SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, HarnessEnding ended) =>
        Accounts.Refused(conclusion, adapter, selection, ended);

    internal Noted RefuseAccount(ISessionAdapter adapter, HarnessSelection selection, string owner) => Accounts.Refuse(adapter, selection, owner);

    internal SessionConclusion ModelRefused(
        SessionConclusion conclusion, string agent, HarnessSelection selection, string? model, HarnessEnding ended, string sessionId) =>
        Accounts.ModelRefused(conclusion, agent, selection, model, ended, sessionId);

    internal SessionConclusion AccountSignedOut(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, HarnessEnding? ended = null) =>
        Accounts.SignedOut(conclusion, adapter, selection, turnFailed, ended);

    internal SessionWatching Watching => LazyInitializer.EnsureInitialized(ref _watching, () => new SessionWatching(home, config, Context, output));

    private SessionWatching? _watching;

    internal LineageBrackets Brackets =>
        LazyInitializer.EnsureInitialized(ref _brackets, () => new LineageBrackets(home, Context.Processes, Watching));

    private LineageBrackets? _brackets;

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
    internal ServerEntry? Connector(string sessionId, IReadOnlyDictionary<string, string>? scope, string? proc = null) =>
        KnowledgeConnector.Offer(
            Variable, AppContext.BaseDirectory,
            KnowledgeConnector.SessionScope(home, sessionId, scope, proc, KnowledgeConnector.KeptRole(Context.Conversations.RoleOf, sessionId)));

}
