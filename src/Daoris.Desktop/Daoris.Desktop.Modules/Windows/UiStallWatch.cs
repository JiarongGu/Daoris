using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// How late the window's own thread answers, and what held it, into the machine log as <c>ui.stalled</c> (FREEZE1, D56's
/// note): the frameless window's caption is the page's strip, and a press there is handed to this thread to move, maximise
/// or close the window. A thread that answers late is a frozen bar while the page, on its own thread, goes on working.
/// </summary>
/// <remarks>
/// <para><b>A look a second, answered on the thread.</b> <see cref="Look"/> posts a probe to the window's thread and notes
/// when; the probe notes when it ran. A probe that waited <see cref="Late"/> or more opens a stall, and the stall is written
/// whole once a probe answers in time again: the longest wait, how long it lasted, and what ran meanwhile. A stall that does
/// not end is written once every <see cref="Longest"/>, so a thread that never recovers still says so.</para>
///
/// <para><b>What held it.</b> The bridge dispatches each page request on this thread, and a route runs there until its
/// first wait (<see cref="Middleware"/>). The route on the thread whenever the timer found a probe waiting is named
/// (<c>during</c>), and so is the slowest route's synchronous part (<c>slowest</c>, <c>slowestMs</c>), with how many routes
/// ran (<c>requests</c>). Beside them, what else the thread does that no route shows: the bus events the kit's flush
/// serializes on it (<c>events</c>) and the full collections that stop every thread (<c>gc2</c>), each since the thread last
/// answered in time. A stall with no route is then the flush, a collection or the engine, and the counts say which.</para>
///
/// <para><b>Names and counts only</b> (D94): a route is <c>MODULE.TYPE</c> as the page sent it, written only where it is
/// an identifier, so a page that put a path or a sentence there writes nothing. Never a payload.</para>
///
/// <para>Every effect is handed in, the clock, the post and the collections, so the tests drive a minute's stall with no
/// thread and no time.</para>
/// </remarks>
public sealed partial class UiStallWatch : IDisposable
{
    /// <summary>The machine log's event (the machine log design §4).</summary>
    public const string Event = "ui.stalled";

    private readonly MachineLog _log;
    private volatile Func<Action, bool> _post;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<int> _collections;
    private readonly IDisposable? _subscription;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _disposed;

    // The probe on its way, by when it was posted; null while none waits.
    private DateTimeOffset? _sent;

    // The thread the probes run on, once one has (0 before): a route on another window's thread holds this one up not at all.
    private int _thread;

    // The route whose synchronous part runs on the thread now, or null.
    private string? _running;

    // Since the thread last answered in time: what ran on it and what else happened.
    private int _requests;
    private string? _slowest;
    private TimeSpan _slowestFor;
    private long _events;
    private long _eventsSince;
    private int _collectionsSince;

    // The stall open now, or null.
    private Stall? _stall;

    private sealed class Stall(DateTimeOffset from)
    {
        public DateTimeOffset From { get; set; } = from;

        public DateTimeOffset Until { get; set; } = from;

        public TimeSpan Longest { get; set; }

        public List<string> During { get; } = [];
    }

    /// <param name="log">Where a stall is written.</param>
    /// <param name="post">
    /// Run an action on the window's thread later: false where there is no window to run it. Null until <see cref="Start"/>
    /// hands the window's own, since the watch is composed with the dispatcher, before any window exists.
    /// </param>
    /// <param name="bus">The bus whose events the kit's flush serializes on the window's thread; null counts none.</param>
    /// <param name="clock">What time it is; the system's by default.</param>
    /// <param name="collections">How many full (generation 2) collections there have been; the runtime's by default.</param>
    public UiStallWatch(
        MachineLog log, Func<Action, bool>? post = null, IEventBus? bus = null, Func<DateTimeOffset>? clock = null,
        Func<int>? collections = null)
    {
        _log = log;
        _post = post ?? (_ => false);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _collections = collections ?? (() => GC.CollectionCount(2));
        _subscription = bus?.SubscribeToAll(_ =>
        {
            Interlocked.Increment(ref _events);
            return Task.CompletedTask;
        });
        _collectionsSince = _collections();
    }

