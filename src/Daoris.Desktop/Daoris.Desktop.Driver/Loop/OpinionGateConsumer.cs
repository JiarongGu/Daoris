using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>What the gate asks of one owed opinion: the opinion, and what woke it, a fact by its kind or one of the gate's own turns.</summary>
internal sealed record GateWork(OpinionDue Due, string Woke)
{
    public bool ByFact => Woke.Contains('/', StringComparison.Ordinal);
}

/// <summary>
/// The opinion's gate as a consumer of the dispatch (<see cref="BusFlowConsumer{TWork}"/>): each opinion fact it is handed (owed, asked, given,
/// delivered, answered, the person's word) judges that owed opinion's gate at once by its rule (<see cref="OpinionGateJudge.JudgeDueAsync"/>):
/// deliver, recheck, pass by the policy, or wait named. A move of the quests, of a run's nodes or of the standing choices judges the owed
/// opinions it reaches, since their holds, chain, graph's opinion node, rule and process are read from them. Its key is the owed opinion; the
/// layer gives it one judgment of an owed opinion at a time, one of a repository at a time (each runs git's content proof) and
/// <see cref="Bounds"/> keys across repositories, coalesces the facts waiting on one opinion, and turns the judgment into the
/// acknowledgement: no look judges an opinion.
/// </summary>
/// <remarks>
/// <para><b>What it reads, it reads at the fact.</b> A fact names an opinion by its id; the opinion's record names its session and occasion,
/// read as the fact is handled, so nothing waits on a look's read. The author's answers reach it as the go-on's <c>opinion.answered</c>;
/// the service journals none of its own yet. A move judged after a judgment that read before it waits for that judgment, so the later
/// one reads the move; a move names the quests and repositories it changed, and judges only what they reach.</para>
/// <para><b>Time as timer events.</b> A judgment that finds a turn ahead (a cool-off's reset, a reading pass's end), or a pass or a delivery
/// a drain held, judges that opinion again then, asked of the layer as a fact's work is; a pass or a delivery begun beside that ends without
/// a fact (a turn in flight, a busy conversation) is judged again after a backoff; a judgment that throws keeps the turn it found. These live
/// in memory: a start judges every owed opinion once (<see cref="ResumedAsync"/>), which rebuilds the gate's view and its turns. The
/// watchdog's timers are the net under all of it.</para>
/// </remarks>
internal sealed class OpinionGateConsumer : BusFlowConsumer<GateWork>, IDisposable
{
    /// <summary>Its durable name in the queue.</summary>
    public const string Name = "opinion-gate";

    /// <summary>Its bounds: the git bound's figure of owed opinions at once across every repository, one within a repository.</summary>
    public static readonly BusFlowBounds Bounds = new(KeysAtOnce: 4, PerGroup: 1);

    /// <summary>The first wait before a pass or a delivery that ended without a fact is judged again, doubled at each until <see cref="AgainMost"/>.</summary>
    public static readonly TimeSpan AgainFirst = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan AgainMost = TimeSpan.FromMinutes(10);

    /// <summary>The wait before a pass a draining update held is judged again.</summary>
    public static readonly TimeSpan Drained = TimeSpan.FromMinutes(1);

    /// <summary>The gate's own wakes, which no fact carries, measured beside the facts' kinds (<see cref="BusMeasures.Gate"/>).</summary>
    public const string WokeResumed = "resumed", WokeTurn = "turn", WokeDrained = "drained", WokeHeld = "held", WokeBeside = "beside", WokeRetry = "retry";

