using System.Runtime.ExceptionServices;

namespace Daoris.Driver;

/// <summary>The events a chain carries (FABRIC-CHAIN2, the design §C5): each a wake naming a subject, never a value.</summary>
public static class ChainEvents
{
    /// <summary>A repository's refs moved: the ref watcher, Daoris's own ref moves, a fetch's completion.</summary>
    public const string Refs = "refs";

    /// <summary>A file of the home's record folders changed: a request, the configuration, a lock.</summary>
    public const string Home = "home";

    /// <summary>A ledger family moved: the service's change stream (§A4.2), or a session leaving its tree or ending.</summary>
    public const string Ledger = "ledger";

    /// <summary>A route answered a person's or a terminal's press (D169): never the chain's own publish, which link 4 makes.</summary>
    public const string Press = "press";

    /// <summary>The window has the person's focus (the shell only).</summary>
    public const string Focus = "focus";

    /// <summary>A plugin's believed <c>work/step</c> (§C4).</summary>
    public const string PluginStep = "plugin.step";

    /// <summary>A harness's frame (§A4.4).</summary>
    public const string Harness = "harness";

    /// <summary>A process ended.</summary>
    public const string Exit = "exit";

    /// <summary>A topic's deadline: the whole topic is due (§C5). A link hears it as <see cref="TimerOf"/> its topic.</summary>
    public const string Timer = "timer";

    /// <summary>
    /// The start's reconciliation (§C6, §C7): every topic judged whole and every pending effect found from the persisted facts, so
    /// a crash between recording a fact and acting on it loses nothing. Not a source: <see cref="Chain.RecoverAsync"/> says it once.
    /// </summary>
    public const string Start = "start";

    /// <summary>The end judgement found a step reached, or no longer.</summary>
    public const string WorkStep = "work.step";

    /// <summary>The end judgement found the line holding the work, or no longer.</summary>
    public const string WorkHeld = "work.held";

    /// <summary>The end judgement found the cycle's end reached, or no longer.</summary>
    public const string WorkEnded = "work.ended";

    /// <summary>The look's tidy removed a unit, or moved it aside.</summary>
    public const string WorkTidied = "work.tidied";

    /// <summary>What a link hears of <paramref name="topic"/>'s deadline: <c>timer(&lt;topic&gt;)</c>.</summary>
    public static string TimerOf(string topic) => $"{Timer}({topic})";
}

/// <summary>One event: its type, the subject it names (a repository, a unit, a folder, a family, a topic), and the topic whose queue it came through.</summary>
public sealed record ChainEvent(string Type, string Subject, string? Topic = null)
{
    /// <summary>What a link hears it as: its type, or <c>timer(&lt;topic&gt;)</c> for a deadline.</summary>
    public string Key => Type == ChainEvents.Timer ? ChainEvents.TimerOf(Topic ?? Subject) : Type;

    /// <summary>The deadline of <paramref name="topic"/>: the whole topic, or the one subject a root of it names.</summary>
    public static ChainEvent Timer(string topic, string? subject = null) => new(ChainEvents.Timer, subject ?? topic, topic);

    /// <summary>The start's reconciliation, said once.</summary>
    internal static ChainEvent Started { get; } = new(ChainEvents.Start, ChainEvents.Start);
}

/// <summary>The sorts of link (the design §C6).</summary>
public enum ChainKind
{
    /// <summary>Fires when a fact changes, never on a fact that stands: it emits only what its judgement found changed (links 1, 3, 4).</summary>
    Fact,

    /// <summary>
    /// Level-triggered (link 2, and every later effect): pending while its condition stands and it has not happened, tried when
    /// its facts change, on the wakes that can unblock it and at its deadline, and found again at the start from the persisted facts.
    /// </summary>
    Effect,

    /// <summary>A topic's own reader, run on each wake it hears, reading its store afresh and emitting nothing into the chain (links 5-7).</summary>
    Reader,
}

/// <summary>The design §C6's links by number, which is their rank. Which rows are bound, and by whom, is D173's FABRIC-CHAIN2 note.</summary>
public static class ChainRanks
{
    /// <summary>The end judgement for a repository or a unit (§B4).</summary>
    public const int Judge = 1;

