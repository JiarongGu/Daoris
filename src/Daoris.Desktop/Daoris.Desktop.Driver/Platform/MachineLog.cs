using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The machine log's desktop writer (LOG1, D94): what happens on this machine, one JSON line per event,
/// in the home's <c>logs/</c>, so Daoris can be improved from the owner's real use.
/// </summary>
/// <remarks>
/// <para><b>One file per source per day</b> (<c>&lt;date&gt;.&lt;source&gt;.jsonl</c>, the date in UTC):
/// the shell, the headless driver and the browser each write their own, and the HTTP host writes its own
/// with its own code. Two processes appending to one file can overwrite each other's lines, and the
/// artefacts share no code to hold a lock between them, so the format is the contract
/// (<c>docs/2026-09-30-machine-log-design.md</c> §3, the twins rule).</para>
///
/// <para><b>Appended, not written whole.</b> A log grows a line at a time, as a session transcript does;
/// the atomic write-beside-and-rename is for files that are replaced.</para>
///
/// <para><b>Each line at the file's end</b> (MACHINELOGAPPEND1): a handle opened to append on Windows writes at
/// its own position, so two processes of one source on one day's file (a later launch beside the running
/// desktop) overwrote each other's lines. Each line is written at the end the file has now, under a mutex
/// named for the file that every writer of it takes; a writer that cannot open it still writes at the end.</para>
///
/// <para><b>Never a reason to fail.</b> No home is no log; a write that fails is dropped. The log is
/// evidence of what happened, and a process that stopped because its evidence could not be written
/// would be the worse outcome.</para>
///
/// <para><b>What is never written here</b> is the caller's to keep out, and D94 lists it: anyone's words,
/// a file's contents, a secret, a URL's query. The one caller that passes someone else's fields — the
/// page, over the bridge — is filtered by the module that takes them.</para>
/// </remarks>
public sealed class MachineLog : IDisposable
{
    /// <summary>How many days of files are kept, today included.</summary>
    public const int KeptDays = 30;

    /// <summary>Where a file stops growing: a loop that logs without end cannot fill a disk.</summary>
    public const long CapBytes = 20L * 1024 * 1024;

    /// <summary>How long a line waits for another writer of its file; past it, it is written at the end regardless.</summary>
    private static readonly TimeSpan TurnWait = TimeSpan.FromSeconds(1);

