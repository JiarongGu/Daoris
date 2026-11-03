using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Daoris's <see cref="AgentRun"/> over today's two transports (AGENTPROVIDER1m-i; the agent-layer design §2.4, §4.1): an
/// <see cref="AcpSession"/> held over the process's stdio on a protocol door, the native capture over its streams on a native one. It is the
/// unit a start is: the downloads it is handed kept, the process bracketed by its own lineage token, started through the agents' route and
/// tracked, a reaper beside it, the capture, a native prompt on its input, the wait for the exit and the capture bounded past it, written once
/// beneath the contract so each executor holds the run and none prepares a process itself. Every executor's run holds it (ONEWAY1h-i,
/// through <c>Driver.HoldAsync</c>), a native run's go-on with held words and its resume's one retry too (ONEWAY1h-ii, <see cref="GoOnAsync"/>
/// and <see cref="RestartAsync"/>).
/// </summary>
/// <remarks>
/// <para><b>Admitted by the run's own evidence</b> (design §2.4): its door's wire and its resume. An operation the door's evidence does not
/// admit is answered <see cref="AgentRun.Unsupported"/> before anything is written, which <c>run.unsupported-refused</c> holds on every door.
/// The words' operations are not admitted until m-ii (<see cref="WordsAbility"/>): today's word paths stay beneath the run, on the session and
/// the process it holds, so no executor calls <see cref="SendAsync"/> or <see cref="SteerAsync"/>.</para>
/// <para><b>An executor's own steps between the spawn and the open.</b> A start keeps its actor, opens its watch and its inbox once its process
/// is up and before anything reads it (D205 row b, SESSWATCH1a), so such an executor spawns first (<see cref="SpawnAsync"/>), takes its steps,
/// hands the rest of its start (<see cref="HandStart"/>) and its records, then opens; the open spawns the process itself otherwise.</para>
/// <para><b>What is deleted later.</b> <see cref="Session"/> is the conversation's runner's alone, until m-ii moves its words behind
/// <c>DoorInput</c>; <c>LayerRun</c> refuses a conversation before then. <see cref="HandRecords"/> goes at row l, <see cref="GoOnAsync"/> and
/// <see cref="RestartAsync"/> at m-ii.</para>
/// <para>The token <see cref="OpenAsync"/> or <see cref="ResumeAsync"/> is given governs the whole run, as the token <c>HoldAsync</c> is
/// given does: the protocol door's session is driven on it, and the native capture reads on it.</para>
/// </remarks>
internal sealed class TransportRun(ITransportHost host, ISessionAdapter adapter, TransportStart start) : AgentRun, IAsyncDisposable
{
    /// <summary>The ability a word operation is refused as until m-ii's <c>DoorInput</c> carries a word's id (design §4.1).</summary>
    public const string WordsAbility = "words through the run before DoorInput (row m-ii)";

    /// <summary>The ability a later choice on the same session is refused as until <c>IChoiceReask</c> (row n).</summary>
    public const string ChoiceAbility = "a later choice on the same session before IChoiceReask (row n)";

    private TransportStart _start = start;

    // Process, its tracking and its reaper of each run, in the order made: ended in reverse, the go-on's before the first run's.
    private readonly List<IDisposable> _kept = [];

    private readonly List<Task> _captures = [];

    private RunRecords? _records;

    private RunCarriers _carriers = new();

    private HarnessWords _own = new();

    // The first process, once spawned, and the session it was spawned for; its capture once the run opened.
    private (Process Process, string Proc, string SessionId)? _spawned;

    private Leg? _opened;

    private TextWriter? _file;

    private bool _disposed;

    /// <summary>
    /// The protocol door's session, once the run has opened one: the conversation's runner reaches it through this accessor alone (design §4.1,
    /// round 5), for the words it frames and writes itself. Null before the open and on a native door. Deleted at m-ii.
    /// </summary>
    public AcpSession? Session { get; private set; }

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
    public bool GoesOn => !IsProtocol && adapter.Resumes && _start.Target is not null && _start.Selection is not null;