    /// <summary>The look's automatic tidy, judging again inside its hold (D88).</summary>
    public const int Tidy = 2;

    /// <summary><c>work/ended</c> to the plugins that listen.</summary>
    public const int Ended = 3;

    /// <summary><c>STATE_CHANGED</c> with the existing scopes (D169).</summary>
    public const int Publish = 4;

    /// <summary>The local plan.</summary>
    public const int Plan = 5;

    /// <summary>A plugin's <c>work/state</c> ask with a fetch of the line (§A4.3).</summary>
    public const int Ask = 6;

    /// <summary>A home folder's reader.</summary>
    public const int Read = 7;
}

/// <summary>
/// One row of the link table (the design §C6): its rank, what it hears, and what it may emit. An effect's
/// <see cref="Wakes"/> are the events that can unblock it without any of its facts changing.
/// </summary>
public sealed record ChainLink(int Rank, string Id, ChainKind Kind, IReadOnlyList<string> On, IReadOnlyList<string> Emits)
{
    /// <summary>An effect's unblocking wakes: a session leaving its tree, a review closed, a lock released, the configuration changed, its deadline, the start.</summary>
    public IReadOnlyList<string> Wakes { get; init; } = [];

    /// <summary>Whether the link hears an event by its <see cref="ChainEvent.Key"/>.</summary>
    public bool Hears(string key) => On.Contains(key, StringComparer.Ordinal) || Wakes.Contains(key, StringComparer.Ordinal);
}

/// <summary>
/// The link table (FABRIC-CHAIN2, the design §C6), ranked: a link may emit only events that links ranked above it hear, so a wake
/// runs each link at most once, in rank order, and no link wakes itself. A table that breaks a rule is never built.
/// </summary>
public sealed class ChainTable
{
    /// <summary>A table of <paramref name="links"/>, refused whole where <see cref="Check"/> finds anything.</summary>
    public ChainTable(IEnumerable<ChainLink> links)
    {
        Links = [.. links.OrderBy(link => link.Rank)];
        var problems = Check(Links);
        if (problems.Count > 0) throw new ArgumentException(string.Join(" ", problems), nameof(links));
    }

    /// <summary>The links, by rank.</summary>
    public IReadOnlyList<ChainLink> Links { get; }

    /// <summary>
    /// The fixed links of §C6. What judges, what may remove and when, what is published, and that every topic keeps its
    /// reconciliation stay fixed under any cycle (§C6); a workspace's bounded menu, if it comes, adds only effects that write no
    /// ref and no ledger family a link hears (FABRIC-CHAIN3).
    /// </summary>
    public static ChainTable Fixed { get; } = new(
    [
        new(ChainRanks.Judge, "judge", ChainKind.Fact,
            [ChainEvents.Refs, ChainEvents.TimerOf(ChangeTopics.Refs), ChainEvents.PluginStep],
            [ChainEvents.WorkStep, ChainEvents.WorkHeld, ChainEvents.WorkEnded]),
        new(ChainRanks.Tidy, "tidy", ChainKind.Effect, [ChainEvents.WorkHeld, ChainEvents.WorkEnded], [ChainEvents.WorkTidied])
        {
            // A session leaving the tree or ending; a review closed; a lock released or the configuration changed; its deadline;
            // and the start, which finds it from the persisted facts.
            Wakes = [ChainEvents.Ledger, ChainEvents.Press, ChainEvents.Home, ChainEvents.TimerOf(ChangeTopics.Refs), ChainEvents.Start],
        },
        new(ChainRanks.Ended, "ended", ChainKind.Fact, [ChainEvents.WorkEnded], []),
        new(ChainRanks.Publish, "publish", ChainKind.Fact,
            [ChainEvents.WorkStep, ChainEvents.WorkHeld, ChainEvents.WorkEnded, ChainEvents.WorkTidied], []),
        new(ChainRanks.Plan, "plan", ChainKind.Reader, [ChainEvents.Ledger, ChainEvents.TimerOf(ChangeTopics.Look)], []),
        new(ChainRanks.Ask, "ask", ChainKind.Reader, [ChainEvents.Refs, ChainEvents.Press, ChainEvents.Focus], []),
        // Each home folder's reader hears its own deadline: the requests folder's, and the record files' (FABRIC-EVENT4).
        new(ChainRanks.Read, "read", ChainKind.Reader,
        [
            ChainEvents.Home, ChainEvents.TimerOf(ChangeTopics.Requests), ChainEvents.TimerOf(ChangeTopics.Config),
            ChainEvents.TimerOf(ChangeTopics.Plugins), ChainEvents.TimerOf(ChangeTopics.Lines), ChainEvents.TimerOf(ChangeTopics.Words),
            ChainEvents.TimerOf(ChangeTopics.Update), ChainEvents.TimerOf(ChangeTopics.Help),
        ], []),
    ]);

