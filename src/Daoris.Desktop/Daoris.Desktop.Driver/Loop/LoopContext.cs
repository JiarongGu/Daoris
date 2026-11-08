namespace Daoris.Driver;

/// <summary>
/// What the loop's classes share: what the <see cref="Driver"/> is built with that more than one of them reads, the
/// sessions it runs and the worktrees it keeps, the per-look adapters, servers and catalogue, and the settables a test hands in after
/// construction. The driver makes one and hands it to each class, as the chat runner does with its context.
/// </summary>
/// <remarks>
/// <para><b>Read at each use, never copied at build.</b> The look sets <see cref="Adapters"/>, <see cref="Servers"/> and
/// <see cref="Catalog"/> at its top, and a test sets <see cref="Passes"/> and <see cref="OpinionGit"/> after the driver is
/// built, so a class that reads one reads it here at the moment it needs it.</para>
/// </remarks>
internal sealed class LoopContext(
    ServiceClient service, DriverConfig config, string home, ProviderRegistry adapters, SessionProcesses? processes, HarnessRoster? harnesses,
    SessionEvents? events, SessionOutput? output, SessionUsage? usage, RunningSessions? running, HookSet? hooks, IInAppBrowser? browser)
{
    /// <summary>The plugins that speak, shared across ticks; null where no plugin is read at all.</summary>
    public HookSet? Hooks { get; } = hooks;

    /// <summary>Daoris's own browser, asked for by a plugin server that drives it; null where no shell carries one.</summary>
    public IInAppBrowser? Browser { get; } = browser;

    /// <summary>The harness's own conversation per session, beside the transcripts under the same home.</summary>
    public HarnessConversations Conversations { get; } = new(home);

    /// <summary>The words a record could not go on with, beside the conversations, so a later look leaves them waiting.</summary>
    public GoOnMarks Marks { get; } = new(home);

    /// <summary>
    /// The supervisor's decisions, live in memory: one per instance, which the shell hands the modules' handler
    /// too. Inert: its admission refuses every act, so no decision is kept and every supervisor's word is
    /// quarantined. A test sets its own.
    /// </summary>
    public QuestionSupervisorDecisions Supervisor { get; set; } = new(home, NoDecisionAdmission.Instance);

    /// <summary>
    /// What reached each part from its ask's channel, shared across the per-tick instances, so a message reaches a running
    /// part once whichever look reads it.
    /// </summary>
    public ChannelReach Channel { get; } = new();

    /// <summary>
    /// The sources' reopens told to the sessions that said their ready, shared across the per-tick instances, with
    /// the marks beside the sessions' transcripts that hold the once across a restart.
    /// </summary>
    public ReopenTelling Reopens { get; } = new(new ReopenMarks(home));

    /// <summary>The person's choices to go on in a new session rather than wait for a cooling account, beside the marks.</summary>
    public NewSessionChoices NewSessions { get; } = new(home);

    /// <summary>
    /// How a running session is stopped for a take that lost and for a hand-over: its process ended through
    /// <see cref="SessionProcesses"/>, unless a test hands in its stand-in run's stop. Set once, before a look; read at each use.
    /// </summary>
    public Func<string, Noted, bool>? Stops { get; set; }

    /// <summary>
    /// How a conversation an agent asked to hand over goes on with another agent: the shell's chat runner's own
    /// hand-over (<c>ChatRunner.HandOverAsync</c> with the agent's why and asker), taken at once where the conversation is not talking,
    /// kept for its turn's end where it is; the shell's loop hands it in. Null where the loop holds no runner (the headless host): the
    /// look keeps the box, which a runner takes at the conversation's turn's end or as it comes up. Set once, before a look. The step is
    /// the arranged order's for the agent (its model and effort), or null where the order names neither.
    /// </summary>
    public Func<string, string, HandOverStep?, CancellationToken, Task<HandedOver>>? HandsOver { get; set; }

    /// <summary>
    /// How a chat run's second-opinion findings reach its conversation: the shell's chat
    /// runner, which takes them as a turn of their own only between turns, or resumes an ended conversation with them; the shell's loop hands
    /// it in, as it hands <see cref="HandsOver"/>. Null where the loop holds no runner (the headless host): the findings wait. Set once, before a look.
    /// </summary>
    public IChatSendBacks? SendsBack { get; set; }

    /// <summary>
    /// How a closed quest's session whose own conversation is proven lost goes on by itself as a conversation in its tree
    /// (a closed quest opens no driven record): the shell's chat runner's own move
    /// (<c>ChatRunner.GoOnLostAsync</c>), handed the brief and the person's words; the shell's loop hands it in. Null where the loop holds
    /// no runner (the headless host): the session waits for the person, marked, the page's press ready. Set once, before a look.
    /// </summary>
    public Func<string, ContinueReason, CancellationToken, Task<StartedFrom>>? GoesOnLost { get; set; }

    /// <summary>
    /// The roster's doors in its order, as a hand-over's order reads them (<see cref="HandOverOrder"/>), unless a test hands
    /// in a stand-in, since the roster asks each binary its version once per binary.
    /// </summary>
    public Func<CancellationToken, Task<IReadOnlyList<HandOverDoor>>>? HandOverDoors { get; set; }

    /// <summary>How a helper's pass is run, unless a test hands in a stand-in, since a real pass spawns git and an agent.</summary>
    public Func<HelperPassAsk, ReviewerChoice, CancellationToken, Task<HelperPassRun>>? HelperPasses { get; set; }

    /// <summary>How an asker's tree is read, its last commit and its repository's registered checkout, unless a test hands in a stand-in.</summary>
    public Func<string, CancellationToken, Task<(string? Tip, string? Root)>>? HelperTrees { get; set; }

    /// <summary>How an asker's work in progress is captured for its helper, unless a test hands in a stand-in, since a real capture spawns git.</summary>
    public Func<string, CancellationToken, Task<HelperCaptureRead>>? HelperCaptures { get; set; }

    /// <summary>How a helper's agent is chosen, unless a test hands in a choice.</summary>
    public Func<HelperAsk, HelperRule, PriorSession, CancellationToken, Task<ReviewerChoice>>? HelperChoices { get; set; }

    /// <summary>The decision door's half an agent supervisor's look reaches, the window's; none where no agent supervisor decides.</summary>
    public ISupervisorDesk? Supervising { get; set; }

    /// <summary>An agent supervisor's turn, unless a test hands its own; its choice is walked as <see cref="SupervisePass"/> walks it.</summary>
    public Func<SuperviseRunAsk, CancellationToken, Task<SuperviseRunEnd>>? SuperviseRuns { get; set; }

    /// <summary>The walk of an agent supervisor's order less its spent choices, unless a test hands its own.</summary>
    public Func<SupervisorWaiting, IReadOnlyList<ActorEffective>, CancellationToken, Task<ReviewerChoice>>? SuperviseChoices { get; set; }

    /// <summary>How a piece's agent is chosen and admitted, unless a test hands in a choice: never a production start.</summary>
    public Func<PartnerAsk, PriorSession, CancellationToken, Task<ReviewerChoice>>? PartnerChoices { get; set; }

    /// <summary>
    /// The measurements a test's stand-in agents are admitted by, in place of <see cref="PartnerMeasurements.Rows"/>; a start
    /// that reads them is never a production start. Null for every production start, which reads the code's table and no stand-in's row.
    /// </summary>
    public IReadOnlyList<PartnerMeasurement>? PartnerMeasured { get; set; }

    /// <summary>How a partner's pass is run (the copy, the job, the record, the turn), unless a test hands in a stand-in, since a real pass spawns git and an agent.</summary>
    public Func<PartnerPassAsk, ReviewerChoice, CancellationToken, Task<PartnerPassRun>>? PartnerPasses { get; set; }

    /// <summary>How a piece's copy is kept and imported, unless a test hands in a stand-in, since the keeper spawns git.</summary>
    public Func<PartnerAsk, PartnerState, CancellationToken, Task<PieceKept>>? PartnerKeeps { get; set; }

    /// <summary>How a lead's repository's checkout is read from its tree, unless a test hands in a stand-in.</summary>
    public Func<string, CancellationToken, Task<string?>>? PartnerTrees { get; set; }

    /// <summary>How a brainstorm's turn is run, unless a test hands in a stand-in, since a real turn spawns an agent.</summary>
    public Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? BrainstormTurns { get; set; }

    /// <summary>How a brainstorm's packet searches Daoris's knowledge, one wording at a time, unless a test hands in a stand-in.</summary>
    public Func<string, string, CancellationToken, Task<BrainstormSearch>>? BrainstormSearches { get; set; }

    /// <summary>
    /// How a brainstorm's repository is read, its checkout and the commit it stands at, unless a test hands in a stand-in, since a
    /// real read spawns git.
    /// </summary>
    public Func<string, CancellationToken, Task<(string? Root, string? Revision)>>? BrainstormCheckouts { get; set; }

    /// <summary>
    /// How long a brainstorm's minute lasts: a minute, unless a test shortens it so a pass's minutes and the request's
    /// deadline can be met in a fast run.
    /// </summary>
    public TimeSpan? BrainstormMinute { get; set; }

    /// <summary>The clock for brainstorm deadlines and cancellation timers, held by timing tests.</summary>
    public TimeProvider BrainstormClock { get; set; } = TimeProvider.System;

    /// <summary>
    /// How a meeting's turn is run, where a test hands in a stand-in that writes its message. With none, a meeting's turn
    /// is refused and never started: no tool carries a meeting's message yet, so a real agent's turn could not answer.
    /// </summary>
    public Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? MeetingTurns { get; set; }

    /// <summary>
    /// Whether this driver's looks leave beside them the work no part of the look waits on: the watch's,
    /// whose driver is handed <see cref="Driver.Wanted"/>. A one-look run and a gate's run-until-idle do it in the look, as each reads
    /// one look whole.
    /// </summary>
    public bool Beside { get; set; }

    /// <summary>The machine log, the driver's <see cref="Driver.Log"/>; null where nothing keeps one.</summary>
    public MachineLog? Log { get; set; }

    /// <summary>Told after a look's news is kept as a notice: the desktop wakes its notices slice. Set once, before a look.</summary>
    public Action? Noticed { get; set; }

    /// <summary>
    /// Each line the look says, classed where it is made: <c>look.said</c> in <see cref="Log"/>, and a news line
    /// kept as a notice under <see cref="Home"/>.
    /// </summary>
    public LookSaid Said => LazyInitializer.EnsureInitialized(ref _said, () => new LookSaid(Home, () => Log, () => Noticed?.Invoke()));

    private LookSaid? _said;

    /// <summary>
    /// A start the update's drain holds: said once as the drain's, under <paramref name="family"/>, for as long as it drains
    /// (<see cref="InstallUpdate.HeldLine"/>). The caller starts nothing and writes no state, so the work waits unchanged and goes at the
    /// first look that does not drain.
    /// </summary>
    /// <param name="key">What is held, unique within the family, to say it once.</param>
    /// <param name="held">What is held, in the line's words.</param>
    public void DrainHolds(List<string> events, string family, string key, string held)
    {
        if (Runs.DrainHeld($"{family}/{key}")) events.Add(Said.Progress(family, InstallUpdate.HeldLine(family, held)));
    }

    public ServiceClient Service { get; } = service;

    public DriverConfig Config { get; } = config;

    public string Home { get; } = home;

    /// <summary>
    /// Shared across the per-tick instances a watch loop constructs, so a probe is paid for once and not at every tick; per-instance
    /// when nobody passes one.
    /// </summary>
    public HarnessRoster Harnesses { get; } = harnesses ?? new HarnessRoster(adapters, HarnessSettings.ResolvePath(home));

    /// <summary>Shared across the per-tick instances, so a control surface can reach what is actually running.</summary>
    public SessionProcesses Processes { get; } = processes ?? new SessionProcesses();

    /// <summary>
    /// Where a session's structure is kept: the shell's shared record, or the home's own. Its turns are kept in the usage
    /// history, watched once however many looks share it.
    /// </summary>
    public SessionEvents Events { get; } = UsageTurns.Watched(home, events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)));

    public SessionOutput? Output { get; } = output;

    /// <summary>What sessions consumed; null where nobody is keeping the record.</summary>
    public SessionUsage? Usage { get; } = usage;

    /// <summary>The sessions this driver started and has not yet reported ending, and which quest each holds.</summary>
    public RunningSessions Runs { get; } = running ?? new RunningSessions();

    /// <summary>The session worktrees, beside the transcripts under the same home.</summary>
    public SessionTrees Trees { get; } = new(home);

    /// <summary>
    /// The run's memory of a role's acts as a start reads it, handed by the application holding the
    /// judge; null for a loop with none, whose starts no role caused and which honours no role's release.
    /// </summary>
    public IRoleStarts? Roles { get; set; }

    /// <summary>
    /// The adapters this tick spawns with: the build's, plus whatever the plugins read this tick declare. Set once per tick,
    /// before any start is run.
    /// </summary>
    public ProviderRegistry Adapters { get; set; } = adapters;

    /// <summary>The servers this tick hands every session: what the contributing plugins declare, beside the knowledge host.</summary>
    public IReadOnlyList<ServerEntry> Servers { get; set; } = [];

    /// <summary>The catalogue those servers came from: which plugin declares each, for the machine log's `plugin.served`.</summary>
    public PluginCatalog Catalog { get; set; } = PluginCatalog.None;

    /// <summary>
    /// How a pass is run: the driver's own, unless a test hands in a stand-in, since a real pass spawns git and an agent, which the
    /// suite's fast half may not. The recheck is run through it.
    /// </summary>
    public Func<OpinionPassAsk, ReviewerChoice, CancellationToken, Task<OpinionPassRun>>? Passes { get; set; }

    /// <summary>
    /// How a fix's commit is read from git when the working session's turn ends: the review's seam,
    /// <see cref="WorkingTree.ReadGitAsync"/> unless a test hands in its stand-in.
    /// </summary>
    public WorkingTree.GitRead OpinionGit { get; set; } = WorkingTree.ReadGitAsync;
}