    private static readonly JsonWriterOptions Options = new()
    {
        // Readable in a text editor, 中文 included: the file is read by people as well as by the report.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string? _home;
    private readonly string? _folder;
    private readonly string _source;
    private readonly Func<DateTimeOffset> _clock;
    private readonly long _cap;
    private readonly object _gate = new();
    private FileStream? _file;
    private Mutex? _turn;
    private Exception? _ended;
    private string? _day;
    private bool _full;
    private bool _disposed;

    /// <param name="home">The Daoris home, or null for none: nothing is written.</param>
    /// <param name="source">Which process kind this is: <c>desktop</c>, <c>driver</c>, <c>browser</c>.</param>
    /// <param name="clock">What time it is; the system's by default.</param>
    /// <param name="cap">Where a file stops growing.</param>
    public MachineLog(string? home, string source, Func<DateTimeOffset>? clock = null, long cap = CapBytes)
    {
        _home = string.IsNullOrEmpty(home) ? null : home;
        _folder = _home is null ? null : DaorisHome.Entry(_home, HomeEntry.Logs);
        _source = source;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _cap = cap;
    }

    /// <summary>A log that writes nothing, for code that runs where no log was handed in.</summary>
    public static MachineLog None { get; } = new(null, "none");

    /// <summary>This process's log under the home the environment names, with its old files pruned.</summary>
    public static MachineLog Open(string source)
    {
        var log = new MachineLog(DaorisHome.Resolve(), source);
        log.Prune();
        return log;
    }

    /// <summary>Whether there is anywhere to write: false with no home.</summary>
    public bool Writing => _folder is not null;

    /// <summary>
    /// The folder this log writes into, and every other process's beside it — the home's <c>logs/</c> —
    /// or null with no home. What the Settings domain reads and opens (LOG1c).
    /// </summary>
    public string? Location => _folder;

    public void Info(string @event, params (string Key, object? Value)[] data) => Write("info", @event, data);

    public void Warn(string @event, params (string Key, object? Value)[] data) => Write("warn", @event, data);

    public void Error(string @event, params (string Key, object? Value)[] data) => Write("error", @event, data);

    /// <summary>
    /// One line. <paramref name="data"/>'s values are strings, numbers, booleans or null; anything else is
    /// written as its text, so a line is never nested.
    /// </summary>
    public void Write(string level, string @event, IReadOnlyList<(string Key, object? Value)> data)
    {
        if (_folder is null) return;
        try
        {
            var now = _clock().ToUniversalTime();
            var line = Line(now, level, @event, data);
            lock (_gate)
            {
                if (_disposed) return;
                var file = FileFor(now);
                if (file is null || _full) return;
                var held = TakeTurn();
                try
                {
                    // The end the file has now, which another process's line may have moved since this handle's last.
                    file.Seek(0, SeekOrigin.End);
                    if (file.Length + line.Length > _cap)
                    {
                        _full = true;
                        line = Line(now, "warn", "log.full", [("capBytes", _cap)]);
                    }

                    file.Write(line);
                    file.Flush();
                }
                finally
                {
                    if (held) _turn!.ReleaseMutex();
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Dropped: the log is evidence, never a reason to fail.
        }
    }

    /// <summary>Delete this machine's log files older than <see cref="KeptDays"/>; files it did not name stay.</summary>
    public void Prune()
    {
        if (_folder is null) return;
        foreach (var name in Due(_folder, _clock())) Remove(_folder, name, _clock());
    }

    /// <summary>
    /// The log files in <paramref name="folder"/> past <see cref="KeptDays"/> at <paramref name="now"/>, by name: what the prune takes, and
    /// what the person's press lists (KEEP1e). Never today's, which its writer holds. None where the folder cannot be read.
    /// </summary>
    public static IReadOnlyList<string> Due(string folder, DateTimeOffset now)
    {
        try
        {
            if (!Directory.Exists(folder)) return [];
            return [.. Directory.EnumerateFiles(folder, "*.jsonl").Select(Path.GetFileName).OfType<string>().Where(name => Past(name, now))
                .Order(StringComparer.Ordinal)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// One log file the prune or a press names, removed where its day is still past <see cref="KeptDays"/>: its bytes, or null where it
    /// stays (no such day's file, within its days, or a delete refused; another process may still hold yesterday's file).
    /// </summary>
    public static long? Remove(string folder, string name, DateTimeOffset now)
    {
        if (!Past(name, now) || name != Path.GetFileName(name)) return null;
        var path = Path.Combine(folder, name);
        try
        {
            if (!File.Exists(path)) return null;
            var bytes = new FileInfo(path).Length;
            File.Delete(path);
            return bytes;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The next start tries again.
            return null;
        }
    }

    /// <summary>Whether a log file's name says a day past <see cref="KeptDays"/> at <paramref name="now"/>.</summary>
    private static bool Past(string name, DateTimeOffset now) =>
        name.Length >= 10 && name.EndsWith(".jsonl", StringComparison.Ordinal)
        && DateTime.TryParseExact(name[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
        && day < now.ToUniversalTime().Date.AddDays(1 - KeptDays);

    /// <summary>
    /// Write every exception nothing else caught (LOG1a): an unhandled one ends the process, and until
    /// now it ended it without a trace; an unobserved task's is written and the process runs on.
    /// </summary>
    public void WatchUnhandled()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Unhandled(e.ExceptionObject as Exception, e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) => Failed("an unobserved task", e.Exception, false);
    }

    /// <summary>
    /// Run a synchronous entry point's body (the desktop's <c>app.Run()</c>, MACHINELOGAPPEND1), writing the exception that
    /// leaves it while this log is still open, and letting it go on as it was: the <c>using</c> that holds this log may close
    /// it before the runtime raises that exception as unhandled, and the line was dropped.
    /// </summary>
    /// <remarks>Written once: <see cref="WatchUnhandled"/>'s handler, if it is reached with this log still open, skips it.</remarks>
    public void RunWatched(Action body)
    {
        try
        {
            body();
        }
        catch (Exception error)
        {
            Failed("unhandled", error, terminating: true);
            Volatile.Write(ref _ended, error);
            throw;
        }
    }

    /// <summary>An exception nothing caught, written unless <see cref="RunWatched"/> already wrote it.</summary>
    internal void Unhandled(Exception? error, bool terminating)
    {
        if (error is not null && ReferenceEquals(error, Volatile.Read(ref _ended))) return;
        Failed("unhandled", error, terminating);
    }

    /// <summary>
    /// Write the exception that ends an async entry point holding this log in a <c>using</c> (HOSTSTART2, the HTTP host's
    /// HOSTSTART1), which <see cref="WatchUnhandled"/> cannot: the <c>using</c> closes this log as the exception leaves the
    /// entry point, before the runtime raises it as unhandled, so that line is dropped and a process that died at start
    /// leaves nothing of why. The headless driver calls it beside its log's opening.
    /// </summary>
    /// <returns>The watch, whose <see cref="EntryPointWatch.Running"/> the process calls once its start is over.</returns>
    /// <remarks>Call it on the entry point's own thread, before its first await: that thread is the one it watches.</remarks>
    public EntryPointWatch WatchEntryPoint()
    {
        var watch = ForEntryPoint();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => watch.Raised(e.ExceptionObject as Exception, e.IsTerminating);
        return watch;
    }

    /// <summary>A watch on the calling thread, with this log's home, source, clock and cap, registered nowhere.</summary>
    internal EntryPointWatch ForEntryPoint() => new(_home, _source, _clock, _cap);

    /// <summary>
    /// A task nobody awaits, observed (LOG2b): when it fails, its exception is written as the
    /// <c>error</c> event with <paramref name="where"/>, and then <paramref name="failed"/> runs. A task
    /// that finishes or is cancelled writes nothing.
    /// </summary>
    /// <returns>The observation: it completes after <paramref name="task"/> does, and never faults.</returns>
    /// <remarks>
    /// The first real log's one <c>error</c> in the browser was a task's exception the finalizer rethrew:
    /// an AggregateException whose only place was "an unobserved task", written whenever a collection
    /// happened to run. A task started and let go belongs here, so its failure is said where and when it
    /// happened, and never left to the finalizer.
    /// </remarks>
    public Task Observe(Task task, string where, Action? failed = null) =>
        task.ContinueWith(
            done =>
            {
                if (done.Exception is not { } error) return;
                Failed(where, error.InnerExceptions.Count == 1 ? error.InnerExceptions[0] : error);
                try
                {
                    failed?.Invoke();
                }
                catch (Exception next)
                {
                    // Whatever the reaction threw is written too: an observation that faulted would be
                    // one more task nobody observes.
                    Failed(where, next);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    /// <summary>An exception, written as the <c>error</c> event.</summary>
    public void Failed(string where, Exception? error, bool terminating = false) =>
        Error("error",
            ("where", where),
            ("type", error?.GetType().FullName),
            ("message", error?.Message),
            ("stack", error?.ToString()),
            ("terminating", terminating));

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _file?.Dispose();
            _file = null;
            _turn?.Dispose();
            _turn = null;
        }
    }

    private FileStream? FileFor(DateTimeOffset now)
    {
        var day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_file is not null && day == _day) return _file;

        _file?.Dispose();
        _file = null;
        _turn?.Dispose();
        _turn = null;
        _day = day;
        Directory.CreateDirectory(_folder!);
        var path = Path.Combine(_folder!, $"{day}.{_source}.jsonl");
        // Shared for reading and deleting: a person tails it, and a prune elsewhere may remove an old one.
        _file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _turn = TurnFor(path);
        _full = _file.Length >= _cap;
        return _file;
    }

    /// <summary>
    /// The mutex every writer of <paramref name="path"/> shares, named for the file in the session's namespace; null where
    /// it cannot be opened (another token's), and the line is written at the end without it.
    /// </summary>
    private static Mutex? TurnFor(string path)
    {
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())))[..32];
        try
        {
            return new Mutex(false, $@"Local\Daoris.MachineLog.{key}");
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            return null;
        }
    }

    /// <summary>Whether this thread holds the file's mutex: a writer gone without releasing it hands it on.</summary>
    private bool TakeTurn()
    {
        if (_turn is null) return false;
        try
        {
            return _turn.WaitOne(TurnWait);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    private byte[] Line(DateTimeOffset now, string level, string @event, IReadOnlyList<(string Key, object? Value)> data)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, Options))
        {
            json.WriteStartObject();
            json.WriteString("time", now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            json.WriteString("source", _source);
            json.WriteString("level", level);
            json.WriteString("event", @event);
            json.WriteStartObject("data");
            foreach (var (key, value) in data)
            {
                json.WritePropertyName(key);
                Value(json, value);
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }

    private static void Value(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null: json.WriteNullValue(); break;
            case bool flag: json.WriteBooleanValue(flag); break;
            case int number: json.WriteNumberValue(number); break;
            case long number: json.WriteNumberValue(number); break;
            case double number: json.WriteNumberValue(number); break;
            case float number: json.WriteNumberValue(number); break;
            case decimal number: json.WriteNumberValue(number); break;
            case string text: json.WriteStringValue(text); break;
            default: json.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
        }
    }
}

/// <summary>
/// The exception that ends an async entry point, written once that entry point's own log is closed (HOSTSTART2): the
/// <c>error</c> event, its <c>where</c> <c>start</c> until <see cref="Running"/> is called and <c>unhandled</c> after,
/// <c>terminating</c> the runtime's word. Made by <see cref="MachineLog.WatchEntryPoint"/>.
/// </summary>
/// <remarks>
/// Only the exception that leaves the entry point is raised on the entry point's own thread, and only once nothing
/// writes through its log, so a writer of its own takes the line there; any other thread's is
/// <see cref="MachineLog.WatchUnhandled"/>'s, through the log still open, and is never written twice.
/// <b>A twin</b>: the service's <c>Daoris.Knowledge.EntryPointWatch</c> is the same with its own code (HOSTSTART1).
/// </remarks>
public sealed class EntryPointWatch
{
    private readonly string? _home;
    private readonly string _source;
    private readonly Func<DateTimeOffset> _clock;
    private readonly long _cap;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private int _running;

    internal EntryPointWatch(string? home, string source, Func<DateTimeOffset> clock, long cap)
    {
        _home = home;
        _source = source;
        _clock = clock;
        _cap = cap;
    }

    /// <summary>The start is over: an exception that ends the process from now on is <c>unhandled</c>.</summary>
    public void Running() => Volatile.Write(ref _running, 1);

    /// <summary>Write <paramref name="error"/>, when it was raised on the thread this watch was made on.</summary>
    internal void Raised(Exception? error, bool terminating)
    {
        if (Environment.CurrentManagedThreadId != _thread) return;
        using var last = new MachineLog(_home, _source, _clock, _cap);
        last.Failed(Volatile.Read(ref _running) == 1 ? "unhandled" : "start", error, terminating);
    }
}
