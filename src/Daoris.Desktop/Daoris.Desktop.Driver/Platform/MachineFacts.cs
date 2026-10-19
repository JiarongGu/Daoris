using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>How a topic's subjects are judged (PARALLEL1a, D181 points 2-4).</summary>
/// <param name="Deadline">The longest a subject goes unjudged: its reconciliation, whether anything woke it or not.</param>
/// <param name="Bound">
/// How long one judgement may take: its token is cancelled then, and its place among the topic's judgements given back, so one that
/// does not answer holds no other subject. The subject stays as last judged and is judged again at its next wake or deadline.
/// </param>
/// <param name="AtOnce">How many of the topic's subjects are judged together.</param>
public sealed record FactRules(TimeSpan Deadline, TimeSpan Bound, int AtOnce = 2)
{
    /// <summary>
    /// The least time between two judgements of one subject that wakes ask for: a burst of wakes on a busy machine is one judgement
    /// each rest, for a topic whose judgement reads the whole machine. None by default.
    /// </summary>
    public TimeSpan Rest { get; init; } = TimeSpan.Zero;
}

/// <summary>One subject's fact as last judged: its value, and when the judgement that read it began.</summary>
public sealed record JudgedFact(object Value, DateTimeOffset At);

/// <summary>
/// The machine's facts as they stand at one moment (PARALLEL1a): immutable, so a reader holds a consistent whole for as long as it
/// likes while the judges write the next one.
/// </summary>
public sealed class MachineFactsSnapshot
{
    private static readonly ImmutableDictionary<string, ImmutableDictionary<string, JudgedFact>> None =
        ImmutableDictionary.Create<string, ImmutableDictionary<string, JudgedFact>>(StringComparer.Ordinal);

    private readonly ImmutableDictionary<string, ImmutableDictionary<string, JudgedFact>> _topics;

    private MachineFactsSnapshot(ImmutableDictionary<string, ImmutableDictionary<string, JudgedFact>> topics, long version)
    {
        _topics = topics;
        Version = version;
    }

    /// <summary>Nothing judged yet.</summary>
    public static MachineFactsSnapshot Empty { get; } = new(None, 0);

    /// <summary>How many facts landed before this snapshot was written: a later one is greater.</summary>
    public long Version { get; }

    /// <summary>The fact judged for <paramref name="subject"/> of <paramref name="topic"/>; null where none was judged yet.</summary>
    public JudgedFact? Of(string topic, string subject) =>
        _topics.TryGetValue(topic, out var subjects) && subjects.TryGetValue(subject, out var fact) ? fact : null;

    /// <summary>The value judged, where it is one of <typeparamref name="T"/>; null where none was judged yet.</summary>
    public T? Value<T>(string topic, string subject) where T : class => Of(topic, subject)?.Value as T;

    /// <summary>The subjects of <paramref name="topic"/> that have a fact, in no order.</summary>
    public IReadOnlyList<string> Subjects(string topic) => _topics.TryGetValue(topic, out var subjects) ? [.. subjects.Keys] : [];

    /// <summary>This snapshot with one subject's fact written, or dropped where <paramref name="fact"/> is null.</summary>
    internal MachineFactsSnapshot With(string topic, string subject, JudgedFact? fact)
    {
        var subjects = _topics.GetValueOrDefault(topic) ?? ImmutableDictionary.Create<string, JudgedFact>(StringComparer.OrdinalIgnoreCase);
        if (fact is null && !subjects.ContainsKey(subject)) return this;
        subjects = fact is null ? subjects.Remove(subject) : subjects.SetItem(subject, fact);
        return new(_topics.SetItem(topic, subjects), Version + 1);
    }
}

