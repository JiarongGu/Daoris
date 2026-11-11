using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Contracts.Parts;
using static Daoris.Driver.AcpProtocol;

namespace Daoris.Driver;

/// <summary>
/// One session held over the Agent Client Protocol: JSON-RPC 2.0 in newline-delimited frames, over a spawned process's stdio. This
/// class is the root that wires the door's units: the reader and its turn's stages, the open, the run, the prompts and their state,
/// the waits and the episodes each live in a unit of their own, reached through the wiring interfaces it implements.
/// </summary>
/// <remarks>
/// <para><b>Why a protocol door at all.</b> The pipe door gives the driver a wall of text and an exit code. This one gives tool
/// boundaries with ids, inputs and outcomes, turn boundaries, thoughts and context pressure, by contract, with nothing parsed out of
/// another program's stdout.</para>
///
/// <para><b>It takes streams, not a process.</b> Spawning stays the driver's, because observing a process's lifetime is its half of
/// the contract, and a class that owned a process could not be tested without one: every rule here is provable against two in-memory
/// streams and a fake agent.</para>
///
/// <para><b>Permission requests are refused</b> (<see cref="AcpPermission"/>), except where the posture says the person answers them,
/// and each is then held for the person (<see cref="Permissions"/>).</para>
/// </remarks>
/// <param name="closeTimeout">
/// How long to wait for the agent to acknowledge <c>session/close</c>: the turn is over by then, so an agent that wedges after
/// answering must not hang a run that has finished.
/// </param>
/// <param name="posture">
/// The mode id that expresses the posture on this agent's wire, or null where the agent carries none there. 🔴 It belongs to the
/// adapter, never to this class: the harnesses name the same boundary differently or not at all, and a constant would drive the others
/// at whatever mode they started in, silently. Null asks for nothing, which leaves the agent at its own default, the safe direction.
/// </param>
/// <param name="meta">What <c>session/new</c> carries in <c>_meta</c>, the adapter's own vocabulary for the rules composed; null sends none.</param>
/// <param name="onEvent">Where the wire's structure goes: every update as a <see cref="SessionEvent"/>, beside the console's line.</param>
/// <param name="quiet">How long the agent's words may go quiet before the console shows the line they left open (<see cref="AcpConsole"/>).</param>
/// <param name="streams">
/// Where what the session runs beside itself is kept, each subagent and background task a stream of its own (<see cref="AcpStreams"/>);
/// null asks the agent for none, and every update is the session's.
/// </param>
/// <param name="onOptions">Told the session's config options each time they change: as offered, as a change's answer, as the agent's own update.</param>
/// <param name="onConversation">Told the id the open's answer names the moment it arrives, before the first turn can fail or park.</param>
/// <param name="wakeQuiet">
/// How long a session whose own background work ended waits for its harness to wake its agent before Daoris's own go-on goes
/// (<see cref="AcpWake"/>).
/// </param>
public sealed class AcpSession(
    TextReader incoming,
    TextWriter outgoing,
    Action<string> onLine,
    TimeSpan? closeTimeout = null,
    string? posture = null,
    object? meta = null,
    Action<SessionEvent>? onEvent = null,
    TimeSpan? quiet = null,
    ISessionStreams? streams = null,
    Action<IReadOnlyList<AcpConfigOption>>? onOptions = null,
    Action<string>? onConversation = null,
    TimeSpan? wakeQuiet = null) : ITurnRunWiring
{
    /// <summary>The protocol version this client speaks. Stated, never negotiated downward silently.</summary>
    private const int ProtocolVersion = 1;

    /// <summary>
    /// What the open says where the agent does not offer a required posture, given the mode asked and what the agent offers
    /// (<see cref="DrivenAccess.Unoffered"/>). Null says the full access's sentence.
    /// </summary>
    public Func<string, string, string>? Unoffered { get; set; }

    public Func<ConnectorPlan?, IReadOnlyList<ServerEntry>>? OwnServers { get; set; }

    /// <summary>Where each child the streams beside it announce and end is said, as its reading; null says none.</summary>
    public IReadingSink? Children { get; init; }

    public Action<string, SessionEvent>? ChildCalls { get; init; }

    /// <summary>
    /// Where this session's permission requests are held for the person (<see cref="HeldPermissions"/>): only where its posture is
    /// <see cref="PostureAsked.PersonAnswers"/>. Null, or a posture without the mark, refuses each as it comes.
    /// </summary>
    public PermissionHolds? Permissions { get; set; }

    /// <summary>
    /// Asked at a turn's end before a held word opens a turn of Daoris's: whether the setup the session opened under changed since. Yes
    /// ends the run there, never mid-turn: every held word is handed back unprompted (<see cref="HandedBack"/>), none prompted into a
    /// process opened under the old posture. A word already sent is the agent's and is waited for. Null never asks.
    /// </summary>
    public Func<bool>? SetupChanged { get; set; }

    public Func<string, bool>? LimitSaid { get; set; }

    /// <summary>
    /// Whether this client asks the agent for typed session failures: set for an agent whose toolchain declares it marks a terminal
    /// failure for such a client alone (<see cref="ErrorWords.TypedFailures"/>).
    /// </summary>
    public bool TypedFailures { get; init; }

    /// <summary>
    /// Asked at a turn's end what the quest and its tree say, so a turn that ended with the quest done and the tree uncommitted is carried
    /// on rather than the session's end (<see cref="AcpWaits.CarryOnAsync"/>). Null never carries on.
    /// </summary>
    public Func<CancellationToken, Task<TurnCarry>>? CarriesOn
    {
        get => Ends.CarriesOn;
        set => Ends.CarriesOn = value;
    }

    private AcpWaits Ends => LazyInitializer.EnsureInitialized(ref _ends, () => new AcpWaits(this));
    private AcpWaits? _ends;

    /// <summary>
    /// The words the run handed back unprompted, in the order said, for the driver to keep on their record: those held as its setup
    /// changed or as it left a silent turn of a closed quest, then every word it had been asked to send and never wrote, turned away at its
    /// door as it ended (<see cref="AcpTurnedAway"/>).
    /// </summary>
    public IReadOnlyList<IAgentWord> HandedBack => _open.HandedBack;

    /// <summary>
    /// How often a session that waits on its own background work reads its quest again: once it no longer waits on that work, the session
    /// ends. The driver sets every fourth of its own ticks; this default is that at its default tick.
    /// </summary>
    public TimeSpan BackgroundLook { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long a session whose quest no longer waits lets a turn run silent before it ends anyway; a turn that speaks is never cut. Its
    /// end is not always said: a wakeup the agent scheduled for itself starts a turn whose result carries no origin.
    /// </summary>
    public TimeSpan QuestDoneQuiet { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The clock the session's own waits are read off: the machine's in production, one a test moves. A transient episode's waits are its
    /// retry's (<see cref="TransientRetry.Clock"/>).
    /// </summary>
    public TimeProvider Clock
    {
        get => _wake.Clock;
        init => _wake.Clock = value;
    }

    /// <summary>
    /// The session's own background work still running, by name, when its run ended: the driver gives an agent that leaves such work a
    /// bound to exit before it ends the agent's tree. Empty until the run ends.
    /// </summary>
    public IReadOnlyList<string> LeftRunning { get; private set; } = [];

    private readonly bool _personAnswers = PostureAsked.Parse(posture).PersonAnswers;

    private readonly TimeSpan _closeTimeout = closeTimeout ?? TimeSpan.FromSeconds(5);

    /// <summary>The prompts on the wire and the run's door they pass: every lock of that state is its own.</summary>
    private readonly OpenPrompts _open = new();

    private bool _takesWordsMidTurn;

    private bool _steersNow;

    private readonly StepCalls _calls = new();

    private IAgentInbox? _inbox;

    /// <summary>A conversation's words, which a transient episode holds where no run hands an inbox; set once its session is open.</summary>
    private IEpisodeWords? _episodeWords;

    private IEpisodeWords? EpisodeWords => _episodeWords ?? _inbox;

    private int _updates;
    private string? _sessionId;

    /// <summary>The reader, which lives as long as the conversation does, and what ends it.</summary>
    private readonly CancellationTokenSource _pumpStop = new();
    private Task? _pump;

    private readonly AcpConsole _console = new(onLine, quiet ?? AcpConsole.Quiet);

    private AcpStreams? _beside;

    /// <summary>The background work its harness says it runs, read whether or not its streams are kept.</summary>
    private readonly AcpBackground _background = new();

    /// <summary>The background work its harness says still runs, with when each began (<see cref="LongCallLook"/>).</summary>
    public IReadOnlyList<RunningTask> BackgroundRunning => _background.RunningSince;

    /// <summary>Whether no turn runs and the session waits on its own background work.</summary>
    public bool WaitsOnOwnWork => Ends.WaitsOnOwnWork;

    private readonly AcpWake _wake = new();

    private readonly TimeSpan _wakeQuiet = wakeQuiet ?? AcpWake.Quiet;

    /// <summary>
    /// Why the process is being ended from outside (<see cref="Ending"/>), and whether the stream is over, after which nothing said
    /// changes how it ended. Under the lock.
    /// </summary>
    private readonly object _endGate = new();
    private Noted? _ending;
    private bool _streamOver;

    /// <summary>
    /// Told why the agent's process is about to be ended from outside the session, before its kill: the stream's end that follows is then
    /// that cause's (<see cref="AcpEnded"/>), never the agent having exited. Said once; a stream already over keeps its own diagnosis.
    /// </summary>
    public void Ending(Noted why)
    {
        lock (_endGate)
        {
            if (!_streamOver) _ending ??= why;
        }
    }

    /// <summary>The stream's end as a failure: its cause's where one is said before it (<see cref="Ending"/>), else <paramref name="sentence"/>.</summary>
    private DriverException StreamEnded(string sentence)
    {
        Noted? why;
        lock (_endGate) why = _ending;
        return why is not null ? new AcpEnded(why) : new DriverException(sentence);
    }

    /// <summary>
    /// The largest context reading this session has reported, or null: what a conversation, which never runs <see cref="RunAsync"/>,
    /// counts toward its account.
    /// </summary>
    public AcpUsage? Usage => TurnStages.Of<AcpMeasure>().Usage;

    /// <summary>
    /// Told what a <c>usage_update</c> says about the account's windows, as the adapter's meta carries them, for the agent's table to
    /// read. Raised on the reader's thread; a listener's failure is its own.
    /// </summary>
    public event Action<JsonElement>? LimitsSaid;

    private AcpOptions Options => LazyInitializer.EnsureInitialized(ref _optionsKept, () => new AcpOptions(
        onLine, onOptions, (method, args, ct) => RequestAsync(method, args, ct), () => _sessionId));
    private AcpOptions? _optionsKept;

    /// <summary>The session's config options as the agent last said them, the mode included; which a person may set is the caller's to judge (<see cref="AcpConfigOption.ThePersons"/>).</summary>
    public IReadOnlyList<AcpConfigOption> ConfigOptions => Options.Current;

    private void KeepOptions(IReadOnlyList<AcpConfigOption> options) => Options.Keep(options);

    /// <summary>Change one of the session's config options mid-conversation: the agent's whole list after it is kept and returned.</summary>
    public Task<IReadOnlyList<AcpConfigOption>> SetConfigOptionAsync(string configId, string value, CancellationToken ct) =>
        Options.SetAsync(configId, value, ct);

    /// <summary>The list the last setter returned; null when it returned none.</summary>
    public IReadOnlyList<AcpConfigOption>? LastAcknowledgedOptions => Options.LastAcknowledged;

    /// <summary>What this start's actor asked, set on the open session by category before the posture and the first prompt.</summary>
    public ActorAsked? Carry
    {
        get => Options.Carry;
        set => Options.Carry = value;
    }

    /// <summary>Told what is carried where it differs from <see cref="Carry"/>.</summary>
    public Action<ActorAsked>? Carried
    {
        get => Options.Carried;
        set => Options.Carried = value;
    }

    /// <summary>Asked where the agent refuses <see cref="Carry"/>'s model at the open: a later choice for this session, or null to end it (<see cref="AcpChoiceNotOffered"/>).</summary>
    public IChoiceReask? ModelRefused
    {
        get => Options.ModelRefused;
        set => Options.ModelRefused = value;
    }

    /// <summary>Told the options the open listed, before the posture or any set.</summary>
    public Action<IReadOnlyList<AcpConfigOption>>? OffersRead
    {
        get => Options.OffersRead;
        set => Options.OffersRead = value;
    }

    /// <summary>The options as the open listed them, never overwritten.</summary>
    public IReadOnlyList<AcpConfigOption> OpeningOptions => Options.OpeningOptions;

    /// <summary>The refusal that ended this session's open before any prompt, or null.</summary>
    public ModelRefusal? NotOffered => Options.NotOffered;

    private void KeepOpening() => Options.KeepOpening();

    private Task<bool> CarryAsync(CancellationToken ct) => Options.CarryAsync(ct);

    /// <summary>A driven session's whole life on this wire (<see cref="AcpRun"/>): the open, the target as a prompt, and the person's words after it.</summary>
    /// <param name="cwd">The working tree this session runs in.</param>
    /// <param name="prompt">The composed target, exactly as the pipe door delivers it.</param>
    /// <param name="plan">The connectors this session is handed: the servers that let the composed target's tools be called at all.</param>
    /// <param name="inbox">
    /// What the person tells the session while it works: each word sent at once where the agent takes a prompt during a turn, else the
    /// next prompt at the turn's end. Null for a session nobody may tell anything.
    /// </param>
    /// <param name="asked">Told each of the person's words as the session takes them, so the record keeps it as the person's ask there.</param>
    /// <param name="resume">The harness conversation an answer continues, resumed with <paramref name="prompt"/> as its next turn; null opens a new session.</param>
    /// <param name="blocks">The first prompt as text blocks, one each, in place of <paramref name="prompt"/>; null or empty sends <paramref name="prompt"/>.</param>
    /// <param name="prompted">Told once the first prompt is on the wire: the words it carries were taken, where a refusal before it took nothing.</param>
    /// <param name="files">The files said with <paramref name="blocks"/>' words, each a <c>resource_link</c> after the blocks.</param>
    /// <param name="waitsOnBackground">
    /// Asked when a turn ends while the session's own background work runs: whether its quest stands as a park would leave it. Yes keeps
    /// the session open until that work ends, then goes on with a turn that says so; null never waits.
    /// </param>
    /// <exception cref="AcpResumeRefused">The agent offers no resume, or refused this one: nothing is prompted.</exception>
    /// <exception cref="DriverException">The agent's stream ended while the session waited for its background work.</exception>
    public Task<AcpOutcome> RunAsync(
        string cwd, string prompt, CancellationToken ct, ConnectorPlan? plan = null,
        IAgentInbox? inbox = null, Action<IAgentWord>? asked = null, string? resume = null,
        IReadOnlyList<string>? blocks = null, Action? prompted = null, IReadOnlyList<KeptFile>? files = null,
        Func<CancellationToken, Task<bool>>? waitsOnBackground = null,
        Func<Task<(IReadOnlyList<string> Blocks, IReadOnlyList<KeptFile>? Files)?>>? attempting = null, TransientRetry? retry = null) =>
        Run.RunAsync(cwd, prompt, ct, plan, inbox, asked, resume, blocks, prompted, files, waitsOnBackground, attempting, retry);

    private AcpRun Run => LazyInitializer.EnsureInitialized(ref _run, () => new AcpRun(this, Prompting, Steering, Ends));
    private AcpRun? _run;

    private AcpPrompting Prompting => LazyInitializer.EnsureInitialized(ref _prompting, () => new AcpPrompting(this));
    private AcpPrompting? _prompting;

    private AcpSteering Steering => LazyInitializer.EnsureInitialized(ref _steering, () => new AcpSteering(this, Prompting));
    private AcpSteering? _steering;

    private AcpTry Try => LazyInitializer.EnsureInitialized(ref _try, () => new AcpTry(this, Prompting));
    private AcpTry? _try;

    // The run's own token and whether a run holds the session: only a run's stop puts a cancel on the wire.
    private CancellationToken _stops;
    private bool _inRun;

    /// <summary>The transient retry a run or a conversation is handed, or null, and a failure then ends as it came.</summary>
    private TransientRetry? _retry;

    /// <summary>
    /// What the run's first prompt's write return tells, told again as each try's write returns: a carry-on's moved words are sent at the
    /// first write that returns, whichever prompt it is, and the caller tells it once.
    /// </summary>
    private Action? _prompted;

    private AcpEpisodes Episodes => LazyInitializer.EnsureInitialized(ref _episodes, () => new AcpEpisodes(this));
    private AcpEpisodes? _episodes;

    /// <summary>Whether the run waits between a transient episode's tries, on Daoris's own timer and never on its agent.</summary>
    public bool WaitsOnTransient => Episodes.WaitsOnTransient;

    private Task<string> PromptingAsync(Func<Task<string>> prompt, CancellationToken ct) => Episodes.PromptingAsync(prompt, ct);

    private Task<JsonElement> OpeningAsync(Func<Task<JsonElement>> open, CancellationToken ct) => Episodes.OpeningAsync(open, ct);

    TransientRetry? ITurnRetryWiring.Retry => _retry;

    IEpisodeWords? ITurnRetryWiring.EpisodeWords => EpisodeWords;

    List<IAgentWord> ITurnRetryWiring.TakeUnacknowledged() => _open.TakeUnacknowledged();

    void ITurnRetryWiring.ForgetUnacknowledged() => _open.ForgetUnacknowledged();

    Task<string> ITurnRetryWiring.TryAsync(TransientEpisode episode, CancellationToken ct) => Try.TryAsync(episode, ct);

    /// <summary>
    /// The person's words this run sent and the session never took, once the run has ended: on the wire behind a turn that ended first,
    /// a failed one included. The driver keeps them waiting; empty until then.
    /// </summary>
    public IReadOnlyList<IAgentWord> Unreached => _open.Unreached;

    /// <summary>End what the session runs beside itself, saying so. Idempotent, and never throws.</summary>
    private async Task EndStreamsAsync()
    {
        if (_beside is null) return;
        try
        {
            await _beside.EndAllAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A sink that refuses the endings must not turn a finished run into a failed one, or leave the requests awaited for the timeout.
            onLine($"[ending what ran beside the session failed: {error.Message}]");
        }
    }

    /// <summary>Open the session a conversation lives in: everything before the first prompt, done once (<see cref="AcpOpening"/>).</summary>
    /// <remarks>
    /// The reader runs until the agent's stream ends or <see cref="Release"/>, never until <paramref name="ct"/>, which bounds only the
    /// opening: a conversation outlives the request that started it, and every later turn's updates arrive on this same reader.
    /// </remarks>
    public Task OpenAsync(string cwd, CancellationToken ct, ConnectorPlan? plan = null) =>
        Opening.OpenAsync(cwd, ct, plan);

    private AcpOpening Opening => LazyInitializer.EnsureInitialized(ref _opening, () => new AcpOpening(
        meta, Options, HandshakeAsync, OpeningAsync, (method, args, ct) => RequestAsync(method, args, ct), SetPostureAsync,
        id => _sessionId = id, replaying => _replaying = replaying, Told, plan => OwnServers?.Invoke(plan) ?? []));
    private AcpOpening? _opening;

    /// <summary>The reader started and the handshake answered: what every session asks before it is one. The agent's answer.</summary>
    private async Task<JsonElement> HandshakeAsync(CancellationToken ct)
    {
        if (_pump is not null) throw new DriverException("this ACP session is already open.");
        if (streams is not null) _beside = new AcpStreams(streams, _console, Emit, quiet ?? AcpConsole.Quiet, children: Children, calls: ChildCalls);
        _pump = Reader.PumpAsync(incoming, _pumpStop.Token);

        var initialized = await RequestAsync(
            "initialize",
            new
            {
                protocolVersion = ProtocolVersion,
                clientCapabilities = Capabilities(streams is not null, TypedFailures),
                clientInfo = new { name = "daoris-driver", version = "0" },
            },
            ct).ConfigureAwait(false);
        _takesWordsMidTurn = TakesWordsMidTurn(initialized);
        _steersNow = SteersNow(initialized);
        _wake.EndsBySilence = !SaysOrigins(initialized);
        return initialized;
    }

    /// <summary>One turn: the text as a prompt on the open session, answered by the agent's stop reason, the record's turn event's word.</summary>
    /// <remarks>
    /// One at a time for a conversation, whose caller queues the person's messages rather than send one into a running turn; a driven
    /// session sends one during a turn only where the agent says it takes one.
    /// </remarks>
    /// <param name="sent">
    /// Told once the prompt is on the wire: a stop asked before that moment must wait for it, or its <c>session/cancel</c> overtakes the
    /// prompt it meant to stop and stops nothing.
    /// </param>
    /// <param name="files">What the person attached, each a <c>resource_link</c>: the protocol's baseline block, which every agent takes.</param>
    public Task<string> PromptAsync(
        string text, CancellationToken ct, Action? sent = null, IReadOnlyList<KeptFile>? files = null) =>
        Prompting.SendPromptAsync(text, files, ct, sent, words: null);

    /// <summary>One turn of one or more text blocks, each its own: a conversation resumed with the person's words waiting on its record.</summary>
    /// <param name="sent">Told once the prompt is on the wire: the words it carries went to the session.</param>
    /// <param name="files">The files said with the words, each a <c>resource_link</c> after the blocks.</param>
    public Task<string> PromptAsync(
        IReadOnlyList<string> blocks, CancellationToken ct, Action? sent = null, IReadOnlyList<KeptFile>? files = null) =>
        Prompting.SendPromptAsync(blocks, files, ct, sent, words: null);

    /// <summary>Whether the agent said, as the session opened, that it takes a prompt during a turn: a conversation's words then go at once.</summary>
    public bool NextStep => _takesWordsMidTurn;

    /// <summary>
    /// Told each of the person's words as the session takes them: a conversation's, whose words said during a turn are taken when the
    /// turn before them is handed off, so the record keeps them there as the person's ask.
    /// </summary>
    public void OnTaken(Action<IAgentWord> taken) => Prompting.OnAsked = taken;

    /// <summary>
    /// The person's words to a conversation whose turn is running, sent at once as a prompt of the same session: taken at the agent's
    /// next step, told through <see cref="OnTaken"/>, and answered by their own stop reason at the turn's real end.
    /// </summary>
    /// <exception cref="DriverException">
    /// The agent did not say it takes a prompt during a turn: a second prompt could reset its running turn, so nothing is sent.
    /// </exception>
    public Task<string> SayAsync(IAgentWord words, CancellationToken ct) =>
        _takesWordsMidTurn
            ? Prompting.MidTurnAsync(words, ct)
            : throw new DriverException("this agent did not say it takes words during a turn — they wait for the turn to end.");

    /// <summary>
    /// A conversation's session is handed its transient retry once it is open: a failure at the open ends as it came, and the
    /// conversation's turns are the words an episode holds, waits on and carries.
    /// </summary>
    internal void Retries(TransientRetry retry, IEpisodeWords words)
    {
        _retry = retry;
        _episodeWords = words;
    }

    /// <summary>
    /// One turn's prompt of a conversation, read for a transient failure: its stop reason, or that of the try that answered. Any other
    /// failure is thrown as it came, and so is one past the bound (<see cref="AcpTransientExhausted"/>).
    /// </summary>
    internal Task<string> RetriedAsync(Func<Task<string>> prompt, CancellationToken ct) => PromptingAsync(prompt, ct);

    /// <summary>
    /// A word handed to a running turn whose own prompt is refused after the word is taken (<see cref="AcpWordTurnFailed"/>): the turn
    /// that failed is the one it opened, so its refusal is read as any prompt's, a transient one opening the episode.
    /// </summary>
    internal Task<string> RecoveredAsync(AcpRefusal refusal, CancellationToken ct) =>
        PromptingAsync(() => Task.FromException<string>(refusal), ct);

    /// <summary>Whether the person's *Send now* steers into this session's running turn (<see cref="SteersNow"/>).</summary>
    public bool Steers => _steersNow;

    /// <summary>The person's *Send now*, steered into the running turn (<see cref="AcpSteering.SteerAsync"/>).</summary>
    /// <returns>The adapter's outcome: <c>injected</c>, or <c>promptRequired</c> where no turn is running after all.</returns>
    /// <exception cref="AcpRefusal">The agent refused the steer, in its words.</exception>
    /// <exception cref="DriverException">The agent never said it steers, or the session is not open: nothing is sent.</exception>
    public Task<string> SteerAsync(IAgentWord words, CancellationToken ct) => Steering.SteerAsync(words, ct);

    /// <summary>A steer answered, costing only its words where it did not land (<see cref="AcpSteering.SteeredAsync"/>): never thrown.</summary>
    internal Task<string> SteeredAsync(
        IAgentWord words, CancellationToken ct, string who = "session", Func<IAgentWord, bool>? handBack = null) =>
        Steering.SteeredAsync(words, ct, who, handBack);

    /// <summary>
    /// Stop the turn in flight and keep the session: the agent winds its work up and answers the prompt with its own stop reason. A
    /// notification, so there is nothing to wait for here.
    /// </summary>
    /// <remarks>
    /// A permission request held for the person is answered <c>cancelled</c> after the cancel goes, as the protocol asks of a client that
    /// cancels: the turn it is asked in is stopping, and nothing the person says now could land in it.
    /// </remarks>
    public async Task CancelTurnAsync()
    {
        if (_sessionId is null) return;
        Prompting.StopAsked();
        try
        {
            await NotifyAsync("session/cancel", new { sessionId = _sessionId }).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            // An agent that has exited has nothing left to stop: its stdin is the closed thing.
        }
        finally
        {
            await TurnStages.Of<AcpPermission>().ReleaseHeldAsync(PermissionWhy.Cancelled).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stop one piece of background work the session started, by the adapter's own request: its stream ends when the wire says how, as
    /// every task's does. False where the session is not open, or the adapter stopped nothing; a refusal is its own words.
    /// </summary>
    public async Task<bool> StopTaskAsync(string asyncTaskId, CancellationToken ct)
    {
        if (_sessionId is null) return false;
        JsonElement answer;
        try
        {
            answer = await RequestAsync("_session/async_task/stop", new { sessionId = _sessionId, asyncTaskId }, ct)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or (DriverException and not AcpRefusal))
        {
            // An agent that has exited stopped nothing, whether its stream ended with the stop pending or the write found it closed.
            return false;
        }

        return answer.ValueKind == JsonValueKind.Object
            && answer.TryGetProperty("stopped", out var stopped)
            && stopped.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Close the session politely so the agent can flush and persist; its exit is still what the driver observes. Bounded, because an
    /// agent that stops answering after its last turn would otherwise hang a run that is already finished.
    /// </summary>
    public async Task CloseAsync(CancellationToken ct)
    {
        if (_sessionId is null) return;

        // Nothing held for the person outlives the session it is asked in: answered before the close goes.
        await TurnStages.Of<AcpPermission>().ReleaseHeldAsync(PermissionWhy.Ended).ConfigureAwait(false);

        using var closing = CancellationTokenSource.CreateLinkedTokenSource(ct);
        closing.CancelAfter(_closeTimeout);
        try
        {
            await RequestAsync("session/close", new { sessionId = _sessionId }, closing.Token)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Best-effort by construction: the conversation is over either way, and a closed stdin is an agent already gone.
        }
    }

    /// <summary>Stop reading. Observed rather than awaited: an agent may never send another byte, and waiting would turn a stop into a hang.</summary>
    public void Release()
    {
        _pumpStop.Cancel();
        _ = _pump?.ContinueWith(static t => t.Exception, TaskScheduler.Default);
    }

    /// <summary>Completes when the agent's stream has ended and every line it sent has been handled.</summary>
    public Task Ended => _pump ?? Task.CompletedTask;

    private AcpReader Reader => LazyInitializer.EnsureInitialized(ref _reader, () => new AcpReader(this, Wire));
    private AcpReader? _reader;

    /// <summary>
    /// The turn's stages, registered by their modules (<see cref="TurnStageAttribute"/>), ordered once per process and built once per
    /// session with the session as their wiring.
    /// </summary>
    internal PartSet<ITurnStage> TurnStages =>
        LazyInitializer.EnsureInitialized(ref _turnStages, () => Parts.Build(TurnOrder.Value, type => (ITurnStage)Activator.CreateInstance(type, this)!));

    private PartSet<ITurnStage>? _turnStages;

    private static readonly Lazy<IReadOnlyList<PartDeclaration>> TurnOrder = new(() => Parts.Declared<TurnStageAttribute>(typeof(AcpSession).Assembly));

    PartSet<ITurnStage> ITurnReadWiring.Stages => TurnStages;

    void ITurnReadWiring.StreamIsOver()
    {
        lock (_endGate) _streamOver = true;
    }

    void ITurnWiring.Say(string line) => onLine(line);

    void ITurnWiring.Emit(SessionEvent e) => Emit(e);

    Task ITurnWiring.SendAsync(JsonNode frame) => SendAsync(frame);

    AcpConsole ITurnWiring.Console => _console;

    AcpWake ITurnWiring.Wake => _wake;

    string? ITurnWiring.SessionId => _sessionId;

    bool ITurnWiring.Replayed(JsonElement parameters) => Replayed(parameters);

    void ITurnWiring.Counted() => Interlocked.Increment(ref _updates);

    AcpBackground ITurnWiring.Background => _background;

    AcpStreams? ITurnWiring.Beside => _beside;

    void ITurnWiring.Heard() => Prompting.Heard();

    bool ITurnWiring.Prompted() => _open.Prompted;

    bool ITurnWiring.PersonAnswers => _personAnswers;

    bool ITurnWiring.StreamOver
    {
        get { lock (_endGate) return _streamOver; }
    }

    RefusedCalls ITurnWiring.Refused => _refused;

    void ITurnWiring.Track(JsonElement update, SessionEvent? mapped) => _calls.Track(update, mapped);

    void ITurnWiring.SayLimits(JsonElement limits) => LimitsSaid?.Invoke(limits);

    void ITurnWiring.KeepOptions(IReadOnlyList<AcpConfigOption> options) => KeepOptions(options);

    void ITurnEndWiring.Note(Noted noted)
    {
        _console.End();
        onLine(noted.Note);
        Emit(new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts.Count > 0 ? noted.Parts : null,
        });
    }

    DriverException ITurnEndWiring.StreamEnded(string sentence) => StreamEnded(sentence);

    Task<string> ITurnEndWiring.PromptingAsync(Func<Task<string>> prompt, CancellationToken ct) => PromptingAsync(prompt, ct);

    Task<string> ITurnEndWiring.SendOwnAsync(string text, CancellationToken ct, Action? sent, Func<bool>? when) =>
        Prompting.SendPromptAsync(text, files: null, ct, sent, words: null, when);

    TimeSpan ITurnEndWiring.WakeQuiet => _wakeQuiet;

    bool ITurnEndWiring.DoorShut => _open.DoorShut;

    SemaphoreSlim ITurnEndWiring.WriteLock => Wire.WriteLock;

    /// <remarks>
    /// The inbox holds every word said from now, so none is dispatched to the session, before the door turns away what has not been
    /// written: called under the write lock.
    /// </remarks>
    void ITurnEndWiring.Shut()
    {
        _inbox?.Settling();
        _open.Shut();
    }

    void ITurnEndWiring.EndWokenTurn() => Prompting.EndWokenTurn();

    OpenPrompts ITurnPromptWiring.Open => _open;

    Task<JsonElement> ITurnPromptWiring.RequestAsync(
        string method, object parameters, CancellationToken ct, Action? sent, Action<JsonElement>? answered, Func<bool>? admit) =>
        RequestAsync(method, parameters, ct, sent, answered, admit);

    bool ITurnPromptWiring.Stopped => !_inRun || _stops.IsCancellationRequested;

    IAgentInbox? ITurnPromptWiring.Inbox => _inbox;

    Action? ITurnPromptWiring.OnPrompted => _prompted;

    AcpMeasure ITurnPromptWiring.Measure => TurnStages.Of<AcpMeasure>();

    StepCalls ITurnPromptWiring.Calls => _calls;

    void ITurnRunWiring.Begin(CancellationToken stops, Action<IAgentWord>? asked, IAgentInbox? inbox, TransientRetry? retry, Action? prompted)
    {
        _stops = stops;
        _inRun = true;
        Prompting.OnAsked = asked;
        _inbox = inbox;
        _retry = retry;
        _prompted = prompted;
    }

    Task ITurnReadWiring.EndStreamsAsync() => EndStreamsAsync();

    int ITurnRunWiring.Updates => _updates;

    void ITurnRunWiring.Left(IReadOnlyList<string> running) => LeftRunning = running;

    private readonly RefusedCalls _refused = new();

    /// <summary>An event to whoever keeps the record: one that fails costs a console line, never the turn, which the record does not run.</summary>
    private void Emit(SessionEvent e)
    {
        if (onEvent is null) return;
        try
        {
            onEvent(e);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            onLine($"[the conversation record could not keep an event: {error.Message}]");
        }
    }

    private AcpPosture Posture => LazyInitializer.EnsureInitialized(ref _posture, () => new AcpPosture(
        posture, () => Unoffered, (method, args, ct) => RequestAsync(method, args, ct), () => _sessionId));
    private AcpPosture? _posture;

    private Task SetPostureAsync(JsonElement created, CancellationToken ct, bool again = false) => Posture.SetAsync(created, ct, again);

    private AcpWire Wire => LazyInitializer.EnsureInitialized(ref _wire, () => new AcpWire(outgoing, onLine, () => TypedFailures));
    private AcpWire? _wire;

    private Task<JsonElement> RequestAsync(
        string method, object parameters, CancellationToken ct, Action? sent = null, Action<JsonElement>? answered = null,
        Func<bool>? admit = null) =>
        Wire.RequestAsync(method, parameters, ct, sent, answered, admit);

    private Task NotifyAsync(string method, object parameters) => Wire.NotifyAsync(method, parameters);

    private Task SendAsync(JsonNode frame, Func<bool>? admit = null) => Wire.SendAsync(frame, admit);

    /// <summary>The conversation whose replay is arriving while a <c>session/load</c> is answered, or null.</summary>
    private volatile string? _replaying;

    /// <summary>Resume a conversation instead of opening one: <c>session/resume</c> where the agent advertises it, else <c>session/load</c>.</summary>
    /// <exception cref="AcpResumeRefused">The agent offers neither, or refused this one.</exception>
    public Task ResumeAsync(string cwd, string conversation, CancellationToken ct, ConnectorPlan? plan = null) =>
        Opening.ResumeAsync(cwd, conversation, ct, plan);

    /// <summary>Whether an update is a load's replay of the conversation being resumed.</summary>
    private bool Replayed(JsonElement parameters) =>
        _replaying is { } replaying
        && parameters.TryGetProperty("sessionId", out var id) && id.ValueKind == JsonValueKind.String
        && string.Equals(id.GetString(), replaying, StringComparison.Ordinal);

    /// <summary>Tell whoever keeps it the id the agent named; a listener's failure costs a console line, never the session.</summary>
    private void Told(string conversation)
    {
        try
        {
            onConversation?.Invoke(conversation);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            onLine($"[the session's conversation id could not be kept: {error.Message}]");
        }
    }
}
