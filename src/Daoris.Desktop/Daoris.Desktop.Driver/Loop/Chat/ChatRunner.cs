using System.Diagnostics;
using System.Text.Json;
using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// A conversation with an agent in one repository (D49 §3) — the session D46 built, entered by a
/// person instead of planned from a quest.
/// </summary>
/// <remarks>
/// <para><b>Nothing here is a chat loop.</b> Daoris pipes text and observes; the harness carries the
/// model and the conversation (D24, `model-decoupling`). This class spawns a process, relays the
/// person's lines into it, keeps the transcript and the console exactly as the driven path does, and
/// concludes the record from what it can see. A Daoris-owned loop calling a model API was rejected in
/// the design: it would duplicate what every harness already is, and produce sessions with no
/// doctrine path into them.</para>
///
/// <para><b>A chat may open on a dirty tree</b>, where a driven session may not (D46 §3). The
/// clean-tree rule exists because an unattended agent would entangle itself with somebody's work in
/// flight; in a conversation the person IS present, and it is their work in flight.</para>
///
/// <para><b>The lock is the ledger's</b>, not this class's: one active session per repository,
/// judged in one place for every door. This asks, and reports the refusal verbatim.</para>
///
/// <para><b>An ended chat goes on</b> (MSG1c, D137 §2.2): the person's words kept on its record are taken up here at
/// once, its record reopening and its own conversation resumed with them (<see cref="ChatGoOn"/>).</para>
/// </remarks>
public sealed class ChatRunner : IDisposable, IChatPressing, IChatSendBacks, IExecutorWiring
{
    private readonly ChatOptions _options;

    private readonly ChatAccountReads _accounts;

    private readonly ChatClosing _closing;

    /// <summary>The shell crossed its close boundary: no further turn starts.</summary>
    public bool Closing => _context.Closing;
    /// <summary>Turns and starts the update drain waits on, including conversations going on before their process exists.</summary>
    public int InFlight => _context.Opening.Count + _context.GoingOn.Keys.Union(
        _context.Turned.Where(pair => pair.Value.Turns.State is { Taking: true } or { Opening: true }).Select(pair => pair.Key)).Count();
    private readonly ServiceClient _service;

    private readonly string _home;

    private readonly SessionProcesses _processes;

    private readonly SessionOutput? _output;

    private readonly IInAppBrowser? _browser;

    private readonly SessionUsage _usage;

    private readonly PluginLog _plugins;

    // Shared with the driver where a shell has both, so a probe is paid for once; its own where it
    // does not, which is the headless chat door's case.
    private readonly HarnessRoster _harnesses;

    private readonly SessionEvents _events;

    // What the runner's classes share: the stores and the live maps.
    private readonly ChatContext _context;

    // What the conversations' turns are judged by: the person's words, their stops and each turn's end.
    private readonly AttentionWatch _answers = new();

    private readonly ChatTold _told;

    private readonly ChatStarts _starts;

    private readonly ChatWords _words;

    private readonly ChatTurnDoors _turns;

    private readonly ChatSendBack _sendBack;

    private readonly ChatWalk _walk;

    private readonly ChatServers _servers;

    private readonly ChatConclusion _conclusion;

    private readonly ChatWatch _watch;

    private readonly ProtocolHeld _held;