/// <summary>
/// The machine's facts, kept by judges on background tasks and read as last judged (PARALLEL1a, D181 points 1-4): what a person's
/// press reads, so it never waits on a machine-wide read. One owner holds it for its life (the shell's loop), registers each topic's
/// judge, and tells it the wakes the change sources hear; a reader takes <see cref="Current"/>, which never blocks, and hears
/// <see cref="Changed"/> as each fact lands.
/// </summary>
/// <remarks>
/// <para><b>The judges' rules are the events design's (§A3).</b> A wake is a hint, never a value: the judge reads its subject
/// afresh. A subject is never judged twice at once, and one woken while it is judged is judged once more after. A topic judges at
/// most <see cref="FactRules.AtOnce"/> of its subjects together. Each subject is judged again at its topic's deadline, the
/// reconciliation behind every source, so a missed wake costs latency, never correctness.</para>
///
/// <para><b>Its own scheduling, not the change watch's drain.</b> <see cref="ChangeWatch"/> takes a topic's due subjects as one batch
/// and awaits the whole batch, so one judgement that did not answer would hold every other subject of its topic until its bound. A
/// fact's subjects are independent (D181 point 3), so each runs on its own: a judgement past its bound gives its place back, and its
/// subject stays judging, never judged beside it, until it ends.</para>
///
/// <para><b>Swapped whole.</b> Writers take a lock to build the next snapshot, and publish it with one atomic write; readers take no
/// lock. A judgement carries the context the facts were started in, never its waker's, so a press's flow (its cancellation, a test's
/// seam, a landing's memo of git's answers) never reaches a judge.</para>
/// </remarks>
public sealed class MachineFacts : IDisposable
{
    /// <summary>How often the deadlines are looked at.</summary>
    public static readonly TimeSpan Tick = TimeSpan.FromSeconds(10);

    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, TopicState> _topics = new(StringComparer.Ordinal);
    private readonly List<(string Topic, string Subject, TaskCompletionSource<MachineFactsSnapshot> Landed)> _first = [];
    private readonly CancellationTokenSource _stopping = new();
    private MachineFactsSnapshot _current = MachineFactsSnapshot.Empty;
    private ExecutionContext? _owner;
    private CancellationTokenRegistration _linked;
    private bool _started;
    private bool _disposed;

    /// <param name="clock">What time it is; the system's unless a test hands its own.</param>
    public MachineFacts(Func<DateTimeOffset>? clock = null) => _clock = clock ?? (() => DateTimeOffset.UtcNow);

    /// <summary>Where a judgement that failed or did not answer is written; none by default.</summary>
    public MachineLog? Log { get; init; }

    /// <summary>The facts as last judged: never waits, whatever is being judged.</summary>
    public MachineFactsSnapshot Current => Volatile.Read(ref _current);

    /// <summary>Told each snapshot once a fact landed in it, on the judge's thread; a handler that fails is written and passed over.</summary>
    public event Action<MachineFactsSnapshot>? Changed;

    /// <summary>Whether the judges run: a wake before is kept for the start.</summary>
    public bool Started
    {
        get
        {
            lock (_gate) return _started;
        }
    }

    /// <summary>
    /// A topic, its rules and its judge: the judge reads one subject afresh and answers its value, or null for a subject with
    /// nothing to keep (its fact is dropped). A later registration of the same topic replaces the rules and the judge.
    /// </summary>
    public void Topic(string name, FactRules rules, Func<string, CancellationToken, Task<object?>> judge)
    {
        lock (_gate)
        {
            if (_topics.TryGetValue(name, out var known))
            {
                known.Rules = rules;
                known.Judge = judge;
                known.Slots = new SemaphoreSlim(Math.Max(1, rules.AtOnce));
                return;
            }

            _topics[name] = new TopicState(name, rules, judge);
        }
    }

    /// <summary>A subject of <paramref name="topic"/> known now: judged at once where it never was, and at each deadline.</summary>
    public void Subject(string topic, string subject)
    {
        Action? start;
        lock (_gate)
        {
            if (Known(topic, subject, out var isNew) is not { } state || !isNew) return;
            start = Due(_topics[topic], state);
        }

        start?.Invoke();
    }

