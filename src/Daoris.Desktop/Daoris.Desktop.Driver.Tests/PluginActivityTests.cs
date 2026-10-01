using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a plugin did (PLUGUI1d, D119 §4.1): <see cref="PluginActivity.Read"/> summarises the machine log's lines for
/// one plugin over a period, with its pushes from the landing record, for the page's Activity and
/// <c>daoris-driver plugins activity</c> alike.
/// </summary>
public sealed class PluginActivityTests : IDisposable
{
    private const string Id = "acme.gate";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-activity-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly List<(string Source, string Line)> _lines = [];

    public PluginActivityTests()
    {
        Directory.CreateDirectory(_home);
        Install(Id, """
            { "id": "acme.gate", "version": "1.0.0",
              "harnesses": [ { "name": "gate-agent", "command": ["node", "${plugin}/agent.mjs"] } ],
              "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider", "session/ended", "work/land"] },
              "servers": [ { "name": "browser", "command": ["node", "${plugin}/browser.mjs", "${browser}"] } ] }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_fixture_logs_summary_by_point_process_agents_servers_and_trials()
    {
        Line(-60, "desktop", "plugin.started", new { plugin = Id, points = "quest/consider,session/ended", ms = 40, by = "loop" });
        foreach (var (ms, answer) in new[] { (10, "allow"), (30, "allow"), (50, "allow"), (20, "hold") })
        {
            Line(-50, "desktop", "plugin.called", new { plugin = Id, point = "quest/consider", answer, ms });
        }

        Line(-45, "desktop", "plugin.failed", new { plugin = Id, where = "quest/consider", kind = "late", code = (int?)null, ms = 10000, by = "loop" }, "warn");
        Line(-44, "desktop", "plugin.called", new { plugin = Id, point = "session/ended", answer = "answered", ms = 5 });
        Line(-40, "desktop", "plugin.failed", new { plugin = Id, where = "process", kind = "exited", code = 1, ms = (int?)null, by = "loop" }, "warn");
        Line(-39, "desktop", "plugin.failed", new { plugin = Id, where = "start", kind = "unstartable", code = (int?)null, ms = 3, by = "loop" }, "warn");
        Line(-38, "desktop", "plugin.started", new { plugin = Id, points = "quest/consider,session/ended", ms = 45, by = "loop" });
        Line(-30, "driver", "plugin.started", new { plugin = Id, points = "work/land", ms = 50, by = "landing" });
        Line(-30, "driver", "plugin.called", new { plugin = Id, point = "work/land", answer = "pushed", ms = 2000 });
        Line(-30, "driver", "plugin.stopped", new { plugin = Id, why = "ended", by = "landing" });
        Line(-20, "desktop", "plugin.served", new { plugin = Id, server = "browser", session = "s1", handed = true });
        Line(-20, "desktop", "plugin.served", new { plugin = Id, server = "browser", session = "s2", handed = true });
        Line(-20, "driver", "plugin.served", new { plugin = Id, server = "browser", session = "s3", handed = false });
        Line(-10, "driver", "plugin.tried", new { plugin = Id, passed = true, checks = 4, failed = 0, ms = 900, door = "terminal" });
        Line(-9, "desktop", "plugin.tried", new { plugin = Id, passed = false, checks = 4, failed = 1, ms = 800, door = "screen" }, "warn");
        foreach (var adapter in new[] { "gate-agent", "gate-agent", "GATE-AGENT", "claude-code" })
        {
            Line(-5, "desktop", "session.started", new { session = "x", kind = "driven", adapter, repository = "engine" });
        }

        // Another plugin's lines are not this one's.
        Line(-5, "desktop", "plugin.called", new { plugin = "acme.other", point = "quest/consider", answer = "hold", ms = 1 });
        Write();

        var read = PluginActivity.Read(_home, Id, Now.AddDays(-7));

        Assert.True(read.Logged);
        Assert.Equal(["quest/consider", "session/ended", "work/land"], read.Points.Select(p => p.Point));
        var consider = read.Points[0];
        Assert.Equal(new Dictionary<string, int> { ["allow"] = 3, ["hold"] = 1 }, consider.Answers);
        Assert.Equal(new Dictionary<string, int> { ["late"] = 1 }, consider.Failures);
        // The median of 10, 20, 30 and 50 is the two middles' mean; a late failure's wait is no answer time.
        Assert.Equal(25, consider.MedianMs);
        Assert.Equal(50, consider.SlowestMs);
        Assert.Equal((2000L, 2000L), (read.Points[2].MedianMs!.Value, read.Points[2].SlowestMs!.Value));

        Assert.Equal(new ProcessActivity(Starts: 3, Exits: 1, FailedStarts: 1), read.Process);
        Assert.Equal(new TrialActivity(Passed: 1, Failed: 1), read.Trials);
        Assert.Equal([new CountedName("gate-agent", 3)], read.Agents);
        Assert.Equal([new ServerActivity("browser", Handed: 2, Withheld: 1)], read.Servers);
        Assert.DoesNotContain(read.Recent, e => e.Event == "called" && e.Ms == 1);
    }

    [Fact]
    public void A_torn_line_is_skipped_and_counted_and_the_rest_are_read()
    {
        Line(-5, "desktop", "plugin.called", new { plugin = Id, point = "quest/consider", answer = "allow", ms = 7 });
        Write();
        File.AppendAllText(Path.Combine(_home, "logs", "2026-10-01.desktop.jsonl"),
            """{"time":"2026-10-01T11:59:00.000Z","source":"desktop","level":"info","event":"plugin.cal""" + "\n");

        var read = PluginActivity.Read(_home, Id, Now.AddDays(-7));

        Assert.Equal(1, read.Skipped);
        Assert.Equal(1, read.Points[0].Answers["allow"]);
    }

    [Fact]
    public void Only_the_period_is_read()
    {
        Line(-60 * 24 * 10, "desktop", "plugin.called", new { plugin = Id, point = "quest/consider", answer = "allow", ms = 7 });
        Line(-60, "desktop", "plugin.called", new { plugin = Id, point = "quest/consider", answer = "hold", ms = 8 });
        Write();

        var week = PluginActivity.Read(_home, Id, Now - PluginActivity.DefaultPeriod);
        var month = PluginActivity.Read(_home, Id, Now.AddDays(-30));

        Assert.Equal(new Dictionary<string, int> { ["hold"] = 1 }, week.Points[0].Answers);
        Assert.Equal(2, month.Points[0].Answers.Values.Sum());
        Assert.Equal(Now - PluginActivity.DefaultPeriod, week.Since);
    }

    [Fact]
    public void The_newest_fifty_events_newest_first_and_how_many_more_there_were()
    {
        for (var i = 0; i < 60; i++)
        {
            Line(-120 + i, "desktop", "plugin.called", new { plugin = Id, point = "quest/consider", answer = "allow", ms = i });
        }

        Line(-1, "desktop", "plugin.failed", new { plugin = Id, where = "quest/consider", kind = "unreadable", code = (int?)null, ms = 4, by = "loop" }, "warn");
        Write();

        var read = PluginActivity.Read(_home, Id, Now.AddDays(-7));

        Assert.Equal(PluginActivity.RecentLines, read.Recent.Count);
        Assert.Equal(11, read.More);
        Assert.Equal(new ActivityEvent(Now.AddMinutes(-1), "failed", "quest/consider", "unreadable", 4), read.Recent[0]);
        Assert.Equal(new ActivityEvent(Now.AddMinutes(-61), "called", "quest/consider", "allow", 59), read.Recent[1]);
        Assert.True(read.Recent.Zip(read.Recent.Skip(1)).All(pair => pair.First.At >= pair.Second.At));
    }

    [Fact]
    public void Its_pushes_come_from_the_landing_record_never_from_the_log()
    {
        var record = new LandedBranches(_home);
        record.Record(Landed("engine", "feature/a", Now.AddDays(-1)));
        record.Pushed("engine", "feature/a", new PluginLanding(Id, true, "https://example.test/pull/1", "pushed"), "abc");
        record.Record(Landed("game", "feature/b", Now.AddDays(-2)));
        record.Pushed("game", "feature/b", new PluginLanding("acme.other", true, "https://example.test/pull/2", "pushed"), "abc");
        record.Record(Landed("engine", "feature/c", Now.AddDays(-3)));
        record.Pushed("engine", "feature/c", new PluginLanding(Id, false, null, "gh is not signed in"), "abc");
        record.Record(Landed("engine", "feature/old", Now.AddDays(-20)));
        record.Pushed("engine", "feature/old", new PluginLanding(Id, true, "https://example.test/pull/0", "pushed"), "abc");

        var read = PluginActivity.Read(_home, Id, Now.AddDays(-7));

        Assert.Equal([new PluginPush("engine", "feature/a", "https://example.test/pull/1", Now.AddDays(-1))], read.Pushes);
    }

    [Fact]
    public void No_log_on_this_machine_is_said_and_an_unknown_plugin_is_refused()
    {
        var read = PluginActivity.Read(_home, Id, Now.AddDays(-7));

        Assert.False(read.Logged);
        Assert.All(read.Points, point => Assert.Empty(point.Answers));
        Assert.Empty(read.Recent);

        var refused = Assert.Throws<DriverException>(() => PluginActivity.Read(_home, "acme.nobody", Now.AddDays(-7)));
        Assert.Contains("no plugin `acme.nobody`", refused.Message);
    }

    // ——— helpers

    private void Install(string id, string manifest)
    {
        var folder = Path.Combine(_home, "plugins", id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
    }

    private static LandedBranch Landed(string repository, string branch, DateTimeOffset at) =>
        new(repository, "default", branch, "main", "abc", "s1", "q1", "Fix", at);

    private void Line(int minutes, string source, string @event, object data, string level = "info")
    {
        var time = Now.AddMinutes(minutes).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
        _lines.Add((source, JsonSerializer.Serialize(new { time, source, level, @event, data })));
    }

    /// <summary>Each line into its source's file of its day, as the writers lay them out.</summary>
    private void Write()
    {
        var folder = Path.Combine(_home, "logs");
        Directory.CreateDirectory(folder);
        foreach (var (source, line) in _lines)
        {
            var day = JsonDocument.Parse(line).RootElement.GetProperty("time").GetString()![..10];
            File.AppendAllText(Path.Combine(folder, $"{day}.{source}.jsonl"), line + "\n");
        }
    }
}