    /// <summary>How often the thread is looked at.</summary>
    public TimeSpan Every { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How long a probe may wait before the thread counts as late: a press on the bar waits as long.</summary>
    public TimeSpan Late { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>The longest a stall goes unwritten while it lasts.</summary>
    public TimeSpan Longest { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Look every <see cref="Every"/> from now on, on a timer of its own.</summary>
    /// <param name="post">The window's thread, where it was not handed in at construction.</param>
    public void Start(Func<Action, bool>? post = null)
    {
        lock (_gate)
        {
            if (_disposed || _timer is not null) return;
            if (post is not null) _post = post;
            _timer = new Timer(_ => Tick(), null, Every, Every);
        }
    }

    // The timer's call: a watch never takes the application down, so what it could not do is written and it looks again.
    private void Tick()
    {
        try
        {
            Look();
        }
        catch (Exception error)
        {
            _log.Failed("the window thread's watch", error);
        }
    }

    /// <summary>
    /// One look: a probe posted where none waits, or, where one does, note what holds the thread. Called by the timer, and
    /// by a test as the timer.
    /// </summary>
    public void Look()
    {
        try
        {
            if (!Waiting()) return;
            if (!_post(Answered))
            {
                lock (_gate) _sent = null; // no window to look at: nothing to measure
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException)
        {
            // The window went between the look and the post.
            lock (_gate) _sent = null;
        }
    }

    // True where a probe is to be posted now. Where one waits already, what holds the thread is noted instead.
    private bool Waiting()
    {
        lock (_gate)
        {
            if (_disposed) return false;
            var now = _clock();
            if (_sent is not { } sent)
            {
                _sent = now;
                return true;
            }

            var waited = now - sent;
            if (waited < Late) return false;

            var stall = Open(sent);
            if (waited > stall.Longest) stall.Longest = waited;
            stall.Until = now;
            if (Volatile.Read(ref _running) is { } running && !stall.During.Contains(running) && stall.During.Count < 4)
            {
                stall.During.Add(running);
            }

            // A stall that lasts is written as it stands, and goes on as a new one from here.
            if (now - stall.From >= Longest)
            {
                Write(stall, ongoing: true);
                _stall = new Stall(now) { Longest = TimeSpan.Zero };
            }

            return false;
        }
    }

    // The probe, on the window's thread.
    private void Answered()
    {
        lock (_gate)
        {
            if (_sent is not { } sent) return;
            _sent = null;
            if (_thread == 0) Volatile.Write(ref _thread, Environment.CurrentManagedThreadId);
            var now = _clock();
            var waited = now - sent;
            if (waited >= Late)
            {
                var stall = Open(sent);
                if (waited > stall.Longest) stall.Longest = waited;
                stall.Until = now;
                return;
            }

            // In time: a stall that was open ends here, and what is counted starts again from now.
            if (_stall is { } ended) Write(ended, ongoing: false);
            Since();
        }
    }

    private Stall Open(DateTimeOffset from) => _stall ??= new Stall(from);

    // Under the gate.
    private void Since()
    {
        _stall = null;
        _requests = 0;
        _slowest = null;
        _slowestFor = TimeSpan.Zero;
        _eventsSince = Interlocked.Read(ref _events);
        _collectionsSince = _collections();
    }

    // Under the gate.
    private void Write(Stall stall, bool ongoing)
    {
        _log.Warn(Event,
            ("ms", (long)stall.Longest.TotalMilliseconds),
            ("seconds", (long)Math.Ceiling((stall.Until - stall.From).TotalSeconds)),
            ("ongoing", ongoing),
            ("during", stall.During.Count == 0 ? null : string.Join(",", stall.During)),
            ("slowest", _slowest),
            ("slowestMs", _slowest is null ? null : (long)_slowestFor.TotalMilliseconds),
            ("requests", _requests),
            ("events", (int)(Interlocked.Read(ref _events) - _eventsSince)),
            ("gc2", _collections() - _collectionsSince));
        Since();
    }

    /// <summary>
    /// The dispatcher's middleware (the kit's <c>UseMessageDispatcher</c>, beside <see cref="RefusalLog"/>): marks the route
    /// whose synchronous part runs on the window's thread, and times that part, which is what holds the thread. Its waits
    /// are not timed: the thread is free while it waits.
    /// </summary>
    public MessageMiddleware Middleware => (request, next, _) =>
    {
        var window = Volatile.Read(ref _thread);
        if (window != 0 && Environment.CurrentManagedThreadId != window) return next();

        var name = Named(request);
        var previous = Interlocked.Exchange(ref _running, name);
        var started = _clock();
        try
        {
            // Not awaited here: the part of the route that runs before its first wait runs inside this call.
            return next();
        }
        finally
        {
            Volatile.Write(ref _running, previous);
            Ran(name, _clock() - started);
        }
    };

    private void Ran(string? name, TimeSpan held)
    {
        lock (_gate)
        {
            _requests++;
            if (name is not null && held > _slowestFor)
            {
                _slowest = name;
                _slowestFor = held;
            }
        }
    }

    /// <summary>A route as <c>MODULE.TYPE</c>, or null where either is no identifier.</summary>
    private static string? Named(IpcRequest request)
    {
        var name = $"{request.Module}.{request.Type}";
        return Identifier().IsMatch(name) ? name : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_]+(?:[.-][A-Za-z0-9_]+)*$")]
    private static partial Regex Identifier();

    /// <summary>Stop looking; a stall still open is written as it stands.</summary>
    public void Dispose()
    {
        Timer? timer;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            timer = _timer;
            _timer = null;
            if (_stall is { } open) Write(open, ongoing: true);
        }

        timer?.Dispose();
        _subscription?.Dispose();
    }
}
