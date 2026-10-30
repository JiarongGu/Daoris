using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Daoris's <see cref="AgentRun"/> over today's two transports (AGENTPROVIDER1m-i part 1; the agent-layer design §2.4, §4.1): an
/// <see cref="AcpSession"/> held over the process's stdio on a protocol door, the native capture over its streams on a native one. It is the
/// unit a start is today: the process started through the agents' route and tracked, a reaper beside it, the capture, the wait for the exit
/// and the capture bounded past it (<c>Driver.HoldAsync</c>; the go-on and the retry of <c>DriverContinuations.GoOnAsync</c> and
/// <c>AgainAsync</c>), written once beneath the contract so each executor holds the run and none prepares a process itself.
/// </summary>
/// <remarks>
/// <para><b>Admitted by the run's own evidence</b> (design §2.4): its door's wire and its resume. An operation the door's evidence does not
/// admit is answered <see cref="AgentRun.Unsupported"/> before anything is written, which <c>run.unsupported-refused</c> holds on every door.
/// The words' operations are not admitted until m-ii (<see cref="WordsAbility"/>): today's word paths stay beneath the run, on the session and
/// the process it holds, so no executor calls <see cref="SendAsync"/> or <see cref="SteerAsync"/>.</para>
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

    // Process, its tracking and its reaper of each run, in the order made: ended in reverse, the go-on's before the first run's.
    private readonly List<IDisposable> _kept = [];

    private readonly List<Task> _captures = [];

    private RunRecords? _records;

    private RunCarriers _carriers = new();

    private HarnessWords _own = new();

    private Leg? _opened;

    private TextWriter? _file;

    private Task? _errors;

    private bool _disposed;

    /// <summary>
    /// The protocol door's session, once the run has opened one: the conversation's runner reaches it through this accessor alone (design §4.1,
    /// round 5), for the words it frames and writes itself. Null before the open and on a native door. Deleted at m-ii.
    /// </summary>
    public AcpSession? Session { get; private set; }

    /// <summary>The process the run started last, for the code that stops or reads it beside the run; null before the open.</summary>
    public Process? Process { get; private set; }

    /// <summary>Whether the run started its process: no refusal here ever did.</summary>
    public bool Started => Process is not null;

    /// <summary>Set where a protocol resume was refused by the agent: nothing was prompted, and this is why (ANSWER1a).</summary>
    public AcpResumeRefused? ResumeRefused { get; private set; }

    private bool IsProtocol => adapter.Wire == SessionWire.Acp;

    /// <summary>One run of the process: the first run's, a go-on's or a retry's, each with its own capture.</summary>
    private sealed class Leg(Process process, Task capture)
    {
        public Process Process { get; } = process;

        /// <summary>What the exit waits for, bounded: the structured capture, the plain one, the protocol drive or the stderr pump.</summary>
        public Task Capture { get; } = capture;

        public IStreamMapper? Mapper { get; init; }

        public Task<AcpUsage?>? Structured { get; init; }

        public Task<(AcpOutcome? Outcome, string? Failure)>? Drive { get; init; }
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
        var exit = await host.WaitAsync(leg.Process, ct, start.Minutes, start.TimingOut, start.Quiet, start.QuietEnding).ConfigureAwait(false);
        return await ConcludedAsync(leg, exit, first: true).ConfigureAwait(false);
    }

    public override async Task<AgentRunEnd> GoOnAsync(string conversation, string prompt, IReadOnlyList<SessionEvent> opening, CancellationToken ct)
    {
        if (IsProtocol || !adapter.Resumes || start.Target is not { } target || start.Selection is not { } selection) throw Unsupported("a go-on");

        // The adapter's own resume of the kept conversation, the words its prompt, then the settings and servers the run before it was handed, so it
        // runs under the same rules (twin of DriverContinuations.GoOnStart; the executors' move deletes it).
        var next = adapter.PrepareResume(target, host.Command(adapter.Name), conversation, prompt) ?? throw Unsupported("a resume");
        if (_carriers.Settings is not null) adapter.HandSettings(next, _carriers.Settings);
        if (_carriers.Servers is not null) adapter.HandServers(next, _carriers.Servers);
        var info = host.Prepared(adapter, target, selection, next);

        var process = await StartProcessAsync(info, StartClass.Person, piece: null, ct).ConfigureAwait(false);
        var leg = Captured(process, append: true, opening, preamble: null, ct);
        var exit = await host.WaitAsync(process, ct, null, null, default, null).ConfigureAwait(false);
        return await ConcludedAsync(leg, exit, first: false).ConfigureAwait(false);
    }

    public override async Task<AgentRunEnd> RestartAsync(CancellationToken ct)
    {
        if (IsProtocol || start.Again is not { } again) throw Unsupported("a restart");
        var records = Records();

        records.Output?.Append(records.SessionId, "— its harness ended before it reopened its conversation, so the same resume is tried once more.");
        again();
        // The first run's start again, already the launcher's where that one was (TERMTRUST1a): left as it is, and said again.
        var process = await StartProcessAsync(start.Info, StartClass.Person, piece: null, ct).ConfigureAwait(false);
        var leg = Captured(process, append: true, opening: null, preamble: null, ct);
        var exit = await host.WaitAsync(process, ct, null, null, default, null).ConfigureAwait(false);
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

    private RunRecords Records() => _records ?? throw new InvalidOperationException("the run was handed no records: HandRecords comes before the open.");

    private static Task Refused(string ability) => Task.FromException(Unsupported(ability));

    private async Task BeginAsync(string? conversation, CancellationToken ct)
    {
        var records = Records();
        if (Process is not null) throw new InvalidOperationException("the run is open already.");

        // The downloads this start is handed, kept on its record before it is spawned (KEEP1b), as each start's caller keeps them today.
        await host.KeepHandedAsync(records.SessionId, start.Info, ct).ConfigureAwait(false);
        var process = await StartProcessAsync(start.Info, start.Whose, start.Piece, ct).ConfigureAwait(false);
        _opened = IsProtocol
            ? await OpenedProtocolAsync(process, conversation, ct).ConfigureAwait(false)
            : Captured(process, append: conversation is not null, records.Opening, records.Preamble, ct);
    }

    /// <summary>The process, through the agents' route (TERMTRUST1a), tracked and reaped as every start's is (REV3, ORPHAN1).</summary>
    private async Task<Process> StartProcessAsync(ProcessStartInfo info, StartClass whose, string? piece, CancellationToken ct)
    {
        var sessionId = Records().SessionId;
        var process = await AgentStarts.StartAsync(info, piece is null ? ContainedKind.Agent : ContainedKind.Piece, whose, host.Home, host.Log, ct, piece)
                .ConfigureAwait(false)
            ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
        _kept.Add(process);
        _kept.Add(host.Processes.Track(sessionId, process, start.RefusesInput, start.DrivesBrowser));
        _kept.Add(new Disposer(() => SessionProcesses.EndIfRunning(process)));
        Process = process;
        return process;
    }

    /// <summary>The native door's capture of one run: its structured stream where the adapter reads one, else the plain text (D76, CONV3).</summary>
    private Leg Captured(Process process, bool append, IReadOnlyList<SessionEvent>? opening, string? preamble, CancellationToken ct)
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
                    lineage: records.LineageHome is { } home ? LineageStreams.Current.Writer(home, id) : null),
                id, records.KeepAs);
            _captures.Add(structured);
            return new Leg(process, structured) { Mapper = mapper, Structured = structured };
        }

        var plain = HarnessCapture.CaptureAsync(
            host.Hearing(process.StandardOutput, id), host.Hearing(process.StandardError, id), records.Transcript, id, records.Output, ct, preamble);
        _captures.Add(plain);
        return new Leg(process, plain);
    }

    /// <summary>
    /// The native door's conversation id, once its output has ended (ANSWER1a): kept for a quest's session and told to a resume, whose run that
    /// never named one ended before the harness opened its conversation (twin of <c>Driver.KeepingAsync</c>).
    /// </summary>
    private async Task<AcpUsage?> KeepingAsync(IStreamMapper mapper, Task<AcpUsage?> capture, string sessionId, string? keepAs)
    {
        var usage = await capture.ConfigureAwait(false);
        if (mapper.Conversation is { } conversation)
        {
            if (keepAs is not null) host.KeepConversation(sessionId, keepAs, conversation);
            // It opened the conversation its argument carried the person's words to, so the words went (MSG1b).
            start.Named?.Invoke();
        }

        return usage;
    }

    /// <summary>
    /// The protocol door over the process (D53): an ACP session held over its stdio, the rendered updates reaching the transcript and the console,
    /// stderr beside them. Driven where the start says what the session does (<see cref="TransportProtocol.Run"/>), otherwise opened and held.
    /// </summary>
    private async Task<Leg> OpenedProtocolAsync(Process process, string? conversation, CancellationToken ct)
    {
        var records = Records();
        var id = records.SessionId;
        var protocol = start.Protocol ?? throw new InvalidOperationException($"the `{adapter.Name}` run was handed no protocol session to open.");
        var file = _file = AgentContext.Current.OpenTranscript(records.Transcript, append: conversation is not null);
        var errors = _errors = HarnessCapture.PumpAsync(host.Hearing(process.StandardError, id), file, id, records.Output, ct);
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
            Lineage = records.LineageHome is { } home ? LineageStreams.Current.Writer(home, id) : null,
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
            return new Leg(process, errors);
        }

        var drive = DriveAsync(process, session, run, conversation, protocol.Ending, errors, Line, ct);
        _captures.Add(drive);
        return new Leg(process, drive) { Drive = drive };
    }

    /// <summary>
    /// The driven session's run and its ending, as <c>Driver.CaptureAcpAsync</c> ends it: a refused resume is no turn that failed, a protocol
    /// failure is the ending's failure (ACPEND1), stdin is closed so the agent winds up and exits, one lingering on its own background work is
    /// ended after a bound (DONEEND1), and the stderr pump is awaited last.
    /// </summary>
    private async Task<(AcpOutcome? Outcome, string? Failure)> DriveAsync(
        Process process, AcpSession session, Func<AcpSession, string?, CancellationToken, Task<AcpOutcome>> run, string? conversation,
        AcpEnding? ending, Task errors, Action<string> line, CancellationToken ct)
    {
        try
        {
            var outcome = await run(session, conversation, ct).ConfigureAwait(false);
            line($"— the turn ended: {outcome.StopReason}, after {outcome.Updates} update(s). The "
                 + "session record is concluded from the exit code and the quest's own state, not "
                 + "from this line (D46 §4).");
            return (outcome, null);
        }
        catch (AcpResumeRefused refused) when (conversation is not null)
        {
            // Not a turn that failed (ANSWER1a): nothing was prompted. The executor says why, and the resume ends the park.
            ResumeRefused = refused;
            return (null, null);
        }
        catch (DriverException error)
        {
            // A protocol failure belongs in the transcript and goes to the conclusion beside the exit (ACPEND1); the executor says it by its code.
            return (null, error.Message);
        }
        finally
        {
            CloseInput(process);
            if (session.LeftRunning is { Count: > 0 } work && await host.EndedLingeringAsync(process, ending).ConfigureAwait(false))
            {
                line($"— its agent had not exited {host.LingerBound.TotalSeconds:0} s after its session ended, its own background work still "
                     + $"running ({string.Join(", ", work.Select(name => $"`{name}`"))}): Daoris ended its process tree.");
            }

            await errors.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The run's end as the conclusion reads it (design §4.1): the capture bounded past the exit (SESSWATCH1a), the usage it reported, the
    /// harness's ending by its door, and whether the conversation was named.
    /// </summary>
    private async Task<AgentRunEnd> ConcludedAsync(Leg leg, int? exit, bool first)
    {
        var records = Records();
        // 🔴 State E (SESSWATCH1a): a stray its agent started may hold the capture open past the exit; bounded, and concluded by the exit.
        var drained = await host.CaptureWithinAsync(leg.Capture, records.SessionId).ConfigureAwait(false);

        AcpOutcome? outcome = null;
        string? failure = null;
        AcpUsage? used = null;
        if (leg.Drive is not null && drained)
        {
            (outcome, failure) = await leg.Drive.ConfigureAwait(false);
            used = outcome?.Usage;
        }
        else if (leg.Structured is not null && drained)
        {
            used = await leg.Structured.ConfigureAwait(false);
        }

        // DONEEND1: an agent the run ended because it lingered after its session had ended exited as its session did, cleanly.
        if (first && start.Protocol?.Ending?.EndedLingering == true && exit is not null) exit = 0;

        var ending = Ending(IsProtocol, leg.Structured is not null, exit, failure, _own, records.Transcript) with
        {
            // APIREFUSAL1: the door's own word that its provider refused the turn it ended on.
            ProviderRefused = outcome?.ProviderRefused == true,
            // MODELROLE1c2: its agent refused its choice at the open, before any prompt.
            NotOffered = outcome?.NotOffered,
        };
        var named = leg.Mapper is not null ? leg.Mapper.Conversation is not null : outcome is { SessionId.Length: > 0 };
        return new AgentRunEnd(exit, used, ending, named);
    }

    /// <summary>
    /// What the harness itself said about how its run ended (AGT3c), by its door: the protocol door's failure; the native door's failed turn and
    /// the words it wrote outside its protocol and on its stderr; and for a door that carries only text, all it gives, its transcript, beside the
    /// exit. The twin of <c>Driver.Ending</c>, which the executors' move deletes.
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
    /// Lets the run go: each process ended if still running, untracked and let go, the go-on's before the first run's (the driver's order); then
    /// the captures observed, bounded as a stray's hold is (SESSWATCH1a), and the transcript closed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        for (var at = _kept.Count - 1; at >= 0; at--) _kept[at].Dispose();
        _kept.Clear();

        foreach (var capture in _captures.Where(each => !each.IsCompleted))
        {
            try
            {
                await host.CaptureWithinAsync(capture, _records?.SessionId ?? "").ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // Observed: the run was let go before its capture was read, and its failure is the capture's own to have had.
            }
        }

        if (_file is not null) await _file.DisposeAsync().ConfigureAwait(false);
    }
}