    /// <summary>A subject no longer known: never judged again until it is known or woken, and its fact dropped.</summary>
    public void Forget(string topic, string subject)
    {
        lock (_gate)
        {
            if (!_topics.TryGetValue(topic, out var known)) return;
            known.Subjects.Remove(subject);
        }

        Land(topic, subject, null, _clock());
    }

    /// <summary>
    /// <paramref name="subject"/> of <paramref name="topic"/> moved: it is judged again, once, after any judgement under way. A
    /// wake for a subject not known yet makes it known; one for a topic with no judge is nothing.
    /// </summary>
    public void Wake(string topic, string subject)
    {
        Action? start;
        lock (_gate)
        {
            if (Known(topic, subject, out _) is not { } state) return;
            start = Due(_topics[topic], state);
        }

        start?.Invoke();
    }

    /// <summary>
    /// The facts once <paramref name="subject"/> of <paramref name="topic"/> has a fact: at once where it has, else when its first
    /// lands. What a reader that has nothing at all to stand on waits for, within its own bound (HELPSTUCK1's backstop).
    /// </summary>
    public Task<MachineFactsSnapshot> FirstAsync(string topic, string subject, CancellationToken ct)
    {
        var landed = new TaskCompletionSource<MachineFactsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            var current = Current;
            if (current.Of(topic, subject) is not null) return Task.FromResult(current);
            if (_disposed) return Task.FromCanceled<MachineFactsSnapshot>(new CancellationToken(canceled: true));
            _first.Add((topic, subject, landed));
        }

