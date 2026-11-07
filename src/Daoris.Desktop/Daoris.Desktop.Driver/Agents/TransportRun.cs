using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Daoris's <see cref="AgentRun"/> over today's two transports (AGENTPROVIDER1m-i; the agent-layer design §2.4, §4.1): an
/// <see cref="AcpSession"/> held over the process's stdio on a protocol door, the native capture over its streams on a native one. It is the
/// unit a start is: the downloads it is handed kept, the process bracketed by its own lineage token, started through the agents' route and
/// tracked, a reaper beside it, the capture, a native prompt on its input, the wait for the exit and the capture bounded past it, written once
/// beneath the contract so each executor holds the run and none prepares a process itself. Every executor's run holds it (ONEWAY1h-i,
/// through <see cref="SessionHold.HoldAsync{T}"/>), a native run's next process with held words (ONEWAY1h-ii, <see cref="ResumeAsync"/> after its
/// end) and a native resume's one retry, the run's own behind <see cref="IResumeState"/>, a driven run's and a conversation's alike (m-ii,
/// D243), and a conversation's, its words through <see cref="Input"/> (ONEWAY1h-iii, the conversation's runner).
/// </summary>
/// <remarks>
/// <para><b>Admitted by the run's own evidence</b> (design §2.4): its door's wire and its resume. An operation the door's evidence does not
/// admit is answered <see cref="AgentRun.Unsupported"/> before anything is written, which <c>run.unsupported-refused</c> holds on every door.
/// A native door's words go through <see cref="Input"/>, one queue with each word's id, so a retry replays what the ended process never
/// acknowledged and never what it did; a protocol door's are <see cref="Words"/>' and its input refuses a line (<see cref="LinesAbility"/>, D246).</para>
/// <para><b>An executor's own steps between the spawn and the open.</b> A start keeps its actor, opens its watch and its inbox once its process
/// is up and before anything reads it (D205 row b, SESSWATCH1a), so such an executor spawns first (<see cref="SpawnAsync"/>), takes its steps,
/// hands the rest of its start (<see cref="HandStart"/>) and its sink, then opens; the open spawns the process itself otherwise. One that
/// composes what names its process before the spawn, the conversation's connector, brackets it first (<see cref="BracketAsync"/>).</para>
/// <para><b>One channel out</b> (row l): what the run's door says, the record's events, the console's lines, the streams beside, the
/// conversation named, the account's windows and every failure, is a <see cref="WireReading"/> written to the sink its executor composed;
/// the run writes its own transcript and names no store above it.</para>
/// <para><b>A protocol door's words</b> are <see cref="Words"/>', which the conversation's turns say; the session is left to the options its
/// agent lists.</para>
/// <para>The token <see cref="OpenAsync"/> or <see cref="ResumeAsync"/> is given governs the whole run, as the token <c>HoldAsync</c> is
/// given does: the protocol door's session is driven on it, and the native capture reads on it.</para>
/// </remarks>
internal sealed class TransportRun(ITransportHost host, ProviderRegistry adapters, AgentDoor adapter, TransportStart start) : HeldRun
{
    /// <summary>The ability a line on a protocol door's input is refused as: its words are requests on its session, <see cref="Words"/>' (D246).</summary>
    public const string LinesAbility = "a line written to a protocol door's process";

    /// <summary>The ability a later choice on the same session is refused as until <c>IChoiceReask</c> (row n).</summary>
    public const string ChoiceAbility = "a later choice on the same session before IChoiceReask (row n)";

    private TransportStart _start = start;

    // Process, its tracking and its reaper of each run, in the order made: ended in reverse, the go-on's before the first run's.
    private readonly List<IDisposable> _kept = [];

    private readonly List<Task> _captures = [];

    private IReadingSink? _sink;

    // The processes this run has started: the stamp each reading carries of the process that said it.
    private int _generation;

    // The native resume's state its one retry reads and marks (row m-ii); null for a start and for a resume handed none.
    private IResumeState? _resume;

    private RunCarriers _carriers = new();

    private HarnessWords _own = start.Own ?? new();

    // The first process's token once bracketed and until its spawn takes it, and the session it was issued for.
    private (string Proc, string SessionId)? _bracketed;

    // The first process, once spawned, and the session it was spawned for; its capture once the run opened.
    private (Process Process, string Proc, string SessionId)? _spawned;

    private Leg? _opened;

