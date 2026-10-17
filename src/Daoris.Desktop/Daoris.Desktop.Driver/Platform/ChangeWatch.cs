using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>What a source says moved under a root it watches: a hint, never a value (FABRIC-EVENT2, the design §A3).</summary>
public enum ChangeKind
{
    Created,
    Changed,
    Deleted,

    /// <summary>Arrived under this name by a rename; the name it left is told as <see cref="Deleted"/> first.</summary>
    Renamed,
}

/// <summary>Why a subject is judged, as the machine log's <c>change.judged</c> writes it.</summary>
public static class ChangeWhy
{
    /// <summary>Its root said something it hears moved, and the window has been quiet since.</summary>
    public const string Hint = "hint";

    /// <summary>Its topic's deadline came with no judgement since: the reconciliation behind every source.</summary>
    public const string Deadline = "deadline";

    /// <summary>Its last judgement failed, so it is unknown and tried again, sooner than its deadline.</summary>
    public const string Retry = "retry";

    /// <summary>Its root's folder, absent until now, was opened at a reconciliation.</summary>
    public const string Opened = "opened";

    /// <summary>Its root was opened again: a back-off after a loss ended, or the folder was replaced at its path.</summary>
    public const string Reopened = "reopened";

    /// <summary>Its root's watcher overflowed or lost its handle: what it missed is unknown.</summary>
    public const string Lost = "lost";

    /// <summary>The machine resumed from sleep: every root is reopened and judged.</summary>
    public const string Resumed = "resumed";

    /// <summary>
    /// This process moved it itself (FABRIC-END3: a tree opened, <i>Accept</i>, the tidy's removals): judged at once, without the quiet
    /// window, and the watcher's echo already heard is taken up with it (<see cref="ChangeWatch.Now"/>).
    /// </summary>
    public const string Own = "own";
}

/// <summary>The topics a change wakes: each is one judge's, and says which of its stores to read again.</summary>
public static class ChangeTopics
{
    /// <summary>The requests and presses a terminal writes into the home's <c>sessions/requests/</c> (row 7).</summary>
    public const string Requests = "requests";

    /// <summary>A repository's refs, heard in its common git dir (§A4.1): the tidy's pace is its deadline (§C5), FABRIC-END3 its root.</summary>
    public const string Refs = "refs";

    /// <summary>The part of the look no event yet carries, which the ledger wakes: <c>pollSeconds</c> is its deadline (§C5).</summary>
    public const string Look = "look";

    /// <summary>
    /// A ledger family moved, the subject naming it (§A4.2's change stream): its wake runs the look's local plan alone, the remote
    /// sync left to the <see cref="Look"/> topic's deadline (FABRIC-EVENT3a).
    /// </summary>
    public const string Ledger = "ledger";

    /// <summary>The driver's configuration file (<c>driver.json</c>), heard moving; the loop reads it afresh at each look (FABRIC-EVENT4, link 7).</summary>
    public const string Config = "config";

    /// <summary>The plugins folder, heard when a plugin arrives, leaves or changes its manifest (FABRIC-EVENT4, link 7).</summary>
    public const string Plugins = "plugins";

    /// <summary><c>lines-moved.json</c>, which says a repository's line moved and is to be followed (FABRIC-EVENT4, link 7).</summary>
    public const string Lines = "lines";

    /// <summary>The held words' file under the sessions folder, which another process or a clear may change (FABRIC-EVENT4, link 7).</summary>
    public const string Words = "words";

    /// <summary>
    /// The landing gate's files (FABRIC-END7): a second opinion's record, where the person's answer is kept, and the workflows, a
    /// version and a run's binding with the person's say-so. Another process's write wakes the look's plan, whose landing looks at
    /// the due entries a gate holds.
    /// </summary>
    public const string Gates = "gates";

    /// <summary>
    /// A proposal arriving in Ask Daoris's folder, written by the connector in another process (FABRIC-EVENT5a): told to the window
    /// as <c>help</c>, which needs no look, so the shell's watch alone listens for it.
    /// </summary>
    public const string Help = "help";

    /// <summary>The staged update's folder and the person's request, the installing shell's alone (FABRIC-EVENT4, link 7).</summary>
    public const string Update = "update";

