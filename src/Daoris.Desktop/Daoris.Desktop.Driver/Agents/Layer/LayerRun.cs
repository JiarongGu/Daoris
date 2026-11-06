using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// <see cref="AgentRun"/> over the layer, one turn per call (the agent-lyntai design §4.1, §5.4, §5.5). A start or a resume builds a spawn's
/// container of its own over its own <see cref="LauncherRunner"/>, which this run owns and disposes at its close; a resume is a start of its
/// own, never the earlier run's container. Each event the layer yields is translated once (<see cref="LayerReadings"/>) and handed to the
/// run's sink as today's transports hand theirs, each line written to the transcript as it is shown; the lines the harness says outside a
/// frame and on stderr are the runner's, kept as its own words. The end is read as the conclusion reads it, its exit off the runner since
/// the layer's terminal carries none; a stop Daoris asked before the terminal is a stop. An operation the layer's members do not carry is
/// refused before anything is written.
/// </summary>
internal sealed class LayerRun(LayerRegistration registration, LayerStart start, LauncherSeat seat, Func<string, bool> exists)
    : AgentRun, IAsyncDisposable
{
    /// <summary>A word into a layer run's turn: its door's own until the long-lived run (LQ3) or the layer's CLI turn (LQ13).</summary>
    public const string WordsAbility = "a word into a layer run's turn (LQ3, LQ13)";

    public const string ChoiceAbility = "a later choice on a layer run's session (AG11, LQ8)";

    public const string TaskAbility = "a task stopped on a layer run (AG18 with LQ7)";

    /// <summary>The native resume's one retry reads the conversation as the agent's own initialization named it (LQ17 widened).</summary>
    public const string RetryAbility = "a native resume's one retry on a layer run (LQ17's opening evidence)";

    private readonly object _gate = new();

    private readonly List<string> _own = [];

    private IReadingSink _sink = ReadingSinks.None;

    private LayerReadings? _readings;

    private LauncherRunner? _runner;

    private Task? _turn;

    private TextWriter? _file;

    /// <summary>The spawn's container this run built and owns; null before its start.</summary>
    internal LayerContainer? Container { get; private set; }

    /// <summary>The carriers handed for the start, kept for the run's go-ons.</summary>
    internal RunCarriers? Carriers { get; private set; }

    /// <summary>The turn's failure as Daoris's outcome, through the ingress; null where it did not fail or was stopped.</summary>
    public NormalizedFailure? Failure => _readings?.Failure;

    /// <summary>Whether Daoris stopped the turn before its terminal.</summary>
    public bool Stopped => _runner?.Stopped == true;

    public override Task OpenAsync(CancellationToken ct) => BeginAsync(resume: null);

    public override Task ResumeAsync(string conversation, CancellationToken ct, IResumeState? state = null) =>
        state is not null ? Task.FromException(Unsupported(RetryAbility)) : BeginAsync(conversation);

    public override Task ApplyChoiceAsync(ActorAsked choice, CancellationToken ct) => Task.FromException(Unsupported(ChoiceAbility));

    internal override DoorInput Input { get; } = new(WordsAbility);

    internal override ProtocolWords? Words => null;

    public override async Task StopTurnAsync(CancellationToken ct)
    {
        if (_runner is not { } runner) return;
        _readings?.Stop();
        runner.Stop();
        if (_turn is { } turn) await turn.WaitAsync(ct).ConfigureAwait(false);
    }

    public override Task StopTaskAsync(string task, CancellationToken ct) => Task.FromException(Unsupported(TaskAbility));

    public override async Task CloseAsync(CancellationToken ct)
    {
        await StopTurnAsync(ct).ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }

    public override void HandSink(IReadingSink sink) => _sink = sink;

    public override void HandCarriers(RunCarriers carriers) => Carriers = carriers;

    public override async Task<AgentRunEnd> EndAsync(CancellationToken ct)
    {
        var turn = _turn ?? throw new InvalidOperationException("the layer run was never started.");
        await turn.WaitAsync(ct).ConfigureAwait(false);
        var readings = _readings!;
        var exit = _runner!.Exit;
        var failure = readings.Ended is { IsError: true } ended && readings.Failure is not null
            ? ended.Diagnostic ?? ended.Subtype ?? ended.Verdict.ToString()
            : null;
        string[] own;
        lock (_gate) own = [.. _own.TakeLast(HarnessEnding.LinesKept)];
        return new AgentRunEnd(exit, readings.Usage, new HarnessEnding(exit, failure, own) { ModelRefused = readings.ModelRefused }, readings.Conversation is not null);
    }

    public async ValueTask DisposeAsync()
    {
        if (_turn is { } turn) await turn.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (Container is { } container) await container.DisposeAsync().ConfigureAwait(false);
        Close();
    }

    private Task BeginAsync(string? resume)
    {
        if (_turn is not null) return Task.FromException(new InvalidOperationException("a layer run is one turn: a resume is a run of its own."));
        _readings = new LayerReadings(start.Transport, start.Clock, start.CoolOff, new LayerRefusals(registration.ModelRefusals));
        _file = AgentContext.Current.OpenTranscript(start.Transcript, append: resume is not null);
        _runner = new LauncherRunner(seat with { Outside = Own, Error = Own }, patience: null, exists);
        Container = LayerContainer.ForSpawn(registration, _runner);
        var session = Container.Open(registration.Door, start.Kind);
        var options = new AgentSessionOptions
        {
            Prompt = start.Prompt,
            ResumeToken = resume,
            WorkingDirectory = start.WorkingDirectory,
            Account = start.Account,
            Model = start.Model,
            ToolPolicy = start.Policy,
        };
        _turn = Task.Run(() => TurnAsync(session, options));
        return Task.CompletedTask;
    }

    /// <summary>The turn, to its terminal: a stop ends it through the runner's kill, never a token, so its terminal is still read.</summary>
    private async Task TurnAsync(IAgentSession session, AgentSessionOptions options)
    {
        try
        {
            await foreach (var evt in session.StreamAsync(options, CancellationToken.None).ConfigureAwait(false))
            {
                foreach (var reading in _readings!.Read(evt)) Hand(reading);
            }
        }
        finally
        {
            Close();
        }
    }

    /// <summary>A line the harness says outside a frame or on stderr: the transcript's and the console's, and its own words.</summary>
    private void Own(string line)
    {
        lock (_gate) _own.Add(line);
        Hand(new WireReading.Shown(line));
    }

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
