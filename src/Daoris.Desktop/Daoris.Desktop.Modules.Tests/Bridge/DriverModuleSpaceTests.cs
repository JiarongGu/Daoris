using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The <i>Disk space</i> card's read over the bridge (`SystemRoutes.cs`, KEEP1c, the cleanup design §4.4): the driver library's one reader
/// as the window answers it, a null kept as a field, a kind counted only when asked, each kind's end told as <c>wiring</c>, and nothing
/// removed. The sizes are the process's (<see cref="SpaceSizes.Shared"/>), keyed by each test's own home.
/// </summary>
public sealed class DriverModuleSpaceTests : DriverModuleBridge
{
    [Fact]
    public async Task The_read_walks_nothing_and_a_kind_asked_is_counted_and_told()
    {
        var module = new DriverModule(Bus, Loop());
        var logs = DaorisHome.Entry(Home, HomeEntry.Logs);
        Directory.CreateDirectory(logs);
        File.WriteAllBytes(Path.Combine(logs, "2026-10-01.driver.jsonl"), new byte[50]);

        var open = await AnswerAsync(module, "SYSTEM_SPACE");
        var row = open.GetProperty("kinds").EnumerateArray().Single(kind => kind.GetProperty("id").GetString() == "logs");
        // Never counted: its size is a field, null, and the open told nothing.
        Assert.Equal(JsonValueKind.Null, row.GetProperty("bytes").ValueKind);
        Assert.Equal(JsonValueKind.Null, open.GetProperty("run").ValueKind);
        Assert.Equal("2026-10-31", row.GetProperty("next").GetProperty("at").GetString()![..10]);
        Assert.Equal(30, row.GetProperty("rule").GetProperty("days").GetInt32());
        await Task.Delay(100);
        Assert.DoesNotContain(Raised, message => message.Type == StatePublisher.Type);

        await AnswerAsync(module, "SYSTEM_SPACE", new { measure = new[] { "logs", "nowhere" } });
        await SpaceSizes.Shared.Finished(Home);
        var read = await AnswerAsync(module, "SYSTEM_SPACE");

        var counted = read.GetProperty("kinds").EnumerateArray().Single(kind => kind.GetProperty("id").GetString() == "logs");
        Assert.Equal(50, counted.GetProperty("bytes").GetInt64());
        Assert.False(counted.GetProperty("counting").GetBoolean());
        Assert.All(read.GetProperty("kinds").EnumerateArray().Where(kind => kind.GetProperty("id").GetString() != "logs"),
            kind => Assert.Equal(JsonValueKind.Null, kind.GetProperty("bytes").ValueKind));
        // The walk's start and the kind's end, each told as `wiring`.
        await UntilAsync(() => Raised.Count(message => message.Type == StatePublisher.Type) == 2);
        Assert.All(Raised.Where(message => message.Type == StatePublisher.Type), told =>
            Assert.Equal("""{"scopes":["wiring"]}""", JsonSerializer.Serialize(told.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));
        Assert.True(File.Exists(Path.Combine(logs, "2026-10-01.driver.jsonl")), "the read removes nothing");
    }

    [Fact]
    public async Task A_kinds_last_and_the_run_locks_holder_are_answered()
    {
        var module = new DriverModule(Bus, Loop());
        Assert.Null(KeepingRecord.WriteLast(Home, KeepingRecord.Read(Home), "downloads",
            new KeepingLast(new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero), KeepingRun.Look, "run-1", 1, 300, new Dictionary<string, int>())));
        using var run = KeepingRun.TryTake(Home, KeepingRun.Terminal);

        var answer = await AnswerAsync(module, "SYSTEM_SPACE");

        var downloads = answer.GetProperty("kinds").EnumerateArray().Single(kind => kind.GetProperty("id").GetString() == "downloads");
        Assert.Equal(300, downloads.GetProperty("last").GetProperty("removed").GetProperty("bytes").GetInt64());
        Assert.Equal("terminal", answer.GetProperty("held").GetProperty("door").GetString());
        Assert.False(answer.GetProperty("held").GetProperty("gone").GetBoolean());
    }

    /// <summary>A log file whose day is past the thirty the log keeps, by today's date.</summary>
    private string OldLog()
    {
        var logs = DaorisHome.Entry(Home, HomeEntry.Logs);
        Directory.CreateDirectory(logs);
        var name = $"{DateTime.UtcNow.AddDays(-60):yyyy-MM-dd}.driver.jsonl";
        File.WriteAllBytes(Path.Combine(logs, name), new byte[50]);
        return Path.Combine(logs, name);
    }

