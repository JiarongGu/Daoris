using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The one door every git of Daoris's passes: a bound on how many run at once (not only start), a queue per repository, an
/// identical queued start shared, and a read nobody waits on ended. The start gate and <see cref="HeldChild"/> refuse a git's image
/// started in no place of its (<see cref="GitPlace.Admit"/>).
/// </summary>
/// <remarks>
/// <para><b>Places</b> are <see cref="GitPlaces"/>' rules. A git holds its place from before its creation until every process started
/// in it exits; its creation inside the place still passes <see cref="ProcessStarts"/>.</para>
/// <para><b>Whose</b> is the flow's (<see cref="AsPerson"/>); a shared ask takes the highest class among its askers.</para>
/// <para><b>A queue per repository</b>, keyed by its common directory. A place that frees goes to a person's oldest ask that may go, else to
/// the oldest that may go, so an ask held back by its class's cap never holds up a later local read. Within a repository a write passes nothing queued ahead of it, and nothing
/// queued behind it passes it.</para>
/// <para><b>A shared answer</b>: an ask takes another's only where its own start would have been that start (<see cref="GitRunAsk.Start"/>)
/// and that start is still queued with no write of its repository queued after it, so its answer is as new as the ask's own would be.</para>
/// <para><b>Ending</b>: a read that takes no lock (<see cref="GitRunAsk.Endable"/>) is dropped while queued and ended while running once
/// every asker has left. Every other git is dropped only while queued; once placed it runs untokened to its end or its bound.</para>
/// <para><b>Re-entry</b>: an ask made beneath a placed git (<see cref="Beneath"/>, told from the caller's process ancestry by
/// <see cref="GitCallers"/>: a hook of a push asking a read, a diff's textconv helper asking the window) is placed at once in a place
/// that git lends, above the bound and counted toward none of its caps, one at a time for each parent, to a depth of two; deeper is
/// refused as <c>git.start.depth</c>. So no git waits on a place its own descendant needs.</para>
/// <para><b>A git that outlived its kill</b> leaves the bound and every class's count for a count apart, said as <c>git.place.apart</c>
/// and again at its exit; while its repository holds one, the background's asks of it are answered at once as git's failure
/// (<c>stalled</c>), and a person's are placed as asked. So hung gits stall their own repositories alone.</para>
/// </remarks>
public sealed class GitScheduler
{
    /// <summary>How many gits run at once in run places: the start gate's figure.</summary>
    public const int Bound = ProcessStarts.Bound;

    /// <summary>How many of them may wait outside Daoris.</summary>
    public const int OutsideBound = 2;

    /// <summary>How many of the background's reads one repository runs at once.</summary>
    public const int RepositoryBound = 2;

    /// <summary>How deep places are lent: a hook's git, and that git's hook's.</summary>
    public const int LentDepth = 2;

    /// <summary>The one scheduler of the process, as the start lock is one per process.</summary>
    public static GitScheduler Shared { get; } = new(Bound, OutsideBound, RepositoryBound);

    private static readonly AsyncLocal<bool> s_person = new();
    private static readonly AsyncLocal<GitPlace?> s_beneath = new();
    private static readonly AsyncLocal<Action<long>?> s_timed = new();

    private readonly object _gate = new();
    private readonly GitPlaces _places;
    private readonly Dictionary<string, LinkedList<Entry>> _queues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Entry> _queuedByStart = new(StringComparer.Ordinal);
    private readonly List<Entry> _lending = [];
    private readonly HashSet<Entry> _running = [];
    private readonly List<Removal> _removing = [];
    private readonly Dictionary<string, int> _stalled = new(StringComparer.OrdinalIgnoreCase);
    private long _order;
    private int _apart;
    private int _lent;
    private int _started;

    internal GitScheduler(int bound, int outside, int repository) => _places = new GitPlaces(bound, outside, repository);

    /// <summary>Whom a place apart, a stalled repository's refusal and a re-entry too deep are said to; the program's machine log by default.</summary>
    internal Action<string, (string Key, object? Value)[]> Said { get; init; } = SayToHome;

    /// <summary>Whose this flow's gits are: a person's within <see cref="AsPerson"/>, else the background's.</summary>
    internal static StartClass Whose => s_person.Value ? StartClass.Person : StartClass.Background;

    /// <summary>
    /// Until disposed, the gits this flow asks are a person's: a bridge route's work and a terminal verb's. Set inside the caller's
    /// own flow, so it reaches what the caller awaits and starts.
    /// </summary>
    public static IDisposable AsPerson()
    {
        var before = s_person.Value;
        s_person.Value = true;
        return new Restore(() => s_person.Value = before);
    }

