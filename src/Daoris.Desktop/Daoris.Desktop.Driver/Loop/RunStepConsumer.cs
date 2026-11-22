using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A run's progression as the bus's second consumer: each fact that ends a step of a run (a check passed, failed or unknown; a working session's
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
internal sealed class RunStepConsumer : IBusConsumer
{
    /// <summary>Its durable name in the queue.</summary>
    public const string Name = "run-step";

    /// <summary>The longest a pass is waited for at a fact; past it the fact is tried again after a backoff.</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    /// <summary>How long after its pass ends a begun node still counts as that pass's: the record's write and its fact's stamp may land after it.</summary>
    public static readonly TimeSpan Slack = TimeSpan.FromSeconds(2);

    private static readonly string[] Ending =
    [
        BusTypes.CheckPassed.Kind, BusTypes.CheckFailed.Kind, BusTypes.CheckUnknown.Kind, BusTypes.StepSettled.Kind, BusTypes.StepTurnEnded.Kind,
        BusTypes.PolicyPassed.Kind, BusTypes.LandOutcome.Kind,
    ];

    private readonly Func<string, CancellationToken, Task<bool>> _pass;
    private readonly Func<string?, CancellationToken, Task<int>> _take;
    private readonly BusLog _bus;
    private readonly Action<string, string> _warn;
    private readonly TimeSpan _slack;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _ending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (DateTimeOffset Start, DateTimeOffset End)> _passed = new(StringComparer.Ordinal);

    /// <param name="pass">Asks the evaluation runtime's pass for one unit and waits for it: true only where it judged the unit.</param>
    /// <param name="take">Takes the step settle waiting in the box of the session named, or every box where none is; how many it took.</param>
    /// <param name="bus">The log it measures into, whose clock it reads.</param>
    /// <param name="warn">Where a start's failed take is said, once for each key.</param>
    /// <param name="slack">How long after a pass a begun node is still its own; <see cref="Slack"/> where none is given.</param>
    public RunStepConsumer(
        Func<string, CancellationToken, Task<bool>> pass, Func<string?, CancellationToken, Task<int>> take, BusLog bus, Action<string, string> warn,
        TimeSpan? slack = null)
    {
        _slack = slack ?? Slack;
        _pass = pass;
        _take = take;
        _bus = bus;
        _warn = warn;
    }

    public BusSubscription Subscription { get; } = BusTypes.Subscribe(Name, [.. Ending, BusTypes.NodeBegun.Kind]);

    public string Obliged => BusTypes.Step;

    public bool Folds(string kind) => kind != BusTypes.NodeBegun.Kind;

    public async Task<BusHandled> HandleAsync(BusEnvelope entry, CancellationToken ct)
    {
        using var asking = GitAskers.Is(GitAskers.Steps);
        // A check's facts name their run by its bare id; every other fact names its unit.
        var unit = entry.Kind.StartsWith("check.", StringComparison.Ordinal) ? RunUnits.IdOf(entry.Subject) : entry.Subject;
        if (entry.Kind == BusTypes.NodeBegun.Kind)
        {
            Compare(unit, entry.At);
            return BusHandled.Judged;
        }

        // The earliest ending fact no node has begun after yet: the lag a node begun later is measured from.
        _ending.AddOrUpdate(unit, entry.At, (_, was) => was < entry.At ? was : entry.At);
        // A settle's box is taken at the fact, only the box its session named, so the settle is on the run's record before the pass reads it.
        if (entry.Kind == BusTypes.StepSettled.Kind) await _take(entry.Payload.FirstOrDefault(pair => pair.Key == "session").Value as string, ct).ConfigureAwait(false);
        var start = _bus.Clock.GetUtcNow();
        // A unit the pass did not judge (a drain, a closing loop, no lease, a run it does not govern yet) leaves the obligation armed for the look's whole pass.
        if (!await _pass(unit, ct).ConfigureAwait(false)) return BusHandled.Judged;
        if (_passed.Count >= 1024) _passed.Clear();
        _passed[unit] = (start, _bus.Clock.GetUtcNow());
        return BusHandled.Ended;
    }

    /// <summary>A start takes the settles waiting in their boxes; the first look's whole pass judges every run, so no pass is asked here.</summary>
    public async Task ResumedAsync(CancellationToken ct)
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
        var by = _passed.TryGetValue(unit, out var window) && at >= window.Start && at <= window.End + _slack
            ? BusMeasures.ByConsumer
            : _ending.ContainsKey(unit) ? BusMeasures.ByLook : BusMeasures.ByOther;
        var lag = _ending.TryRemove(unit, out var since) ? Math.Max(0, (long)(at - since).TotalMilliseconds) : 0;
        _bus.Measured(BusMeasures.Began, by, lag);
    }
}
