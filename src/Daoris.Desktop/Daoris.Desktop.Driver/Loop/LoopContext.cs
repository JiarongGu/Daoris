namespace Daoris.Driver;

/// <summary>
/// What the loop's classes share (PART10m, D166): what the <see cref="Driver"/> was built with that more than one of them reads, the
/// sessions it runs and the worktrees it keeps, the per-look adapters, servers and catalogue, and the settables a test hands in after
/// construction. The driver makes one and hands it to each class, as the chat runner does with its context (PART10g).
/// </summary>
/// <remarks>
/// <para><b>Read at each use, never copied at build.</b> The look sets <see cref="Adapters"/>, <see cref="Servers"/> and
/// <see cref="Catalog"/> at its top (D64 §3), and a test sets <see cref="Passes"/> and <see cref="OpinionGit"/> after the driver is
/// built, so a class that reads one reads it here at the moment it needs it.</para>
/// </remarks>
internal sealed class LoopContext(
    ServiceClient service, DriverConfig config, string home, AdapterSet adapters, SessionProcesses? processes, HarnessRoster? harnesses,
    SessionEvents? events, SessionOutput? output, SessionUsage? usage, RunningSessions? running, HookSet? hooks, IInAppBrowser? browser)
{
    /// <summary>The plugins that speak (D64), shared across ticks; null where no plugin is read at all.</summary>
    public HookSet? Hooks { get; } = hooks;

    /// <summary>Daoris's own browser (D78), asked for by a plugin server that drives it; null where no shell carries one.</summary>
    public IInAppBrowser? Browser { get; } = browser;

    /// <summary>The harness's own conversation per session (ANSWER1a), beside the transcripts under the same home.</summary>
    public HarnessConversations Conversations { get; } = new(home);

    /// <summary>The words a record could not go on with (MSG1b), beside the conversations, so a later look leaves them waiting.</summary>
    public GoOnMarks Marks { get; } = new(home);

    /// <summary>
    /// What reached each part from its ask's channel (SESSONE2b), shared across the per-tick instances, so a message reaches a running
    /// part once whichever look reads it.
    /// </summary>
    public ChannelReach Channel { get; } = new();

    /// <summary>
    /// The sources' reopens told to the sessions that said their ready (FABRIC-CLOSE1c8), shared across the per-tick instances, with
    /// the marks beside the sessions' transcripts that hold the once across a restart.
    /// </summary>
    public ReopenTelling Reopens { get; } = new(new ReopenMarks(home));

    /// <summary>The person's choices to go on in a new session rather than wait for a cooling account (MSG1g), beside the marks.</summary>
    public NewSessionChoices NewSessions { get; } = new(home);

    /// <summary>
    /// How a running session is stopped for a take that lost (D68 §5) and for a hand-over (MIXAGENT1a-ii): its process ended through
    /// <see cref="SessionProcesses"/>, unless a test hands in its stand-in run's stop. Set once, before a look; read at each use.
    /// </summary>
    public Func<string, Noted, bool>? Stops { get; set; }

    /// <summary>
    /// How a conversation an agent asked to hand over goes on with another agent (MIXAGENT1c): the shell's chat runner's own
    /// hand-over (<c>ChatRunner.HandOverAsync</c> with the agent's why and asker), taken at once where the conversation is not talking,
    /// kept for its turn's end where it is; the shell's loop hands it in. Null where the loop holds no runner (the headless host): the
    /// look keeps the box, which a runner takes at the conversation's turn's end or as it comes up. Set once, before a look.
    /// </summary>
    public Func<string, string, CancellationToken, Task<HandedOver>>? HandsOver { get; set; }

    /// <summary>
    /// How a closed quest's session whose own conversation is proven lost goes on by itself as a conversation in its tree (SESSCTX1c part
    /// B, D189 point 3; the steward's choice: a closed quest opens no driven record): the shell's chat runner's own move
    /// (<c>ChatRunner.GoOnLostAsync</c>), handed the brief and the person's words; the shell's loop hands it in. Null where the loop holds
    /// no runner (the headless host): the session waits for the person, marked, the page's press ready. Set once, before a look.
    /// </summary>
    public Func<string, ContinueReason, CancellationToken, Task<StartedFrom>>? GoesOnLost { get; set; }

    /// <summary>
    /// The roster's doors in its order, as a hand-over's order reads them (MIXAGENT1c, <see cref="HandOverOrder"/>), unless a test hands
    /// in a stand-in, since the roster asks each binary its version once a process (MOD8).
    /// </summary>
    public Func<CancellationToken, Task<IReadOnlyList<HandOverDoor>>>? HandOverDoors { get; set; }

    /// <summary>How a helper's pass is run, unless a test hands in a stand-in, since a real pass spawns git and an agent (MOD8).</summary>
    public Func<HelperPassAsk, ReviewerChoice, CancellationToken, Task<HelperPassRun>>? HelperPasses { get; set; }

    /// <summary>How an asker's tree is read, its last commit and its repository's registered checkout, unless a test hands in a stand-in (MOD8).</summary>
    public Func<string, CancellationToken, Task<(string? Tip, string? Root)>>? HelperTrees { get; set; }

    /// <summary>How a helper's agent is chosen, unless a test hands in a choice.</summary>
    public Func<HelperAsk, HelperRule, PriorSession, CancellationToken, Task<ReviewerChoice>>? HelperChoices { get; set; }

    /// <summary>How a brainstorm's turn is run, unless a test hands in a stand-in, since a real turn spawns an agent (MOD8).</summary>
    public Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? BrainstormTurns { get; set; }

    /// <summary>How a brainstorm's packet searches Daoris's knowledge, one wording at a time, unless a test hands in a stand-in.</summary>
    public Func<string, string, CancellationToken, Task<BrainstormSearch>>? BrainstormSearches { get; set; }

    /// <summary>
    /// How a brainstorm's repository is read, its checkout and the commit it stands at, unless a test hands in a stand-in, since a
    /// real read spawns git (MOD8).
    /// </summary>
    public Func<string, CancellationToken, Task<(string? Root, string? Revision)>>? BrainstormCheckouts { get; set; }

    /// <summary>
    /// How long a brainstorm's minute lasts (BRAINSTORM1b): a minute, unless a test shortens it so a pass's minutes and the request's
    /// deadline can be met in a fast run (MOD8).
    /// </summary>
    public TimeSpan? BrainstormMinute { get; set; }

    /// <summary>
    /// How a meeting's turn is run (AGENTROOM1a), where a test hands in a stand-in that writes its message. With none, a meeting's turn
    /// is refused and never started: no tool carries a meeting's message yet, so a real agent's turn could not answer.
    /// </summary>
    public Func<BrainstormTurn, CancellationToken, Task<BrainstormTurnRun>>? MeetingTurns { get; set; }

    /// <summary>
    /// Whether this driver's looks leave beside them the work no part of the look waits on (PARALLEL1b, D181 point 3): the watch's,
    /// whose driver is handed <see cref="Driver.Wanted"/>. A one-look run and a gate's run-until-idle do it in the look, as each reads
    /// one look whole.
    /// </summary>
    public bool Beside { get; set; }

    public ServiceClient Service { get; } = service;

    public DriverConfig Config { get; } = config;

    public string Home { get; } = home;

    /// <summary>
    /// Shared across the per-tick instances a watch loop constructs, so a probe is paid for once and not at every tick; per-instance
    /// when nobody passes one.
    /// </summary>
    public HarnessRoster Harnesses { get; } = harnesses ?? new HarnessRoster(adapters);

    /// <summary>Shared across the per-tick instances, so a control surface can reach what is actually running.</summary>
    public SessionProcesses Processes { get; } = processes ?? new SessionProcesses();

    /// <summary>
    /// Where a session's structure is kept (D76 §2): the shell's shared record, or the home's own. Its turns are kept in the usage
    /// history (USAGECAP1a2), watched once however many looks share it.
    /// </summary>
    public SessionEvents Events { get; } = UsageTurns.Watched(home, events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)));

    public SessionOutput? Output { get; } = output;

    /// <summary>What sessions consumed (TOOL3); null where nobody is keeping the record.</summary>
    public SessionUsage? Usage { get; } = usage;

    /// <summary>The sessions this driver started and has not yet reported ending, and which quest each holds (D68 §5).</summary>
    public RunningSessions Runs { get; } = running ?? new RunningSessions();

    /// <summary>The worktree half of D51, beside the transcripts under the same home.</summary>
    public SessionTrees Trees { get; } = new(home);

    /// <summary>
    /// The adapters this tick spawns with: the build's, plus whatever the plugins read this tick declare (D64 §3). Set once per tick,
    /// before any start is run.
    /// </summary>
    public AdapterSet Adapters { get; set; } = adapters;

    /// <summary>The servers this tick hands every session (D65 §1f): what the contributing plugins declare, beside the knowledge host.</summary>
    public IReadOnlyList<AcpMcpServer> Servers { get; set; } = [];

    /// <summary>The catalogue those servers came from: which plugin declares each, for the machine log's `plugin.served` (PLUGUI1d).</summary>
    public PluginCatalog Catalog { get; set; } = PluginCatalog.None;

    /// <summary>
    /// How a pass is run: the driver's own, unless a test hands in a stand-in, since a real pass spawns git and an agent, which the
    /// suite's fast half may not (MOD8). The recheck is run through it (XAGENT1e).
    /// </summary>
    public Func<OpinionPassAsk, ReviewerChoice, CancellationToken, Task<OpinionPassRun>>? Passes { get; set; }

    /// <summary>
    /// How a fix's commit is read from git when the working session's turn ends (XAGENT1e, design §6.4): the review's seam,
    /// <see cref="WorkingTree.ReadGitAsync"/> unless a test hands in its stand-in.
    /// </summary>
    public WorkingTree.GitRead OpinionGit { get; set; } = WorkingTree.ReadGitAsync;
}
