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
}
