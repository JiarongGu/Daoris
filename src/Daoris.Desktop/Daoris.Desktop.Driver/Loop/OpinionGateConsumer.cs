using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The opinion's gate as the bus's first consumer : each opinion fact it is handed (owed, asked, given, delivered,
/// answered, the person's word) judges that owed opinion's gate at once by its rule (<see cref="OpinionGateJudge.JudgeDueAsync"/>): deliver,
/// recheck, pass by the policy, or wait named. A move of the quests, of a run's nodes or of the standing choices judges every owed opinion,
/// since its holds, its chain, its graph's opinion node, its rule and its process are read from them. Its acknowledgement ends what is owed
/// it: an owed opinion judged, answers or a word judged, a move judged, and a clean pass, which leaves nothing to deliver. No look judges an
/// opinion.
/// </summary>
/// <remarks>
/// <para><b>What it reads, it reads at the fact.</b> A fact names an opinion by its id; the opinion's record names its session and occasion,
/// read as the fact is handled, so nothing waits on a look's read. The author's answers reach it as the go-on's <c>opinion.answered</c>;
/// the service journals none of its own yet. A move judged after a judgment that read before it waits for that judgment, so the later
/// one reads the move.</para>
/// <para><b>Time as timer events.</b> A judgment that finds a turn ahead (a cool-off's reset, a reading pass's end), or a pass or a delivery
/// a drain held, judges that opinion again then; a pass or a delivery begun beside that ends without a fact (a turn in flight, a busy
/// conversation) is judged again after a backoff; a judgment that throws keeps the turn it found. These live in memory: a start judges
/// every owed opinion once (<see cref="ResumedAsync"/>), which rebuilds the gate's view and its turns. The watchdog's timers are the net
/// under all of it.</para>
/// <para><b>One judgment of an owed opinion at a time.</b> Facts of one opinion arrive in order; facts of several that name one owed
/// opinion are judged one after another, never two judgments of one at once, and the owed opinions one fact names are judged beside each
/// other.</para>
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
    private readonly SemaphoreSlim _round = new(1, 1);
    private long _heard;
    private long _rounded;

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

    /// <summary>The facts every owed opinion's judgment reads: its quests' holds and chains, its run's nodes, its rule and process.</summary>
    private static readonly HashSet<string> Everyone = new([BusTypes.QuestMoved.Kind, BusTypes.NodeBegun.Kind, BusTypes.ChoicesWritten.Kind], StringComparer.Ordinal);

    public BusSubscription Subscription { get; } = BusTypes.Subscribe(
        Name,
        [
            .. new[] { BusTypes.OpinionOwed, BusTypes.OpinionAsked, BusTypes.OpinionGiven, BusTypes.OpinionDelivered, BusTypes.OpinionAnswered, BusTypes.OpinionSaid }
                .Select(type => type.Kind),
            .. Everyone,
        ]);

    public string Obliged => BusTypes.OpinionGate;

    public async Task<BusHandled> HandleAsync(BusEnvelope entry, CancellationToken ct)
    {
        if (Everyone.Contains(entry.Kind)) await RoundAsync(ct).ConfigureAwait(false);
        else await JudgeAllAsync(await DuesOfAsync(entry, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        return Ends(entry) ? BusHandled.Ended : BusHandled.Judged;
    }

    public bool Folds(string kind) => Everyone.Contains(kind);

    /// <summary>Each owed opinion judged beside the others: one whose judgment is slow holds none of the rest.</summary>
    private Task JudgeAllAsync(IEnumerable<OpinionDue> dues, CancellationToken ct) => Task.WhenAll(dues.Select(due =>
    {
        _quiet.TryRemove(OpinionGateJudge.Subject(due), out _);
        return JudgeAsync(due, ct);
    }));

    /// <summary>
    /// A move's round: every owed opinion judged once, one round at a time; a move heard before a round began is judged by it, so a burst of
    /// moves on several subjects costs one round for those that waited.
    /// </summary>
    private async Task RoundAsync(CancellationToken ct)
    {
        var heard = Interlocked.Increment(ref _heard);
        await _round.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Interlocked.Read(ref _rounded) >= heard) return;
            var upto = Interlocked.Read(ref _heard);
            await JudgeAllAsync(_open(), ct).ConfigureAwait(false);
            Interlocked.Exchange(ref _rounded, upto);
        }
        finally
        {
            _round.Release();
        }
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

    /// <summary>
    /// The owed opinions <paramref name="entry"/> names: an owed fact by its session and occasion (a person's ask, by its session alone), the
    /// person's word by its session, an opinion's by its record's.
    /// </summary>
    private async Task<IReadOnlyList<OpinionDue>> DuesOfAsync(BusEnvelope entry, CancellationToken ct)
    {
        var open = _open();
        if (open.Count == 0) return [];
        if (entry.Kind == BusTypes.OpinionOwed.Kind)
        {
            var session = Text(entry, "session");
            var occasion = Text(entry, "occasion");
            // A person's ask carries its own occasion (asked, failure), which no owed opinion has: it reaches each of its session's.
            var pressed = Text(entry, "by") == OpinionBusFacts.Pressed;
            return [.. open.Where(due => Ids.Equals(due.Session, session) && (pressed || occasion is null || due.Occasion == occasion))];
        }

        if (entry.Kind == BusTypes.OpinionSaid.Kind)
        {
            var session = Text(entry, "session") ?? entry.Subject;
            return [.. open.Where(due => Ids.Equals(due.Session, session))];
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
            // The turn set before stands until this judgment answers, so one that throws keeps it.
            _again.TryGetValue(subject, out var pending);
            var fact = await _judge(due, Ended, ct).ConfigureAwait(false);
            if (pending is not null && _again.TryRemove(new KeyValuePair<string, CancellationTokenSource>(subject, pending))) pending.Cancel();
            if (fact is null) return;
            var now = _clock.GetUtcNow();
            DateTimeOffset? next = fact.Turns.Where(turn => turn > now).Select(turn => (DateTimeOffset?)turn).Min();
            // A pass or a delivery a drain held is judged again once a minute, so the drain's end reaches it.
            if (((fact.Act is not null && !fact.Begun) || fact.Drained) && (next is null || next > now + Drained)) next = now + Drained;
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
