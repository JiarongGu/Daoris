using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Which part of the look a look runs (FABRIC-EVENT3a, the fabric events design §A3, §C5, §C6 link 5). The whole look's only
/// further parts are the remote sync at its head and the lost-claim stop after it, so a wake defers those to the <c>look</c>
/// topic's deadline and skips no judgement the whole look would have made.
/// </summary>
public enum LookPart
{
    /// <summary>
    /// The <c>look</c> topic's: the remote sync and the lost-claim stop, then the local plan. At the topic's deadline
    /// (<c>pollSeconds</c> after the last whole look), at the loop's start, after a failed look's pace, and on a plain nudge.
    /// </summary>
    Look,

    /// <summary>
    /// A wake's (a <c>ledger</c> family moved, a terminal's words kept, a session's end): the local plan alone, every part of the
    /// look but the remote sync and the lost-claim stop, the sync after an ending it reports included. PARALLEL1b: the stop is a
    /// plan's too where a sync beside the looks ended since the last look asked, while a session here held a quest.
    /// </summary>
    Plan,
}

/// <summary>
/// The watch loop, owned once by the library — the headless host and the desktop shell both ran the
/// same algorithm (re-read the config, tick, wait) as two copies, four six-argument constructions
/// between them. Each host keeps only its reporting half: what a tick report BECOMES (console lines,
/// event-bus notifications) differs by door; when the loop looks does not.
/// </summary>
/// <remarks>
/// <para>The person's standing choices are re-read every tick, so a hold, an opt-in, or a new adapter
/// command takes effect without a restart — the shell's controls are edits to the config file, and a
/// control that needs a bounce is a control nobody trusts.</para>
///
/// <para><b>The watch keeps the running sessions</b> (DEV3, D115 §3.1). A look starts what may start and
/// returns, so the loop looks again at its pace, at a nudge, or the moment a session ends, while others
/// still work. A quest published while a long session runs starts at the next look, not after it.</para>
/// </remarks>
public sealed class DriverWatch(
    ServiceClient service, string configPath, string home, SessionProcesses processes, RemoteSyncSet? sync,
    // The live console, where something is watching (D49 §2). Null in the headless host: a buffer
    // nobody reads is memory spent on an audience that does not exist.
    SessionOutput? output = null,
    // The harness cache (D49 §4), shared across ticks for the same reason the process registry is:
    // a probe spawns a process, and re-detecting every harness every tick would be absurd.
    HarnessRoster? harnesses = null,
    // What sessions consumed (TOOL3). Null in the headless host for the same reason the console
    // buffer is: a record nobody reads is a file written for an audience that does not exist.
    SessionUsage? usage = null,
    // The plugins that speak (D64), owned by whoever owns this loop and stopped with it; every tick
    // reconciles them against the catalogue, so an edit between ticks takes effect at the next.
    HookSet? hooks = null,
    // Where a session's structure is kept (D76 §2), shared so the shell hears each event live. Null
    // in the headless host: each tick's driver keeps the record under the home with nobody watching.
    SessionEvents? events = null,
    // Daoris's own browser (D78), where a shell carries one. Null in the headless host, where a server
    // that drives it is not handed.
    IInAppBrowser? browser = null)
{
    private CancellationTokenSource _pause = new();

    private volatile NodeRuntime? _nodes;

    /// <summary>The quests the evaluation runtime last read (FABRIC-CLOSE1q3): what a door judges a held close against; null before a pass has read.</summary>
    public IReadOnlyDictionary<string, QuestView>? LastQuests => _nodes?.LastQuests;

    /// <summary>The loop's evaluation runtime once it runs, or null: a test holds its pass with one of its own (PARALLEL1h).</summary>
    internal NodeRuntime? Nodes => _nodes;

    // Every nudge counted, so one that lands while a look runs — before the wait it would have ended —
    // still ends that wait (DEV3): looks are short now, and a person's control nudges the loop often.
    private int _nudges;

    /// <summary>This machine's harnesses, as the loop sees them — also what a roster surface reads.</summary>
    private readonly HarnessRoster _harnesses = harnesses ?? new HarnessRoster(AdapterSet.Built(), HarnessSettings.ResolvePath(home));

    /// <inheritdoc cref="_harnesses"/>
    public HarnessRoster Harnesses => _harnesses;

    /// <summary>
    /// The sessions this loop started and still runs (DEV3): kept here, since each look's driver is built
    /// fresh and a session outlives the look that started it.
    /// </summary>
    public RunningSessions Running { get; } = new();

    /// <summary>Handed to every look's driver: <see cref="Driver.Runner"/>, a test's in-process stand-in for a start's run (DEV3).</summary>
    internal Func<Consideration, Action, CancellationToken, Task<StartRun>>? Runner { get; init; }

    /// <summary>Handed to every look's driver beside <see cref="Runner"/>: <see cref="Driver.Stops"/>, that stand-in's stop (DEV3c).</summary>
    internal Func<string, Noted, bool>? Stops { get; init; }

    /// <summary>Handed to every look's driver (<see cref="Driver.Log"/>): where a failure nothing else awaits is written (ANSWER2).</summary>
    public MachineLog? Log { get; init; }

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.HandsOver"/>): the shell's chat runner's hand-over of a conversation whose
    /// agent asked (MIXAGENT1c). Null in the headless host, which runs no conversation.
    /// </summary>
    public Func<string, string, CancellationToken, Task<HandedOver>>? HandsOver { get; init; }

    /// <summary>
    /// Told after a look's news is kept as a notice (NOTIFY2h, <see cref="LookSaid"/>): the desktop wakes its notices slice, since a write
    /// this process marked wakes nothing on its own home watch. Null where no window reads the record: the headless host's.
    /// </summary>
    public Action? Noticed { get; init; }

    /// <summary>The watch's own lines, classed where each is made (NOTIFY2h): a swept session, the tidy's, a line followed.</summary>
    private LookSaid Lines => LazyInitializer.EnsureInitialized(ref _lines, () => new LookSaid(home, () => Log, () => Noticed?.Invoke()));

    private LookSaid? _lines;

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.GoesOnLost"/>): the shell's chat runner's move of a closed quest's session whose
    /// conversation is proven lost to a conversation in its tree (SESSCTX1c part B). Null in the headless host, which runs no conversation.
    /// </summary>
    public Func<string, ContinueReason, CancellationToken, Task<StartedFrom>>? GoesOnLost { get; init; }

    /// <summary>
    /// The look's <c>limits</c> stage (LIMITRESUME1c, D206 §3.4): the shell's chat runner's wake of the conversations that wait for an
    /// account, run beside each look as the tidy is, one at a time, and never once the loop closes. It asks nothing where no wait has
    /// come. Null in the headless host, which runs no conversation.
    /// </summary>
    public Func<DriverConfig, CancellationToken, Task>? Limits { get; init; }

    private LookStages? _stages;

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.Stages"/>): each stage of the looks and of their work beside them, timed into the
    /// next <c>watch.looked</c>'s <c>stages</c>, and a stage that hangs named in <see cref="Log"/> as it hangs (LOOKWAIT1). Over
    /// <see cref="Log"/> at <see cref="LookStages.SlowAfter"/>, unless a test hands its own with a shorter threshold.
    /// </summary>
    internal LookStages Stages
    {
        get => LazyInitializer.EnsureInitialized(ref _stages, () => new LookStages(Log));
        init => _stages = value;
    }

    /// <summary>
    /// Handed to every look's driver (<see cref="Driver.Reviews"/>): the review's showing in Daoris's browser (REVIEWENV1d), one
    /// for the shell's life, since a served tab outlives the look and the session that started it. Null in the headless host.
    /// </summary>
    public ReviewDesk? Reviews { get; init; }

    /// <summary>
    /// Whether an update is draining this loop (UPDATE1, D139 §2), asked at every look: while it answers true, each look
    /// plans with <see cref="InstallUpdate.Drained"/>, so nothing new starts, and says the hold as the update's
    /// (<see cref="InstallUpdate.HeldFor"/>). The look itself goes on — the endings, the plugins' word on them, the sync —
    /// and what runs is never touched. Null, as in the headless host, is never.
    /// </summary>
    public Func<bool>? Draining { get; init; }

    /// <summary>
    /// The look's own tidy of session branches (AUTOTIDY1, AUTOTIDY2a, D88's notes), run beside the looks once each pace, as the start's
    /// line-following runs beside them; what it says joins the report of the look that finds it done. Null keeps the watch's
    /// own, at <see cref="BranchTidying.DefaultPace"/>.
    /// </summary>
    public BranchTidying? Tidying { get; init; }

    /// <summary>
    /// What the work beside the looks waits for before it starts (STARTFAST1b, the start-path design §5c): the line-following and the
    /// tidy take the background's git places the first page's pulls ask for, so the shell holds them until the page's word that its
    /// first screen has its data, or a bound. Its token is the loop's. Null waits for nothing, as in the headless host.
    /// </summary>
    public Func<CancellationToken, Task>? FirstScreen { get; init; }

    /// <summary>The <see cref="FirstScreen"/> wait, where there is one; the loop's close ends it as a cancellation.</summary>
    private Task FirstScreenAsync(CancellationToken ct) => FirstScreen?.Invoke(ct) ?? Task.CompletedTask;

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown. A plain nudge carries no topic, and means everything: the whole look, the remote
    /// sync first (FABRIC-EVENT3a).</summary>
    public void Nudge() => Woken(null);

    /// <summary>
    /// Look now, because <paramref name="topic"/>'s <paramref name="subject"/> moved (FABRIC-EVENT2, the design §A3): a wake from a
    /// change source, never a value. It runs the look's local plan alone (FABRIC-EVENT3a, §C6 link 5), and leaves the remote sync to
    /// the <c>look</c> topic's deadline, unless that deadline came with it. The topic and subject are what its <c>watch.looked</c>
    /// line says woke it. A subject is a name, never a path.
    /// </summary>
    public void Nudge(string topic, string subject) => Woken(new Wake(topic, subject));

    /// <summary>
    /// The supervisor's decisions (SUPERVISOR1a-i, D208; the supervisor design §3.4), one for this loop's life and handed to every look's
    /// driver, so a decision kept in one look is the same in the next; the window's shell hands its own, shared with its routes.
    /// </summary>
    public QuestionSupervisorDecisions Supervisor
    {
        get => _supervisor ??= new(home, NoDecisionAdmission.Instance, Log);
        init => _supervisor = value;
    }

    private QuestionSupervisorDecisions? _supervisor;

    /// <summary>
    /// The run's memory of a role's acts as each look's starts read it (STEWARDROLE1b, D209 §3.1a, §5.1a), asked at every look since the
    /// window's pipe and its judge come after the loop; null or answering null where none holds it, so no start is a role's and no role's
    /// release is honoured.
    /// </summary>
    public Func<IRoleStarts?>? Roles { get; init; }

    /// <summary>What watches the home's record files (<see cref="HomeWatch"/>): the machine's watchers unless a test hands its fake.</summary>
    internal IChangeSource? Source { get; init; }

    /// <summary>
    /// Told each move a change source heard (PARALLEL1a, D181 point 2): its topic and subject, a repository's refs as the ref watch
    /// judges them and every topic's wake, for the judges that keep the machine's facts. Never a plain nudge, which is a press, not a
    /// move, and never a deadline. Null tells nobody. The third argument says a ref Daoris moved itself (OWNMOVES1,
    /// <see cref="RefMoves.Mine"/>): a landing or an <i>Accept</i>, whose repository the facts judge past their rest; every other move
    /// is told with it false.
    /// </summary>
    public Action<string, string, bool>? Heard { get; init; }

    private HomeWatch? _files;

    private RefWatch? _refs;

    /// <summary>
    /// The machine resumed from sleep (FABRIC-EVENT4, FABRIC-END3): the record files' and the repositories' watchers are reopened and each
    /// judged now.
    /// </summary>
    public void Resumed()
    {
        _files?.Resumed();
        _refs?.Resumed();
    }

    /// <summary>The ref watch this loop runs (FABRIC-END3), which a test steps and counts; null until the loop starts.</summary>
    internal RefWatch? Refs => _refs;

    /// <summary>Told each pass the evaluation runtime made (link 8, FABRIC-RUN1d's tests); null tells nobody.</summary>
    internal Action<NodeRuntimePass>? Passed { get; init; }

    /// <summary>
    /// The evaluation runtime's own wake (FABRIC-RUN1d, §A3): a timer its records persist came due (<c>due</c>), or the awaited set it told
    /// changed (<see cref="NodesAwaited"/>). Link 8 alone, never the local plan: it wakes no look. Before the loop binds link 8, nothing.
    /// </summary>
    internal void NodesWoken(string subject) => WakeNodes(new ChainEvent(ChainEvents.Nodes, subject, ChangeTopics.Nodes));

    /// <summary>The runtime's wake whose pass first runs the plugins' supervision: the awaited set it told changed (FABRIC-RUN1d).</summary>
    private const string NodesAwaited = "awaited";

    /// <summary>Fires link 8 alone on the loop's chain (FABRIC-RUN1d): set while the loop runs with a hook set, null before and after.</summary>
    private Action<ChainEvent>? _nodesWake;

    private void WakeNodes(ChainEvent wake) => Volatile.Read(ref _nodesWake)?.Invoke(wake);

    /// <summary>The doors' asks for a whole pass of link 8 (FABRIC-CUTOVER1d, <see cref="PassAsks"/>): set while the runtime runs, null otherwise.</summary>
    private PassAsks? _passAsks;

    /// <summary>
    /// Ask link 8 for a pass that reads the units afresh and judges every governed run (FABRIC-CUTOVER1d; the cutover design §4): what a landing
    /// door held by a run's graph for a judgement not made yet asks, and a press waits for within <paramref name="bound"/>. True where a pass that
    /// took the ask ended in time; false past the bound, where it failed, and where this loop runs no runtime.
    /// </summary>
    public Task<bool> PassWholeAsync(TimeSpan bound, CancellationToken ct) =>
        Volatile.Read(ref _passAsks) is { } asks ? asks.WaitAsync(bound, ct) : Task.FromResult(false);

    /// <summary>
    /// Look now, because a terminal's words for <paramref name="session"/> were kept (FABRIC-EVENT2b): the requests topic's wake,
    /// so <c>watch.looked</c> says the words woke it, not that something nudged.
    /// </summary>
    public void Said(string session) => Nudge(ChangeTopics.Requests, session);

    // The window's focus told to the due moments of a pull request's ask (FABRIC-END6); one that comes before the loop starts them is kept
    // until they exist (FABRIC-END6c), since a start's window takes the focus first.
    private readonly PendingFocus _focus = new();

    /// <summary>
    /// The window has the person's focus (FABRIC-END6, §A4.3): a moment to ask about each open pull request, within D148's minute. It wakes
    /// no look; where the loop has not started, it is kept, once, for the moments.
    /// </summary>
    public void Focused() => _focus.Focused();

    // What the last nudge said woke the loop: null for a plain nudge, which means everything.
    private Wake? _woke;

    private sealed record Wake(string Topic, string Subject);

    // Whether a plain nudge came since the loop last took its nudges up (FABRIC-EVENT3a): set before the nudge is counted, so the look
    // that counts it is whole, whatever topic's wake came after it and replaced `_woke`.
    private int _everything;

    // The wakes since the last look, which a plan's landing looks at alone (FABRIC-END7); the whole look takes them and lands on all.
    private readonly LandingWakes _landing = new();

    /// <summary>The wakes the landing has not judged with yet (FABRIC-END7), which a test reads.</summary>
    internal LandingWakes Landing => _landing;

    /// <summary>
    /// A look's landing did not judge with the wakes it took (FABRIC-END7's review): they go back, and where a landing still ran, its end
    /// offers them again. A landing that ended before they went back found nothing held, so they are offered here.
    /// </summary>
    private void Unspent(LandingTaken taken, bool busy)
    {
        _landing.Unspent(taken, heldBack: busy);
        if (busy && !Running.Landing) Reoffered();
    }

    /// <summary>
    /// A landing pass ended (FABRIC-END7's review): the wakes held back while it ran wake a plan now, as a session's end does, rather than
    /// wait for the next wake or the whole look. Nothing held back wakes nothing.
    /// </summary>
    private void Reoffered()
    {
        if (_landing.Reoffer()) Woken(LandingEnd, heard: false);
    }

    /// <summary>What <c>watch.looked</c> says woke a plan a landing's end offered its held wakes to: the ledger's sessions, as a session's end.</summary>
    private static readonly Wake LandingEnd = new(ChangeTopics.Ledger, "sessions");

    /// <param name="heard">False for a wake the landing already holds (<see cref="Reoffered"/>), which is not heard a second time.</param>
    private void Woken(Wake? wake, bool heard = true)
    {
        if (wake is null) Interlocked.Exchange(ref _everything, 1);
        else if (heard) _landing.Hear(wake.Topic, wake.Subject);

        // PARALLEL1a: a move is told to the facts' judges too; a ref's is told where the ref watch hears it, so not twice.
        if (wake is not null && heard && wake.Topic != ChangeTopics.Refs) Heard?.Invoke(wake.Topic, wake.Subject, false);
        Volatile.Write(ref _woke, wake);
        // FABRIC-END4a: what can unblock a refused tidy wakes link 2 too: a press (a plain nudge), the home's files, a ledger family.
        _refs?.Unblocked(wake is null ? ChainEvents.Press
            : wake.Topic is ChangeTopics.Ledger or ChangeTopics.Requests ? ChainEvents.Ledger
            : ChainEvents.Home);
        Interlocked.Increment(ref _nudges);

        // Swap first, atomically, so two racing nudges each cancel a source that is no longer current
        // — the swapped-out source is cancelled, disposed, and never waited on again.
        var paused = Interlocked.Exchange(ref _pause, new CancellationTokenSource());
        paused.Cancel();
        paused.Dispose();
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
    /// 🔴 <b>It returns only once every session it started has written how it ended</b> (REV3, D104): a
    /// close cancels them on the loop's own token, and each records itself stopped by the driver's
    /// shutdown, to be carried on at the next start. A failure that ends the loop ends them the same way
    /// first, rather than leave them working with nothing watching.
    /// </remarks>
    /// <param name="said">
    /// Where a door can say a part of a look alone (the headless host's console): handed what a look that failed or was closed
    /// had said before (DEV3c), its lines and what ended, before the failure is heard or ends the loop. Null carries them into
    /// the next look's report instead, as the orphan sweep's lines are: a part handed to <paramref name="onReport"/> would
    /// replace a screen's standing answers with nothing, and a look that failed returns no report.
    /// </param>
    public async Task RunAsync(
        Func<TickReport, DriverConfig, Task> onReport,
        Func<Exception, Task>? onError,
        CancellationToken ct,
        Action<TickReport>? said = null)
    {
        // Once, as the loop starts: how its git is wired, a coded line (GITAUTH1b, the git-auth design §11).
        Wired(home);

        // Once, before the first tick: what the last run left `working` with nothing behind it — a
        // crash, a kill, a power cut — ended before this tick counts it against the cap and its
        // repository's lock (2026-09-25). Retried each tick until the service has answered it once.
        var swept = false;

        // What is said and not yet in a report: the sweep's lines, and what a look that failed or was closed had said (DEV3c).
        // The next report opens with them, or `said` has them as the failure is heard.
        var carried = new List<string>();
        var carriedEnded = new List<SessionEnded>();
        void SayCarried()
        {
            if (said is null || (carried.Count == 0 && carriedEnded.Count == 0)) return;
            said(new TickReport([], [.. carried], Progressed: false, Concluded: [.. carriedEnded]));
            carried.Clear();
            carriedEnded.Clear();
        }

        // The last choices that read, for the wait: a torn file keeps the pace it had.
        var config = DriverConfig.Empty;

        // Once, as the loop starts (WSSETUP5, D124 §3.1): every repository with a checkout here registered from its line,
        // since a line the person moved outside Daoris is read nowhere else. Beside the first looks rather than before
        // them: a workspace's lines are read with git, seconds of it, and no start should wait on that. What it says
        // joins the next look's report; a pass the service did not answer is tried again at the next look.
        Task<IReadOnlyList<string>?>? following = null;
        var followed = false;

        // The session branches tidied beside the looks (AUTOTIDY1, AUTOTIDY2a): a pass once each pace, its lines joining the report
        // of the look that finds it done, as the following's do.
        // KEEP1b: the downloads' rule reads and locks the loop's own choices, as the Downloads row's press does.
        // NOTIFY2h2: each of its lines classed as it is made, the look's pass's and link 2's alike.
        // KEEP1g: history's daily clear judges a live session by the loop's processes, and forgets a cleared one's numbering in its record.
        // KEEP1f: the floor's third act clears the browser's caches only through the shell's browser host, where it carries one.
        var tidy = Tidying ?? new BranchTidying(home)
        {
            ConfigPath = configPath, Sizes = SpaceSizes.Shared, Look = Lines, Processes = processes, Events = events, Caches = browser as IBrowserCaches,
            Volume = SpaceVolumes.Read,
        };
        Task<IReadOnlyList<string>>? tidying = null;

        // LIMITRESUME1c: the conversations that wait for an account, woken beside the looks (D206 §3.4), one pass at a time.
        Task? limiting = null;

        // What woke each look, for its line in the machine log (FABRIC-EVENT2): the loop's start, its pace, a session's end, or a
        // nudge with what it said moved; and how many nudges the look took up at once.
        var by = "start";
        Wake? woke = null;
        var counted = Volatile.Read(ref _nudges);

        // The `look` topic's deadline (FABRIC-EVENT3a, the design §C5): `pollSeconds` after the last whole look ended, whatever woke
        // the looks between, so a stream of wakes never holds the reconciliation, or the remote sync in it, back.
        var whole = Stopwatch.GetTimestamp();

        // The look is link 5 of the design §C6 (FABRIC-EVENT3a), on this loop's own chain: a wake is a `ledger` event, and runs the
        // local plan alone; the `look` topic's deadline is its `timer(look)`, and runs the whole look, the remote sync first. The loop
        // stays the topic's queue, as it was the look's: what wakes it during a look is one look after it, and its wait hears a
        // session's end, which a change watch's pump does not, so the watch under the chain is never started.
        await using var queue = new ChangeWatch();
        var chain = new Chain(queue) { Log = Log };

        // A plugin asked about an open pull request when the remote's facts may have moved (FABRIC-END6, §A4.3, link 6): origin's line
        // heard by the ref watch below, Daoris's push and the window's focus, within D148's minute. Made first, so the ref watch's first
        // judgements read each line's baseline.
        await using var moments = PullRequestMoments.ForDriver(home, service.RegistryAsync, Log);
        moments.Start();
        _focus.Ready(moments.Focused);

        // The home's record files heard, not found at the pace (FABRIC-EVENT4, §A4.1, link 7): the configuration, the plugins folder,
        // the moved lines and the held words each wake the local plan by their topic, and the pace stays their deadline. Both
        // loops run this one watch, so the headless host and the shell hear the same files.
        await using var files = HomeWatch.ForDriver(home, configPath, Nudge, source: Source, log: Log);
        _files = files;
        files.Start();

        // The refs heard on each repository's common git dir (FABRIC-END3, §B4, link 1): a ref move judges that repository's ends on its
        // own, never the look and never the remote sync, and the tidy's pass below stays the reconciliation at its pace.
        // Link 2 on the same chain (FABRIC-END4a, §C3, §C6): the tidy as an effect, tried where link 1 found a repository held, on the
        // wakes that can unblock it, at the `refs` deadline and at the start; what it says joins the next look's report.
        var effected = new List<string>();
        var effect = new TidyEffect(
            home,
            async (name, token) => (await RefWatch.RepositoriesAsync(home, service.RegistryAsync, token).ConfigureAwait(false))
                .FirstOrDefault(each => string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase)),
            token => BranchTidying.InUseAsync(home, service, asked => BranchTidying.ReviewingAsync(Reviews, service, asked), token))
        {
            Saying = tidy,
            Log = Log,
            Said = lines =>
            {
                lock (effected) effected.AddRange(lines);
            },
        };
        await using var refs = RefWatch.ForDriver(home, service.RegistryAsync, source: Source, log: Log,
            heard: (repository, mine) =>
            {
                LandingWakes.Moved(home, repository, Nudge); // FABRIC-END7: a due tree that moved wakes its landing.
                // PARALLEL1a: and the facts that repository's refs move; OWNMOVES1: past their rest where Daoris moved them itself.
                Heard?.Invoke(ChangeTopics.Refs, repository, mine);
            },
            tidy: effect);
        _refs = refs;
        refs.Start();

        // The ledger heard, not found at the pace (FABRIC-EVENT3b, §A4.2): one wait on the service's change stream for the loop's life,
        // each family that moved waking the local plan by the `ledger` topic, as a quest an agent published, and the pace its deadline.
        using var listening = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ledger = LedgerChanges.RunAsync(service, Nudge, Log, listening.Token);

        // The start's reconciliation (canary 16): every pending tidy found from the persisted facts and tried once, beside the first looks.
        var recovering = Task.Run(async () =>
        {
            try
            {
                await refs.RecoverAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The loop closed before the start's reconciliation ended: the next start finds what is still pending.
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Log?.Failed("the start's reconciliation of the tidy", error);
            }
        }, CancellationToken.None);
        // FABRIC-RUN1, link 8: the evaluation runtime, begun after the plan on every wake it hears and at the look's deadline, and run beside
        // the look (PARALLEL1h); only where this loop
        // holds a hook set, and its passes write only where it holds the home's lease. It reads nothing unless a graph naming a plugin's
        // performed node governs a unit, and the next timer the node records persist wakes link 8 alone at its moment (FABRIC-RUN1d). FABRIC-CLOSE1b: a closure graph
        // arms it too, and its `quest.close` is sent through this loop's service client, the local host's close door. FABRIC-CLOSE1l: a quest is
        // bound at its take, as its first session opens (Driver's start), never here.
        var nodes = hooks is null ? null : new NodeRuntime(new NodeRecordFiles(home), hooks, () => DateTimeOffset.UtcNow, () => HomeLease.HeldHere(home), service);
        _nodes = nodes;
        // FABRIC-RUN1d: the runtime's own wakes run link 8 alone: its timer and a plugin's step, never the local plan.
        using var nodesDue = new System.Threading.Timer(_ => NodesWoken("due"));
        // SUPERVISOR1a-i (the supervisor design §3.4): the look nudged at a held decision's goesOnAt, which releases it.
        using var supervisorDue = new System.Threading.Timer(_ => Nudge(ChangeTopics.Words, SupervisorHanded.From));

        TickReport? looked = null;
        chain.Bind(ChainRanks.Plan, async (heard, token) =>
        {
            // FABRIC-RUN1e: choices that no longer arm the runtime withdraw its awaited set before the plan's supervision reads it, so the
            // look that reads the last graph's removal starts no plugin for it; link 8 then reads nothing.
            if (nodes is not null) await Stages.TimeAsync("withdraw", () => nodes.WithdrawAsync(config, token)).ConfigureAwait(false);
            looked = await LookAsync(heard.Any(cause => cause.Key == Whole) ? LookPart.Look : LookPart.Plan, token).ConfigureAwait(false);
            return new ChainFired(looked.Progressed ? 1 : 0, []);
        });

        if (nodes is not null)
        {
            // PARALLEL1h (D181 point 3): link 8 begins the pass beside the look on the running set, one at a time and one more after it
            // where asked, so a read or a plugin that holds a pass holds no next look; the closing loop waits for it as it waits for the
            // sessions. A whole look's ask is kept until a pass takes it, so a pass that follows reads the units afresh for it.
            NodeRuntime runtime = nodes;
            HookSet supervised = hooks!;
            var wholeAsked = 0;
            var superviseAsked = 0;
            // FABRIC-CUTOVER1d: a landing door's ask for a whole pass wakes link 8 alone, as the runtime's own wakes do; the pass that begins after
            // it takes it, reading the units afresh, and answers it once it ends.
            var asks = new PassAsks(() => NodesWoken("door"));
            chain.Bind(ChainRanks.Nodes, (heard, token) =>
            {
                if (heard.Any(cause => cause.Key == Whole)) Interlocked.Exchange(ref wholeAsked, 1);
                if (heard.Any(cause => cause is { Type: ChainEvents.Nodes, Subject: NodesAwaited })) Interlocked.Exchange(ref superviseAsked, 1);
                Running.PassBeside(PassAsync, token);
                return Task.FromResult(ChainFired.Nothing);
            });

            // FABRIC-RUN1d: the runtime's own wakes and a plugin's step fire link 8 alone on this loop's chain, beside whatever look runs; one
            // that fails is logged and the next wake or look passes again.
            Volatile.Write(ref _nodesWake, wake => _ = Task.Run(async () =>
            {
                try
                {
                    await chain.FireAsync([wake], ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // The loop closed: nothing is passed after it.
                }
                catch (Exception error)
                {
                    Log?.Failed("the evaluation runtime's own wake", error);
                }
            }, CancellationToken.None));
            supervised.StepArrived = plugin => WakeNodes(new ChainEvent(ChainEvents.PluginStep, plugin, ChangeTopics.Nodes));
            Volatile.Write(ref _passAsks, asks);

            // One pass, on the choices the latest look read. What it says joins the next look's report; what it acted on is in the records.
            async Task<IReadOnlyList<string>> PassAsync(CancellationToken token)
            {
                var asked = asks.Take();
                var whole = (Interlocked.Exchange(ref wholeAsked, 0) == 1) | asked.Asked;
                var lines = new List<string>();
                NodeRuntimePass pass;
                try
                {
                    // FABRIC-RUN1d: a set naming a plugin anew starts its process here, with no plan, as the plan's supervision did at the look
                    // it woke; withdrawn first where the choices no longer arm the runtime (FABRIC-RUN1e), so it starts nothing for a removed graph.
                    if (Interlocked.Exchange(ref superviseAsked, 0) == 1 && !await runtime.WithdrawAsync(config, token).ConfigureAwait(false))
                    {
                        lines.AddRange(await Stages.TimeAsync("supervise", () => supervised.ReconcileAsync(PluginCatalog.Load(home), token), beside: true)
                            .ConfigureAwait(false));
                    }

                    pass = await Stages.TimeAsync("nodes", () => runtime.PassAsync(new NodeRuntimeLook(config, whole)
                    {
                        Units = NodeUnitsAsync,
                        Steps = () => [.. PluginCatalog.Load(home).Contributing.SelectMany(plugin => plugin.Manifest.Steps)],
                        // FABRIC-PERFORM1d: a receipt's role is what its plugin's manifest declares of its schema.
                        Resources = plugin => PluginCatalog.Load(home).Contributing.FirstOrDefault(each => each.Manifest.Id == plugin)?.Manifest.Resources,
                        // FABRIC-PERFORM1i: an observed node's event is bound to its branch's tip read now, once per checkout per pass.
                        Tips = NodeTips.ReadAsync,
                        // FABRIC-RUN1b: the look's git reads for `pushed` and an ahead `pull-request`, asked only where one waits on them.
                        Remotes = NodeTips.RemotesAsync,
                        Holds = NodeTips.HoldsAsync,
                        // FABRIC-RUN1c: D157's gates for a unit whose opinion or look waits, read as a landing door reads them; and a quest
                        // whose run is bound to a named version left to that version.
                        Gates = new NodeGates(home, service).ReadAsync,
                        Bound = NodeRuntime.BoundIn(home),
                    }, token), beside: true).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException
                                                  or UnauthorizedAccessException)
                {
                    PassAsks.Done(asked, passed: false);
                    return [.. lines, $"work  the plugins' steps were not judged this time, and are judged again at the next look: {error.Message}"];
                }
                catch
                {
                    PassAsks.Done(asked, passed: false);
                    throw;
                }

                // FABRIC-CUTOVER1d: the doors that asked this pass read the runs' records it kept.
                PassAsks.Done(asked, passed: true);
                NodesDue(nodesDue, runtime.NextDue);
                Passed?.Invoke(pass);

                // A set naming a plugin anew wakes link 8 once more (FABRIC-RUN1d), whose supervision starts the process the start then goes to.
                if (pass.AwaitedChanged) NodesWoken(NodesAwaited);
                var acted = pass.Performed.Count(done => Acted.Contains(done.What));
                if (acted > 0) Log?.Info("chain.fired", ("link", ChainRanks.Nodes), ("topic", ChangeTopics.Nodes), ("count", acted));
                return [.. lines, .. NodeRuntimeLines(pass)];
            }
        }

        Task<NodeUnitsRead> NodeUnitsAsync(CancellationToken token) => ReadNodeUnitsAsync(home, service, token);

        // One look of the part link 5 heard, asked once whether an update drains, so the plan and what the report says of it
        // agree (UPDATE1).
        async Task<TickReport> LookAsync(LookPart part, CancellationToken token)
        {
            var draining = Draining?.Invoke() == true;
            // FABRIC-END7's review: the wakes this look's landing judges with, put back unless it did judge with them.
            var taken = _landing.Take(whole: part == LookPart.Look);
            var driver = new Driver(
                    service, draining ? InstallUpdate.Drained(config) : config, AdapterSet.Built(), home, processes, sync, output,
                    _harnesses, usage, hooks, events, browser, Running)
                {
                    Runner = Runner, Stops = Stops, HandsOver = HandsOver, GoesOnLost = GoesOnLost, Log = Log, Noticed = Noticed, Stages = Stages, Reviews = Reviews,
                    Part = part, Woke = taken.Woke,
                    LandingEnded = Reoffered, Wanted = token => WantedAsync(part, token), WokeBack = () => Unspent(taken, busy: false),
                    // FABRIC-CUTOVER1c: an ended graph run whose chain came back here re-arms link 8, which a wake then judges on what is there now.
                    RunReturned = run =>
                    {
                        if (_nodes?.Return(run) == true) NodesWoken("returned");
                    },
                    // FABRIC-CUTOVER1d: the look's automatic landing held by a run's graph asks for a whole pass, waiting for none.
                    PassAsked = () => _ = PassWholeAsync(TimeSpan.Zero, CancellationToken.None),
                    // ACCESSSWAP1b: a session outlives this look's reading of the file, so its run reads the file again at its turn's end.
                    ConfigPath = configPath,
                };
            driver.Context.Supervisor = Supervisor;
            driver.Context.Roles = Roles?.Invoke();
            try
            {
                var report = await driver.TickAsync(token, failed: said =>
                {
                    carried.AddRange(said.Events);
                    carriedEnded.AddRange(said.Concluded);
                }).ConfigureAwait(false);
                return draining ? report with { Considerations = InstallUpdate.HeldFor(report.Considerations, config) } : report;
            }
            finally
            {
                NodesDue(supervisorDue, Supervisor.NextGoesOn());
                if (!driver.WokeSpent) Unspent(taken, driver.LandingBusy);
            }
        }

        // PARALLEL1b (D181 point 3): what a look waits on for a start that has not yet opened its record, and leaves it beside on: a
        // nudge since the loop counted its nudges, an ending waiting to be said, or the `look` topic's deadline (a whole look's own
        // is a pace from now). Never throws: the look reads its own token to learn whether it was the close.
        async Task WantedAsync(LookPart part, CancellationToken token)
        {
            CancellationToken paused;
            try
            {
                paused = Volatile.Read(ref _pause).Token;
            }
            catch (ObjectDisposedException)
            {
                return; // A nudge swapped the source as this began: that is a nudge.
            }

            if (Volatile.Read(ref _nudges) != counted) return;
            var pace = TimeSpan.FromSeconds(config.PollSeconds);
            var left = part == LookPart.Look ? pace : pace - Stopwatch.GetElapsedTime(whole);
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token, paused);
            await Task.WhenAny(Running.EndedAsync(waiting.Token), Task.Delay(left > TimeSpan.Zero ? left : TimeSpan.Zero, waiting.Token))
                .ConfigureAwait(false);
            await waiting.CancelAsync().ConfigureAwait(false);
        }

        // What the sessions run on: the loop's own token, and cancelled by the loop itself when a failure
        // ends it, so no session outlives the loop that watches it (DEV3).
        using var sessions = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var nudged = Volatile.Read(ref _nudges);
                var nudges = nudged - counted;
                counted = nudged;
                // Taken after the count, so a plain nudge the count missed still makes this look whole, and is looked at again.
                var everything = Interlocked.Exchange(ref _everything, 0) == 1;
                var part = by is "start" or "pace" || everything || Due(whole, config) ? LookPart.Look : LookPart.Plan;
                var failed = false;
                var began = Stopwatch.GetTimestamp();
                try
                {
                    // 🔴 Inside the catch (REV3): a hand edit that tore the file threw from here, outside it,
                    // and the watch died on its first tick — silently, in the shell. Now it is said, nothing
                    // ticks on choices it cannot read, and the next look after the fix ticks again.
                    config = Load(configPath);

                    if (!swept)
                    {
                        var orphans = await Stages.TimeAsync("sweep", () => Orphans.EndAsync(service, processes, ct: ct)).ConfigureAwait(false);
                        // An end nobody pressed and nobody must answer: news (NOTIFY2h).
                        foreach (var ended in orphans)
                        {
                            carried.Add(Lines.News(
                                "sweep", new LookNews(LookSaid.Swept, ended.Id, ended.Id), $"stopped  session {ended.Id} ({ended.Repository}): {Orphans.Note}"));
                        }
                        swept = true;
                        // SUPERVISOR1a-i (the supervisor design §3.4): the decisions an earlier run left closed before this first look's
                        // carry-ons, held and released ones stale and admitted ones interrupted; their waiting words the look takes back.
                        Supervisor.SweepAtStart(DateTimeOffset.UtcNow);
                        // A record the sweep ended concludes too (LAND2b, design §2): one whose quest its session closed done
                        // before the crash is due, and this first look lands it.
                        await OrphansDueAsync(orphans, config, ct).ConfigureAwait(false);
                        // COWORK1c (the message-urgency design §6, trap 8): the words a shell before this one wrote to its agent and never
                        // took off leave their records, said where their conversation never showed them taken, and are never sent again.
                        carried.AddRange(await Stages.TimeAsync("uncertain", () => UncertainWords.SettleAsync(service, events, ct, Lines)).ConfigureAwait(false));
                    }

                    if (!followed) following ??= FollowEveryLineAsync(config, ct);

                    looked = null;
                    await chain.FireAsync([Cause(part, woke)], sessions.Token).ConfigureAwait(false);
                    var report = looked ?? throw new InvalidOperationException("link 5 of the loop's chain never ran the look.");
                    if (carried.Count > 0 || carriedEnded.Count > 0)
                    {
                        report = report with { Events = [.. carried, .. report.Events], Concluded = [.. carriedEnded, .. report.Concluded] };
                        carried.Clear();
                        carriedEnded.Clear();
                    }

                    if (!followed && following is { IsCompleted: true } done)
                    {
                        var lines = done.IsCompletedSuccessfully ? done.Result : null;
                        following = null;
                        if (lines is not null)
                        {
                            followed = true;
                            // Classed where the pass made them (NOTIFY2h2).
                            if (lines.Count > 0) report = report with { Events = [.. report.Events, .. lines] };
                        }
                    }

                    if (tidying is { IsCompleted: true } tidied)
                    {
                        tidying = null;
                        // A pass that failed past its own catches is said, and so observed, rather than left to the finalizer.
                        // Classed where the tidy made them (NOTIFY2h2).
                        IReadOnlyList<string> lines = tidied.IsCompletedSuccessfully ? tidied.Result
                            : tidied.Exception?.InnerException is { } failure
                                ? [Lines.Retry("tidy", $"tidy  the session branches were not looked at this time, and are looked at again later: {failure.Message}")]
                                : [];
                        if (lines.Count > 0) report = report with { Events = [.. report.Events, .. lines] };
                    }

                    // What link 2 removed or kept since the last look (FABRIC-END4a).
                    lock (effected)
                    {
                        // Classed where the tidy made them (NOTIFY2h2).
                        if (effected.Count > 0) report = report with { Events = [.. report.Events, .. effected] };
                        effected.Clear();
                    }

                    if (tidying is null && tidy.Due) tidying = TidyAsync(tidy, ct);

                    if (limiting is { IsCompleted: true }) limiting = null;
                    if (limiting is null && Limits is { } limits) limiting = LimitsAsync(limits, config, ct);

                    Looked(by, woke, part, nudges, began, failed: false);
                    await onReport(report, config).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    SayCarried();
                    break;
                }
                catch (Exception error) when (onError is not null)
                {
                    failed = true;
                    Looked(by, woke, part, nudges, began, failed: true);
                    SayCarried();
                    await onError(error).ConfigureAwait(false);
                }

                // A whole look that failed was still the deadline's: the next comes a pace later, as it always did, not at once.
                if (part == LookPart.Look) whole = Stopwatch.GetTimestamp();

                // Interruptible four ways: cancellation ends the loop; the pace, a nudge and a session ending
                // each end the wait — an ending at once, so a freed slot is used and the ending is said. Not
                // after a failed look: the endings it never reported are still waiting, and waking on them
                // would look again at once, every time, against a service that is not answering.
                CancellationTokenSource wait;
                try
                {
                    wait = CancellationTokenSource.CreateLinkedTokenSource(ct, _pause.Token);
                }
                catch (ObjectDisposedException)
                {
                    // A nudge swapped the source as the wait began: that is a nudge, so look again.
                    (by, woke) = ("nudge", Volatile.Read(ref _woke));
                    continue;
                }

                using (wait)
                {
                    // A nudge during the look came before this wait could hear it: look again now.
                    if (Volatile.Read(ref _nudges) != nudged)
                    {
                        (by, woke) = ("nudge", Volatile.Read(ref _woke));
                        continue;
                    }

                    // Up to the `look` topic's deadline (FABRIC-EVENT3a), or the whole pace after a failed look, as it always was.
                    var pace = TimeSpan.FromSeconds(config.PollSeconds);
                    var left = failed ? pace : pace - Stopwatch.GetElapsedTime(whole);
                    await Running.NextAsync(left > TimeSpan.Zero ? left : TimeSpan.Zero, wait.Token, endings: !failed).ConfigureAwait(false);
                    woke = Volatile.Read(ref _nudges) != nudged ? Volatile.Read(ref _woke) : null;
                    by = Volatile.Read(ref _nudges) != nudged ? "nudge"
                        : failed || Due(whole, config) ? "pace"
                        : "ended";
                    // A session ending may free the tree a refused tidy waits on (FABRIC-END4a, canary 17).
                    if (by == "ended") refs.Unblocked(ChainEvents.Ledger);
                }
            }
        }
        catch
        {
            // A failure with nobody to hear it ends the loop: what its look had said is said first (DEV3c).
            SayCarried();
            await sessions.CancelAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            // FABRIC-RUN1d: no wake of the runtime's fires link 8 once the loop closes; FABRIC-CUTOVER1d: nor does a door's ask, and one asked is
            // answered as not passed, so no press waits on a loop that closed.
            Volatile.Write(ref _nodesWake, null);
            if (Interlocked.Exchange(ref _passAsks, null) is { } unasked) PassAsks.Done(unasked.Take(), passed: false);
            if (hooks is not null) hooks.StepArrived = null;

            // The change stream's wait ends with the loop, and is waited for, so no ask outlives it.
            await listening.CancelAsync().ConfigureAwait(false);
            await ledger.ConfigureAwait(false);
            await Running.SettledAsync().ConfigureAwait(false);
            // The start's pass ends on the loop's own token; waited for, so nothing it writes outlives the loop.
            if (following is not null) await following.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            // The tidy's too: a removal half done when the loop closed is git's to finish, never left running unwatched.
            if (tidying is not null) await tidying.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            if (limiting is not null) await limiting.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            await recovering.ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
        }
    }

    /// <summary>How much sooner than its pace a wait may end and still be the pace's: a timer's own granularity.</summary>
    private static readonly TimeSpan Early = TimeSpan.FromMilliseconds(50);

    /// <summary>What link 5 hears as the <c>look</c> topic's deadline: <c>timer(look)</c>, the whole look.</summary>
    private static readonly string Whole = ChainEvents.TimerOf(ChangeTopics.Look);

    /// <summary>Whether the <c>look</c> topic's deadline has come: <c>pollSeconds</c> since the last whole look ended, a timer's granularity allowed.</summary>
    private static bool Due(long whole, DriverConfig config) =>
        Stopwatch.GetElapsedTime(whole) >= TimeSpan.FromSeconds(config.PollSeconds) - Early;

    /// <summary>
    /// A look's cause on the loop's chain (FABRIC-EVENT3a): the <c>look</c> topic's <c>timer</c> for a whole look; otherwise a
    /// <c>ledger</c> event naming what moved, its topic kept, since every wake the loop hears is a local fact the plan reads: a
    /// ledger family, a terminal's words kept on its record, a session's end.
    /// </summary>
    private static ChainEvent Cause(LookPart part, Wake? woke) =>
        part == LookPart.Look
            ? ChainEvent.Timer(ChangeTopics.Look)
            : new ChainEvent(ChainEvents.Ledger, woke?.Subject ?? "sessions", woke?.Topic ?? ChangeTopics.Ledger);

    /// <summary>What link 8 counts as acting (FABRIC-RUN1): an attempt sent or delivered again, an outcome applied, a retry made ready, a start.</summary>
    private static readonly HashSet<string> Acted =
        new([PerformedWhat.Sent, PerformedWhat.Redelivered, PerformedWhat.Applied, PerformedWhat.Retry, PerformedWhat.Begun], StringComparer.Ordinal);

    /// <summary>
    /// The units a pass of the evaluation runtime reads (link 8's <see cref="NodeRuntimeLook.Units"/>), each repository's checkout the awaited
    /// set names, and the tip of each branch a bound quest's chain worked (FABRIC-CLOSE1p).
    /// </summary>
    /// <remarks>
    /// No roots go to the inventory: its recovery refs are its own entries, not a node's, and gathering them (a listing and a <c>rev-list</c> of
    /// every repository the home names) on every read would be git starts nothing here reads. The tips are one <see cref="RefSnapshot"/> per
    /// repository a bound quest's chain worked in, taken for this read alone (<see cref="QuestClosing.TipsAsync"/>): a home with no quest
    /// bound to a closure graph, the owner's install, starts no git here.
    /// </remarks>
    internal static async Task<NodeUnitsRead> ReadNodeUnitsAsync(string home, ServiceClient service, CancellationToken token)
    {
        var unread = new List<string>();
        var unlinked = new List<string>();
        var units = await WorkUnits.ReadAsync(home, new Dictionary<string, string>(), new HashSet<string>(), service, unread, token, unlinked).ConfigureAwait(false);
        var registry = await service.RegistryAsync(token).ConfigureAwait(false);
        // GITAUTH1c: the doors find a checkout's repository from the files alone, so the registry's roots are kept for them.
        GitFolders.Remember(home, registry.Where(repo => repo.Root is { Length: > 0 }).Select(repo => (repo.Root!, repo.Repository, repo.Workspace)));
        var roots = registry.Where(repo => repo.Root is { Length: > 0 }).GroupBy(repo => repo.Repository, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Root!, StringComparer.Ordinal);
        // FABRIC-CUTOVER1c (the cutover design §2.3): why the work's attribution was not read travels with the units, so a run's branch is never
        // handed to Current on a link the outage dropped.
        return new NodeUnitsRead(units, roots) { Tips = await QuestClosing.TipsAsync(units, roots, token).ConfigureAwait(false), Unread = unlinked };
    }

    /// <summary>
    /// One pass's reads of D157's gates for the evaluation runtime (FABRIC-RUN1c, link 8's <see cref="NodeRuntimeLook.Gates"/>): a unit's work
    /// read as a landing door reads it (<see cref="SessionTrees.GateAsync"/>), at its newest session's tree here: a branch unit's newest executor,
    /// a quest unit's newest session serving it. The sessions are read once a pass, at the first unit asked. A unit with no such tree here (a
    /// teammate's work, a tree tidied), or a read that failed, reads nothing, and its nodes wait. FABRIC-CUTOVER1d (the cutover design §2.2): at the
    /// commit given, a run's branch at the tip its pass read, as a landing door judges at a commit it pins; else at that tree's <c>HEAD</c>.
    /// </summary>
    internal sealed class NodeGates(string home, ServiceClient service)
    {
        private Task<IReadOnlyList<SessionRecord>>? _sessions;

        public async Task<(OpinionGateState? Opinion, ReviewGateState? Review)> ReadAsync(WorkUnit unit, string? tip, CancellationToken ct)
        {
            try
            {
                var sessions = await (_sessions ??= service.SessionRecordsAsync(ct)).ConfigureAwait(false);
                var serving = unit.Kind == WorkUnitKind.Quest
                    ? sessions.Where(session => session.Quest is { Length: > 0 } quest && string.Equals(QuestUnits.IdOf(quest.TrimStart('#')), unit.Id, StringComparison.Ordinal))
                    : sessions.Where(session => unit.Executors.Contains(session.Id, StringComparer.Ordinal));
                if (serving.Where(session => session.Tree is { Length: > 0 } tree && Directory.Exists(tree)).MaxBy(session => session.Updated) is not { } newest)
                {
                    return (null, null);
                }

                var gate = await new SessionTrees(home).GateAsync(newest.Tree!, newest.Quest, new ServiceReviewWorld(service), newest.Id, ct, tip: tip).ConfigureAwait(false);
                return (gate.Opinion, gate.Review);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException
                                              or UnauthorizedAccessException)
            {
                return (null, null);
            }
        }
    }

    /// <summary>What a person reads of a pass (FABRIC-RUN1): each attempt sent, answered, retried, unknown or held, and what the pass said.</summary>
    internal static IReadOnlyList<string> NodeRuntimeLines(NodeRuntimePass pass) =>
    [
        .. pass.Performed
            .Where(done => done.What is PerformedWhat.Sent or PerformedWhat.Redelivered or PerformedWhat.Applied or PerformedWhat.Retry
                or PerformedWhat.Unknown or PerformedWhat.Held or PerformedWhat.Stale)
            .Select(done => $"work  {done.Unit}'s {done.Node}: {done.What}{(done.Code is { } code ? $" ({code})" : "")}"),
        .. pass.Said,
    ];

    /// <summary>
    /// The next timer the node records persist, set to wake a plan at its moment (FABRIC-RUN1). A moment already past is left to the look's
    /// deadline: the pass that just ran judged everything due then, so waking again at once would only spin on what it could not act on.
    /// </summary>
    private static void NodesDue(System.Threading.Timer timer, DateTimeOffset? at)
    {
        var left = at is { } moment ? moment - DateTimeOffset.UtcNow : TimeSpan.Zero;
        timer.Change(left <= TimeSpan.Zero ? Timeout.InfiniteTimeSpan : left > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : left, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// The look, timed (FABRIC-EVENT2, the design §A2): <c>watch.looked</c> with its ms, what woke it (<c>start</c>, <c>pace</c>,
    /// <c>ended</c> for a session's end, or <c>nudge</c> with the topic and subject a nudge carried, none for a plain one), the part
    /// it ran (FABRIC-EVENT3a: <c>look</c> for the whole look, <c>plan</c> for a wake's local plan), how many nudges it took up, and
    /// whether it failed. Up to the report, not the door's handling of it. LOOKWAIT1: its <c>stages</c>, each part of the look and of
    /// the work beside it that ended since the last line (<see cref="LookStages.Take"/>), so a slow look says which part was slow.
    /// </summary>
    private void Looked(string by, Wake? woke, LookPart part, int nudges, long began, bool failed) =>
        Log?.Info("watch.looked",
            ("ms", (long)Stopwatch.GetElapsedTime(began).TotalMilliseconds),
            ("by", by),
            ("topic", woke?.Topic),
            ("subject", woke?.Subject),
            ("part", part == LookPart.Look ? "look" : "plan"),
            ("nudges", nudges),
            ("failed", failed),
            ("stages", Stages.Take()));

    /// <summary>
    /// One pass of the <c>limits</c> stage (LIMITRESUME1c, D206 §3.4), started on the pool so the look beside it does not wait on a
    /// conversation's start. A closing loop ends it with nothing said; a failure past its own catches is written to the log.
    /// </summary>
    private Task LimitsAsync(Func<DriverConfig, CancellationToken, Task> limits, DriverConfig config, CancellationToken ct) =>
        Task.Run(async () =>
        {
            try
            {
                await Stages.TimeAsync("limits", () => limits(config, ct), beside: true).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The loop closed.
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or IOException or UnauthorizedAccessException)
            {
                // The next look's pass asks again.
            }
        }, CancellationToken.None);

    /// <summary>
    /// One tidy pass (AUTOTIDY1), started on the pool so the look beside it does not wait on its git. A closing loop ends it
    /// with nothing said; what else stops it is said once, in a later look's report.
    /// </summary>
    private Task<IReadOnlyList<string>> TidyAsync(BranchTidying tidy, CancellationToken ct)
    {
        tidy.Began();
        return Task.Run<IReadOnlyList<string>>(async () =>
        {
            try
            {
                await FirstScreenAsync(ct).ConfigureAwait(false);
                // A review step in progress keeps the tree it shows from (REVIEWENV1d), as a session in use keeps its own.
                return await Stages.TimeAsync(
                        "tidy", () => tidy.LookAsync(service, Log, ct, token => BranchTidying.ReviewingAsync(Reviews, service, token)), beside: true)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return [];
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              or IOException or UnauthorizedAccessException)
            {
                return [Lines.Retry("tidy", $"tidy  the session branches were not looked at this time, and are looked at again later: {error.Message}")];
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Every repository with a checkout here, followed from its line (WSSETUP5): what the pass says, for a look's report,
    /// or null where the service did not answer, so the next look tries again. Started on the pool, so the look beside it
    /// does not wait on its git.
    /// </summary>
    private Task<IReadOnlyList<string>?> FollowEveryLineAsync(DriverConfig config, CancellationToken ct) =>
        Task.Run<IReadOnlyList<string>?>(async () =>
        {
            try
            {
                await FirstScreenAsync(ct).ConfigureAwait(false);
                var report = await Stages.TimeAsync(
                        "follow", () => RegistrationFollow.FollowAsync(new RegistrationWorld(service, home, config), only: null, ct), beside: true)
                    .ConfigureAwait(false);
                // Each line classed as it is made (NOTIFY2h2).
                return
                [
                    .. report.Followed.Select(followed => RegistrationFollow.EventLine(followed, Lines)).OfType<string>(),
                    .. report.Refresh is { } refresh
                        ? [Lines.Retry("registry", $"registry  the index was not read again after registering: {refresh}")]
                        : Array.Empty<string>(),
                ];
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // A closing loop, its first-screen wait among what it ends (STARTFAST1b): nothing followed, nothing said.
                return null;
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                return null;
            }
        }, CancellationToken.None);

    /// <summary>
    /// Each record the sweep ended that served a quest, read against its repository's rule (LAND2b): its done under the switch
    /// is due. Only a done is acted on: the sweep's stop is an interruption, carried on at the next start (D104), so an end that
    /// is not a done says nothing here.
    /// </summary>
    private async Task OrphansDueAsync(IReadOnlyList<OrphanEnded> orphans, DriverConfig config, CancellationToken ct)
    {
        foreach (var ended in orphans.Where(ended => ended.Quest is not null && ended.Tree is { Length: > 0 }))
        {
            QuestView? quest;
            try
            {
                quest = await service.FindQuestAsync(ended.Quest!, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                // Not due, then: its work waits for the person's Accept, as it would have before the switch.
                continue;
            }

            // FABRIC-CLOSE1h (§E5): a closure graph's submission reads as its done.
            if (!QuestResults.Judged(quest)) continue;
            // WORKFLOW1f: its run's named workflow, where it has one, says whether it lands with no press.
            var process = await AutoLander.ProcessAsync(home, config, new ServiceReviewWorld(service), quest, ended.Tree!, quest.Workspace, ct)
                .ConfigureAwait(false);
            AutoLander.Concluded(home, config, events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)), ended.Id, quest,
                QuestResults.Status(quest), "stopped", ended.Tree!, quest.Workspace, process: process);
        }
    }

    /// <summary>The standing choices, or the reader's sentence about why they could not be read and what the loop does about it.</summary>
    private static DriverConfig Load(string path)
    {
        try
        {
            return DriverConfig.Load(path);
        }
        catch (DriverConfigUnreadableException error)
        {
            // The file and where in it are the reader's to say (CONFIGREAD1); what the loop does about it is the watch's.
            throw new DriverException(
                $"{error.Message} Nothing is driven until it reads; the loop keeps watching it, and takes the fix at its next look.");
        }
    }
}