    /// <summary>KEEP1e (the cleanup design §3.3, §4.4): Free up space's list answers each rule's now and changes nothing; its run is the press.</summary>
    [Fact]
    public async Task The_list_changes_nothing_and_a_press_runs_what_it_names_read_in_the_space()
    {
        var module = new DriverModule(Bus, Loop());
        var log = OldLog();

        var plan = await AnswerAsync(module, "SYSTEM_SPACE_FREE", new { plan = true });
        var unit = plan.GetProperty("now").EnumerateArray().Single(each => each.GetProperty("kind").GetString() == "logs");
        Assert.Equal(Path.GetFileName(log), unit.GetProperty("id").GetString());
        Assert.Equal(50, unit.GetProperty("bytes").GetInt64());
        Assert.Equal(["history", "trees"], plan.GetProperty("doors").EnumerateArray().Select(door => door.GetString()));
        Assert.True(File.Exists(log), "the list removes nothing");
        Assert.False(File.Exists(KeepingRun.PathOf(Home)), "the list takes no run lock");

        var started = await AnswerAsync(module, "SYSTEM_SPACE_FREE", new { units = new[] { new { kind = "logs", id = Path.GetFileName(log), why = "past" } } });
        var id = started.GetProperty("run").GetString();
        Assert.False(started.GetProperty("joined").GetBoolean());
        await SpaceRuns.Shared.Last(Home)!.Finished;

        var space = await AnswerAsync(module, "SYSTEM_SPACE");
        var run = space.GetProperty("run");
        Assert.Equal(id, run.GetProperty("id").GetString());
        Assert.Equal(("press", "done"), (run.GetProperty("door").GetString(), run.GetProperty("outcome").GetString()));
        var logs = run.GetProperty("kinds").EnumerateArray().Single();
        Assert.Equal(("logs", "done", 1), (logs.GetProperty("id").GetString(), logs.GetProperty("state").GetString(), logs.GetProperty("removed").GetProperty("count").GetInt32()));
        Assert.False(File.Exists(log));
        Assert.Equal(JsonValueKind.Null, space.GetProperty("held").ValueKind);
        // The kind's end and the whole's, each told as `wiring`.
        await UntilAsync(() => Raised.Count(message => message.Type == StatePublisher.Type) >= 2);
    }

    /// <summary>KEEP1e (§3.1): a press while another run holds the home's run lock is refused as data, its holder named, nothing removed.</summary>
    [Fact]
    public async Task A_press_while_another_holds_the_run_lock_is_refused_naming_it()
    {
        var module = new DriverModule(Bus, Loop());
        var log = OldLog();
        using var held = KeepingRun.TryTake(Home, KeepingRun.Terminal);

        var answer = await AnswerAsync(module, "SYSTEM_SPACE_FREE", new { units = new[] { new { kind = "logs", id = Path.GetFileName(log), why = "past" } } });

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("run").ValueKind);
        Assert.Equal("held", answer.GetProperty("why").GetString());
        Assert.Equal("terminal", answer.GetProperty("held").GetProperty("door").GetString());
        Assert.Contains("holds Daoris's keeping", answer.GetProperty("message").GetString());
        Assert.True(File.Exists(log));
    }

    [Theory]
    [InlineData("""{ "units": [{ "kind": "logs", "id": "a.jsonl" }], "early": [{ "kind": "left-over", "id": "webview2", "own": true }] }""", 2)]
    [InlineData("""{ "early": [{ "kind": "set-aside", "id": "games/Daoris/x", "why": "early" }] }""", 1)]
    public void A_press_names_the_units_now_and_early(string payload, int asked)
    {
        using var document = JsonDocument.Parse(payload);
        Assert.Equal(asked, SpaceFree.Asked(document.RootElement).Count);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{ "units": [] }""")]
    [InlineData("""{ "units": [{ "kind": "everything", "id": "x" }] }""")]
    [InlineData("""{ "units": [{ "kind": "logs" }] }""")]
    public void A_press_naming_no_listed_unit_is_malformed(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        Assert.Throws<ArgumentException>(() => SpaceFree.Asked(document.RootElement));
    }

    [Theory]
    [InlineData("""{ "measure": true }""", 9)]
    [InlineData("""{ "measure": ["logs", "nowhere", 3] }""", 1)]
    [InlineData("""{ "measure": false }""", 0)]
    [InlineData("""{}""", 0)]
    public void A_read_asks_every_kind_the_named_ones_or_none(string payload, int asked)
    {
        using var document = JsonDocument.Parse(payload);
        Assert.Equal(asked, SystemRoutes.SpaceAsked(document.RootElement).Count);
    }
}