    /// <summary>
    /// What is wrong with a table, one sentence each: two links sharing a rank; a link hearing nothing; a link emitting what a link
    /// at or below its own rank hears (a link to a lower rank, or to itself); a reader that emits; wakes on a link that is no
    /// effect; and an effect with no deadline or no start among its wakes, which a refusal or a crash would leave pending for ever.
    /// </summary>
    public static IReadOnlyList<string> Check(IEnumerable<ChainLink> links)
    {
        var all = links.ToList();
        var problems = new List<string>();
        foreach (var shared in all.GroupBy(link => link.Rank).Where(group => group.Count() > 1))
        {
            problems.Add($"links {string.Join(" and ", shared.Select(link => $"`{link.Id}`"))} share rank {shared.Key}: each link has its own.");
        }

        foreach (var link in all)
        {
            if (link.On.Count + link.Wakes.Count == 0) problems.Add($"link {link.Rank} hears nothing.");
            if (link.Kind == ChainKind.Reader && link.Emits.Count > 0)
            {
                problems.Add($"link {link.Rank} is a reader and emits `{string.Join("`, `", link.Emits)}`: a reader emits nothing into the chain.");
            }

            if (link.Kind != ChainKind.Effect && link.Wakes.Count > 0)
            {
                problems.Add($"link {link.Rank} has wakes and is no effect: only an effect is tried on what unblocks it.");
            }

            if (link.Kind == ChainKind.Effect)
            {
                if (!link.Wakes.Any(wake => wake.StartsWith(ChainEvents.Timer + "(", StringComparison.Ordinal)))
                {
                    problems.Add($"link {link.Rank} is an effect with no deadline among its wakes: a refused try would wait for ever.");
                }

                if (!link.Wakes.Contains(ChainEvents.Start, StringComparer.Ordinal))
                {
                    problems.Add($"link {link.Rank} is an effect the start does not wake: a crash between its fact and it would lose it.");
                }
            }

            foreach (var emitted in link.Emits)
            {
                foreach (var hearer in all.Where(other => other.Rank <= link.Rank && other.Hears(emitted)))
                {
                    problems.Add(
                        $"link {link.Rank} emits `{emitted}`, which link {hearer.Rank} hears at a rank not above its own: a link may wake only links ranked above it.");
                }
            }
        }

        return problems;
    }
}

/// <summary>What a link did on one wake: how many things it acted on (zero for nothing changed), and what it emits.</summary>
public sealed record ChainFired(int Acted, IReadOnlyList<ChainEvent> Emitted)
{
    /// <summary>Nothing changed, and nothing follows.</summary>
    public static ChainFired Nothing { get; } = new(0, []);

    /// <summary>A fact link's changes, each emitted: only what its judgement found changed.</summary>
    public static ChainFired Of(IReadOnlyList<ChainEvent> changed) => new(changed.Count, changed);
}

/// <summary>A unit an effect is pending for, and the moment it may first be tried, counted from a persisted fact (<c>Held.Since</c>), or none.</summary>
public sealed record ChainPending(string Unit, DateTimeOffset? From = null);

/// <summary>How one try of an effect went.</summary>
public enum ChainOutcome
{
    /// <summary>It happened: its unit no longer meets its condition (a removed unit is a trace).</summary>
    Done,

    /// <summary>It was refused (the tree busy): not an outcome; the effect stays pending.</summary>
    Refused,
}

/// <summary>One try of an effect, and what it emits once done.</summary>
public sealed record ChainTry(ChainOutcome Outcome, IReadOnlyList<ChainEvent> Emitted)
{
    /// <summary>Refused: still pending, tried again on an unblocking wake.</summary>
    public static ChainTry Refused { get; } = new(ChainOutcome.Refused, []);

