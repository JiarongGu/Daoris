using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>What a run's pass is asked at its facts: the settle boxes (by session, null for every box) to take before it.</summary>
internal sealed record StepWork(IReadOnlyList<string?> Boxes);

/// <summary>
/// A run's progression as the dispatch's second consumer (<see cref="BusFlowConsumer{TWork}"/>, keyed by the run): each fact that ends a step of a run (a check passed, failed or unknown; a working session's
/// settle taken from its box; the session of a plugin step ending its turn; the policy passing a second opinion at its gate; a landing's try
/// ending) asks the one pass of the evaluation runtime for that run at once, which judges that run first, so the next step begins at the fact
/// and not at a look's rotation. The pass is the existing one (<see cref="PassAsks.WaitUnitAsync"/>); this consumer is its wake by fact, never
/// a second way to progress a run. Its acknowledgement, written once the pass judged the run, ends the facts' obligations.
/// </summary>
/// <remarks>
/// <para><b>What the pass reads.</b> It reads no units: the run's record, its tips and its gates are read at the pass, as a plugin step's wake
/// reads them, and the governance (its graph, its scope, its quests) is the last whole read's, which none of these facts changes. A run the
/// last read did not govern yet is left to the whole pass the look makes. A run the pass did not judge in time (a drain, a closing loop, no lease) is
/// acknowledged without ending the fact's obligation, so no retry is spent and the stall view keeps it until a look's pass reaches it. Handling is idempotent: a
/// fact redelivered after a crash asks a pass that finds nothing owed and begins nothing twice.</para>
/// <para><b>Facts of one run</b> are handled one after another in order, and those of several runs beside each other, at most the queue's
/// subjects at once; the passes they ask coalesce, one pass judging every unit asked before it began. Facts of one kind waiting in a row on a run
/// fold into the last.</para>
/// <para><b>The net.</b> The look's whole pass stays: an ending no fact names (a tip, a timer, a binding, the configuration, an owed end) still
/// reaches the run there. A node begun is told apart as the consumer's or the look's (<see cref="BusMeasures.Began"/>), with its lag from the
/// ending fact, so <c>daoris bus stats</c> shows how many steps still wait for a look. A start takes the settles waiting in their boxes
/// (<see cref="ResumedAsync"/>) and asks no pass: it runs after the first look, whose whole pass judges every run, so a restart loses nothing.</para>
/// </remarks>
internal sealed class RunStepConsumer : BusFlowConsumer<StepWork>
{
    /// <summary>Its durable name in the queue.</summary>
    public const string Name = "run-step";

    /// <summary>Its bounds: the queue's four runs at once, no group.</summary>
    public static readonly BusFlowBounds Bounds = new(KeysAtOnce: 4);

    /// <summary>The longest a pass is waited for at a fact; past it the fact is tried again after a backoff.</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    /// <summary>How long after its pass ends a begun node still counts as that pass's: the record's write and its fact's stamp may land after it.</summary>
    public static readonly TimeSpan Slack = TimeSpan.FromSeconds(2);

    private static readonly string[] Ending =
    [
        BusTypes.CheckPassed.Kind, BusTypes.CheckFailed.Kind, BusTypes.CheckUnknown.Kind, BusTypes.StepSettled.Kind, BusTypes.StepTurnEnded.Kind,
        BusTypes.PolicyPassed.Kind, BusTypes.LandOutcome.Kind,
    ];

    private readonly Func<string, CancellationToken, Task<PassOutcome>> _pass;
    private readonly Func<string?, CancellationToken, Task<int>> _take;
    private readonly BusLog _bus;
    private readonly Action<string, string> _warn;
    private readonly TimeSpan _slack;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _ending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (DateTimeOffset Start, DateTimeOffset End)> _passed = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, Asking> _asking = new();

    /// <param name="pass">Asks the evaluation runtime's pass for one unit and waits for it: how it answered, judged only where it kept the unit's judgement.</param>
    /// <param name="take">Takes the step settle waiting in the box of the session named, or every box where none is; how many it took.</param>
    /// <param name="bus">The log it measures into, whose clock it reads.</param>
    /// <param name="warn">Where a start's failed take is said, once for each key.</param>
    /// <param name="slack">How long after a pass a begun node is still its own; <see cref="Slack"/> where none is given.</param>
    public RunStepConsumer(
        Func<string, CancellationToken, Task<PassOutcome>> pass, Func<string?, CancellationToken, Task<int>> take, BusLog bus, Action<string, string> warn,
        TimeSpan? slack = null)
        : base(Bounds, bus.Clock)
    {
        _slack = slack ?? Slack;
        _pass = pass;
        _take = take;
        _bus = bus;
        _warn = warn;
    }

    public override BusSubscription Subscription { get; } = BusTypes.Subscribe(Name, [.. Ending, BusTypes.NodeBegun.Kind]);

    public override string Obliged => BusTypes.Step;

    protected override bool Coalesces(string kind) => kind != BusTypes.NodeBegun.Kind;

    /// <summary>A node begun is only compared; every ending fact's job moves when its run's pass judged the run.</summary>
    protected override bool Settles(BusEnvelope entry) => entry.Kind != BusTypes.NodeBegun.Kind;

