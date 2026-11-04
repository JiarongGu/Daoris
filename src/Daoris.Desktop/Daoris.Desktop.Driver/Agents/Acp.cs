using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Contracts.Parts;
using static Daoris.Driver.AcpProtocol;

namespace Daoris.Driver;

/// <summary>
/// One session held over the Agent Client Protocol (D53): JSON-RPC 2.0 in newline-delimited frames,
/// over a spawned process's stdio.
/// </summary>
/// <remarks>
/// <para><b>Why a protocol door at all.</b> The pipe door gives the driver a wall of text and an exit
/// code. This one gives tool boundaries with ids, inputs and outcomes, turn boundaries, thoughts and
/// context pressure — by contract, with nothing parsed out of another program's stdout, which is the
/// coupling D23/D24 exist to prevent and which D52 rejected by name. One wire reaches dsh natively
/// and claude-code and codex through the ACP project's adapters.</para>
///
/// <para><b>It takes streams, not a process.</b> Spawning stays the driver's (D46 §5: the driver owns
/// process lifetime, because observing it is its half of the contract), and a class that owned a
/// process could not be tested without one. Every rule below is provable against two in-memory
/// streams and a fake agent.</para>
///
/// <para><b>Permission requests are refused</b> — see <see cref="AnswerPermission"/> — except under a repository's asking
/// permission prompts, where each is held for the person to answer (AUTONOMY1b3a, <see cref="Permissions"/>).</para>
/// </remarks>
/// <param name="closeTimeout">
/// How long to wait for the agent to acknowledge `session/close`. The turn is already over by then,
/// so this is courtesy with a bound: an agent that wedges after answering the prompt must not be able
/// to hang a run that has finished. Found by a test that never returned.
/// </param>
/// <param name="posture">
/// The mode id that expresses D37 on THIS agent's wire, or null when the agent carries no posture
/// there (ACP3).
/// </param>
/// <remarks>
/// 🔴 <b>The posture belongs to the adapter, not to this class.</b> It was a constant here while one
/// harness rode the door, and that was wrong the moment a second arrived: Claude Code names the same
/// boundary <c>acceptEdits</c>, Codex names it <c>agent</c>, and dsh does not express it on the wire
/// at all. A constant would have driven two of the three at whatever mode they happened to start in,
/// silently. Null asks for nothing, which leaves the agent at its own default — the safe direction,
/// since every default observed is equal to or stricter than what Daoris would set.
/// </remarks>
/// <param name="meta">
/// What <c>session/new</c> carries in <c>_meta</c> — the adapter's own vocabulary for the rules Daoris
/// composed (PERM1, D72) — or null, and then no <c>_meta</c> is sent at all.
/// </param>
/// <param name="onEvent">
/// Where the wire's STRUCTURE goes (D76 §1, CONV1): every update as a <see cref="SessionEvent"/>, beside
/// the line the console gets. Null where nobody keeps the record.
/// </param>
/// <param name="quiet">
/// How long the agent's words may go quiet before the console shows the line they left open
/// (<see cref="AcpConsole"/>); <see cref="AcpConsole.Quiet"/> when not given.
/// </param>
/// <param name="streams">
/// Where what the session runs beside itself is kept (CONSOLE2): each subagent and each background
/// task as a console stream of its own (<see cref="AcpStreams"/>). Null asks the agent for none, and
/// every update is the session's, as before.
/// </param>
/// <param name="onOptions">
/// Told the session's config options each time they change (AGT6b, D98): as <c>session/new</c> offered
/// them, as an answer to a change, and as the agent's own <c>config_option_update</c>. Null where nobody
/// asks.
/// </param>
/// <param name="onConversation">
/// Told the id <c>session/new</c>'s answer names, the moment it arrives (ANSWER1a, D131 §1): what an answer
/// to a park resumes, kept before the first turn can fail or park. Null where nobody keeps it.
/// </param>
/// <param name="wakeQuiet">
/// How long a session whose turn waited on its own background work waits, once the work ended, for its harness to wake its
/// agent before Daoris's own go-on goes (BGWAIT2, <see cref="AcpWake"/>); <see cref="AcpWake.Quiet"/> when not given.
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
    /// What the open says where the agent does not offer a required posture, given the mode asked and what the agent offers, said: a
    /// driven start's repository's permission prompts (AUTONOMY1b, <see cref="DrivenAccess.Unoffered"/>). Null says Ask Daoris's full
    /// access, whose required posture came first (HELPMODE1a1).
    /// </summary>
    public Func<string, string, string>? Unoffered { get; set; }

    /// <summary>The part's lineage, told each child the streams beside it announce and end (COWORKVIEW1a); null tells none.</summary>
    public ILineageWriter? Lineage { get; init; }

    /// <summary>
    /// The mark a posture carries after <see cref="AgentPostures.Required"/> where the person answers its permission requests (AUTONOMY1b3a,
    /// D184 point 3): a driven start's asking permission prompts on the protocol door (<see cref="DrivenAccess"/>). Never set on the wire.
    /// </summary>
    public const char PersonAnswers = '?';

    /// <summary>
    /// Where this session's permission requests are held for the person (AUTONOMY1b3a, <see cref="HeldPermissions"/>): only where its
    /// posture carries <see cref="PersonAnswers"/>. Null, or a posture without the mark, refuses each as it comes, as every other
    /// posture always has (D52).
    /// </summary>
    public PermissionHolds? Permissions { get; set; }

    /// <summary>
    /// Asked at a turn's end before a word held for it is prompted (ACCESSSWAP1b, D183's note): whether the setup this session opened
    /// under changed since, a driven session's repository's permission prompts. Yes ends the run there, never mid-turn: every held word is
    /// handed back unprompted (<see cref="HandedBack"/>), none prompted into a process opened under the old posture. Null never asks.
    /// </summary>
    /// <remarks>
    /// Asked only where a held word would open a turn of Daoris's: a word already sent is the agent's and is waited for, and the turn a
    /// session's own background work wakes is its own (BGWAIT1), so neither is cut.
    /// </remarks>
    public Func<bool>? SetupChanged { get; set; }

    /// <summary>
    /// Whether this client asks the agent for AIR's typed session failures (CODEXSTATUS1, the agent-errors design §4.3): set for an
    /// agent whose toolchain declares it marks a terminal failure for such a client alone (<see cref="ErrorWords.TypedFailures"/>).
    /// </summary>
    public bool TypedFailures { get; init; }

    /// <summary>
    /// Asked at a turn's end (LANDBACK1 part 2, D87's note): what the quest and its tree say, so a turn that ended with the quest done and
    /// the tree uncommitted is carried on rather than the session's end (<see cref="AcpWaits.CarryOnAsync"/>). Dirty, with carry-ons left, the run
    /// also waits for the session's own background work as a park would have it wait (<see cref="AcpWaits.Of"/>), never cutting that wait on
    /// the closed quest. Null never carries on, and the end is as before.
    /// </summary>
    public Func<CancellationToken, Task<TurnCarry>>? CarriesOn
    {
        get => Ends.CarriesOn;
        set => Ends.CarriesOn = value;
    }

    private AcpWaits Ends => LazyInitializer.EnsureInitialized(ref _ends, () => new AcpWaits(this));
    private AcpWaits? _ends;

    /// <summary>
    /// The words held for the turn's end that the run handed back unprompted because its setup changed (ACCESSSWAP1b), in the order
    /// said: the driver keeps them on the record for the run that goes on under the new setup. So too those held when the run left a
    /// silent turn of a closed quest (RUNENDS1), and, after them, every word the run was asked to send and never wrote, turned away at its
    /// door as the run ended (<see cref="AcpTurnedAway"/>). Empty otherwise.
    /// </summary>
    public IReadOnlyList<IAgentWord> HandedBack => _open.HandedBack;

    /// <summary>
    /// How often a session that waits on its own background work reads its quest again (DONEEND1, D83's note): once it no longer
    /// waits on that work, the session ends. The driver sets every fourth of its own ticks; this default is that at its default tick.
    /// </summary>
    public TimeSpan BackgroundLook { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long a session whose quest no longer waits (RUNENDS1, D83's note) lets a turn run silent before it ends anyway: a turn its
    /// harness started, or a prompt still open, that read no update for this long. A turn that speaks is never cut. Its end is not
    /// always said: a wakeup the agent scheduled for itself starts a turn whose result carries no origin the adapter calls autonomous.
    /// </summary>
    public TimeSpan QuestDoneQuiet { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The clock the session's own waits are read off (RUNENDS1, <c>.claude/knowledge/held-clocks.md</c>): its look at its quest every
    /// <see cref="BackgroundLook"/>, the wake's quiet before Daoris's go-on, and <see cref="QuestDoneQuiet"/>. The machine's in
    /// production; a test hands one it moves. A transient episode's waits are its retry's (<see cref="TransientRetry.Clock"/>).
    /// </summary>
    public TimeProvider Clock
    {
        get => _wake.Clock;
        init => _wake.Clock = value;
    }

    /// <summary>
    /// The session's own background work still running, by name, when its run ended (DONEEND1): what ends with the session. The
    /// driver gives an agent that leaves such work a bound to exit before it ends the agent's tree. Empty until the run ends.
    /// </summary>
    public IReadOnlyList<string> LeftRunning { get; private set; } = [];

    /// <summary>Whether the posture says the person answers this session's permission requests.</summary>
    private readonly bool _personAnswers = AcpPosture.Marks(posture).PersonAnswers;

    private readonly TimeSpan _closeTimeout = closeTimeout ?? TimeSpan.FromSeconds(5);

    /// <summary>The prompts on the wire and not yet answered, and the run's door they pass: every lock of that state is its own.</summary>
    private readonly OpenPrompts _open = new();

    /// <summary>Whether the agent said it takes a prompt during a turn (<see cref="TakesWordsMidTurn"/>).</summary>
    private bool _takesWordsMidTurn;

    /// <summary>Whether *Send now* steers into the running turn (<see cref="SteersNow"/>, STEER2a).</summary>
    private bool _steersNow;

    private readonly StepCalls _calls = new();

    /// <summary>
    /// A driven run's inbox (COWORK1d, the message-urgency design §2.3): told on the reader's thread the moment the answer to the last
    /// prompt open is read, so it refuses another agent's claim from then on, before the run judges its next step.
    /// </summary>
    private IAgentInbox? _inbox;

    /// <summary>
    /// A conversation's words (AGENTERRORS1b, the design §7), read by the transient episode where a run has no inbox: its turns' queue.
    /// Set once the conversation's session is open, never at the open.
    /// </summary>
    private IEpisodeWords? _episodeWords;

    /// <summary>Whose words an episode holds, waits on and carries: a run's inbox, or a conversation's turns.</summary>
    private IEpisodeWords? EpisodeWords => _episodeWords ?? _inbox;

    private int _updates;
    private string? _sessionId;

    /// <summary>The reader, which lives as long as the conversation does, and what ends it.</summary>
    private readonly CancellationTokenSource _pumpStop = new();
    private Task? _pump;

    /// <summary>The console's lines, joined from the agent's streamed words (UX5 U3).</summary>
    private readonly AcpConsole _console = new(onLine, quiet ?? AcpConsole.Quiet);

    /// <summary>What the session runs beside itself (CONSOLE2), once it is open and when it is kept.</summary>
    private AcpStreams? _beside;

    /// <summary>The background work its harness says it runs (BGWAIT1), read whether or not its streams are kept.</summary>
    private readonly AcpBackground _background = new();

    /// <summary>The background work its harness says still runs, with when each began (FABRIC-PIPELINE1c, <see cref="LongCalls"/>).</summary>
    public IReadOnlyList<RunningTask> BackgroundRunning => _background.RunningSince;

    /// <summary>Whether no turn runs and the session waits on its own background work (BGWAIT1, SESSWATCH1a): the watcher's state C.</summary>
    public bool WaitsOnOwnWork => Ends.WaitsOnOwnWork;

    /// <summary>The turns its harness starts by itself when that work ends (BGWAIT2), read from the session's opening.</summary>
    private readonly AcpWake _wake = new();

    private readonly TimeSpan _wakeQuiet = wakeQuiet ?? AcpWake.Quiet;

    /// <summary>
    /// Why the process is being ended from outside (BGWAIT2, <see cref="Ending"/>), and whether the stream is over, after which
    /// nothing said changes how it ended. Under the lock.
    /// </summary>
    private readonly object _endGate = new();
    private Noted? _ending;
    private bool _streamOver;

    /// <summary>
    /// Told why the agent's process is about to be ended from outside the session (BGWAIT2): the timeout, said before its kill.
    /// The stream's end that follows is then that cause's, said by its own line (<see cref="AcpEnded"/>), and never the agent
    /// having exited, as the install's record said of a session the timeout killed. Said once; a stream already over keeps its
    /// own diagnosis, since the agent ended it first.
    /// </summary>
    public void Ending(Noted why)
    {
        lock (_endGate)
        {
            if (!_streamOver) _ending ??= why;
        }
    }

    /// <summary>The stream's end as a failure: its cause's where one was said before it (<see cref="Ending"/>), else <paramref name="sentence"/>.</summary>
    private DriverException StreamEnded(string sentence)
    {
        Noted? why;
        lock (_endGate) why = _ending;
        return why is not null ? new AcpEnded(why) : new DriverException(sentence);
    }

    /// <summary>
    /// The largest context reading this session has reported (TOOL3), or null when it reported none —
    /// what a conversation, which never runs <see cref="RunAsync"/>, counts toward its account (USAGE1).
    /// </summary>
    public AcpUsage? Usage => TurnStages.Of<AcpMeasure>().Usage;

    /// <summary>
    /// Told what a <c>usage_update</c> says about the account's windows (TOOL6c, D130 §5.2): its
    /// <c>_meta["_claude/rateLimit"]</c>, which Claude Code's adapter fills with the frame's <c>rate_limit_info</c> unchanged
    /// (limit-signals evidence §3), for the agent's table to read. Raised on the reader's thread; a listener's failure is its own.
    /// </summary>
    public event Action<JsonElement>? LimitsSaid;

    private AcpOptions Options => LazyInitializer.EnsureInitialized(ref _optionsKept, () => new AcpOptions(
        onLine, onOptions, (method, args, ct) => RequestAsync(method, args, ct), () => _sessionId));
    private AcpOptions? _optionsKept;

    /// <summary>The session's config options as the agent last said them (AGT6b, D98), the mode included; which a person may set is the caller's to judge (<see cref="AcpConfigOption.ThePersons"/>).</summary>
    public IReadOnlyList<AcpConfigOption> ConfigOptions => Options.Current;

    private void KeepOptions(IReadOnlyList<AcpConfigOption> options) => Options.Keep(options);

    /// <summary>Change one of the session's config options, mid-conversation (AGT6b, D98): the agent's whole list after it is kept and returned.</summary>
    public Task<IReadOnlyList<AcpConfigOption>> SetConfigOptionAsync(string configId, string value, CancellationToken ct) =>
        Options.SetAsync(configId, value, ct);

    /// <summary>The list the last setter returned; null when it returned none (D205).</summary>
    public IReadOnlyList<AcpConfigOption>? LastAcknowledgedOptions => Options.LastAcknowledged;

    /// <summary>What this start's actor asked, set on the open session by category before the posture and the first prompt (D205 §3.2).</summary>
    public ActorAsked? Carry
    {
        get => Options.Carry;
        set => Options.Carry = value;
    }

    /// <summary>Told what was carried where it differs from <see cref="Carry"/> (D205 §3.3, §5).</summary>
    public Action<ActorAsked>? Carried
    {
        get => Options.Carried;
        set => Options.Carried = value;
    }

    /// <summary>Asked where the agent refuses <see cref="Carry"/>'s model at the open: a later choice for this session, or null to end it (<see cref="AcpChoiceNotOffered"/>).</summary>
    public Func<ModelRefusal, Task<ActorAsked?>>? ModelRefused
    {
        get => Options.ModelRefused;
        set => Options.ModelRefused = value;
    }

    /// <summary>Told the options the open listed, before the posture or any set (D205 §9.2).</summary>
    public Action<IReadOnlyList<AcpConfigOption>>? OffersRead
    {
        get => Options.OffersRead;
        set => Options.OffersRead = value;
    }

    /// <summary>The options as the open listed them, never overwritten (D205 §3.3).</summary>
    public IReadOnlyList<AcpConfigOption> OpeningOptions => Options.OpeningOptions;

    /// <summary>The refusal that ended this session's open before any prompt (D205 §3.3), or null.</summary>
    public ModelRefusal? NotOffered => Options.NotOffered;

    private void KeepOpening() => Options.KeepOpening();

    private Task<bool> CarryAsync(CancellationToken ct) => Options.CarryAsync(ct);

    /// <summary>
    /// Run one turn end to end: handshake, a session on the tree, the target as a prompt, and every
    /// update rendered as it arrives — a driven session's whole life on this wire.
    /// </summary>
    /// <param name="cwd">The working tree this session runs in — the registered root, or a session tree (D51).</param>
    /// <param name="prompt">The composed target, exactly as the pipe door delivers it.</param>
    /// <param name="servers">
    /// The MCP servers this session is handed (ACP4) — in practice the machine's own knowledge host,
    /// which is what makes the composed target's "respond to `#id` with `take`" a thing the session
    /// can actually do.
    /// </param>
    /// <param name="inbox">
    /// What the person tells the session while it works (SESS3). Where the agent says it takes a prompt during a turn,
    /// each word is sent the moment it is said and reaches it at its next step (STEER1, D136); anywhere else each word
    /// held when a turn ends is the next prompt of this same session, and a stop of the turn in hand lets one go at once.
    /// The run ends only when nothing is held and no word is on its way. Null for a session nobody may tell anything —
    /// an intake (INT4h).
    /// </param>
    /// <param name="asked">
    /// Told each of the person's words as the session takes them, so the record keeps it as the person's ask there: as it
    /// is sent when no turn runs, or when the prompt before it is answered (STEER1).
    /// </param>
    /// <param name="resume">
    /// The harness conversation an answer continues (ANSWER1a, D131 §1), resumed rather than a new session opened, with
    /// <paramref name="prompt"/> — the answer — as its next turn. Null opens a new session, as every start does.
    /// </param>
    /// <param name="blocks">
    /// The first prompt as text blocks, one each, in place of <paramref name="prompt"/>'s one (MSG1b, D137 §2.2): the person's
    /// words a record goes on with, each its own block in the order said. Null or empty sends <paramref name="prompt"/>.
    /// </param>
    /// <param name="prompted">
    /// Told once the first prompt is on the wire (MSG1b): the words it carries were taken by the session, where a refusal
    /// before it, or a failure of the handshake, took nothing.
    /// </param>
    /// <param name="files">
    /// The files said with <paramref name="blocks"/>' words (MSG1d3, D137 §2.4), each a <c>resource_link</c> to where it is
    /// kept after the blocks, as a conversation's message carries what was attached (CONV4c). Null or empty for none.
    /// </param>
    /// <param name="waitsOnBackground">
    /// Asked when a turn ends while background work the harness says the session started still runs (BGWAIT1): whether the
    /// session's quest stands as a park would leave it, so the turn's end would otherwise read as asking the person. Yes
    /// keeps the session open until that work ends, by the wire's word, then goes on with a turn that says so. Null never
    /// waits, and what it left running ends with the session (D105 §3).
    /// </param>
    /// <exception cref="AcpResumeRefused">The agent offers no resume, or refused this one: nothing was prompted.</exception>
    /// <exception cref="DriverException">The agent's stream ended while the session waited for its background work.</exception>
    public Task<AcpOutcome> RunAsync(
        string cwd, string prompt, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null,
        IAgentInbox? inbox = null, Action<IAgentWord>? asked = null, string? resume = null,
        IReadOnlyList<string>? blocks = null, Action? prompted = null, IReadOnlyList<KeptFile>? files = null,
        Func<CancellationToken, Task<bool>>? waitsOnBackground = null,
        Func<Task<(IReadOnlyList<string> Blocks, IReadOnlyList<KeptFile>? Files)?>>? attempting = null, TransientRetry? retry = null) =>
        Run.RunAsync(cwd, prompt, ct, servers, inbox, asked, resume, blocks, prompted, files, waitsOnBackground, attempting, retry);

    private AcpRun Run => LazyInitializer.EnsureInitialized(ref _run, () => new AcpRun(this, Prompting, Steering, Ends));
    private AcpRun? _run;

    private AcpPrompting Prompting => LazyInitializer.EnsureInitialized(ref _prompting, () => new AcpPrompting(this));
    private AcpPrompting? _prompting;

    private AcpSteering Steering => LazyInitializer.EnsureInitialized(ref _steering, () => new AcpSteering(this, Prompting));
    private AcpSteering? _steering;

    private AcpTry Try => LazyInitializer.EnsureInitialized(ref _try, () => new AcpTry(this, Prompting));
    private AcpTry? _try;

    /// <summary>The driver's own sentence by its code: the console and the record keep the English as written.</summary>
    private void Note(Noted noted)
    {
        _console.End();
        onLine(noted.Note);
        Emit(new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts.Count > 0 ? noted.Parts : null,
        });
    }

    // The run's own token and whether a run holds the session: only a run's stop puts a cancel on the wire.
    private CancellationToken _stops;
    private bool _inRun;

    /// <summary>The run's transient retry (AGENTERRORS1a), or null where it was handed none and keeps today's end.</summary>
    private TransientRetry? _retry;

    /// <summary>
    /// What the run's first prompt's write return tells (<see cref="RunAsync"/>'s <c>prompted</c>), told again as each try of a transient
    /// episode's write returns (FOUNDATION1f-iv part b): a driven carry-on's moved words' <c>sent</c> is the first write that returns, whichever
    /// prompt it was, and the caller tells it once.
    /// </summary>
    private Action? _prompted;

    private AcpEpisodes Episodes => LazyInitializer.EnsureInitialized(ref _episodes, () => new AcpEpisodes(this));
    private AcpEpisodes? _episodes;

    /// <summary>Whether the run waits between a transient episode's tries, on Daoris's own timer and never on its agent (AGENTERRORS1a, the design §5.1).</summary>
    public bool WaitsOnTransient => Episodes.WaitsOnTransient;

    /// <summary>One of the run's prompts, read for a transient failure where the run was handed a retry (AGENTERRORS1a, the design §5.1).</summary>
    private Task<string> PromptingAsync(Func<Task<string>> prompt, CancellationToken ct) => Episodes.PromptingAsync(prompt, ct);

    private Task<JsonElement> OpeningAsync(Func<Task<JsonElement>> open, CancellationToken ct) => Episodes.OpeningAsync(open, ct);

    TransientRetry? ITurnRetryWiring.Retry => _retry;

    IEpisodeWords? ITurnRetryWiring.EpisodeWords => EpisodeWords;

    List<IAgentWord> ITurnRetryWiring.TakeUnacknowledged() => _open.TakeUnacknowledged();

    void ITurnRetryWiring.ForgetUnacknowledged() => _open.ForgetUnacknowledged();

    Task<string> ITurnRetryWiring.TryAsync(TransientEpisode episode, CancellationToken ct) => Try.TryAsync(episode, ct);

    /// <summary>
    /// The person's words this run sent and the session never took, once the run has ended (TURNFAIL1): on the wire behind a
    /// turn that ended first, a failed one included. The driver keeps them waiting where the door failed; empty until then.
    /// </summary>
    public IReadOnlyList<IAgentWord> Unreached => _open.Unreached;

    /// <summary>End what the session runs beside itself, saying so. Idempotent, and never throws.</summary>
    private Task EndStreamsAsync() => _beside?.EndAllAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Open the session a conversation lives in (CONV3b): the reader started, the handshake, a session
    /// on the tree, and the posture — everything before the first prompt, done once.
    /// </summary>
    /// <remarks>
    /// The reader runs until the agent's stream ends or <see cref="Release"/> — NOT until
    /// <paramref name="ct"/>, which bounds only the opening: a conversation outlives the request that
    /// started it, and the updates of every later turn arrive on this same reader.
    /// </remarks>
    public Task OpenAsync(string cwd, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null) =>
        Opening.OpenAsync(cwd, ct, servers);

    private AcpOpening Opening => LazyInitializer.EnsureInitialized(ref _opening, () => new AcpOpening(
        meta, Options, HandshakeAsync, OpeningAsync, (method, args, ct) => RequestAsync(method, args, ct), SetPostureAsync,
        id => _sessionId = id, replaying => _replaying = replaying, Told));
    private AcpOpening? _opening;

    /// <summary>The reader started and the handshake answered: what every session asks before it is one. The agent's answer.</summary>
    private async Task<JsonElement> HandshakeAsync(CancellationToken ct)
    {
        if (_pump is not null) throw new DriverException("this ACP session is already open.");
        if (streams is not null) _beside = new AcpStreams(streams, _console, Emit, quiet ?? AcpConsole.Quiet, lineage: Lineage);
        _pump = PumpAsync(_pumpStop.Token);

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

    /// <summary>
    /// One turn: the text as a prompt on the open session, answered by the agent's stop reason — which
    /// is also what the record's turn event carries, as the wire's word and nothing more.
    /// </summary>
    /// <remarks>
    /// One at a time, for a conversation: its caller queues a person's messages rather than sending one into a turn still
    /// running (CONV4a). A driven session sends one during a turn only where the agent says it takes one (STEER1,
    /// <see cref="RunAsync"/>), and the turn event is kept as the wire answers each, in the order it does.
    /// </remarks>
    /// <param name="sent">
    /// Told once the prompt is on the wire (CONV4a): a stop asked before that moment must wait for it,
    /// or its <c>session/cancel</c> overtakes the prompt it meant to stop and stops nothing.
    /// </param>
    /// <param name="files">
    /// What the person attached (CONV4c), each a <c>resource_link</c> to where it is kept — the
    /// protocol's baseline block, which every agent takes and <c>claude-code-acp</c> reads without a tool
    /// call (docs/2026-09-25-message-content-evidence.md).
    /// </param>
    public Task<string> PromptAsync(
        string text, CancellationToken ct, Action? sent = null, IReadOnlyList<KeptFile>? files = null) =>
        Prompting.SendPromptAsync(text, files, ct, sent, words: null);

    /// <summary>
    /// One turn of one or more text blocks, each its own (MSG1c, D137 §2.2): a conversation that goes on is resumed with the
    /// person's words waiting on its record, each its own block in the order said, as a driven record's are (MSG1b).
    /// </summary>
    /// <param name="sent">Told once the prompt is on the wire: the words it carries went to the session.</param>
    /// <param name="files">
    /// The files said with the words (MSG1d3, D137 §2.4), each a <c>resource_link</c> after the blocks, as a conversation's
    /// message carries them (CONV4c).
    /// </param>
    public Task<string> PromptAsync(
        IReadOnlyList<string> blocks, CancellationToken ct, Action? sent = null, IReadOnlyList<KeptFile>? files = null) =>
        Prompting.SendPromptAsync(blocks, files, ct, sent, words: null);

    /// <summary>
    /// Whether this session's agent said it takes a prompt during a turn (STEER1, D136), read from its <c>initialize</c>
    /// answer once the session is open: a conversation's words then go at once, at its next step (MSG1c, D137 §2.1).
    /// </summary>
    public bool NextStep => _takesWordsMidTurn;

    /// <summary>
    /// Told each of the person's words as the session takes them (MSG1c): a conversation's, whose words said during a turn
    /// are taken when the turn before them is handed off, so the record keeps them there as the person's ask.
    /// </summary>
    public void OnTaken(Action<IAgentWord> taken) => Prompting.OnAsked = taken;

    /// <summary>
    /// The person's words to a conversation whose turn is running, sent at once as a prompt of the same session (MSG1c,
    /// D137 §2.1): taken at the agent's next step, told through <see cref="OnTaken"/> where the wire says so, and answered
    /// by their own stop reason at the turn's real end.
    /// </summary>
    /// <exception cref="DriverException">
    /// The agent did not say it takes a prompt during a turn: a second prompt would reset <c>codex-acp</c>'s running turn
    /// (STEER1 §5), so nothing is sent.
    /// </exception>
    public Task<string> SayAsync(IAgentWord words, CancellationToken ct) =>
        _takesWordsMidTurn
            ? Prompting.MidTurnAsync(words, ct)
            : throw new DriverException("this agent did not say it takes words during a turn — they wait for the turn to end.");

    /// <summary>
    /// A conversation's session is handed its transient retry (AGENTERRORS1b, D210, the agent-errors design §5.1, §7), once it is open: a
    /// failure at the open keeps today's end, and the conversation's turns are the words an episode holds, waits on and carries.
    /// </summary>
    internal void Retries(TransientRetry retry, IEpisodeWords words)
    {
        _retry = retry;
        _episodeWords = words;
    }

    /// <summary>
    /// One turn's prompt of a conversation, read for a transient failure (AGENTERRORS1b): its stop reason, or that of the try that answered.
    /// Any other failure is thrown as it came, and so is one past the bound (<see cref="AcpTransientExhausted"/>).
    /// </summary>
    internal Task<string> RetriedAsync(Func<Task<string>> prompt, CancellationToken ct) => PromptingAsync(prompt, ct);

    /// <summary>
    /// A word handed to a running turn whose own prompt was refused after the word was taken (<see cref="AcpWordTurnFailed"/>, shape 2): the
    /// turn that failed is the one it opened, so its refusal is read as any prompt's, a transient one opening the episode.
    /// </summary>
    internal Task<string> RecoveredAsync(AcpRefusal refusal, CancellationToken ct) =>
        PromptingAsync(() => Task.FromException<string>(refusal), ct);

    /// <summary>Whether the person's *Send now* steers into this session's running turn (<see cref="SteersNow"/>, STEER2a).</summary>
    public bool Steers => _steersNow;

    /// <summary>The person's *Send now*, steered into the running turn (<see cref="AcpSteering.SteerAsync"/>).</summary>
    /// <returns>The adapter's outcome: <c>injected</c>, or <c>promptRequired</c> where no turn was running after all.</returns>
    /// <exception cref="AcpRefusal">The agent refused the steer, in its words.</exception>
    /// <exception cref="DriverException">The agent never said it steers, or the session is not open: nothing is sent.</exception>
    public Task<string> SteerAsync(IAgentWord words, CancellationToken ct) => Steering.SteerAsync(words, ct);

    /// <summary>
    /// A driven session's steer, answered (STEER2a): where the agent refused it, answered anything but <c>injected</c>, or never
    /// had it, it costs those words, said by its code with their id so the page settles them (D136 point 6), never the run.
    /// </summary>
    /// <param name="who">What the English calls the one that refused: a conversation's runner says "conversation".</param>
    /// <returns>The adapter's outcome, or <c>refused</c> or <c>unreached</c>: never a stop reason, and never thrown.</returns>
    /// <param name="handBack">
    /// Where the steer was answered <c>promptRequired</c> (the turn ended between the press and the steer, STEER2d) the adapter
    /// left the words to the client: this takes them back to be prompted next, and says whether it did. Said once, taken once
    /// where they are finally taken, so nothing is noted when it did.
    /// </param>
    internal Task<string> SteeredAsync(
        IAgentWord words, CancellationToken ct, string who = "session", Func<IAgentWord, bool>? handBack = null) =>
        Steering.SteeredAsync(words, ct, who, handBack);

    /// <summary>
    /// Stop the turn in flight and keep the session (D49's interrupt; CONV4's "stop the turn"): the agent
    /// winds its work up and answers the prompt with its own stop reason. A notification, so there is
    /// nothing to wait for here.
    /// </summary>
    /// <remarks>
    /// A permission request held for the person (AUTONOMY1b3a) is answered <c>cancelled</c> after the cancel goes, as the protocol asks
    /// of a client that cancels: the turn it was asked in is stopping, and nothing the person says now could land in it.
    /// </remarks>
    public async Task CancelTurnAsync()
    {
        if (_sessionId is null) return;
        Prompting.StopAsked();
        try
        {
            await NotifyAsync("session/cancel", new { sessionId = _sessionId }).ConfigureAwait(false);
        }
        finally
        {
            await TurnStages.Of<AcpPermission>().ReleaseHeldAsync(PermissionWhy.Cancelled).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stop one piece of background work the session started (CONSOLE3a): the adapter's own request,
    /// <c>_session/async_task/stop</c> with the session and the task, answered <c>{ stopped }</c>. Its
    /// stream ends when the wire says how, as every task's does. False when the session is not open,
    /// or the adapter answered that it stopped nothing; a refusal it sends back is its own words.
    /// </summary>
    public async Task<bool> StopTaskAsync(string asyncTaskId, CancellationToken ct)
    {
        if (_sessionId is null) return false;
        var answer = await RequestAsync("_session/async_task/stop", new { sessionId = _sessionId, asyncTaskId }, ct)
            .ConfigureAwait(false);
        return answer.ValueKind == JsonValueKind.Object
            && answer.TryGetProperty("stopped", out var stopped)
            && stopped.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Close the session politely so the agent can flush and persist; its exit is still what the driver
    /// observes, and a close that fails changes nothing about what already happened.
    /// </summary>
    /// <remarks>
    /// BOUNDED, because "best effort" without a bound is an unbounded wait: an agent that stops
    /// answering after its last turn would otherwise hang a run that is already finished.
    /// </remarks>
    public async Task CloseAsync(CancellationToken ct)
    {
        if (_sessionId is null) return;

        // Nothing held for the person outlives the session it was asked in (AUTONOMY1b3a): answered before the close goes.
        await TurnStages.Of<AcpPermission>().ReleaseHeldAsync(PermissionWhy.Ended).ConfigureAwait(false);

        using var closing = CancellationTokenSource.CreateLinkedTokenSource(ct);
        closing.CancelAfter(_closeTimeout);
        try
        {
            await RequestAsync("session/close", new { sessionId = _sessionId }, closing.Token)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or OperationCanceledException)
        {
            // Best-effort by construction: the conversation is over either way.
        }
    }

    /// <summary>
    /// Stop reading. Observed rather than awaited: an agent may never send another byte, and waiting on
    /// its reader would turn a stop into a hang.
    /// </summary>
    public void Release()
    {
        _pumpStop.Cancel();
        _ = _pump?.ContinueWith(static t => t.Exception, TaskScheduler.Default);
    }

    /// <summary>
    /// Completes when the agent's stream has ended and every line it sent has been handled — what a
    /// conversation's transcript waits on before it closes.
    /// </summary>
    public Task Ended => _pump ?? Task.CompletedTask;

    /// <summary>
    /// Read frames until the stream ends, dispatching each: a response completes its request, a
    /// notification is rendered, and a request from the agent is answered.
    /// </summary>
    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            await Wire.ReadAsync(
                incoming,
                DispatchAsync,
                raw: line =>
                {
                    _console.End();
                    onLine(line);
                    Emit(new SessionEvent { Kind = SessionEventKind.Raw, Raw = line });
                },
                unreadable: (error, frame) =>
                {
                    _console.End();
                    onLine($"[unreadable frame: {error.Message}] {AcpRender.Compact(frame)}");
                    Emit(new SessionEvent
                    {
                        Kind = SessionEventKind.Raw, Title = "unreadable frame", Text = error.Message, Raw = AcpRender.Compact(frame),
                    });
                },
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The run ended; the reader is nobody's business now.
        }
        finally
        {
            // What it ran beside itself ends with it (CONSOLE2), said before the console closes.
            await EndStreamsAsync().ConfigureAwait(false);

            // An agent's last words before its stream ended are the ones a person reads the console for.
            _console.Dispose();

            // Whatever was still awaited will never be answered. Faulting it here is what turns an
            // agent that died mid-handshake into a sentence rather than a hang. 🔴 BGWAIT2: where the timeout said it was
            // ending the process before the stream ended, the sentence is the timeout's; whatever is said after is not.
            lock (_endGate) _streamOver = true;

            // What it held for the person ends with the stream, said before the stream's end is (AUTONOMY1b3a): there is no wire to
            // answer on now, and an answer the person gives after would land nowhere.
            await TurnStages.Of<AcpPermission>().ReleaseHeldAsync(PermissionWhy.Ended).ConfigureAwait(false);
            Fail(StreamEnded(
                "the ACP agent's stream ended before it answered — the agent exited, or it is not "
                + "speaking ACP on stdout."));
        }
    }

    private async Task DispatchAsync(JsonElement frame)
    {
        var hasMethod = frame.TryGetProperty("method", out var method);
        var hasId = frame.TryGetProperty("id", out var id);
        var name = hasMethod && method.ValueKind == JsonValueKind.String ? method.GetString() : null;

        // Anything but an update ends the line the agent's words left open: a turn's answer, a
        // refusal, a request. An update decides for itself (UX5 U3).
        if (name != "session/update") _console.End();

        if (!hasMethod && hasId)
        {
            Complete(id, frame);
            return;
        }

        if (!hasMethod) return; // neither a call nor an answer; nothing to do with it

        if (hasId)
        {
            try
            {
                await AnswerRequestAsync(name, id, frame).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // 🔴 A request is ANSWERED, whatever went wrong reading it (REV3): an unanswered one is
                // a turn hung for ever, which is worse than any error the agent can be told.
                await SendAsync(new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = JsonNode.Parse(id.GetRawText()),
                    ["error"] = new JsonObject { ["code"] = -32603, ["message"] = $"daoris-driver could not read {name}: {error.Message}" },
                }).ConfigureAwait(false);
                throw;
            }

            return;
        }

        if (name == "session/update" && frame.TryGetProperty("params", out var p)
            && p.TryGetProperty("update", out var update))
        {
            await StagesAsync(TurnPhase.Update, new TurnFrame(name, frame, default, update)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The turn's stages, registered by their modules (<see cref="TurnStageAttribute"/>), ordered once per process and built once per
    /// session with the session as their wiring.
    /// </summary>
    internal PartSet<ITurnStage> TurnStages =>
        LazyInitializer.EnsureInitialized(ref _turnStages, () => Parts.Build(TurnOrder.Value, type => (ITurnStage)Activator.CreateInstance(type, this)!));

    private PartSet<ITurnStage>? _turnStages;

    private static readonly Lazy<IReadOnlyList<PartDeclaration>> TurnOrder = new(() => Parts.Declared<TurnStageAttribute>(typeof(AcpSession).Assembly));

    /// <summary>The stages of <paramref name="phase"/>, in order, until one takes the frame: whether one did.</summary>
    private async Task<bool> StagesAsync(TurnPhase phase, TurnFrame frame)
    {
        foreach (var stage in TurnStages.In(phase))
        {
            if (await stage.RunAsync(frame).ConfigureAwait(false)) return true;
        }

        return false;
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

    void ITurnEndWiring.Note(Noted noted) => Note(noted);

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

    Task ITurnRunWiring.EndStreamsAsync() => EndStreamsAsync();

    int ITurnRunWiring.Updates => _updates;

    void ITurnRunWiring.Left(IReadOnlyList<string> running) => LeftRunning = running;

    private readonly RefusedCalls _refused = new();

    /// <summary>
    /// An event to whoever keeps the record — and a record that fails costs a line on the console,
    /// never the turn: the conversation enriches the session, it does not run it.
    /// </summary>
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

    private void Complete(JsonElement id, JsonElement frame) => Wire.Complete(id, frame);

    private async Task AnswerRequestAsync(string? method, JsonElement id, JsonElement frame)
    {
        if (await StagesAsync(TurnPhase.Request, new TurnFrame(method, frame, id, default)).ConfigureAwait(false)) return;

        // Anything else the agent asks of the client, this client does not implement — and says so in
        // the protocol's own vocabulary rather than leaving the agent waiting. An unanswered request
        // is a hung turn, which is the worst available outcome.
        await SendAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonNode.Parse(id.GetRawText()),
            ["error"] = new JsonObject
            {
                ["code"] = -32601,
                ["message"] = $"daoris-driver does not implement {method}",
            },
        }).ConfigureAwait(false);
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

    private void Fail(Exception error) => Wire.Fail(error);

    // ---- Resuming a conversation (ANSWER1a, D131) ----

    /// <summary>The conversation whose replay is arriving while a <c>session/load</c> is answered, or null.</summary>
    private volatile string? _replaying;

    /// <summary>Resume a conversation instead of opening one (ANSWER1a, D131 §1): <c>session/resume</c> where the agent advertises it, else <c>session/load</c>.</summary>
    /// <exception cref="AcpResumeRefused">The agent offers neither, or refused this one.</exception>
    public Task ResumeAsync(string cwd, string conversation, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null) =>
        Opening.ResumeAsync(cwd, conversation, ct, servers);

    /// <summary>Whether an update is a load's replay of the conversation being resumed.</summary>
    private bool Replayed(JsonElement parameters) =>
        _replaying is { } replaying
        && parameters.TryGetProperty("sessionId", out var id) && id.ValueKind == JsonValueKind.String
        && string.Equals(id.GetString(), replaying, StringComparison.Ordinal);

    /// <summary>Tell whoever keeps it the id the agent named (ANSWER1a); a listener's failure costs a console line, never the session.</summary>
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
