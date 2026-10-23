using System.Diagnostics;
using System.Text.Json;

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
    RunningSessions? running = null)
{
    /// <summary>
    /// How one quest's start is run, from its holds to its record's conclusion: <see cref="RunAsync"/>, unless a
    /// test hands in an in-process stand-in (DEV3). Every real run spawns git and a harness, which the suite's fast
    /// half may not, and what a look does with its starts is the scheduling that half has to hold. The action is
    /// the run's to call once its session record is open.
    /// </summary>
    internal Func<Consideration, Action, CancellationToken, Task<StartRun>>? Runner { get; init; }

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
    /// <c>daoris-driver access</c> wrote since is seen only here. The watch's; null where nothing names it, a one-look run's and a gate's,
    /// and a session there goes on under its start's setup until its next start.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>What a session whose take lost is told, in its record (D68 §5).</summary>
    public const string LostClaim =
        "stopped by this machine's driver: another machine's take on the quest reached the remote first, so this "
        + "session's take is a conflict on the quest and its work would double someone else's.";

    /// <summary><see cref="LostClaim"/> with its code (LANG1a).</summary>
    public static Noted LostClaimNoted => Noted.Of(NoteCodes.EndedLostClaim, LostClaim);

    /// <summary>
    /// What a session whose take lost to the person's reopen of the work its step grew from is told (FABRIC-CLOSE1c18): the claim's
    /// answer says so beside the lost claim, and no other machine took anything first.
    /// </summary>
    public const string LostClaimToReopen =
        "stopped by this machine's driver: you reopened the work this session's step grew from, and that reached the remote before this "
        + "session's take did, so the take is a conflict on the quest. Its work stays in its tree; the step is open again for the work as "
        + "it stands now.";

    /// <summary><see cref="LostClaimToReopen"/> with its code.</summary>
    public static Noted LostClaimToReopenNoted => Noted.Of(NoteCodes.EndedLostClaimReopened, LostClaimToReopen);

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
    /// The wakes a plan was woken by (FABRIC-END7), which its landing looks at alone (<see cref="AutoLander.ChooseAsync"/>): null, as
    /// the whole look hands it, lands on every due entry.
    /// </summary>
    public IReadOnlyCollection<LandingWake>? Woke { get; init; }

    /// <summary>
    /// Whether this look's landing judged with <see cref="Woke"/> (FABRIC-END7's review): set once its selection read the due list, or
    /// found it is not this process's to land. False where the look failed before it, the selection failed, or a landing still ran: the
    /// loop puts the wakes back.
    /// </summary>
    internal bool WokeSpent { get; private set; }

    /// <summary>Whether the selection was left because a landing still ran (FABRIC-END7's review): that landing's end offers the wakes again.</summary>
    internal bool LandingBusy { get; private set; }

    /// <summary>
    /// Told once a landing pass this look started has ended and no longer runs (FABRIC-END7's review), so the loop offers again the wakes
    /// it held back meanwhile.
    /// </summary>
    public Action? LandingEnded { get; init; }

    /// <summary>
    /// Hands back the wakes this look took (PARALLEL1g): told by a selection beside the look that failed before it judged with them, as a
    /// look whose selection failed leaves them unspent (FABRIC-END7's review), so the next look judges them again.
    /// </summary>
    internal Action? WokeBack { get; init; }

    /// <summary>
    /// Told a run a start found bound already, to a named workflow (FABRIC-CUTOVER1c; the cutover design §3): where it is a graph run whose record
    /// holds its end, its chain has come back here, and the loop's runtime is asked for a pass that judges it. Null tells nobody.
    /// </summary>
    internal Action<string>? RunReturned { get; init; }

    /// <summary>
    /// Asks the loop's runtime for a whole pass (FABRIC-CUTOVER1d; the cutover design §4): the look's automatic landing held by a run's graph for a
    /// judgement its next pass makes asks it, and tries again at its next look. Null asks nobody.
    /// </summary>
    internal Action? PassAsked { get; init; }

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

    private DriverHandOvers HandOvers => LazyInitializer.EnsureInitialized(ref _handOvers, () => new DriverHandOvers(Context));

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

    private DriverBrainstorms? _brainstorms;

    private DriverBrainstorms Brainstorms => LazyInitializer.EnsureInitialized(ref _brainstorms, () => new DriverBrainstorms(Context, this));

    /// <summary>The brainstorms on file at a look (BRAINSTORM1a, D176): <see cref="DriverBrainstorms.BrainstormsAsync"/>.</summary>
    internal Task<int> BrainstormsAsync(Snapshot snapshot, int taken, List<string> events, CancellationToken ct) =>
        Brainstorms.BrainstormsAsync(snapshot, taken, events, ct);

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

    /// <summary>The quests handed to another agent, at a look (MIXAGENT1a-ii): <see cref="DriverHandOvers.HandOversAsync"/>.</summary>
    /// <summary>
    /// The supervisor's release through the service's say (SUPERVISOR1a-i, D208; the supervisor design §3.4): no text, its decision,
    /// question and choice, the receipt as the say's key. Kept answers the word's id; a refusal its word; a lost response throws, which
    /// the release keeps held to repeat.
    /// </summary>
    internal async Task<SupervisorSaid> SupervisorSayAsync(
        string session, string decision, string question, string choice, string key, CancellationToken ct)
    {
        var said = await service.SayAsync(
            session, "", ct: ct, from: SupervisorHanded.From, key: key, supervised: (decision, question, choice)).ConfigureAwait(false);
        return said.Kept && said.Word?.Id is { } word ? new SupervisorSaid(word, null) : new SupervisorSaid(null, said.Refusal ?? "unsaid");
    }

    /// <summary>
    /// A park whose own conversation cannot resume and that nothing carries on (<see cref="DriverContinuations.CannotGoOn"/>); a
    /// supervisor's decision on it goes <c>stale</c> first, its word taken back, so the park is a wait on the owner again (SUPERVISOR1a-i,
    /// the supervisor design §6.1).
    /// </summary>
    private async Task<StartRun> CannotGoOnAsync(QuestView quest, PriorSession park, ContinueReason why, CancellationToken ct)
    {
        if (Context.Supervisor.Moving(park.Session))
        {
            await Context.Supervisor.CannotGoOnAsync(
                park.Session, async (session, word, token) => (await service.TakeBackAsync(session, word, ct: token).ConfigureAwait(false)).Done,
                DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        }

        return GoOns.CannotGoOn(quest, park, why);
    }

    internal Task<Snapshot> HandOversAsync(Snapshot snapshot, List<string> events, CancellationToken ct) =>
        HandOvers.HandOversAsync(snapshot, events, ct);

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

    /// <summary>The asks due an intake at a look (D65 §1b): <see cref="DriverIntakes.IntakesDueAsync"/>.</summary>
    private Task<IReadOnlyList<AskView>> IntakesDueAsync(int slots, List<string> events, CancellationToken ct) =>
        Intakes.IntakesDueAsync(slots, events, ct);

    /// <summary>One intake run in its room (D65 §1b): <see cref="DriverIntakes.RunIntakeAsync"/>.</summary>
    private Task<StartRun> RunIntakeAsync(AskView ask, List<TrustHold> untrusted, Action onOpened, CancellationToken ct) =>
        Intakes.RunIntakeAsync(ask, untrusted, onOpened, ct);

    /// <summary>Every parked intake whose ask was answered since is ended: <see cref="DriverIntakes.ConcludeAnsweredAsync"/>.</summary>
    private Task ConcludeAnsweredAsync(
        IReadOnlyList<SessionView> active, List<string> events, List<SessionEnded> concluded, CancellationToken ct) =>
        Intakes.ConcludeAnsweredAsync(active, events, concluded, ct);

    /// <summary>The record the person's words wait on, continued or ended saying why (ANSWER1a): <see cref="DriverContinuations.ContinueAsync"/>.</summary>
    private Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> ContinueAsync(
        Consideration start, PriorSession park, HarnessSelection selection, IReadOnlyList<RepoView> registry,
        TreeLock? starting, Action onOpened, CancellationToken ct, string agent, ContinueReason? account = null, string? inSync = null,
        DrivenAccess? permissions = null, bool treeStands = true) =>
        GoOns.ContinueAsync(start, park, selection, registry, starting, onOpened, ct, agent, account, inSync, permissions, treeStands);

    /// <summary>
    /// What became of a session's tree before it went on (TREEGONE1), said on that session's record: its console and its conversation,
    /// by code. The tree made again in its place is said once, as it happens.
    /// </summary>
    private void TreeSaid(string session, Noted said)
    {
        Context.Output?.Append(session, said.Note);
        Context.Events.Keep(
            session,
            new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts.Count > 0 ? said.Parts : null },
            say: null);
    }

    /// <summary>
    /// A tree a start goes on in, brought up to date where the line holds some of its commits (LANDSYNC2): what the session is told,
    /// or null. Git or a disk that fails here is no reason to hold the start: it goes on in the tree as it stands, told nothing.
    /// </summary>
    private async Task<TreeStartSync?> StartInSyncAsync(string tree, CancellationToken ct)
    {
        try
        {
            return await Context.Trees.StartInSyncAsync(tree, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// A step's tree the person went on with after reopening its source (FABRIC-CLOSE1c5), brought up to date with the source's new
    /// submission, read off the wire now, then with the line: what the session is told, or null. A source the service cannot answer is
    /// read as one naming no commit, so only the line is synced and that said; git or a disk that fails is no reason to hold the start.
    /// </summary>
    private async Task<TreeStartSync?> GoOnInSyncAsync(string tree, QuestView step, CancellationToken ct, bool broughtForward = false)
    {
        QuestView? source = null;
        try
        {
            if (step.Parent is { Length: > 0 } parent) source = await service.FindQuestAsync(parent, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException)
        {
            // Read as naming no commit: the session is told only the line was synced.
        }

        try
        {
            return await Context.Trees.GoOnInSyncAsync(tree, $"#{step.Parent}", Planner.SourceCommit(step, source), ct, broughtForward).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

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
    /// The look's bind (WORKFLOW1e, D157 point 11 as D190 amends it; the workflow design §4.5, the cutover design §8): what the starts
    /// bound before their take since the last look (<see cref="BindAtStartAsync"/>), said; then each run whose session opened in this look
    /// and that no binding names yet, bound to the workflow §4.1 chooses for it now, which only a run opened before FABRIC-CUTOVER1g or a
    /// start a test stands in for leaves. Workflows' use case (<see cref="WorkflowRunStart"/>), composed here over the loop's service,
    /// home, choices and plugins, and the clock.
    /// </summary>
    private Task BindRunsAsync(IReadOnlyList<Consideration> opened, List<string> events, CancellationToken ct) =>
        WorkflowRunStart.BindAsync(
            [.. opened.Select(start => new WorkflowStarted(start.Quest, start.Workspace))], new StartWorld(service), home, config,
            WorkflowCurrent.PluginsOf(Context.Catalog), () => DateTimeOffset.UtcNow, events, ct, Lines, made: Context.Runs.TakeBound());

    /// <summary>
    /// A start's bind of its run before its take (FABRIC-CUTOVER1g, D190's binding moment; the cutover design §8): Workflows' use case
    /// (<see cref="WorkflowRunStart.AtStartAsync(WorkflowStarted, IWorkflowStartWorld, string, DriverConfig, IReadOnlyList{WorkflowPlugin}, Func{DateTimeOffset}, CancellationToken)"/>)
    /// over the loop's service, home, choices and plugins. A binding it made is kept for the next look's bind to say.
    /// </summary>
    private async Task<WorkflowBind> BindAtStartAsync(QuestView quest, string? workspace, CancellationToken ct)
    {
        var bound = await WorkflowRunStart.AtStartAsync(
                new WorkflowStarted(quest, workspace), new StartWorld(service), home, config, WorkflowCurrent.PluginsOf(Context.Catalog),
                () => DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        if (bound.Came == WorkflowBindCame.Bound) Context.Runs.Bound(bound);
        return bound;
    }

    /// <summary>What binding a run reads and says through the loop's service: its quests, its asks, and the machine log's line.</summary>
    private sealed class StartWorld(ServiceClient service) : IWorkflowStartWorld
    {
        public Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct) => service.EveryQuestAsync(ct);

        public Task<AskView?> FindAskAsync(string id, CancellationToken ct) => service.FindAskAsync(id, ct);

        public void Chosen(LandingLine line) => service.LandingSaid(line);
    }

    /// <summary>
    /// The door this machine's starts ride — the protocol door where any entry of Build's order rides it, else the first entry's wire
    /// (MODELROLE1c, D205 §4.1's <c>no-connector</c>: the planner asks whether any entry can drive a repository that has not adopted
    /// Daoris, and the start passes each entry that cannot), read against this tick's adapters so a plugin's harness counts. An adapter
    /// nobody knows is the pipe: the stricter door plans nothing more than it should, and the spawn names the unknown adapter itself.
    /// </summary>
    private SessionWire Door()
    {
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = ActorChoices.Effective("build", config);
        }
        catch (DriverException)
        {
            order = [new ActorEffective(config.Adapter, null, null, "before")];
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
    /// A new start's walk over Build's effective order (MODELROLE1c, D205 §4.1): choice by choice, each choice's accounts by the existing
    /// walk before the next. An entry that cannot run here at all is passed by its code before any account is asked: <c>unavailable</c>
    /// (no such agent), <c>no-posture</c> (its harness has no mode for the repository's permission prompts, judged by the start's own
    /// <see cref="DrivenAccess"/>) and <c>no-connector</c> (the pipe door, for a repository that has not adopted Daoris). The first entry
    /// whose walk allows a start is taken; with none, the start holds on the wait's sentence, the soonest cool-off beside it. An order of
    /// one entry is today's walk exactly, its own hold or refusal unchanged.
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
    internal async Task<BuildWalk> WalkBuildPassesAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct, bool counts = true,
        (ActorEffective Entry, string? Account)? refused = null, long except = 0)
    {
        var quest = start.Quest;
        var order = ActorChoices.Effective("build", config);
        IReadOnlyCollection<string>? PassedFor(ActorEffective entry) =>
            refused is { Entry: var at, Account: { } account } && at == entry ? [account] : null;
        if (order.Count == 1)
        {
            var (only, _) = await Context.Harnesses
                .SelectHandedAsync(order[0].Agent, config, start.Workspace, null, StartKind.Driven, ct, order[0].Model, PassedFor(order[0]), except, counts)
                .ConfigureAwait(false);
            return new BuildWalk(only, order[0], []);
        }

        var adopted = registry.FirstOrDefault(repo => string.Equals(repo.Repository, quest.To, StringComparison.OrdinalIgnoreCase))?.Adopted ?? true;
        var access = AccessRules.Resolve(config, quest.To, start.Workspace);
        var passes = new List<BuildPass>();
        HarnessSelection? held = null;
        CoolingEntry? soonest = null;
        foreach (var entry in order)
        {
            string? code = null;
            string? why = null;
            if (!Context.Adapters.Names.Contains(entry.Agent, StringComparer.OrdinalIgnoreCase))
            {
                (code, why) = ("unavailable", $"`{entry.Agent}` is not an agent on this machine");
            }
            else
            {
                var adapter = Context.Adapters.Resolve(entry.Agent);
                if (DrivenAccess.For(access, quest.To, adapter).Refusal is { } unposed) (code, why) = ("no-posture", unposed);
                else if (!adopted && adapter.Wire != SessionWire.Acp)
                {
                    (code, why) = ("no-connector", $"`{entry.Agent}` cannot take a quest here: `{quest.To}` has not adopted Daoris");
                }
            }

            if (code is null)
            {
                var (selection, _) = await Context.Harnesses
                    .SelectHandedAsync(entry.Agent, config, start.Workspace, null, StartKind.Driven, ct, entry.Model, PassedFor(entry), except, counts)
                    .ConfigureAwait(false);
                if (selection.Allowed)
                {
                    if (counts)
                    {
                        Log?.Write("info", "actor.chose", [("actor", "build"), ("quest", quest.Id), ("choice", entry.From), ("agent", entry.Agent),
                            ("model", entry.Model), ("passed", passes.Select(pass => pass.Said).ToList())]);
                    }

                    return new BuildWalk(selection, entry, passes);
                }

                held ??= selection;
                if (selection.Cooling is { } cooling && (soonest is null || cooling.Until < soonest.Until)) soonest = cooling;
                // An entry whose every ready account does not offer its model is passed `not-offered` (D205 §3.3), never cooled.
                (code, why) = (selection.NotOffered is not null ? AgentOffers.NotOffered
                    : selection.Cooling is not null ? "cooling" : selection.SignedOut is not null ? "signed-out" : "held", selection.Refusal);
            }

            passes.Add(new BuildPass(entry, code, why));
            if (counts) Log?.Write("info", "actor.passed", [("actor", "build"), ("quest", quest.Id), ("choice", entry.From), ("agent", entry.Agent), ("code", code)]);
        }

        // The wait's sentence (D205 §4.5): judged again at every look, the first choice that can run, in order, taken.
        var wait = $"Build waits: none of its choices can run now. {string.Join("; ", passes.Select(pass => pass.Said))}.";
        return new BuildWalk((held ?? new HarnessSelection(wait)) with { Refusal = wait, Cooling = soonest }, null, passes);
    }

    /// <summary>
    /// The start's re-ask where its agent refused the entry's model at the open (D205 §3.3, MODELROLE1c2): the walk asked again as if the
    /// refusal had been known before the spawn, taking no count of its own. Its answer is a later entry to set on the same session where it
    /// names the same agent on the same account; anything else is null, and the opened record ends <c>choice-not-offered</c>, the next look
    /// starting what the walk then answers.
    /// </summary>
    internal async Task<(ActorAsked Asked, string Choice)?> ReaskAsync(
        Consideration start, IReadOnlyList<RepoView> registry, ActorEffective refused, HarnessSelection selection, CancellationToken ct)
    {
        BuildWalk again;
        try
        {
            again = await WalkBuildPassesAsync(start, registry, ct, counts: false, refused: (refused, selection.Profile), except: selection.Number)
                .ConfigureAwait(false);
        }
        catch (DriverException)
        {
            return null;
        }

        var same = again.Chosen is { } later && later != refused
                   && string.Equals(later.Agent, refused.Agent, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(again.Selection.Profile, selection.Profile, StringComparison.OrdinalIgnoreCase);
        Log?.Write("info", "actor.reasked", [("actor", "build"), ("quest", start.Quest.Id), ("refused", refused.From),
            ("answer", again.Chosen?.From), ("same", same)]);
        return same ? (again.Chosen!.Asked, again.Chosen.From) : null;
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

        // The sync runs BESIDE the sessions, not only around them (D68 §6): every whole look syncs, and a whole look comes
        // round at the watch's pace while sessions work. After it, every session this driver runs whose take came
        // back LOST is stopped, because its quest is another machine's and its work would double theirs (D68 §5).
        // An unconfirmed take keeps working. A wake's plan does neither (FABRIC-EVENT3a): the claims it would ask after move
        // only when the sync runs, so both wait for the `look` topic's deadline, and a wake costs the remote nothing.
        // PARALLEL1b (D181 point 3): under the watch the sync runs beside the look, on its own task, so a remote that does not answer
        // holds that pass and no local start; this look plans over what the last pass brought, and the change stream wakes the plan
        // that reads what this one brings. The stop stays the look's, so a stop a look made is said with it: at every whole look, and
        // at the first look after a pass beside ended while a session here held a quest. Both are `|`, so a whole look takes the due.
        if (Part == LookPart.Look)
        {
            if (Wanted is not null) SyncBeside(ct);
            else await SyncAsync(events, ct).ConfigureAwait(false);
        }

        if (sync is not null && (Part == LookPart.Look | Context.Runs.TakeClaimsDue()))
        {
            await Stages.TimeAsync("claims", () => StopLostClaimsAsync(events, ct)).ConfigureAwait(false);
        }

        // The plugins, read fresh each tick like the config (D64): a harness declared since the
        // last look is spawnable now, a server declared since is handed now, a hook process
        // disabled since is stopped now. The catalogue refuses a name this build carries before
        // anything of that plugin is taken.
        await Stages.TimeAsync("plugins", async () =>
        {
            var catalog = PluginCatalog.Load(home, _built.Names);
            Context.Adapters = _built.WithPlugins(catalog);
            Context.Servers = catalog.Servers;
            Context.Catalog = catalog;
            Context.Harnesses.Use(Context.Adapters);
            // One set of Daoris's accounts (POOL1a, D197): the wiring folded at the come-up, and again at a next look a held lock refused.
            Context.Harnesses.Pool(Log);
            if (Context.Hooks is { } hooks)
            {
                events.AddRange(await hooks.ReconcileAsync(catalog, ct, Lines).ConfigureAwait(false));
            }
        }).ConfigureAwait(false);

        // What sessions proposed about the rules (PERM2, D74), settled BEFORE this tick spawns anything:
        // a narrowing is applied at once, so this tick's sessions are already handed it, and a widening
        // is held for the person — who alone may let an agent do more.
        try
        {
            // Each line classed where the proposals settle it (NOTIFY2h2).
            using (Stages.Begin("rules")) events.AddRange(RuleProposals.Settle(home, DateTimeOffset.UtcNow, Lines));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            events.Add(Lines.Retry("rules", $"rules  the proposals could not be settled this tick: {error.Message}"));
        }

        // SESSWATCH1a (D200, the session-watch design §2): the watcher looks in on every running driven session, from memory, reading a
        // job only for one unheard for half its bound; each step it takes is said, and nothing quiet is ended but under --quiet-end.
        using (Stages.Begin("watch")) events.AddRange(LookIn(Lines));

        // The lines Daoris moved since each was last followed (WSSETUP5, D124 §3.1), registered from the line before this
        // look reads the registry, so a set-up that just landed is planned by what it now declares. Nothing is read with
        // git, or asked of the service, unless a line moved.
        await Stages.TimeAsync("lines", () => FollowMovedLinesAsync(events, ct)).ConfigureAwait(false);

        // The workspace plans (WSSETUP6, D124 §4.1), worked before this look reads the quests, so a set-up a plan publishes now
        // is planned, and started, in this same look. Nothing is read unless a plan works and is not paused.
        await Stages.TimeAsync("setup", () => WorkSetupPlansAsync(events, ct)).ConfigureAwait(false);

        // The goal's walk counts Daoris's sessions from this look's records (TOOL6b, D130 §4.2), and every start chosen
        // while they were read: marked first, so a conversation chosen meanwhile is not lost.
        var mark = Context.Harnesses.Mark();
        // PARALLEL1b: the starts still opening, read before the snapshot as well as at the plan, so one whose record opened while the
        // snapshot was read is held by one of the two, whichever the ledger's answer missed.
        var reservedBefore = Context.Runs.Reserved;
        var snapshot = await Stages.TimeAsync("snapshot", () => service.SnapshotAsync(ct)).ConfigureAwait(false);
        // The quests of every paused work, read again at each look (PAUSE1b, design §2.3): the planner holds each.
        snapshot = await Stages.TimeAsync("paused", () => PausedWork.LookAsync(service, config, snapshot, ct)).ConfigureAwait(false);
        // SUPERVISOR1a-i (D208; the supervisor design §3.4, §6.1, §6.3): each decision not yet final compared, settled and released
        // first, so the release's own word is recorded before anything judges it; then a word claiming the supervisor that Daoris did
        // not attribute is taken back before anything plans, and never planned from.
        SupervisorTakeBack takeBack = async (session, word, token) =>
            (await service.TakeBackAsync(session, word, ct: token).ConfigureAwait(false)).Done;
        var undecided = snapshot;
        snapshot = await Stages.TimeAsync("supervisor", async () => await SupervisorQuarantine.LookAsync(
            await SupervisorQuarantine.DecideAsync(undecided, Context.Supervisor, SupervisorSayAsync, takeBack, DateTimeOffset.UtcNow, ct)
                .ConfigureAwait(false),
            Context.Supervisor, takeBack, Log, ct).ConfigureAwait(false)).ConfigureAwait(false);
        // 🔴 COWORK1e (D188 point 4; the message-urgency design §2.3): answers that would take their line on alone a third time in a row
        // with nobody between are marked for the person first, so the planner leaves them waiting. A record needs its line read only where
        // answers alone wait on it.
        var lookedAt = snapshot;
        // A record a supervisor's held decision holds is not picked (SUPERVISOR1a-i, §6.3): nothing goes on from it inside the window.
        await Stages.TimeAsync("alone", () => new AnswersAloneHold(Context.Events, Context.Marks)
            .LookAsync(
                service,
                lookedAt.LastRun.Values.Concat(lookedAt.NamedByReopen.Values).Where(record => !lookedAt.SupervisorHolds(record)),
                DateTimeOffset.UtcNow, ct)).ConfigureAwait(false);
        // The words a record could not go on with, read again at each look (MSG1b): the planner leaves them waiting. The session a
        // reopen names is one the planner goes on with too (FABRIC-CLOSE1c9).
        snapshot = snapshot with { Unable = Context.Marks.For(snapshot.LastRun.Values.Concat(snapshot.NamedByReopen.Values)) };
        // COAGENT1b (the co-agent design §5): the pieces on file, whose reservations every starter below counts under the cap.
        snapshot = snapshot with { Pieces = new PartnerFiles(home).Reservations() };
        // The quests handed to another agent (MIXAGENT1a-ii, D163 point 1): the boxes taken, the quests that left dropped, and what
        // stands read for the planner, which reads the stop that handed one over as released.
        var read = snapshot;
        snapshot = await Stages.TimeAsync("handovers", () => HandOversAsync(read, events, ct)).ConfigureAwait(false);
        // RETRYWORK1: the commits a failed session left beyond its start, for each open quest it failed before taking, so the planner
        // carries the quest on in that tree rather than building its work again in a new one. No git starts where no such quest is.
        var handed = snapshot;
        snapshot = await Stages.TimeAsync("committed", () => CommittedWork.LookAsync(handed, CommittedWork.CountAsync, ct)).ConfigureAwait(false);
        Context.Harnesses.Look(snapshot.Started, mark);

        // The ask's channel (SESSONE2b, D167 point 7; the co-working design §2.3): what is new for each line of work on an ask reaches
        // its newest part here where it can take it now, and otherwise waits for the entry's next part. One read per such line of work.
        var looked = snapshot;
        events.AddRange(await Stages.TimeAsync("channel", () => Context.Channel
            .LookAsync(new ServiceChannelDoors(service), ChannelReach.Parts(looked, Context.Processes), ct, Lines))
            .ConfigureAwait(false));

        // A source's reopen (FABRIC-CLOSE1c8, §E5): the session that said the reopened ready is told once, with the person's words,
        // and the planner's go-on resumes it at the next look; a running one this loop runs at its next step, through its inbox
        // (COWORK1b). Read from the quests this look read: nothing more where none is reopened.
        events.AddRange(await Stages.TimeAsync("reopens", () => Context.Reopens
                .LookAsync(new ServiceReopenDoors(service, Context.Processes, Context.Events), looked, ct, Lines))
            .ConfigureAwait(false));

        // The set-ups waiting for the person here, and each set-up step's tab let go once they answered it (REVIEWENV1d, design
        // §2.3): kept served from look to look until then. A miss is said and tried again at the next look.
        if (Reviews is { } showing)
        {
            try
            {
                events.AddRange(await Stages.TimeAsync("reviews", () => showing.LookAsync(looked.Quests, service.FindQuestAsync, ct, Lines))
                    .ConfigureAwait(false));
            }
            catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException or DriverException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                events.Add(Lines.Retry("review", $"review  the set-ups' tabs were not looked at this look: {error.Message}"));
            }
        }

        // The second opinions owed (XAGENT1f, the second-agent design §2.1, §7, §8.1): asked and delivered beside the look, and the
        // starts they sit held. A pass finishes work already done, so it takes its slot before any quest's first start (D130).
        // PARALLEL1g (D181 point 3): under the watch each is judged as its topic's judge beside the look, and the plan reads what each last
        // found; one not judged yet holds its chain. The facts' wake is taken first, so a judgment landing after this wakes the next look.
        Context.Runs.TakeFactsMoved();
        var judgedOpinions = new List<OpinionJudged>();
        var (opinionSits, passes) = await Stages.TimeAsync("opinions", () => Context.Beside
            ? Task.FromResult(Gate.OpinionsBeside(looked, judgedOpinions, Stages, ct))
            : OpinionsAsync(looked, events, ct, judgedOpinions)).ConfigureAwait(false);
        snapshot = snapshot with { Opinions = opinionSits };

        // The helpers asked for (MIXAGENT2a-ii, D163 point 2): read, judged and started beside the look, each taking a slot under
        // the cap as a pass does, and each answer handed to the session that asked. PARALLEL1g: under the watch an asker's record, an
        // answer's handing and a restart's settling are the `helper` topic's judge's; the verdict and its start stay here.
        var judged = snapshot;
        var taken = passes;
        passes += await Stages.TimeAsync("helpers", () => HelpersAsync(judged, taken, events, ct)).ConfigureAwait(false);

        // The pieces asked of partners (COAGENT1b, D199 points 2 to 4): judged and reserved in the common admission, each started beside
        // the look in its copy and its strict job, and carried from where it stands to its answer.
        taken = passes;
        passes += await Stages.TimeAsync("partners", () => PiecesAsync(judged, taken, events, ct)).ConfigureAwait(false);

        // The brainstorms on file (BRAINSTORM1a, D176): each request frozen into its packet for the person to read, and each one the
        // person sent run beside the look, taking a slot under the cap as a pass does.
        taken = passes;
        passes += await Stages.TimeAsync("brainstorms", () => BrainstormsAsync(judged, taken, events, ct)).ConfigureAwait(false);

        // The meetings on file (AGENTROOM1a, D177): each round's packet frozen for the person to read, and each round they sent run
        // beside the look, its turns one at a time through a brainstorm's pass, each taking a slot under the cap.
        taken = passes;
        passes += await Stages.TimeAsync("meetings", () => MeetingsAsync(judged, taken, events, ct)).ConfigureAwait(false);

        // Whether this loop carries Daoris's browser decides whether a local set-up step can be shown here (REVIEWENV1c).
        IReadOnlyList<Consideration> plan;
        using (Stages.Begin("plan"))
        {
            snapshot = snapshot with { Starting = [.. reservedBefore.Union(Context.Runs.Reserved)] };
            plan = Planner.Plan(snapshot, passes > 0 ? config with { Cap = Math.Max(0, config.Cap - passes) } : config, Door(), window: Context.Browser is not null);
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

        // A parked intake has no process left to watch — it asked the person and ended — so its record
        // ends when the person answers the ask (D65 §1b). Looked at every tick, whatever the intake
        // setting says now: turning intakes off must not strand one that is waiting.
        await Stages.TimeAsync("answered", () => ConcludeAnsweredAsync(snapshot.Active, events, concluded, ct)).ConfigureAwait(false);

        // The asks this machine answers with a session (D65 §1b) — only where a harness is named for
        // it, and only in the slots the quests left: work somebody already asked for goes first. PARALLEL1b: a start still opening
        // holds its slot, and an ask whose intake is still opening is not begun again. A start a plugin will hold still counts here,
        // since each start asks its plugins on its own below: the intakes may wait a look for a slot a hold left, never overrun the cap.
        var slotsLeft = config.Cap - snapshot.SlotsHeld - Planner.Opening(snapshot).Count - starts.Count - passes;
        var intakes = (await Stages.TimeAsync("intakes", () => IntakesDueAsync(slotsLeft, events, ct)).ConfigureAwait(false))
            .Where(ask => !snapshot.Starting.Any(start => string.Equals(start.Ask, ask.Id, StringComparison.OrdinalIgnoreCase)))
            .ToList();

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
                start.Quest.Id, null, start.Quest.To, start.Workspace, new HandedQuests(home).Read(start.Quest.Id)?.Agent ?? config.Adapter, start);
            Begin(started, new StartReservation(start.Quest.Id, null, start.Quest.To, TreeGoneBackInto(start)), (own, opened) => ClaimedAsync(start, own, opened));
        }

        foreach (var ask in intakes)
        {
            var started = new StartBegun(null, ask.Id, $"ask #{ask.Id}", ask.Workspace, config.IntakeAdapter!);
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

        // WORKFLOW1e (design §4.5), as FABRIC-CUTOVER1g moves it (D190, the cutover design §8): a start binds its run itself, before its
        // take, and the look says what the starts bound since the last look, a start held or refused after its bind included. A run whose
        // session opened in this look and that no binding names yet (one opened before that build, or a start a test stands in for) is
        // bound here. PARALLEL1b: what a start an earlier look let go of bound is said by the look after it.
        var openedStarts = outcomes
            .Where(each => each.Opened && each.Started.Start is not null)
            .Select(each => each.Started.Start!)
            .DistinctBy(start => start.Quest.Id, StringComparer.Ordinal)
            .ToList();
        await Stages.TimeAsync("bind", () => BindRunsAsync(openedStarts, events, ct)).ConfigureAwait(false);

        await Stages.TimeAsync("endings", () => EndingsAsync(events, concluded, ct)).ConfigureAwait(false);

        // Done work lands itself (LAND2b, D145 point 2): after the endings and their sync, so a done that just closed is read
        // as the service now holds it. The look chooses; the landings run beside it, as its starts do. PARALLEL1g: under the watch the
        // choosing runs beside it too, in the landing's pass (SelectBeside).
        await Stages.TimeAsync("landing", () => LandDueAsync(events, ct)).ConfigureAwait(false);

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
                return await fresh.UntilOpenAsync(
                        () => Runner is { } runner ? runner(start, opened, ct) : RunAsync(start, snapshot.Repositories, own, opened, fresh, ct), ct)
                    .ConfigureAwait(false);
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
        started is { Quest: { } quest, Start: { GoesOn: false, Resumes: { Limit: true } prior } }
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
                return new AccountWait(
                    held[0].Started.Adapter, cooling.Agent, cooling.Account, workspaces.Count == 1 ? workspaces[0] : null,
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
                    held[0].Started.Adapter, held[0].Came!.SignedOut!.Agent, null, workspaces.Count == 1 ? workspaces[0] : null,
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
    /// The sessions due to land automatically (LAND2b, D145 point 2, design §2): chosen in this look, landed beside it. One pass
    /// at a time, since a plugin's two minutes may outlast a look; what it says joins a later look's report. A due list or a
    /// service that does not answer is said, and its sessions are tried again at the next look: landing is never a dead look.
    /// </summary>
    /// <remarks>
    /// FABRIC-END7's review: only the process holding the home's lease selects or launches one (finding 2), as only it keeps an end
    /// (<see cref="EndKeeping"/>): a loop beside it (<c>drive --share</c>) has its own guard and its own looks, so both could land one
    /// entry and each call its plugin. The person's press is unchanged. <see cref="Woke"/> is spent only where the selection judged
    /// with it (finding 3, <see cref="WokeSpent"/>).
    /// </remarks>
    private async Task LandDueAsync(List<string> events, CancellationToken ct)
    {
        if (!HomeLease.HeldHere(home))
        {
            // Not this process's to land: the lease holder's looks land it, and its wakes are this loop's to drop.
            WokeSpent = true;
            return;
        }

        if (Context.Runs.Landing)
        {
            LandingBusy = true;
            return;
        }

        try
        {
            // The rule's plugin says its lines on the console under its name, as at a press (D100), and what the landing did
            // with it is logged where the loop's hooks are (PLUGUI1d).
            var lander = new AutoLander(
                home, new ServiceAutoLandingWorld(service),
                new SessionTrees(home, new LandingPlugins(home, say: (plugin, line) => output?.Append($"plugin:{plugin}", line), pluginLog: Context.Hooks?.Log)),
                Context.Events, service.LandingSaid) { Look = Lines, PassAsked = PassAsked };
            if (Context.Beside)
            {
                SelectBeside(lander, ct);
                return;
            }

            var (chosen, said) = await lander.ChooseAsync(config, ct, Woke).ConfigureAwait(false);
            WokeSpent = true;
            // Each line classed where the lander made it (NOTIFY2h2).
            events.AddRange(said);
            if (chosen.Count > 0 && !Context.Runs.Beside(token => lander.LandAsync(chosen, token), ct, LandingEnded))
            {
                // A pass began meanwhile (looks run one at a time, so none should): what was chosen is chosen again after it.
                WokeSpent = false;
                LandingBusy = true;
            }
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                          or UnauthorizedAccessException)
        {
            events.Add(Lines.Retry(
                "landing", $"landing  the sessions due to land could not be looked at this look, and are tried again at the next: {error.Message}"));
        }
    }

    /// <summary>
    /// The selection and its landings, under the watch, as one landing pass beside the look (PARALLEL1g, D181 point 3): a due list, a
    /// workflow, a quest or a tree slow to read holds that pass, never a start. The wakes this look took are the pass's to judge with, so
    /// they are spent here, once (FABRIC-END7); a selection that fails hands them back (<see cref="WokeBack"/>) before it says so, and
    /// its line wakes the look that judges them again. A look while the pass runs leaves its own wakes for the pass's end, as before.
    /// </summary>
    private void SelectBeside(AutoLander lander, CancellationToken ct)
    {
        if (lander.Due.Open().Count == 0)
        {
            WokeSpent = true;
            return;
        }

        var woke = Woke;
        var back = WokeBack;
        if (!Context.Runs.Beside(async token =>
            {
                AutoChosen chosen;
                try
                {
                    chosen = await Stages.TimeAsync("landing", () => lander.ChooseAsync(config, token, woke), beside: true).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or JsonException or IOException
                                                  or UnauthorizedAccessException
                                                  || (error is OperationCanceledException && !token.IsCancellationRequested))
                {
                    back?.Invoke();
                    // Said once until a selection reads again, so one that keeps failing wakes no look of its own (LookFacts.Failed).
                    return Context.Runs.Facts.Failed("landing")
                        ? [Lines.Retry("landing", $"landing  the sessions due to land could not be looked at this time, and are tried again at a later look: {error.Message}")]
                        : [];
                }

                Context.Runs.Facts.Judged("landing");
                if (chosen.Chosen.Count == 0) return chosen.Said;
                IReadOnlyList<string> landed = await lander.LandAsync(chosen.Chosen, token).ConfigureAwait(false);
                return [.. chosen.Said, .. landed];
            }, ct, LandingEnded))
        {
            // A pass began meanwhile (looks run one at a time, so none should): its end offers the wakes again.
            LandingBusy = true;
            return;
        }

        WokeSpent = true;
    }

    /// <summary>
    /// A driven record that just concluded, read against its repository's rule (LAND2b): a done under the switch joins the due
    /// list, and the rest under it is said in its conversation. Never a reason to fail the run: the record has concluded.
    /// </summary>
    internal async Task ConcludedForLandingAsync(
        string sessionId, QuestView quest, string? status, string state, string workTree, string? workspace, CancellationToken ct)
    {
        // WORKFLOW1f: its run's process, read once for both, so a named workflow's landing step and opinion step decide what is owed.
        var process = status == "Done"
            ? await AutoLander.ProcessAsync(home, config, new ServiceReviewWorld(service), quest, workTree, workspace, ct).ConfigureAwait(false)
            : null;
        AutoLander.Concluded(home, config, Context.Events, sessionId, quest, status, state, workTree, workspace, process: process);
        // XAGENT1f: a done whose rule reads it is owed a second opinion, which a later look asks (the second-agent design §2.1).
        OpinionLook.Concluded(home, config, sessionId, quest, status, state, workTree, process: process);
    }

    /// <summary>
    /// Each repository whose line Daoris moved since it was last followed, registered from its line (WSSETUP5), with what
    /// changed or must be fixed added to <paramref name="events"/>. A pass the service did not answer is said, and the
    /// lines stay due for the next look: following is never a dead look.
    /// </summary>
    private async Task FollowMovedLinesAsync(List<string> events, CancellationToken ct)
    {
        var due = RegistryFollowing.Due(home);
        if (due.Count == 0) return;
        try
        {
            var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(service, home, config), due, ct).ConfigureAwait(false);
            events.AddRange(report.Followed.Select(followed => RegistrationFollow.EventLine(followed, Lines)).OfType<string>());
            if (report.Refresh is { } refresh) events.Add(Lines.Retry("registry", $"registry  the index was not read again after registering: {refresh}"));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            events.Add(Lines.Retry(
                "registry", $"registry  the lines Daoris moved could not be followed this look, and are tried again at the next: {error.Message}"));
        }
    }

    /// <summary>
    /// Every workspace plan under the home worked once (WSSETUP6): the next set-up published while fewer than its
    /// <c>atOnce</c> are open, a refusal skipped, the pilot's pause taken, each said in <paramref name="events"/>. A plan the
    /// service could not answer for is said, and tried again at the next look: a plan is never a dead look.
    /// </summary>
    private async Task WorkSetupPlansAsync(List<string> events, CancellationToken ct)
    {
        try
        {
            var world = SetupPlans?.Invoke() ?? new WorkspaceSetupWorld(service, home);
            events.AddRange(await WorkspaceSetup.TickAsync(
                world, config, Door(), home, DateOnly.FromDateTime(DateTime.Now), DateTimeOffset.UtcNow, ct, Lines).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                          or IOException or UnauthorizedAccessException)
        {
            events.Add(Lines.Retry(
                "setup", $"setup  the workspace plans could not be worked this look, and are tried again at the next: {error.Message}"));
        }
    }

    /// <summary>
    /// The sync, once, with what it said added to <paramref name="events"/>. It runs before a look's snapshot,
    /// so the look plans over what the team has done (D68). Its failure is an event, never a dead look: the
    /// next retries, and every verb has already committed here — but a sync dying quietly looks exactly like
    /// a family with nothing to say, so the wall is named.
    /// </summary>
    /// <param name="beside">Whether it runs beside the look (<see cref="SyncBeside"/>), for its stage's mark (LOOKWAIT1).</param>
    private async Task SyncAsync(List<string> events, CancellationToken ct, bool beside = false)
    {
        if (sync is null) return;
        SyncReport synced;
        using (var syncing = Stages.Begin("sync", beside: beside))
        {
            // Its gate's queue, and its git's waits for a place (LOOKWAIT1b), said as one.
            synced = await syncing.RunAsync(() => sync.RunOnceAsync(ct)).ConfigureAwait(false);
            syncing.Queued(synced.Queued);
        }

        lock (events)
        {
            if (synced.Problem is not null)
            {
                events.Add(Lines.Retry("sync", $"sync  {synced.Problem}"));
            }

            // What the remote understood and deliberately did not take (D48 §6) — a stale or
            // branch feed, a quest it would not keep, a move that lost to another machine's.
            // Reported as its own kind of line: each is news about the family, not a fault here.
            foreach (var note in synced.Notes) events.Add(Lines.Progress("sync", $"held  {note}"));
        }
    }

    /// <summary>
    /// The sync, run beside the look on the running set's own task (PARALLEL1b, D181 point 3): what it says joins a later look's report,
    /// one pass at a time, and one asked for while a pass works follows it once. The lost-claim stop after it is the next look's
    /// (<see cref="RunningSessions.TakeClaimsDue"/>), so a stop is still said by the look that made it.
    /// </summary>
    private void SyncBeside(CancellationToken ct)
    {
        if (sync is null) return;
        Context.Runs.SyncBeside(async token =>
        {
            var said = new List<string>();
            await SyncAsync(said, token, beside: true).ConfigureAwait(false);
            return said;
        }, ct);
    }

    /// <summary>
    /// What ended since the last look, into the report (DEV3): each run's line and what it ended as, then
    /// everything that ended told to the plugins that listen, then one more sync.
    /// </summary>
    /// <remarks>
    /// Taken last, so an ending is never lost with a look that failed before its report. The sync carries what
    /// those sessions did — a take, a close, a chain's next step — in the look that reports them, rather than a
    /// look later (sync design §8). Nothing concluded, nothing new to carry.
    /// </remarks>
    private async Task EndingsAsync(List<string> events, List<SessionEnded> concluded, CancellationToken ct)
    {
        foreach (var ended in Context.Runs.Drain())
        {
            // As the pass that made it classed it (NOTIFY2h2); else an end the watch announces, or the person's own (NOTIFY2h, the
            // design §5.1: `:1246` is `progress`).
            events.Add(Lines.Ran(ended.Look, ended.Ended is { ByPerson: true }, ended.Line));
            if (ended.Ended is not null) concluded.Add(ended.Ended);
        }

        // Contained: nothing a plugin says here changes the record, which moved on the exit code and the
        // quest before this line ran (D46 §4). PARALLEL1b: under the watch, as the `hooks` topic's judge beside the look, a session's
        // ending told once and HooksAtOnce endings at a time, so a plugin slow to hear holds no later look; what it says joins a later
        // report. A one-look run and a gate's run-until-idle tell them in the look, as before.
        if (Context.Hooks is { } hooks)
        {
            foreach (var ended in concluded)
            {
                if (Wanted is not null)
                {
                    Context.Runs.Judge("hooks", ended.Session, HooksAtOnce,
                        token => Stages.TimeAsync("hooks", () => hooks.EndedAsync(ended, token, Lines), ended.Session, beside: true), ct);
                    continue;
                }

                events.AddRange(await Stages.TimeAsync("hooks", () => hooks.EndedAsync(ended, ct, Lines), ended.Session).ConfigureAwait(false));
            }
        }

        // Under the watch, beside the look (PARALLEL1b): the report after it carries what the sync said.
        if (concluded.Count == 0) return;
        if (Wanted is not null) SyncBeside(ct);
        else await SyncAsync(events, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Stop every session this driver is running whose take LOST (D68 §5) — found by asking this
    /// machine's host where its claim on the session's quest stands. A driver stops only its own
    /// processes (D47 §6), and the record says why, not that the person did.
    /// </summary>
    /// <remarks>
    /// Once each (DEV3b): a session already stopped for its lost take is still running until it has written how it ended, and
    /// a pass in between asked again and said a second stop.
    /// </remarks>
    private async Task StopLostClaimsAsync(List<string> events, CancellationToken ct)
    {
        // PARALLEL1b (D181 point 3): asked at once, at most ClaimsAtOnce together, so a host slow to answer for one quest holds that
        // question alone, not every session's after it until the client gives up on it.
        await Parallel.ForEachAsync(
            Context.Runs.Live.ToArray(), new ParallelOptions { MaxDegreeOfParallelism = ClaimsAtOnce, CancellationToken = ct },
            async (live, token) =>
            {
                var (quest, session) = (live.Key, live.Value);
                string claim;
                string? why;
                try
                {
                    (claim, why) = await service.ClaimStandingAsync(quest, token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                                  // The client's own timeout (DEV3a): a host that took the question and stalled.
                                                  || (error is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    return; // The host did not answer; the next pass asks again.
                }

                // FABRIC-CLOSE1c18: a take lost to the person's reopen is stopped alike, and said as that, never as another machine's take.
                var reopened = why == "source-reopened";
                if (claim == "lost" && (Stops ?? StopForLostClaim)(session, reopened ? LostClaimToReopenNoted : LostClaimNoted))
                {
                    var news = new LookNews(reopened ? LookSaid.LostToReopen : LookSaid.LostClaim, session, session, quest);
                    lock (events) events.Add(Lines.News("stop", news, $"stop  session {session} (#{quest}): {(reopened ? LostClaimToReopen : LostClaim)}"));
                }
            }).ConfigureAwait(false);
    }

    /// <summary>How many claim questions the lost-claim stop asks this machine's host together (PARALLEL1b): local, and each one small.</summary>
    internal const int ClaimsAtOnce = 4;

    /// <summary>
    /// How many endings the plugins hear at once beside the looks (PARALLEL1b; a topic bounded, the events design §A3): one slow answer
    /// holds its own ending, and a burst of endings reaches each plugin's one process two requests at a time, never all at once.
    /// </summary>
    internal const int HooksAtOnce = 2;

    /// <summary>The process's stop, unless the driver already stopped it for its reason: true only when it stopped it now.</summary>
    private bool StopForLostClaim(string session, Noted reason) =>
        Context.Processes.StopReason(session) is null && Context.Processes.Stop(session, reason);

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
                        await SyncAsync(pending, ct).ConfigureAwait(false);
                        await StopLostClaimsAsync(pending, ct).ConfigureAwait(false);
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
                await EndingsAsync(pending, concluded, ct).ConfigureAwait(false);
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
    private async Task<StartRun> RunAsync(
        Consideration start, IReadOnlyList<RepoView> registry, List<TrustHold> untrusted, Action onOpened, FreshTree fresh, CancellationToken ct)
    {
        var quest = start.Quest;
        var root = start.Root!;
        var isolated = config.OpensOwnTree(quest.To);
        StartRun Hold(string why) => new($"held  #{quest.Id} → {quest.To}: {why}", false, null, why);

        // The agent this start runs on (MIXAGENT1a-ii, D163 point 1): the one a hand-over named for the quest, held until the quest
        // leaves this machine, else the machine's. A local, and a parameter wherever it is passed on: starts run side by side.
        var handedQuest = new HandedQuests(home).Read(quest.Id);
        var agent = handedQuest?.Agent ?? config.Adapter;
        // Whether this start carries on from the very session the quest was handed over from: the one whose instruction and note say so.
        var handedFrom = handedQuest is not null && start.Resumes is { } earlier
                         && string.Equals(earlier.Session, handedQuest.From, StringComparison.Ordinal);
        string? passedAccount = null;
        // MODELROLE1c (D205 §4.0): the entry of Build's order this start runs, kept on its record as its choice and carried to its agent; null
        // for a continuation, which keeps its record's asked choice (§3.5(a)).
        ActorEffective? chosen = null;
        // What the walk passed before the entry it took (D205 §4.6), said in the opening note's actor line.
        IReadOnlyList<BuildPass> buildPasses = [];

        // 🔴 SESSCTX1c (D189's amendment): the person's words waiting on a record whose agent this machine no longer has wait for the
        // person, its conversation saying so with the hand-over's offer, since only they can choose the agent it goes on with. Asked
        // where the roster or the access setup refuses the agent, before anything is spawned; any other start's refusal holds as before.
        StartRun? AgentGone()
        {
            if (!start.GoesOn || start.Resumes is not { WordsWaiting: true } waiting
                || Context.Adapters.Names.Contains(agent, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            var gone = Continuations.AgentGone(agent, waiting.Session);
            GoOns.HeldSaying(waiting, gone);
            return Hold(gone.Note);
        }

        // Clean tree, or nothing: uncommitted changes are somebody's work in flight (D46 §3). No
        // session record exists yet, so a hold here costs nothing and destroys nothing. VACUOUS for a
        // repository opted into session trees (D51 rule 5) — a fresh tree is clean by construction,
        // and the person's work in flight in the root no longer holding the driver is the point of
        // the whole decision.
        if (!isolated)
        {
            var (clean, detail) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
            if (!clean)
            {
                return Hold(detail);
            }
        }

        // The harness, and which account it runs as (D49 §4) — asked BEFORE the record is opened, in
        // the same place and for the same reason as the clean-tree rule above: a session record for a
        // spawn that could never have happened would hold the repository and explain nothing. A
        // missing binary and a logged-out profile are the same shape of answer, and each names the
        // action that fixes it.
        // Held, never thrown (REV3): an adapter name the roster does not know — a typo in `driver.json` —
        // threw from here out of the tick, every tick, losing the report's holds and considerations
        // with it. The intake's twin already held on the same sentence.
        HarnessSelection selection;
        ResumeChoice? resume = null;
        // 🔴 LIMITRESUME1b (D206; the limit-resume design §3.3): the record an account's limit ended, which this start resumes in its own
        // conversation where its own account is ready again, decided here inside the carry-on's own start, never beside the look (round 2's
        // B1): the same look would plan the carry-on, and its walk land briefed on the account just freed. A hand-over made from it is the
        // person's press and decides the start instead (step 0).
        var limitedLine = LimitResume.Line(start, handedFrom);
        // Whether this start resumes that record's own conversation on its own account: the limit's intent, separate from any words waiting.
        var limitResume = false;
        try
        {
            if (GoOns.ResumesOwn(start, agent) is { } words)
            {
                // A resume asks for its record's own account (MSG1g, D137 §2.2): its conversation lives in that account's
                // configuration home (D131 §1), so the walk that chooses a start's account never chooses a resume's. A limited record's
                // words go where its line goes, never waiting on its cooling account while another could take them (step 2).
                resume = await Context.Harnesses
                    .ResumeAsync(
                        agent, config, start.Workspace, words.Profile, StartKind.Driven, Context.NewSessions.Covers(words), ct: ct,
                        limited: limitedLine is not null, model: words.Asked?.NamedModel)
                    .ConfigureAwait(false);
                selection = resume.Selection;
                limitResume = limitedLine is not null && resume.Own == NextHold.Ready && selection.Allowed;
            }
            else if (!start.GoesOn && limitedLine is not null && GoOns.LimitResumes(limitedLine, agent)
                     && await Context.Harnesses
                             .ResumeAsync(agent, config, start.Workspace, limitedLine.Profile, StartKind.Driven, walks: false, ct: ct, limited: true,
                                 model: limitedLine.Asked?.NamedModel)
                             .ConfigureAwait(false) is { Own: NextHold.Ready, Selection.Allowed: true } own)
            {
                // Step 1: its own account ready, its maker's reading asked, the judge passing and a door known to fail closed, so its
                // own conversation resumes there, asked before the walk, so its own account wins where another is ready too.
                resume = own;
                selection = own.Selection;
                limitResume = true;
            }
            else if (handedQuest is null && start.Resumes is null)
            {
                // A new quest's first start walks Build's order, choice by choice, each choice's accounts before the next (MODELROLE1c,
                // D205 §4.1): its agent is the entry the walk took.
                var walked = await WalkBuildPassesAsync(start, registry, ct).ConfigureAwait(false);
                (selection, chosen, buildPasses) = (walked.Selection, walked.Chosen, walked.Passes);
                agent = chosen?.Agent ?? agent;
            }
            else
            {
                // A carry-on and a first start take the walk as any start does (D130 §16.2): no context stays with an account. A
                // handed quest asks for the account its hand-over named first, and the walk goes on within the named agent's, its model
                // Build's entry for that agent (D205 §4.0, `handed`). A carry-on keeps its record's asked choice (§3.5(a)).
                // Each account judged by its reading for the model: the carry-on's record's asked, a handed quest's Build entry (§3.5(a), (d)).
                if (handedQuest is not null && start.Resumes is null) chosen = ActorChoices.Effective("build", config, handed: agent)[0];
                var model = chosen?.Model ?? start.Resumes?.Asked?.NamedModel;
                (selection, passedAccount) = await Context.Harnesses
                    .SelectHandedAsync(agent, config, start.Workspace, handedQuest?.Account, StartKind.Driven, ct, model)
                    .ConfigureAwait(false);
            }
        }
        catch (DriverException error)
        {
            return AgentGone() ?? Hold(error.Message);
        }

        if (resume is { Waits: true })
        {
            // The words wait for the account's reset (MSG1g, D125 §2), held as any start on a cooling account is, so the look
            // says the wait once and a screen shows the quest waiting for an account; the record's conversation says why, with
            // the door out of the wait. Nothing was spawned or probed to learn it.
            var why = DriverContinuations.WaitsFor(quest, start.Resumes!, resume.Selection);
            GoOns.HeldSaying(start.Resumes!, ResumeWords.NotYetOn(resume, DriverContinuations.DoorOf(quest, start.Resumes!)));
            return Hold(why) with { Cooling = resume.Selection.Cooling };
        }

        if (!selection.Allowed)
        {
            // LIMITRESUME1b (the design §3.3 step 3): a line a limit cut off, with no account it may run on ready, waits with a sentence
            // of its own naming the earliest reset and whose account, said on its record once per wait; the next look past it takes it.
            if (limitedLine is not null && selection.Cooling is { } earliest)
            {
                var waits = LimitResume.Waits(earliest, Context.Harnesses.Zone, AccountNames.Of(home, earliest.Agent));
                GoOns.HeldSaying(limitedLine, waits);
                return Hold(waits.Note) with { Cooling = earliest, SignedOut = selection.SignedOut };
            }

            // A cooling account's hold carries its cool-off (TOOL4d), so the look says the wait once and a screen shows
            // the quest waiting for an account; and the accounts it passed not signed in (TOOL6g), so both say the sign-in.
            return Hold(selection.Refusal!) with { Cooling = selection.Cooling, SignedOut = selection.SignedOut };
        }

        // The access setup this start runs under (AUTONOMY1a, D184 point 1), resolved once here from the file this look read, so its
        // record keeps the one it started under, as D183 keeps Ask Daoris's mode. Its permission prompts name the harness's mode
        // (AUTONOMY1b, D184 point 3); one its harness has none of is held here, before any tree grows or any record opens, naming the
        // harness (D183 point 1), never run in its own mode under that label. A resume goes on under the same.
        var access = AccessRules.Resolve(config, quest.To, start.Workspace);
        DrivenAccess permissions;
        try
        {
            permissions = DrivenAccess.For(access, quest.To, Context.Adapters.Resolve(agent));
        }
        catch (DriverException error)
        {
            return AgentGone() ?? Hold(error.Message);
        }

        if (permissions.Refusal is { } unsupported) return Hold(unsupported);

        // The session's own tree, where the repository opted in (D51) — grown BEFORE the record for
        // the same reason every refusal above is asked before it: a failure here holds the quest
        // open and records nothing. The branch grows from the canonical line as WSP4 resolves it.
        // A RESUME (D79) carries on in the tree its earlier session asked from — that tree holds the work
        // done before the question — where it still stands, or made again below on its branch (TREEGONE1). One whose work
        // landed and whose branch the tidy took grows a fresh tree like a first start. A CARRY-ON after a
        // cut-off (D80) is the same move for a different reason: the quest waits on nothing, and the
        // earlier session failed before closing it.
        // TREEGONE1 (D51, D131): a session never runs with its tree gone. The tree a session goes on in is asked for as git lists it,
        // on its branch, before anything goes on there: made again in its place where its folder is gone and its branch stands, and
        // said; where its branch is gone too, or it could not be made, the start is held for the person and said, and no harness
        // starts in the checkout or a folder left behind in its place. Asked with the repository's trees held, as an open is (LEFT2).
        // A session whose work landed and whose branch the tidy took grows a fresh tree as before (D79, D82).
        var priorTree = start.Resumes is { Tree: { Length: > 0 } resumesIn } && Context.Trees.Holds(resumesIn) ? resumesIn : null;
        var entryTree = isolated && start.Entry?.Into is { Tree: { Length: > 0 } partTree } ? partTree : null;
        TreeStanding? priorStands = null, entryStands = null;
        if (priorTree is not null || (entryTree is not null && Context.Trees.Holds(entryTree)))
        {
            using var returning = TreeLock.TryStarting(home, start.Workspace, quest.To);
            if (returning is null) return Hold(TreeLock.Replaying(quest.To));
            if (priorTree is not null) priorStands = await Context.Trees.EnsureAsync(root, priorTree, ct).ConfigureAwait(false);
            if (entryTree is not null && Context.Trees.Holds(entryTree))
            {
                entryStands = await Context.Trees.EnsureAsync(root, entryTree, ct).ConfigureAwait(false);
            }
        }

        if (priorStands is { Said: { } priorSaid } && start.Resumes is { } goneFrom)
        {
            var landedAway = priorStands.Stand == TreeStand.Gone && Context.Trees.Recorded.Landing(goneFrom.Session) is not null;
            if (priorStands.Stand == TreeStand.Remade) TreeSaid(goneFrom.Session, priorSaid);
            else if (!landedAway)
            {
                GoOns.HeldSaying(goneFrom, priorSaid);
                return Hold(priorSaid.Note);
            }
        }

        if (entryStands is { Stand: TreeStand.Remade, Said: { } entrySaid }) TreeSaid(start.Entry!.Into!.Session, entrySaid);
        else if (entryStands is { Stand: TreeStand.Unmade, Said: { } entryUnmade }) return Hold(entryUnmade.Note);

        static bool StandsNow(TreeStanding? stands, string tree) =>
            stands is null ? Directory.Exists(tree) : stands.Stand is TreeStand.Stands or TreeStand.Remade;
        var resumedIn = start.Resumes is { Tree: { Length: > 0 } resumedTree } && isolated && StandsNow(priorStands, resumedTree)
            ? resumedTree
            : null;
        // 🔴 SAYHELD1: words going on are never a resume (D79), whatever question the quest still names: the planner plans them only once
        // a resume began (its ResumedOnAnswer), and that session was handed the answer.
        var resuming = start.Resumes is not null && !start.GoesOn && quest.Awaits is { Length: > 0 };
        var carryingOn = start.Resumes is not null && !resuming;
        // A further part of a session (SESSONE1d, D167 point 4 as amended) goes into the tree the planner found free, where it still
        // stands; one the tidy took at its landing grows again below, from the branch that work landed on (D82), else from the line.
        var joinedIn = entryTree is not null && StandsNow(entryStands, entryTree) ? entryTree : null;

        // What the question came back with, which is the whole point of resuming: the session is handed
        // it. Read before the record opens, like every hold above — a question the service no longer
        // knows leaves nothing to resume with.
        QuestView? answered = null;
        if (resuming && quest.Awaits is { Length: > 0 } question)
        {
            answered = await service.FindQuestAsync(question, ct).ConfigureAwait(false);
            if (answered is null)
            {
                return Hold($"`#{quest.Id}` waited on `#{question}`, which this service does not hold — "
                    + "there is no answer to resume with.");
            }
        }

        // 🔴 SAYHELD1 (A), the owner's case on the install: the words kept on the session that asked while its quest waited, which it never
        // took, go to the session this resume opens, read as the record holds them now (SAIDBACK1f), so one taken back never goes.
        // SESSWATCH1a: and the person's words that waited for a run they ended with End and carry on go to the part that carries it on.
        var handsOn = answered is not null || (carryingOn && Observation.IsQuietPressed(start.Resumes!.NoteParts))
            ? await GoOns.ResumeWordsAsync(start.Resumes!, ct).ConfigureAwait(false)
            : null;

        // LANDSYNC2 (D87's 2026-10-15 amendment point 7): a session going on in a tree that stands — a resume, words going on, a
        // carry-on, a further part gone into its session's tree — starts from the line where the line came to hold some of its commits,
        // and is told what was done. Asked before this start's own hold below, since bringing a tree up to date holds the repository's
        // trees alone; never a reason to hold the start.
        // FABRIC-CLOSE1c5 (§E5): the person went on with a step whose source they reopened, so its tree is first brought up to date
        // with the source's new submission, then with the line; a conflict leaves both, and the session is told. FABRIC-CLOSE1c13: so
        // is one whose session had ended normally, going on with the words Daoris told it of the go-on (ReopenTelling). SYNCMERGE1b: and
        // a stand-down whose lost take the person brought forward, onto the step's current source, which its fresh take was made on.
        var inSync = resumedIn is { } reopenedIn && start.Resumes is { } goesFrom
                     && (ReopenTelling.BringsToSource(quest, goesFrom) || Planner.BringsForwardToSource(quest, start))
            ? await GoOnInSyncAsync(reopenedIn, quest, ct, broughtForward: start.BroughtForward is not null).ConfigureAwait(false)
            : (resumedIn ?? joinedIn) is { } goingOn ? await StartInSyncAsync(goingOn, ct).ConfigureAwait(false) : null;

        // 🔴 WSR6's open window (LEFT2): bringing the repository up to date must not rebase a tree a session is
        // starting in. Its trees are held, beside any other start, from choosing the tree until the record is open,
        // when the ledger holds it; a replay holding them holds this start for one look. Let go right after the
        // open below; the `using` lets go on every hold and throw between.
        using var starting = isolated ? TreeLock.TryStarting(home, start.Workspace, quest.To) : null;
        if (isolated && starting is null) return Hold(TreeLock.Replaying(quest.To));

        // STARTGIT1: git's guess at the checkout's line is asked once for this start, by the tree's opening and by the landing it
        // tells the session of, which each asked it afresh (three `symbolic-ref origin/HEAD`, up to four `rev-parse --verify`).
        // Let go before the spawn: a session runs for minutes, and what follows it asks git as it stands then.
        var oneLook = Context.Trees.OneLook();

        TreeOpened? opened = null;
        if (isolated && resumedIn is null && joinedIn is null)
        {
            try
            {
                // A chain's next step in its parent's repository grows from the branch the parent
                // landed on (CHAIN2), named as the trees name their branches — so a verify step sees
                // the unmerged work it exists to check. Where a tidy took that branch at the parent's
                // landing, it grows from the chain's landed branch instead, so its own done can move
                // that branch on (LAND2c, D82 as D145 amends it).
                opened = await Context.Trees.OpenAsync(
                        root, quest.To, start.Workspace ?? "default", ct,
                        // TREEFOLLOW1: the step before's branch as its tree has it now, where its agent moved it onto one of its own.
                        from: start.BuildsOn?.Tree is { Length: > 0 } parentTree
                            ? await Context.Trees.BranchNowAsync(parentTree, ct).ConfigureAwait(false) ?? $"daoris/{Path.GetFileName(parentTree)}"
                            : null,
                        landed: start.BuildsOn is { } stepBefore
                                && Context.Trees.Recorded.Landing(stepBefore.Session) is { GoneAt: null } chain
                                && string.Equals(chain.Repository, quest.To, StringComparison.OrdinalIgnoreCase)
                            ? chain.Branch
                            : entryTree is not null ? EntryLanded(start.Entry!, quest.To) : null,
                        partOf: entryTree is not null ? start.Entry!.Into!.Session : null)
                    .ConfigureAwait(false);
                fresh.Opened(opened.Path);
            }
            catch (DriverException error)
            {
                return Hold(error.Message);
            }
        }

        var workTree = opened?.Path ?? resumedIn ?? joinedIn ?? root;

        // FABRIC-CUTOVER1g (D190's binding moment, the cutover design §8; amends D157 point 11): the run is bound here, once its tree is
        // open and before its take names a closure graph below, so the take names the graph its run is bound to and no look binds it later
        // to something else. A start held before this binds nothing; one held or refused after it keeps its run bound to what it chose, so
        // a choice changed since applies to the next run. A bind not made, or a binding that does not read, holds the start, said, and the
        // next look tries again, its fresh tree let go as a refused open's is.
        var runBound = await BindAtStartAsync(quest, start.Workspace, ct).ConfigureAwait(false);
        if (runBound.Holds)
        {
            await fresh.LetGoAsync().ConfigureAwait(false);
            return Hold(runBound.Why!);
        }

        // FABRIC-CUTOVER1c (the cutover design §3): a run bound already to a named version may be a graph run that ended and whose chain has come
        // back here; the runtime is asked, and re-arms for it until a pass judges it where its record holds its end.
        if (runBound is { Came: WorkflowBindCame.Already, Run: { } returned, Binding.IsCurrent: false }) RunReturned?.Invoke(returned);

        // A set-up step follows its repository's own procedure, which its tree must hold (REVIEWENV1c, design §2.1): checked
        // here, once the tree exists, and held before any record, a fresh tree going as a refused open's does.
        var setUp = ReviewSetUps.Environment(config, quest, start.Workspace);
        if (setUp is not null && ReviewSetUps.ProcedureMissing(setUp, quest, workTree) is { } unfollowable)
        {
            await fresh.LetGoAsync().ConfigureAwait(false);
            return Hold(unfollowable);
        }

        // A local set-up step's tab, opened before its session so its instruction names a tab the person sees (REVIEWENV1d,
        // design §2.3 step 1): a tab an agent opens over CDP has no window. A browser that will not open one holds the start.
        if (setUp is not null && Reviews is { } desk && await desk.OpenForStepAsync(quest, setUp, ct).ConfigureAwait(false) is { } unshowable)
        {
            await fresh.LetGoAsync().ConfigureAwait(false);
            return Hold(unshowable);
        }

        // 🔴 Can this harness use what the repository allows it? (DEPLOY1.) Claude Code ignores a
        // repository's `permissions.allow` until a person has accepted that path, so a session in an
        // untrusted tree does the work and then cannot take or close the quest it exists to serve —
        // measured three times, nine minutes and a real login each. Asked HERE, with the other
        // pre-spawn holds, because the whole value is not spending that.
        //
        // Held rather than failed: nothing is wrong with the quest, and one command fixes it.
        // Resolved defensively: an unknown adapter name is its own error with its own sentence,
        // reported where it already was, so this check simply does not run for one.
        //
        // 🔴 Only where the connector's allowance would NOT reach the session (D73). The rules Daoris
        // hands over at spawn are honoured in an untrusted folder on both doors (measured, PERM1), so
        // a session handed the `connector` default takes and closes its quest whoever trusted what,
        // and trust then decides only whether the repository's OWN allow-list counts.
        ISessionAdapter? preflight = null;
        try { preflight = Context.Adapters.Resolve(agent); } catch (DriverException) { }

        // Asked of the account the session runs as, and again of the walk's pick where a resume's words go to a new record below.
        StartRun? Untrusted(HarnessSelection chosen)
        {
            if (preflight is not { Toolchain.TrustFile: { Length: > 0 } trustFile }
                || HandedConnector(preflight, start.Workspace, quest.To, "quest_respond"))
            {
                return null;
            }

            var trustPath = ClaudeTrust.PathFor(trustFile, chosen.ProfileHome, preflight.Toolchain!.ProfileVariable);
            if (ClaudeTrust.Accepted(trustPath, workTree) != false) return null;

            // Said twice, on purpose: the sentence for a person, the fact for a screen (D73). With no account home its file is
            // the own sign-in's, which Daoris never writes (POOL1a3, D197 point 3): the screen offers no grant of it.
            lock (untrusted) untrusted.Add(new TrustHold(workTree, trustPath, Quest: quest.Id, Own: chosen.ProfileHome is null));
            return Hold(ClaudeTrust.Refusal(workTree, chosen.ProfileHome is null ? null : chosen.Profile));
        }

        if (Untrusted(selection) is { } untrustedHold) return untrustedHold;

        // An answer continues its own session (ANSWER1a, D131), and so do words to a session that had ended (MSG1b, D137
        // §2.2): the record they wait on goes on itself where its harness conversation resumes. Otherwise a park ends, and
        // the words are handed on below in a new session, saying why; an answer a service from before ANSWER1b ended the
        // record with is tried, and said `ended`.
        ContinueReason? fellBack = null;
        var answerKept = false;

        // 🔴 LIMITRESUME1b (the design §3.3 step 1, round 3's B8 item 4): Daoris's word is kept on the limited record only here, just
        // before it goes on, after every hold between (access, tree, replay, trust), so a held start leaves no word on it. The person's
        // words, where a GoOn carries any, are already on it, so theirs go first and Daoris's last. A word the door would not keep leaves
        // the start the carry-on it was planned.
        var planned = start;
        string? limitWord = null;
        if (limitResume && await GoOns.KeepLimitWordAsync(start.Resumes!, ct).ConfigureAwait(false) is { } keptWord)
        {
            limitWord = keptWord.Word;
            start = start with { Resumes = keptWord.Record, GoesOn = true };
            if (keptWord.Record.Limit) LimitResumed[quest.Id] = keptWord.Record.Session;
        }
        else if (limitResume && !start.GoesOn)
        {
            limitResume = false;
            resume = null;
        }

        var written = start.Resumes;
        if (start.Resumes is { } park
            && (start.GoesOn || park is { State: "completed", Answer: not null, Said: null })
            && !resuming)
        {
            // Where its own account could not carry the words, the walk's pick did, and the reason says why (MSG1g).
            (var continued, fellBack, answerKept) = await ContinueAsync(
                    start, park, selection, registry, starting, onOpened, ct, agent, account: resume?.Elsewhere, inSync: inSync?.Told,
                    permissions: permissions, treeStands: priorStands is null or { Stand: TreeStand.Stands or TreeStand.Remade })
                .ConfigureAwait(false);
            // The fresh tree opened above is used by none of the returns that follow, so each lets it go by the clean path (GOONTREE1,
            // D51): a tree and branch for a start that is never made would be left at every look. On no token (QUEUEDTREE1): a
            // continuation returns as the driver closes, and a removal on the closed token would leave it to the sweep.
            if (continued is not null)
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                return continued;
            }

            // 🔴 LIMITRESUME1b (the design §3.3, round 3's B8 items 1-3): its own conversation refused, before the wire or on it, Daoris's
            // word comes off by its id and the line goes on in this same start as the carry-on that was planned, briefed, the person's
            // words after the brief as theirs, said with the refusal's own code. Another client holding the conversation keeps the word,
            // marked with the rest below, so the line is held for the person and never spins.
            if (limitWord is not null && !fellBack!.Holds)
            {
                await GoOns.TakeLimitWordsOffAsync(park.Session, [limitWord], ct).ConfigureAwait(false);
                LimitResumed.TryRemove(quest.Id, out _);
                var refusedOwn = LimitResume.Refused(fellBack);
                Context.Output?.Append(park.Session, refusedOwn.Note);
                Context.Events.Keep(
                    park.Session, new SessionEvent { Kind = SessionEventKind.Note, Text = refusedOwn.Note, Parts = refusedOwn.Parts }, say: null);
            }

            // 🔴 SAIDBACK1f (D137 §2.4): the words as the record holds them now, not as the look read them, so a word taken back
            // since never goes to the new session; none left, nothing is sent. A limit's intent carries the line on all the same.
            park = await GoOns.HandedOnRecordAsync(park, ct).ConfigureAwait(false);
            if (!park.Parked && !park.Running && !park.WordsWaiting && limitWord is null)
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                return DriverContinuations.TakenBackFirst(quest, park.Session);
            }

            written = park;

            // Nothing carries the words on by itself from a closed quest's session, nor from a never (D137 §2.2): they stay
            // waiting as said, marked so a later look leaves them there, and the person's press is the next move (MSG1f). 🔴 SESSCTX1c
            // (D189's amendment): nor where another client holds its conversation (`elsewhere`), since a second writer in its tree could
            // redo its work; every other refusal carries a driven record on below, handed the brief, `from` why `retry`.
            if (fellBack!.Never || fellBack.Holds || quest.Status is not ("Open" or "Taken"))
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                // 🔴 SESSCTX1c part B (D189 point 3; the steward's choice: a closed quest opens no driven record): a closed quest's session
                // whose own conversation is proven lost goes on by itself as a conversation in its tree, through the shell's chat runner,
                // handed the brief and the person's words. Anything else, and a host with no runner, waits for the person as before.
                if (!fellBack.Never && !fellBack.Holds && fellBack.Lost && Context.GoesOnLost is { } goesOn
                    && await GoOns.GoesOnLostAsync(goesOn, quest, park, fellBack, ct).ConfigureAwait(false) is { } moved)
                {
                    return moved;
                }

                return await CannotGoOnAsync(quest, park, fellBack, ct).ConfigureAwait(false);
            }

            // Another agent's findings go on only in their own session (XAGENT1e, design §6.7): where they are all that waits,
            // nothing is carried on, and they go to the person. The person's words beside them are carried on as ever.
            // A helper's answer is the same (MIXAGENT2a-ii): another agent's claim, never carried to a new session as the person's.
            // So are Daoris's own words (COWORK1b): a message of Daoris's never starts a new session (the message-urgency design §4.1).
            if (park.Waiting.Any(word => !word.Persons) && !park.Waiting.Any(word => word.Persons))
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                return await CannotGoOnAsync(quest, park, fellBack, ct).ConfigureAwait(false);
            }

            // A taken quest is carried on, an open one started, handed the words, in the tree the session worked in. A limited line with
            // none of the person's words left goes on as the carry-on planned for it (LIMITRESUME1b).
            start = start with
            {
                Resumes = park.Parked || park.Said is null ? park with { State = "completed" } : park,
                GoesOn = limitWord is not null && !park.WordsWaiting ? planned.GoesOn : start.GoesOn,
            };

            // 🔴 OWNSPACE1a (D175 point 3): only the record that began in the tool's own home goes on there. Its words carried to a
            // new record are a new start, which takes the walk (D130 §16.2) and holds where the walk holds, before the record opens:
            // never a new session in the person's home.
            if (resume is not null)
            {
                var resumed = selection;
                try
                {
                    (selection, var passed) = await Context.Harnesses
                        .CarriedOnAsync(resume, agent, config, start.Workspace, handedQuest?.Account, ct, start.Resumes?.Asked?.NamedModel)
                        .ConfigureAwait(false);
                    passedAccount ??= passed;
                }
                catch (DriverException error)
                {
                    await fresh.LetGoAsync().ConfigureAwait(false);
                    return Hold(error.Message);
                }

                if (!selection.Allowed)
                {
                    await fresh.LetGoAsync().ConfigureAwait(false);
                    return Hold(selection.Refusal!) with { Cooling = selection.Cooling, SignedOut = selection.SignedOut };
                }

                if (!ReferenceEquals(resumed, selection) && Untrusted(selection) is { } walkedUntrusted)
                {
                    await fresh.LetGoAsync().ConfigureAwait(false);
                    return walkedUntrusted;
                }
            }
        }

        // A resume that was tried let go of the starting hold (LEFT2) when the park held the tree; the carry-on's open below
        // needs it again, until its own record holds the tree.
        using var restarting = isolated && answerKept ? TreeLock.TryStarting(home, start.Workspace, quest.To) : null;
        if (isolated && answerKept && restarting is null) return Hold(TreeLock.Replaying(quest.To));

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
            pressed: start.BroughtForward is not null || (start.Resumes?.PersonStopped == true && !handedFrom));

        // The ledger judges the open — the same door any other client would use. A refusal here is
        // an answer (someone else got there first), not an error. Once asked, the ledger may hold the
        // tree even where a close cuts its answer off, so a close from here leaves it (QUEUEDTREE1).
        fresh.Asked();
        var (sessionId, message) = await service
            // The tree this spawn will hold (D51) — its own where the repository opted in, the
            // registered root otherwise. Stated rather than left to the service to infer, because
            // this side is the one that knows where it is about to run a process.
            .OpenSessionAsync(
                quest.Id, agent, selection.Version, selection.Profile, workTree, before, ct,
                // Whether this is a set-up, for the machine log alone, so a set-up's cost can be read back (WSSETUP11).
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
        if (chosen is not null) RotatedOpening.SayActor(Context.Events, sessionId, "build", chosen, buildPasses);

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
            // session's conversation, where the person wrote them.
            if (fellBack is not null) await GoOns.HandedOnAsync(written!, sessionId, fellBack, ct).ConfigureAwait(false);
            // SAYHELD1 (A): and those said to the session that asked while its quest waited go to its resume, the same way.
            else if (handsOn is not null)
            {
                await GoOns.HandedOnAsync(handsOn, sessionId, ContinueWhy.Of(ContinueWhy.Resumed), ct, resumed: true).ConfigureAwait(false);
            }

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
            var elsewhere = prior is not null && OnAnotherAccount(prior, selection.Profile);
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
            var target = WithAccess(WithLanguage(WithStanding(
                await WithAskWordsAsync(SessionTarget.ForQuest(quest, workTree, service.BaseUrl), service, ct).ConfigureAwait(false),
                config), config, start.Workspace), access) with
            {
                // The mode its repository's permission prompts name on its harness (AUTONOMY1b), on either door.
                Access = permissions,
                // MODELROLE1c (D205 §3.2): what the entry the walk took names, carried on every run; a continuation keeps its record's.
                Asked = chosen?.Asked ?? (start.Resumes is { } carried ? carried.Asked : ActorAsked.Before(adapter.Name)),
                Choice = chosen?.From,
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
                // where the remote helper is laid out for it, what `git remote -v` shows under the rewrite (GITAUTH1b, the design §5.3).
                Git = GitHolds.Told(
                    GitHolds.OfRepository(home, quest.To, RemoteTarget.Workspace(start.Workspace)),
                    rewritten: GitWiring.DoorPath(home, GitWiring.RemoteHelper) is not null,
                    origin: GitWiring.DoorPath(home, GitWiring.RemoteHelper) is null
                        ? null
                        : await GitRemotes.AddressOf(workTree, "origin", ct: ct).ConfigureAwait(false)),
                // What its tree's open copied of the checkout's dependencies, so it skips the `npm ci` that would delete them (TREEDEPS1).
                Dependencies = opened?.Dependencies,
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
                PersonSaid = carryingOn ? start.Resumes!.Answer : null,
                // SAYHELD1 (A): a resume's, the words said to the session that asked while it waited, each under its id.
                Handed = handsOn is null ? [] : [.. handsOn.Waiting.Where(DriverContinuations.HandedToResume)],
                // The person's stop, which they released with Try again (SESSUX1b): told so, never that it was cut off.
                Released = carryingOn && start.Resumes!.PersonStopped && !handedFrom,
                // The quest was handed over from that session (MIXAGENT1a-ii): told so, with the agent it ran on.
                BroughtForward = carryingOn && quest.Status != "Open" && start.BroughtForward is not null,
                HandedOver = carryingOn && handedFrom ? start.Resumes!.Adapter ?? "another agent" : null,
                Brief = brief,
                // MIXAGENT1c: the note its agent left where it asked to hand over, told as that agent's claim; the brief carries it.
                HandedNote = handedNote,
                // Another agent's accounts are not this agent's: a hand-over says no account was cooling for it to be told of.
                AccountChanged = elsewhere && !handedFrom && prior!.Limit && Context.Harnesses.CoolingOf(agent, prior.Profile) is not null,
                // A set-up step is told its environment as the rule declares it now (REVIEWENV1c, design §2.2), and its account
                // says where that rule was set (REVIEWENV1c2).
                SetUp = setUp,
                SetUpFrom = setUp is null ? null : ReviewRules.Resolve(config, quest.To, start.Workspace)?.Source,
            };
            oneLook.Dispose();
            var (info, harnessNotice) = Prepare(adapter, target, selection);
            await KeepHandedAsync(sessionId, info, ct).ConfigureAwait(false);

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
            if (carryingOn && !answerKept && start.Resumes!.Answer is { Length: > 0 } answeredWith && written is { Parked: true } or { Said: null })
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
            var rules = HandRules(
                adapter, info, sessionId, start.Workspace, quest.To, workTree, target.AttachmentsDirectory, across: across, plugins: skillPlugins,
                continued: brief?.Kept, heavy: heavy);

            Context.Runs.Live[quest.Id] = sessionId;
            using var live = new Disposer(() => Context.Runs.Live.TryRemove(quest.Id, out var _));

            return await HoldAsync(
                "build", adapter, info, target, sessionId, transcript, workTree, JoinNotices(harnessNotice, browserNotice), rules, handed,
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
                goOn: GoOns.GoOnWith(adapter, target, selection, rules.File, handed),
                stopMade: stop => stoppedFirst = stop,
                // The part works: what it was handed from the ask's channel is acknowledged by its id (SESSONE2b, COWORK1d).
                working: channelHeld is null ? null : () => _ = ChannelReach.HandedAsync(channelDoors, channelHeld, CancellationToken.None),
                // BGWAIT1: a turn that ends on the session's own background work, holding its quest as a park would, is not
                // a question. Read from the quest as the conclusion reads it, by the conclusion's own rule.
                waitsOnBackground: WaitsOnBackground(service, quest, sessionId, resumed: start.Resumes is not null),
                // MODELROLE1c2 (D205 §3.3): its agent's refusal of the entry's model at the open is re-asked; a new start's walk may answer a
                // later entry on the same agent and account, set on this session. A continuation keeps its asked choice and a handed quest walks
                // its named agent's accounts alone (§3.5(a), (d)), so their record ends.
                offers: new OpenOffers(
                    selection.Version, selection.Profile,
                    chosen is { } walkedEntry && handedQuest is null && start.Resumes is null
                        ? _ => ReaskAsync(start, registry, walkedEntry, selection, ct)
                        : _ => Task.FromResult<(ActorAsked Asked, string Choice)?>(null)),
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

                    conclusion = AccountRefused(conclusion, adapter, selection, ended);
                    conclusion = AccountSignedOut(conclusion, adapter, selection, turnFailed);
                    (conclusion, var limited) = AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);

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

                    // LAND2b: a done under a rule that accepts automatically is due, and a later look lands it beside itself. FABRIC-CLOSE1h
                    // (§E5): in a closure graph its submission reads as that done, so it is due at the agent's `ready`.
                    await ConcludedForLandingAsync(sessionId, quest, QuestResults.Status(after), conclusion.State, workTree, start.Workspace, ct)
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
                                Quest: quest.Id, Adapter: adapter.Name, Account: selection.Profile) { Limit = limited, NotOffered = notOffered });
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
        var info = prepared ?? adapter.Prepare(target with { Home = target.Home ?? DaorisHome.Resolve(Variable) }, config.Commands.GetValueOrDefault(adapter.Name));

        // The environment seam every harness already carries for exactly this (D49 §4). Applied
        // by the driver rather than inside the adapter's Prepare, so one line governs both doors
        // and no adapter can forget it.
        if (adapter.Toolchain is { } toolchain)
        {
            HarnessProbe.Apply(
                info, toolchain, selection.ProfileHome, selection.Binary, selection.ClaudeExecutable,
                selection.Environment);
        }

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
            brainstorm is not null ? [.. PermissionRules.HardDeny(rules), BrainstormPermissions.HardDeny]
            : partner ? [.. PermissionRules.HardDeny(rules), PartnerPermissions.HardDeny]
            : helper ? [.. PermissionRules.HardDeny(rules), OpinionPermissions.HelperHardDeny]
            : opinion ? [.. PermissionRules.HardDeny(rules), OpinionPermissions.HardDeny] : [.. PermissionRules.HardDeny(rules), .. heavyTold],
            // A partner is held to its copy whatever the person's defaults say (COAGENT1b, the co-agent design §7.4).
            PermissionRules.GuardsTree(rules) || partner ? TreeGuard.For(home, tree, across?.Writes.Select(target => target.Path)) : null,
            // Which agent made each call, and each subagent's start and stop (FABRIC-C1a): the part's children bound by its lineage.
            ChildLineage.HookFor(home, sessionId));
        var handed = RulesHanded(takes: true, rules, composed, workspace, repository);
        if (file is null) return (null, null, handed);

        if (adapter.Wire == SessionWire.Pipe)
        {
            adapter.HandSettings(info, file);
            return (file, null, handed);
        }

        return (file, adapter.AcpSessionMeta(file, plugins ?? []), handed);
    }

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
    internal (SessionConclusion Conclusion, bool Limit) AccountLimited(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, string sessionId,
        AcpUsage? used)
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
            limited = Context.Harnesses.Limited(adapter.Name, selection.Profile, turnFailed, sessionId, config.CoolOff);
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

        var owner = refusing.Owner(adapter.Name);
        // The hold a later start says names the account by the person's name, read now (ACCT2b); the note, which travels and
        // whose cleaner knows only the record's own id, keeps the id.
        var named = selection.Profile is { } profile ? AccountNames.Said(AccountNames.Of(Context.Harnesses.Home, owner), profile) : null;
        var hold = RefusedAccounts.RefusedHold(owner, selection.Profile, named);
        Context.Harnesses.Refuse(adapter.Name, selection.Profile, hold.Note, hold);
        Context.Harnesses.SignedOut(adapter.Name, selection.Profile);
        return conclusion.Then(" ", RefusedAccounts.RefusedNote(owner, selection.Profile));
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
    internal SessionConclusion AccountSignedOut(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed) =>
        conclusion.State == "failed" && RefusedAccounts.SignInRefused(Context.Harnesses, adapter, selection.Profile, turnFailed) is { } line
            ? conclusion.Then(" ", line)
            : conclusion;

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
    /// The branch a session's work last landed on here (SESSONE1d; D82 by entry rather than by chain): the newest of its parts'
    /// landings in the repository whose record stands, or null where none of them landed on a branch.
    /// </summary>
    private string? EntryLanded(EntryParts entry, string repository) =>
        entry.Parts
            .Select(part => Context.Trees.Recorded.Landing(part.Session))
            .FirstOrDefault(landing => landing is { GoneAt: null }
                                       && string.Equals(landing.Repository, repository, StringComparison.OrdinalIgnoreCase))
            ?.Branch;

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
        var carried = resumes.Answer is not null
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

    /// <summary>
    /// Start a prepared session and hold it to its conclusion — the part a quest's session and an
    /// intake share. REV3's F1 was one defect in two copies of this, fixed twice; CLEAN1 made it one.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>The conclusion runs inside the scope that tracks the process</b>, and must. The stop
    /// flags it reads live in the tracking entry, and a record still saying <c>working</c> with nothing
    /// tracked here is what the orphan sweep concludes for itself.</para>
    ///
    /// <para>🔴 <b>Whatever ends this call ends the agent while it is still tracked</b> — the reaper is
    /// declared after <c>tracked</c>, so it runs first — never working on untracked in a tree just
    /// unlocked (REV3).</para>
    ///
    /// <para>The ACP task is held <b>as its own type</b>: assigning it to a bare <c>Task</c> compiles and
    /// silently discards the outcome, which is where the usage measurement lives (TOOL3).</para>
    /// </remarks>
    /// <param name="conclude">
    /// The exit code (null when the timeout killed it), the usage the door reported (null when it
    /// reported none), the protocol door's failure (ACPEND1), and what the harness itself said of its
    /// ending (AGT3c), to the caller's conclusion — which reads the stop flags and moves the record.
    /// </param>
    /// <param name="said">
    /// Told what the session's door says about its account's windows (TOOL6c), on either door, as it says it.
    /// </param>
    /// <param name="resume">
    /// The conversation an answer continues (ANSWER1a, D131 §1): the run resumes it with the answer as its prompt, appends
    /// to the record's transcript and conversation, and says on it whether the agent would not resume. Null for a start.
    /// </param>
    /// <param name="workingNote">What the record says while it works, where a start's opening sentence is not it.</param>
    /// <param name="goOn">
    /// How a native run goes on with what the person said while it worked (MSG1b, D137 §2.1): its own conversation resumed
    /// with the words, before the record concludes. Null on a door that cannot resume, which holds no words.
    /// </param>
    /// <param name="working">Told once the ledger has moved the record to working, and so holds its tree (LEFT2, MSG1b).</param>
    /// <param name="waitsOnBackground">
    /// On the protocol door, asked when a turn ends while the session's own background work runs (BGWAIT1): whether its
    /// quest stands as a park would leave it, so the session waits for that work instead. Null never waits (D105 §3).
    /// </param>
    /// <param name="minutes">
    /// How long it may run before it is stopped, where that is not the machine's session timeout: a reviewer's pass (XAGENT1d).
    /// </param>
    /// <param name="noConnector">
    /// What the protocol door's transcript says where this machine has no connector, when it is not a quest's or an intake's
    /// sentence: a reviewer's (XAGENT1d).
    /// </param>
    /// <param name="stopMade">
    /// Told, as the run lets go of its process, the stop made of it, or null for none (FLAKE3): what a driver's close that cuts
    /// the conclusion off records in place of itself (<see cref="Observation.Closed"/>).
    /// </param>
    /// <param name="movedFirst">
    /// That the caller moved the record to working before the spawn (SAIDBACK1e, D137 §2.4): a resume whose words are read once
    /// that move is made, so no word is taken back after its prompt is built. The move is not made again here.
    /// </param>
    /// <param name="spawned">
    /// Told once the process started, before anything else here can fail: what tells such a caller a run that never started, its
    /// record to be put back, from one that did (SAIDBACK1e).
    /// </param>
    /// <param name="piece">
    /// A partner's piece's job (COAGENT1b), which its harness joins beneath this process's agents' job and is never started outside
    /// (TERMTRUST1a); null for every other run.
    /// </param>
    internal async Task<T> HoldAsync<T>(
        string actor, ISessionAdapter adapter, ProcessStartInfo info, SessionTarget target, string sessionId, string transcript,
        string cwd, string? harnessNotice, (string? File, object? Meta, HandedSection? Handed) rules, string? handed, string? refusesInput,
        Func<int?, AcpUsage?, string?, HarnessEnding, Task<T>> conclude, CancellationToken ct,
        IReadOnlyDictionary<string, string>? scope = null, string? preamble = null,
        IReadOnlyList<AcpMcpServer>? handedServers = null, bool drivesBrowser = false, Action<JsonElement>? said = null,
        ResumeAsk? resume = null, Noted? workingNote = null, Func<string, string, ProcessStartInfo?>? goOn = null,
        Action? working = null, Func<CancellationToken, Task<bool>>? waitsOnBackground = null, int? minutes = null,
        string? noConnector = null, Action<SessionStop?>? stopMade = null, bool movedFirst = false, Action? spawned = null,
        string? piece = null, OpenOffers? offers = null)
    {
        // Through the start gate (PROCSTART2): a press that starts a session reaches here before its first wait, and the
        // runtime's start lock is no thread's to wait on but the pool's. Seen through once begun, as the start always was.
        // PARALLEL1d: a run that goes on with the person's words (ANSWER1a) is the person's start; every other the loop's.
        // PARALLEL1f: its wait for a place is on the loop's token, so a driver closing ends a start still queued, which throws
        // before `spawned` is told: its caller puts back what it took, or closes its record as the driver's (D104).
        // TERMTRUST1a (D201 point 4): through the launcher, so the harness is born in this process's agents' job, and a partner's in its
        // piece's job beneath it; the process answered is the launcher's, which the doors read as the session's.
        var whose = resume is null ? StartClass.Background : StartClass.Person;
        using var process = await AgentStarts.StartAsync(info, piece is null ? ContainedKind.Agent : ContainedKind.Piece, whose, home, Log, ct, piece)
                .ConfigureAwait(false)
            ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
        spawned?.Invoke();
        // Kept beside the process, so who is driving Daoris's browser lasts exactly as long as it (BRW8).
        using var tracked = Context.Processes.Track(sessionId, process, refusesInput, drivesBrowser);
        using var reaper = new Disposer(() => SessionProcesses.EndIfRunning(process));
        // D205 row b: what this start's record already says, and what it asked, kept as reporting alone — a service that does not
        // answer here costs the record its provenance, never the run.
        PriorSession? prior = null;
        if (target.Brainstorm is null)
        {
            try { prior = await service.RecordAsync(sessionId, ct).ConfigureAwait(false); }
            catch (Exception error) when (error is not OperationCanceledException) { Log?.Write("warn", "actor.read-failed", [("session", sessionId)]); }
        }
        actor = prior?.Actor ?? actor;
        var asked = ActorAsked.ForSpawn(adapter.Name, info, target.Asked ?? prior?.Asked);
        // MODELROLE1c: the entry a new start's walk took (D205 §4.0); a continuation keeps its record's own.
        var choice = target.Choice ?? prior?.Choice ?? "before";
        if (target.Brainstorm is null)
            await service.TryKeepActorAsync(sessionId, Log, choice, asked, ct: ct).ConfigureAwait(false);
        UsageTurns.ActorBound(home, Context.Events, sessionId, actor);
        await using var actorRun = new ActorRun(Context.Events, sessionId, actor, prior?.Ran,
            ran => target.Brainstorm is null ? service.KeepActorAsync(sessionId, ran: ran) : Task.CompletedTask, Log);
        // SESSWATCH1a (D200): a quest's session and an intake are watched while they run, never ended on time; a pass keeps minutes of
        // its own and is not watched (the session-watch design §2.4).
        using var watched = minutes is null && target.Opinion is null && target.Helper is null && target.Brainstorm is null && target.Piece is null
            ? Watch.Watch(
                sessionId,
                new WatchedRun(target.Ask is null ? SessionWatch.Quest : SessionWatch.Intake, target.Ask is null ? target.QuestId : null, adapter.Name)
                {
                    Tree = cwd,
                })
            : null;
        using var servers = new Disposer(() => SpawnServers.Remove(handed));
        using var ruled = new Disposer(() => SpawnSettings.Remove(rules.File));
        // The runs a native session goes on in with the person's words (MSG1b), each kept tracked until the record has
        // concluded: the conclusion reads whose stop it was from the run that ended last. Ended before the first run's.
        var wentOn = new List<IDisposable>();
        using var goneOn = new Disposer(() =>
        {
            for (var at = wentOn.Count - 1; at >= 0; at--) wentOn[at].Dispose();
        });
        // 🔴 The stop made of it, told as the run lets go, whatever ends it (FLAKE3): disposed before the runs above are
        // untracked, so it is read while they still are, for a close that cuts the conclusion off to record that stop.
        using var letGo = new Disposer(() => stopMade?.Invoke(Context.Processes.StopMade(sessionId)));

        // Which door this harness is held over (D53). The protocol door drives an ACP session on the
        // same process; the pipe door reads its text, or its structure where its own wire carries one
        // (D76, CONV3). All of them end the same way: the record is concluded from the exit code and
        // what the session was for, never from what the session said about itself (D46 §4).
        // A resume's prompt is the answer as it is (ANSWER1a): the conversation already holds the target. After it, the answers
        // to the go-aheads it asked since its start (KNOWUSE1a).
        var composed = resume is null ? TargetPrompt.Composed(TargetPrompt.ForStart(target, adapter)) : null;
        var prompt = resume?.Prompt ?? composed!.Text;
        // What the target was composed of, kept beside it on its event with the rules handed beside it (CONTEXT1, D143 point 1);
        // a resume composes nothing, so its record keeps none. Nor a reviewer's (XAGENT1d): its instruction is its own
        // composer's, which the account's sections do not name yet, and an intake's section would say it was one.
        var account = composed is null || target.Opinion is not null || target.Helper is not null || target.Piece is not null || target.Brainstorm is not null ? null
            : rules.Handed is { } rulesHanded ? composed.Account.Beside(rulesHanded)
            : composed.Account;
        // A quest's session keeps the id its harness names, which an answer to a park resumes (ANSWER1a). Never an
        // intake's: it is answered through its ask. Nor a reviewer's: every pass is a fresh conversation (D155 point 6).
        var keepAs = target.OneTurn ? null : adapter.Name;
        // What the person tells a quest's session while it works (SESS3), held for the protocol door to
        // hand over between turns. Never an intake's or a reviewer's: each is one turn framed as one prompt (INT4h, D155 point 5).
        // On the native door, where the run can resume, held until it ends and gone on with in its own conversation (MSG1b,
        // D137 §2.1).
        var held = goOn is not null && adapter.Wire == SessionWire.Pipe && !target.OneTurn ? Context.Processes.OpenInbox(sessionId) : null;
        var inbox = adapter.Wire == SessionWire.Acp && !target.OneTurn ? Context.Processes.OpenInbox(sessionId) : held;
        using var unheld = new Disposer(() => inbox?.Close());
        if (held is not null)
        {
            // In the record the moment they are said, with when they reach it (D136 §3): the run's own transcript is open in
            // its capture, so the line goes to the console and the record, and the transcript has it as the next run opens.
            held.OnSaid((message, _) =>
            {
                output?.Append(sessionId, $"— the person added, which reaches the session when its turn ends: {message.Text}");
                Context.Events.Keep(sessionId, ChatMessage.Words(message) with { Reaches = "turn-end", Level = message.LevelSaid }, say: null);
            });
            // A word the person took back or reworded while it waited is settled, by its id (QUEUEEDIT1a).
            held.OnWithdrawn(message =>
            {
                output?.Append(sessionId, $"— the person took back what they added: {message.Text}");
                Context.Events.Keep(sessionId, ChatTurns.Withdrawn(message.Id!), say: null);
            });
            held.Attach(interrupt: null);
        }
        // ACCESSSWAP1b (D183's note): a driven session's permission prompts are read again from the driver's file at a turn's end where a
        // word is held for the next, so a change made while it runs, on the page or at a terminal, applies at its next turn: the run ends
        // there, its held words handed back to its record, and the next look goes on in the same conversation under the setup standing
        // then (DriverContinuations). Only a session that holds words: never an intake's or a reviewer's, and never Ask Daoris's, whose
        // own swap is ChatRunner's (ACCESSSWAP1).
        SetupSwap? swapped = null;
        var started = inbox is not null && ConfigPath is not null ? target.Access : null;
        Func<bool>? setupChanged = started is null ? null : () => (swapped = SetupNow(started, adapter, ConfigPath!)) is not null;
        // And held beside its inbox while it runs, so the page's press tells its console when the change applies (SET_ACCESS).
        using var setupHeld = started is null ? null : Context.Processes.OpenSetup(sessionId, now => started.SwapIn(now, adapter));
        // BGWAIT2: the timeout is said to the protocol door before the process is killed, so the stream's end it causes is said as
        // the timeout's, never as the agent having exited.
        var ending = adapter.Wire == SessionWire.Acp ? new AcpEnding() : null;
        // MODELROLE1c2 (D205 §3.3, §9.2): what the open lists is kept as its agent's offer reading on its account, and a choice it refuses
        // is kept refused there before the start's re-ask answers: a later choice set on this same session, its record's choice and asked
        // moving with it, or the open ending before its prompt.
        Action<IReadOnlyList<AcpConfigOption>>? offered = offers is null
            ? null
            : options =>
            {
                if (AgentOffers.Offered(home, adapter.Name, offers.Version, offers.Account, Values("model"), Values("thought_level"), DateTimeOffset.UtcNow)
                    is { } unkept)
                {
                    Log?.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
                }

                IReadOnlyList<string>? Values(string category) =>
                    options.FirstOrDefault(option => option.Category == category) is { } option ? [.. option.Choices.Select(each => each.Value)] : null;
            };
        Func<ModelRefusal, Task<ActorAsked?>>? refusedAt = offers?.Refused is not { } reask
            ? null
            : async refusal =>
            {
                if (AgentOffers.Refused(home, adapter.Name, offers.Version, offers.Account, refusal.Value, DateTimeOffset.UtcNow) is { } unkept)
                {
                    Log?.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
                }

                Log?.Write("info", "actor.refused", [("actor", actor), ("session", sessionId), ("agent", adapter.Name), ("model", refusal.Value),
                    ("code", refusal.Code)]);
                if (await reask(refusal).ConfigureAwait(false) is not { } later) return null;
                // The same-account move (D205 §3.3): the record's choice and asked move through their later door, and the opening note
                // says both, the refused entry passed on this account.
                var refusedEntry = new ActorEffective(adapter.Name, refusal.Value, null, choice);
                var ranEntry = new ActorEffective(adapter.Name, later.Asked.NamedModel, later.Asked.Effort.Value, later.Choice);
                var why = $"`{offers.Account ?? "its own sign-in"}` did not offer `{refusal.Value}`";
                RotatedOpening.SayActor(Context.Events, sessionId, actor, ranEntry, [new BuildPass(refusedEntry, refusal.Code, why)]);
                choice = later.Choice;
                await service.TryKeepActorAsync(sessionId, Log, choice, later.Asked, ct: CancellationToken.None).ConfigureAwait(false);
                return later.Asked;
            };
        var acp = adapter.Wire == SessionWire.Acp
            // The posture rides with it, because it is the ADAPTER's (ACP3): three harnesses name the
            // same D37 boundary three different ways, and one of them does not name it on the wire. A driven session's repository's
            // asking or unrestricted permission prompts name the adapter's mode for it, required (AUTONOMY1b, D184 point 3).
            ? CaptureAcpAsync(
                process, transcript, sessionId, cwd, prompt, target.Access?.Posture ?? adapter.AcpPosture, harnessNotice, ct, scope, rules.Meta,
                handedServers ?? Context.Servers, target.PersonSaid, inbox, said, keepAs, resume, account, waitsOnBackground, noConnector, ending,
                unoffered: target.Access is { } access ? access.Unoffered : null, setupChanged: setupChanged, handed: target.Handed,
                // D205 §3.2: what its actor asked, set on the session before the first prompt; what the agent would not take is kept as
                // asked and not carried, so the record never says a choice was taken that was not.
                asked: asked, carried: target.Brainstorm is not null ? null
                    : notCarried => _ = service.TryKeepActorAsync(sessionId, Log, choice, notCarried, ct: CancellationToken.None),
                modelRefused: refusedAt, offerRead: offered)
            : null;
        // What the native door's harness says in its own words, apart from its agent's, for its ending (AGT3c).
        var own = new HarnessWords();
        var structured = acp is null
            ? Structured(adapter, process, transcript, sessionId, prompt, ct, preamble, target.PersonSaid, said, keepAs, resume, account, own, target.Handed)
            : null;
        Task capture = acp ?? structured ?? CaptureAsync(process, transcript, sessionId, ct, preamble);

        if (!movedFirst)
        {
            try
            {
                await service.AdvanceAsync(
                    sessionId, "working", note: workingNote?.Note, transcript: transcript, ct: ct, parts: workingNote?.Parts).ConfigureAwait(false);
            }
            catch
            {
                // 🔴 The record would not become this run's (ANSWER2: a second resume met working → working), so the run is ended
                // here, and what was reading its output is observed: its failure is written with its reason, never left unawaited.
                await AbandonedAsync(process, capture, sessionId, Log, output).ConfigureAwait(false);
                throw;
            }
        }

        working?.Invoke();

        var exitCode = await WaitAsync(
                process, ct, minutes, timingOut: ending is null ? null : bound => ending.Say(Observation.TimedOut(bound)),
                quiet: watched?.Ending ?? default,
                quietEnding: ending is null ? null : () => ending.Say(Observation.Quiet(watched!.EndedMinutes ?? 0)))
            .ConfigureAwait(false);
        // 🔴 State E (SESSWATCH1a): a stray its agent started may hold the capture open past the exit; bounded, and concluded by the exit.
        var drained = await CaptureWithinAsync(capture, sessionId).ConfigureAwait(false);

        // What it consumed, where the door reported it (TOOL3/D57 §4). A pipe with only text reports
        // nothing, and a surface then says "not measured" rather than zero.
        var (outcome, turnFailed, acpLeft) = acp is not null && drained ? await acp.ConfigureAwait(false) : (null, null, []);
        // DONEEND1: an agent the run ended because it lingered after its session had ended exited as its session did, cleanly: on this
        // door the agent exits 0 once its stdin closes whatever happened, and the kill was Daoris's own, never the agent's end.
        if (ending?.EndedLingering == true && exitCode is not null) exitCode = 0;
        List<ChatMessage> left = [.. acpLeft];
        var used = acp is not null ? outcome?.Usage
            : structured is not null && drained ? await structured.ConfigureAwait(false)
            : null;

        // 🔴 SESSCTX1c (D189's amendment, the session-context design §2): a native resume whose harness ended on a failed exit before it
        // named its conversation proves nothing of its loss, so it is tried once more as the same resume, the same conversation, account
        // and access, before its conclusion reads a second such end as `exited`. Never after a stop.
        if (structured is not null && resume is { Named: false, Refused: null, Retried: false } && exitCode is int early && early != 0
            && !Context.Processes.WasStopRequested(sessionId))
        {
            (exitCode, used) = await GoOns.AgainAsync(
                    info, adapter, sessionId, transcript, refusesInput, drivesBrowser, resume, said, wentOn, keepAs, own, ct)
                .ConfigureAwait(false);
        }

        // 🔴 A session never concludes while words are held (MSG1b, D137 §2.1): on the native door its process has exited, so
        // its own conversation is resumed with them under the same record, which stays working, until a run ends with none.
        if (held is not null)
        {
            (exitCode, used) = await GoOns.GoOnAsync(
                held, goOn!, adapter, sessionId, transcript, refusesInput, drivesBrowser, exitCode, used, resume, said, wentOn, left, ct, own,
                setupChanged)
                .ConfigureAwait(false);
        }

        // ACCESSSWAP1b: the run Daoris ended for a changed setup says so on its console as it ends; its record's note says it once concluded.
        if (swapped is not null && left.Count > 0)
        {
            output?.Append(
                sessionId,
                $"— its permission prompts changed from `{swapped.From}` to `{swapped.To}` while it worked: Daoris ended it at its turn's end, "
                + $"never mid-turn, and the {left.Count} word(s) held for it go on in the same conversation under the new setup at the next look.");
        }

        var concluded = await conclude(
                exitCode, used, turnFailed,
                Ending(acp is not null, structured is not null, exitCode, turnFailed, own, transcript) with
                {
                    SetupChanged = left.Count > 0 ? swapped : null,
                    // APIREFUSAL1: the door's own word that its provider refused the turn it ended on.
                    ProviderRefused = outcome?.ProviderRefused == true,
                    // MODELROLE1c2: its agent refused its choice at the open, before any prompt.
                    NotOffered = outcome?.NotOffered,
                })
            .ConfigureAwait(false);

        // The part's native children end with it (FABRIC-C1a): a run killed, stopped or failed says nothing of theirs, so each its
        // harness did not see stop is lost; a clean end ends them with it.
        ChildLineage.End(home, sessionId, clean: exitCode == 0 && Context.Processes.StopMade(sessionId) is null, DateTimeOffset.UtcNow);

        // The words its failed door never delivered wait on its ended record to go again (TURNFAIL1, USERSHOWN1): kept once the
        // conclusion moved it, since the say door refuses a running record. ACCESSSWAP1b: so do the words a changed setup handed back,
        // to go on under it; unless a stop was made of it since, which ends it with them unreached as any stop does, since words on its
        // record would carry it on past the stop.
        if (left.Count > 0 && swapped is not null && turnFailed is null && Context.Processes.StopMade(sessionId) is not null)
        {
            var unreached = WordsNotes.Unreached(left.Count, "never reached the session", UnreachedWhy.EndedFirst);
            output?.Append(sessionId, unreached.Note);
            Context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = unreached.Note, Parts = unreached.Parts }, say: null);
        }
        else if (left.Count > 0)
        {
            await WordsLeft.KeepAsync(
                    service, Context.Events, sessionId, left, CancellationToken.None,
                    why: swapped is not null && turnFailed is null ? Observation.WordsUnderNewSetup : null)
                .ConfigureAwait(false);
        }

        return concluded;
    }

    /// <summary>
    /// What a running driven session's permission prompts changed to (ACCESSSWAP1b), read from the driver's file now; null where they did
    /// not, or the file does not read: a write torn half-way is read again at the next turn's end, never taken for a change.
    /// </summary>
    private static SetupSwap? SetupNow(DrivenAccess started, ISessionAdapter adapter, string configPath)
    {
        try
        {
            return started.SwapIn(DriverConfig.Load(configPath), adapter);
        }
        catch (Exception error) when (error is DriverConfigUnreadableException or DriverException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
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
    internal static async Task AbandonedAsync(
        Process? process, Task capture, string sessionId, MachineLog? log, SessionOutput? output, TimeSpan? within = null)
    {
        if (process is not null) SessionProcesses.EndIfRunning(process);
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
    /// What the harness itself said about how its run ended (AGT3c), by its door: the protocol door's failure; the native
    /// door's failed turn and the words it wrote outside its protocol and on its stderr; and for a door that carries only text,
    /// all it gives, its transcript, beside the exit.
    /// </summary>
    internal static HarnessEnding Ending(bool protocol, bool structured, int? exit, string? turnFailed, HarnessWords own, string transcript) =>
        protocol ? new HarnessEnding(exit, turnFailed, [])
        : structured ? own.Ended(exit)
        : HarnessEnding.Transcript(exit, transcript);

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
    /// Both streams into one transcript file — diagnostic, never the record (D46 §4) — and, since
    /// D49 §2, teed into the live console as they pass.
    /// </summary>
    /// <remarks>
    /// One pump, two destinations, and the file is written FIRST: the durable copy must never be the
    /// thing that loses a line to an in-memory reader's problem. The console is optional because the
    /// headless driver has nobody to show it to — the buffer exists only where something reads it.
    /// </remarks>
    private Task CaptureAsync(
        Process process, string transcript, string sessionId, CancellationToken ct, string? preamble = null) =>
        HarnessCapture.CaptureAsync(
            Hearing(process.StandardOutput, sessionId), Hearing(process.StandardError, sessionId), transcript, sessionId, output, ct, preamble);

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
    /// The structured capture for a pipe-door harness whose adapter reads its stdout (CONV3), or null
    /// when the adapter has no reader and the door stays text.
    /// </summary>
    /// <param name="keepAs">The adapter a quest's session keeps its harness's conversation id under (ANSWER1a); null keeps none.</param>
    /// <param name="resume">The conversation an answer continues: the record's transcript and conversation go on.</param>
    /// <param name="account">What the prompt was composed of (CONTEXT1), kept beside it on its event.</param>
    /// <param name="own">Where the harness's own words are kept for its run's ending (AGT3c).</param>
    /// <param name="handed">A resume's words from the session that asked, shown after the target under their ids (SAYHELD1).</param>
    private Task<AcpUsage?>? Structured(
        ISessionAdapter adapter, Process process, string transcript, string sessionId, string prompt,
        CancellationToken ct, string? preamble = null, string? personSaid = null, Action<JsonElement>? said = null,
        string? keepAs = null, ResumeAsk? resume = null, InstructionAccount? account = null, HarnessWords? own = null,
        IReadOnlyList<SaidWordView>? handed = null) =>
        adapter.StructuredOutput() is { } mapper
            ? KeepingAsync(mapper, HarnessCapture.CaptureStructuredAsync(
                Hearing(process.StandardOutput, sessionId), Hearing(process.StandardError, sessionId), transcript, sessionId, output,
                Context.Events, mapper, resume is null ? prompt : null, ct, preamble, personSaid: personSaid, handed: handed,
                opened: resume?.Opening(), append: resume is not null, said: said, account: account, own: own, lineageHome: home),
                sessionId, keepAs, resume)
            : null;

    /// <summary>
    /// The native door's conversation id, once its output has ended (ANSWER1a): kept for a quest's session, and told to a
    /// resume, whose run that never named one ended before the harness opened its conversation.
    /// </summary>
    internal async Task<AcpUsage?> KeepingAsync(IStreamMapper mapper, Task<AcpUsage?> capture, string sessionId, string? keepAs, ResumeAsk? resume)
    {
        var usage = await capture.ConfigureAwait(false);
        if (mapper.Conversation is { } conversation)
        {
            if (keepAs is not null) Context.Conversations.Keep(sessionId, keepAs, conversation);
            // It opened the conversation its argument carried the person's words to, so the words went (MSG1b).
            if (resume is not null) resume.Named = resume.Prompted = true;
        }

        return usage;
    }

    /// <summary>
    /// What a session with no connector is told on its transcript: what it can and cannot do, and the two commands that
    /// lay one down, the install's first, as <see cref="KnowledgeConnector.Candidates"/> ranks them (CONNECTOR1b).
    /// </summary>
    internal static string NoConnectorForSession =>
        $"— no {KnowledgeConnector.ExecutableName} on this machine, so this session has no "
        + "connector: it can do the work but cannot take or close its own quest. "
        + "`npm run publish:desktop -- --to <install> --service` lays one beside the application, and "
        + "`npm run publish:service -- --install` lands one in the home's `bin/`.";

    /// <summary>
    /// The protocol door's capture (D53): an ACP session held over this process's stdio, with the
    /// RENDERED updates reaching the transcript and the console rather than the wire itself.
    /// </summary>
    /// <remarks>
    /// <para><b>stdout belongs to the protocol; stderr is still text a person wants.</b> Agents log
    /// there, and its lines go to the same two destinations through the same tee, so a transcript
    /// stays one readable stream.</para>
    ///
    /// <para><b>The ending is end-of-input, not a kill.</b> When the turn is over the driver closes
    /// stdin and the agent winds up and exits on its own — the same ending the chat door records as
    /// `completed` (D49/SES2), and the exit the conclusion below is actually drawn from.</para>
    ///
    /// <para><b>The wire's own ending is written down and nothing more.</b> It is a self-report, and
    /// the protocol flattens an aborted, blocked or errored turn into `end_turn`, so a record moved
    /// by it would be a record that cannot tell a refusal from a success.</para>
    /// </remarks>
    /// <param name="scope">
    /// What this session's connector carries beyond the store — an intake's ask and session (D65 §1b).
    /// Null for a quest's session.
    /// </param>
    /// <param name="meta">What <c>session/new</c> carries for the rules composed for this session (PERM1).</param>
    /// <param name="keepAs">The adapter a quest's session keeps its harness's conversation id under (ANSWER1a); null keeps none.</param>
    /// <param name="resume">
    /// The conversation an answer continues (ANSWER1a): resumed rather than opened, the record's transcript and
    /// conversation going on, and an agent that would not resume it told to the resume, never as a turn that failed.
    /// </param>
    /// <param name="account">What the prompt was composed of (CONTEXT1), kept beside it on its event; null for a resume.</param>
    /// <param name="waitsOnBackground">Whether a turn that ended on its own background work waits for it (BGWAIT1); null never.</param>
    /// <param name="noConnector">What is said where there is no connector, when it is neither a quest's nor an intake's sentence.</param>
    /// <param name="ending">Where the run's wait says the timeout before its kill (BGWAIT2), handed the door once it is made.</param>
    /// <param name="unoffered">
    /// What the open says where the agent does not offer a required posture, a driven start's permission prompts' (AUTONOMY1b); null
    /// says Ask Daoris's (<see cref="AcpSession.Unoffered"/>).
    /// </param>
    /// <param name="setupChanged">
    /// Asked at a turn's end before a held word is prompted, whether the setup the run started under changed (ACCESSSWAP1b,
    /// <see cref="AcpSession.SetupChanged"/>): yes ends the run there, the held words handed back as <c>Left</c>. Null never asks.
    /// </param>
    private async Task<(AcpOutcome? Outcome, string? Failure, IReadOnlyList<ChatMessage> Left)> CaptureAcpAsync(
        Process process, string transcript, string sessionId, string cwd, string prompt,
        string? posture, string? harnessNotice, CancellationToken ct,
        IReadOnlyDictionary<string, string>? scope = null, object? meta = null,
        IReadOnlyList<AcpMcpServer>? servers = null, string? personSaid = null, DrivenInbox? inbox = null,
        Action<JsonElement>? said = null, string? keepAs = null, ResumeAsk? resume = null, InstructionAccount? account = null,
        Func<CancellationToken, Task<bool>>? waitsOnBackground = null, string? noConnector = null, AcpEnding? ending = null,
        Func<string, string, string>? unoffered = null, Func<bool>? setupChanged = null, IReadOnlyList<SaidWordView>? handed = null,
        ActorAsked? asked = null, Action<ActorAsked>? carried = null, Func<ModelRefusal, Task<ActorAsked?>>? modelRefused = null,
        Action<IReadOnlyList<AcpConfigOption>>? offerRead = null)
    {
        await using var file = TranscriptFile.Open(transcript, append: resume is not null);

        void Line(string text)
        {
            lock (file) file.WriteLine(text);
            output?.Append(sessionId, text);
        }

        // The record's half (D76 §2): a record that cannot be written costs a console line, never the session.
        void Event(SessionEvent e) => Context.Events.Keep(sessionId, e, Line);

        // A driver line that is also part of the conversation: a note in the record as well.
        void Said(string text)
        {
            Line(text);
            Event(new SessionEvent { Kind = SessionEventKind.Note, Text = text });
        }

        // The same by its code (CONVNOTE2d1): the console keeps the English, the record its parts too.
        void SaidByCode(Noted noted)
        {
            Line(noted.Note);
            Event(new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts.Count > 0 ? noted.Parts : null });
        }

        var errors = HarnessCapture.PumpAsync(Hearing(process.StandardError, sessionId), file, sessionId, output, ct);
        AcpSession? session = null;

        try
        {
            // What was asked, first: the target the driver composed is the conversation's opening line,
            // and a person's answer it carries on from follows it, as theirs. A resumed run opens with its own first
            // line and the answer (ANSWER1a): the target is already in the conversation above.
            if (resume is not null)
            {
                foreach (var opening in resume.Opening())
                {
                    if (opening.Kind == SessionEventKind.Note && opening.Text is { } note) Line(note);
                    Event(opening);
                }
            }
            else
            {
                foreach (var opening in HarnessCapture.Opening(prompt, personSaid, account, handed)) Event(opening);
            }

            // What this harness is doing that daoris has not been able to govern (ACP3). On the
            // transcript rather than swallowed: it is a fact about how this session ran, and the
            // person can act on it in one command.
            if (harnessNotice is { Length: > 0 }) Said(harnessNotice);

            // The session's voice (ACP4). Located per run rather than once, because a machine can
            // gain the host between ticks — and a machine that has none still drives, without a
            // connector, exactly as it did before.
            var connector = Connector(sessionId, scope);
            if (connector is null)
            {
                Said(noConnector ?? (scope is null ? NoConnectorForSession : DriverIntakes.NoConnectorForIntake));
            }

            // The knowledge host first, then whatever the plugins hand every session (D65 §1f): a
            // browser, a ticket system — tools the session can reach, under the repository's own
            // posture like every other tool it has.
            var offered = new List<AcpMcpServer>();
            if (connector is not null) offered.Add(connector);
            offered.AddRange(servers ?? Context.Servers);

            // What it runs beside itself, each its own console stream (CONSOLE2) — kept wherever the
            // session's own console is — and stoppable from its tab while the session runs (CONSOLE3a).
            session = new AcpSession(
                Hearing(process.StandardOutput, sessionId), process.StandardInput, Line, closeTimeout: null, posture, meta, Event,
                streams: output is null ? null : new SessionStreams(output, sessionId),
                onConversation: keepAs is null ? null : conversation => Context.Conversations.Keep(sessionId, keepAs, conversation))
            {
                Unoffered = unoffered,
                Permissions = HeldPermissions.Of(Context.Events).For(sessionId), // AUTONOMY1b3a: held only under asking's mark
                SetupChanged = setupChanged,
                BackgroundLook = BackgroundLook, // DONEEND1: its quest read again while it waits on its own background work
                Carry = asked, // MODELROLE1c (D205 §3.2): its actor's model and effort, set before the first prompt
                Carried = carried,
                ModelRefused = modelRefused, // MODELROLE1c2 (D205 §3.3): a choice refused at the open, re-asked before any prompt
                OffersRead = offerRead, // and what the open listed, kept as its agent's offer reading (§9.2)
            };
            ending?.Open(session);
            // SESSWATCH1a: whether it waits on its own background work, the watcher's state C.
            var waitsOn = session;
            Watch.OwnWork(sessionId, () => waitsOn.WaitsOnOwnWork, () => [.. waitsOn.BackgroundRunning.Select(task => task.Name)]);
            // What it says about its account's windows, kept as it says it (TOOL6c).
            if (said is not null) session.LimitsSaid += said;
            using var stops = output is null ? null : Context.Processes.OpenTaskStops(sessionId, session.StopTaskAsync);
            // FABRIC-PIPELINE1c (D192 point 5's fourth layer): under its run's light checks, a call or a background task past its bound is
            // told to it once, as Daoris's urgent word, and never ended; the checks its start kept, so every run of it reads the same, and
            // the bound kept beside them where its graph version set one (FABRIC-PIPELINE1b3).
            var acpSession = session;
            using var longCalls = LongCalls.Watch(
                SessionChecks.Held(home, sessionId), sessionId, RunningCalls.Of(Context.Events), () => acpSession.BackgroundRunning, inbox,
                inbox is null ? null : new SaidDoor(service, sessionId),
                failed: error => Line($"— Daoris's look at this session's long calls failed, and tells it nothing more: {error}"),
                minutes: SessionChecks.HeldMinutes(home, sessionId));
            // The person's words in the record the moment they are said, with when they reach the session (STEER1, D136):
            // the page shows them waiting in the turn they were said in, rather than nothing until they are handed over.
            inbox?.OnSaid((message, reach) =>
            {
                // Another session's message on the ask's channel is that agent's claim, never the person's (SESSONE2b); at its next step
                // where urgent, and beside words the session is told anyway when its turn ends otherwise (COWORK1d).
                Line(message.Channel is { Person: false } claim
                    ? reach == DrivenReach.NextStep
                        ? $"— another session's message `{claim.Id}` on the ask's channel, which reaches the session at its next step"
                        : $"— another session's message `{claim.Id}` on the ask's channel, which goes beside what the session is told when its turn ends"
                    // COWORK1d: an urgent ask's helper answer, the helper's claim, never the person's.
                    : message.Helped is { Helper: var helper }
                        ? $"— helper `{helper}`'s answer, which reaches the session {(reach == DrivenReach.NextStep ? "at its next step" : "when its turn ends")}"
                    // Daoris's own word (COWORK1b, FABRIC-PIPELINE1c): never said as the person's.
                    : message.Daoris is not null
                        ? $"— Daoris said, which reaches the session {(reach == DrivenReach.NextStep ? "at its next step" : "when its turn ends")}: {message.Text}"
                    : reach switch
                    {
                        DrivenReach.NextStep => $"— the person added, which reaches the session at its next step: {message.Text}",
                        // The person's *Send now* (STEER2a): steered into the running turn, cutting the step in flight.
                        DrivenReach.Steered => $"— the person sent now, which the session takes at once: {message.Text}",
                        _ => $"— the person added, which reaches the session when its turn ends: {message.Text}",
                    });
                // Its level beside its reach (COWORK1a): what was asked, and what the door gave.
                Event(ChatMessage.Words(message) with { Reaches = SendNowWhy.Spelled(reach), Level = message.LevelSaid });
            });
            // A word the person took back or reworded while it waited is settled, by its id (QUEUEEDIT1a).
            inbox?.OnWithdrawn(message =>
            {
                Line($"— the person took back what they added: {message.Text}");
                Event(ChatTurns.Withdrawn(message.Id!));
            });
            var outcome = await session
                .RunAsync(
                    cwd, prompt, ct, offered, inbox,
                    // The same words as the turn's ask where the session took them (SESS3, STEER1): recorded before
                    // anything the agent did with them, so the answer never sits above the question.
                    asked: message =>
                    {
                        Line(message.Channel is { Person: false } claim
                            ? $"— the session took another session's message `{claim.Id}` on the ask's channel"
                            : message.Helped is { Helper: var helper }
                                ? $"— the session took helper `{helper}`'s answer"
                                : $"— the session took what the person added: {message.Text}");
                        Event(ChatMessage.Words(message));
                        // COWORK1d: a channel message's entry acknowledges it taken, on the door's word.
                        message.Acknowledge?.Invoke(ChannelAcks.Taken);
                    },
                    resume: resume?.Conversation,
                    // Each of the person's words its own block (MSG1b, D137 §2.2), and taken once the prompt is on the wire.
                    blocks: resume?.Blocks,
                    prompted: resume is null ? null : () => resume.Prompted = true,
                    // COWORK1c: marked attempted on the record before the opening prompt carrying them is written.
                    attempting: resume is null ? null : () => SaidDoor.AttemptedAsync(service, sessionId, resume.Words),
                    // The files said with them, a link each after the blocks (MSG1d3, D137 §2.4).
                    files: resume?.Files,
                    waitsOnBackground: waitsOnBackground)
                .ConfigureAwait(false);

            Line($"— the turn ended: {outcome.StopReason}, after {outcome.Updates} update(s). The "
                 + "session record is concluded from the exit code and the quest's own state, not "
                 + "from this line (D46 §4).");
            // ACCESSSWAP1b: the words a changed setup handed back, kept on the record once it concluded, to go on under the new one.
            return (outcome, null, session.HandedBack);
        }
        catch (AcpResumeRefused refused) when (resume is not null)
        {
            // Not a turn that failed (ANSWER1a): nothing was prompted. The agent's own words stay in this machine's record of
            // the conversation, and the reason goes to the resume, which ends the park and carries the answer on.
            resume.Refused = refused.Why;
            // Its reason by code, the agent's words its own (CONVNOTE2e). A resume keeps its conversation under the adapter it
            // runs on, which is the one it ran on, so that is the agent `elsewhere` names. SESSCTX1c: a record that waits for the
            // person (`elsewhere`) says no carry-on.
            SaidByCode(refused.Why.Holds ? DoorNotes.ResumeRefused(refused, keepAs) : DoorNotes.AnswerCarriedOn(refused, keepAs));
            return (null, null, []);
        }
        catch (DriverException error)
        {
            // A protocol failure is a fact about this run and belongs in its transcript — and it goes to
            // the conclusion too (ACPEND1), beside the exit code and the quest's state. 🔴 The exit alone
            // cannot carry it here: the driver closes stdin below and the agent winds up and exits 0,
            // so an account limit that refused the turn mid-edit read as a clean exit, and a taken
            // quest as a stand-down. A failed call is not the session describing its work, which is
            // the self-report D46 §4 keeps out; it is the door saying the work was cut off. 🔴 BGWAIT2: a stream the timeout
            // ended is said as the timeout, by its own line; the install's record said "the agent exited" of one.
            SaidByCode(error is AcpEnded ended ? ended.Why : DoorNotes.Failed(error.Message));
            // 🔴 The person's words it never took (TURNFAIL1, USERSHOWN1): sent behind the failed turn, or held for its end. They
            // go back to the run, which keeps them on the record once it ended, to go again (WordsLeft); closed here, so the
            // finally below says nothing of them.
            return (null, error.Message, [.. session?.Unreached ?? [], .. session?.HandedBack ?? [], .. inbox?.Close() ?? []]);
        }
        finally
        {
            // What the person said that never reached it (SESS3): said, never dropped without a trace.
            if (inbox?.Close() is { Count: > 0 } left)
            {
                SaidByCode(WordsNotes.Unreached(left.Count, "never reached the session", UnreachedWhy.EndedFirst));
            }

            try
            {
                process.StandardInput.Close();
            }
            catch (Exception error) when (error is InvalidOperationException or IOException or ObjectDisposedException)
            {
                // Already gone: the agent exited first, which is the ending this was asking for.
            }

            // 🔴 DONEEND1: an agent whose session ended while its own background work ran (a test run its quest no longer waited on)
            // may never exit, that work holding it, and the run would then wait for the timeout. Given a bound, its tree is ended
            // here, the work with it, before the stderr it may hold is waited for.
            if (session?.LeftRunning is { Count: > 0 } work && await EndedLingeringAsync(process, LingerBound, ending).ConfigureAwait(false))
            {
                Line($"— its agent had not exited {LingerBound.TotalSeconds:0} s after its session ended, its own background work still "
                     + $"running ({string.Join(", ", work.Select(name => $"`{name}`"))}): Daoris ended its process tree.");
            }

            await errors.ConfigureAwait(false);
        }
    }

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
    internal AcpMcpServer? Connector(string sessionId, IReadOnlyDictionary<string, string>? scope) =>
        KnowledgeConnector.Offer(Variable, AppContext.BaseDirectory, KnowledgeConnector.SessionScope(home, sessionId, scope));

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
    /// A quest's target with the person's words on the ask it was asked by (DRIFT1b, D133 §2), read from the ask for this
    /// start: every start on the ask is handed them, a first start, a resume and a carry-on on any account alike, since a
    /// carry-on's own record holds only the session before it. A quest no ask asked is handed none, and words the service
    /// did not answer leave today's instruction, saying so (<see cref="AskWords.ReadAsync"/>).
    /// </summary>
    internal static async Task<SessionTarget> WithAskWordsAsync(SessionTarget target, ServiceClient service, CancellationToken ct) =>
        target with { Words = await AskWords.ReadAsync(service, target.Asker, ct).ConfigureAwait(false) };

    /// <summary>
    /// The person's standing answer for the quest's repository on this machine (KNOWUSE1b, D135 §3), read from the config this
    /// tick holds for every start, whatever kind: a follow-up step in another repository is handed that repository's.
    /// </summary>
    internal static SessionTarget WithStanding(SessionTarget target, DriverConfig config) =>
        target with { Standing = config.StandingFor(target.Repository) };

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
    /// Whether a start runs on another account than the session it carries on ran on (TOOL4f): the record's account
    /// against the selection's, compared as the wiring compares names, null being the tool's own home.
    /// </summary>
    internal static bool OnAnotherAccount(PriorSession prior, string? profile) =>
        !string.Equals(prior.Profile ?? "", profile ?? "", StringComparison.OrdinalIgnoreCase);

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
    /// <param name="walked">
    /// The reason of the walk's first step, where the walk chose the account (<see cref="StepSaid.Why"/>). An account over its
    /// curve passed, or the most room under it, is the usage curve's move (SWITCH1c-iii, the switch design §4); a cooling or
    /// refused account passed, a limit reached and the rest are not.
    /// </param>
    internal static ContinuedFrom? ContinuesFrom(
        HandedQuest? handed, PriorSession? carried, string? profile, string? walked = null, bool retried = false, bool pressed = false) =>
        carried is null ? null
        : handed is not null && string.Equals(carried.Session, handed.From, StringComparison.Ordinal)
            ? new ContinuedFrom(carried.Session, handed.Why, handed.AskedBy)
        : carried.Limit && OnAnotherAccount(carried, profile)
            ? new ContinuedFrom(carried.Session, HandOverWhy.Limit, HandOverBy.Daoris)
        : walked is OpeningWhy.OverCurve or OpeningWhy.MostRoom && OnAnotherAccount(carried, profile)
            ? new ContinuedFrom(carried.Session, HandOverWhy.Curve, HandOverBy.Daoris)
        : retried
            ? new ContinuedFrom(carried.Session, HandOverWhy.Retry, HandOverBy.Daoris)
        : new ContinuedFrom(carried.Session, HandOverWhy.CarryOn, pressed ? HandOverBy.Person : HandOverBy.Daoris);
}