    private TextWriter? _file;

    // Set as the protocol door's transcript closes: a line said after it is dropped, never a write to a closed file.
    private bool _closed;

    private bool _disposed;

    // A go-on's process has been started: the run's end then reads that process, which no start's bounds govern.
    private bool _goneOn;

    /// <summary>
    /// The protocol door's session, once the run has opened one: what the conversation's runner reads of it beside its words, the options its
    /// agent lists and the person's change of them. Null before the open and on a native door. Its words are <see cref="Words"/>'.
    /// </summary>
    public AcpSession? Session
    {
        get => _session;
        private set
        {
            _session = value;
            _words = value is null ? null : new ProtocolWords(value);
        }
    }

    private AcpSession? _session;

    private ProtocolWords? _words;

    /// <summary>A protocol door's words and steer, once its session opened: the conversation's turns say them here and write nothing through the session (D246).</summary>
    internal override ProtocolWords? Words => _words;

    /// <summary>The process the run started last, for the code that stops or reads it beside the run; null before the spawn.</summary>
    public Process? Process { get; private set; }

    /// <summary>
    /// The lineage token of the process the run started last (COWORKVIEW1a): what the connector a driven session is offered names; null before
    /// the spawn.
    /// </summary>
    public string? Proc { get; private set; }

    /// <summary>Whether the run started its process: no refusal here ever did.</summary>
    public bool Started => Process is not null;

    /// <summary>Set where a protocol resume was refused by the agent: nothing was prompted, and this is why (ANSWER1a).</summary>
    public AcpResumeRefused? ResumeRefused { get; private set; }

    /// <summary>
    /// Whether the run admits a native go-on with held words (MSG1b): a native door that resumes, handed the target and the selection its
    /// resume is prepared from. Where it admits none, its executor holds no words for it.
    /// </summary>
    public override bool GoesOn => !IsProtocol && adapter.Can.Resumes && _start.Target is not null && _start.Selection is not null;

    private bool IsProtocol => adapter.Transport == Transport.Protocol;

    /// <summary>One run of the process: the first run's, a go-on's or a retry's, each with its own token and capture.</summary>
    private sealed class Leg(Process process, string proc, Task capture)
    {
        public Process Process { get; } = process;

        public string Proc { get; } = proc;

        /// <summary>What the exit waits for, bounded: the structured capture, the plain one, the protocol drive or the stderr pump.</summary>
        public Task Capture { get; } = capture;

        public IStreamMapper? Mapper { get; init; }

        public Task<AcpUsage?>? Structured { get; init; }

        public Task<(AcpOutcome? Outcome, string? Failure, AcpRefusal? Refusal)>? Drive { get; init; }

        /// <summary>A held session's open and both its streams to their ends, answering its usage at the high-water mark.</summary>
        public Task<AcpUsage?>? Held { get; init; }
    }

    // ---- The contract's operations, admitted by the run's own evidence ----

    public override Task OpenAsync(CancellationToken ct) => BeginAsync(conversation: null, ct);

    public override Task ResumeAsync(string conversation, CancellationToken ct, IResumeState? state = null)
    {
        if (!adapter.Can.Resumes) return Refused("a resume");
        _resume = state;
        return _opened is null ? BeginAsync(conversation, ct) : NextProcessAsync(conversation, ct);
    }

    public override Task ApplyChoiceAsync(ActorAsked choice, CancellationToken ct) => Refused(ChoiceAbility);

    internal override DoorInput Input { get; } = new(adapter.Transport == Transport.Protocol ? LinesAbility : null);

    public override async Task StopTurnAsync(CancellationToken ct)
    {
        if (!IsProtocol) throw Unsupported("a stop that keeps the conversation");
        if (Session is { } session) await session.CancelTurnAsync().ConfigureAwait(false);
    }