        return landed.Task.WaitAsync(ct);
    }

    /// <summary>
    /// Wake <paramref name="subject"/> and wait for a judgement of it that began after this call to end, landed or not: a test's
    /// step, or a door that asks for the facts afresh and is willing to wait for them.
    /// </summary>
    public Task JudgedAsync(string topic, string subject, CancellationToken ct)
    {
        Action? start;
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (Known(topic, subject, out _) is not { } state) return Task.CompletedTask;
            state.Waiting.Add((state.Begun + 1, ended));
            start = Due(_topics[topic], state);
        }

        start?.Invoke();
        return ended.Task.WaitAsync(ct);
    }

    /// <summary>
    /// Start judging: every subject known or woken so far, then each at its deadline, until <paramref name="stopping"/> or the
    /// disposal. The judges carry the context this is called in.
    /// </summary>
    public void Start(CancellationToken stopping = default)
    {
        var starts = new List<Action>();
        lock (_gate)
        {
            if (_started || _disposed) return;
            _started = true;
            _owner = ExecutionContext.Capture();
            if (stopping.CanBeCanceled) _linked = stopping.Register(() => _stopping.Cancel());
            foreach (var topic in _topics.Values)
            {
                foreach (var state in topic.Subjects.Values.Where(state => state.Again))
                {
                    if (Due(topic, state) is { } start) starts.Add(start);
                }
            }
        }

        foreach (var start in starts) start();
        Launch(TickingAsync);
    }

    /// <summary>One look at the deadlines: each subject not judged within its topic's, and not being judged, is judged now.</summary>
    internal void Reconcile()
    {
        var starts = new List<Action>();
        lock (_gate)
        {
            if (!_started || _disposed) return;
            var now = _clock();
            foreach (var topic in _topics.Values)
            {
                foreach (var state in topic.Subjects.Values)
                {
                    if (state.Running || now - (state.Began ?? state.Known) < topic.Rules.Deadline) continue;
                    if (Due(topic, state) is { } start) starts.Add(start);
                }
            }
        }

        foreach (var start in starts) start();
    }

    /// <summary>Every judgement begun has ended, or been given up at its bound; a test's settling.</summary>
    internal async Task Settled()
    {
        while (true)
        {
            Task[] running;
            lock (_gate)
            {
                running = [.. _topics.Values.SelectMany(topic => topic.Subjects.Values).Select(state => state.Run)
                    .OfType<Task>().Where(run => !run.IsCompleted)];
            }

            if (running.Length == 0) return;
            await Task.WhenAll(running).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        List<TaskCompletionSource<MachineFactsSnapshot>> first;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            first = [.. _first.Select(waiting => waiting.Landed)];
            _first.Clear();
        }

        _linked.Dispose();
        _stopping.Cancel();
        foreach (var landed in first) landed.TrySetCanceled();
    }

    /// <summary>Under the lock: the subject's state, made known where it was not; null for a topic with no judge.</summary>
    private SubjectState? Known(string topic, string subject, out bool isNew)
    {
        isNew = false;
        if (_disposed || !_topics.TryGetValue(topic, out var known)) return null;
        if (known.Subjects.TryGetValue(subject, out var state)) return state;
        isNew = true;
        state = new SubjectState(subject, _clock());
        known.Subjects[subject] = state;
        return state;
    }

    /// <summary>
    /// Under the lock: the judgement to start for a subject wanted now, or null where one is under way, it rests, or the judges have
    /// not started, each of which keeps it wanted.
    /// </summary>
    private Action? Due(TopicState topic, SubjectState state)
    {
        state.Again = true;
        if (!_started || _disposed || state.Running) return null;

        var rest = topic.Rules.Rest;
        var now = _clock();
        if (rest > TimeSpan.Zero && state.Ended is { } ended && now - ended < rest)
        {
            if (!state.Resting)
            {
                state.Resting = true;
                var wait = rest - (now - ended);
                Launch(() => RestedAsync(topic.Name, state, wait));
            }

            return null;
        }

        state.Again = false;
        state.Running = true;
        state.Begun++;
        return () => Launch(() => state.Run = JudgeAsync(topic, state));
    }

    /// <summary>A subject's rest over: judged now where it was wanted meanwhile.</summary>
    private async Task RestedAsync(string topic, SubjectState state, TimeSpan wait)
    {
        try
        {
            await Task.Delay(wait, _stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Action? start = null;
        lock (_gate)
        {
            state.Resting = false;
            if (state.Again && _topics.TryGetValue(topic, out var known)) start = Due(known, state);
        }

        start?.Invoke();
    }

    /// <summary>One judgement: within the topic's places, bounded, its value landed whenever it answers.</summary>
    private async Task JudgeAsync(TopicState topic, SubjectState state)
    {
        SemaphoreSlim slots;
        Func<string, CancellationToken, Task<object?>> judge;
        TimeSpan bound;
        lock (_gate) (slots, judge, bound) = (topic.Slots, topic.Judge, topic.Rules.Bound);

        var held = false;
        try
        {
            await slots.WaitAsync(_stopping.Token).ConfigureAwait(false);
            held = true;
            var began = _clock();
            lock (_gate) state.Began = began;

            var bounded = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            bounded.CancelAfter(bound);
            Task<object?> judging;
            try
            {
                judging = judge(state.Name, bounded.Token);
            }
            catch (Exception error)
            {
                judging = Task.FromException<object?>(error);
            }

            _ = judging.ContinueWith(_ => bounded.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

            // Past its bound its place is given back, so it holds no other subject; the subject stays judging until it ends.
            using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token))
            {
                var first = await Task.WhenAny(judging, Task.Delay(bound, waiting.Token)).ConfigureAwait(false);
                waiting.Cancel();
                if (first != judging && !_stopping.IsCancellationRequested)
                {
                    slots.Release();
                    held = false;
                    Log?.Warn("facts.late", ("topic", topic.Name), ("subject", state.Name), ("boundMs", (long)bound.TotalMilliseconds));
                }
            }

            Land(topic.Name, state.Name, await judging.ConfigureAwait(false), began);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // Stopping: nothing more is judged.
        }
        catch (Exception error)
        {
            // Unknown, never changed: the subject keeps the fact last judged, and is judged again at its next wake or deadline.
            Log?.Failed($"the facts' judgement of {topic.Name}", error);
        }
        finally
        {
            if (held) slots.Release();
            Ended(topic, state);
        }
    }

    /// <summary>A judgement over: its waiters told, and the subject judged once more where it was woken meanwhile.</summary>
    private void Ended(TopicState topic, SubjectState state)
    {
        Action? start = null;
        var told = new List<TaskCompletionSource>();
        lock (_gate)
        {
            state.Running = false;
            state.Run = null;
            state.Ended = _clock();
            state.Done++;
            foreach (var waiting in state.Waiting.Where(waiting => waiting.Target <= state.Done).ToList())
            {
                state.Waiting.Remove(waiting);
                told.Add(waiting.Ended);
            }

            if (state.Again) start = Due(topic, state);
        }

        foreach (var ended in told) ended.TrySetResult();
        start?.Invoke();
    }

    /// <summary>A fact written, or dropped for a null value, in a snapshot swapped whole; its readers told after.</summary>
    private void Land(string topic, string subject, object? value, DateTimeOffset at)
    {
        MachineFactsSnapshot written;
        var landed = new List<TaskCompletionSource<MachineFactsSnapshot>>();
        lock (_gate)
        {
            if (_disposed) return;
            var current = _current;
            written = current.With(topic, subject, value is null ? null : new JudgedFact(value, at));
            if (ReferenceEquals(written, current)) return;
            Volatile.Write(ref _current, written);
            if (value is not null)
            {
                foreach (var waiting in _first.Where(waiting => waiting.Topic == topic
                             && string.Equals(waiting.Subject, subject, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    _first.Remove(waiting);
                    landed.Add(waiting.Landed);
                }
            }
        }

        foreach (var waiting in landed) waiting.TrySetResult(written);
        if (Changed is not { } changed) return;
        foreach (var handler in changed.GetInvocationList().Cast<Action<MachineFactsSnapshot>>())
        {
            try
            {
                handler(written);
            }
            catch (Exception error)
            {
                Log?.Failed("a reader of the machine's facts", error);
            }
        }
    }

    /// <summary>The deadlines looked at each <see cref="Tick"/>, until stopped.</summary>
    private async Task TickingAsync()
    {
        using var timer = new PeriodicTimer(Tick);
        try
        {
            while (await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false)) Reconcile();
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
    }

    /// <summary>Work started on the pool in the owner's context, never the caller's.</summary>
    private void Launch(Func<Task> work)
    {
        if (_owner is { } owner)
        {
            ExecutionContext.Run(owner, static state => _ = Task.Run((Func<Task>)state!), work);
            return;
        }

        using (ExecutionContext.SuppressFlow()) _ = Task.Run(work);
    }

    private sealed class TopicState(string name, FactRules rules, Func<string, CancellationToken, Task<object?>> judge)
    {
        public string Name { get; } = name;

        public FactRules Rules { get; set; } = rules;

        public Func<string, CancellationToken, Task<object?>> Judge { get; set; } = judge;

        public SemaphoreSlim Slots { get; set; } = new(Math.Max(1, rules.AtOnce));

        public Dictionary<string, SubjectState> Subjects { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SubjectState(string name, DateTimeOffset known)
    {
        public string Name { get; } = name;

        /// <summary>When it became known: its first deadline counts from here where it was never judged.</summary>
        public DateTimeOffset Known { get; } = known;

        /// <summary>Wanted: woken, or known and never judged, while it could not be judged yet.</summary>
        public bool Again { get; set; }

        /// <summary>A judgement is under way, or given up at its bound and not ended.</summary>
        public bool Running { get; set; }

        public Task? Run { get; set; }

        public bool Resting { get; set; }

        public DateTimeOffset? Began { get; set; }

        public DateTimeOffset? Ended { get; set; }

        public long Begun { get; set; }

        public long Done { get; set; }

        public List<(long Target, TaskCompletionSource Ended)> Waiting { get; } = [];
    }
}
