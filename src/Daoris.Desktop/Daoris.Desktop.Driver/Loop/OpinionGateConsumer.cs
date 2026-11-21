using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The opinion's gate as the bus's first consumer : each opinion fact it is handed (owed, asked, given, delivered,
/// answered) judges that owed opinion's gate at once by its rule (<see cref="OpinionGateJudge.JudgeDueAsync"/>): deliver, recheck, pass
/// by the policy, or wait named. Its acknowledgement ends what is owed it: an owed opinion judged, answers judged, and a clean pass, which
/// leaves nothing to deliver. No look judges an opinion.
/// </summary>
/// <remarks>
/// <para><b>What it reads, it reads at the fact.</b> A fact names an opinion by its id; the opinion's record names its session and occasion,
/// read as the fact is handled, so nothing waits on a look's read. The author's answers reach it as the go-on's <c>opinion.answered</c>;
/// the service journals none of its own yet (BUS2b2).</para>
/// <para><b>Time as timer events.</b> A judgment that finds a turn ahead (a cool-off's reset, a reading pass's end) or a pass a drain held
/// judges that opinion again then; a pass or a delivery begun beside that ends without a fact (a turn in flight, a busy conversation) is
/// judged again after a backoff. These live in memory: a start judges every owed opinion once (<see cref="ResumedAsync"/>), which rebuilds
/// the gate's view and its turns. The watchdog's timers are the net under all of it.</para>
/// <para><b>One owed opinion at a time.</b> Facts of one opinion arrive in order; facts of several that name one owed opinion are judged
/// one after another, never two judgments of one at once.</para>
/// </remarks>
internal sealed class OpinionGateConsumer : IBusConsumer, IDisposable
{
    /// <summary>Its durable name in the queue.</summary>
    public const string Name = "opinion-gate";

    /// <summary>The first wait before a pass or a delivery that ended without a fact is judged again, doubled at each until <see cref="AgainMost"/>.</summary>
    public static readonly TimeSpan AgainFirst = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan AgainMost = TimeSpan.FromMinutes(10);

    /// <summary>The wait before a pass a draining update held is judged again.</summary>
    public static readonly TimeSpan Drained = TimeSpan.FromMinutes(1);

    private readonly Func<OpinionDue, Action<string>, CancellationToken, Task<OpinionFact?>> _judge;
    private readonly Func<string, CancellationToken, Task<OpinionView?>> _read;
    private readonly Func<IReadOnlyList<OpinionDue>> _open;
    private readonly TimeProvider _clock;
    private readonly Action<string, string> _warn;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _dues = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _again = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _quiet = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _closing = new();

    /// <param name="judge">One owed opinion judged by the gate's rule, told where a pass or a delivery it began beside ends.</param>
    /// <param name="read">An opinion's record by its id; null where none is.</param>
    /// <param name="open">The opinions owed now.</param>
    /// <param name="clock">The clock its turns wait on.</param>
    /// <param name="warn">Where a judgment between facts that failed is said, once for each key.</param>
    public OpinionGateConsumer(
        Func<OpinionDue, Action<string>, CancellationToken, Task<OpinionFact?>> judge, Func<string, CancellationToken, Task<OpinionView?>> read,
        Func<IReadOnlyList<OpinionDue>> open, TimeProvider clock, Action<string, string> warn)
    {
        _judge = judge;
        _read = read;
        _open = open;
        _clock = clock;
        _warn = warn;
    }

    /// <summary>The lease holder's gate: each judgment by a driver made afresh (<paramref name="driver"/>), the configuration read then.</summary>
    public static OpinionGateConsumer Over(Func<Driver> driver, ServiceClient service, string home, BusLog bus) => new(
        async (due, ended, ct) =>
        {
            var gate = driver().Gate;
            gate.BesideEnded = ended;
            return await gate.JudgeDueAsync(due, ct).ConfigureAwait(false);
        },
        service.Opinions.ReadAsync, () => new OpinionDues(home).Open(), bus.Clock,
        (once, why) => bus.Warn("gate:" + once, "opinion.gate-unjudged", ("why", why)));

    public BusSubscription Subscription { get; } = BusTypes.Subscribe(
        Name, [.. new[] { BusTypes.OpinionOwed, BusTypes.OpinionAsked, BusTypes.OpinionGiven, BusTypes.OpinionDelivered, BusTypes.OpinionAnswered }.Select(type => type.Kind)]);

    public string Obliged => BusTypes.OpinionGate;

    public async Task<BusHandled> HandleAsync(BusEnvelope entry, CancellationToken ct)
    {
        foreach (var due in await DuesOfAsync(entry, ct).ConfigureAwait(false))
        {
            _quiet.TryRemove(OpinionGateJudge.Subject(due), out _);
            await JudgeAsync(due, ct).ConfigureAwait(false);
        }

        return Ends(entry) ? BusHandled.Ended : BusHandled.Judged;
    }

    /// <summary>Every owed opinion judged once at the start: the gate's view and its turns rebuilt, nothing owed left to a look.</summary>
    public async Task ResumedAsync(CancellationToken ct)
    {
        foreach (var due in _open())
        {
            try
            {
                await JudgeAsync(due, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
            {
                _warn("resumed:" + OpinionGateJudge.Subject(due), error.Message);
                Again(due, _clock.GetUtcNow() + AgainFirst);
            }
        }
    }

    /// <summary>Whether its acknowledgement of <paramref name="entry"/> ends the entry's obligation (<see cref="OpinionBusFacts.EndsAtTheGate"/>).</summary>
    public static Func<BusEnvelope, bool> Ends => OpinionBusFacts.EndsAtTheGate;

    public void Dispose()
    {
        _closing.Cancel();
        foreach (var again in _again.Values) again.Cancel();
        _closing.Dispose();
    }

    /// <summary>The owed opinions <paramref name="entry"/> names: an owed fact by its session and occasion, an opinion's by its record's.</summary>
    private async Task<IReadOnlyList<OpinionDue>> DuesOfAsync(BusEnvelope entry, CancellationToken ct)
    {
        var open = _open();
        if (open.Count == 0) return [];
        if (entry.Kind == BusTypes.OpinionOwed.Kind)
        {
            var session = Text(entry, "session");
            var occasion = Text(entry, "occasion");
            return [.. open.Where(due => Ids.Equals(due.Session, session) && (occasion is null || due.Occasion == occasion))];
        }

        if (await _read(entry.Subject, ct).ConfigureAwait(false) is not { } view) return [];
        // The opinion reads its working session's work: that session's owed opinion at its occasion, or at any where its occasion moved.
        var mine = open.Where(due => Ids.Equals(due.Session, view.Working)).ToList();
        var exact = mine.Where(due => due.Occasion == view.Occasion).ToList();
        return exact.Count > 0 ? exact : mine;
    }

    /// <summary>One owed opinion judged, one judgment of it at a time, and its next turn set from what it found.</summary>
    private async Task JudgeAsync(OpinionDue due, CancellationToken ct)
    {
        var subject = OpinionGateJudge.Subject(due);
        var gate = _dues.GetOrAdd(subject, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_again.TryRemove(subject, out var pending)) pending.Cancel();
            var fact = await _judge(due, Ended, ct).ConfigureAwait(false);
            if (fact is null) return;
            var now = _clock.GetUtcNow();
            DateTimeOffset? next = fact.Turns.Where(turn => turn > now).Select(turn => (DateTimeOffset?)turn).Min();
            if (fact.Act is not null && !fact.Begun && (next is null || next > now + Drained)) next = now + Drained;
            if (next is { } at) Again(due, at);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>A pass or a delivery begun beside ended: judged again after a backoff, unless a fact judges it first.</summary>
    private void Ended(string subject)
    {
        if (_open().FirstOrDefault(due => Ids.Equals(OpinionGateJudge.Subject(due), subject)) is not { } due) return;
        var quiet = _quiet.AddOrUpdate(subject, 1, (_, was) => Math.Min(was + 1, 16));
        var wait = TimeSpan.FromTicks(Math.Min(AgainMost.Ticks, AgainFirst.Ticks << Math.Min(10, quiet - 1)));
        Again(due, _clock.GetUtcNow() + wait);
    }

    /// <summary>The owed opinion judged again at <paramref name="at"/>, replacing any turn set before; a failure is said once and tried after a backoff.</summary>
    private void Again(OpinionDue due, DateTimeOffset at)
    {
        if (_closing.IsCancellationRequested) return;
        var subject = OpinionGateJudge.Subject(due);
        var source = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        if (_again.TryRemove(subject, out var before)) before.Cancel();
        _again[subject] = source;
        var token = source.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var wait = at - _clock.GetUtcNow();
                if (wait > TimeSpan.Zero) await Task.Delay(wait, _clock, token).ConfigureAwait(false);
                // Its turn came: no longer pending, so the judgment it runs does not cancel itself.
                _again.TryRemove(new KeyValuePair<string, CancellationTokenSource>(subject, source));
                if (_open().FirstOrDefault(each => Ids.Equals(OpinionGateJudge.Subject(each), subject)) is not { } owed) return;
                await JudgeAsync(owed, _closing.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                _warn("again:" + subject, error.Message);
                Ended(subject);
            }
        }, CancellationToken.None);
    }

    private static string? Text(BusEnvelope entry, string key) => entry.Payload.FirstOrDefault(pair => pair.Key == key).Value as string;

    private static readonly StringComparer Ids = StringComparer.OrdinalIgnoreCase;
}