    private readonly Func<OpinionDue, Action<string>, CancellationToken, Task<OpinionFact?>> _judge;
    private readonly Func<string, CancellationToken, Task<OpinionView?>> _read;
    private readonly Func<IReadOnlyList<OpinionDue>> _open;
    private readonly TimeProvider _clock;
    private readonly Action<string, string> _warn;
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
        : base(Name, Reads, Bounds, clock)
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
        service.Opinions.ReadAsync, () => new OpinionDues(home).OpenStrict(), bus.Clock,
        (once, why) => bus.Warn("gate:" + once, "opinion.gate-unjudged", ("why", why)));

    /// <summary>The facts every owed opinion's judgment reads: its quests' holds and chains, its run's nodes, its rule and process.</summary>
    private static readonly HashSet<string> Everyone = new([BusTypes.QuestMoved.Kind, BusTypes.NodeBegun.Kind, BusTypes.ChoicesWritten.Kind], StringComparer.Ordinal);

    private static readonly string[] Reads =
    [
        .. new[] { BusTypes.OpinionOwed, BusTypes.OpinionAsked, BusTypes.OpinionGiven, BusTypes.OpinionDelivered, BusTypes.OpinionAnswered, BusTypes.OpinionSaid }
            .Select(type => type.Kind),
        .. Everyone,
    ];

    /// <summary>The owed opinion now owed under <paramref name="key"/>, judged again by a retry; none where it has closed.</summary>
    protected override Task<BusFlowPiece<GateWork>?> RetryWorkAsync(string key, string kind, CancellationToken ct) =>
        Task.FromResult(_open().FirstOrDefault(due => Ids.Equals(OpinionGateJudge.Subject(due), key)) is { } owed ? Piece(owed, BusTypes.FlowRetry.Kind) : null);

    public override string Obliged => BusTypes.OpinionGate;

    /// <summary>Whether its acknowledgement of <paramref name="entry"/> ends the entry's obligation (<see cref="OpinionBusFacts.EndsAtTheGate"/>).</summary>
    public static Func<BusEnvelope, bool> Ends => OpinionBusFacts.EndsAtTheGate;

    protected override bool Coalesces(string kind) => Everyone.Contains(kind);

    protected override bool Settles(BusEnvelope entry) => Ends(entry);

    protected override GateWork Merge(GateWork earlier, GateWork later) => new(later.Due, later.ByFact || !earlier.ByFact ? later.Woke : earlier.Woke);

    /// <summary>The owed opinions a fact reaches, each a piece of work keyed by the opinion and grouped by its repository.</summary>
    protected override async Task<IReadOnlyList<BusFlowPiece<GateWork>>> PiecesAsync(BusEnvelope entry, CancellationToken ct)
    {
        var dues = Everyone.Contains(entry.Kind) ? Reached(entry) : await DuesOfAsync(entry, ct).ConfigureAwait(false);
        return [.. dues.Select(due => Piece(due, entry.Kind))];
    }

    private static BusFlowPiece<GateWork> Piece(OpinionDue due, string woke) => new(OpinionGateJudge.Subject(due), new GateWork(due, woke), due.Repository);

    /// <summary>
    /// The owed opinions a move reaches: one that names its quests and repositories reaches the opinions they reach
    /// (<see cref="QuestMoves.Reaches"/>); a node begun, the standing choices written, and a move that names nothing reach every one.
    /// </summary>
    private IEnumerable<OpinionDue> Reached(BusEnvelope entry)
    {
        var quests = entry.Kind == BusTypes.QuestMoved.Kind ? entry.Payload.FirstOrDefault(pair => pair.Key == "quests").Value as IReadOnlyList<string> : null;
        var repositories = entry.Kind == BusTypes.QuestMoved.Kind ? entry.Payload.FirstOrDefault(pair => pair.Key == "repositories").Value as IReadOnlyList<string> : null;
        if (quests is null || repositories is null) return _open();
        HashSet<string> questSet = new(quests, Ids), repositorySet = new(repositories, Ids);
        return _open().Where(due => QuestMoves.Reaches(due, questSet, repositorySet));
    }

    /// <summary>One owed opinion judged, and its next turn set from what it found; the layer has made it the only judgment of that opinion and of its repository now.</summary>
    protected override async Task<BusFlowOutcome> MoveAsync(string subject, GateWork work, CancellationToken ct)
    {
        using var asking = GitAskers.Is(GitAskers.Gate);
        var due = work.Due;
        Measured(BusMeasures.Gate, work.Woke);
        if (work.ByFact) _quiet.TryRemove(subject, out _);
        // The turn set before stands until this judgment answers, so one that throws keeps it.
        _again.TryGetValue(subject, out var pending);
        var fact = await _judge(due, Ended, ct).ConfigureAwait(false);
        if (pending is not null && _again.TryRemove(new KeyValuePair<string, CancellationTokenSource>(subject, pending))) pending.Cancel();
        if (fact is null) return BusFlowOutcome.Moved;
        var now = _clock.GetUtcNow();
        DateTimeOffset? next = fact.Turns.Where(turn => turn > now).Select(turn => (DateTimeOffset?)turn).Min();
        var woke = WokeTurn;
        // A pass or a delivery a drain held is judged again once a minute, so the drain's end reaches it.
        if (((fact.Act is not null && !fact.Begun) || fact.Drained) && (next is null || next > now + Drained))
        {
            next = now + Drained;
            woke = fact.Drained ? WokeDrained : WokeHeld;
        }

        if (next is { } at) Again(due, at, woke);
        return BusFlowOutcome.Moved;
    }

    /// <summary>Every owed opinion judged once at the start: the gate's view and its turns rebuilt, nothing owed left to a look.</summary>
    public override async Task ResumedAsync(CancellationToken ct)
    {
        foreach (var due in _open())
        {
            var outcome = await AskAsync(Piece(due, WokeResumed), ct).ConfigureAwait(false);
            if (outcome.Result != BusFlowResult.Failed) continue;
            _warn("resumed:" + OpinionGateJudge.Subject(due), outcome.Why);
            Again(due, _clock.GetUtcNow() + AgainFirst, WokeRetry);
        }
    }

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

    /// <summary>A pass or a delivery begun beside ended: judged again after a backoff, unless a fact judges it first.</summary>
    private void Ended(string subject) => Ended(subject, WokeBeside);

    private void Ended(string subject, string woke)
    {
        if (_open().FirstOrDefault(due => Ids.Equals(OpinionGateJudge.Subject(due), subject)) is not { } due) return;
        var quiet = _quiet.AddOrUpdate(subject, 1, (_, was) => Math.Min(was + 1, 16));
        var wait = TimeSpan.FromTicks(Math.Min(AgainMost.Ticks, AgainFirst.Ticks << Math.Min(10, quiet - 1)));
        Again(due, _clock.GetUtcNow() + wait, woke);
    }

    /// <summary>The owed opinion judged again at <paramref name="at"/>, replacing any turn set before; a failure is said once and tried after a backoff.</summary>
    private void Again(OpinionDue due, DateTimeOffset at, string woke)
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
                var outcome = await AskAsync(Piece(owed, woke), _closing.Token).ConfigureAwait(false);
                if (outcome.Result != BusFlowResult.Failed) return;
                _warn("again:" + subject, outcome.Why);
                Ended(subject, WokeRetry);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                _warn("again:" + subject, error.Message);
                Ended(subject, WokeRetry);
            }
        }, CancellationToken.None);
    }

    private static string? Text(BusEnvelope entry, string key) => entry.Payload.FirstOrDefault(pair => pair.Key == key).Value as string;

    private static readonly StringComparer Ids = StringComparer.OrdinalIgnoreCase;
}
