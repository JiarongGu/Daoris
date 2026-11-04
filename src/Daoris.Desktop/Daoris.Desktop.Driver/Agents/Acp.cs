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
    TimeSpan? wakeQuiet = null) : ITurnRetryWiring
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
    /// the tree uncommitted is carried on rather than the session's end (<see cref="CarryOnAsync"/>). Dirty, with carry-ons left, the run
    /// also waits for the session's own background work as a park would have it wait (<see cref="Waits"/>), never cutting that wait on
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
    public IReadOnlyList<IAgentWord> HandedBack
    {
        get
        {
            lock (_promptsGate) return [.. _handedBack, .. _turnedAway];
        }
    }

    private IReadOnlyList<IAgentWord> _handedBack = [];

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

    /// <summary>The prompts on the wire and not yet answered, oldest first, and the lock that guards them (STEER1).</summary>
    private readonly LinkedList<OpenPrompt> _prompts = [];
    private readonly object _promptsGate = new();

    /// <summary>Whether the agent said it takes a prompt during a turn (<see cref="TakesWordsMidTurn"/>).</summary>
    private bool _takesWordsMidTurn;

    /// <summary>Whether *Send now* steers into the running turn (<see cref="SteersNow"/>, STEER2a).</summary>
    private bool _steersNow;

    private readonly StepCalls _calls = new();

    /// <summary>
    /// The status the record closes a call with that a steer cut and that never heard again (STEER2a, D136's 2026-10-16 note;
    /// evidence §6, turn B): written when the turn's prompt is answered, so the page shows it cut, never still running.
    /// </summary>
    public const string CutStatus = "cut";

    /// <summary>Told each of the person's words as the session takes them (SESS3, STEER1); a run's.</summary>
    private Action<IAgentWord>? _asked;

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

    /// <summary>
    /// One prompt on the wire: the person's words, or null for the driver's own — and whether the session has taken them.
    /// </summary>
    private sealed class OpenPrompt(IAgentWord? words, IReadOnlyList<IAgentWord>? together = null)
    {
        public IAgentWord? Words { get; } = words;

        /// <summary>The words a transient try carries after <see cref="Words"/> in the same prompt (AGENTERRORS1a, §5.1), in the order said.</summary>
        public IReadOnlyList<IAgentWord> Together { get; } = together ?? [];

        /// <summary>Every word this prompt carries, <see cref="Words"/> first.</summary>
        public IEnumerable<IAgentWord> All => Words is null ? Together : [Words, .. Together];

        public bool Taken { get; set; }

        /// <summary>
        /// Whether an update of this prompt's turn came while it was the oldest open (AGENTERRORS1a, §5.1): what makes a word whose prompt
        /// then met an error taken, where none makes it unacknowledged. Read from the turn, never from <see cref="Taken"/> alone.
        /// </summary>
        public bool Heard { get; set; }

        /// <summary>Whether it is the run's first prompt (AGENTERRORS1a): a resume's blocks, or a start's target.</summary>
        public bool Opening { get; init; }

        /// <summary>
        /// Whether its write returned (Codex's owed read of D210, finding 3): on the wire, so after an episode opened a prompt whose stream
        /// is lost before any terminal answer is uncertain, never unreached. Set under <c>_promptsGate</c>.
        /// </summary>
        public bool Written { get; set; }
    }

    /// <summary>
    /// A prompt's way to the wire (RUNENDS1 after review, finding 2): entered when the run is asked to send it, before its words are
    /// marked on their record; admitted under the write lock as it is written, and gone from the door then; or turned away once the run's
    /// door has shut, its words handed back. Read and changed under <c>_promptsGate</c>.
    /// </summary>
    private sealed class Passage(IReadOnlyList<IAgentWord> words)
    {
        /// <summary>The person's words it carries, which go back to the driver where it is turned away.</summary>
        public IReadOnlyList<IAgentWord> Words { get; set; } = words;

        public bool TurnedAway { get; set; }
    }

    /// <summary>The passages entered and not yet admitted or turned away, and whether the run's door has shut (<see cref="Shut"/>).</summary>
    private readonly List<Passage> _passing = [];
    private bool _shut;

    /// <summary>The words turned away at the run's door, in the order they came to it: handed back after the rest (<see cref="HandedBack"/>).</summary>
    private readonly List<IAgentWord> _turnedAway = [];

    private int _promptCount;
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
    public async Task<AcpOutcome> RunAsync(
        string cwd, string prompt, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null,
        IAgentInbox? inbox = null, Action<IAgentWord>? asked = null, string? resume = null,
        IReadOnlyList<string>? blocks = null, Action? prompted = null, IReadOnlyList<KeptFile>? files = null,
        Func<CancellationToken, Task<bool>>? waitsOnBackground = null,
        Func<Task<(IReadOnlyList<string> Blocks, IReadOnlyList<KeptFile>? Files)?>>? attempting = null, TransientRetry? retry = null)
    {
        _stops = ct;
        _inRun = true;
        _asked = asked;
        _inbox = inbox;
        _retry = retry;
        _prompted = prompted;
        try
        {
            try
            {
                if (resume is null) await OpenAsync(cwd, ct, servers).ConfigureAwait(false);
                else await ResumeAsync(cwd, resume, ct, servers).ConfigureAwait(false);
            }
            catch (AcpChoiceNotOffered refused)
            {
                // D205 §3.3 (MODELROLE1c2): the choice refused at the open, no prompt sent, so nothing of the person's was taken; the
                // session closes here and its record ends as the start's re-ask said.
                onLine($"[{refused.Message}]");
                await CloseAsync(ct).ConfigureAwait(false);
                return new AcpOutcome(AcpOutcome.ChoiceNotOffered, _sessionId ?? "", _updates, Usage) { NotOffered = refused.Refusal };
            }

            // Where the agent says it takes a prompt during its turn, each word goes the moment it is said and reaches it
            // at its next step (STEER1, D136); anywhere else it waits for the turn's end. Words flow only once the target
            // is on the wire, so none overtakes the prompt it follows.
            // The person's *Send now* steers into the running turn where the agent offers that too (STEER2a).
            inbox?.Attach(
                CancelTurnAsync,
                _takesWordsMidTurn ? words => MidTurnAsync(words, ct) : null,
                _steersNow ? words => SteeredAsync(words, ct, handBack: inbox is null ? null : inbox.HandBack) : null);
            Action? sent = inbox is null && prompted is null
                ? null
                : () =>
                {
                    inbox?.Flow();
                    prompted?.Invoke();
                };
            // COWORK1c (the message-urgency design §6): the words a resumed run carries are marked attempted on its record once the agent
            // took the conversation back and before the prompt carrying them is written.
            // FOUNDATION1 row p (the design §3.4 rule 5): acknowledged, and the blocks and files read again from the words it names, so an
            // answer naming fewer than asked sends only those; none named, nothing is prompted and the words wait on their record.
            if (attempting is not null)
            {
                if (await attempting().ConfigureAwait(false) is not { } named) throw new AcpWordsUnmarked(AcpWordsUnmarked.Sentence);
                blocks = named.Blocks;
                files = named.Files;
            }

            // AGENTERRORS1a (§5.1): each prompt site of a run handed a retry reads its failure, and a transient one opens the episode here.
            var stopReason = await PromptingAsync(
                    () => blocks is { Count: > 0 }
                        ? SendPromptAsync(blocks, files, ct, sent, words: null)
                        : PromptAsync(prompt, ct, sent),
                    ct)
                .ConfigureAwait(false);

            // LANDBACK1 part 2: what the loop asks of the quest is its own wait on background work, or a carry-on owed for a done quest's tree.
            var turnWaits = Ends.Of(waitsOnBackground);

            // The person's words, one prompt each, in the order said — the session keeps its context. A word sent during
            // the turn is waited for until it is answered; one held is prompted now, and a turn stopped to send it ends
            // `cancelled` before it goes.
            while (true)
            {
                // A turn that ended while the session's own background work runs, where its quest would park it on the
                // person, waits for that work and goes on with a turn that says it ended (BGWAIT1). Words held for the
                // turn's end go first; words that go at once still go while it waits.
                if (!Holds(inbox) && await Ends.AfterBackgroundAsync(turnWaits, ct).ConfigureAwait(false) is { } wentOn)
                {
                    stopReason = wentOn;
                    continue;
                }

                // RUNENDS1: a silent turn of a closed quest was left and the run's door shut: every word still held goes back unprompted.
                if (DoorShut)
                {
                    var waiting = inbox?.Close() ?? [];
                    lock (_promptsGate) _handedBack = waiting;
                    break;
                }

                // LANDBACK1 part 2: the turn ended with its quest done and its tree uncommitted, and nothing of its own runs: not its end.
                if (!Holds(inbox) && await Ends.CarryOnAsync(ct).ConfigureAwait(false) is { } carriedOn)
                {
                    stopReason = carriedOn;
                    continue;
                }

                if (inbox?.NextOrClose() is not { } next) break;

                // A steer, or a stop still on its way, is no turn of its own: the run waits for it, then asks again (STEER2a).
                if (next.Waits is { } waits)
                {
                    await waits.ConfigureAwait(false);
                    continue;
                }

                if (next.Held is { } held)
                {
                    // ACCESSSWAP1b (D183's note): the setup changed while the turn ran, so the run ends at this turn's end and every word
                    // held goes back unprompted, to go on in the same conversation under the new setup. Daoris's words go back unjudged:
                    // the look judges them on the record before its go-on (COWORK1b).
                    if (SetupChanged?.Invoke() == true)
                    {
                        var rest = inbox!.Close();
                        lock (_promptsGate) _handedBack = [held, .. rest];
                        break;
                    }

                    // COWORK1b (the message-urgency design §4.3): Daoris's word held for the turn's end is judged again by its key;
                    // one whose fact a later submission answered is dropped, said so, and never prompted late.
                    if (!await held.GoesAsync(ct).ConfigureAwait(false))
                    {
                        Emit(held.Dropped());
                        // COWORK1c: settled without the wire, so off its record too.
                        held.Went?.Invoke();
                        continue;
                    }

                    try
                    {
                        stopReason = await PromptingAsync(() => SendPromptAsync(held.Prompt, held.Files, ct, sent: null, held), ct)
                            .ConfigureAwait(false);
                    }
                    catch (AcpWordsUnmarked)
                    {
                        // Row p: the record did not name it marked, so it was not written: it waits on its record, said, never the session's work.
                        Note(WordsNotes.Unreached(1, "were not sent", UnreachedWhy.Unmarked));
                    }

                    continue;
                }

                try
                {
                    stopReason = await next.Answer!.ConfigureAwait(false);
                }
                catch (AcpWordTurnFailed failed)
                {
                    // 🔴 AGENTERRORS1a2, shape 2 (the design §0, §4.2): the word was taken, so the turn that failed is the one it opened,
                    // and its refusal is that turn's prompt failing: read as any prompt site's, a transient one opening the episode.
                    stopReason = await PromptingAsync(() => Task.FromException<string>(failed.Refusal), ct).ConfigureAwait(false);
                }
                catch (AcpRefusal refused)
                {
                    // A word the agent would not take costs that word, said in its own words, never the session's work.
                    Note(WordsNotes.Refused(refused.Words));
                }
                catch (AcpWordsUnmarked)
                {
                    // Row p: a word the record did not name marked was not written: it waits on its record, said, never the session's work.
                    Note(WordsNotes.Unreached(1, "were not sent", UnreachedWhy.Unmarked));
                }
            }

            await CloseAsync(ct).ConfigureAwait(false);
            return new AcpOutcome(stopReason, _sessionId!, _updates, Usage) { Cut = _turnCut };
        }
        finally
        {
            // 🔴 RUNENDS1 after review (finding 2): the run's door shuts before its words are counted. A word on its way to the wire, its
            // mark still awaited, was in no list the end reads, and was written to the closed session once the mark landed; it now goes
            // back with the rest (HandedBack), and nothing is written after.
            await ShutAsync().ConfigureAwait(false);

            // Words sent while it worked that it never took (STEER1): said, never shown for ever as on their way.
            if (Untaken() is > 0 and var untaken)
            {
                Note(WordsNotes.Unreached(untaken, "while it worked never reached it", UnreachedWhy.EndedFirst));
            }

            // A driven session's end is its last turn's (CONSOLE2, BGWAIT1): what it left running ends with it, and
            // is said while the caller's transcript is still open, before the reader is let go. DONEEND1: named for the driver,
            // which ends the agent's tree where it does not exit with that work still running.
            LeftRunning = _background.Running;
            await EndStreamsAsync().ConfigureAwait(false);
            Release();
        }
    }

    /// <summary>The driver's own sentence, on the console and in the record.</summary>
    private void Note(string text) => Note(new Noted(text, []));

    /// <summary>The driver's own sentence by its code (CONVNOTE2d1): the console and the record keep the English as written.</summary>
    private void Note(Noted noted)
    {
        _console.End();
        onLine(noted.Note);
        Emit(new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts.Count > 0 ? noted.Parts : null,
        });
    }

    /// <summary>Whether the person's words are held for the turn's end: those go before anything else.</summary>
    private static bool Holds(IAgentInbox? inbox) => inbox?.Holds == true;

    /// <summary>
    /// The run's door shuts (RUNENDS1 after review, finding 2): from here no word of the run's reaches the wire. The inbox holds every word
    /// said from now, as an episode's settling does, so none is dispatched to the session, and each goes back with the inbox's close; a
    /// word dispatched and not yet written is turned away at its write, and goes back to the driver (<see cref="HandedBack"/>); one
    /// already written is the session's, answered or said unreached at the run's end (<see cref="Untaken"/>). Called under the write lock.
    /// </summary>
    private void Shut()
    {
        _inbox?.Settling();
        lock (_promptsGate)
        {
            _shut = true;
            foreach (var passage in _passing)
            {
                passage.TurnedAway = true;
                _turnedAway.AddRange(passage.Words);
            }

            // Their prompts were never admitted, so none of them is open.
            _passing.Clear();
        }
    }

    /// <summary>The run's door shut at its end (<see cref="Shut"/>), between two writes, never inside one.</summary>
    private async Task ShutAsync()
    {
        await Wire.WriteLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Shut();
        }
        finally
        {
            Wire.WriteLock.Release();
        }
    }

    /// <summary>Whether the run's door has shut, a silent turn left (RUNENDS1): the run ends at once.</summary>
    private bool DoorShut
    {
        get
        {
            lock (_promptsGate) return _shut;
        }
    }

    /// <summary>
    /// A prompt asked to go (RUNENDS1 after review): entered at the run's door before its words are marked on their record, so a run that
    /// ends meanwhile turns it away and hands its words back, rather than lose it.
    /// </summary>
    /// <exception cref="AcpTurnedAway">The door has shut: nothing is marked or written, and the words went back.</exception>
    private Passage Enter(IEnumerable<IAgentWord?> words)
    {
        var passage = new Passage([.. words.OfType<IAgentWord>()]);
        lock (_promptsGate)
        {
            if (!_shut)
            {
                _passing.Add(passage);
                return passage;
            }

            _turnedAway.AddRange(passage.Words);
        }

        throw new AcpTurnedAway();
    }

    /// <summary>
    /// A passage admitted to the wire as its write begins, under the write lock, and its prompt open from then; false where the door shut
    /// first, or where <paramref name="when"/>, what the prompt was decided on, no longer holds (RUNENDS1, second read), its words then
    /// going back with the run's.
    /// </summary>
    /// <remarks><paramref name="when"/> reads the wake under its own gate before the prompts' gate is taken: the order every look takes.</remarks>
    private bool Admit(Passage passage, OpenPrompt? open = null, Func<bool>? when = null)
    {
        var holds = when?.Invoke() ?? true;
        lock (_promptsGate)
        {
            if (passage.TurnedAway) return false;
            _passing.Remove(passage);
            if (!holds)
            {
                _turnedAway.AddRange(passage.Words);
                return false;
            }

            if (open is not null)
            {
                // 🔴 COWORK1c (trap 8): words prompted to an idle session are taken at the first thing the agent does for them, or by
                // their own answer, never before the wire, as words after an error are (USERSHOWN1).
                if (open.Words is not null && _prompts.Count == 0) _afterError = true;
                _prompts.AddLast(open);
            }

            return true;
        }
    }

    /// <summary>
    /// Whether the run's own token was cancelled, the person's stop or the driver's shutdown (RUNENDS1, second read, finding 3): the one
    /// cancellation a run puts on the wire. A conversation, which has no run, keeps its stop as it was.
    /// </summary>
    private bool Stopped => !_inRun || _stops.IsCancellationRequested;

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

    List<IAgentWord> ITurnRetryWiring.TakeUnacknowledged()
    {
        lock (_promptsGate)
        {
            var words = _unacknowledged.ToList();
            _unacknowledged.Clear();
            return words;
        }
    }

    void ITurnRetryWiring.ForgetUnacknowledged()
    {
        lock (_promptsGate) _unacknowledged.Clear();
    }

    Task<string> ITurnRetryWiring.TryAsync(TransientEpisode episode, CancellationToken ct) => TryAsync(episode, ct);

    /// <summary>
    /// One try (§5.1): one prompt of the person's held words, verbatim and in the order said, then Daoris's go-on, kept through the run's
    /// dispatcher before the episode's first try and carried as a plain block on every later one, neither kept nor shown again.
    /// </summary>
    private async Task<string> TryAsync(TransientEpisode episode, CancellationToken ct)
    {
        var retry = _retry!;
        var held = EpisodeWords?.TakeHeld() ?? [];
        // RUNENDS1: the held words go through the run's door from here, so a run that ends before the try is written hands them back.
        var passage = Enter(held);
        var text = TransientRetry.GoOnText(episode.Words);
        IAgentWord? goOn = null;
        if (episode.Tries == 1 && retry.KeepGoOn is { } keep)
        {
            try
            {
                goOn = await keep(episode, text, ct).WaitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException cancelled || cancelled.CancellationToken != ct)
            {
                // A keep that failed, or cancelled on its own, costs the go-on its record, never the try (RUNENDS1, second read).
                ct.ThrowIfCancellationRequested();
                onLine($"[the go-on could not be kept: {error.Message}]");
            }
        }

        if (_sessionId is null) throw new DriverException("this ACP session is not open — nothing can be prompted on it.");

        // 🔴 A kept word goes to the wire only once its record acknowledged its attempt (Codex's owed read of D210, finding 4): the
        // dispatcher's mark fails open, and a word written unmarked whose take-off then failed would read as never sent at a restart. One
        // of the person's that the record would not mark is not written: it waits on its record for the next run, which sends it once. The
        // go-on unmarked is settled without the wire, its kept copy off the record, and goes as the try's plain block. RUNENDS1: each mark
        // is waited for only until the try is cut; the held words then go back with the run's (the door).
        List<IAgentWord> going = [];
        foreach (var word in held)
        {
            if (await MarkedAsync(word).WaitAsync(ct).ConfigureAwait(false)) going.Add(word);
            else onLine($"[the record did not mark `{word.Id}` attempted, so no try sends it: it waits on its record for the next run]");
        }

        if (goOn is not null && !await MarkedAsync(goOn).WaitAsync(ct).ConfigureAwait(false))
        {
            goOn.Went?.Invoke();
            goOn = null;
        }

        List<IAgentWord> words = [.. going, .. goOn is null ? [] : new[] { goOn }];
        List<string> texts = [.. going.Select(word => word.Prompt), text];
        var prompt = Blocks(texts, [.. going.SelectMany(word => word.Files)]);

        var open = new OpenPrompt(words.FirstOrDefault(), [.. words.Skip(1)]);
        lock (_promptsGate)
        {
            if (passage.TurnedAway) throw new AcpTurnedAway();
            // A word the record would not mark waits on its record: it is not handed back with the rest.
            passage.Words = going;
        }

        var wrote = words.Select(each => Wrote(null, each)).OfType<Action>().ToList();
        try
        {
            // RUNENDS1, second read (findings 1 and 3): a try the cut reached before its write is never written, and goes back with the
            // run's words; the cut alone cancels nothing on the wire.
            var result = await RequestAsync(
                    "session/prompt", new { sessionId = _sessionId, prompt }, ct,
                    OnWire(open, () =>
                    {
                        // On the wire: a word said from now goes as it did, and the go-on leaves the record by its id (R3-O5). Where the try
                        // fails, the episode holds them again before it settles (finding 2).
                        EpisodeWords?.Waited();
                        _prompted?.Invoke();
                        foreach (var each in wrote) each();
                        if (goOn?.Id is { } id) episode.GoOnId = id;
                    }),
                    answered: frame => Answered(open, frame), admit: () => Admit(passage, open, () => !ct.IsCancellationRequested))
                .ConfigureAwait(false);
            return result.TryGetProperty("stopReason", out var reason) ? reason.GetString() ?? "unknown" : "unknown";
        }
        catch (AcpTurnedAway) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (OperationCanceledException)
        {
            if (Stopped) await CancelTurnAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// A try's word marked attempted on its record before the wire (finding 4): a kept word's mark as its record answered it, and every
    /// other word, kept nowhere, marked as any is, after the attempts asked before it.
    /// </summary>
    private async Task<bool> MarkedAsync(IAgentWord word)
    {
        return await Attempted(word).ConfigureAwait(false);
    }

    /// <summary>
    /// The person's words this run sent and the session never took, once the run has ended (TURNFAIL1): on the wire behind a
    /// turn that ended first, a failed one included. The driver keeps them waiting where the door failed; empty until then.
    /// </summary>
    public IReadOnlyList<IAgentWord> Unreached { get; private set; } = [];

    /// <summary>How many of the person's words are on the wire and not yet taken, forgotten as they are counted and kept as <see cref="Unreached"/>.</summary>
    /// <remarks>
    /// 🔴 <b>After an episode opened, a written prompt with no answer is uncertain, never unreached</b> (Codex's owed read of D210, finding
    /// 3): a transport lost behind a written word faults its request with no answer to read its disposition from, and kept as unreached
    /// the driver would keep it to go again (<see cref="WordsLeft"/>). It is said by the <c>lost</c> note and never sent again. A run with
    /// no episode keeps TURNFAIL1's reading.
    /// </remarks>
    private int Untaken()
    {
        List<IAgentWord> uncertain;
        lock (_promptsGate)
        {
            var left = _prompts.Where(open => open.Words is not null && !open.Taken).ToList();
            var written = _retry?.Opened == true ? left.Where(open => open.Written).ToList() : [];
            Unreached = [.. left.Except(written).Select(open => open.Words!)];
            uncertain = [.. written.SelectMany(open => open.All).Where(word => !word.IsDaoris)];
            _prompts.Clear();
        }

        if (uncertain.Count > 0)
        {
            _retry!.Unacknowledged(uncertain);
            List<string> ids = [.. uncertain.Select(word => word.Id).OfType<string>()];
            if (ids.Count > 0) Emit(new SessionEvent { Kind = SessionEventKind.Note, Text = SessionNotes.LostLine, Words = ids, Code = SessionEventCodes.Lost });
        }

        return Unreached.Count;
    }

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
    public async Task OpenAsync(string cwd, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null)
    {
        await HandshakeAsync(ct).ConfigureAwait(false);
        var offered = Offer(servers);

        // The rules composed for this session ride here when the adapter takes them (PERM1) —
        // and a session given none sends exactly what it sent before they existed.
        // AGENTERRORS1a2 (§5.6): a transient refusal of the raw operation is tried again on the schedule, never the handshake.
        var openMeta = OpenMeta(meta, offered);
        var created = await OpeningAsync(
            () => RequestAsync(
                "session/new",
                openMeta is null
                    ? new { cwd, mcpServers = offered }
                    : (object)new { cwd, mcpServers = offered, _meta = openMeta },
                ct),
            ct).ConfigureAwait(false);
        // Read without trusting the shape (REV3): an id of the wrong kind threw past every catch that
        // names this client's own failures, and a conversation waited for a session that never came.
        _sessionId = created.ValueKind == JsonValueKind.Object && created.TryGetProperty("sessionId", out var id)
                     && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
        if (string.IsNullOrEmpty(_sessionId))
        {
            throw new DriverException("the ACP agent created a session without an id — nothing can be sent to it.");
        }

        // What an answer to a park resumes (ANSWER1a), told before the first turn can fail or park.
        Told(_sessionId);

        // What the agent offers to change about this session — its mode, its model, its effort (AGT6b):
        // kept as it said them, so a person may be offered the ones that are theirs.
        KeepOptions(created.TryGetProperty("configOptions", out var offeredOptions)
            ? AcpConfigOption.Read(offeredOptions)
            : []);
        // The opening's own snapshot and the agent's offer reading, before the posture or any set (D205 §3.3, §9.2).
        KeepOpening();

        // What its actor asked, before the posture, which a model change can move, and before the first prompt (D205 §3.2).
        var carried = await CarryAsync(ct).ConfigureAwait(false);
        await SetPostureAsync(created, ct, again: carried).ConfigureAwait(false);
    }

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
        SendPromptAsync(text, files, ct, sent, words: null);

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
        SendPromptAsync(blocks, files, ct, sent, words: null);

    /// <summary>
    /// Whether this session's agent said it takes a prompt during a turn (STEER1, D136), read from its <c>initialize</c>
    /// answer once the session is open: a conversation's words then go at once, at its next step (MSG1c, D137 §2.1).
    /// </summary>
    public bool NextStep => _takesWordsMidTurn;

    /// <summary>
    /// Told each of the person's words as the session takes them (MSG1c): a conversation's, whose words said during a turn
    /// are taken when the turn before them is handed off, so the record keeps them there as the person's ask.
    /// </summary>
    public void OnTaken(Action<IAgentWord> taken) => _asked = taken;

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
            ? MidTurnAsync(words, ct)
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

    /// <summary>
    /// The person's *Send now* (STEER2a, D136's 2026-10-16 note): the words steered into the running turn by the adapter's
    /// <c>_session/steering</c>, which delivers them at priority <c>now</c>, cutting the step in flight (evidence §2, §6 turn B).
    /// The request asks the adapter never to start a turn of its own (<c>idleBehavior: promptRequired</c>), since a turn it
    /// started would answer nobody here. Answered <c>injected</c>, the words are taken there, told through the run's listener on
    /// the reader's thread before anything after the answer is read, and the calls still open are marked as cut.
    /// </summary>
    /// <returns>The adapter's outcome: <c>injected</c>, or <c>promptRequired</c> where no turn was running after all.</returns>
    /// <exception cref="AcpRefusal">The agent refused the steer, in its words.</exception>
    /// <exception cref="DriverException">The agent never said it steers, or the session is not open: nothing is sent.</exception>
    public async Task<string> SteerAsync(IAgentWord words, CancellationToken ct)
    {
        if (!_steersNow)
        {
            throw new DriverException(
                "this agent did not say it steers words into a running turn — they reach it at its next step or its turn's end.");
        }

        if (_sessionId is null) throw new DriverException("this ACP session is not open — nothing can be steered into it.");

        // COWORK1c: a kept word is marked attempted on its record before the wire, in the order asked for, and written only once the record
        // named it marked (FOUNDATION1 row p): its steer says it never reached, and it waits on its record. RUNENDS1: through the run's door.
        var passage = Enter([words]);
        var marked = await Attempted(words).ConfigureAwait(false);
        lock (_promptsGate)
        {
            if (passage.TurnedAway) throw new AcpTurnedAway();
            if (!marked)
            {
                _passing.Remove(passage);
                throw new AcpWordsUnmarked(AcpWordsUnmarked.Sentence);
            }
        }

        // The adapter's own parameters (claude-agent-acp 0.84.0, `parseSteerRequest`, acp-agent.js:215-238): the session, the
        // prompt's content blocks as `session/prompt` carries them, and the steering `_meta`. The priority is the adapter's.
        var answer = await RequestAsync(
            "_session/steering",
            new { sessionId = _sessionId, prompt = Blocks([words.Prompt], words.Files), _meta = new { steering = new { idleBehavior = "promptRequired" } } },
            ct,
            Wrote(null, words),
            answered: frame =>
            {
                if (Outcome(frame) != "injected") return;
                _calls.CutOpen();
                Asked(words);
            },
            admit: () => Admit(passage)).ConfigureAwait(false);
        return Outcome(answer) ?? "unknown";
    }

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
    internal async Task<string> SteeredAsync(
        IAgentWord words, CancellationToken ct, string who = "session", Func<IAgentWord, bool>? handBack = null)
    {
        try
        {
            var outcome = await SteerAsync(words, ct).ConfigureAwait(false);
            if (outcome == "promptRequired" && handBack?.Invoke(words) == true) return outcome;
            if (outcome != "injected") NoteWords(WordsNotes.NeverReached(NotSteered(outcome)), words);
            return outcome;
        }
        catch (AcpTurnedAway)
        {
            // RUNENDS1: the run ended before the steer was written, and its words went back with the run's: nothing more is said here.
            return "unreached";
        }
        catch (AcpRefusal refused)
        {
            NoteWords(WordsNotes.Refused(refused.Words, who), words);
            return "refused";
        }
        catch (Exception error) when (error is DriverException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            NoteWords(WordsNotes.NeverReached(error.Message), words);
            return "unreached";
        }
    }

    /// <summary>The program's sentence for a steer the agent answered with anything but <c>injected</c> (STEER2a).</summary>
    public static string NotSteered(string outcome) =>
        $"the agent answered the steer `{outcome}`, so no running turn took them.";

    /// <summary>The driver's sentence about the person's words, by its code and with their id, so the page settles the words (MSG1f).</summary>
    private void NoteWords(Noted noted, IAgentWord words)
    {
        _console.End();
        onLine(noted.Note);
        Emit(new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts.Count > 0 ? noted.Parts : null,
            Words = words.Id is { } id ? [id] : null,
        });
    }

    /// <summary>A steer's <c>outcome</c>, from its answer frame or its result; null where it said none.</summary>
    private static string? Outcome(JsonElement answer)
    {
        var result = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("result", out var inner) ? inner : answer;
        return result.ValueKind == JsonValueKind.Object && result.TryGetProperty("outcome", out var outcome)
            && outcome.ValueKind == JsonValueKind.String
            ? outcome.GetString()
            : null;
    }

    /// <summary>The calls a steer cut that never heard again, closed in the record as cut when the turn's prompt is answered (STEER2a).</summary>
    private void CloseCut()
    {
        foreach (var id in _calls.TakeCut()) Emit(new SessionEvent { Kind = SessionEventKind.Tool, Id = id, Status = CutStatus });
    }

    private void Track(JsonElement update, SessionEvent? mapped) => _calls.Track(update, mapped);

    /// <summary>
    /// One prompt on the open session, answered by the agent's stop reason: the driver's own, or the person's words
    /// (<paramref name="words"/>), which the session takes as they go when no prompt is open, and when the one before
    /// them is answered otherwise (STEER1) — the moment <c>claude-agent-acp</c> hands the running turn off to them.
    /// </summary>
    private Task<string> SendPromptAsync(
        string text, IReadOnlyList<KeptFile>? files, CancellationToken ct, Action? sent, IAgentWord? words, Func<bool>? when = null) =>
        SendPromptAsync([text], files, ct, sent, words, when);

    /// <summary>One prompt of one or more text blocks, each its own (MSG1b), then the files as links.</summary>
    /// <param name="when">
    /// Read under the write lock as the prompt is written (RUNENDS1, second read, finding 2): false where what the prompt was decided on
    /// no longer holds, and it is not written at all (<see cref="AcpTurnedAway"/>). Null always holds.
    /// </param>
    private async Task<string> SendPromptAsync(
        IReadOnlyList<string> texts, IReadOnlyList<KeptFile>? files, CancellationToken ct, Action? sent, IAgentWord? words,
        Func<bool>? when = null)
    {
        if (_sessionId is null) throw new DriverException("this ACP session is not open — nothing can be prompted on it.");

        var prompt = Blocks(texts, files);
        // RUNENDS1 after review (finding 2): through the run's door from before the mark to the write, so a run that ends between the two
        // hands the words back and writes nothing after.
        var passage = Enter([words]);
        // COWORK1c (the message-urgency design §6): a kept word is marked attempted on its record before the wire, in the order the
        // prompts were asked for, so a shell that comes up again never sends it twice. Acknowledged (FOUNDATION1 row p): written only once the
        // record named it marked, else it waits on its record and is said.
        var marked = await Attempted(words).ConfigureAwait(false);

        OpenPrompt open;
        lock (_promptsGate)
        {
            if (passage.TurnedAway) throw new AcpTurnedAway();
            if (!marked)
            {
                _passing.Remove(passage);
                throw new AcpWordsUnmarked(AcpWordsUnmarked.Sentence);
            }

            // Open from its admission, under the write lock (Admit): the prompts open are those on the wire, in the order written.
            open = new OpenPrompt(words) { Opening = Interlocked.Increment(ref _promptCount) == 1 };
        }

        JsonElement result;
        try
        {
            result = await RequestAsync(
                "session/prompt",
                new { sessionId = _sessionId, prompt },
                ct, OnWire(open, Wrote(sent, words)), answered: frame => Answered(open, frame), admit: () => Admit(passage, open, when))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The person's stop is a VERB on this wire (D49's two endings): cancel the turn in
            // flight rather than killing the process, so the agent winds up its own work. Sent
            // here rather than from a cancellation callback, because a callback fires on whatever
            // thread cancelled — including, in the worst case, one already inside the write lock.
            // 🔴 RUNENDS1, second read (finding 3): only the person's stop, or the driver's shutdown, puts a cancel on the wire. The
            // session's own cut, as it leaves a closed quest's silent turn, stops its wait for the answer and nothing more: a cancel sent
            // then lands on whatever turn runs, a turn that spoke since the quiet was read among them.
            if (Stopped) await CancelTurnAsync().ConfigureAwait(false);
            throw;
        }

        return result.TryGetProperty("stopReason", out var reason)
            ? reason.GetString() ?? "unknown"
            : "unknown";
    }

    /// <summary>The attempts marked so far, in the order the words were asked to go (COWORK1c), and the lock that guards the chain.</summary>
    private Task _attempts = Task.CompletedTask;
    private readonly object _attemptsGate = new();

    /// <summary>
    /// A kept word's attempt, marked on its record before the wire (COWORK1c, the message-urgency design §6), after every attempt asked
    /// for before it, so two words said at once are written in the order their dispatcher handed them. Completed at once for words kept
    /// nowhere, and for the driver's own prompts. True where the word may be written: nothing marks it, or the record named it marked (FOUNDATION1
    /// row p, the design §3.4 rule 5). Never faults.
    /// </summary>
    private Task<bool> Attempted(IAgentWord? words)
    {
        if (words?.Attempting is not { } attempting) return Task.FromResult(true);
        lock (_attemptsGate)
        {
            var acknowledged = AfterAsync(_attempts, words, attempting);
            _attempts = acknowledged;
            return acknowledged;
        }

        static async Task<bool> AfterAsync(Task before, IAgentWord words, Func<Task<IReadOnlyList<string>>> attempting)
        {
            await before.ConfigureAwait(false);
            var marked = await attempting().ConfigureAwait(false);
            return words.Id is not { } id || marked.Contains(id, StringComparer.Ordinal);
        }
    }

    /// <summary>A prompt's write returned: marked on the wire (Codex's finding 3), then what the write's return tells.</summary>
    private Action OnWire(OpenPrompt open, Action? wrote) => () =>
    {
        lock (_promptsGate) open.Written = true;
        wrote?.Invoke();
    };

    /// <summary>
    /// What the write's return tells (COWORK1c, the design §7): the caller's own, then, for words under an id, their receipt
    /// <c>sent</c>, and their dispatcher, which takes a kept word off its record. <i>Sent</i> is the write's return, never the
    /// prompt's answer, which comes with the turn's end.
    /// </summary>
    private Action? Wrote(Action? sent, IAgentWord? words)
    {
        if (words?.Id is not { } id) return sent;
        return () =>
        {
            sent?.Invoke();
            Emit(new SessionEvent { Kind = SessionEventKind.Receipt, Id = id, Receipt = Receipts.Sent });
            try
            {
                words.Went?.Invoke();
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // The word went: what keeps the record is its dispatcher's, which says nothing more here.
            }
        };
    }

    /// <summary>A prompt's content: each text its own block (MSG1b), then each file a <c>resource_link</c> to where it is kept (CONV4c).</summary>
    private static object[] Blocks(IReadOnlyList<string> texts, IReadOnlyList<KeptFile>? files) =>
    [
        .. texts.Select(text => (object)new { type = "text", text }),
        .. (files ?? []).Select(file => (object)new { type = "resource_link", uri = new Uri(file.Path).AbsoluteUri, name = file.Name }),
    ];

    /// <summary>
    /// A prompt answered, on the reader's thread before the next frame is read: its turn event, as the wire's word and
    /// nothing more, and the person's words behind it taken (STEER1) — so the record reads in the order the wire said it,
    /// and what the agent does next is the words' turn.
    /// </summary>
    private void Answered(OpenPrompt open, JsonElement frame)
    {
        var answered = frame.TryGetProperty("result", out var result);
        if (answered)
        {
            // A word an error handed nothing to, answered before the agent did anything else for it: it was taken after all.
            if (open.Words is not null && TakeLate(open) is { } own) AskedAll(own);

            // A call a steer cut that never heard again ends with the turn it was cut in, as cut (STEER2a).
            CloseCut();
            // The turn's own counts, where the agent reported them (CONV5): the response's `usage`.
            var tokens = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("usage", out var usage)
                ? TurnTokens.Read(usage, "inputTokens", "outputTokens", "cachedReadTokens", "cachedWriteTokens")
                : null;
            var stopReason = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("stopReason", out var reason)
                ? reason.GetString() ?? "unknown"
                : "unknown";
            Emit(new SessionEvent
            {
                Kind = SessionEventKind.Turn,
                StopReason = stopReason,
                Tokens = tokens,
                Models = TurnStages.Of<AcpMeasure>().ModelsOf(result, tokens),
            });

            // CODEXPERM1: a turn that ends cancelled when this client never asked it to stop was cut by the agent, after a refusal if one came.
            _turnCut = stopReason == "cancelled" && !_cancelAsked ? new TurnCut(_refused.Last) : null;
            _refused.Last = null;
            _cancelAsked = false;

            // COWORK1c (the design §7), COMMS1h: the turn that took the words ended, their receipt `ended`: what Daoris saw, never an
            // answer; and only where the words were taken here.
            bool took;
            lock (_promptsGate) took = open.Taken;
            if (took && stopReason == "end_turn" && open.Words?.Id is { } word)
            {
                Emit(new SessionEvent { Kind = SessionEventKind.Receipt, Id = word, Receipt = Receipts.Ended });
            }
        }
        else
        {
            // An error answered the prompt, and the turn its updates named ends without a response: its names are not the next's.
            TurnStages.Of<AcpMeasure>().Forget();
        }

        OpenPrompt? taken = null;
        bool last;
        lock (_promptsGate)
        {
            // AGENTERRORS1a (§5.1, round 3's read, R3-O2): a word's disposition is read before its prompt leaves, from its turn. Answered
            // with an error before any update of its turn, it may or may not be in the agent's conversation: unacknowledged.
            if (_retry is not null && !answered && !open.Heard)
            {
                _unacknowledged.AddRange(open.All.Where(word => !word.IsDaoris));
                // The opening prompt's blocks (a resume's words) carry no message here: the conclusion takes their disposition from this.
                if (open.Opening) _retry.OpeningUnacknowledged = true;
            }

            // AGENTERRORS1a2, shape 2 (§4.2): a word's prompt refused after the word was taken, by its mark as the error came, never by a
            // later update; read by its mid-turn send, which makes it the turn's prompt failing.
            if (_retry is not null && !answered && open is { Taken: true, Words: { } refusedTaken }) _refusedTaken.Add(refusedTaken);
            _prompts.Remove(open);
            last = _prompts.Count == 0;
            // 🔴 USERSHOWN1: an error hands the turn to nothing. The install's record kept a word as taken at the instant its
            // door failed (ECONNRESET), the agent never having read it. The word is taken once the agent does something for
            // it (TakeLate, a conversation's door going on after the error), or is handed back as never reached.
            if (answered && _prompts.First?.Value is { Taken: false, Words: not null } head)
            {
                head.Taken = true;
                taken = head;
            }

            _afterError = !answered;
        }

        if (taken is not null) AskedAll(taken);

        // 🔴 COWORK1d (trap 3, the design §2.3): the last open prompt is answered, so the turn is over; a claim written from here on would
        // start a turn of its own, so the inbox refuses claims before the run judges its next step.
        if (last) _inbox?.LastAnswered();

        // A prompt fewer is open: the wait for a woken turn reads again whether its quiet may run (BGWAIT2), and a closed quest's quiet
        // counts from this answer (RUNENDS1).
        _wake.Answered();
    }

    /// <summary>Whether a prompt is on the wire and not yet answered (STEER1): what the agent says meanwhile is its turn's.</summary>
    private bool Prompted()
    {
        lock (_promptsGate) return _prompts.Count > 0;
    }

    /// <summary>
    /// After an error answered the prompt before it (USERSHOWN1), the next word is taken by the first thing the agent does on
    /// the wire for the session, or by its own answer: told then, before that thing is kept, so the answer never sits above the
    /// question. The words taken now, where they are <paramref name="only"/>'s (or, null, the first waiting); else null.
    /// </summary>
    private OpenPrompt? TakeLate(OpenPrompt? only = null)
    {
        lock (_promptsGate)
        {
            if (!_afterError || _prompts.First?.Value is not { Taken: false, Words: not null } head)
            {
                _afterError = false;
                return null;
            }

            if (only is not null && head != only) return null;
            _afterError = false;
            head.Taken = true;
            return head;
        }
    }

    /// <summary>Every word a prompt carries told taken, in the order said (AGENTERRORS1a: a try carries the held words in one prompt).</summary>
    private void AskedAll(OpenPrompt open)
    {
        foreach (var words in open.All) Asked(words);
    }

    /// <summary>
    /// The person's words whose prompt an error answered before any update of its turn (AGENTERRORS1a, §5.1), gathered as each failure is
    /// read, under <see cref="_promptsGate"/>: read by the episode, and said by the <c>lost</c> note.
    /// </summary>
    private readonly List<IAgentWord> _unacknowledged = [];

    /// <summary>An error answered the prompt before a word, which waits for the agent's next update to be taken (USERSHOWN1).</summary>
    private bool _afterError;

    /// <summary>
    /// The person's words sent during a turn whose own prompt an error answered after they were taken (AGENTERRORS1a2, shape 2), under
    /// <see cref="_promptsGate"/>: each read once by <see cref="MidTurnAsync"/>, by reference.
    /// </summary>
    private readonly HashSet<IAgentWord> _refusedTaken = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// A word sent during a running turn (STEER1), as <see cref="SendPromptAsync(string, IReadOnlyList{KeptFile}?, CancellationToken, Action?, IAgentWord?)"/>
    /// sends it. 🔴 AGENTERRORS1a2, shape 2 (the design §0, §4.2): where the run was handed a retry and the word's prompt was refused after
    /// the word was taken (the turn before it answered, handing the turn to it), the refusal is thrown as <see cref="AcpWordTurnFailed"/>:
    /// the turn that failed is the one the word opened, never that word's own refusal. Refused before it was taken, it is the word's, as today.
    /// </summary>
    private async Task<string> MidTurnAsync(IAgentWord words, CancellationToken ct)
    {
        try
        {
            return await SendPromptAsync(words.Prompt, words.Files, ct, sent: null, words).ConfigureAwait(false);
        }
        catch (AcpRefusal refused) when (RefusedTaken(words))
        {
            throw new AcpWordTurnFailed(refused);
        }
    }

    /// <summary>Whether this word's prompt was refused after it was taken (shape 2), read once.</summary>
    private bool RefusedTaken(IAgentWord words)
    {
        lock (_promptsGate) return _refusedTaken.Remove(words);
    }

    /// <summary>Tell the run's listener the session took the person's words; its failure costs a line, never the turn.</summary>
    private void Asked(IAgentWord words)
    {
        try
        {
            _asked?.Invoke(words);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            onLine($"[the person's words could not be recorded: {error.Message}]");
        }
    }

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
        _cancelAsked = true;
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

    void ITurnWiring.Heard()
    {
        // AGENTERRORS1a (§5.1): an update of the oldest open prompt's turn shows its words taken, whatever answers it.
        lock (_promptsGate)
        {
            if (_prompts.First?.Value is { } turn) turn.Heard = true;
        }

        if (TakeLate() is { } late) AskedAll(late);
    }

    bool ITurnWiring.Prompted() => Prompted();

    bool ITurnWiring.PersonAnswers => _personAnswers;

    bool ITurnWiring.StreamOver
    {
        get { lock (_endGate) return _streamOver; }
    }

    RefusedCalls ITurnWiring.Refused => _refused;

    void ITurnWiring.Track(JsonElement update, SessionEvent? mapped) => Track(update, mapped);

    void ITurnWiring.SayLimits(JsonElement limits) => LimitsSaid?.Invoke(limits);

    void ITurnWiring.KeepOptions(IReadOnlyList<AcpConfigOption> options) => KeepOptions(options);

    void ITurnEndWiring.Note(Noted noted) => Note(noted);

    DriverException ITurnEndWiring.StreamEnded(string sentence) => StreamEnded(sentence);

    Task<string> ITurnEndWiring.PromptingAsync(Func<Task<string>> prompt, CancellationToken ct) => PromptingAsync(prompt, ct);

    Task<string> ITurnEndWiring.SendOwnAsync(string text, CancellationToken ct, Action? sent, Func<bool>? when) =>
        SendPromptAsync(text, files: null, ct, sent, words: null, when);

    TimeSpan ITurnEndWiring.WakeQuiet => _wakeQuiet;

    bool ITurnEndWiring.DoorShut => DoorShut;

    SemaphoreSlim ITurnEndWiring.WriteLock => Wire.WriteLock;

    void ITurnEndWiring.Shut() => Shut();

    /// <summary>The turn its harness woke ended, by the adapter's word: closed as an answered turn is, with no counts, since no answer carried any.</summary>
    private void EndWokenTurn()
    {
        CloseCut();
        Emit(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = AcpWaits.WokenStop, Models = TurnStages.Of<AcpMeasure>().ModelsOf(default, null) });
    }

    void ITurnEndWiring.EndWokenTurn() => EndWokenTurn();

    private readonly RefusedCalls _refused = new();

    // Whether this client asked the turn to stop, and how the last turn was cut.
    private volatile bool _cancelAsked;
    private volatile TurnCut? _turnCut;

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

    /// <summary>
    /// Resume a conversation instead of opening one (ANSWER1a, D131 §1): the reader started, the handshake, then
    /// <c>session/resume</c> where the agent advertises it, else <c>session/load</c> where it advertises that, on the same
    /// tree, with the servers and the rules a new session would be handed, and the posture set on its answer.
    /// </summary>
    /// <remarks>
    /// <para><b><c>session/resume</c> first</b>: it resumes without replaying, and the record already holds the
    /// conversation. A load replays every message as an update before it answers; those are dropped here, so nothing is
    /// kept twice, and what the agent says after the answer is the conversation going on.</para>
    ///
    /// <para><b>Read from the adapter's own source</b> (<c>claude-agent-acp</c> 0.84.0): <c>initialize</c> answers
    /// <c>agentCapabilities.loadSession</c> and <c>agentCapabilities.sessionCapabilities.resume</c>; both requests answer
    /// <c>modes</c> and <c>configOptions</c> and no id, the id being the one asked for; a conversation not in the
    /// account's home is refused <c>resource_not_found</c>.</para>
    /// </remarks>
    /// <exception cref="AcpResumeRefused">The agent offers neither, or refused this one.</exception>
    public async Task ResumeAsync(string cwd, string conversation, CancellationToken ct, IReadOnlyList<AcpMcpServer>? servers = null)
    {
        var initialized = await HandshakeAsync(ct).ConfigureAwait(false);
        var method = ResumeMethod(initialized)
            ?? throw new AcpResumeRefused(ContinueWhy.Of(ContinueWhy.Offered), words: null);

        var offered = Offer(servers);
        var openMeta = OpenMeta(meta, offered);
        object parameters = openMeta is null
            ? new { sessionId = conversation, cwd, mcpServers = offered }
            : new { sessionId = conversation, cwd, mcpServers = offered, _meta = openMeta };

        JsonElement resumed;
        _replaying = method == "session/load" ? conversation : null;
        try
        {
            // AGENTERRORS1a2 (§5.6): a transient refusal is tried again here, the raw operation alone; past the bound it ends exhausted.
            resumed = await OpeningAsync(() => RequestAsync(method, parameters, ct), ct).ConfigureAwait(false);
        }
        catch (AcpRefusal refused)
        {
            // By the error's structure, never its words: another client holding the thread is its data's reason (MSG1b).
            var why = refused.Code == ResourceNotFound ? ContinueWhy.Gone
                : string.Equals(refused.Reason, ActiveWriter, StringComparison.Ordinal) ? ContinueWhy.Elsewhere
                : ContinueWhy.Refused;
            throw new AcpResumeRefused(ContinueWhy.Of(why), refused.Words);
        }
        finally
        {
            _replaying = null;
        }

        _sessionId = conversation;
        KeepOptions(resumed.ValueKind == JsonValueKind.Object && resumed.TryGetProperty("configOptions", out var options)
            ? AcpConfigOption.Read(options)
            : []);
        KeepOpening();
        // A resume carries its record's asked choice again: a resumed conversation keeps no model of its own (D205 §4.2; MODELROLE1a).
        var carried = await CarryAsync(ct).ConfigureAwait(false);
        // 🔴 HELPMODE1a4 (D183 point 3): set on every answer, an object or not, so a required posture is never skipped here.
        await SetPostureAsync(resumed, ct, again: carried).ConfigureAwait(false);
    }

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