    /// <summary>
    /// Until disposed, the gits this flow asks descend from the git in <paramref name="place"/>: a hook's ask, or a textconv or filter
    /// helper's, placed in a place that git lends, since that git cannot end before its descendant is answered. Null lends none.
    /// </summary>
    internal static IDisposable Beneath(GitPlace? place)
    {
        var before = s_beneath.Value;
        s_beneath.Value = place;
        return new Restore(() => s_beneath.Value = before);
    }

    /// <summary>The place of a running git among <paramref name="ancestors"/> (a caller's parents, nearest first); null where none is one's.</summary>
    internal GitPlace? PlaceOf(IReadOnlyList<int> ancestors)
    {
        lock (_gate)
        {
            foreach (var id in ancestors)
            {
                if (_running.FirstOrDefault(entry => entry.Place?.Runs(id) == true) is { } held) return held.Place;
            }
        }

        return null;
    }

    /// <summary>Whether any git holds a place now: no caller is beneath one otherwise, so its ancestry is not read.</summary>
    internal bool Any
    {
        get
        {
            lock (_gate) return _running.Count > 0;
        }
    }

    /// <summary>Tell <paramref name="timed"/> how long each git this flow asks waited in its queue, in <see cref="Stopwatch"/> ticks, until disposed.</summary>
    internal static IDisposable Timing(Action<long> timed)
    {
        var before = s_timed.Value;
        s_timed.Value = timed;
        return new Restore(() => s_timed.Value = before);
    }

    /// <summary>The run places held: the tests' window.</summary>
    internal int Running
    {
        get
        {
            lock (_gate) return _places.Running;
        }
    }

    /// <summary>The places lent to re-entries that are held now.</summary>
    internal int Lent
    {
        get
        {
            lock (_gate) return _lent;
        }
    }

    /// <summary>The gits counted apart: their kill outlived, their places left the bound.</summary>
    internal int Apart
    {
        get
        {
            lock (_gate) return _apart;
        }
    }

    /// <summary>How many runs it has begun: an ask that shared another's begins none.</summary>
    internal int Started => Volatile.Read(ref _started);

    /// <summary>
    /// Run <paramref name="run"/> for <paramref name="ask"/> once it is placed, or take the answer of an identical start still queued.
    /// <paramref name="run"/> is handed the token it runs under: its askers' for an endable read, none for every other git. Cancelling
    /// <paramref name="ct"/> leaves the ask: this caller is answered with its cancellation, and the git is dropped or ended as above.
    /// </summary>
    /// <exception cref="GitUnplacedException">Its tree is being removed, its repository is stalled, or it is a re-entry too deep.</exception>
    internal async Task<T> RunAsync<T>(GitRunAsk ask, Func<CancellationToken, Task<T>> run, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IDisposable? removal = ask.Removes is { } removes ? await RemovingAsync(removes, ct).ConfigureAwait(false) : null;
        try
        {
            return await AskAsync(ask, run, ct).ConfigureAwait(false);
        }
        finally
        {
            removal?.Dispose();
        }
    }

    private async Task<T> AskAsync<T>(GitRunAsk ask, Func<CancellationToken, Task<T>> run, CancellationToken ct)
    {
        var parent = s_beneath.Value;
        if (parent is { Depth: >= LentDepth })
        {
            Said("git.start.depth", [("depth", parent.Depth + 1)]);
            throw GitUnplacedException.Depth();
        }

        Entry entry;
        var joined = false;
        lock (_gate)
        {
            if (parent is null && Whose == StartClass.Background && _stalled.ContainsKey(ask.Repository))
            {
                entry = null!;
            }
            else if (parent is null && ask.Start is { } key && _queuedByStart.TryGetValue(key, out var queued))
            {
                entry = queued;
                joined = true;
            }
            else
            {
                entry = new Entry(ask, ++_order) { Parent = parent };
                if (parent is not null) _lending.Add(entry);
                else Enqueue(entry);
            }

            if (entry is not null)
            {
                entry.Askers++;
                if (Whose == StartClass.Person) entry.Whose = StartClass.Person;
            }
        }

        if (entry is null)
        {
            Said("git.stalled", [("repository", NameOf(ask.Repository))]);
            throw GitUnplacedException.Stalled(ask.Repository);
        }

        if (!joined) _ = DriveAsync(entry, run, s_timed.Value);
        Pump();
        try
        {
            return (T)(await entry.Result.Task.WaitAsync(ct).ConfigureAwait(false))!;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Left once, here, where this asker's own wait ended: a registration on its token would be disposed by the wait's own end first.
            Leave(entry);
            throw;
        }
    }

