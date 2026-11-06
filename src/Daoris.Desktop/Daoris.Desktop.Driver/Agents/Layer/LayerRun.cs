using System.Diagnostics;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// <see cref="AgentRun"/> over the layer, one turn per process (the agent-lyntai design §4.1, §5.4, §5.5). Each process, the first's, the
/// native resume's one retry's and each go-on's, is a turn of its own over its own spawn's container and <see cref="LauncherRunner"/>, which
/// this run owns and disposes at its close. Each event the layer yields is translated once (<see cref="LayerReadings"/>) and handed to the
/// run's sink as today's transports hand theirs, each line written to the transcript as it is shown; the lines the harness says outside a
/// frame and on stderr are the runner's, kept as its own words with what its stream says of its background work (<see cref="HarnessWords"/>).
/// The end is read as the conclusion reads it, its exit off the runner or the host's wait since the layer's terminal carries none; a stop
/// Daoris asked before the terminal is a stop. An operation the layer's members do not carry is refused before anything is written.
/// </summary>
/// <remarks>
/// <para><b>Its words are the native door's</b> (D243): a turn's prompt is composed of the lines its <see cref="Input"/> hands the next
/// process (the start's, those a go-on holds, those the ended process never acknowledged) and the layer writes it whole on stdin; the words are
/// written as that write returns and acknowledged once the agent names its conversation.</para>
/// <para><b>Held by an executor</b> (<see cref="LayerHold"/>, AGENTLYNTAI1d), it takes today's driven run's steps around each process: the
/// downloads kept, the lineage token on its environment, the process tracked and reaped, each raw line stamped for the watch, the wait under
/// the start's bound and the watch's quiet, whose kill its readings say nothing of, as today's capture says nothing of a stream cut off.</para>
/// </remarks>
internal sealed class LayerRun(
    LayerRegistration registration, LayerStart start, LauncherSeat seat, Func<string, bool> exists, LayerHold? hold = null) : HeldRun
{
    public const string ChoiceAbility = "a later choice on a layer run's session (AG11, LQ8)";

    public const string TaskAbility = "a task stopped on a layer run (AG18 with LQ7)";

    private readonly object _gate = new();

    private readonly List<LayerContainer> _containers = [];

    private IReadingSink _sink = ReadingSinks.None;

    private HarnessWords _own = new();

    private IResumeState? _resume;

    private string? _conversation;

    private Leg? _leg;

    private int _generation;

    private bool _goneOn;

    private bool _killed;

    private TextWriter? _file;

    /// <summary>One process of the run: its readings, runner and container, its lineage token where it is held, and its turn.</summary>
    private sealed class Leg(LayerReadings readings, LauncherRunner runner, LayerContainer container, string? proc, Action named)
    {
        public LayerReadings Readings { get; } = readings;

        public LauncherRunner Runner { get; } = runner;

        public LayerContainer Container { get; } = container;

        public string? Proc { get; } = proc;

        /// <summary>The leg's children read off its frames, each start and end once, as today's capture reads them (AGENTPROVIDER1l).</summary>
        public NativeChildren Children { get; } = new();

        /// <summary>Told once the agent named its conversation and the sink kept it.</summary>
        public Action Named { get; } = named;

        /// <summary>The process the launcher answered; null where the turn ended before one started.</summary>
        public TaskCompletionSource<Process?> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The process handed its lineage token, or the token let go; the started process where the run is not held.</summary>
        public Task<Process?> Spawned { get; set; } = Task.FromResult<Process?>(null);

        public Task Turn { get; set; } = Task.CompletedTask;
    }

    /// <summary>The latest process's container; null before the start.</summary>
    internal LayerContainer? Container => _leg?.Container;

    /// <summary>The carriers handed for the start, kept for the run's go-ons.</summary>
    internal RunCarriers? Carriers { get; private set; }

    /// <summary>The latest turn's failure as Daoris's outcome, through the ingress; null where it did not fail or was stopped.</summary>
    public NormalizedFailure? Failure => _leg?.Readings.Failure;

    /// <summary>Whether Daoris stopped the latest turn before its terminal.</summary>
    public bool Stopped => _leg?.Runner.Stopped == true;

    public override bool GoesOn => hold?.GoesOn == true;

    internal override TransportStart Start => Held().Start;

    internal override Task SpawnAsync(string sessionId, CancellationToken ct) => Held().BracketAsync(sessionId, ct);

    internal override void HandStart(TransportStart completed)
    {
        if (_leg is not null) throw new InvalidOperationException("the run is open already: its start is the one it opened with.");
        Held().Hand(completed);
    }

    internal override async Task AbandonAsync()
    {
        if (_leg is not { } leg || hold is null) return;
        if (await leg.Spawned.ConfigureAwait(false) is { } process && leg.Proc is { } proc)
        {
            await hold.AbandonedAsync(proc, process, leg.Turn).ConfigureAwait(false);
        }
    }

    public override Task OpenAsync(CancellationToken ct) => BeginAsync(resume: null, ct);

    /// <summary>A resume's start, its state handed for the one retry; once a process ended, a go-on with the words its input holds.</summary>
    public override Task ResumeAsync(string conversation, CancellationToken ct, IResumeState? state = null)
    {
        if (_leg is null)
        {
            _resume = state;
            return BeginAsync(conversation, ct);
        }

        if (hold is { GoesOn: false }) return Task.FromException(Unsupported("a go-on"));
        _goneOn = true;
        return LegAsync(conversation, first: false, Input.Openings(), preamble: null, named: () => Input.Acknowledge(), ct);
    }

    public override Task ApplyChoiceAsync(ActorAsked choice, CancellationToken ct) => Task.FromException(Unsupported(ChoiceAbility));

    internal override DoorInput Input { get; } = new();

    internal override ProtocolWords? Words => null;

    public override async Task StopTurnAsync(CancellationToken ct)
    {
        if (_leg is not { } leg) return;
        leg.Readings.Stop();
        leg.Runner.Stop();
        await leg.Turn.WaitAsync(ct).ConfigureAwait(false);
    }

    public override Task StopTaskAsync(string task, CancellationToken ct) => Task.FromException(Unsupported(TaskAbility));

    public override async Task CloseAsync(CancellationToken ct)
    {
        await StopTurnAsync(ct).ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }

    public override void HandSink(IReadingSink sink) => _sink = new StampedSink(sink, () => hold?.Start.Session, () => _generation, () => _leg?.Proc);

    public override void HandCarriers(RunCarriers carriers) => Carriers = carriers;

    public override async Task<AgentRunEnd> EndAsync(CancellationToken ct)
    {
        var end = await EndedAsync(_leg ?? throw new InvalidOperationException("the layer run was never started."), bounded: !_goneOn, ct)
            .ConfigureAwait(false);

        // The native resume's one retry (design §2.6, D243): a resume whose harness ended on a failed exit before it named its conversation is
        // tried once more as the same resume, handed every word the ended process never acknowledged. Never after a clean exit, a fresh start,
        // a refusal or a stop.
        if (_resume is { Named: false, Refused: false, Retried: false } resume && end.Exit is int early && early != 0 && !_killed
            && hold?.StopAsked != true && !Stopped)
        {
            Say("— its harness ended before it reopened its conversation, so the same resume is tried once more.");
            resume.Again();
            Input.MayRetry = false;
            await LegAsync(_conversation, first: false, opening: null, preamble: null, Told, ct).ConfigureAwait(false);
            end = await EndedAsync(_leg!, bounded: false, ct).ConfigureAwait(false);
        }

        Input.MayRetry = false;
        Input.Detach();
        return end;
    }

    public override async ValueTask DisposeAsync()
    {
        // Each process ended and untracked first, so a turn still reading ends with it.
        hold?.Dispose();
        if (_leg is { Turn.IsCompleted: false } leg)
        {
            _killed = true;
            leg.Runner.Stop();
        }

        if (_leg is { } last) await last.Turn.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        LayerContainer[] containers;
        lock (_gate)
        {
            containers = [.. _containers];
            _containers.Clear();
        }

        foreach (var container in containers) await container.DisposeAsync().ConfigureAwait(false);
        Close();
    }

    private LayerHold Held() => hold ?? throw new InvalidOperationException("a layer run its executor does not hold takes no executor's step.");

    private Task BeginAsync(string? resume, CancellationToken ct)
    {
        if (_leg is not null) return Task.FromException(new InvalidOperationException("the layer run is begun already: its go-ons and retry are its own."));
        _conversation = resume;
        _own = hold?.Start.Own ?? _own;
        Input.MayRetry = _resume is { Named: false, Refused: false, Retried: false };
        Input.Hold(new DoorWord(null, hold?.Start.Prompt ?? start.Prompt, hold?.Start.Written));
        return LegAsync(resume, first: true, hold?.Start.Opening, hold?.Start.Preamble, Told, ct);
    }

    /// <summary>
    /// One process: its token issued where it is held, its transcript opened (appended past the first, or on a resume), its preamble and the
    /// record's opening said, then its turn begun with the prompt its input composes, which is written as the layer's stdin write returns.
    /// </summary>
    private async Task LegAsync(
        string? resume, bool first, IReadOnlyList<SessionEvent>? opening, string? preamble, Action named, CancellationToken ct)
    {
        if (_leg is { } before) await before.Turn.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        var proc = hold is null ? null : await hold.TokenAsync(first, ct).ConfigureAwait(false);
        var prompt = string.Join('\n', Input.Lines());
        LauncherRunner? runner = null;
        var readings = new LayerReadings(start.Transport, start.Clock, start.CoolOff, new LayerRefusals(registration.ModelRefusals), registration.Steps)
        {
            Killed = () => _killed || hold?.StopAsked == true,
            Clean = () => runner?.Exit == 0,
        };

        lock (_gate) _file = AgentContext.Current.OpenTranscript(hold?.Start.Transcript ?? start.Transcript, append: !first || resume is not null);
        Interlocked.Increment(ref _generation);
        _own.Begin();
        if (preamble is { Length: > 0 }) Line(preamble);
        foreach (var said in opening ?? [])
        {
            if (said.Kind == SessionEventKind.Note && said.Text is { } note) Line(note);
            Hand(new WireReading.Said(said));
        }

        Leg? leg = null;
        runner = new LauncherRunner(
            seat with
            {
                Whose = hold is null ? seat.Whose : first ? hold.Start.Whose : StartClass.Person,
                Environment = hold?.Environment ?? seat.Environment,
                Started = process =>
                {
                    hold?.Track(process);
                    seat.Started(process);
                    leg!.Started.TrySetResult(process);
                },
                Heard = hold is null ? seat.Heard : hold.Heard,
                Wrote = Wrote,
                Framed = Framed,
                Outside = Own,
                Error = Own,
            },
            patience: null, exists);
        var container = LayerContainer.ForSpawn(registration, runner);
        lock (_gate) _containers.Add(container);
        var session = container.Open(registration.Door, start.Kind);

        // The door's own options type where it has one (`with` keeps it), the turn's members the layer carries set on it.
        var options = (registration.Options?.Invoke(prompt, Carriers) ?? new AgentSessionOptions { Prompt = prompt }) with
        {
            ResumeToken = resume,
            WorkingDirectory = start.WorkingDirectory,
            Account = start.Account,
            Model = start.Model,
            ToolPolicy = start.Policy,
            PermissionMode = start.PermissionMode,
            Effort = start.Effort,
            PluginDirectories = start.Plugins,
        };
        leg = new Leg(readings, runner, container, proc, named);
        leg.Spawned = proc is null || hold is null ? leg.Started.Task : hold.SpawnedAsync(proc, leg.Started.Task);
        _leg = leg;
        leg.Turn = Task.Run(() => TurnAsync(leg, session, options));
    }

    /// <summary>The turn, to its terminal: a stop ends it through the runner's kill, never a token, so its terminal is still read.</summary>
    private async Task TurnAsync(Leg leg, IAgentSession session, AgentSessionOptions options)
    {
        try
        {
            await foreach (var evt in session.StreamAsync(options, CancellationToken.None).ConfigureAwait(false))
            {
                Handed(leg, leg.Readings.Read(evt));
            }
        }
        finally
        {
            Handed(leg, leg.Readings.Flush());
            leg.Started.TrySetResult(null);
            Close();
        }
    }

    /// <summary>A turn's readings handed in order, its process told once the conversation it named is kept.</summary>
    private void Handed(Leg leg, IReadOnlyList<WireReading> readings)
    {
        foreach (var reading in readings)
        {
            Hand(reading);
            if (reading is WireReading.ConversationNamed) leg.Named();
        }
    }

    /// <summary>
    /// The process's end as the conclusion reads it: where it is held, its exit under the host's wait (the first process bounded by its
    /// start's minutes and the watch's quiet, whose kill its readings then say nothing of), its turn drained and its lineage ended; else the
    /// turn's end and the runner's exit. The harness's own words, its failed turn's and a refused model's among them, are its ending.
    /// </summary>
    private async Task<AgentRunEnd> EndedAsync(Leg leg, bool bounded, CancellationToken ct)
    {
        var process = await leg.Spawned.WaitAsync(ct).ConfigureAwait(false);
        int? exit;
        if (hold is not null && process is not null && leg.Proc is { } proc)
        {
            exit = await hold.EndedAsync(proc, process, leg.Turn, bounded, () => _killed = true, ct).ConfigureAwait(false);
        }
        else
        {
            await leg.Turn.WaitAsync(ct).ConfigureAwait(false);
            exit = leg.Runner.Exit;
        }

        var readings = leg.Readings;
        if (readings.Ended is { IsError: true } ended && readings.Failure is not null)
        {
            _own.Failed(ended.Diagnostic ?? ended.Subtype ?? ended.Verdict.ToString());
        }

        if (readings.ModelRefused) _own.ModelRefused();
        return new AgentRunEnd(exit, readings.Usage, _own.Ended(exit), readings.Conversation is not null);
    }

    /// <summary>The first process's and its retry's agent named its conversation: the resume's state marked, its words acknowledged, the executor told.</summary>
    private void Told()
    {
        _resume?.Opened();
        Input.Acknowledge();
        hold?.Start.Named?.Invoke();
    }

    /// <summary>The layer's stdin write returned: each word the prompt carried is written, and none is taken from a later write.</summary>
    private void Wrote()
    {
        Input.AttachAsync((_, _) => Task.FromResult(true), CancellationToken.None).GetAwaiter().GetResult();
        Input.Detach();
    }

    /// <summary>
    /// Each raw frame of the turn's stdout, read before the layer reads its line: what the harness's ending reads of its own background work
    /// (BGWAIT1c), as today's capture reads it.
    /// </summary>
    private void Framed(NativeFrame frame)
    {
        _own.Heard(frame);
        if (_leg is { } leg && leg.Children.Read(frame) is { } child) Hand(child);
    }

    /// <summary>A line the harness says outside a frame or on stderr: the transcript's and the console's, and its own words.</summary>
    private void Own(string line)
    {
        if (_leg is { } leg) Handed(leg, leg.Readings.Flush());
        _own.Said(line);
        Hand(new WireReading.Shown(line));
    }

    /// <summary>A line the run says itself, as today's driver says its own: observed, on the transcript and the console.</summary>
    private void Say(string text) => Hand(new WireReading.Shown(text) { By = ReadingBy.Observed });

    private void Line(string text) => Hand(new WireReading.Shown(text));

    private void Hand(WireReading reading)
    {
        if (reading is WireReading.Shown shown)
        {
            lock (_gate) _file?.WriteLine(shown.Text);
        }

        _sink.Read(reading);
    }

    private void Close()
    {
        lock (_gate)
        {
            _file?.Dispose();
            _file = null;
        }
    }
}
