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
    /// <summary>The folder under the home.</summary>
    public const string Folder = "logs";

    /// <summary>How many days of files are kept, today included.</summary>
    public const int KeptDays = 30;

    /// <summary>Where a file stops growing: a loop that logs without end cannot fill a disk.</summary>
    public const long CapBytes = 20L * 1024 * 1024;

    private static readonly JsonWriterOptions Options = new()
    {
        // Readable in a text editor, 中文 included: the file is read by people as well as by the report.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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
    /// <param name="source">Which process kind this is: <c>desktop</c>, <c>driver</c>, <c>browser</c>.</param>
    /// <param name="clock">What time it is; the system's by default.</param>
    /// <param name="cap">Where a file stops growing.</param>
    public MachineLog(string? home, string source, Func<DateTimeOffset>? clock = null, long cap = CapBytes)
    {
        _folder = string.IsNullOrEmpty(home) ? null : Path.Combine(home, Folder);
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
                    // Another process may still hold yesterday's file; the next start tries again.
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Write every exception nothing else caught (LOG1a): an unhandled one ends the process, and until
    /// now it ended it without a trace; an unobserved task's is written and the process runs on.
    /// </summary>
    public void WatchUnhandled()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Failed("unhandled", e.ExceptionObject as Exception, e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) => Failed("an unobserved task", e.Exception, false);
    }

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
        // Shared for reading and deleting: a person tails it, and a prune elsewhere may remove an old one.
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
