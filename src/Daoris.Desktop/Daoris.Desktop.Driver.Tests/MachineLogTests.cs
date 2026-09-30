using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The machine log's desktop writer (LOG1a, D94): one JSON line per event, one file per source per
/// day, kept thirty days, capped, and never a reason for anything to fail.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The line table is a twin's</b> (<c>docs/2026-09-30-machine-log-design.md</c> §3): the HTTP
/// host writes the same format with its own code, and its tests hold the same lines. Change one table and
/// the other changes with it.</para>
/// </remarks>
public sealed class MachineLogTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-log-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = new(2026, 9, 30, 7, 10, 0, 123, TimeSpan.Zero);

    public MachineLogTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Logs => Path.Combine(_home, MachineLog.Folder);

    private MachineLog Log(string source = "desktop", long cap = MachineLog.CapBytes) =>
        new(_home, source, () => _now, cap);

    private string[] Lines(string file) => StubFile.Lines(Path.Combine(Logs, file));

    /// <summary>The twin's table: what each event writes, byte for byte.</summary>
    public static TheoryData<string, string, (string, object?)[], string> Table => new()
    {
        {
            "info", "session.opened", [("session", "76cdd5db"), ("adapter", "claude-code-acp"), ("openMs", 5840)],
            """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","event":"session.opened","data":{"session":"76cdd5db","adapter":"claude-code-acp","openMs":5840}}"""
        },
        {
            "error", "error", [("where", "the driver loop"), ("type", "System.IO.IOException"), ("message", "盘满了 \"full\"")],
            """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"error","event":"error","data":{"where":"the driver loop","type":"System.IO.IOException","message":"盘满了 \"full\""}}"""
        },
        {
            "warn", "log", [("installed", true), ("ratio", 0.5), ("missing", null)],
            """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"warn","event":"log","data":{"installed":true,"ratio":0.5,"missing":null}}"""
        },
        {
            "info", "app.started", [],
            """{"time":"2026-09-30T07:10:00.123Z","source":"desktop","level":"info","event":"app.started","data":{}}"""
        },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Each_event_is_one_line_in_the_shared_format(string level, string @event, (string, object?)[] data, string line)
    {
        using (var log = Log()) log.Write(level, @event, data);

        Assert.Equal([line], Lines("2026-09-30.desktop.jsonl"));
    }

    /// <summary>One file per source per day, the date in UTC: two processes never append to one file.</summary>
    [Fact]
    public void Each_source_writes_its_own_file_and_a_new_day_starts_a_new_one()
    {
        using var desktop = Log();
        using var browser = Log("browser");

        desktop.Info("app.started");
        browser.Info("app.started");
        _now = new DateTimeOffset(2026, 10, 1, 0, 0, 1, TimeSpan.Zero);
        desktop.Info("app.stopped", ("uptimeSeconds", 60));

        Assert.Single(Lines("2026-09-30.desktop.jsonl"));
        Assert.Single(Lines("2026-09-30.browser.jsonl"));
        Assert.Contains("\"event\":\"app.stopped\"", Assert.Single(Lines("2026-10-01.desktop.jsonl")));
    }

    /// <summary>Thirty days are kept; what is older goes when a writer starts, and nothing else does.</summary>
    [Fact]
    public void Files_older_than_the_kept_days_are_deleted_when_a_writer_starts()
    {
        Directory.CreateDirectory(Logs);
        File.WriteAllText(Path.Combine(Logs, "2026-08-30.desktop.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(Logs, "2026-08-31.host.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(Logs, "2026-09-01.desktop.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(Logs, "notes.txt"), "the person's own file\n");

        using var log = Log();
        log.Prune();

        Assert.Equal(
            ["2026-09-01.desktop.jsonl", "notes.txt"],
            Directory.GetFiles(Logs).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    /// <summary>A file stops growing at its cap, saying so once, so a loop that logs without end cannot fill a disk.</summary>
    [Fact]
    public void A_full_file_says_so_once_and_takes_nothing_more_that_day()
    {
        using var log = Log(cap: 600);

        for (var i = 0; i < 20; i++) log.Info("view.opened", ("view", "sessions"));
        _now = _now.AddDays(1);
        log.Info("view.opened", ("view", "quests"));

        var lines = Lines("2026-09-30.desktop.jsonl");
        Assert.Contains("\"event\":\"log.full\"", lines[^1]);
        Assert.Single(lines, line => line.Contains("log.full", StringComparison.Ordinal));
        Assert.True(lines.Length < 20);
        Assert.Single(Lines("2026-10-01.desktop.jsonl"));
    }

    /// <summary>No home is no log, and the process runs on: the log is evidence, never a reason to stop.</summary>
    [Fact]
    public void With_no_home_nothing_is_written_and_nothing_throws()
    {
        using var log = new MachineLog(null, "desktop", () => _now);

        log.Info("app.started");
        log.Prune();

        Assert.False(log.Writing);
    }

    /// <summary>A write that fails is dropped, never thrown: here the logs folder cannot be made.</summary>
    [Fact]
    public void A_write_that_fails_is_dropped_not_thrown()
    {
        File.WriteAllText(Logs, "a file where the folder would go");
        using var log = Log();

        log.Info("app.started");
        log.Error("error", ("message", "still nothing thrown"));
        log.Prune();
    }

    /// <summary>Written from many threads at once, every line lands whole.</summary>
    [Fact]
    public void Lines_written_from_many_threads_land_whole()
    {
        using (var log = Log())
        {
            Parallel.For(0, 200, i => log.Info("turn.ended", ("session", $"s{i}"), ("turnMs", i)));
        }

        var lines = Lines("2026-09-30.desktop.jsonl");
        Assert.Equal(200, lines.Length);
        Assert.All(lines, line => System.Text.Json.JsonDocument.Parse(line).Dispose());
    }

    /// <summary>
    /// LOG2b: a task nobody awaits, observed. The first real log's one <c>error</c> was an exception the
    /// finalizer rethrew for such a task, an AggregateException whose only place was "an unobserved
    /// task". Observed, a failure is a line that says where it happened, when it happened.
    /// </summary>
    [Fact]
    public async Task A_task_nobody_awaits_that_fails_is_written_where_it_happened()
    {
        using var log = Log("browser");
        var failed = 0;

        await log.Observe(Task.FromException(new IOException("the pipe broke")), "the browser's first window", () => failed++);

        var line = Assert.Single(Lines("2026-09-30.browser.jsonl"));
        Assert.Contains("\"level\":\"error\",\"event\":\"error\"", line);
        Assert.Contains("\"where\":\"the browser's first window\"", line);
        Assert.Contains("\"type\":\"System.IO.IOException\"", line);
        Assert.Contains("\"message\":\"the pipe broke\"", line);
        Assert.Equal(1, failed);
    }

    /// <summary>A task that finished, or was cancelled because its reason went away, is no failure.</summary>
    [Fact]
    public async Task A_task_that_finished_or_was_cancelled_writes_nothing()
    {
        using var log = Log("browser");
        var failed = 0;

        await log.Observe(Task.CompletedTask, "the browser's first window", () => failed++);
        await log.Observe(Task.FromCanceled(new CancellationToken(canceled: true)), "the browser's first window", () => failed++);

        Assert.False(File.Exists(Path.Combine(Logs, "2026-09-30.browser.jsonl")));
        Assert.Equal(0, failed);
    }

    /// <summary>What runs after a failure may fail too: that is written as well, and the observation still ends cleanly.</summary>
    [Fact]
    public async Task What_runs_after_a_failure_failing_too_is_written_and_the_observation_never_faults()
    {
        using var log = Log("browser");

        await log.Observe(
            Task.FromException(new IOException("first")), "the browser's first window",
            () => throw new InvalidOperationException("second"));

        var lines = Lines("2026-09-30.browser.jsonl");
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"type\":\"System.IO.IOException\"", lines[0]);
        Assert.Contains("\"type\":\"System.InvalidOperationException\"", lines[1]);
    }

    /// <summary>
    /// The point of it: an observed failure never reaches the finalizer. The same failure left alone does,
    /// which is what shows this probe can see one at all.
    /// </summary>
    [Fact]
    public async Task An_observed_failure_never_reaches_the_finalizer_and_one_left_alone_does()
    {
        using var log = Log("browser");

        Assert.False(await ReachesTheFinalizerAsync(task => log.Observe(task, "the browser's first window")));
        Assert.True(await ReachesTheFinalizerAsync(observe: null));
    }

    /// <summary>
    /// Whether a failed task, observed by <paramref name="observe"/> or not at all, has its exception
    /// rethrown by the finalizer: <see cref="TaskScheduler.UnobservedTaskException"/>, recognised by a
    /// message nobody else's task carries.
    /// </summary>
    private static async Task<bool> ReachesTheFinalizerAsync(Func<Task, Task>? observe)
    {
        var marker = "log2b-" + Guid.NewGuid().ToString("N");
        var reached = false;
        void Seen(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception.InnerExceptions.Any(inner => inner.Message == marker)) Volatile.Write(ref reached, true);
        }

        TaskScheduler.UnobservedTaskException += Seen;
        try
        {
            await FailUnawaitedAsync(marker, observe);
            for (var attempt = 0; attempt < 10 && !Volatile.Read(ref reached); attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Delay(20);
            }

            return Volatile.Read(ref reached);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Seen;
        }
    }

    /// <summary>A failed task made in a frame of its own, so nothing here keeps it alive once it is let go.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Task FailUnawaitedAsync(string marker, Func<Task, Task>? observe)
    {
        var failed = Task.FromException(new IOException(marker));
        return observe is null ? Task.CompletedTask : observe(failed);
    }

    /// <summary>A reader opens the file while it is being written — as a person tailing it would.</summary>
    [Fact]
    public void The_file_can_be_read_while_it_is_written()
    {
        using var log = Log();
        log.Info("app.started");

        using var reader = new FileStream(
            Path.Combine(Logs, "2026-09-30.desktop.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        log.Info("view.opened", ("view", "sessions"));

        Assert.Equal(2, Lines("2026-09-30.desktop.jsonl").Length);
    }
}
