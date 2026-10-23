using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The desktop's start, timed stage by stage (STARTFAST1): one <c>start.stages</c> line in the machine log per start, each
/// stage in ms from the process's start, so a slow start says where its seconds went rather than an <c>app.started</c> eleven
/// seconds in with nothing between. A <see cref="Stopwatch"/> and the process's start time, read once: no extra process.
/// </summary>
/// <remarks>
/// <para>Each stage counts once, its first mark; a stage marked again keeps its first time. The line is written once every
/// stage is marked, or at <see cref="Bound"/> after the splash lifted, or at the stop, whichever is first, with the stages
/// never reached named in <c>missing</c> (a pipe another loop's lease kept unserved, a host that never came up).</para>
///
/// <para>The line's stages are in <see cref="Order"/>, whatever order they were marked in: the values say when each came.</para>
/// </remarks>
public sealed class StartStages
{
    /// <summary>The process up to <c>Main</c>: the runtime's own start, read from the process's start time.</summary>
    public const string Main = "main";

    /// <summary>The home established, an install's older layout moved into D172's places (HOMELAYOUT3).</summary>
    public const string Home = "home";

    /// <summary>The driver loop started, as the application starts (<c>app.started</c>).</summary>
    public const string Driver = "driver";

    /// <summary>The terminal's pipe served (RPC1a), once the loop holds the home.</summary>
    public const string Pipe = "pipe";

    /// <summary>The local service host answering, started or adopted.</summary>
    public const string Host = "host";

    /// <summary>The main window shown.</summary>
    public const string Shown = "shown";

    /// <summary>The page's browser made: the engine ready to load the page (Chromium since D92).</summary>
    public const string Browser = "browser";

    /// <summary>The page's word that its first screen has its data (the kit's <c>CLOSE_SPLASH</c>, SPLASHDATA1).</summary>
    public const string Page = "page";

    /// <summary>The splash lifted, by the page or by its bound (<see cref="SplashLift"/>).</summary>
    public const string Splash = "splash";

    /// <summary>Every stage, in the order the line gives them.</summary>
    public static IReadOnlyList<string> Order { get; } = [Main, Home, Driver, Pipe, Host, Shown, Browser, Page, Splash];

    /// <summary>How long after the splash lifted the line waits for the stages still to come before it is written without them.</summary>
    public static TimeSpan Bound { get; } = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private readonly Dictionary<string, long> _marks = new(StringComparer.Ordinal);
    private readonly Stopwatch _since;
    private readonly long _before;
    private readonly TimeSpan _bound;
    private string? _by;
    private bool _written;

    /// <param name="before">The ms from the process's start to this timer's: the <see cref="Main"/> stage.</param>
    /// <param name="bound">How long after the splash the line waits for the rest; <see cref="Bound"/> by default.</param>
    public StartStages(long before, TimeSpan? bound = null)
    {
        _since = Stopwatch.StartNew();
        _before = Math.Max(0, before);
        _bound = bound ?? Bound;
        _marks[Main] = _before;
    }

    /// <summary>A timer begun now, at the top of <c>Main</c>: the process's start read once, and nothing where it cannot be.</summary>
    public static StartStages Begin()
    {
        long before = 0;
        try
        {
            using var self = Process.GetCurrentProcess();
            before = (long)(DateTime.Now - self.StartTime).TotalMilliseconds;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // A start time the system will not say leaves the first stage at nought, and the rest still timed from Main.
        }

        return new StartStages(before);
    }

    /// <summary>Where the line goes, set once the log is open (it opens after the home, which is a stage).</summary>
    public MachineLog? Log { get; set; }

    /// <summary>The ms each stage came at, from the process's start, of those marked so far.</summary>
    public IReadOnlyDictionary<string, long> Marks
    {
        get
        {
            lock (_gate) return new Dictionary<string, long>(_marks, StringComparer.Ordinal);
        }
    }

    private readonly TaskCompletionSource _paged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The page's word that its first screen has its data, or <paramref name="bound"/> from this call, whichever is first (STARTFAST1b,
    /// the start-path design §5c): what the work the first screen does not need waits for, so the page's own pulls are not queued
    /// behind it. Never faults; its bound is the window's own, since a page that never says holds nothing past it.
    /// </summary>
    public Task FirstScreenAsync(TimeSpan bound) =>
        _paged.Task.IsCompleted ? Task.CompletedTask : Task.WhenAny(_paged.Task, Task.Delay(bound));

    /// <summary>Mark <paramref name="stage"/> now, from any thread; its first mark counts.</summary>
    public void Mark(string stage)
    {
        if (stage == Page) _paged.TrySetResult();
        bool all;
        lock (_gate)
        {
            if (_written || _marks.ContainsKey(stage)) return;
            _marks[stage] = _before + _since.ElapsedMilliseconds;
            all = Order.All(_marks.ContainsKey);
        }

        if (all) Write();
    }

    /// <summary>
    /// The splash lifted, by <paramref name="by"/> (<c>page</c> or <c>bound</c>): the last stage a person waits through. The line
    /// waits <see cref="Bound"/> more for the stages still to come.
    /// </summary>
    public void Lifted(string by)
    {
        lock (_gate) _by ??= by;
        Mark(Splash);
        _ = Task.Delay(_bound).ContinueWith(_ => Write(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>Write the line now with what was marked, once: at the stop, or the bound after the splash.</summary>
    public void Write()
    {
        List<(string Key, object? Value)> data;
        lock (_gate)
        {
            if (_written) return;
            _written = true;
            data = [.. Order.Select(stage => (stage, _marks.TryGetValue(stage, out var ms) ? (object?)ms : null))];
            data.Add(("by", _by));
            var missing = Order.Where(stage => !_marks.ContainsKey(stage)).ToArray();
            data.Add(("missing", missing.Length == 0 ? null : string.Join(",", missing)));
        }

        Log?.Info("start.stages", [.. data]);
    }
}