    /// <param name="events">
    /// Where a conversation's structure is kept (D76): the shell's shared record, so its page hears each message live; the
    /// home's own where nobody passes one, which is the headless chat door's case.
    /// </param>
    /// <param name="usage">
    /// What each account has carried (TOOL3): the loop's one record where a shell has one, so a chat's end and a driven
    /// session's never write it at once; the home's own for the headless chat door (USAGE1).
    /// </param>
    /// <param name="browser">
    /// Daoris's own browser (D78), asked for by a plugin server that drives it. Null where no shell carries one, which is
    /// the headless chat door's case: such a server is then not handed.
    /// </param>
    /// <param name="plugins">
    /// Where a conversation's plugin servers, handed or withheld, are written without their words (PLUGUI1e, D119 §4.2): the
    /// shell's loop hands its log. Null writes nothing, the headless chat door's case, which writes no session lines either.
    /// </param>
    public ChatRunner(
        ServiceClient service,
        AdapterSet adapters,
        string home,
        SessionProcesses processes,
        SessionOutput? output = null,
        HarnessRoster? harnesses = null,
        SessionEvents? events = null,
        SessionUsage? usage = null,
        IInAppBrowser? browser = null,
        PluginLog? plugins = null)
    {
        _service = service;
        _home = home;
        _processes = processes;
        _output = output;
        _browser = browser;
        _usage = usage ?? new SessionUsage(home);
        _plugins = plugins ?? PluginLog.None;
        // The roster of the home it was handed, as every other default here is, or the file the override names.
        _harnesses = harnesses ?? new HarnessRoster(adapters, HarnessSettings.ResolvePath(home));
        // Its turns kept in the usage history (USAGECAP1a2), watched once however many runners share the record.
        _events = UsageTurns.Watched(home, events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)));
        _context = new ChatContext(service, home, processes, output, _harnesses, _events);
        _options = new ChatOptions(_context);
        _accounts = new ChatAccountReads(_context);

        Executors = Parts.Build(ExecutorOrder.Value, type => (IExecutor)Activator.CreateInstance(type, this)!);
        _told = new ChatTold(_context, _context.ChatRuns);
        _words = new ChatWords(_context, _told, _answers);
        _turns = new ChatTurnDoors(_context, _answers);
        _starts = new ChatStarts(this, _told);
        _sendBack = new ChatSendBack(this, _context.ChatRuns);
        _walk = new ChatWalk(_context);
        _servers = new ChatServers(_context);
        _conclusion = new ChatConclusion(this);
        _watch = new ChatWatch(this, _told, _conclusion, _answers);
        _closing = new ChatClosing(this, _conclusion);
        _held = new ProtocolHeld(this, (id, options) => OptionsChanged?.Invoke(id, options));

        // One holder for the moves of the home, the hand-over's among them (FOUNDATION1f-v part a): it imports the boxes an older build wrote and
        // the come-up takes a dead shell's moves up through it, and the hand-over takes the move it leaves standing.
        Moves = Executors.Of<LimitMove>().Moves;

        // Once every executor is there to reach: the person's words to an ended chat are taken up as the record hears them (MSG1d, MSG1c).
        GoOn.Hear();
    }

    /// <summary>
    /// The kinds of run a conversation takes, registered by their modules (<see cref="ExecutorAttribute"/>), ordered once per process and
    /// built once per runner with the runner as their wiring. A door for a kind reaches its executor by type.
    /// </summary>
    internal PartSet<IExecutor> Executors { get; }

    private static readonly Lazy<IReadOnlyList<PartDeclaration>> ExecutorOrder =
        new(() => Parts.Declared<ExecutorAttribute>(typeof(ChatRunner).Assembly));

    /// <summary>
    /// The hand-overs this run holds that wait to be taken (FOUNDATION1f-v part a), at a look: a dead shell's or an older build's the come-up or
    /// an earlier look left standing, each judged again as it is taken; one whose conversation is talking waits for its turn's end.
    /// </summary>
    /// <param name="onEnded">Told when a new conversation ends, as a start's is.</param>
    public Task<IReadOnlyList<ChatStart>> TakeHandOversAsync(Func<string, string, Task> onEnded, CancellationToken ct = default) =>
        Executors.Of<ChatHandOver>().TakeAllAsync(onEnded, ct);

    /// <summary>
    /// The look's <c>limits</c> stage (LIMITRESUME1c, D206 §3.4; <see cref="LimitWaits.LookAsync"/>): each conversation that waits for an
    /// account whose wait has come goes on, its own account first, or moves; nothing is asked where no wait has come.
    /// </summary>
    /// <param name="onEnded">Told when a conversation it woke ends, as a start's is.</param>
    public Task<IReadOnlyList<ChatStart>> WakeLimitsAsync(
        DriverConfig config, Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        Waits.LookAsync(config, onEnded, ct);

    /// <summary>How many times the limits stage asked the service: a test holds that a look with no wait come asks nothing.</summary>
    internal int LimitsAsked => Waits.Asked;

    /// <summary>
    /// The conversations' move record this runner's live limit move and curve move write and its come-up takes up (FOUNDATION1f-ii part b,
    /// f-iii part a): a proof of an older build's come-up hands it a holder whose take-up reads every run as alive, and so takes nothing, as a
    /// build before the store does.
    /// </summary>
    internal ConversationMoves Moves
    {
        get => Executors.Of<LimitMove>().Moves;
        set => Executors.Of<LimitMove>().Moves = Curve.Moves = Executors.Of<ChatHandOver>().Moves = value;
    }

    /// <summary>
    /// The clock a conversation's waits between tries of a transient failure are read off (AGENTERRORS1b, D210,
    /// <c>.claude/knowledge/held-clocks.md</c>): the machine's, and a held one in a test that passes each wait.
    /// </summary>
    public TimeProvider RetryClock { get; init; } = TimeProvider.System;

    /// <summary>
    /// *Stop waiting* (LIMITRESUME1c, D206 §3.7, N27): an ended conversation held by its limit waits no more. Its waiting words are marked
    /// as words that cannot go on by themselves are, Daoris's limit word taken off by its id, and its conversation says so, which leaves
    /// the limits stage's wait, so nothing goes on by itself; the person's next word goes on as any word to an ended conversation does.
    /// A live one ends by the person's *Stop*, as any does.
    /// </summary>
    /// <returns>Null where it stopped waiting; else why not.</returns>
    public async Task<string?> StopWaitingAsync(string sessionId, CancellationToken ct = default)
    {
        if (_context.Holds(sessionId)) return $"conversation `{sessionId}` runs here: stop it to end its wait.";
        // FOUNDATION1g part c: held by the record's flag, or by its events where an older build ended it.
        var ended = await _context.ReadAgainAsync(sessionId, ct).ConfigureAwait(false);
        if (LimitHeld.Waiting(_context.Events.After(sessionId, 0).Events, ended?.Limit == true) is null)
        {
            return $"conversation `{sessionId}` does not wait for its account's limit.";
        }

        var record = await GoOn.LimitWordOffAsync(sessionId).ConfigureAwait(false);
        if (record is not { Kind: "chat", Teammate: false }) return $"session `{sessionId}` is no conversation of this machine's.";
        _context.Marks.Mark(sessionId, record.Waiting.Select(word => word.Id).OfType<string>(), GoOnMarks.StoppedWaiting, DateTimeOffset.UtcNow);
        _context.Note(sessionId, LimitNotes.StoppedWaiting());
        return null;
    }

    /// <summary>
    /// *Go on with Daoris's brief* (SESSCTX1c part B, D189 point 3; <see cref="BriefedMove.MoveAsync"/>): a conversation, or a closed
    /// quest's session, whose words wait for the person goes on in a new part of its session, in its tree on its agent and account,
    /// handed Daoris's brief and then their words. Beside *Start a conversation with these words*, which stays the person's fresh start.
    /// </summary>
    /// <param name="onEnded">Told when the new part ends, as a start's is.</param>
    public Task<StartedFrom> GoOnBriefedAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        Executors.Of<BriefedMove>().MoveAsync(sessionId, lost: null, HandOverBy.Person, config, onEnded, ct);

    /// <summary>
    /// A closed quest's session whose own conversation is proven lost goes on by itself as a conversation in its tree (SESSCTX1c part B,
    /// the steward's choice: a closed quest opens no driven record), handed Daoris's brief and the person's words; the driver hands it
    /// here through its seam, since conversations are this runner's.
    /// </summary>
    /// <remarks>
    /// LIMITSWITCH2 (D137's note of 2026-10-25): where the reason is <see cref="ContinueWhy.Account"/>, its own conversation is not lost but on
    /// an account that cools while another of its agent's is ready, so the ended limit move's body opens it there; the brief's move opens
    /// on the account the record ran on, which is cooling.
    /// </remarks>
    public async Task<StartedFrom> GoOnLostAsync(
        string sessionId, ContinueReason lost, DriverConfig config, Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (lost.Code != ContinueWhy.Account) return await Executors.Of<BriefedMove>().MoveAsync(sessionId, lost, HandOverBy.Daoris, config, onEnded, ct).ConfigureAwait(false);

        if (_context.Closing) return new StartedFrom(null, false, null, "the application is closing, so no conversation goes on now.");
        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_context.GoingOn.TryAdd(sessionId, going.Task))
        {
            return new StartedFrom(null, false, StartFrom.Running, $"`{sessionId}` is going on already.");
        }

        try
        {
            return await Executors.Of<LimitMove>().MoveEndedAsync(sessionId, lost: null, config, onEnded, ct, holding: true, switching: true).ConfigureAwait(false)
                   ?? new StartedFrom(null, false, null, $"`{sessionId}` did not go on in a new part now.");
        }
        finally
        {
            _context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary>
    /// The ended chats whose words wait from while no runner heard them are taken up, as the shell comes up (MSG1c2,
    /// <see cref="ChatGoOn.TakeUpAsync"/>).
    /// </summary>
    public Task<IReadOnlyList<ChatStart>> TakeUpAsync(CancellationToken ct = default) => GoOn.TakeUpAsync(ct);

    /// <summary>Go on with an ended chat the person wrote to (MSG1c, <see cref="ChatGoOn.GoOnAsync"/>).</summary>
    public Task<ChatStart> GoOnAsync(
        string sessionId, DriverConfig? config = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        GoOn.GoOnAsync(sessionId, config, onEnded, ct);

    /// <summary>
    /// The person let an answer the answers-alone bound held go on (COWORK1e, <see cref="ChatGoOn.AnswerGoesOn"/>): said as an open
    /// conversation's next turn, or gone on with where it ended.
    /// </summary>
    public void AnswerGoesOn(string sessionId) => GoOn.AnswerGoesOn(sessionId);

    /// <summary>
    /// A conversation this runner took up by itself ended (MSG1c2): the session and the state its record took, as a start's
    /// <c>onEnded</c> is told. No route started it, so the shell announces it as the page's <c>SESSION_ENDED</c>.
    /// </summary>
    public event Action<string, string>? TakenUpEnded
    {
        add => GoOn.TakenUpEnded += value;
        remove => GoOn.TakenUpEnded -= value;
    }

    /// <summary>
    /// A hand-over's or a curve move's take is over, its box gone (HANDWORDS1, <see cref="ChatHandOver.TakeEnded"/>): the shell's
    /// judge hands the words it held meanwhile to the conversation that continues it.
    /// </summary>
    public event Action<string>? TakeEnded
    {
        add => Executors.Of<ChatHandOver>().TakeEnded += value;
        remove => Executors.Of<ChatHandOver>().TakeEnded -= value;
    }

    /// <summary>
    /// The machine as Ask Daoris's room says it (ASKHIST1, <see cref="ChatHelp.DescribeHelp"/>): the shell's reading, which a
    /// conversation going on finds the room written from. Null where nothing reads it.
    /// </summary>
    public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp
    {
        get => Executors.Of<ChatHelp>().DescribeHelp;
        set => Executors.Of<ChatHelp>().DescribeHelp = value;
    }

    /// <summary>
    /// How long an Ask Daoris start waits for the machine's reading before the room as last written serves
    /// (<see cref="HelpStart.ReadBound"/>).
    /// </summary>
    internal TimeSpan HelpReadBound
    {
        get => Executors.Of<HelpStart>().ReadBound;
        set => Executors.Of<HelpStart>().ReadBound = value;
    }

    /// <summary>Place a new Ask Daoris conversation from an earlier one's words, its room read and agent spawned after (HELPSTART1b, <see cref="ChatHelp.PlaceHelpFromAsync"/>).</summary>
    /// <param name="workspace">The workspace the person's window was on at the press (ASKSCOPE1a), as <see cref="PlaceHelpAsync"/> takes it.</param>
    public Task<HelpPlaced> PlaceHelpFromAsync(
        string earlier, string adapter, DriverConfig config, string? workspace = null, CancellationToken ct = default,
        IReadOnlyList<ActorEffective>? order = null) =>
        Executors.Of<ChatHelp>().PlaceHelpFromAsync(earlier, adapter, config, workspace, ct, order);

    /// <summary>Start a new Ask Daoris conversation from an earlier one's words (ASKHIST1, <see cref="ChatHelp.StartHelpFromAsync"/>).</summary>
    public Task<ChatStart> StartHelpFromAsync(
        string earlier, string adapter, DriverConfig config, HelpMachine machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        Executors.Of<ChatHelp>().StartHelpFromAsync(earlier, adapter, config, machine, onEnded, ct);

    /// <inheritdoc cref="WordsStart.StartFromAsync"/>
    public Task<StartedFrom> StartFromAsync(
        string sessionId, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, CancellationToken ct = default,
        IReadOnlyList<ActorEffective>? order = null) =>
        Executors.Of<WordsStart>().StartFromAsync(sessionId, adapter, config, onEnded, ct, order);

    /// <inheritdoc cref="WordsStart.StartWithAsync"/>
    public Task<StartedFrom> StartWithAsync(
        string repository, string adapter, DriverConfig config, string text, IReadOnlyList<ChatUpload> files, string? requestId,
        bool? ownTree = null, string? profile = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default,
        IReadOnlyList<ActorEffective>? order = null) =>
        Executors.Of<WordsStart>().StartWithAsync(repository, adapter, config, text, files, requestId, ownTree, profile, onEnded, ct, order);


    /// <summary>
    /// *Hand a conversation over* (MIXAGENT1a-i, D163): judged and kept in its box, then taken at once, or as its turn ends where
    /// it is talking (<see cref="ChatHandOver.HandOverAsync"/>).
    /// </summary>
    /// <param name="why">One of <see cref="HandOverWhy"/>.</param>
    /// <param name="askedBy">One of <see cref="HandOverBy"/>.</param>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    public Task<HandedOver> HandOverAsync(
        string sessionId, string agent, string? account, string why, string askedBy, DriverConfig config,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default, string? model = null, string? effort = null) =>
        Executors.Of<ChatHandOver>().HandOverAsync(sessionId, agent, account, why, askedBy, config, onEnded, ct, model, effort);

    /// <summary>
    /// Judge a conversation by its account's usage curve and, where the decision says so, move it (SWITCH1c-ii,
    /// <see cref="CurveMove.MoveAsync"/>): null where nothing was judged.
    /// </summary>
    internal Task<SwitchChoice?> MoveByCurveAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default) =>
        Curve.MoveAsync(sessionId, config, onEnded, ct);

    /// <summary>The curve's move, whose <see cref="CurveMove.Decided"/> a test hears (FOUNDATION1h branch 1): the runner names no course type.</summary>
    internal CurveMove Curve => Executors.Of<CurveMove>();

    /// <summary>The briefed move, whose <see cref="BriefedMove.Decided"/> a test hears (FOUNDATION1h branch 4): the runner names no course type.</summary>
    internal BriefedMove Briefed => Executors.Of<BriefedMove>();

    /// <summary>The conversation's go-on, whose <see cref="ChatGoOn.Decided"/> a test hears (FOUNDATION1h branch 7): the runner names no course type.</summary>
    internal ChatGoOn GoOn => Executors.Of<ChatGoOn>();

    /// <summary>The helper's words, whose <see cref="ChatHelperWords.Decided"/> a test hears (FOUNDATION1h branch 7).</summary>
    internal ChatHelperWords HelperWords => Executors.Of<ChatHelperWords>();

    /// <summary>The limit's wake, whose <see cref="LimitWaits.Decided"/> a test hears (FOUNDATION1h branch 6): the runner names no course type.</summary>
    internal LimitWaits Waits => Executors.Of<LimitWaits>();

    /// <summary>What each judged turn's end came to, by session: a test hears it; no person reads it.</summary>
    internal event Action<string, SwitchChoice>? CurveJudged
    {
        add => Curve.Judged += value;
        remove => Curve.Judged -= value;
    }

    /// <summary>
    /// Where a conversation's turns stand — whether one is in flight, and what the person sent that is
    /// not at the harness yet — each time that changes (CONV4a). The page shows the waiting messages as
    /// queued, since they are in no record until they are sent, and offers a stop while a turn runs.
    /// </summary>
    public event Action<string, ChatQueue>? QueueChanged;

    /// <summary>
    /// A conversation answered the person (NOTIFY1a): the turn their words started ended, judged by <see cref="AttentionWatch"/>
    /// as its turns move. Told from the conversation's thread; the shell says it as it says the tick's attention.
    /// </summary>
    public event Action<AttentionEvent>? Answered;

    /// <summary>
    /// A conversation's model and effort, as its agent offers them, each time they change (AGT6b, D98): as
    /// its session opened, after a change, and when the agent changed them itself. Only the options that
    /// are the person's (<see cref="AcpConfigOption.ThePersons"/>).
    /// </summary>
    public event Action<string, IReadOnlyList<AcpConfigOption>>? OptionsChanged;

    /// <inheritdoc cref="HelpAccessSwap.WiredKept"/>
    public event Action<string>? WiredKept
    {
        add => Executors.Of<HelpAccessSwap>().WiredKept += value;
        remove => Executors.Of<HelpAccessSwap>().WiredKept -= value;
    }

    /// <summary>The note a conversation's record takes when the driver holding it closes.</summary>
    public const string ClosedNote =
        "the application closed while this conversation ran; its process was ended with it.";

    /// <summary><see cref="ClosedNote"/> with its code (LANG1a).</summary>
    public static Noted ClosedNoted => Noted.Of(NoteCodes.ChatClosed, ClosedNote);

    /// <inheritdoc cref="ChatStarts.StartAsync(string, string, DriverConfig, Func{string, string, Task}?, string?, bool?, CancellationToken, string?, ContinuedFrom?, IReadOnlyList{ActorEffective}?, EarlierMaker?, HarnessSelection?, bool, bool, MoveWords?)"/>
    public Task<ChatStart> StartAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded = null, string? profile = null, bool? ownTree = null,
        CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null, IReadOnlyList<ActorEffective>? order = null,
        EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false, bool ownFirst = false, MoveWords? moves = null) =>
        _starts.StartAsync(
            repository, adapter, config, onEnded, profile, ownTree, ct, tree, from, order, leftBy, settled, picked, ownFirst, moves);

    /// <inheritdoc cref="RepositoryPlacement.In"/>
    internal ChatPlace RepositoryPlace(
        DriverConfig config, IReadOnlyList<RepoView> registry, RepoView known, string repository, string workTree,
        IReadOnlyList<string>? continued = null, bool answers = false) =>
        RepositoryPlacement.In(_home, config, registry, known, repository, workTree, continued, answers);

    /// <inheritdoc cref="HelpStart"/>
    public Task<ChatStart> StartHelpAsync(
        string adapter, DriverConfig config, HelpMachine machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default, IReadOnlyList<ActorEffective>? order = null) =>
        Executors.Of<HelpStart>().StartHelpAsync(adapter, config, machine, onEnded, ct, order);

    /// <inheritdoc cref="HelpStart.PlaceHelpAsync"/>
    public Task<HelpPlaced> PlaceHelpAsync(
        string adapter, DriverConfig config, string? workspace = null, CancellationToken ct = default, string? carries = null,
        IReadOnlyList<ActorEffective>? order = null) =>
        Executors.Of<HelpStart>().PlaceHelpAsync(adapter, config, workspace, ct, carries, order);

    /// <inheritdoc cref="HelpStart.RunHelpAsync"/>
    public Task<ChatStart> RunHelpAsync(
        HelpPlaced placed, DriverConfig config, Func<CancellationToken, Task<HelpMachine?>> machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        Executors.Of<HelpStart>().RunHelpAsync(placed, config, machine, onEnded, ct);

    /// <inheritdoc cref="ChatWalk.AskAsync"/>
    internal Task<ConversationWalk> WalkAsync(
        string actor, IReadOnlyList<ActorEffective> order, DriverConfig config, string? workspace, string? profile, CancellationToken ct,
        (ActorEffective Entry, string? Account)? refused = null, bool counts = true, long except = 0) =>
        _walk.AskAsync(actor, order, config, workspace, profile, ct, refused, counts, except);

    /// <summary>Where Ask Daoris's conversation runs (<see cref="HelpPlacement.In"/>).</summary>
    internal ChatPlace HelpPlace(string room, IReadOnlyList<HelpRead> reads, string? workspace = null) => HelpPlacement.In(_home, room, reads, workspace);

    /// <inheritdoc cref="HelpAccessSwap.Chosen"/>
    public void HelpAccessChosen(string mode) => Executors.Of<HelpAccessSwap>().Chosen(mode);

    /// <inheritdoc cref="HelpAccessSwap.Swapping"/>
    public IReadOnlyList<string> HelpSwapping(string mode) => Executors.Of<HelpAccessSwap>().Swapping(mode);

    /// <inheritdoc cref="HelpAccessSwap.GrantPending"/>
    public bool GrantPending(string grant, ConfiguredAccess chosen) => Executors.Of<HelpAccessSwap>().GrantPending(grant, chosen);

    /// <inheritdoc cref="HelpAccessSwap.TurnChanged"/>
    internal void HelpTurnChanged(string sessionId, ChatQueue queue) => Executors.Of<HelpAccessSwap>().TurnChanged(sessionId, queue);

    /// <summary>The runner's shared state, for tests that stand a conversation up without a spawn.</summary>
    internal ChatContext Context => _context;

    /// <summary>
    /// A conversation whose record is open: its process spawned with its rules and servers, its door's
    /// turns held, and its watch started — or, when the spawn fails, its record concluded.
    /// </summary>
    /// <param name="goOn">
    /// An ended chat going on (MSG1c, D137 §2.2): its record already moved to working by the ledger's one move out of an
    /// ended state, its own conversation resumed rather than opened, and the person's words waiting its first turn. Null
    /// for a new conversation.
    /// </param>
    /// <param name="chosen">
    /// The entry of its actor's order this new conversation runs (MODELROLE1d, D205 §4.1): its <c>choice</c> and what it asks. Null
    /// for one going on, which keeps its record's, and for a start that walked nothing, which runs as before.
    /// </param>
    /// <param name="pending">
    /// Its pending start (MODELROLE1d2, D205 §3.5(b)): refused at the open, it is re-asked, and where the answer is not this same session the
    /// record ends <c>choice-not-offered</c> and the replacement opens with its words. Null for one going on, and for a start that asks nothing.
    /// </param>
    internal async Task<ChatStart> RunAsync(
        string actor, string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
        ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn = null, ActorEffective? chosen = null,
        ChatPending? pending = null)
    {
        if (goOn is not null) pending = null;
        var workTree = place.Tree;
        var transcript = DaorisHome.Entry(_home, HomeEntry.Sessions, $"{sessionId}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);
        // The place as asked, before its mode is applied below: what the one retry of a native go-on runs in again (SESSCTX1c).
        var asked = place;
        actor = goOn?.Record.Actor ?? actor;
        var actorAsked = goOn?.Record.Asked ?? chosen?.Asked ?? ActorAsked.Before(resolved.Name);
        var choice = goOn?.Record.Choice ?? chosen?.From ?? "before";
        // MODELROLE1d (D205 §4.2): a conversation going on re-applies the person's last composer change over its asked choice, so its
        // resume sets what they last left; the record's asked stays as its open set it.
        var overriding = goOn is null ? null : ActorOverride.Read(goOn.Record.Override);
        _options.Kept(sessionId, overriding);
        var carrying = actorAsked.Overlaid(overriding);

        // AGENTPROVIDER1m-i (ONEWAY1h-iii): the run over today's transports holds the conversation's process, its capture and its end, its stop
        // and its close; its words stay beside it, on the session and the process the run holds, until m-ii.
        TransportRun? run = null;
        TransportStart? start = null;
        string? rules = null;
        string? servers = null;
        string? skills = null;
        object? meta = null;
        IReadOnlyList<AcpMcpServer> pluginServers = [];
        // What its door is handed of the servers: the knowledge connector and the plugins', composed once for both doors, so the
        // receipt names the list the spawn got (HELPWIRED1).
        IReadOnlyList<AcpMcpServer> offered = [];
        Noted? browserNotice = null;
        var drivesBrowser = false;
        string? modeSaid = null;
        string? helpMode = null;
        try
        {
            // A conversation going on is working already: the ledger moved it there before anything was spawned.
            if (goOn is null) await _service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            // HELPMODE1a1 (D183 point 1): Ask Daoris's place under the mode the person chose, refused where its agent has none such.
            var ran = goOn is null ? null : HelpModes.Recorded(goOn.Record.HelpMode);
            (place, modeSaid, helpMode) = HelpPlacement.UnderMode(place, resolved, config, ran);

            // HELPMODE1a2 (D183 point 3): one going on under another mode than it last ran under runs under the new one, and its record
            // says so before anything is spawned, as its transcript's first line does.
            if (ran is not null && helpMode is not null && helpMode != ran)
            {
                await _service.KeepHelpModeAsync(sessionId, helpMode, ct).ConfigureAwait(false);
            }

            var info = resolved.PrepareChat(
                // The native door resumes its own conversation by the id kept for it; the protocol door on its wire.
                new ChatTarget(place.Name, workTree, _service.BaseUrl)
                {
                    Asked = carrying,
                    Resume = resolved.Wire == SessionWire.Pipe ? goOn?.Ask.Conversation : null,
                    // Its harness's mode that asks nothing, where Ask Daoris runs with full access on the native door (HELPMODE1a1).
                    PermissionMode = place.PermissionMode,
                    // Its own record, so a take through the connector its harness starts marks it (CHATTAKE1b).
                    Session = sessionId,
                },
                config.Commands.GetValueOrDefault(resolved.Name));
            // Its toolchain's environment and its tools at the start, as every driven session's (CONTEXTONE1d).
            Driver.Equip(resolved, info, selection);

            // CONTEXTONE1c (D230 point 3): its standing block, on its harness, told with a new conversation's first message and never at a
            // go-on, which resumes the same conversation; a hand-over's adds the memory its brief hands on (WithProcess).
            if (place.Block is { } block && goOn is null) _told.HoldBlock(sessionId, StandingBlocks.OnHarness(block, resolved, workTree));

            // The run's bracket, before what names its process is composed: what this start is handed of the downloads kept on its record
            // (KEEP1b), a go-on as a first start, and its process's lineage token issued (COWORKVIEW1a), a go-on's the one issued at its move,
            // its retry's its own.
            start = new TransportStart(info, StartClass.Person) { Moved = goOn?.Proc };
            run = new TransportRun(new ChatTransportHost(_home, _processes, _service, _context.Conversations, config, _output), resolved, start);
            var proc = await run.BracketAsync(sessionId, ct).ConfigureAwait(false);

            // HELPPLUG1b (D170 point 4): the skills plugins hand this conversation's destination, composed under the home and
            // handed by the harness's own session-only door; the protocol door carries them beside its rules below.
            IReadOnlyList<string> skillPlugins = [];
            if (place.Plugins)
            {
                (skills, skillPlugins) = SpawnSkills.Hand(
                    resolved, info, _home, sessionId, PluginCatalog.Load(_home, AdapterSet.Built().Names), place.ServersTo);
            }

            // What the conversation's agent may do (PERM1, D72), handed by each door's own way: a flag on
            // the pipe, `session/new`'s `_meta` on the protocol door, now that a conversation there opens a
            // session (CONV3b).
            RuleLists? composed = null;
            var guarded = false;
            // CHATFLOW1h (the chat-flow design §4.4): a conversation in a tree of its own is held to its run's checks at every execution, its open, a
            // hand-over's next record and a go-on alike: the tree locked in its id for `run-heavy` to find, the checks kept by it, each heavy gate's
            // command denied under `light`, and the conversation told only then.
            var heavy = await _told.HeldChecksAsync(sessionId, workTree, place, config, resolved, ct).ConfigureAwait(false);
            if (resolved.TakesSettings)
            {
                var file = PermissionRules.Load(_home);
                composed = place.Rules(file, sessionId);
                if (heavy is { Deny.Count: > 0 }) composed = composed with { Deny = [.. composed.Deny.Union(heavy.Deny, StringComparer.Ordinal)] };
                var guard = PermissionRules.GuardsTree(file) && place.Guarded ? TreeGuard.For(_home, workTree, place.Also) : null;
                guarded = guard is not null;
                rules = SpawnSettings.Write(
                    _home, sessionId, composed, [.. PermissionRules.HardDeny(file), .. heavy.HardDeny is { } sentence ? [sentence] : (IEnumerable<string>)[]], guard,
                    // Which agent made each call, and each subagent's start and stop (FABRIC-C1a).
                    ChildLineage.HookFor(_home, sessionId));
                if (resolved.Wire == SessionWire.Pipe) resolved.HandSettings(info, rules);
                else meta = resolved.AcpSessionMeta(rules, skillPlugins);
            }

            // The plugins' servers, resolved once for both doors — with Daoris's own browser brought up
            // for one that drives it (D78), or that one left out and the conversation told why.
            if (place.Plugins)
            {
                IReadOnlyList<string> drivers;
                (pluginServers, browserNotice, drivesBrowser, drivers) = await ChatServers.HandAsync(
                    _home, _browser, _plugins, sessionId, ct, place.ServersTo, place.EveryServer).ConfigureAwait(false);
                // A new conversation's agent is told of the browser with its first message (BRWHINT1a); one going on resumes a
                // conversation already told when it opened, so it is told nothing again.
                if (goOn is null && InAppBrowserServers.Told(drivers) is { } line) _context.Browsers[sessionId] = line;
                if (browserNotice is not null)
                {
                    _context.Record(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = browserNotice.Note, Parts = browserNotice.Parts });
                }
            }

            // 🔴 The servers the plugins hand every session (D64, D65 §1f), on this door too (REV3). The
            // protocol door carries them on `session/new` (`Servers`); a driven or intake session on the
            // pipe is handed a file — and a conversation on the pipe was handed nothing at all. A room
            // has no `.mcp.json` of its own, so its connector goes in the same file (HELP1a).
            offered = resolved.Wire == SessionWire.Acp || place.ConnectorOnPipe
                ? _servers.For(sessionId, place.Circle, pluginServers, resolved.Wire == SessionWire.Acp ? proc : null)
                : pluginServers;
            if (resolved.Wire == SessionWire.Pipe) servers = SpawnServers.Hand(resolved, info, _home, sessionId, offered);

            // HELPWIRED1 (D183 point 4): what Ask Daoris's start handed, kept now its every input is composed and before its process
            // starts, read from the spawn itself: whether its command line or its protocol door's meta names each file.
            if (helpMode is not null)
            {
                var pipe = resolved.Wire == SessionWire.Pipe;
                Executors.Of<HelpAccessSwap>().KeepWired(sessionId, new HelpHanded(helpMode, goOn is not null, resolved.Name, resolved.Wire, workTree)
                {
                    Version = selection.Version,
                    Account = selection.Profile,
                    Posture = pipe ? null : place.Posture ?? resolved.AcpPosture,
                    PermissionMode = pipe ? place.PermissionMode : null,
                    Rules = composed,
                    RulesHanded = rules is not null && (pipe ? HelpWired.Names(info, rules) : HelpWired.Carries(meta, rules)),
                    Guarded = guarded,
                    Reads = place.Reads,
                    SkillPlugins = skillPlugins,
                    SkillsHanded = skillPlugins.Count > 0
                        && skillPlugins.All(plugin => pipe ? HelpWired.Names(info, plugin) : HelpWired.Carries(meta, plugin)),
                    Servers = offered,
                    ServersHanded = !pipe || (servers is not null && HelpWired.Names(info, servers)),
                    Withheld = browserNotice,
                    Housekeeping = place.Housekeeping,
                    Settings = place.Settings,
                });
            }

            // D205 row b: reported alone, so a refusal here costs the record its provenance, never the conversation.
            // An override re-applied is the person's change, never the open's asked, so the record's asked is left as it was.
            if (overriding is null)
            {
                using var startLog = new MachineLog(_home, "driver");
                await _service.TryKeepActorAsync(sessionId, startLog, choice, ActorAsked.ForSpawn(resolved.Name, info, actorAsked), ct: ct)
                    .ConfigureAwait(false);
            }
            // Never once the driver is closing. The run's spawn passes the start gate (PROCSTART2), which a conversation's press may reach before
            // its first wait, through the launcher (TERMTRUST1a, D201 point 4): its harness born in this process's agents' job, the launcher read
            // as the conversation's process.
            if (_context.Closing)
            {
                // Let go before its spawn, the run lets its token go.
                await run.DisposeAsync().ConfigureAwait(false);
                SpawnSettings.Remove(rules);
                SpawnServers.Remove(servers);
                SpawnSkills.Remove(skills);
                await _conclusion.ConcludeAsync(sessionId, "stopped", ClosedNoted, onEnded).ConfigureAwait(false);
                if (_processes.Close.Build is { } build) _closing.KeepRestart(sessionId, build);
                return new(sessionId, message);
            }

            // Tracked from its spawn, so who is driving Daoris's browser lasts exactly as long as it (BRW8), and reaped; a conversation is not
            // ended by the request that started it, so its spawn is on no request's token.
            start = start with { DrivesBrowser = drivesBrowser };
            run.HandStart(start);
            await run.SpawnAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // What the run was handed goes with it: a token never spawned, a go-on's among them, or one its spawn let go.
            if (run is not null) await run.DisposeAsync().ConfigureAwait(false);
            else if (goOn?.Proc is { } moved) LineageTokens.Current.Unspawned(moved);
            // The record exists and must say what happened, or it sits at `queued` or `starting` forever
            // holding the repository — the same rule the driven path follows. EVERY failure: a client
            // timeout is an OperationCanceledException too, and filtering those out left the record
            // stranded where no stop and no sweep reaches it (REV3).
            SpawnSettings.Remove(rules);
            SpawnServers.Remove(servers);
            SpawnSkills.Remove(skills);
            _told.LetGoChecks(sessionId);
            var cancelled = error is OperationCanceledException && ct.IsCancellationRequested;
            await _conclusion.ConcludeAsync(
                sessionId, "failed",
                cancelled
                    ? Noted.Of(NoteCodes.ChatCancelled, "the request that started it was cancelled before its process started.")
                    : Observation.Failure(error.Message),
                onEnded).ConfigureAwait(false);
            if (cancelled) throw;
            return new(null, error.Message);
        }

        // Its turns are kept in the usage history under the account it runs as (USAGECAP1a2), as a driven session's are: bound
        // before any word is queued to it.
        UsageTurns.Bound(_home, _events, sessionId, TurnBinding.Of(resolved, selection.Profile) with { Actor = actor });
        var actorLog = new MachineLog(_home, "driver");
        var actorRun = new ActorRun(_events, sessionId, actor, goOn?.Record.Ran, ran => _service.KeepActorAsync(sessionId, ran: ran), actorLog);
        _context.ActorRuns[sessionId] = actorRun;

        // The conversation outlives this call: the person types, the harness answers, and the record
        // moves when the process does. Watched on an unbound token deliberately — a chat is not ended
        // by the request that started it.
        _context.Talking[sessionId] = resolved;
        _context.Runs[sessionId] = run!;
        // One at a time in each workspace (ASKHIST1, ASKSCOPE1a): going back to an earlier conversation of its workspace sets this one
        // aside first, and one of another workspace's leaves it be.
        if (string.Equals(place.Name, HelpRoom.Repository, StringComparison.Ordinal)) _context.Rooms[sessionId] = RemoteTarget.Workspace(place.Workspace);
        // The access it got, its mode and its configured access, which a change of the person's is compared with while it runs
        // (ACCESSSWAP1).
        if (helpMode is not null)
        {
            _context.AccessHanded[sessionId] = new ConfiguredAccess(place.Housekeeping) { Settings = place.Settings, CheckoutsOff = place.CheckoutsOff };
            _context.HelpModes[sessionId] = helpMode;
        }

        // 🔴 On the protocol door a conversation is ONE session held over the wire (CONV3b): opened
        // once, each message a turn on it. It used to be spawned as a pipe, and a person's words went
        // into the JSON-RPC stream as raw text — the harness declared itself interactive and could not
        // hold a conversation at all.
        ProtocolChat? chat = null;
        NativeChat? native = null;
        var mapper = resolved.StructuredOutput();
        // The turn's end it was last judged at by its account's usage curve (SWITCH1c-ii): told under its turns' own lock, in order.
        DateTimeOffset? judged = null;
        // Whether a turn's refusal was read for its account's limit since the last turn's end was judged (LIMITGOON1): set on the turn's
        // own thread, taken at its end.
        var limitedTurn = 0;
        // Only a repository's chat has a chat run (CHATFLOW1c); Ask Daoris, a helper and a reviewer have none, which the bind reads too.
        var bindsRun = actor == "chat";
        void Changed(ChatQueue queue)
        {
            QueueChanged?.Invoke(sessionId, queue);
            // A full-access conversation the person narrowed mid-turn ends as its turn ends (HELPMODE1a5).
            HelpTurnChanged(sessionId, queue);
            // A conversation asked to go on with another agent while it talked is handed over as its turn ends (MIXAGENT1a-i).
            Executors.Of<ChatHandOver>().Due(sessionId, queue, config, onEnded);
            // One over its account's usage curve goes on with another account of its agent as its turn ends (SWITCH1c-ii), judged once;
            // one whose turn its account's limit refused goes on, in the same conversation, on the next account (LIMITGOON1), and only that.
            if (CurveMove.TurnJustEnded(queue, judged))
            {
                judged = queue.LastTurnEnded;
                // CHATFLOW1c: a chat whose run is not bound yet (its tree grew before this build, or its bind failed) binds now.
                if (bindsRun) _ = _context.ChatRuns.TurnEndedAsync(sessionId, workTree, config);
                if (Interlocked.Exchange(ref limitedTurn, 0) == 1) Executors.Of<LimitMove>().Due(sessionId, config, onEnded);
                else Curve.Due(sessionId, config, onEnded);
            }

            // A turn the person's words started has ended (NOTIFY1a): judged last, so a listener's failure moves nothing above.
            if (_answers.Turned(sessionId, place.Name, queue) is { } answered) Answered?.Invoke(answered);
        }
        // A conversation going on hands its words to the native door only once its watch began, so a word the person says meanwhile goes
        // after them; the protocol door waits for its session to open anyway (MSG1c).
        var tracked = goOn is null ? null : new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (resolved.Wire == SessionWire.Acp)
        {
            chat = new ProtocolChat(
                Changed,
                stopped: () => _processes.WasStopRequested(sessionId),
                limited: (failure, byCode) =>
                {
                    var cooling = Limited(sessionId, resolved, selection.Profile, failure, config.CoolOff, byCode);
                    if (cooling is not null) Interlocked.Exchange(ref limitedTurn, 1);
                    return cooling;
                },
                signedOut: failure => SignedOut(resolved, selection.Profile, failure),
                goOn: goOn?.Ask,
                // On this door the words went once their prompt is on the wire.
                wentOn: goOn is null ? null : () =>
                {
                    if (goOn.Sent(named: false)) _ = GoOn.WentOnAsync(sessionId, goOn, resolved.Name);
                })
            {
                // AGENTERRORS1b (D210, the agent-errors design §5.1, §7): a transient failure of one of its turns is tried again in this same
                // conversation, on this process, with the one reader over the adapter set; handed to its session once it is open.
                Retry = new ChatRetry(
                    sessionId, resolved.Name, RetryClock,
                    refused =>
                    {
                        var read = AgentErrors.Read(_harnesses.Adapters, resolved, DoorFailure.OfPrompt(refused), RetryClock.GetUtcNow(), _harnesses.Zone);
                        using var log = new MachineLog(_home, "driver");
                        log.Info("agent.error", ("session", sessionId), ("adapter", resolved.Name), ("account", selection.Profile), ("kind", read.Spelled), ("by", read.By));
                        return read;
                    })
                {
                    Tried = (episode, number, next) =>
                    {
                        using var log = new MachineLog(_home, "driver");
                        log.Info(
                            "agent.transient",
                            ("session", sessionId), ("adapter", resolved.Name), ("account", selection.Profile), ("by", episode.First.By), ("try", number),
                            ("next", next));
                    },
                },
                // MODELROLE1d (D205 §3.2): its actor's model and effort set before its first prompt, the composer opening on them; what
                // the agent would not take kept as asked and not carried, and what its open listed kept as its agent's reading (§9.2).
                Asked = carrying,
                Carried = overriding is not null ? null
                    : notCarried => _ = _service.TryKeepActorAsync(sessionId, null, choice, notCarried, ct: CancellationToken.None),
                OffersRead = options => _options.KeepOffers(resolved.Name, selection, sessionId, options),
                Pending = pending,
                Shown = said => _context.TakeShown(sessionId, said),
                ModelRefused = pending is null ? null : async refusal =>
                {
                    // MODELROLE1d2 (D205 §3.5(b)): kept as the account's reading first, then the walk asked again as if it had been known.
                    var later = await Executors.Of<ChatReplacement>().ReaskAsync(pending, sessionId, resolved, selection, config, refusal).ConfigureAwait(false);
                    if (later is null) return null;
                    choice = later.From;
                    await _service.TryKeepActorAsync(sessionId, null, choice, later.Asked, ct: CancellationToken.None).ConfigureAwait(false);
                    return later.Asked;
                },
            };
            chat.Turns.Closing = () => _context.Closing;
            chat.Turns.Unsent = count => _closing.Unsent(sessionId, count);
            _context.Turned[sessionId] = chat;
        }
        else if (mapper is not null)
        {
            // 🔴 Turns are only visible where the wire says where one ends (CONV4a). A text-only pipe
            // takes each line at once, as it always did.
            native = new NativeChat(
                sessionId, resolved, _processes, e => _context.Record(sessionId, e), Changed, tracked?.Task,
                // On this door the words went once their line is on stdin and the harness has opened the conversation.
                wentOn: goOn is null ? null : () =>
                {
                    if (goOn.Sent(named: true)) _ = GoOn.WentOnAsync(sessionId, goOn, resolved.Name);
                })
            {
                Shown = said => _context.TakeShown(sessionId, said),
            };
            native.Turns.Closing = () => _context.Closing;
            native.Turns.Unsent = count => _closing.Unsent(sessionId, count);
            _context.Turned[sessionId] = native;
        }

        if (_context.Closing)
        {
            (chat?.Turns ?? native?.Turns)?.HoldForClose();
            if (_processes.Close.Build is not null) _closing.UpdateCuts.TryAdd(sessionId, 0);
        }

        // The person's words a conversation goes on with are its first turn (MSG1c, D137 §2.2), queued before any word said
        // to it once it runs.
        // COWORK1c: marked attempted on the record before the turn carrying them is on the wire, so a shell cut off under it never sends
        // them again.
        var first = goOn is null
            ? null
            : new ChatMessage(goOn.Ask.Prompt, [])
            {
                GoesOn = goOn.Ask, Attempting = () => SaidDoor.AttemptedAsync(_context.Service, sessionId, goOn.Ask.Words),
                // CHATFLOW1e (§6.1 (f)): a go-on with another agent's findings has the answering turn's completion of its own.
                Answering = goOn.Answering,
            };
        if (first is not null)
        {
            // Their words, so the turn's end they start is an answer they wait on (NOTIFY1a).
            _answers.Asked(sessionId);
            (chat?.Turns ?? native?.Turns)?.Say(first);
        }

        // What the conversation consumed counts toward the account it ran as (USAGE1), as a driven
        // session's does: at its end, at its high-water mark, where the door reported one.
        void Measured(AcpUsage used) => _usage.Record(new UsageEntry(
            sessionId, place.Name, resolved.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));

        // What its door says about its account's windows is kept for the next start's walk (TOOL6c), as a driven session's is.
        void Said(JsonElement info)
        {
            try
            {
                _harnesses.Said(resolved.Name, selection.Profile, info, sessionId);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Nothing kept is nothing said: the account reads as unknown, as it did before the door spoke.
            }
        }

        // What the run is handed before its open: a native door's events and its conversation named, the protocol door's session held open.
        run!.HandStart(start! with
        {
            // The turn's end, once the record holds it, lets the next message go (CONV4a).
            Observed = native is null ? null : e =>
            {
                if (e.Kind == SessionEventKind.Turn) native.TurnEnded();
            },
            // 🔴 Its harness opened the conversation its words went to, so they went (MSG1c): off the record by their ids.
            Named = goOn is null ? null : () =>
            {
                if (goOn.Named()) _ = GoOn.WentOnAsync(sessionId, goOn, resolved.Name);
            },
            Protocol = chat is null ? null : _held.Of(run, chat, sessionId, workTree, place.Posture ?? resolved.AcpPosture, meta, offered, resolved, Said),
        });

        var watch = _watch.WatchAsync(
            sessionId, run, transcript, onEnded, Measured, rules, mapper, chat, native, servers, Said,
            new ChatWatched(resolved.Name, place.Keeps, goOn) { Skills = skills, Preamble = modeSaid }, actorRun, actorLog);
        _context.Watching[sessionId] = watch;
        _ = watch.ContinueWith(
            _ => _context.Watching.TryRemove(new KeyValuePair<string, Task>(sessionId, watch)), TaskScheduler.Default);
        // Its watch began: the words it goes on with may reach it.
        tracked?.TrySetResult(true);
        // MODELROLE1d2 (D205 §3.5(b)): where its open was refused and the re-ask answered another session, the replacement opens once this
        // run let its process, its files and its turns go, in the same call as far as the person is concerned.
        if (pending is not null) _ = Executors.Of<ChatReplacement>().ReplaceAsync(watch, pending, sessionId, selection);
        // 🔴 SESSCTX1c (D189's amendment): a native go-on whose harness ended before naming its conversation is tried once more as the
        // same resume, its record still working, once its watch let its process, its files and its turns go; never while closing.
        if (goOn is not null)
        {
            _ = AgainAsync(
                watch,
                () => RunAsync(
                    actor, sessionId, message, resolved, selection, config, asked, onEnded, CancellationToken.None, new GoingOn(goOn.Record, goOn.Ask)),
                () => goOn.Ask.Refused is { Lost: true } lost
                    ? GoOn.LostAsync(goOn.Record, lost, resolved.Name, config, onEnded, held: false, CancellationToken.None)
                    : Task.FromResult(new ChatStart(null, "")));
        }

        return new(sessionId, message);
    }

    /// <summary>
    /// The one retry of a native go-on (SESSCTX1c), where its watch asked for it and the runner is not closing; else, where the wire
    /// refused the resume as lost (SESSCTX1c part B), the new part it goes on in, once this run let its process, files and turns go, since
    /// that part starts in the same tree.
    /// </summary>
    private async Task AgainAsync(Task<bool> watch, Func<Task<ChatStart>> again, Func<Task<ChatStart>> lost)
    {
        try
        {
            var retry = await watch.ConfigureAwait(false);
            if (_context.Closing) return;
            await (retry ? again() : lost()).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Its own run concludes the record whatever fails there; a watch that threw has said its own failure.
        }
    }

    /// <summary>
    /// A conversation's start refused at its open moved on: the refused record and the replacement that took its words
    /// (<see cref="ChatReplacement.Replaced"/>).
    /// </summary>
    public event Action<string, string>? Replaced
    {
        add => Executors.Of<ChatReplacement>().Replaced += value;
        remove => Executors.Of<ChatReplacement>().Replaced -= value;
    }

    /// <inheritdoc cref="ChatClosing.StopAll"/>
    public void StopAll(TimeSpan bound) => _closing.StopAll(bound);

    /// <summary>
    /// Ends every conversation and waits for each record (<see cref="StopAll"/>). Dispose it inside the scope of the
    /// <see cref="ServiceClient"/> it was given: its conclusions go through that client, and a runner stopped after the client was disposed
    /// writes nothing.
    /// </summary>
    public void Dispose() => StopAll(TimeSpan.FromSeconds(10));

    /// <inheritdoc cref="ChatClosing.Finish"/>
    public bool Finish(string sessionId) => _closing.Finish(sessionId);

    /// <inheritdoc cref="ChatClosing.Finishing"/>
    public bool Finishing(string sessionId) => _closing.Finishing(sessionId);

    /// <inheritdoc cref="ChatClosing.WoundUpAsync"/>
    public Task<bool> WoundUpAsync(string sessionId, TimeSpan bound, CancellationToken ct = default) => _closing.WoundUpAsync(sessionId, bound, ct);

    /// <inheritdoc cref="ChatWords.Say(string, string, out string?, IReadOnlyList{ChatUpload}?, string?, MessageLevel)"/>
    public bool Say(string sessionId, string message, IReadOnlyList<ChatUpload>? files = null, string? preface = null) =>
        _words.Say(sessionId, message, out _, files, preface);

    /// <inheritdoc cref="ChatWords.Say(string, string, out string?, IReadOnlyList{ChatUpload}?, string?, MessageLevel)"/>
    public bool Say(
        string sessionId, string message, out string? reaches, IReadOnlyList<ChatUpload>? files = null, string? preface = null,
        MessageLevel level = MessageLevel.Urgent) =>
        _words.Say(sessionId, message, out reaches, files, preface, level);

    /// <inheritdoc cref="ChatWords.CurrentOf"/>
    public string CurrentOf(string sessionId) => _words.CurrentOf(sessionId);

    /// <inheritdoc cref="ChatWords.Replacing"/>
    public bool Replacing(string sessionId) => _words.Replacing(sessionId);

    /// <inheritdoc cref="ChatWords.Tell"/>
    public bool Tell(string sessionId, string message, string key) => _words.Tell(sessionId, message, key);

    /// <inheritdoc cref="ChatWords.SayKeptAsync"/>
    public Task<ChatSaid?> SayKeptAsync(
        string sessionId, string message, IReadOnlyList<ChatUpload>? files, string? preface, MessageLevel level, string? key,
        CancellationToken ct = default) =>
        _words.SayKeptAsync(sessionId, message, files, preface, level, key, ct);

    /// <inheritdoc cref="ChatWords.SayNowAsync"/>
    public Task<SentNow?> SayNowAsync(
        string sessionId, string message, IReadOnlyList<ChatUpload>? files = null, string? preface = null) =>
        _words.SayNowAsync(sessionId, message, files, preface);

    /// <inheritdoc cref="ChatWords.HandDraft"/>
    public void HandDraft(string sessionId, WorkflowDraftTold draft) => _words.HandDraft(sessionId, draft);

    /// <inheritdoc cref="ChatTold.With"/>
    internal ChatMessage WithProcess(string sessionId, ChatMessage message) => _words.WithProcess(sessionId, message);

    /// <inheritdoc cref="ChatTurnDoors.Taking"/>
    public bool Taking(string sessionId) => _turns.Taking(sessionId);

    /// <inheritdoc cref="ChatTurnDoors.Reach"/>
    public string? Reach(string sessionId) => _turns.Reach(sessionId);

    /// <inheritdoc cref="ChatTurnDoors.Door"/>
    public TurnDoor? Door(string sessionId) => _turns.Door(sessionId);

    /// <inheritdoc cref="ChatTurnDoors.Holds"/>
    public bool Holds(string sessionId) => _turns.Holds(sessionId);

    /// <inheritdoc cref="ChatTurnDoors.Queue"/>
    public ChatQueue Queue(string sessionId) => _turns.Queue(sessionId);

    /// <inheritdoc cref="ChatTurnDoors.TurnEnded"/>
    public bool? TurnEnded(string sessionId) => _turns.TurnEnded(sessionId);

    /// <inheritdoc cref="IChatPressing.HoldAsync"/>
    public Task<PressHold> HoldAsync(string sessionId, CancellationToken ct = default) => _turns.HoldAsync(sessionId, ct);

    /// <inheritdoc cref="IChatPressing.Release"/>
    public void Release(string sessionId) => _turns.Release(sessionId);

    /// <inheritdoc cref="IChatSendBacks.SendBackAsync"/>
    public Task<SentBack> SendBackAsync(ChatSendBackAsk ask, CancellationToken ct = default) => _sendBack.SendBackAsync(ask, ct);

    /// <inheritdoc cref="IChatSendBacks.GoOnWithFindingsAsync"/>
    public Task<SentBack> GoOnWithFindingsAsync(ChatSendBackAsk ask, CancellationToken ct = default) => _sendBack.GoOnWithFindingsAsync(ask, ct);

    /// <inheritdoc cref="ChatSendBack.AnswersFindingsAsync"/>
    internal Task<bool> AnswersFindingsAsync(string sessionId, string tree, DriverConfig config) => _sendBack.AnswersFindingsAsync(sessionId, tree, config);

    /// <inheritdoc cref="ChatTurnDoors.EditQueued"/>
    public QueueEdit EditQueued(string sessionId, string id, string? text, MessageLevel? level = null) => _turns.EditQueued(sessionId, id, text, level);

    /// <inheritdoc cref="ChatTurnDoors.EditQueuedAsync"/>
    public Task<QueueEdit> EditQueuedAsync(
        string sessionId, string id, string? text, MessageLevel? level = null, CancellationToken ct = default) =>
        _turns.EditQueuedAsync(sessionId, id, text, level, ct);

    /// <inheritdoc cref="ChatTurnDoors.CancelTurnAsync"/>
    public Task<TurnStop> CancelTurnAsync(string sessionId, bool keep = false) => _turns.CancelTurnAsync(sessionId, keep);

    /// <inheritdoc cref="ChatOptions.Of"/>
    public IReadOnlyList<AcpConfigOption> Options(string sessionId) => _options.Of(sessionId);

    /// <inheritdoc cref="ChatOptions.SetAsync"/>
    public Task<IReadOnlyList<AcpConfigOption>> SetOptionAsync(string sessionId, string configId, string value, CancellationToken ct) =>
        _options.SetAsync(sessionId, configId, value, ct);

    // The chat runner's port (Course's IChatRunning): the operations the course's executors call, forwarded as they are. The public ones
    // implement it where they stand; the internal ones, whose places are internal records, answer it here.
    Task<ChatStart> IChatRunning.RunAsync(
        string actor, string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
        ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn, ActorEffective? chosen, ChatPending? pending) =>
        RunAsync(actor, sessionId, message, resolved, selection, config, place, onEnded, ct, goOn, chosen, pending);

    ChatPlace IChatRunning.RepositoryPlace(
        DriverConfig config, IReadOnlyList<RepoView> registry, RepoView known, string repository, string workTree, IReadOnlyList<string>? continued,
        bool answers) =>
        RepositoryPlace(config, registry, known, repository, workTree, continued, answers);

    ChatPlace IChatRunning.HelpPlace(string room, IReadOnlyList<HelpRead> reads, string? workspace) => HelpPlace(room, reads, workspace);

    Task<bool> IChatRunning.AnswersFindingsAsync(string sessionId, string tree, DriverConfig config) => AnswersFindingsAsync(sessionId, tree, config);

    ChatMessage IChatRunning.WithProcess(string sessionId, ChatMessage message) => WithProcess(sessionId, message);

    Task<ConversationWalk> IChatRunning.WalkAsync(
        string actor, IReadOnlyList<ActorEffective> order, DriverConfig config, string? workspace, string? profile, CancellationToken ct,
        (ActorEffective Entry, string? Account)? refused, bool counts, long except) =>
        WalkAsync(actor, order, config, workspace, profile, ct, refused, counts, except);

    ChatContext IExecutorWiring.Context => _context;

    T IExecutorWiring.Of<T>() => Executors.Of<T>();

    Task IExecutorWiring.ConcludeAsync(string sessionId, string state, Noted note, Func<string, string, Task>? onEnded) =>
        _conclusion.ConcludeAsync(sessionId, state, note, onEnded);

    RotatedOpening.Carried? IExecutorWiring.LimitCarried(string? carries) => LimitCarried(carries);

    /// <summary>What a turn's message puts in the record as it is sent (<see cref="ChatRecorded.Recorded"/>).</summary>
    internal static IEnumerable<SessionEvent> Recorded(ChatMessage message, IReadOnlyList<SessionEvent>? shown = null) =>
        ChatRecorded.Recorded(message, shown);

    /// <summary>What the agent was told beside the person's words, as the record keeps it (<see cref="ChatRecorded.Told"/>).</summary>
    internal static SessionEvent? Told(ChatMessage message) => ChatRecorded.Told(message);

    /// <summary>The browser the agent was handed and told of, as the record keeps it (<see cref="ChatRecorded.ToldBrowser"/>).</summary>
    internal static SessionEvent? ToldBrowser(ChatMessage message) => ChatRecorded.ToldBrowser(message);

    /// <summary>The repository's process the agent was told, as the record keeps it (<see cref="ChatRecorded.ToldProcess"/>).</summary>
    internal static SessionEvent? ToldProcess(ChatMessage message) => ChatRecorded.ToldProcess(message);

    /// <inheritdoc cref="ChatTold.Process"/>
    internal static ProcessBriefing ProcessTold(ResolvedCommits commits, LandingPlan? plan, string? dependencies = null, ISessionAdapter? adapter = null) =>
        ChatTold.Process(commits, plan, dependencies, adapter);

    /// <inheritdoc cref="ChatAccountReads.Limited"/>
    internal Noted? Limited(string sessionId, ISessionAdapter adapter, string? profile, string failure, TimeSpan? coolOff = null, LimitSeen? byCode = null) =>
        _accounts.Limited(sessionId, adapter, profile, failure, coolOff, byCode);

    /// <inheritdoc cref="ChatAccountReads.Carried"/>
    private RotatedOpening.Carried? LimitCarried(string? carries) => _accounts.Carried(carries);

    /// <inheritdoc cref="ChatAccountReads.SignedOut"/>
    internal Noted? SignedOut(ISessionAdapter adapter, string? profile, string failure) => _accounts.SignedOut(adapter, profile, failure);

    /// <summary>What a conversation's ending note adds where Daoris kept no id for its harness conversation (<see cref="ChatEnded.NotKept"/>).</summary>
    public const string NotKept = ChatEnded.NotKept;

    /// <summary>The English a conversation the person ended concludes with (<see cref="ChatEnded.EndedNote"/>): what the limits stage leaves by.</summary>
    internal const string EndedByPersonNote = ChatEnded.EndedByPersonNote;

    /// <summary>How a conversation that simply ended says so (<see cref="ChatEnded.EndedNote"/>).</summary>
    internal static Noted EndedNote(bool stopped, bool notKept) => ChatEnded.EndedNote(stopped, notKept);

}
