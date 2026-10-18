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

    /// <summary>How a helper's pass is run, unless a test hands in a stand-in, since a real pass spawns git and an agent (MOD8).</summary>
    public Func<HelperPassAsk, ReviewerChoice, CancellationToken, Task<HelperPassRun>>? HelperPasses { get; set; }

    /// <summary>How an asker's tree is read, its last commit and its repository's registered checkout, unless a test hands in a stand-in (MOD8).</summary>
    public Func<string, CancellationToken, Task<(string? Tip, string? Root)>>? HelperTrees { get; set; }

    /// <summary>How a helper's agent is chosen, unless a test hands in a choice.</summary>
    public Func<HelperAsk, HelperRule, PriorSession, CancellationToken, Task<ReviewerChoice>>? HelperChoices { get; set; }

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

    /// <summary>Where a session's structure is kept (D76 §2): the shell's shared record, or the home's own, with nobody watching.</summary>
    public SessionEvents Events { get; } = events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));

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