    /// <summary>Done, emitting <paramref name="emitted"/>.</summary>
    public static ChainTry Done(params ChainEvent[] emitted) => new(ChainOutcome.Done, emitted);
}

/// <summary>The seam an effect link sits behind: its pending units read from the persisted facts, and one try of one unit.</summary>
public interface IChainEffect
{
    /// <summary>Every unit whose condition stands and whose effect has not happened, read from the persisted facts, never from memory.</summary>
    Task<IReadOnlyList<ChainPending>> PendingAsync(CancellationToken ct);

    /// <summary>One try on <paramref name="unit"/>, judging again before it acts. A throw leaves it pending, as a refusal does.</summary>
    Task<ChainTry> TryAsync(string unit, CancellationToken ct);
}

/// <summary>
/// The chains (FABRIC-CHAIN2, the design §C6), on the change watch's topic queue: each topic's wake becomes an event, and its
/// deadline a <c>timer</c> event (§C5); the event runs the links that hear it in rank order, each at most once, and what a link
/// emits is heard by the links above it in the same wake.
/// </summary>
/// <remarks>
/// <para><b>A fact link</b> is handed what it heard and answers what changed; a judgement that finds what it found emits nothing.
/// <b>An effect link</b> asks its seam what is pending from the persisted facts, and tries each unit at most once per wake: a unit
/// its facts name, or every pending unit on a wake that can unblock it. A refusal is not an outcome, and stays pending. A wake of
/// deadlines alone tries a unit at most once per deadline while nothing changes. The start's <see cref="RecoverAsync"/> judges
/// every topic whole and finds every pending effect, so a crash between a fact and its effect loses nothing.</para>
///
/// <para><b>Convergence</b> (§C6): ranks make a wake finite and keep a link from waking itself; an effect's echo through the world
/// (the tidy's deleted branch, heard by the ref watcher) comes back as a new wake, judged once, and emits nothing, because a fact
/// link emits only on a change and a removed unit is a trace that meets no effect's condition.</para>
///
/// <para><b>In memory only:</b> which unit was tried when, for the deadline's bound. A restart loses it, and the start's
/// reconciliation tries each pending unit once; a chain's progress is never a second record (§C7).</para>
///
/// <para>The machine log, with codes and no words (§C7): <c>chain.fired</c> (link, topic, count) when a link acted,
/// <c>chain.pending</c> (link, code, count) when an effect stays pending (<c>refused</c>, <c>failed</c>) or is found at the start
/// (<c>recovered</c>), and <c>chain.dropped</c> (code, link, event) when a link emitted outside its row (<c>undeclared</c>).</para>
/// </remarks>
public sealed class Chain
{
    private readonly ChangeWatch _watch;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private readonly Dictionary<int, Func<IReadOnlyList<ChainEvent>, CancellationToken, Task<ChainFired>>> _judges = [];
    private readonly Dictionary<int, IChainEffect> _effects = [];
    private readonly Dictionary<string, (string Event, TimeSpan Deadline)> _topics = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Rank, string Unit), DateTimeOffset> _tried = [];
    private readonly HashSet<(int Rank, string Unit)> _trying = [];

    /// <param name="watch">The topic queue the chain's sources feed; its owner starts and disposes it.</param>
    /// <param name="table">The link table: <see cref="ChainTable.Fixed"/> unless a test hands its own.</param>
    /// <param name="clock">What time it is; the system's unless a test hands its own.</param>
    public Chain(ChangeWatch watch, ChainTable? table = null, Func<DateTimeOffset>? clock = null)
    {
        _watch = watch;
        Table = table ?? ChainTable.Fixed;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>The link table the chain runs.</summary>
    public ChainTable Table { get; }

    /// <summary>Where <c>chain.*</c> lines are written; none by default.</summary>
    public MachineLog? Log { get; set; }

    /// <summary>A fact link's or a reader's behaviour: handed the events it heard on one wake, each once. A row left unbound is skipped.</summary>
    public Chain Bind(int rank, Func<IReadOnlyList<ChainEvent>, CancellationToken, Task<ChainFired>> judge)
    {
        if (Row(rank).Kind == ChainKind.Effect) throw new ArgumentException($"link {rank} is an effect, bound to an {nameof(IChainEffect)}.", nameof(rank));
        lock (_gate) _judges[rank] = judge;
        return this;
    }

    /// <summary>An effect link's seam. A row left unbound is skipped.</summary>
    public Chain Bind(int rank, IChainEffect effect)
    {
        if (Row(rank).Kind != ChainKind.Effect) throw new ArgumentException($"link {rank} is no effect.", nameof(rank));
        lock (_gate) _effects[rank] = effect;
        return this;
    }

    /// <summary>
    /// A topic on the watch whose wakes are <paramref name="event"/> events and whose deadline is a <c>timer</c> event naming it,
    /// judged whole at that deadline whether a root watches it or only hints feed it.
    /// </summary>
    public void Topic(string topic, string @event, TimeSpan deadline, int atOnce = 1)
    {
        lock (_gate) _topics[topic] = (@event, deadline);
        _watch.Topic(topic, deadline, WakeAsync, atOnce);
        _watch.Subject(topic, topic);
    }

    /// <summary>A hint from a source that is no root of the watch: a stream, a process's exit, a fetch done.</summary>
    public void Hint(string topic, string subject) => _watch.Hint(topic, subject);

    /// <summary>
    /// The start's reconciliation (§C6): one wake with every topic's deadline and the start, so every topic is judged whole and
    /// every effect's pending units, read from the persisted facts, are tried once. What the links acted on.
    /// </summary>
    public Task<int> RecoverAsync(CancellationToken ct)
    {
        string[] topics;
        lock (_gate) topics = [.. _topics.Keys];
        return FireAsync([.. topics.Select(topic => ChainEvent.Timer(topic)), ChainEvent.Started], ct);
    }

    /// <summary>The watch's judge: a deadline is the topic's <c>timer</c>; anything else that woke it (a hint, a retry, a root opened, lost or resumed) is the topic's own event.</summary>
    private Task<int> WakeAsync(ChangeWake wake, CancellationToken ct)
    {
        string type;
        lock (_gate) type = _topics.TryGetValue(wake.Topic, out var known) ? known.Event : wake.Topic;
        var cause = wake.Why == ChangeWhy.Deadline ? ChainEvent.Timer(wake.Topic, wake.Subject) : new ChainEvent(type, wake.Subject, wake.Topic);
        return FireAsync([cause], ct);
    }

    /// <summary>
    /// One wake: the links that hear <paramref name="causes"/> run in rank order, each once, and what each emits joins what the
    /// links above it hear. A link that fails leaves the others running, and the wake throws after them, so its topic tries again.
    /// What the links acted on.
    /// </summary>
    public async Task<int> FireAsync(IReadOnlyList<ChainEvent> causes, CancellationToken ct)
    {
        var heard = causes.Distinct().ToList();
        var topic = causes.Count == 1 ? causes[0].Topic ?? causes[0].Type : ChainEvents.Start;
        var acted = 0;
        List<Exception>? failures = null;
        foreach (var link in Table.Links)
        {
            ct.ThrowIfCancellationRequested();
            ChainEvent[] mine = [.. heard.Where(@event => link.Hears(@event.Key))];
            if (mine.Length == 0) continue;

            ChainFired? fired;
            try
            {
                fired = link.Kind == ChainKind.Effect
                    ? await TryAsync(link, mine, ct).ConfigureAwait(false)
                    : await JudgeAsync(link, mine, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                (failures ??= []).Add(error);
                continue;
            }

            // A row this chain's owner left unbound: its source or its effect is not this owner's.
            if (fired is null) continue;
            acted += fired.Acted;
            foreach (var emitted in fired.Emitted.Distinct())
            {
                // Outside its row: dropped, so a link can never reach one its rank does not allow, whatever its code returns.
                if (link.Emits.Contains(emitted.Key, StringComparer.Ordinal)) heard.Add(emitted);
                else Log?.Warn("chain.dropped", ("code", "undeclared"), ("link", link.Rank), ("event", emitted.Key));
            }

            if (fired.Acted > 0) Log?.Info("chain.fired", ("link", link.Rank), ("topic", topic), ("count", fired.Acted));
        }

        if (failures is [var failure]) ExceptionDispatchInfo.Capture(failure).Throw();
        if (failures is { Count: > 1 }) throw new AggregateException(failures);
        return acted;
    }

    private ChainLink Row(int rank) =>
        Table.Links.FirstOrDefault(link => link.Rank == rank) ?? throw new ArgumentException($"link {rank} is no row of the table.", nameof(rank));

    private async Task<ChainFired?> JudgeAsync(ChainLink link, ChainEvent[] heard, CancellationToken ct)
    {
        Func<IReadOnlyList<ChainEvent>, CancellationToken, Task<ChainFired>>? judge;
        lock (_gate) _judges.TryGetValue(link.Rank, out judge);
        if (judge is null) return null;
        return await judge(heard, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// An effect link on one wake: its pending units read afresh, and each it may try tried once. A unit its facts name is tried; a
    /// wake that can unblock it tries every pending unit; a wake of deadlines alone tries a unit at most once per deadline.
    /// </summary>
    private async Task<ChainFired?> TryAsync(ChainLink link, ChainEvent[] heard, CancellationToken ct)
    {
        IChainEffect? effect;
        lock (_gate) _effects.TryGetValue(link.Rank, out effect);
        if (effect is null) return null;

        var pending = await effect.PendingAsync(ct).ConfigureAwait(false);
        var now = _clock();
        var unblocked = heard.Any(@event => link.Wakes.Contains(@event.Key, StringComparer.Ordinal));
        var named = heard.Where(@event => link.On.Contains(@event.Key, StringComparer.Ordinal)).Select(@event => @event.Subject)
            .ToHashSet(StringComparer.Ordinal);
        var bound = heard.All(@event => @event.Type == ChainEvents.Timer) ? Deadline(heard) : (TimeSpan?)null;
        if (heard.Any(@event => @event.Type == ChainEvents.Start) && pending.Count > 0)
        {
            Log?.Info("chain.pending", ("link", link.Rank), ("code", "recovered"), ("count", pending.Count));
        }

        lock (_gate)
        {
            // A unit no longer pending is forgotten: its next pending starts afresh.
            foreach (var key in _tried.Keys.Where(key => key.Rank == link.Rank && !pending.Any(unit => unit.Unit == key.Unit)).ToList())
            {
                _tried.Remove(key);
            }
        }

        var done = 0;
        var refused = 0;
        var failed = 0;
        Exception? failure = null;
        var emitted = new List<ChainEvent>();
        foreach (var unit in pending.DistinctBy(unit => unit.Unit))
        {
            if (!unblocked && !named.Contains(unit.Unit)) continue;

            // Its wait, counted from a persisted fact, has not run out: a restart neither resets nor shortens it.
            if (unit.From is { } from && from > now) continue;
            var key = (link.Rank, unit.Unit);
            lock (_gate)
            {
                if (bound is { } every && _tried.TryGetValue(key, out var last) && now - last < every) continue;
                if (!_trying.Add(key)) continue;
                _tried[key] = now;
            }

            try
            {
                var tried = await effect.TryAsync(unit.Unit, ct).ConfigureAwait(false);
                if (tried.Outcome == ChainOutcome.Done)
                {
                    done++;
                    emitted.AddRange(tried.Emitted);
                }
                else
                {
                    refused++;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                failed++;
                failure ??= error;
            }
            finally
            {
                lock (_gate) _trying.Remove(key);
            }
        }

        if (refused > 0) Log?.Info("chain.pending", ("link", link.Rank), ("code", "refused"), ("count", refused));
        if (failed > 0)
        {
            Log?.Warn("chain.pending", ("link", link.Rank), ("code", "failed"), ("count", failed));
            Log?.Failed($"the chain's link {link.Rank}", failure);
        }

        return new ChainFired(done, emitted);
    }

    /// <summary>The shortest deadline among the timers heard: the least a unit waits between tries while nothing changes.</summary>
    private TimeSpan Deadline(ChainEvent[] timers)
    {
        lock (_gate)
        {
            var deadlines = timers.Select(timer => _topics.TryGetValue(timer.Topic ?? timer.Subject, out var known) ? known.Deadline : TimeSpan.Zero);
            return deadlines.DefaultIfEmpty(TimeSpan.Zero).Min();
        }
    }
}