    /// <summary>
    /// A timer the units' node records persist came due (FABRIC-RUN1: a retry's delay, a running answer's wait, an attempt's deadline): a
    /// wake of the evaluation runtime, link 8, at the moment the record names rather than at the look's deadline.
    /// </summary>
    public const string Nodes = "nodes";

    /// <summary>The window has the person's focus (FABRIC-END6): a moment to ask about an open pull request, the shell's alone (link 6).</summary>
    public const string Focus = "focus";
}

/// <summary>One judgement asked: of which topic's subject, and why. A subject is a name, never a path.</summary>
public sealed record ChangeWake(string Topic, string Subject, string Why);

/// <summary>
/// The seam a watcher sits behind (FABRIC-EVENT2): <see cref="FolderWatcherSource"/> on the machine, a fake in a test that raises
/// moves, overflows, renames and losses, or raises nothing at all.
/// </summary>
public interface IChangeSource
{
    /// <summary>A watch on <paramref name="folder"/>, telling <paramref name="sink"/>; null where the folder is absent or cannot be watched now.</summary>
    IChangeHandle? Open(string folder, bool subfolders, IChangeSink sink);
}

/// <summary>What a source tells, from whatever thread it raises on.</summary>
public interface IChangeSink
{
    /// <summary><paramref name="name"/>, under the folder with <c>/</c> between its parts, moved.</summary>
    void Moved(string name, ChangeKind kind);

    /// <summary>The watcher overflowed or lost its handle: <paramref name="why"/> is a word, never a path.</summary>
    void Lost(string why);
}

/// <summary>One open watch on one folder.</summary>
public interface IChangeHandle : IDisposable
{
    /// <summary>Whether the folder at the path is still the one this handle opened: false once it is gone or was replaced.</summary>
    bool Same { get; }
}

/// <summary>
/// The change watch (FABRIC-EVENT2, the design §A3 and §A4.1): roots a source watches, each hint debounced per topic and subject
/// into its topic's queue, drained in one step by the topic's judge, one judgement per subject at a time, with each topic's
/// deadline as the reconciliation behind every source, and the watcher's recovery.
/// </summary>
/// <remarks>
/// <para><b>A hint is a wake, never a fact</b> (D173's events note; the design §A1). A source says a topic and a subject, and the
/// judge reads its store afresh: a hint's path is never read as a value, and a replace is told as a deletion then a rename (§A5).
/// A missed hint costs latency, never correctness, since each subject is judged at its topic's deadline whatever was heard; an
/// extra one costs one judgement.</para>
///
/// <para><b>The queue.</b> A hint waits for <see cref="Quiet"/> after the last for its subject, at most <see cref="Cap"/> after the
/// first, then joins its topic's due set. The topic's drain takes the whole set in one swap, so a hint that arrives during a
/// judgement is kept for the next one, and a topic's subjects are judged at most <c>atOnce</c> together, each once, never twice
/// at once.</para>
///
/// <para><b>Recovery</b>, since a watcher fails quietly as well as loudly. Each open has a generation, and a callback from an old
/// one is dropped. A loss judges its root at once and reopens it, with a back-off once it is lost again within <see cref="Calm"/>,
/// and after <see cref="Tries"/> tries leaves it to its deadline, said once. A root absent, gone, or replaced at its path is found
/// at its reconciliation, never in a loop. A wait that overran by <see cref="Asleep"/> was the machine asleep: every root is
/// reopened and judged (<see cref="Resumed"/>, which a host that hears the power events may call itself). At most
/// <see cref="MostRoots"/> roots are watched; the rest stay on their deadline, said once.</para>
///
/// <para><b>Its own writes wake nothing.</b> A root hears only what its filter names, so a judge's writes (an answer, a take, a
/// log line) are outside it: the filter is the guard, and each consumer's tests hold it.</para>
///
/// <para>In <c>Platform</c>, not a <c>Domain</c>: a <see cref="FileSystemWatcher"/> is I/O (the structure rule). Domain code takes
/// the hints, never the watcher.</para>
/// </remarks>
public sealed class ChangeWatch : IAsyncDisposable
{
    /// <summary>The quiet window after a subject's last hint.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);

    /// <summary>The longest a subject's hints are coalesced, so a steady stream still wakes.</summary>
    public static readonly TimeSpan Cap = TimeSpan.FromSeconds(2);

