using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A run's progression as the bus's second consumer: each fact that ends a step of a run (a check passed, failed or unknown; a working session's
/// settle taken from its box; the session of a plugin step ending its turn; the policy passing a second opinion at its gate; a landing's try
/// ending) asks the one pass of the evaluation runtime for that run at once, which reads the units afresh and judges that run first, so the
/// next step begins at the fact and not at a look's rotation. The pass is the existing one (<see cref="PassAsks"/>); this consumer is its wake
/// by fact, never a second way to progress a run. Its acknowledgement, written once the pass judged the run, ends the facts' obligations.
/// </summary>
/// <remarks>
/// <para><b>What the pass reads.</b> An ask names the unit and reads the units afresh, so the pass judges the run on the record and the
/// governance as they stand, never on the last look's cache. A pass that did not judge it in time (a drain, a busy pass, a closing loop) throws, so
/// the queue tries the fact again with its backoff and the acknowledgement is never written before the run is judged. Handling is idempotent: a
/// fact redelivered after a crash asks a pass that finds nothing owed and begins nothing twice.</para>
/// <para><b>Facts of one run</b> are handled one after another in order, and those of several runs beside each other, at most the queue's
/// subjects at once; the passes they ask coalesce, one pass judging every unit asked before it began. Facts of one kind waiting in a row on a run
/// fold into the last.</para>
/// <para><b>The net.</b> The look's whole pass stays: an ending no fact names (a tip, a timer, a binding, the configuration, an owed end) still
/// reaches the run there. A node begun is told apart as the consumer's or the look's (<see cref="BusMeasures.Began"/>), with its lag from the
/// ending fact, so <c>daoris bus stats</c> shows how many steps still wait for a look. A start passes every open run once and takes the
/// settles waiting in their boxes (<see cref="ResumedAsync"/>), so a restart loses nothing.</para>
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
    private readonly Func<CancellationToken, Task<int>> _take;
    private readonly Func<IReadOnlyList<string>> _open;
    private readonly BusLog _bus;
    private readonly Action<string, string> _warn;
    private readonly TimeSpan _slack;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _ending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (DateTimeOffset Start, DateTimeOffset End)> _passed = new(StringComparer.Ordinal);

    /// <param name="pass">Asks the evaluation runtime's pass for one unit and waits for it: true where it judged the unit or ended, false where it did not in time.</param>
    /// <param name="take">Takes each working session's step settle waiting in its box; how many it took.</param>
    /// <param name="open">The units of the runs not ended.</param>
    /// <param name="bus">The log it measures into, whose clock it reads.</param>
    /// <param name="warn">Where a start's failed pass is said, once for each key.</param>
    /// <param name="slack">How long after a pass a begun node is still its own; <see cref="Slack"/> where none is given.</param>
    public RunStepConsumer(
        Func<string, CancellationToken, Task<bool>> pass, Func<CancellationToken, Task<int>> take, Func<IReadOnlyList<string>> open, BusLog bus,
        Action<string, string> warn, TimeSpan? slack = null)
    {
        _slack = slack ?? Slack;
        _pass = pass;
        _take = take;
        _open = open;
        _bus = bus;
        _warn = warn;
    }

    public BusSubscription Subscription { get; } = BusTypes.Subscribe(Name, [.. Ending, BusTypes.NodeBegun.Kind]);

    public string Obliged => BusTypes.Step;

    public bool Folds(string kind) => kind != BusTypes.NodeBegun.Kind;

    public async Task<BusHandled> HandleAsync(BusEnvelope entry, CancellationToken ct)
    {
        var unit = UnitOf(entry.Subject);
        if (entry.Kind == BusTypes.NodeBegun.Kind)
        {
            Compare(unit, entry.At);
            return BusHandled.Judged;
        }

        // The earliest ending fact no node has begun after yet: the lag a node begun later is measured from.
        _ending.AddOrUpdate(unit, entry.At, (_, was) => was < entry.At ? was : entry.At);
        // A settle's box is taken at the fact, never at a look: the settle is on the run's record before the pass reads it.
        if (entry.Kind == BusTypes.StepSettled.Kind) await _take(ct).ConfigureAwait(false);
        var start = _bus.Clock.GetUtcNow();
        if (!await _pass(unit, ct).ConfigureAwait(false)) throw new InvalidOperationException($"the pass did not judge {unit} within {Bound.TotalMinutes:0} minutes");
        if (_passed.Count >= 1024) _passed.Clear();
        _passed[unit] = (start, _bus.Clock.GetUtcNow());
        return BusHandled.Ended;
    }

    /// <summary>A start takes the settles waiting in their boxes and passes every open run once; a pass that fails is said, and the look's whole pass stands.</summary>
    public async Task ResumedAsync(CancellationToken ct)
    {
        try
        {
            await _take(ct).ConfigureAwait(false);
            // One ask: the pass reads every unit afresh whichever it names, so a start never fans out over the open runs.
            if (_open().FirstOrDefault() is { } first) await _pass(first, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
        {
            _warn("resumed", error.Message);
        }
    }

    /// <summary>The unit a fact's subject names: a run's unit as it stands, a run's id as its unit.</summary>
    private static string UnitOf(string subject) => RunUnits.Is(subject) ? subject : RunUnits.IdOf(subject);

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