    /// <summary>A check's facts name their run by its bare id; every other fact names its unit.</summary>
    private static string UnitOf(BusEnvelope entry) => entry.Kind.StartsWith("check.", StringComparison.Ordinal) ? RunUnits.IdOf(entry.Subject) : entry.Subject;

    protected override void Observe(BusEnvelope entry)
    {
        var unit = UnitOf(entry);
        if (entry.Kind == BusTypes.NodeBegun.Kind) Compare(unit, entry.At);
        // The earliest ending fact no node has begun after yet: the lag a node begun later is measured from.
        else _ending.AddOrUpdate(unit, entry.At, (_, was) => was < entry.At ? was : entry.At);
    }

    /// <summary>An ending fact asks its run's pass, keyed by the run; a settle names the box its session wrote, taken before the pass. A node begun asks none.</summary>
    protected override Task<IReadOnlyList<BusFlowPiece<StepWork>>> PiecesAsync(BusEnvelope entry, CancellationToken ct)
    {
        if (entry.Kind == BusTypes.NodeBegun.Kind) return Task.FromResult<IReadOnlyList<BusFlowPiece<StepWork>>>([]);
        var boxes = entry.Kind == BusTypes.StepSettled.Kind ? new StepWork([entry.Payload.FirstOrDefault(pair => pair.Key == "session").Value as string]) : new StepWork([]);
        return Task.FromResult<IReadOnlyList<BusFlowPiece<StepWork>>>([new(UnitOf(entry), boxes)]);
    }

    protected override StepWork Merge(StepWork earlier, StepWork later) => new([.. earlier.Boxes.Union(later.Boxes)]);

    protected override async Task<BusFlowOutcome> MoveAsync(string unit, StepWork work, CancellationToken ct)
    {
        using var asking = GitAskers.Is(GitAskers.Steps);
        // A settle's box is taken at the fact, only the box its session named, so the settle is on the run's record before the pass reads it.
        foreach (var box in work.Boxes) await _take(box, ct).ConfigureAwait(false);
        var start = _bus.Clock.GetUtcNow();
        // The node's fact is handled beside this one (another subject) and can come before the pass returns: it waits on this ask, credited to
        // the consumer only where the ask came to be judged, else the look's.
        var ask = new Asking(unit, start);
        var key = Guid.NewGuid();
        _asking[key] = ask;
        var outcome = PassOutcome.Failed;
        try
        {
            outcome = await _pass(unit, ct).ConfigureAwait(false);
            if (outcome.IsAnswer())
            {
                if (_passed.Count >= 1024) _passed.Clear();
                _passed[unit] = (start, _bus.Clock.GetUtcNow());
            }
        }
        finally
        {
            _asking.TryRemove(key, out _);
            foreach (var lag in ask.Take()) Credit(outcome.IsAnswer() ? BusMeasures.ByConsumer : BusMeasures.ByLook, lag);
        }

        // One row a reason, in the log's next write with the other measures.
        _bus.Measured(BusMeasures.Step, outcome.Name(), 1);
        // A unit the pass did not judge (a drain, a closing loop, no lease, a run it does not govern yet) leaves the obligation armed for the look's whole pass.
        return outcome.IsAnswer() ? BusFlowOutcome.Moved : BusFlowOutcome.NotMoved(outcome.Name());
    }

    /// <summary>A start takes the settles waiting in their boxes; the first look's whole pass judges every run, so no pass is asked here.</summary>
    public override async Task ResumedAsync(CancellationToken ct)
    {
        try
        {
            await _take(null, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
        {
            _warn("resumed", error.Message);
        }
    }

    /// <summary>
    /// A node begun, told as the consumer's where its record is written within a pass this consumer asked, else the look's where an ending fact
    /// stands before it, else another's; with its lag from the ending fact.
    /// </summary>
    private void Compare(string unit, DateTimeOffset at)
    {
        var open = _asking.Values.FirstOrDefault(each => each.Unit == unit && at >= each.Start);
        var lag = _ending.TryRemove(unit, out var since) ? Math.Max(0, (long)(at - since).TotalMilliseconds) : 0;
        if (open is not null && !open.Add(lag)) open = null;
        var inWindow = _passed.TryGetValue(unit, out var window) && at >= window.Start && at <= window.End + _slack;
        var by = inWindow ? BusMeasures.ByConsumer : since != default ? BusMeasures.ByLook : BusMeasures.ByOther;
        if (open is null) Credit(by, lag);
    }

    private void Credit(string by, long lag)
    {
        _bus.Measured(BusMeasures.Began, by, lag);
        if (by == BusMeasures.ByConsumer) _bus.Measured(BusMeasures.Step, BusMeasures.StepBegan, 1);
    }

    /// <summary>One ask in flight: the nodes begun meanwhile, their lags kept until the ask's outcome says whose they are.</summary>
    private sealed class Asking(string unit, DateTimeOffset start)
    {
        private List<long>? _lags = [];

        public string Unit { get; } = unit;

        public DateTimeOffset Start { get; } = start;

        /// <summary>Keeps a lag; false where the ask has ended and its lags were taken.</summary>
        public bool Add(long lag)
        {
            lock (this)
            {
                if (_lags is null) return false;
                _lags.Add(lag);
                return true;
            }
        }

        public IReadOnlyList<long> Take()
        {
            lock (this)
            {
                var taken = _lags ?? [];
                _lags = null;
                return taken;
            }
        }
    }
}