    private bool IsProtocol => adapter.Wire == SessionWire.Acp;

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
    }

    // ---- The contract's operations, admitted by the run's own evidence ----

    public override Task OpenAsync(CancellationToken ct) => BeginAsync(conversation: null, ct);

    public override Task ResumeAsync(string conversation, CancellationToken ct) =>
        adapter.Resumes ? BeginAsync(conversation, ct) : Refused("a resume");

    public override Task ApplyChoiceAsync(ActorAsked choice, CancellationToken ct) => Refused(ChoiceAbility);

    public override Task SendAsync(string text, CancellationToken ct) => Refused(WordsAbility);

    public override Task SteerAsync(string text, CancellationToken ct) => Refused(WordsAbility);

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

    public override async Task CloseAsync(CancellationToken ct)
    {
        if (Process is not { } process) return;
        if (Session is { } session)
        {
            await session.CloseAsync(ct).ConfigureAwait(false);
            CloseInput(process);
            return;
        }

        SessionProcesses.EndIfRunning(process);
    }

    // ---- The five members every executor uses today ----

    public override void HandRecords(RunRecords records)
    {
        _records = records;
        _own = records.Own ?? new HarnessWords();
    }

    public override void HandCarriers(RunCarriers carriers) => _carriers = carriers;

    public override async Task<AgentRunEnd> EndAsync(CancellationToken ct)
    {
        var leg = _opened ?? throw new InvalidOperationException("the run is not open: nothing started its process.");
        var exit = await host.WaitAsync(Records().SessionId, leg.Proc, leg.Process, ct, _start.Minutes, _start.TimingOut, _start.Quiet, _start.QuietEnding)
            .ConfigureAwait(false);
        return await ConcludedAsync(leg, exit, first: true).ConfigureAwait(false);
    }

    public override Task<AgentRunEnd> GoOnAsync(string conversation, string prompt, IReadOnlyList<SessionEvent> opening, CancellationToken ct) =>
        GoOnAsync(conversation, prompt, opening, started: null, ct);

    /// <summary>
    /// The go-on, its executor told once its process is up and before anything reads it (<paramref name="started"/>): where the words it carries
    /// are sent (COWORK1c), and the line between a go-on that never started and one that did. A conversation it names is kept, and never told
    /// to the resume this run began with (<see cref="TransportStart.Named"/>), whose own words it does not carry.
    /// </summary>
    internal async Task<AgentRunEnd> GoOnAsync(
        string conversation, string prompt, IReadOnlyList<SessionEvent> opening, Action? started, CancellationToken ct)
    {
        if (!GoesOn) throw Unsupported("a go-on");

        // The adapter's own resume of the kept conversation, then the settings and servers the run before it was handed, so it runs under the
        // same rules; the words go on its input.
        var (target, selection) = (_start.Target!, _start.Selection!);
        var next = adapter.PrepareResume(target, host.Command(adapter.Name), conversation) ?? throw Unsupported("a resume");
        if (_carriers.Settings is not null) adapter.HandSettings(next, _carriers.Settings);
        if (_carriers.Servers is not null) adapter.HandServers(next, _carriers.Servers);
        var info = host.Prepared(adapter, target, selection, next);

        var (process, proc) = await StartProcessAsync(Records().SessionId, info, StartClass.Person, piece: null, moved: null, ct).ConfigureAwait(false);
        started?.Invoke();
        var leg = Captured(process, proc, append: true, opening, preamble: null, named: null, ct);
        await PromptedAsync(process, prompt, ct).ConfigureAwait(false);
        var exit = await host.WaitAsync(Records().SessionId, proc, process, ct, null, null, default, null).ConfigureAwait(false);
        return await ConcludedAsync(leg, exit, first: false).ConfigureAwait(false);
    }

    public override async Task<AgentRunEnd> RestartAsync(CancellationToken ct)
    {
        if (IsProtocol || _start.Again is not { } again) throw Unsupported("a restart");
        var records = Records();

        records.Output?.Append(records.SessionId, "— its harness ended before it reopened its conversation, so the same resume is tried once more.");
        again();
        // The first run's start again, already the launcher's where that one was (TERMTRUST1a): left as it is, and said again, under a token of
        // its own (COWORKVIEW1a).
        var (process, proc) = await StartProcessAsync(records.SessionId, _start.Info, StartClass.Person, piece: null, moved: null, ct).ConfigureAwait(false);
        var leg = Captured(process, proc, append: true, opening: null, preamble: null, _start.Named, ct);
        if (_start.Prompt is { } prompt) await PromptedAsync(process, prompt, ct).ConfigureAwait(false);
        var exit = await host.WaitAsync(records.SessionId, proc, process, ct, null, null, default, null).ConfigureAwait(false);
        return await ConcludedAsync(leg, exit, first: false).ConfigureAwait(false);
    }

    /// <summary>
    /// A line the run says on its transcript and its console, as the driver's own lines are said: what an executor adds to the record's
    /// words beside the stream, a failure's and a refused resume's among them. Only the console where a native capture holds the transcript.
    /// </summary>
    public void Say(string text)
    {
        var records = Records();
        if (_file is { } file)
        {
            lock (file) file.WriteLine(text);
        }

        records.Output?.Append(records.SessionId, text);
    }

    // ---- The unit of a start ----

    /// <summary>
    /// The first process up before the open: the downloads its start is handed kept on its record (KEEP1b), its lineage bracketed, started
    /// through the agents' route, tracked and reaped, nothing reading it yet. For an executor whose own steps come between its spawn and its
    /// capture; the open spawns it otherwise.
    /// </summary>
    internal async Task SpawnAsync(string sessionId, CancellationToken ct)
    {
        if (_spawned is not null) throw new InvalidOperationException("the run's process is started already.");

        // The downloads this start is handed, kept on its record before it is spawned (KEEP1b): the run's spawn is the one place a start keeps them.
        await host.KeepHandedAsync(sessionId, _start.Info, ct).ConfigureAwait(false);
        var (process, proc) = await StartProcessAsync(sessionId, _start.Info, _start.Whose, _start.Piece, _start.Moved, ct).ConfigureAwait(false);
        _spawned = (process, proc, sessionId);
    }

    /// <summary>
    /// The start as its executor completed it once its process is up, before the open: its protocol's session, its prompt and its wait, which
    /// its own steps since the spawn made. The same process's, so its spawn is the one already made.
    /// </summary>
    internal void HandStart(TransportStart completed)
    {
        if (_opened is not null) throw new InvalidOperationException("the run is open already: its start is the one it opened with.");
        if (!ReferenceEquals(completed.Info, _start.Info) || completed.Whose != _start.Whose || completed.Piece != _start.Piece
            || completed.Moved != _start.Moved)
        {
            throw new InvalidOperationException("a start handed later must start the same process as the run's.");
        }

        _start = completed;
    }

    /// <summary>
    /// The run let go before its end (ANSWER2): as when its record will not move to working, its process ended, its <c>end</c> written unclean
    /// once its exit is read, and its capture observed, a failure there written with its reason, never left unawaited.
    /// </summary>
    internal async Task AbandonAsync()
    {
        if (_opened is not { } leg) return;
        await host.AbandonedAsync(Records().SessionId, leg.Proc, leg.Process, leg.Capture).ConfigureAwait(false);
    }

    private RunRecords Records() => _records ?? throw new InvalidOperationException("the run was handed no records: HandRecords comes before the open.");

    private static Task Refused(string ability) => Task.FromException(Unsupported(ability));

    private async Task BeginAsync(string? conversation, CancellationToken ct)
    {
        var records = Records();
        if (_opened is not null) throw new InvalidOperationException("the run is open already.");
        if (_spawned is null) await SpawnAsync(records.SessionId, ct).ConfigureAwait(false);
        var (process, proc, spawnedFor) = _spawned!.Value;
        if (spawnedFor != records.SessionId) throw new InvalidOperationException($"the run's process was started for {spawnedFor}, not {records.SessionId}.");

        _opened = IsProtocol
            ? await OpenedProtocolAsync(process, proc, conversation, ct).ConfigureAwait(false)
            : Captured(process, proc, append: conversation is not null, records.Opening, records.Preamble, _start.Named, ct);
        // FOUNDATION1f-iv part b: the write's return is where a carry-on's moved words are `sent`, never the process's start.
        if (_start.Prompt is { } prompt && await PromptedAsync(process, prompt, ct).ConfigureAwait(false)) _start.Written?.Invoke();
    }

    /// <summary>
    /// The process, its lineage bracketed (COWORKVIEW1a: its token issued and its start written before the spawn, or the token its move issued),
    /// through the agents' route (TERMTRUST1a), tracked and reaped as every start's is (REV3, ORPHAN1).
    /// </summary>
    private async Task<(Process Process, string Proc)> StartProcessAsync(
        string sessionId, ProcessStartInfo info, StartClass whose, string? piece, string? moved, CancellationToken ct)
    {
        var proc = host.Bracketed(sessionId, info, moved);
        var process = await host.SpawnedAsync(
                    proc, () => AgentStarts.StartAsync(info, piece is null ? ContainedKind.Agent : ContainedKind.Piece, whose, host.Home, host.Log, ct, piece))
                .ConfigureAwait(false)
            ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
        _kept.Add(process);
        _kept.Add(host.Processes.Track(sessionId, process, _start.RefusesInput, _start.DrivesBrowser));
        _kept.Add(new Disposer(() => SessionProcesses.EndIfRunning(process)));
        Process = process;
        Proc = proc;
        return (process, proc);
    }

    /// <summary>A native harness's prompt on its input, once its output is being read, then closed (AGENTLYNTAI1s-i); whether the write returned.</summary>
    private Task<bool> PromptedAsync(Process process, string prompt, CancellationToken ct) => NativePrompt.WriteAsync(process, adapter, prompt, ct);

    /// <summary>
    /// The native door's capture of one run: its structured stream where the adapter reads one, else the plain text (D76, CONV3); a
    /// conversation the stream names told to <paramref name="named"/>.
    /// </summary>
    private Leg Captured(
        Process process, string proc, bool append, IReadOnlyList<SessionEvent>? opening, string? preamble, Action? named, CancellationToken ct)
    {
        var records = Records();
        var id = records.SessionId;
        if (adapter.StructuredOutput() is { } mapper)
        {
            var structured = KeepingAsync(
                mapper,
                HarnessCapture.CaptureStructuredAsync(
                    host.Hearing(process.StandardOutput, id), host.Hearing(process.StandardError, id), records.Transcript, id, records.Output,
                    records.Events, mapper, prompt: null, ct, preamble, opened: opening, append: append, said: records.Said, own: _own,
                    lineage: records.LineageHome is { } home ? LineageStreams.Current.Writer(home, id, proc) : null),
                id, records.KeepAs, named);
            _captures.Add(structured);
            return new Leg(process, proc, structured) { Mapper = mapper, Structured = structured };
        }

        var plain = HarnessCapture.CaptureAsync(
            host.Hearing(process.StandardOutput, id), host.Hearing(process.StandardError, id), records.Transcript, id, records.Output, ct, preamble);
        _captures.Add(plain);
        return new Leg(process, proc, plain);
    }

    /// <summary>
    /// The native door's conversation id, once its output has ended (ANSWER1a): kept for a quest's session and told to a resume, whose run that
    /// never named one ended before the harness opened its conversation.
    /// </summary>
    private async Task<AcpUsage?> KeepingAsync(IStreamMapper mapper, Task<AcpUsage?> capture, string sessionId, string? keepAs, Action? named)
    {
        var usage = await capture.ConfigureAwait(false);
        if (mapper.Conversation is { } conversation)
        {
            if (keepAs is not null) host.KeepConversation(sessionId, keepAs, conversation);
            // It opened the conversation its input carried the person's words to, so the words went (MSG1b).
            named?.Invoke();
        }

        return usage;
    }

    /// <summary>
    /// The protocol door over the process (D53): an ACP session held over its stdio, the rendered updates reaching the transcript and the console,
    /// stderr beside them. Driven where the start says what the session does (<see cref="TransportProtocol.Run"/>), otherwise opened and held.
    /// </summary>
    private async Task<Leg> OpenedProtocolAsync(Process process, string proc, string? conversation, CancellationToken ct)
    {
        var records = Records();
        var id = records.SessionId;
        var protocol = _start.Protocol ?? throw new InvalidOperationException($"the `{adapter.Name}` run was handed no protocol session to open.");
        var file = _file = AgentContext.Current.OpenTranscript(records.Transcript, append: conversation is not null);
        var errors = HarnessCapture.PumpAsync(host.Hearing(process.StandardError, id), file, id, records.Output, ct);
        _captures.Add(errors);

        void Line(string text)
        {
            lock (file) file.WriteLine(text);
            records.Output?.Append(id, text);
        }

        void Event(SessionEvent e) => records.Events?.Keep(id, e, Line);

        // What was asked, first: the target the driver composed is the conversation's opening line, then what this harness is doing that Daoris
        // could not govern, on the transcript rather than swallowed (ACP3).
        foreach (var opening in records.Opening ?? [])
        {
            if (opening.Kind == SessionEventKind.Note && opening.Text is { } note) Line(note);
            Event(opening);
        }

        if (records.Preamble is { Length: > 0 } preamble)
        {
            Line(preamble);
            Event(new SessionEvent { Kind = SessionEventKind.Note, Text = preamble });
        }

        var session = new AcpSession(
            host.Hearing(process.StandardOutput, id), process.StandardInput, Line, closeTimeout: null, protocol.Posture, protocol.Meta, Event,
            streams: records.Output?.ForSession(id),
            onConversation: records.KeepAs is not { } keepAs ? null : named => host.KeepConversation(id, keepAs, named))
        {
            Lineage = records.LineageHome is { } home ? LineageStreams.Current.Writer(home, id, proc) : null,
            TypedFailures = protocol.TypedFailures,
        };
        Session = session;
        protocol.Configure?.Invoke(session);
        protocol.Ending?.Open(session);

        if (protocol.Run is not { } run)
        {
            // The conversation's shape: opened or resumed and held, its words beside it on the accessor. A refusal or a failure is the open's,
            // thrown to the caller as the conversation's runner meets it today (ChatRunner's open), the process ended with the run.
            if (conversation is null) await session.OpenAsync(protocol.Cwd, ct, protocol.Servers).ConfigureAwait(false);
            else await session.ResumeAsync(protocol.Cwd, conversation, ct, protocol.Servers).ConfigureAwait(false);
            return new Leg(process, proc, errors);
        }

        var drive = DriveAsync(process, session, run, conversation, protocol, errors, Line, ct);
        _captures.Remove(errors);
        _captures.Add(drive);
        return new Leg(process, proc, drive) { Drive = drive };
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
            // FOUNDATION1g part b: the refusal as the door carried it, for a conclusion that reads its code.
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
        var records = Records();
        // 🔴 State E (SESSWATCH1a): a stray its agent started may hold the capture open past the exit; bounded, and concluded by the exit.
        var drained = await host.DrainedAsync(records.SessionId, leg.Proc, leg.Capture).ConfigureAwait(false);

        AcpOutcome? outcome = null;
        string? failure = null;
        AcpRefusal? refusal = null;
        AcpUsage? used = null;
        if (leg.Drive is not null && drained)
        {
            (outcome, failure, refusal) = await leg.Drive.ConfigureAwait(false);
            used = outcome?.Usage;
        }
        else if (leg.Structured is not null && drained)
        {
            used = await leg.Structured.ConfigureAwait(false);
        }

        // DONEEND1: an agent the run ended because it lingered after its session had ended exited as its session did, cleanly.
        if (first && _start.Protocol?.Ending?.EndedLingering == true && exit is not null) exit = 0;
        host.Ended(records.SessionId, leg.Proc, exit);

        var ending = Ending(IsProtocol, leg.Structured is not null, exit, failure, _own, records.Transcript) with
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
    /// is closed once nothing writes it.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
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

        if (_file is not { } file) return;
        if (reading.Count == 0)
        {
            await file.DisposeAsync().ConfigureAwait(false);
            return;
        }

        _ = Task.WhenAll(reading).ContinueWith(
            _ => file.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
