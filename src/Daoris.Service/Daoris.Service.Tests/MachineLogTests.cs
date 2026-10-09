using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The machine log's service writer (LOG1a, D94): the HTTP host's and the MCP host's lines, in the
/// format the desktop's writer keeps.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The line table is a twin's</b> (<c>docs/2026-09-30-machine-log-design.md</c> §3): the
/// desktop's <c>MachineLogTests</c> holds the same lines under its own source. The two writers share no
/// code; this table and that one change together.</para>
/// </remarks>
public sealed class MachineLogTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-hostlog-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = new(2026, 9, 30, 7, 10, 0, 123, TimeSpan.Zero);

    public MachineLogTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Logs => Path.Combine(_home, MachineLog.Folder);

    private MachineLog Log(string source = "host", long cap = MachineLog.CapBytes) => new(_home, source, () => _now, cap);

    private string[] Lines(string file)
    {
        var path = Path.Combine(Logs, file);
        if (!File.Exists(path)) return [];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>The twin's table, under this writer's source.</summary>
    public static TheoryData<string, string, (string, object?)[], string> Table => new()
    {
        {
            "info", "session.opened", [("session", "76cdd5db"), ("adapter", "claude-code-acp"), ("openMs", 5840)],
            """{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"info","event":"session.opened","data":{"session":"76cdd5db","adapter":"claude-code-acp","openMs":5840}}"""
        },
        {
            "error", "error", [("where", "the driver loop"), ("type", "System.IO.IOException"), ("message", "盘满了 \"full\"")],
            """{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"error","event":"error","data":{"where":"the driver loop","type":"System.IO.IOException","message":"盘满了 \"full\""}}"""
        },
        {
            "warn", "log", [("installed", true), ("ratio", 0.5), ("missing", null)],
            """{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"warn","event":"log","data":{"installed":true,"ratio":0.5,"missing":null}}"""
        },
        {
            "info", "app.started", [],
            """{"time":"2026-09-30T07:10:00.123Z","source":"host","level":"info","event":"app.started","data":{}}"""
        },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Each_event_is_one_line_in_the_shared_format(string level, string @event, (string, object?)[] data, string line)
    {
        using (var log = Log()) log.Write(level, @event, data);

        Assert.Equal([line], Lines("2026-09-30.host.jsonl"));
    }

    [Fact]
    public void Each_source_writes_its_own_file_and_a_new_day_starts_a_new_one()
    {
        using var host = Log();
        using var mcp = Log("mcp");

        host.Info("app.started");
        mcp.Info("app.started");
        _now = new DateTimeOffset(2026, 10, 1, 0, 0, 1, TimeSpan.Zero);
        host.Info("app.stopped");

        Assert.Single(Lines("2026-09-30.host.jsonl"));
        Assert.Single(Lines("2026-09-30.mcp.jsonl"));
        Assert.Single(Lines("2026-10-01.host.jsonl"));
    }

    [Fact]
    public void Files_older_than_the_kept_days_are_deleted_when_a_writer_starts()
    {
        Directory.CreateDirectory(Logs);
        File.WriteAllText(Path.Combine(Logs, "2026-08-31.desktop.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(Logs, "2026-09-01.host.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(Logs, "notes.txt"), "the person's own file\n");

        using var log = Log();
        log.Prune();

        Assert.Equal(
            ["2026-09-01.host.jsonl", "notes.txt"],
            Directory.GetFiles(Logs).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_full_file_says_so_once_and_takes_nothing_more_that_day()
    {
        using var log = Log(cap: 600);

        for (var i = 0; i < 20; i++) log.Info("request.failed", ("route", "/api/sessions"), ("status", 500));

        var lines = Lines("2026-09-30.host.jsonl");
        Assert.Contains("\"event\":\"log.full\"", lines[^1]);
        Assert.Single(lines, line => line.Contains("log.full", StringComparison.Ordinal));
    }

    [Fact]
    public void With_no_home_or_a_failing_folder_nothing_throws()
    {
        using (var none = new MachineLog(null, "host", () => _now))
        {
            none.Info("app.started");
            Assert.False(none.Writing);
        }

        File.WriteAllText(Logs, "a file where the folder would go");
        using var log = Log();
        log.Info("app.started");
        log.Prune();
    }

    /// <summary>
    /// HOSTSTART1, HOSTSTART2: the exception that ends an async entry point arrives once the entry point's <c>using</c> has
    /// closed its log, which then drops the line; the watch writes it with a writer of its own, at <c>start</c>.
    /// </summary>
    [Fact]
    public void An_entry_points_exception_is_written_at_start_after_its_log_is_closed()
    {
        var log = Log("mcp");
        var watch = log.ForEntryPoint();
        log.Dispose();

        log.Failed("unhandled", new IOException("dropped"), terminating: true);
        watch.Raised(new IOException("the folder is a file"), terminating: true);

        var line = Assert.Single(Lines("2026-09-30.mcp.jsonl"));
        Assert.Contains("\"level\":\"error\",\"event\":\"error\",\"data\":{\"where\":\"start\",\"type\":\"System.IO.IOException\",\"message\":\"the folder is a file\"", line);
        Assert.Contains("\"terminating\":true", line);
    }

    /// <summary>Once the process runs, the same exception is <c>unhandled</c>, as every other unhandled one is.</summary>
    [Fact]
    public void An_entry_points_exception_once_it_runs_is_unhandled()
    {
        using var log = Log();
        var watch = log.ForEntryPoint();

        watch.Running();
        watch.Raised(new InvalidOperationException("later"), terminating: true);

        Assert.Contains("\"where\":\"unhandled\"", Assert.Single(Lines("2026-09-30.host.jsonl")));
    }

    /// <summary>Another thread's exception is the open log's to write, through <c>WatchUnhandled</c>: never written twice.</summary>
    [Fact]
    public void Another_threads_exception_is_left_to_the_open_log()
    {
        using var log = Log();
        var watch = log.ForEntryPoint();

        var other = new Thread(() => watch.Raised(new InvalidOperationException("elsewhere"), terminating: true));
        other.Start();
        other.Join();

        Assert.Empty(Lines("2026-09-30.host.jsonl"));
    }
}