    private void Enqueue(Entry entry)
    {
        if (!_queues.TryGetValue(entry.Ask.Repository, out var queue))
        {
            _queues[entry.Ask.Repository] = queue = new LinkedList<Entry>();
        }

        // A start queued before a write reads the state before it, which an ask made after the write may not take as its own.
        if (entry.Ask.Writes)
        {
            foreach (var queued in queue)
            {
                if (StartOf(queued) is { } key && _queuedByStart.TryGetValue(key, out var held) && ReferenceEquals(held, queued)) _queuedByStart.Remove(key);
            }
        }

        entry.Node = queue.AddLast(entry);
        if (StartOf(entry) is { } start) _queuedByStart[start] = entry;
    }

    /// <summary>The ask's run, in its first asker's flow (its seams, counts and timing), once it is placed.</summary>
    private async Task DriveAsync<T>(Entry entry, Func<CancellationToken, Task<T>> run, Action<long>? timed)
    {
        try
        {
            await entry.Go.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        timed?.Invoke(Stopwatch.GetTimestamp() - entry.AskedAt);
        GitPlace.Enter(entry.Place!);
        Interlocked.Increment(ref _started);
        try
        {
            var answer = await run(entry.Ask.Endable ? entry.Ending.Token : CancellationToken.None).ConfigureAwait(false);
            entry.Result.TrySetResult(answer);
        }
        catch (OperationCanceledException) when (entry.Ending.IsCancellationRequested)
        {
            if (entry.Going is { } folder) entry.Result.TrySetException(GitUnplacedException.Going(folder));
            else entry.Result.TrySetCanceled();
        }
        catch (Exception error)
        {
            entry.Result.TrySetException(error);
        }
        finally
        {
            Ended(entry);
        }
    }

    /// <summary>An asker left: the last to leave drops a queued git, and ends a running endable read.</summary>
    private void Leave(Entry entry)
    {
        var dropped = false;
        var end = false;
        lock (_gate)
        {
            if (--entry.Askers > 0 || entry.Done) return;
            if (!entry.Placed) dropped = Dequeue(entry) || _lending.Remove(entry);
            else end = entry.Ask.Endable;
        }

        if (dropped)
        {
            entry.Go.TrySetCanceled();
            entry.Result.TrySetCanceled();
            Pump();
        }

        if (end) entry.Ending.Cancel();
    }

    /// <summary>A run returned: its place freed where its processes exited, else counted apart until they exit, and said.</summary>
    private void Ended(Entry entry)
    {
        var exited = entry.Place!.Exited;
        lock (_gate)
        {
            entry.Done = true;
            _running.Remove(entry);
            if (entry.Parent is { } parent)
            {
                parent.Lent--;
                _lent--;
            }
            else
            {
                _places.Free(entry.Ask, entry.Whose);
            }

            if (!exited)
            {
                _apart++;
                _stalled[entry.Ask.Repository] = _stalled.GetValueOrDefault(entry.Ask.Repository) + 1;
            }
        }

        if (!exited)
        {
            Said("git.place.apart", [("repository", NameOf(entry.Ask.Repository)), ("apart", Apart)]);
            _ = entry.Place.ExitedAsync().ContinueWith(_ =>
            {
                lock (_gate)
                {
                    _apart--;
                    var left = _stalled.GetValueOrDefault(entry.Ask.Repository) - 1;
                    if (left <= 0) _stalled.Remove(entry.Ask.Repository);
                    else _stalled[entry.Ask.Repository] = left;
                }

                Said("git.place.exited", [("repository", NameOf(entry.Ask.Repository)), ("apart", Apart)]);
                Pump();
            }, TaskScheduler.Default);
        }

        Pump();
    }

    /// <summary>Every ask that may go now placed, then let go outside the lock.</summary>
    private void Pump()
    {
        List<Entry> go = [];
        lock (_gate)
        {
            foreach (var entry in _lending.ToList())
            {
                if (entry.Parent!.Lent > 0) continue;
                entry.Parent.Lent++;
                _lent++;
                _lending.Remove(entry);
                Place(entry, entry.Parent.Depth + 1);
                go.Add(entry);
            }

            while (Next() is { } entry)
            {
                Dequeue(entry);
                _places.Take(entry.Ask, entry.Whose);
                Place(entry, 0);
                go.Add(entry);
            }
        }

        foreach (var entry in go) entry.Go.TrySetResult();
    }

    private void Place(Entry entry, int depth)
    {
        entry.Placed = true;
        entry.Place = new GitPlace(entry.Whose, entry.Ask.Wait, depth);
        _running.Add(entry);
    }

    /// <summary>
    /// Under the lock: a person's oldest ask that may go, else the oldest that may go: an ask its class's cap or its repository's
    /// order holds back never holds up a later one that may go.
    /// </summary>
    private Entry? Next()
    {
        Entry? person = null;
        Entry? oldest = null;
        foreach (var queue in _queues.Values)
        {
            foreach (var entry in Eligible(queue))
            {
                if (entry.Whose == StartClass.Person && (person is null || entry.Order < person.Order)) person = entry;
                if (oldest is null || entry.Order < oldest.Order) oldest = entry;
            }
        }

        return person ?? oldest;
    }

    /// <summary>A repository's asks that may go now, in order: none past a write still queued, a write only with nothing queued ahead.</summary>
    private IEnumerable<Entry> Eligible(LinkedList<Entry> queue)
    {
        var first = true;
        foreach (var entry in queue)
        {
            if (Held(entry)) continue;
            if (entry.Ask.Writes && !first) yield break;
            first = false;
            if (_places.Opens(entry.Ask, entry.Whose)) yield return entry;
            if (entry.Ask.Writes) yield break;
        }
    }

    // An ask in a folder whose removal began before the ask waits until the removal has ended.
    private bool Held(Entry entry) => _removing.Any(removal => removal.Order < entry.Order && Inside(entry.Ask.Folder, removal.Folder));

    private bool Dequeue(Entry entry)
    {
        if (entry.Node is not { List: { } queue } node) return false;
        queue.Remove(node);
        entry.Node = null;
        if (StartOf(entry) is { } start && _queuedByStart.TryGetValue(start, out var held) && ReferenceEquals(held, entry)) _queuedByStart.Remove(start);
        if (queue.Count == 0) _queues.Remove(entry.Ask.Repository);
        return true;
    }

    /// <summary>
    /// Admit the removal of <paramref name="folder"/>: the endable reads inside it dropped while queued and ended while running, their
    /// askers told the tree is going; every other git inside it, queued or running, waited for; and each ask inside it made after this
    /// held until the returned hold is disposed.
    /// </summary>
    internal async Task<IDisposable> RemovingAsync(string folder, CancellationToken ct)
    {
        List<Entry> dropped = [];
        List<Entry> ended = [];
        List<Task> waits = [];
        Removal removal;
        lock (_gate)
        {
            removal = new Removal(folder, ++_order);
            _removing.Add(removal);
            foreach (var entry in _queues.Values.SelectMany(queue => queue).Where(each => Inside(each.Ask.Folder, folder)).ToList())
            {
                if (entry.Ask.Endable && Dequeue(entry)) dropped.Add(entry);
                else waits.Add(entry.Result.Task);
            }

            foreach (var entry in _running.Where(each => Inside(each.Ask.Folder, folder)))
            {
                if (entry.Ask.Endable) ended.Add(entry);
                waits.Add(entry.Result.Task);
            }
        }

        foreach (var entry in dropped)
        {
            entry.Go.TrySetCanceled();
            entry.Result.TrySetException(GitUnplacedException.Going(folder));
        }

        foreach (var entry in ended)
        {
            entry.Going = folder;
            entry.Ending.Cancel();
        }

        Pump();
        var hold = new Restore(() =>
        {
            lock (_gate) _removing.Remove(removal);
            Pump();
        });
        try
        {
            await Task.WhenAll(waits.Select(each => each.ContinueWith(_ => { }, TaskScheduler.Default))).WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            hold.Dispose();
            throw;
        }

        return hold;
    }

    private static bool Inside(string folder, string removed)
    {
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(removed));
        return a.Equals(b, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string? StartOf(Entry entry) => entry.Ask.Start;

    private static string NameOf(string repository) => Path.GetFileName(Path.TrimEndingDirectorySeparator(repository));

    private static void SayToHome(string @event, (string Key, object? Value)[] data)
    {
        if (ProgramHome.Named is not { } home) return;
        using var log = new MachineLog(home, "driver");
        log.Warn(@event, data);
    }

    private sealed class Entry(GitRunAsk ask, long order)
    {
        public GitRunAsk Ask { get; } = ask;

        public long Order { get; } = order;

        public long AskedAt { get; } = Stopwatch.GetTimestamp();

        public TaskCompletionSource<object?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Go { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenSource Ending { get; } = new();

        public StartClass Whose { get; set; } = StartClass.Background;

        public GitPlace? Parent { get; init; }

        public int Askers { get; set; }

        public LinkedListNode<Entry>? Node { get; set; }

        public bool Placed { get; set; }

        public bool Done { get; set; }

        public GitPlace? Place { get; set; }

        public string? Going { get; set; }
    }

    private sealed record Removal(string Folder, long Order);

    private sealed class Restore(Action restore) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) restore();
        }
    }
}
