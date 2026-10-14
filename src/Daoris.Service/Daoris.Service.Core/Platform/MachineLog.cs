using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The machine log's service writer (LOG1, D94): the HTTP host's and the MCP host's lines, one JSON line
/// per event, in the home's <c>logs/</c>.
/// </summary>
/// <remarks>
/// <para><b>A twin</b>: the desktop's <c>Daoris.Driver.MachineLog</c> writes the same format with its own
/// code, and the two share none (the twins rule). The format is the contract
/// (<c>docs/2026-09-30-machine-log-design.md</c> §3), and each side's tests hold the same line table.</para>
///
/// <para><b>One file per source per day</b> (<c>&lt;date&gt;.&lt;source&gt;.jsonl</c>, the date in UTC):
/// two processes appending to one file can overwrite each other's lines. Appended a line at a time, as a
/// log is; kept thirty days; capped so a loop cannot fill a disk.</para>
///
/// <para><b>Never a reason to fail</b>: no home is no log, and a write that fails is dropped. What is
/// never written here is D94's list — anyone's words, a file's contents, a secret, a URL's query — and a
/// request is logged by its route and status, never its query or body.</para>
/// </remarks>
public sealed class MachineLog : IDisposable
{
    /// <summary>The folder under the home.</summary>
    public const string Folder = "logs";

    /// <summary>How many days of files are kept, today included.</summary>
    public const int KeptDays = 30;

    /// <summary>Where a file stops growing.</summary>
    public const long CapBytes = 20L * 1024 * 1024;

    private static readonly JsonWriterOptions Options = new()
    {
        // Readable in a text editor, 中文 included.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string? _home;
    private readonly string? _folder;
    private readonly string _source;
    private readonly Func<DateTimeOffset> _clock;
    private readonly long _cap;
    private readonly object _gate = new();
    private FileStream? _file;
    private string? _day;
    private bool _full;
    private bool _disposed;

    /// <param name="home">The Daoris home, or null for none: nothing is written.</param>
    /// <param name="source">Which process kind this is: <c>host</c> or <c>mcp</c>.</param>
    /// <param name="clock">What time it is; the system's by default.</param>
    /// <param name="cap">Where a file stops growing.</param>
    public MachineLog(string? home, string source, Func<DateTimeOffset>? clock = null, long cap = CapBytes)
    {
        _home = string.IsNullOrEmpty(home) ? null : home;
        _folder = _home is null ? null : Path.Combine(_home, Folder);
        _source = source;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _cap = cap;
    }

    /// <summary>This process's log under the home the environment names, with its old files pruned.</summary>
    public static MachineLog Open(string source)
    {
        var log = new MachineLog(DaorisHome.Resolve(), source);
        log.Prune();
        return log;
    }

    /// <summary>Whether there is anywhere to write: false with no home.</summary>
    public bool Writing => _folder is not null;

    public void Info(string @event, params (string Key, object? Value)[] data) => Write("info", @event, data);

    public void Warn(string @event, params (string Key, object? Value)[] data) => Write("warn", @event, data);

    public void Error(string @event, params (string Key, object? Value)[] data) => Write("error", @event, data);

    /// <summary>One line; values are strings, numbers, booleans or null, and anything else its text.</summary>
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
                if (file.Length + line.Length > _cap)
                {
                    _full = true;
                    Append(file, Line(now, "warn", "log.full", [("capBytes", _cap)]));
                    return;
                }

                Append(file, line);
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
        try
        {
            if (!Directory.Exists(_folder)) return;
            var oldest = _clock().ToUniversalTime().Date.AddDays(1 - KeptDays);
            foreach (var path in Directory.EnumerateFiles(_folder, "*.jsonl"))
            {
                var name = Path.GetFileName(path);
                if (name.Length < 10
                    || !DateTime.TryParseExact(name[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    || day >= oldest)
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Another process may still hold it; the next start tries again.
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Write every exception nothing else caught, as the <c>error</c> event.</summary>
    public void WatchUnhandled()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Failed("unhandled", e.ExceptionObject as Exception, e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) => Failed("an unobserved task", e.Exception, false);
    }

    /// <summary>
    /// Write the exception that ends an async entry point holding this log in a <c>using</c> (HOSTSTART1, HOSTSTART2),
    /// which <see cref="WatchUnhandled"/> cannot: the <c>using</c> closes this log as the exception leaves the entry point,
    /// before the runtime raises it as unhandled, so that line is dropped and a process that died at start leaves nothing
    /// of why. The HTTP host and the MCP connector each call it beside their log's opening.
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
        }
    }

    private FileStream? FileFor(DateTimeOffset now)
    {
        var day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_file is not null && day == _day) return _file;

        _file?.Dispose();
        _file = null;
        _day = day;
        Directory.CreateDirectory(_folder!);
        _file = new FileStream(
            Path.Combine(_folder!, $"{day}.{_source}.jsonl"),
            FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _full = _file.Length >= _cap;
        return _file;
    }

    private static void Append(FileStream file, byte[] line)
    {
        file.Write(line);
        file.Flush();
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

            json.WriteEndObject();
            json.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }
}

/// <summary>
/// The exception that ends an async entry point, written once that entry point's own log is closed (HOSTSTART1,
/// HOSTSTART2): the <c>error</c> event, its <c>where</c> <c>start</c> until <see cref="Running"/> is called and
/// <c>unhandled</c> after, <c>terminating</c> the runtime's word. Made by <see cref="MachineLog.WatchEntryPoint"/>.
/// </summary>
/// <remarks>
/// Only the exception that leaves the entry point is raised on the entry point's own thread, and only once nothing
/// writes through its log, so a writer of its own takes the line there; any other thread's is
/// <see cref="MachineLog.WatchUnhandled"/>'s, through the log still open, and is never written twice.
/// <b>A twin</b>: the driver's <c>Daoris.Driver.EntryPointWatch</c> is the same with its own code.
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

    /// <summary>The start is over: the host serves. An exception that ends the process from now on is <c>unhandled</c>.</summary>
    public void Running() => Volatile.Write(ref _running, 1);

    /// <summary>Write <paramref name="error"/>, when it was raised on the thread this watch was made on.</summary>
    internal void Raised(Exception? error, bool terminating)
    {
        if (Environment.CurrentManagedThreadId != _thread) return;
        using var last = new MachineLog(_home, _source, _clock, _cap);
        last.Failed(Volatile.Read(ref _running) == 1 ? "unhandled" : "start", error, terminating);
    }
}
