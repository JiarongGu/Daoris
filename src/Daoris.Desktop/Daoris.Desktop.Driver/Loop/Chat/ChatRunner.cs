using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// A conversation with an agent in one repository, entered by a person instead of planned from a quest. It wires the units that do the work
/// and the executors that take each kind of run, and keeps the doors callers use, each a line to the unit or executor behind it.
/// </summary>
/// <remarks>
/// <para><b>Nothing here is a chat loop.</b> Daoris pipes text and observes; the harness carries the model and the conversation. A Daoris-owned
/// loop calling a model API was rejected in the design: it would duplicate what every harness already is, and produce sessions with no
/// doctrine path into them.</para>
///
/// <para><b>A chat may open on a dirty tree</b>, where a driven session may not. The clean-tree rule exists because an unattended agent would
/// entangle itself with somebody's work in flight; in a conversation the person is present, and it is their work in flight.</para>
///
/// <para><b>The lock is the ledger's</b>, not this class's: one active session per repository, judged in one place for every door. This asks,
/// and reports the refusal verbatim.</para>
///
/// <para><b>An ended chat goes on</b>: the person's words kept on its record are taken up at once, its record reopening and its own
/// conversation resumed with them (<see cref="ChatGoOn"/>).</para>
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

    private readonly string _home;

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

    private readonly ChatConclusion _conclusion;

    private readonly ChatRun _run;

    /// <param name="events">
    /// Where a conversation's structure is kept: the shell's shared record, so its page hears each message live; the home's own where nobody
    /// passes one, which is the headless chat door's case.
    /// </param>
    /// <param name="usage">
    /// What each account has carried: the loop's one record where a shell has one, so a chat's end and a driven session's never write it at
    /// once; the home's own for the headless chat door.
    /// </param>
    /// <param name="browser">
    /// Daoris's own browser, asked for by a plugin server that drives it. Null where no shell carries one, which is the headless chat door's
    /// case: such a server is then not handed.
    /// </param>
    /// <param name="plugins">
    /// Where a conversation's plugin servers, handed or withheld, are written without their words: the shell's loop hands its log. Null writes
    /// nothing, the headless chat door's case, which writes no session lines either.
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
        _home = home;
        // The roster of the home it was handed, as every other default here is, or the file the override names. Shared with the driver where a
        // shell has both, so a probe is paid for once.
        harnesses ??= new HarnessRoster(adapters, HarnessSettings.ResolvePath(home));
        // Its turns kept in the usage history, watched once however many runners share the record.
        events = UsageTurns.Watched(home, events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)));
        _context = new ChatContext(service, home, processes, output, harnesses, events);
        _options = new ChatOptions(_context);
        _accounts = new ChatAccountReads(_context);

        Executors = Parts.Build(ExecutorOrder.Value, type => (IExecutor)Activator.CreateInstance(type, this)!);
        _told = new ChatTold(_context, _context.ChatRuns);
        _words = new ChatWords(_context, _told, _answers);
        _turns = new ChatTurnDoors(_context, _answers);
        _starts = new ChatStarts(this, _told);
        _sendBack = new ChatSendBack(this, _context.ChatRuns);
        _walk = new ChatWalk(_context);
        _conclusion = new ChatConclusion(this);
        var watch = new ChatWatch(this, _told, _conclusion, _answers);
        _closing = new ChatClosing(this, _conclusion);
        _run = new ChatRun(
            this, _told, _answers, _options, _accounts, new ChatServers(_context), watch, new ProtocolHeld(this, (id, options) => OptionsChanged?.Invoke(id, options)),
            _conclusion, _closing, usage ?? new SessionUsage(home), browser, plugins ?? PluginLog.None, () => RetryClock,
            (id, queue) => QueueChanged?.Invoke(id, queue), answered => Answered?.Invoke(answered));

        // One holder for the moves of the home, the hand-over's among them: it imports the boxes an older build wrote, the come-up takes a dead
        // shell's moves up through it, and the hand-over takes the move it leaves standing.
        Moves = Executors.Of<LimitMove>().Moves;

        // Once every executor is there to reach: the person's words to an ended chat are taken up as the record hears them.
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
    /// The hand-overs this run holds that wait to be taken, at a look: a dead shell's or an older build's the come-up or an earlier look left
    /// standing, each judged again as it is taken; one whose conversation is talking waits for its turn's end.
    /// </summary>
    /// <param name="onEnded">Told when a new conversation ends, as a start's is.</param>
    public Task<IReadOnlyList<ChatStart>> TakeHandOversAsync(Func<string, string, Task> onEnded, CancellationToken ct = default) =>
        Executors.Of<ChatHandOver>().TakeAllAsync(onEnded, ct);

    /// <summary>
    /// The look's <c>limits</c> stage (<see cref="LimitWaits.LookAsync"/>): each conversation that waits for an account whose wait has come
    /// goes on, its own account first, or moves; nothing is asked where no wait has come.
    /// </summary>
    /// <param name="onEnded">Told when a conversation it woke ends, as a start's is.</param>
    public Task<IReadOnlyList<ChatStart>> WakeLimitsAsync(
        DriverConfig config, Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        Waits.LookAsync(config, onEnded, ct);

    /// <summary>How many times the limits stage asked the service: a test holds that a look with no wait come asks nothing.</summary>
    internal int LimitsAsked => Waits.Asked;

    /// <summary>
    /// The conversations' move record this runner's live limit move and curve move write and its come-up takes up: a proof of an older build's
    /// come-up hands it a holder whose take-up reads every run as alive, and so takes nothing, as a build before the store does.
    /// </summary>
    internal ConversationMoves Moves
    {
        get => Executors.Of<LimitMove>().Moves;
        set => Executors.Of<LimitMove>().Moves = Curve.Moves = Executors.Of<ChatHandOver>().Moves = value;
    }

    /// <summary>
    /// The clock a conversation's waits between tries of a transient failure are read off (<c>.claude/knowledge/held-clocks.md</c>): the
    /// machine's, and a held one in a test that passes each wait.
    /// </summary>
    public TimeProvider RetryClock { get; init; } = TimeProvider.System;

    /// <summary>
    /// <i>Stop waiting</i>: an ended conversation held by its limit waits no more. Its waiting words are marked as words that cannot go on by
    /// themselves are, Daoris's limit word taken off by its id, and its conversation says so, which leaves the limits stage's wait, so nothing
    /// goes on by itself; the person's next word goes on as any word to an ended conversation does. A live one ends by the person's
    /// <i>Stop</i>, as any does.
    /// </summary>
    /// <returns>Null where it stopped waiting; else why not.</returns>
    public async Task<string?> StopWaitingAsync(string sessionId, CancellationToken ct = default)
    {
        if (_context.Holds(sessionId)) return $"conversation `{sessionId}` runs here: stop it to end its wait.";
        // Held by the record's flag, or by its events where an older build ended it.
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
    /// <i>Go on with Daoris's brief</i> (<see cref="BriefedMove.MoveAsync"/>): a conversation, or a closed quest's session, whose words wait
    /// for the person goes on in a new part of its session, in its tree on its agent and account, handed Daoris's brief and then their words.
    /// Beside <i>Start a conversation with these words</i>, which stays the person's fresh start.
    /// </summary>
    /// <param name="onEnded">Told when the new part ends, as a start's is.</param>
    public Task<StartedFrom> GoOnBriefedAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        Executors.Of<BriefedMove>().MoveAsync(sessionId, lost: null, HandOverBy.Person, config, onEnded, ct);

    /// <summary>
    /// A closed quest's session whose own conversation is proven lost goes on by itself as a conversation in its tree (a closed quest opens
    /// no driven record), handed Daoris's brief and the person's words; the driver hands it here through its seam, since conversations are this
    /// runner's.
    /// </summary>
    /// <remarks>
    /// Where the reason is <see cref="ContinueWhy.Account"/>, its own conversation is not lost but on an account that cools while another of
    /// its agent's is ready, so the ended limit move's body opens it there; the brief's move opens on the account the record ran on, which is
    /// cooling.
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
    /// The ended chats whose words wait from while no runner heard them are taken up, as the shell comes up (<see cref="ChatGoOn.TakeUpAsync"/>).
    /// </summary>
    public Task<IReadOnlyList<ChatStart>> TakeUpAsync(CancellationToken ct = default) => GoOn.TakeUpAsync(ct);

    /// <summary>Go on with an ended chat the person wrote to (<see cref="ChatGoOn.GoOnAsync"/>).</summary>
    public Task<ChatStart> GoOnAsync(
        string sessionId, DriverConfig? config = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        GoOn.GoOnAsync(sessionId, config, onEnded, ct);

    /// <summary>
    /// The person let an answer the answers-alone bound held go on (<see cref="ChatGoOn.AnswerGoesOn"/>): said as an open conversation's next
    /// turn, or gone on with where it ended.
    /// </summary>
    public void AnswerGoesOn(string sessionId) => GoOn.AnswerGoesOn(sessionId);

    /// <summary>
    /// A conversation this runner took up by itself ended: the session and the state its record took, as a start's <c>onEnded</c> is told. No
    /// route started it, so the shell announces it as the page's <c>SESSION_ENDED</c>.
    /// </summary>
    public event Action<string, string>? TakenUpEnded
    {
        add => GoOn.TakenUpEnded += value;
        remove => GoOn.TakenUpEnded -= value;
    }

    /// <summary>
    /// A hand-over's or a curve move's take is over, its box gone (<see cref="ChatHandOver.TakeEnded"/>): the shell's judge hands the words it
    /// held meanwhile to the conversation that continues it.
    /// </summary>
    public event Action<string>? TakeEnded
    {
        add => Executors.Of<ChatHandOver>().TakeEnded += value;
        remove => Executors.Of<ChatHandOver>().TakeEnded -= value;
    }

    /// <summary>
    /// The machine as Ask Daoris's room says it (<see cref="ChatHelp.DescribeHelp"/>): the shell's reading, which a conversation going on
    /// finds the room written from. Null where nothing reads it.
    /// </summary>
    public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp
    {
        get => Executors.Of<ChatHelp>().DescribeHelp;
        set => Executors.Of<ChatHelp>().DescribeHelp = value;
    }

    /// <summary>How long an Ask Daoris start waits for the machine's reading before the room as last written serves (<see cref="HelpStart.ReadBound"/>).</summary>
    internal TimeSpan HelpReadBound
    {
        get => Executors.Of<HelpStart>().ReadBound;
        set => Executors.Of<HelpStart>().ReadBound = value;
    }

    /// <summary>Place a new Ask Daoris conversation from an earlier one's words, its room read and agent spawned after (<see cref="ChatHelp.PlaceHelpFromAsync"/>).</summary>
    /// <param name="workspace">The workspace the person's window was on at the press, as <see cref="PlaceHelpAsync"/> takes it.</param>
    public Task<HelpPlaced> PlaceHelpFromAsync(
        string earlier, string adapter, DriverConfig config, string? workspace = null, CancellationToken ct = default,
        IReadOnlyList<ActorEffective>? order = null) =>
        Executors.Of<ChatHelp>().PlaceHelpFromAsync(earlier, adapter, config, workspace, ct, order);

    /// <summary>Start a new Ask Daoris conversation from an earlier one's words (<see cref="ChatHelp.StartHelpFromAsync"/>).</summary>
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
    /// <i>Hand a conversation over</i>: judged and kept in its box, then taken at once, or as its turn ends where it is talking
    /// (<see cref="ChatHandOver.HandOverAsync"/>).
    /// </summary>
    /// <param name="why">One of <see cref="HandOverWhy"/>.</param>
    /// <param name="askedBy">One of <see cref="HandOverBy"/>.</param>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    public Task<HandedOver> HandOverAsync(
        string sessionId, string agent, string? account, string why, string askedBy, DriverConfig config,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default, string? model = null, string? effort = null) =>
        Executors.Of<ChatHandOver>().HandOverAsync(sessionId, agent, account, why, askedBy, config, onEnded, ct, model, effort);

    /// <summary>Judge a conversation by its account's usage curve and, where the decision says so, move it (<see cref="CurveMove.MoveAsync"/>): null where nothing was judged.</summary>
    internal Task<SwitchChoice?> MoveByCurveAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default) =>
        Curve.MoveAsync(sessionId, config, onEnded, ct);

    /// <summary>The curve's move, whose <see cref="CurveMove.Decided"/> a test hears: the runner names no course type.</summary>
    internal CurveMove Curve => Executors.Of<CurveMove>();

    /// <summary>The briefed move, whose <see cref="BriefedMove.Decided"/> a test hears: the runner names no course type.</summary>
    internal BriefedMove Briefed => Executors.Of<BriefedMove>();

    /// <summary>The conversation's go-on, whose <see cref="ChatGoOn.Decided"/> a test hears: the runner names no course type.</summary>
    internal ChatGoOn GoOn => Executors.Of<ChatGoOn>();

    /// <summary>The helper's words, whose <see cref="ChatHelperWords.Decided"/> a test hears.</summary>
    internal ChatHelperWords HelperWords => Executors.Of<ChatHelperWords>();

    /// <summary>The limit's wake, whose <see cref="LimitWaits.Decided"/> a test hears: the runner names no course type.</summary>
    internal LimitWaits Waits => Executors.Of<LimitWaits>();

    /// <summary>What each judged turn's end came to, by session: a test hears it; no person reads it.</summary>
    internal event Action<string, SwitchChoice>? CurveJudged
    {
        add => Curve.Judged += value;
        remove => Curve.Judged -= value;
    }

    /// <summary>
    /// Where a conversation's turns stand, whether one is in flight and what the person sent that is not at the harness yet, each time that
    /// changes. The page shows the waiting messages as queued, since they are in no record until they are sent, and offers a stop while a
    /// turn runs.
    /// </summary>
    public event Action<string, ChatQueue>? QueueChanged;

    /// <summary>
    /// A conversation answered the person: the turn their words started ended, judged by <see cref="AttentionWatch"/> as its turns move. Told
    /// from the conversation's thread; the shell says it as it says the tick's attention.
    /// </summary>
    public event Action<AttentionEvent>? Answered;

    /// <summary>
    /// A conversation's model and effort, as its agent offers them, each time they change: as its session opened, after a change, and when
    /// the agent changed them itself. Only the options that are the person's (<see cref="AcpConfigOption.ThePersons"/>).
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

    /// <summary><see cref="ClosedNote"/> with its code.</summary>
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

    /// <inheritdoc cref="ChatRun.RunAsync"/>
    internal Task<ChatStart> RunAsync(
        string actor, string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
        ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn = null, ActorEffective? chosen = null,
        ChatPending? pending = null) =>
        _run.RunAsync(actor, sessionId, message, resolved, selection, config, place, onEnded, ct, goOn, chosen, pending);

    /// <summary>A conversation's start refused at its open moved on: the refused record and the replacement that took its words (<see cref="ChatReplacement.Replaced"/>).</summary>
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

    // The chat runner's port (the course's IChatRunning): the operations the course's executors call, forwarded as they are. The public ones
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
