namespace Daoris.Driver;

/// <summary>
/// The watch loop, owned once by the library — the headless host and the desktop shell both run the
/// same algorithm (re-read the config, tick, wait). Each host keeps only its reporting half: what a
/// tick report BECOMES (console lines, event-bus notifications) differs by door; when the loop looks
/// does not.
/// </summary>
/// <remarks>
/// <para>The person's standing choices are re-read every tick, so a hold, an opt-in, or a new adapter
/// command takes effect without a restart — the shell's controls are edits to the config file, and a
/// control that needs a bounce is a control nobody trusts.</para>
///
/// <para><b>The watch keeps the running sessions.</b> A look starts what may start and
/// returns, so the loop looks again at its pace, at a nudge, or the moment a session ends, while others
/// still work. A quest published while a long session runs starts at the next look, not after it.</para>
///
/// <para><b>Its state, and who touches it.</b> What a nudge says is <see cref="WatchWakes"/>'s: any thread's nudge writes it in one order,
/// and the run's loop reads it, with no lock (its remarks say the order and what a race would break). What a running loop lends this
/// surface (the evaluation runtime, link 8's wake and asks, the home's and the refs' watchers, the window's focus) is
/// <see cref="WatchDoors"/>'s, published one reference at a time by the run as it starts each part and withdrawn as it closes. Each
/// <see cref="RunAsync"/> is one <see cref="WatchRun"/>, whose fields are its loop's own; the declarations owed acceptance are
/// <see cref="IntegrationOwed"/>'s, read by the loop's look alone. The rest is fixed at construction, or made once on first use.</para>
/// </remarks>
public sealed class DriverWatch(
    ServiceClient service, string configPath, string home, SessionProcesses processes, RemoteSyncSet? sync,
    // The live console, where something is watching. Null in the headless host: a buffer
    // nobody reads is memory spent on an audience that does not exist.
    SessionOutput? output = null,
    // The harness cache, shared across ticks for the same reason the process registry is:
    // a probe spawns a process, and re-detecting every harness every tick would be absurd.
    HarnessRoster? harnesses = null,
    // What sessions consumed. Null in the headless host for the same reason the console
    // buffer is: a record nobody reads is a file written for an audience that does not exist.
    SessionUsage? usage = null,
    // The plugins that speak, owned by whoever owns this loop and stopped with it; every tick
    // reconciles them against the catalogue, so an edit between ticks takes effect at the next.
    HookSet? hooks = null,
    // Where a session's structure is kept, shared so the shell hears each event live. Null
    // in the headless host: each tick's driver keeps the record under the home with nobody watching.
    SessionEvents? events = null,
    // Daoris's own browser, where a shell carries one. Null in the headless host, where a server
    // that drives it is not handed.
    IInAppBrowser? browser = null,
    // The adapters every look's driver and the default roster are built over: the
    // seam the parity oracle builds a look over for either path. Null is `ProviderRegistry.Built()`.
    ProviderRegistry? adapters = null)
{
    private readonly WatchWakes _wakes = new();

    private readonly WatchDoors _doors = new();

    /// <summary>The quests the evaluation runtime last read: what a door judges a held close against; null before a pass has read.</summary>
    public IReadOnlyDictionary<string, QuestView>? LastQuests => _doors.Nodes?.LastQuests;

    /// <summary>The runs held at a declined request, as the evaluation runtime last read the quests; none before a pass.</summary>
    public IReadOnlyList<RequestHold> RequestHolds => _doors.Nodes?.RequestHolds() ?? [];

    /// <summary>The runs waiting on a person's task, as the evaluation runtime last governed them; none before a pass.</summary>
    public IReadOnlyList<HumanTask> HumanTasks => _doors.Nodes?.HumanTasks() ?? [];

    /// <summary>The run steps waiting on the person, read through the evaluation runtime; none before it is wired.</summary>
    public IReadOnlyList<PersonWait> PersonWaits => _doors.Nodes?.PersonWaits() ?? [];

    /// <summary>The landed branches whose integration waits for the owner's card, as the evaluation runtime last judged them; none before a pass.</summary>
    public IReadOnlyList<IntegrationCardWait> CardWaits => _doors.Nodes?.CardWaits() ?? [];

    private readonly IntegrationOwed _owed = new();

    /// <summary>The repositories whose integration declaration waits for the owner's acceptance, as the last look read them; none before a look.</summary>
    public IReadOnlyList<DeclarationOwed> DeclarationsOwed => _owed.Under(DriverConfig.Load(configPath));

    /// <summary>
    /// The look's read of the declarations owed acceptance (<see cref="IntegrationOwed"/>): git only where a line's tip moved. What a
    /// repository in full access commits on its own line is accepted here as it lands, and said in one sentence each (returned). A read that
    /// fails keeps what the last one found and is said, never a reason to fail the look.
    /// </summary>
    internal async Task<IReadOnlyList<string>> OwedLookAsync(DriverConfig config, CancellationToken ct)
    {
        var said = new List<string>();
        try
        {
            await _owed.LookAsync(config, await service.RegistryAsync(ct).ConfigureAwait(false), DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            foreach (var owned in _owed.ByMode(DriverConfig.Load(configPath)))
            {
                if (await DeclarationAcceptance.AcceptByModeAsync(home, configPath, owned, ct).ConfigureAwait(false) is { } line) said.Add(line);
            }
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or IOException or UnauthorizedAccessException)
        {
            Log?.Failed("the read of the declarations owed acceptance", error);
        }

        return said;
    }

    /// <summary>The loop's evaluation runtime once it runs, or null: a test holds its pass with one of its own.</summary>
    internal NodeRuntime? Nodes => _doors.Nodes;

    // What a run reaches of what this watch is built over (WatchRun, WatchNodes).
    internal ServiceClient Service => service;

    internal string ConfigPath => configPath;

    internal string Home => home;

    internal SessionProcesses Processes => processes;

    internal RemoteSyncSet? Sync => sync;

    internal SessionOutput? Output => output;

    internal SessionUsage? Usage => usage;

    internal HookSet? Hooks => hooks;

    internal SessionEvents? Events => events;

    internal IInAppBrowser? Browser => browser;

    internal ProviderRegistry Adapters => _adapters;

    /// <summary>
    /// The adapters this loop runs on: the set handed in, or the built one. Each static read on a look's path reads
    /// names alone (<c>ProviderRegistry.Built().Names</c>), as <c>ChatRunner</c>'s plugin catalogue does, so a set handed in is the one that runs.
    /// </summary>
    private readonly ProviderRegistry _adapters = adapters ?? ProviderRegistry.Built();

    /// <summary>This machine's harnesses, as the loop sees them — also what a roster surface reads.</summary>
    private readonly HarnessRoster _harnesses = harnesses ?? new HarnessRoster(adapters ?? ProviderRegistry.Built(), HarnessSettings.ResolvePath(home));

    /// <inheritdoc cref="_harnesses"/>
    public HarnessRoster Harnesses => _harnesses;

    /// <summary>
    /// The sessions this loop started and still runs: kept here, since each look's driver is built
    /// fresh and a session outlives the look that started it.
    /// </summary>
    public RunningSessions Running { get; } = new();

    /// <summary>
    /// Whether this loop runs beside the lease holder, as <c>drive --share</c> does: handed to every look's driver
    /// (<see cref="Driver.Share"/>), so the first look takes up only the dead runs' moves of the lines this loop runs.
    /// </summary>
    public bool Share { get; init; }

    /// <summary>Handed to every look's driver: <see cref="Driver.Runner"/>, a test's in-process stand-in for a start's run.</summary>
    internal Func<Consideration, Action, CancellationToken, Task<StartRun>>? Runner { get; init; }

    /// <summary>Handed to every look's driver beside <see cref="Runner"/>: <see cref="Driver.Stops"/>, that stand-in's stop.</summary>
    internal Func<string, Noted, bool>? Stops { get; init; }

    /// <summary>Handed to every look's driver (<see cref="Driver.Log"/>): where a failure nothing else awaits is written.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.HandsOver"/>): the shell's chat runner's hand-over of a conversation whose
    /// agent asked. Null in the headless host, which runs no conversation.
    /// </summary>
    public Func<string, string, HandOverStep?, CancellationToken, Task<HandedOver>>? HandsOver { get; init; }

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.SendsBack"/>): the shell's chat runner, which takes a chat run's findings into its
    /// conversation only between turns. Null in the headless host, where the findings wait.
    /// </summary>
    public IChatSendBacks? SendsBack { get; init; }

    /// <summary>
    /// Told after a look's news is kept as a notice (<see cref="LookSaid"/>): the desktop wakes its notices slice, because a write
    /// this process marked wakes nothing on its own home watch. Null where no window reads the record: the headless host's.
    /// </summary>
    public Action? Noticed { get; init; }

    /// <summary>The watch's own lines, classed where each is made: a swept session, the tidy's, a line followed.</summary>
    internal LookSaid Lines => LazyInitializer.EnsureInitialized(ref _lines, () => new LookSaid(home, () => Log, () => Noticed?.Invoke()));

    private LookSaid? _lines;

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.GoesOnLost"/>): the shell's chat runner's move of a closed quest's session whose
    /// conversation is proven lost to a conversation in its tree. Null in the headless host, which runs no conversation.
    /// </summary>
    public Func<string, ContinueReason, CancellationToken, Task<StartedFrom>>? GoesOnLost { get; init; }

    /// <summary>
    /// The look's <c>limits</c> stage: the shell's chat runner's wake of the conversations that wait for an
    /// account, run beside each look as the tidy is, one at a time, and never once the loop closes. It asks nothing where no wait has
    /// come. Null in the headless host, which runs no conversation.
    /// </summary>
    public Func<DriverConfig, CancellationToken, Task>? Limits { get; init; }

    private LookStages? _stages;

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.Stages"/>): each stage of the looks and of their work beside them, timed into the
    /// next <c>watch.looked</c>'s <c>stages</c>, and a stage that hangs named in <see cref="Log"/> as it hangs. Over
    /// <see cref="Log"/> at <see cref="LookStages.SlowAfter"/>, unless a test hands its own with a shorter threshold.
    /// </summary>
    internal LookStages Stages
    {
        get => LazyInitializer.EnsureInitialized(ref _stages, () => new LookStages(Log));
        init => _stages = value;
    }

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.Reviews"/>): the review's showing in Daoris's browser, one
    /// for the shell's life, because a served tab outlives the look and the session that started it. Null in the headless host.
    /// </summary>
    public ReviewDesk? Reviews { get; init; }

    /// <summary>
    /// Whether an update is draining this loop, asked at every look: while it answers true, each look
    /// plans with <see cref="InstallUpdate.Drained"/>, so nothing new starts, and says the hold as the update's
    /// (<see cref="InstallUpdate.HeldFor"/>). The look itself goes on — the endings, the plugins' word on them, the sync —
    /// and what runs is never touched. Null, as in the headless host, is never.
    /// </summary>
    public Func<bool>? Draining { get; init; }

    /// <summary>
    /// Whether an update drains now (<see cref="Draining"/>), asked where the loop starts work beside the looks: the tidy and
    /// keeping pass, a tidy of a ref move, the wake of the conversations waiting for an account and the evaluation runtime's pass. A start
    /// the loop holds stays due, so the first moment that does not drain begins it.
    /// </summary>
    internal bool DrainsNow() => Draining?.Invoke() == true;

    /// <summary>
    /// The look's own tidy of session branches, run beside the looks once each pace, as the start's
    /// line-following runs beside them; what it says joins the report of the look that finds it done. Null keeps the watch's
    /// own, at <see cref="BranchTidying.DefaultPace"/>.
    /// </summary>
    public BranchTidying? Tidying { get; init; }

    /// <summary>
    /// What the work beside the looks waits for before it starts: the line-following and the
    /// tidy take the background's git places the first page's pulls ask for, so the shell holds them until the page's word that its
    /// first screen has its data, or a bound. Its token is the loop's. Null waits for nothing, as in the headless host.
    /// </summary>
    public Func<CancellationToken, Task>? FirstScreen { get; init; }

    /// <summary>The <see cref="FirstScreen"/> wait, where there is one; the loop's close ends it as a cancellation.</summary>
    internal Task FirstScreenAsync(CancellationToken ct) => FirstScreen?.Invoke(ct) ?? Task.CompletedTask;

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown. A plain nudge carries no topic, and means everything: the whole look, the remote
    /// sync first.</summary>
    public void Nudge() => Woken(null);

    /// <summary>
    /// Look now, because <paramref name="topic"/>'s <paramref name="subject"/> moved: a wake from a
    /// change source, never a value. It runs the look's local plan alone, and leaves the remote sync to
    /// the <c>look</c> topic's deadline, unless that deadline came with it. The topic and subject are what its <c>watch.looked</c>
    /// line says woke it. A subject is a name, never a path.
    /// </summary>
    public void Nudge(string topic, string subject) => Woken(new WatchWakes.Wake(topic, subject));

    /// <summary>
    /// The supervisor's decisions, one for this loop's life and handed to every look's
    /// driver, so a decision kept in one look is the same in the next; the window's shell hands its own, shared with its routes.
    /// </summary>
    public QuestionSupervisorDecisions Supervisor
    {
        get => _supervisor ??= new(home, NoDecisionAdmission.Instance, Log);
        init => _supervisor = value;
    }

    private QuestionSupervisorDecisions? _supervisor;

    /// <summary>
    /// The run's memory of a role's acts as each look's starts read it, asked at every look since the
    /// window's pipe and its judge come after the loop; null or answering null where none holds it, so no start is a role's and no role's
    /// release is honoured.
    /// </summary>
    public Func<IRoleStarts?>? Roles { get; init; }

    /// <summary>The decision door's half an agent supervisor's look reaches, asked at every look as <see cref="Roles"/> is; none decides without it.</summary>
    public Func<ISupervisorDesk?>? Supervising { get; init; }

    /// <summary>What watches the home's record files (<see cref="HomeWatch"/>): the machine's watchers unless a test hands its fake.</summary>
    internal IChangeSource? Source { get; init; }

    /// <summary>
    /// Told each move a change source heard: its topic and subject, a repository's refs as the ref watch
    /// judges them and every topic's wake, for the judges that keep the machine's facts. Never a plain nudge, which is a press, not a
    /// move, and never a deadline. Null tells nobody. The third argument says a ref Daoris moved itself
    /// (<see cref="RefMoves.Mine"/>): a landing or an <i>Accept</i>, whose repository the facts judge past their rest; every other move
    /// is told with it false.
    /// </summary>
    public Action<string, string, bool>? Heard { get; init; }

    /// <summary>
    /// The machine resumed from sleep: the record files' and the repositories' watchers are reopened and each
    /// judged now.
    /// </summary>
    public void Resumed()
    {
        _doors.Files?.Resumed();
        _doors.Refs?.Resumed();
    }

    /// <summary>The ref watch this loop runs, which a test steps and counts; null until the loop starts.</summary>
    internal RefWatch? Refs => _doors.Refs;

    /// <summary>Told each pass the evaluation runtime made (link 8); null tells nobody.</summary>
    internal Action<NodeRuntimePass>? Passed { get; init; }

    /// <summary>
    /// The door the evaluation runtime's <c>integrate</c> asks for a landed branch: the home's
    /// own where none is handed in; a test hands a stand-in.
    /// </summary>
    internal IIntegrationDoor? IntegrationDoor { get; init; }

    /// <summary>
    /// The evaluation runtime's own wake: a timer its records persist came due (<c>due</c>), or the awaited set it told
    /// changed (<see cref="NodesAwaited"/>). Link 8 alone, never the local plan: it wakes no look. Before the loop binds link 8, nothing.
    /// </summary>
    internal void NodesWoken(string subject) => WakeNodes(new ChainEvent(ChainEvents.Nodes, subject, ChangeTopics.Nodes));

    /// <summary>Fires link 8 alone on the loop's chain, where a run has bound it.</summary>
    internal void WakeNodes(ChainEvent wake) => _doors.Wake(wake);

    /// <summary>
    /// Ask link 8 for a pass that reads the units afresh and judges <paramref name="unit"/> first: what a landing door held by a run's graph
    /// for a judgement not made yet asks, and a press waits for within <paramref name="bound"/>. True where the pass that took the ask judged the
    /// unit, or ended, in time; false past the bound, where it failed, and where this loop runs no runtime.
    /// </summary>
    public Task<bool> PassWholeAsync(string unit, TimeSpan bound, CancellationToken ct) =>
        _doors.Asks is { } asks ? asks.WaitAsync(unit, bound, ct) : Task.FromResult(false);

    /// <summary>
    /// Look now, because a terminal's words for <paramref name="session"/> were kept: the requests topic's wake,
    /// so <c>watch.looked</c> says the words woke it, not that something nudged.
    /// </summary>
    public void Said(string session) => Nudge(ChangeTopics.Requests, session);

    /// <summary>
    /// The window has the person's focus: a moment to ask about each open pull request, within a minute. It wakes
    /// no look; where the loop has not started, it is kept, once, for the moments.
    /// </summary>
    public void Focused() => _doors.Focus.Focused();

    // In WatchWakes' order: the plain flag, the wake, the count and the pause swap; told to the facts' judges and link 2 between.
    private void Woken(WatchWakes.Wake? wake)
    {
        if (wake is null) _wakes.Everything();

        // A move is told to the facts' judges too; a ref's is told where the ref watch hears it, so not twice.
        if (wake is not null && wake.Topic != ChangeTopics.Refs) Heard?.Invoke(wake.Topic, wake.Subject, false);
        _wakes.Woken(wake);
        // What can unblock a refused tidy wakes link 2 too: a press (a plain nudge), the home's files, a ledger family.
        _doors.Refs?.Unblocked(wake is null ? ChainEvents.Press
            : wake.Topic is ChangeTopics.Ledger or ChangeTopics.Requests ? ChainEvents.Ledger
            : ChainEvents.Home);
        _wakes.Counted();
    }

    /// <summary>
    /// Say how this home's git is wired (<see cref="GitLog.Wired"/>): which git Tools runs, and the doors laid out beside it. Never a
    /// reason to fail, as the log is not.
    /// </summary>
    internal static void Wired(string home)
    {
        try
        {
            var git = Tools.Resolve(Tools.Read(home), home, "git");
            GitLog.Wired(home, git.Way?.ToString().ToLowerInvariant(), git.Version, GitWiring.SshForm(home),
                GitWiring.DoorPath(home, GitWiring.RemoteHelper) is not null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
        }
    }

    /// <summary>
    /// Tick until cancelled. <paramref name="onReport"/> receives every tick's report with the config
    /// that produced it; <paramref name="onError"/> decides what a failed tick means — the shell says
    /// so and keeps watching (an unattended loop outlives its service's restarts), while a null lets
    /// the failure propagate, which is the headless host's exit-2 contract.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>It returns only once every session it started has written how it ended</b>: a
    /// close cancels them on the loop's own token, and each records itself stopped by the driver's
    /// shutdown, to be carried on at the next start. A failure that ends the loop ends them the same way
    /// first, rather than leave them working with nothing watching.
    /// </remarks>
    /// <param name="said">
    /// Where a door can say a part of a look alone (the headless host's console): handed what a look that failed or that the loop closed
    /// had said before, its lines and what ended, before the failure is heard or ends the loop. Null carries them into
    /// the next look's report instead, as the orphan sweep's lines are: a part handed to <paramref name="onReport"/> would
    /// replace a screen's standing answers with nothing, and a look that failed returns no report.
    /// </param>
    public Task RunAsync(
        Func<TickReport, DriverConfig, Task> onReport,
        Func<Exception, Task>? onError,
        CancellationToken ct,
        Action<TickReport>? said = null) =>
        new WatchRun(this, _wakes, _doors, onReport, onError, ct, said).RunAsync();
}