    /// <summary>A root lost again within this is lost again, and backs off.</summary>
    public static readonly TimeSpan Calm = TimeSpan.FromMinutes(1);

    /// <summary>How often a root lost again within <see cref="Calm"/> is reopened before it is left to its deadline.</summary>
    public const int Tries = 3;

    /// <summary>How long after each of its <see cref="Tries"/> losses a root is reopened.</summary>
    public static readonly IReadOnlyList<TimeSpan> Backoff = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)];

    /// <summary>How soon a failed judgement is tried again, doubled at each failure, never past its topic's deadline.</summary>
    public static readonly TimeSpan Retry = TimeSpan.FromSeconds(1);

    /// <summary>The most roots watched at once: each holds a handle and a buffer the system pins.</summary>
    public const int MostRoots = 32;

    /// <summary>The longest the watch sleeps between its steps.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromMinutes(1);

    /// <summary>A sleep that overran by this much was the machine asleep.</summary>
    public static readonly TimeSpan Asleep = TimeSpan.FromSeconds(30);

    private readonly IChangeSource _source;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, TopicState> _topics = new(StringComparer.Ordinal);
    private readonly List<Root> _roots = [];
    private readonly Dictionary<(string Topic, string Subject), SubjectState> _subjects = [];
    private readonly Dictionary<(string Topic, string Subject), HintState> _hinted = [];
    private readonly List<IChangeHandle> _closing = [];
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _poke = new(0, 1);
    private Task _pumping = Task.CompletedTask;
    private bool _open;
    private bool _resumed;
    private bool _cappedSaid;
    private bool _disposed;
    private int _dropped;
    private int _judgements;

    /// <param name="source">What watches the folders: the machine's watchers unless a test hands its own.</param>
    /// <param name="clock">What time it is; the system's unless a test hands its own.</param>
    public ChangeWatch(IChangeSource? source = null, Func<DateTimeOffset>? clock = null)
    {
        _source = source ?? new FolderWatcherSource();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Where each judgement that was not an idle deadline, each loss and each failure is written; none by default.</summary>
    public MachineLog? Log { get; set; }

    /// <summary>Callbacks dropped because their watcher's generation was no longer the root's.</summary>
    internal int Dropped => Volatile.Read(ref _dropped);

    /// <summary>Judgements begun.</summary>
    internal int Judgements => Volatile.Read(ref _judgements);

    /// <summary>Whether a judgement runs or waits to run.</summary>
    internal bool Judging
    {
        get
        {
            lock (_gate) return _topics.Values.Any(topic => topic.Draining is not null || topic.Due.Count > 0);
        }
    }

    /// <summary>A topic, its judge, the longest any of its subjects goes unjudged, and how many it judges at once.</summary>
    /// <param name="judge">Reads the subject's truth afresh, and answers how many things it acted on: zero for nothing moved.</param>
    public void Topic(string name, TimeSpan deadline, Func<ChangeWake, CancellationToken, Task<int>> judge, int atOnce = 1)
    {
        lock (_gate) _topics[name] = new TopicState(name, deadline, judge, Math.Max(1, atOnce));
    }

    /// <summary>A folder whose moves wake <paramref name="topic"/>'s judge of <paramref name="subject"/>, where <paramref name="hears"/> names them.</summary>
    /// <param name="hears">A move's name under the folder, with <c>/</c> between parts, and its kind: whether it wakes the judge.</param>
    public void Watch(string topic, string subject, string folder, Func<string, ChangeKind, bool> hears, bool subfolders = false)
    {
        lock (_gate)
        {
            if (!_topics.ContainsKey(topic)) throw new ArgumentException($"`{topic}` is no topic of this watch.", nameof(topic));
            var root = new Root(topic, subject, folder, subfolders, hears) { Capped = _roots.Count(other => !other.Capped) >= MostRoots };
            _roots.Add(root);
            _subjects.TryAdd((topic, subject), new SubjectState { Last = _clock() });
            if (root.Capped) SayCapped(root);
            else if (_open) Open(root);
        }
    }

    /// <summary>
    /// A subject judged at its topic's deadline though no root names it (FABRIC-CHAIN2): a topic only hints feed keeps its
    /// reconciliation, and a chain's topic is judged whole there (§C5's <c>timer</c>).
    /// </summary>
    public void Subject(string topic, string subject)
    {
        lock (_gate)
        {
            if (!_topics.ContainsKey(topic)) throw new ArgumentException($"`{topic}` is no topic of this watch.", nameof(topic));
            _subjects.TryAdd((topic, subject), new SubjectState { Last = _clock() });
        }
    }

    /// <summary>A hint from a source that is no root of this watch: a stream, a process's exit, a fetch done.</summary>
    public void Hint(string topic, string subject)
    {
        lock (_gate)
        {
            if (_disposed) return;
            Heard(topic, subject, _clock());
        }

        Poke();
    }

    /// <summary>
    /// This process moved <paramref name="subject"/> itself and is done writing (FABRIC-END3, the design §B4): due at once, without the
    /// quiet window, so the next read does not wait on the debounce. A watcher's events arrive before the writer's process exits (§A5,
    /// measured), so the echo already heard is taken up with it, and the move is judged once.
    /// </summary>
    public void Now(string topic, string subject)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _hinted.Remove((topic, subject));
            Due(topic, subject, ChangeWhy.Own, _clock());
        }

        Poke();
    }

    /// <summary>
    /// Let go of every root naming <paramref name="topic"/>'s <paramref name="subject"/> (FABRIC-END3: a repository that no longer holds
    /// Daoris's work): its watchers closed, so nothing they still say is heard, and a root the cap left on its deadline takes each place
    /// given back, opened and judged.
    /// </summary>
    public void Unwatch(string topic, string subject)
    {
        lock (_gate)
        {
            var gone = _roots.Where(root => root.Topic == topic && root.Subject == subject).ToList();
            if (gone.Count == 0) return;
            foreach (var root in gone)
            {
                Close(root);
                _roots.Remove(root);
            }

            _subjects.Remove((topic, subject));
            _hinted.Remove((topic, subject));
            foreach (var waiting in _roots.Where(root => root.Capped).Take(gone.Count(root => !root.Capped)).ToList())
            {
                waiting.Capped = false;
                if (_open && Open(waiting) is { } opened) Due(waiting.Topic, waiting.Subject, opened, null);
            }
        }

        Release();
        Poke();
    }

    /// <summary>The machine resumed: every root is reopened and judged at the next step.</summary>
    public void Resumed()
    {
        lock (_gate) _resumed = true;
        Poke();
    }

    /// <summary>Open every root and step whenever something is due, until disposed.</summary>
    public void Start()
    {
        Open();
        _pumping = Task.Run(PumpingAsync);
    }

    /// <summary>Open every root, without stepping: a test steps by hand with <see cref="Pump"/>.</summary>
    internal void Open()
    {
        lock (_gate)
        {
            _open = true;
            var now = _clock();
            foreach (var root in _roots.Where(root => !root.Capped)) Open(root);
            foreach (var subject in _subjects.Values) subject.Last = now;
        }

        Release();
    }

    /// <summary>
    /// One step at the clock's time: losses and a resume handled, back-offs ended, quiet windows closed, deadlines reached and their
    /// roots reconciled, and each topic with subjects due drained beside it.
    /// </summary>
    internal void Pump()
    {
        lock (_gate)
        {
            if (_disposed) return;
            var now = _clock();

            if (_resumed)
            {
                _resumed = false;
                foreach (var root in _roots.Where(root => !root.Capped))
                {
                    Close(root);
                    root.ReopenAt = null;
                    Open(root);
                    Due(root.Topic, root.Subject, ChangeWhy.Resumed, null);
                }
            }

            foreach (var root in _roots.Where(root => root.LostWhy is not null)) Lose(root, now);

            foreach (var root in _roots.Where(root => root.ReopenAt <= now))
            {
                root.ReopenAt = null;
                if (Open(root) is not null) Due(root.Topic, root.Subject, ChangeWhy.Reopened, null);
            }

            foreach (var (key, hinted) in _hinted.Where(pair => pair.Value.FireAt <= now).ToList())
            {
                _hinted.Remove(key);
                Due(key.Topic, key.Subject, ChangeWhy.Hint, hinted.First);
            }

            foreach (var ((topicName, subjectName), subject) in _subjects)
            {
                if (!_topics.TryGetValue(topicName, out var topic) || topic.Due.ContainsKey(subjectName)) continue;
                var deadline = now - subject.Last >= topic.Deadline;
                var retry = subject.RetryAt <= now;
                if (!deadline && !retry) continue;

                subject.Last = now;
                subject.RetryAt = null;
                var why = deadline ? ChangeWhy.Deadline : ChangeWhy.Retry;
                if (deadline)
                {
                    foreach (var root in _roots.Where(root => root.Topic == topicName && root.Subject == subjectName))
                    {
                        if (Reconcile(root, now) is { } opened) why = opened;
                    }
                }

                Due(topicName, subjectName, why, null);
            }

            foreach (var topic in _topics.Values.Where(topic => topic.Due.Count > 0 && topic.Draining is null))
            {
                topic.Draining = Task.Run(() => DrainAsync(topic));
            }
        }

        Release();
    }

    /// <summary>Every judgement begun, and every one it leads to, ended.</summary>
    internal async Task Settled()
    {
        while (true)
        {
            Task[] draining;
            lock (_gate) draining = [.. _topics.Values.Select(topic => topic.Draining).OfType<Task>()];
            if (draining.Length == 0) return;
            await Task.WhenAll(draining).ConfigureAwait(false);
        }
    }

    private async Task PumpingAsync()
    {
        var token = _stopping.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                Pump();
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // A step that failed past its own catches is said, and the next step runs at the next wake.
                Log?.Failed("the change watch", error);
            }

            TimeSpan wait;
            DateTimeOffset before;
            lock (_gate)
            {
                before = _clock();
                wait = Wait(before);
            }

            try
            {
                await _poke.WaitAsync(wait, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // A sleep that overran was the machine asleep: what changed meanwhile is unknown (§A4.1).
            if (_clock() - before > wait + Asleep) Resumed();
        }
    }

    /// <summary>How long until something is due: a quiet window, a deadline, a retry or a back-off; at most <see cref="Longest"/>.</summary>
    private TimeSpan Wait(DateTimeOffset now)
    {
        var next = now + Longest;
        foreach (var hinted in _hinted.Values) next = Min(next, hinted.FireAt);
        foreach (var ((topicName, subjectName), subject) in _subjects)
        {
            if (!_topics.TryGetValue(topicName, out var topic) || topic.Due.ContainsKey(subjectName)) continue;
            next = Min(next, subject.Last + topic.Deadline);
            if (subject.RetryAt is { } retry) next = Min(next, retry);
        }

        foreach (var root in _roots)
        {
            if (root.ReopenAt is { } reopen) next = Min(next, reopen);
        }

        var wait = next - now;
        return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
    }

    private static DateTimeOffset Min(DateTimeOffset one, DateTimeOffset other) => one < other ? one : other;

    private async Task DrainAsync(TopicState topic)
    {
        while (true)
        {
            Dictionary<string, Waking> batch;
            DateTimeOffset taken;
            lock (_gate)
            {
                if (topic.Due.Count == 0 || _stopping.IsCancellationRequested)
                {
                    topic.Draining = null;
                    break;
                }

                // Swapped whole, so a hint that lands while these are judged waits for the next batch, never beside it.
                batch = topic.Due;
                topic.Due = new(StringComparer.Ordinal);
                taken = _clock();
                foreach (var name in batch.Keys)
                {
                    if (_subjects.TryGetValue((topic.Name, name), out var subject)) subject.Last = taken;
                }
            }

            try
            {
                await Parallel.ForEachAsync(
                        batch,
                        new ParallelOptions { MaxDegreeOfParallelism = topic.AtOnce, CancellationToken = _stopping.Token },
                        (due, ct) => new ValueTask(JudgeAsync(topic, due.Key, due.Value, taken, ct)))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                lock (_gate) topic.Draining = null;
                break;
            }
        }

        // The next deadline counts from these judgements: the step that was asleep on the old one looks again.
        Poke();
    }

    private async Task JudgeAsync(TopicState topic, string subject, Waking waking, DateTimeOffset taken, CancellationToken ct)
    {
        Interlocked.Increment(ref _judgements);
        var started = Stopwatch.GetTimestamp();
        var acted = 0;
        Exception? failed = null;
        try
        {
            acted = await topic.Judge(new ChangeWake(topic.Name, subject, waking.Why), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error)
        {
            failed = error;
        }

        var ms = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        lock (_gate)
        {
            if (_subjects.TryGetValue((topic.Name, subject), out var known))
            {
                if (failed is null)
                {
                    known.Fails = 0;
                    known.RetryAt = null;
                }
                else
                {
                    // Unknown, never changed: tried again sooner than the deadline, and later each time it fails again.
                    known.Fails++;
                    var backoff = Retry * Math.Pow(2, Math.Min(known.Fails - 1, 16));
                    known.RetryAt = _clock() + (backoff < topic.Deadline ? backoff : topic.Deadline);
                }
            }
        }

        if (failed is not null) Log?.Failed($"the change watch's judgement of {topic.Name}", failed);

        // An idle deadline is not written: it is the reconciliation's every few seconds. One that acted is, since a source missed it.
        if (waking.Why == ChangeWhy.Deadline && acted == 0 && failed is null) return;
        Log?.Info("change.judged",
            ("topic", topic.Name),
            ("subject", subject),
            ("why", waking.Why),
            ("ms", ms),
            ("acted", acted),
            ("waitedMs", waking.HintedAt is { } at ? (long)(taken - at).TotalMilliseconds : null),
            ("failed", failed is not null));
    }

    private void Heard(string topic, string subject, DateTimeOffset now)
    {
        if (_hinted.TryGetValue((topic, subject), out var hinted)) hinted.Last = now;
        else _hinted[(topic, subject)] = new HintState(now);
    }

    private void Due(string topic, string subject, string why, DateTimeOffset? hintedAt)
    {
        if (_topics.TryGetValue(topic, out var known)) known.Due.TryAdd(subject, new Waking(why, hintedAt));
    }

    /// <summary>The root opened under a new generation: <see cref="ChangeWhy.Opened"/> the first time, <see cref="ChangeWhy.Reopened"/> after; null where it is absent.</summary>
    private string? Open(Root root)
    {
        var generation = ++root.Generation;
        IChangeHandle? handle;
        try
        {
            handle = _source.Open(root.Folder, root.Subfolders, new Sink(this, root, generation));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException)
        {
            handle = null;
        }

        root.Handle = handle;
        if (handle is null) return null;
        var why = root.Ever ? ChangeWhy.Reopened : ChangeWhy.Opened;
        root.Ever = true;
        root.Said = false;
        return why;
    }

    /// <summary>The root's watcher let go, under a new generation so nothing it still says is heard; disposed once the gate is left.</summary>
    private void Close(Root root)
    {
        root.Generation++;
        if (root.Handle is { } handle) _closing.Add(handle);
        root.Handle = null;
    }

    /// <summary>
    /// At a deadline: an absent root tried again, unless a back-off or its losses leave it to the deadline; an open one checked
    /// for its folder replaced at its path, which no watcher reports. What opened it, or null.
    /// </summary>
    private string? Reconcile(Root root, DateTimeOffset now)
    {
        if (root.Capped || root.ReopenAt is not null) return null;
        if (root.Handle is null)
        {
            if (root.Tries > Tries && root.LastLoss is { } last && now - last < Calm) return null;
            return Open(root);
        }

        bool same;
        try
        {
            same = root.Handle.Same;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            same = false;
        }

        if (same) return null;
        Close(root);
        return Open(root) is null ? null : ChangeWhy.Reopened;
    }

    private void Lose(Root root, DateTimeOffset now)
    {
        var why = root.LostWhy;
        root.LostWhy = null;
        Close(root);
        root.Tries = root.LastLoss is { } last && now - last < Calm ? root.Tries + 1 : 1;
        root.LastLoss = now;
        Due(root.Topic, root.Subject, ChangeWhy.Lost, null);
        Log?.Warn("change.lost", ("topic", root.Topic), ("subject", root.Subject), ("why", why), ("tries", root.Tries));
        if (root.Tries <= Tries)
        {
            root.ReopenAt = now + Backoff[root.Tries - 1];
            return;
        }

        root.ReopenAt = null;
        if (root.Said) return;
        root.Said = true;
        Log?.Warn("change.unwatched", ("topic", root.Topic), ("subject", root.Subject), ("tries", root.Tries), ("calmSeconds", (int)Calm.TotalSeconds));
    }

    private void SayCapped(Root root)
    {
        if (_cappedSaid) return;
        _cappedSaid = true;
        Log?.Warn("change.capped", ("topic", root.Topic), ("subject", root.Subject), ("most", MostRoots));
    }

    private void Poke()
    {
        try
        {
            if (_poke.CurrentCount == 0) _poke.Release();
        }
        catch (Exception error) when (error is SemaphoreFullException or ObjectDisposedException)
        {
            // Already poked, or the watch is gone.
        }
    }

    /// <summary>The handles closed under the gate, disposed outside it, so a watcher's own thread waiting on the gate never waits on this.</summary>
    private void Release()
    {
        List<IChangeHandle> closing;
        lock (_gate)
        {
            if (_closing.Count == 0) return;
            closing = [.. _closing];
            _closing.Clear();
        }

        foreach (var handle in closing)
        {
            try
            {
                handle.Dispose();
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException)
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var root in _roots) Close(root);
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        await _pumping.ConfigureAwait(false);
        await Settled().ConfigureAwait(false);
        Release();
        _stopping.Dispose();
        _poke.Dispose();
    }

    private void Moved(Root root, int generation, string name, ChangeKind kind)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (root.Generation != generation)
            {
                _dropped++;
                return;
            }

            bool heard;
            try
            {
                heard = root.Hears(name, kind);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A filter that failed hears it: an extra judgement costs one look, a missed one a wait.
                heard = true;
            }

            if (!heard) return;
            Heard(root.Topic, root.Subject, _clock());
        }

        Poke();
    }

    private void Lost(Root root, int generation, string why)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (root.Generation != generation)
            {
                _dropped++;
                return;
            }

            root.LostWhy ??= why;
        }

        Poke();
    }

    /// <summary>One generation's sink: what its watcher says is heard only while it is still the root's.</summary>
    private sealed class Sink(ChangeWatch watch, Root root, int generation) : IChangeSink
    {
        public void Moved(string name, ChangeKind kind) => watch.Moved(root, generation, name, kind);

        public void Lost(string why) => watch.Lost(root, generation, why);
    }

    private sealed class TopicState(string name, TimeSpan deadline, Func<ChangeWake, CancellationToken, Task<int>> judge, int atOnce)
    {
        public string Name { get; } = name;

        public TimeSpan Deadline { get; } = deadline;

        public Func<ChangeWake, CancellationToken, Task<int>> Judge { get; } = judge;

        public int AtOnce { get; } = atOnce;

        public Dictionary<string, Waking> Due { get; set; } = new(StringComparer.Ordinal);

        public Task? Draining { get; set; }
    }

    private sealed class Root(string topic, string subject, string folder, bool subfolders, Func<string, ChangeKind, bool> hears)
    {
        public string Topic { get; } = topic;

        public string Subject { get; } = subject;

        public string Folder { get; } = folder;

        public bool Subfolders { get; } = subfolders;

        public Func<string, ChangeKind, bool> Hears { get; } = hears;

        public bool Capped { get; set; }

        public IChangeHandle? Handle { get; set; }

        public int Generation { get; set; }

        /// <summary>Whether it was ever open, so its next open is a reopen.</summary>
        public bool Ever { get; set; }

        public string? LostWhy { get; set; }

        public int Tries { get; set; }

        public DateTimeOffset? LastLoss { get; set; }

        public DateTimeOffset? ReopenAt { get; set; }

        /// <summary>Whether its being left to its deadline was said, so it is said once.</summary>
        public bool Said { get; set; }
    }

    private sealed class SubjectState
    {
        public DateTimeOffset Last { get; set; }

        public DateTimeOffset? RetryAt { get; set; }

        public int Fails { get; set; }
    }

    private sealed class HintState(DateTimeOffset first)
    {
        public DateTimeOffset First { get; } = first;

        public DateTimeOffset Last { get; set; } = first;

        public DateTimeOffset FireAt => Last + Quiet < First + Cap ? Last + Quiet : First + Cap;
    }

    private sealed record Waking(string Why, DateTimeOffset? HintedAt);
}