    public override async Task StopTaskAsync(string task, CancellationToken ct)
    {
        if (!IsProtocol) throw Unsupported("one task stopped");
        if (Session is { } session) await session.StopTaskAsync(task, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The run's end as a person finishing makes it (ONEWAY1h-iii): a protocol door's session closed, then the input ended on either door, after
    /// which the agent winds up and exits on its own, and that exit is its end. A native run whose prompt closed its input goes on to its end.
    /// </summary>
    public override async Task CloseAsync(CancellationToken ct)
    {
        if (Process is not { } process) return;
        if (Session is { } session) await session.CloseAsync(ct).ConfigureAwait(false);
        CloseInput(process);
    }

    // ---- The five members every executor uses today ----

    public override void HandSink(IReadingSink sink) => _sink = new StampedSink(sink, () => _start.Session, () => _generation, () => Proc);

    /// <summary>A reading the run's executor says beside its door's own: what a conversation's turn met that its session did not throw to the run.</summary>
    public void Read(WireReading reading) => Sink().Read(reading);

    public override void HandCarriers(RunCarriers carriers) => _carriers = carriers;

    public override async Task<AgentRunEnd> EndAsync(CancellationToken ct)
    {
        var leg = _opened ?? throw new InvalidOperationException("the run is not open: nothing started its process.");
        var exit = _goneOn
            ? await host.WaitAsync(SessionOf(), leg.Proc, leg.Process, ct, null, null, default, null).ConfigureAwait(false)
            : await host.WaitAsync(SessionOf(), leg.Proc, leg.Process, ct, _start.Minutes, _start.TimingOut, _start.Quiet, _start.QuietEnding)
                .ConfigureAwait(false);
        var end = _goneOn
            ? await ConcludedAsync(leg, exit, first: false).ConfigureAwait(false)
            : await ConcludedAsync(leg, exit, first: true).ConfigureAwait(false);

        // 🔴 The native resume's one retry (design §2.6, row m-ii, D243): a structured resume whose harness ended on a failed exit before it
        // named its conversation proves nothing of its loss, so it is tried once more as the same resume, conversation, account and access,
        // handed every word the ended process never acknowledged, and a second such end is the conclusion's. Never after a clean exit, a fresh
        // start, a refusal, a stop asked, or on the plain capture.
        if (leg.Mapper is not null && _resume is { Named: false, Refused: false, Retried: false } resume && end.Exit is int early && early != 0
            && !host.Processes.WasStopRequested(SessionOf()))
        {
            end = await RetriedAsync(resume, ct).ConfigureAwait(false);
        }

        Input.MayRetry = false;
        Input.Detach();
        return end;
    }

    /// <summary>
    /// A native run's next process after its end (MSG1b, D137 §2.1): the adapter's own resume of the kept conversation, then the settings and
    /// servers the run before it was handed, so it runs under the same rules; the words held on <see cref="Input"/> go as its one prompt, and
    /// the record opens with theirs. A conversation it names is never told to the resume this run began with, whose own words it does not carry.
    /// </summary>
    internal async Task NextProcessAsync(string conversation, CancellationToken ct)
    {
        if (!GoesOn) throw Unsupported("a go-on");

        var (target, selection) = (_start.Target!, _start.Selection!);
        var next = adapters.Resume(adapter, _start.Request! with { Resume = conversation }) ?? throw Unsupported("a resume");
        if (_carriers.Settings is not null) adapter.Spawned().HandSettings(next, _carriers.Settings);
        if (_carriers.Servers is not null) adapter.Spawned().HandServers(next, _carriers.Servers);
        var info = host.Prepared(adapters, adapter, target, selection, next);

        var sessionId = SessionOf();
        var (process, proc) = await StartProcessAsync(sessionId, info, host.Bracketed(sessionId, info, moved: null), StartClass.Person, piece: null, ct)
            .ConfigureAwait(false);
        _goneOn = true;
        _opened = Captured(process, proc, append: true, Input.Openings(), preamble: null, named: () => Input.Acknowledge(), ct);
        await AttachInputAsync(process, oneShot: true, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The process's input handed to the run's queue, which writes every word waiting for it and every one the process before it never
    /// acknowledged: a conversation's lines by the session's own door, a driven run's prompt as one write that closes its input.
    /// </summary>
    private Task<bool> AttachInputAsync(Process process, bool oneShot, CancellationToken ct) =>
        IsProtocol
            ? Task.FromResult(true)
            : Input.AttachAsync(
                oneShot
                    ? (text, token) => NativePrompt.WriteAsync(process, adapter, text, token)
                    : (line, _) => Task.FromResult(host.Processes.Send(SessionOf(), line)),
                ct);

    /// <summary>
    /// The same start once more, its transcript appended and its opening not said again (SESSCTX1c): the native resume's one retry, said on
    /// the console and marked on the resume's state before its process starts.
    /// </summary>
    private async Task<AgentRunEnd> RetriedAsync(IResumeState resume, CancellationToken ct)
    {
        var sessionId = SessionOf();

        Sink().Read(new WireReading.Shown("— its harness ended before it reopened its conversation, so the same resume is tried once more.")
        {
            By = ReadingBy.Observed,
        });
        resume.Again();
        // The first run's start again, already the launcher's where that one was (TERMTRUST1a): left as it is, and said again, under a token of
        // its own (COWORKVIEW1a).
        var (process, proc) = await StartProcessAsync(
                sessionId, _start.Info, host.Bracketed(sessionId, _start.Info, moved: null), StartClass.Person, piece: null, ct)
            .ConfigureAwait(false);
        var leg = Captured(process, proc, append: true, opening: null, preamble: null, Told, ct);
        // The retry is the run's own and the last: a word the new process cannot take is no longer kept for another. It is handed every word
        // written to the process that ended, in order, then those said while this one started (D243).
        Input.MayRetry = false;
        await AttachInputAsync(process, oneShot: _start.Prompt is not null, ct).ConfigureAwait(false);
        var exit = await host.WaitAsync(sessionId, proc, process, ct, null, null, default, null).ConfigureAwait(false);
        return await ConcludedAsync(leg, exit, first: false).ConfigureAwait(false);
    }

    /// <summary>
    /// A line the run says on its transcript and its console, as the driver's own lines are said: what an executor adds to the record's
    /// words beside the stream, a failure's and a refused resume's among them. Only the console where a native capture holds the transcript; a
    /// line said once the protocol door's transcript closed is dropped.
    /// </summary>
    public void Say(string text)
    {
        var sink = Sink();
        if (_file is { } file)
        {
            lock (file)
            {
                if (_closed) return;
                file.WriteLine(text);
            }
        }

        sink.Read(new WireReading.Shown(text) { By = ReadingBy.Observed });
    }

    // ---- The unit of a start ----

    /// <summary>
    /// The first process's start made ready before its spawn: the downloads it is handed kept on its record (KEEP1b), then its lineage token
    /// issued, its <c>start</c> written and the token on the spawn's environment, or the token its move issued (COWORKVIEW1a). For an executor
    /// that composes what names the process before it is spawned, the conversation's connector (ONEWAY1h-iii); the spawn brackets it otherwise.
    /// A run let go before its spawn lets the token go.
    /// </summary>
    internal async Task<string> BracketAsync(string sessionId, CancellationToken ct)
    {
        if (_bracketed is not null || _spawned is not null) throw new InvalidOperationException("the run's process is bracketed already.");

        // The downloads this start is handed, kept on its record before it is spawned (KEEP1b): the run is the one place a start keeps them.
        await host.KeepHandedAsync(sessionId, _start.Info, ct).ConfigureAwait(false);
        var proc = host.Bracketed(sessionId, _start.Info, _start.Moved);
        _bracketed = (proc, sessionId);
        return proc;
    }

    /// <summary>
    /// The first process up before the open: bracketed (<see cref="BracketAsync"/>) where it is not yet, started through the agents' route under
    /// its token, tracked and reaped, nothing reading it yet. For an executor whose own steps come between its spawn and its capture; the open
    /// spawns it otherwise.
    /// </summary>
    internal override async Task SpawnAsync(string sessionId, CancellationToken ct)
    {
        if (_spawned is not null) throw new InvalidOperationException("the run's process is started already.");
        if (_bracketed is null) await BracketAsync(sessionId, ct).ConfigureAwait(false);
        var (proc, bracketedFor) = _bracketed!.Value;
        if (bracketedFor != sessionId) throw new InvalidOperationException($"the run's process was bracketed for {bracketedFor}, not {sessionId}.");

        // The spawn takes the token: one it never started is let go by the spawn itself.
        _bracketed = null;
        var (process, _) = await StartProcessAsync(sessionId, _start.Info, proc, _start.Whose, _start.Piece, ct).ConfigureAwait(false);
        _spawned = (process, proc, sessionId);
    }

    /// <summary>
    /// The start as its executor completed it before the open, once its process is up or before its spawn: its protocol's session, its prompt,
    /// its wait and what its tracking says, which its own steps made. The same process's, so its spawn is the one already made or to come.
    /// </summary>
    internal override void HandStart(TransportStart completed)
    {
        if (_opened is not null) throw new InvalidOperationException("the run is open already: its start is the one it opened with.");
        if (!ReferenceEquals(completed.Info, _start.Info) || completed.Whose != _start.Whose || completed.Piece != _start.Piece
            || completed.Moved != _start.Moved)
        {
            throw new InvalidOperationException("a start handed later must start the same process as the run's.");
        }

        _start = completed;
        if (completed.Own is { } own) _own = own;
    }

    /// <summary>The start the run holds, for an executor that completes it later (<see cref="HandStart"/>).</summary>
    internal override TransportStart Start => _start;

    /// <summary>
    /// The run let go before its end (ANSWER2): as when its record will not move to working, its process ended, its <c>end</c> written unclean
    /// once its exit is read, and its capture observed, a failure there written with its reason, never left unawaited. A process spawned and
    /// never opened has no capture to observe.
    /// </summary>
    internal override async Task AbandonAsync()
    {
        if (_spawned is not { } spawned) return;
        if (_opened is { } leg) await host.AbandonedAsync(spawned.SessionId, leg.Proc, leg.Process, leg.Capture).ConfigureAwait(false);
        else await host.AbandonedAsync(spawned.SessionId, spawned.Proc, spawned.Process, Task.CompletedTask).ConfigureAwait(false);
    }

    private IReadingSink Sink() => _sink ?? throw new InvalidOperationException("the run was handed no sink: HandSink comes before the open.");

    private string SessionOf() => _start.Session ?? throw new InvalidOperationException("the run's start names no session: it is handed before the open.");

    private string TranscriptOf() =>
        _start.Transcript ?? throw new InvalidOperationException("the run's start names no transcript: it is handed before the open.");

    private static Task Refused(string ability) => Task.FromException(Unsupported(ability));

    /// <summary>The first run's and its retry's harness named its conversation: the resume's state marked, then the executor told.</summary>
    private void Told()
    {
        _resume?.Opened();
        Input.Acknowledge();
        _start.Named?.Invoke();
    }

    private async Task BeginAsync(string? conversation, CancellationToken ct)
    {
        var sessionId = SessionOf();
        Sink();
        if (_opened is not null) throw new InvalidOperationException("the run is open already.");
        if (_spawned is null) await SpawnAsync(sessionId, ct).ConfigureAwait(false);
        var (process, proc, spawnedFor) = _spawned!.Value;
        if (spawnedFor != sessionId) throw new InvalidOperationException($"the run's process was started for {spawnedFor}, not {sessionId}.");

        // A native resume whose harness may end before it names its conversation is tried once more, and its words wait for that process.
        Input.MayRetry = !IsProtocol && _resume is { Named: false, Refused: false, Retried: false } && adapter.StructuredOutput() is not null;
        _opened = IsProtocol
            ? OpenedProtocol(process, proc, conversation, ct)
            : Captured(process, proc, append: conversation is not null, _start.Opening, _start.Preamble, Told, ct);
        await AttachInputAsync(process, oneShot: _start.Prompt is not null, ct).ConfigureAwait(false);
        // FOUNDATION1f-iv part b: the write's return is where a carry-on's moved words are `sent`, never the process's start.
        if (!IsProtocol && _start.Prompt is { } prompt) await Input.SayAsync(new DoorWord(null, prompt, _start.Written), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The process under the lineage token bracketing it (COWORKVIEW1a: issued and its start written before the spawn, or the token its move
    /// issued), through the agents' route (TERMTRUST1a), tracked and reaped as every start's is (REV3, ORPHAN1).
    /// </summary>
    private async Task<(Process Process, string Proc)> StartProcessAsync(
        string sessionId, ProcessStartInfo info, string proc, StartClass whose, string? piece, CancellationToken ct)
    {
        var process = await host.SpawnedAsync(
                    proc, () => AgentStarts.StartAsync(info, piece is null ? ContainedKind.Agent : ContainedKind.Piece, whose, host.Home, host.Log, ct, piece))
                .ConfigureAwait(false)
            ?? throw new DriverException($"the {adapter.Id.Value} adapter's process did not start");
        _kept.Add(process);
        _kept.Add(host.Processes.Track(sessionId, process, _start.RefusesInput, _start.DrivesBrowser));
        _kept.Add(new Disposer(() => SessionProcesses.EndIfRunning(process)));
        Process = process;
        Proc = proc;
        Interlocked.Increment(ref _generation);
        return (process, proc);
    }

    /// <summary>
    /// The native door's capture of one run: its structured stream where the adapter reads one, else the plain text (D76, CONV3); each event
    /// told to <see cref="TransportStart.Observed"/> once the record holds it, and a conversation the stream names kept the moment it is named
    /// (ANSWER1a, MSG1c: a conversation that lives for hours keeps it before it ends, as the protocol door keeps <c>session/new</c>'s id),
    /// then told to <paramref name="named"/>.
    /// </summary>
    private Leg Captured(
        Process process, string proc, bool append, IReadOnlyList<SessionEvent>? opening, string? preamble, Action? named, CancellationToken ct)
    {
        var id = SessionOf();
        if (adapter.StructuredOutput() is { } mapper)
        {
            // Told once the sink kept the conversation it named: it opened the conversation its input carried the person's words to, so the
            // words went (MSG1b).
            var structured = HarnessCapture.CaptureStructuredAsync(
                host.Hearing(process.StandardOutput, id), host.Hearing(process.StandardError, id), TranscriptOf(), Sink(), mapper, prompt: null, ct,
                preamble, observed: _start.Observed, opened: opening, append: append, named: named, own: _own);
            _captures.Add(structured);
            return new Leg(process, proc, structured) { Mapper = mapper, Structured = structured };
        }

        var plain = HarnessCapture.CaptureAsync(
            host.Hearing(process.StandardOutput, id), host.Hearing(process.StandardError, id), TranscriptOf(), Sink(), ct, preamble);
        _captures.Add(plain);
        return new Leg(process, proc, plain);
    }

    /// <summary>
    /// The protocol door over the process (D53): an ACP session held over its stdio, the rendered updates reaching the transcript and the console,
    /// stderr beside them. Driven where the start says what the session does (<see cref="TransportProtocol.Run"/>), otherwise opened and held.
    /// Either way the open is on its way as this returns, and its outcome is the capture's.
    /// </summary>
    private Leg OpenedProtocol(Process process, string proc, string? conversation, CancellationToken ct)
    {
        var id = SessionOf();
        var sink = Sink();
        var protocol = _start.Protocol ?? throw new InvalidOperationException($"the `{adapter.Id.Value}` run was handed no protocol session to open.");
        var file = _file = AgentContext.Current.OpenTranscript(TranscriptOf(), append: conversation is not null);

        void Line(string text)
        {
            lock (file)
            {
                // A late frame after the transcript closed is dropped from it, never a throw inside the reader.
                if (_closed) return;
                file.WriteLine(text);
            }

            sink.Read(new WireReading.Shown(text));
        }

        void Event(SessionEvent e) => sink.Read(new WireReading.Said(e));

        // A fact about how the run was set up, ahead of anything else, as the native door writes it (HELPMODE1a1). Then what was asked: the
        // target the driver composed is the conversation's opening line, and what this harness is doing that Daoris could not govern, on the
        // transcript rather than swallowed (ACP3).
        if (_start.Preamble is { Length: > 0 } preamble) Line(preamble);
        var errors = HarnessCapture.PumpAsync(host.Hearing(process.StandardError, id), file, sink, ct);
        _captures.Add(errors);
        foreach (var opening in _start.Opening ?? [])
        {
            if (opening.Kind == SessionEventKind.Note && opening.Text is { } note) Line(note);
            Event(opening);
        }

        var session = new AcpSession(
            host.Hearing(process.StandardOutput, id), process.StandardInput, Line, closeTimeout: null, protocol.Posture, protocol.Meta, Event,
            streams: ReadingStreams.Of(sink), onOptions: protocol.Options,
            onConversation: named => sink.Read(new WireReading.ConversationNamed(named)))
        {
            Children = sink,
            TypedFailures = protocol.TypedFailures,
        };
        // What it says of its account's windows is a reading, as the native door's is: the sink's account part keeps it as it says it.
        session.LimitsSaid += windows => sink.Read(new WireReading.WindowsSaid(windows));
        Session = session;
        protocol.Configure?.Invoke(session);
        // A model the open refuses is a reading before its re-ask walks on: the offers store's part keeps it.
        session.ModelRefused = RefusalReading.Of(session.ModelRefused, sink);
        protocol.Ending?.Open(session);

        if (protocol.Run is not { } run)
        {
            // The conversation's shape (ONEWAY1h-iii): opened or resumed and held, its words beside it on the accessor until m-ii.
            var held = HeldAsync(process, session, conversation, protocol, errors, ct);
            _captures.Remove(errors);
            _captures.Add(held);
            return new Leg(process, proc, held) { Held = held };
        }

        var drive = DriveAsync(process, session, run, conversation, protocol, errors, Line, ct);
        _captures.Remove(errors);
        _captures.Add(drive);
        return new Leg(process, proc, drive) { Drive = drive };
    }

    /// <summary>
    /// A held session, its open to the end of both its streams (CONV3b): told once it opened, or, where its open failed, its executor told the
    /// failure and then the process ended (REV3: every failure ends the open, or the queue waits on a session that never came); the transcript
    /// closed once both streams ended, before the run is let go, so the record concluded over it reads it whole; its usage at the high-water
    /// mark, or the open's failure, as the conversation's record concludes it.
    /// </summary>
    private async Task<AcpUsage?> HeldAsync(
        Process process, AcpSession session, string? conversation, TransportProtocol protocol, Task errors, CancellationToken ct)
    {
        Exception? failed = null;
        try
        {
            // 🔴 One going on goes on in its own conversation (MSG1c, D137 §2.2): resumed by the id kept for it, never a new session, which
            // would be a new conversation under the same record.
            await ProtocolOpen.BeginAsync(session, protocol.Cwd, conversation, ct, protocol.Connectors).ConfigureAwait(false);
            protocol.Opened?.Invoke();
        }
        catch (Exception error)
        {
            failed = error;
            if (error is not OperationCanceledException) Sink().Read(new WireReading.Failed(DoorFailures.OfProtocol(error, DoorWhere.Open)));
            // A session that never opened is no session: nothing closes it, and nothing more is said to it.
            Session = null;
            protocol.Unopened?.Invoke(error);
            SessionProcesses.EndIfRunning(process);
        }

        await Task.WhenAll(errors, session.Ended).ConfigureAwait(false);
        CloseTranscript();
        session.Release();

        if (failed is not null) throw new DriverException($"the ACP session could not open: {failed.Message}");
        return session.Usage;
    }

    /// <summary>The protocol door's transcript closed, once: a line said after it is dropped.</summary>
    private void CloseTranscript()
    {
        if (_file is not { } file) return;
        lock (file)
        {
            if (_closed) return;
            _closed = true;
        }

        file.Dispose();
    }

    /// <summary>
    /// The driven session's run and its ending: a refused resume is no turn that failed, a protocol failure is the ending's failure (ACPEND1) and
    /// the refusal it carried the ending's refusal, the executor told the run ended, stdin closed so the agent winds up and exits, one lingering on
    /// its own background work ended after a bound (DONEEND1), and the stderr pump awaited last.
    /// </summary>
    private async Task<(AcpOutcome? Outcome, string? Failure, AcpRefusal? Refusal)> DriveAsync(
        Process process, AcpSession session, Func<AcpSession, string?, CancellationToken, Task<AcpOutcome>> run, string? conversation,
        TransportProtocol protocol, Task errors, Action<string> line, CancellationToken ct)
    {
        try
        {
            var outcome = await run(session, conversation, ct).ConfigureAwait(false);
            line($"— {outcome.TurnEnd}. The "
                 + "session record is concluded from the exit code and the quest's own state, not "
                 + "from this line (D46 §4).");
            return (outcome, null, null);
        }
        catch (AcpResumeRefused refused) when (conversation is not null)
        {
            // Not a turn that failed (ANSWER1a): nothing was prompted. The executor says why, and the resume ends the park.
            ResumeRefused = refused;
            return (null, null, null);
        }
        catch (DriverException error)
        {
            // A protocol failure belongs in the transcript and goes to the conclusion beside the exit (ACPEND1); the executor says it by its code.
            // FOUNDATION1g part b: the refusal as the door carried it, for a conclusion that reads its code. Its one reading on the sink (row l).
            Sink().Read(new WireReading.Failed(DoorFailures.OfProtocol(error, DoorWhere.Prompt)));
            return (null, error.Message, error as AcpRefusal ?? (error as AcpWordTurnFailed)?.Refusal);
        }
        finally
        {
            protocol.Ended?.Invoke();
            CloseInput(process);
            if (session.LeftRunning is { Count: > 0 } work && await host.EndedLingeringAsync(process, protocol.Ending).ConfigureAwait(false))
            {
                line($"— its agent had not exited {host.LingerBound.TotalSeconds:0} s after its session ended, its own background work still "
                     + $"running ({string.Join(", ", work.Select(name => $"`{name}`"))}): Daoris ended its process tree.");
            }

            await errors.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The run's end as the conclusion reads it (design §4.1): the capture bounded past the exit (SESSWATCH1a), the usage it reported, the
    /// harness's ending by its door, and whether the conversation was named; the process's lineage <c>end</c> written once both are read,
    /// before a later run's process starts (COWORKVIEW1a).
    /// </summary>
    private async Task<AgentRunEnd> ConcludedAsync(Leg leg, int? exit, bool first)
    {
        var sessionId = SessionOf();
        // 🔴 State E (SESSWATCH1a): a stray its agent started may hold the capture open past the exit; bounded, and concluded by the exit.
        var drained = await host.DrainedAsync(sessionId, leg.Proc, leg.Capture).ConfigureAwait(false);

        AcpOutcome? outcome = null;
        string? failure = null;
        AcpRefusal? refusal = null;
        AcpUsage? used = null;
        if (leg.Drive is not null && drained)
        {
            (outcome, failure, refusal) = await leg.Drive.ConfigureAwait(false);
            used = outcome?.Usage;
        }
        else if ((leg.Structured ?? leg.Held) is { } read && drained)
        {
            used = await read.ConfigureAwait(false);
        }

        // DONEEND1: an agent the run ended because it lingered after its session had ended exited as its session did, cleanly.
        if (first && _start.Protocol?.Ending?.EndedLingering == true && exit is not null) exit = 0;
        host.Ended(sessionId, leg.Proc, exit);

        var ending = Ending(IsProtocol, leg.Structured is not null, exit, failure, _own, TranscriptOf()) with
        {
            // APIREFUSAL1: the door's own word that its provider refused the turn it ended on.
            ProviderRefused = outcome?.ProviderRefused == true,
            // CODEXPERM1: its agent cut the turn it ended on.
            Cut = outcome?.Cut,
            // MODELROLE1c2: its agent refused its choice at the open, before any prompt.
            NotOffered = outcome?.NotOffered,
            // FOUNDATION1g part b: the refusal its turn ended on, read for a limit by a run with no transient retry.
            Refusal = refusal,
        };
        var named = leg.Mapper is not null ? leg.Mapper.Conversation is not null : outcome is { SessionId.Length: > 0 };
        return new AgentRunEnd(exit, used, ending, named);
    }

    /// <summary>
    /// What the harness itself said about how its run ended (AGT3c), by its door: the protocol door's failure; the native door's failed turn and
    /// the words it wrote outside its protocol and on its stderr; and for a door that carries only text, all it gives, its transcript, beside the
    /// exit.
    /// </summary>
    internal static HarnessEnding Ending(bool protocol, bool structured, int? exit, string? turnFailed, HarnessWords own, string transcript) =>
        protocol ? new HarnessEnding(exit, turnFailed, [])
        : structured ? own.Ended(exit)
        : HarnessEnding.Transcript(exit, transcript);

    private static void CloseInput(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or ObjectDisposedException)
        {
            // Already gone: the agent exited first, which is the ending this was asking for.
        }
    }

    /// <summary>
    /// Lets the run go: each process ended if still running, untracked and let go, the go-on's before the first run's (the driver's order). A
    /// capture still reading is observed and never waited for, as a start let go before its end always left it; the transcript the run holds
    /// is closed once nothing writes it. A token bracketed for a spawn that never came is let go.
    /// </summary>
    public override ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        if (_bracketed is { } unspawned) LineageTokens.Current.Unspawned(unspawned.Proc);
        _bracketed = null;
        for (var at = _kept.Count - 1; at >= 0; at--) _kept[at].Dispose();
        _kept.Clear();

        var reading = _captures.Where(each => !each.IsCompleted).ToList();
        // Observed: the run was let go before its capture was read, and its failure is the capture's own to have had.
        foreach (var capture in reading)
        {
            _ = capture.ContinueWith(
                static done => _ = done.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        if (_file is null) return ValueTask.CompletedTask;
        if (reading.Count == 0)
        {
            CloseTranscript();
            return ValueTask.CompletedTask;
        }

        _ = Task.WhenAll(reading).ContinueWith(
            _ => CloseTranscript(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return ValueTask.CompletedTask;
    }
}
