using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEP1c (D195 point 5; the cleanup design §4, §9): This Daoris's <i>Disk space</i> read and its terminal twin. A kind never counted says
/// so and the read walks nothing; each kind's walk counts its own places alone, within its own budget, a partial count saying why; each
/// kind's <i>next</i> from the disk and the policy, its <i>last</i> from <c>keeping.json</c>, the run lock's holder from its file; and
/// <c>daoris-driver space</c> printing the same read, its <c>--json</c> field for field. Files only, in a home under <c>_fixtures/</c>.
/// </summary>
public sealed class SystemSpaceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "space", Guid.NewGuid().ToString("N")[..8]);

    public SystemSpaceTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A world over this home with its own sizes, so no test reads another's, and a volume that answers.</summary>
    private SpaceWorld World(SpaceSizes? sizes = null) => new(_home)
    {
        Sizes = sizes ?? new SpaceSizes(),
        Now = () => Now,
        Volume = _ => (143L << 30, 1024L << 30),
        Uses = () => DownloadUses.Read(_home, new HarnessSettings(), null),
    };

    private string Write(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private string Trees => DaorisHome.Entry(_home, HomeEntry.Trees);

    private string SetAside(string name, DateTimeOffset moved) =>
        Path.Combine(Trees, ".tidied", "games", "Daoris", $"{name}-{moved.UtcDateTime:yyyyMMdd'T'HHmmssfff'Z'}");

    private static JsonObject KindOf(JsonObject space, string id) =>
        space["kinds"]!.AsArray().Select(node => node!.AsObject()).Single(kind => (string?)kind["id"] == id);

    /// <summary>A home laid out with one file of a known size in each kind's place, and one in none of theirs.</summary>
    private void LayOut()
    {
        Write(Path.Combine(Trees, "games", "Daoris", "s-1", "README.md"), 1000);
        Write(Path.Combine(SetAside("s-0", Now.AddDays(-12)), "README.md"), 2000);
        Write(Path.Combine(HarnessSettings.ManagedHome(_home, "claude-code", "2.0.1"), "bin", "claude.exe"), 300);
        Write(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-01.driver.jsonl"), 50);
        var engine = DaorisHome.Entry(_home, HomeEntry.BrowserEngine);
        Write(Path.Combine(engine, "Default", "Cache", "data_0"), 70);
        Write(Path.Combine(engine, "Default", "Cookies"), 5);
        Write(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions), "s-1.events.jsonl"), 40);
        Write(Path.Combine(_home, "webview2", "EBWebView", "x"), 9);
        // Three bytes that read as choices (KEEP1d): a file that does not read is the policy that keeps the most.
        File.WriteAllText(Write(DaorisHome.Entry(_home, HomeEntry.DriverConfig), 3), "{}\n");
    }

    /// <summary>§4.2, §4.3: the card's rows in their order, each never counted saying so; the read itself walks nothing.</summary>
    [Fact]
    public void A_kind_never_counted_says_so_and_the_read_walks_nothing()
    {
        LayOut();
        var world = World();

        var space = SystemSpace.Read(world);

        Assert.Equal(1, (int)space["schema"]!);
        Assert.Equal(
            ["set-aside", "downloads", "browser-cache", "logs", "history", "trees", "sign-ins", "left-over", "rest"],
            space["kinds"]!.AsArray().Select(kind => (string?)kind!["id"]));
        Assert.All(space["kinds"]!.AsArray(), kind =>
        {
            Assert.Null(kind!["bytes"]);
            Assert.Null(kind["measured"]);
            Assert.False((bool)kind["counting"]!);
            // KEEP1d: the person sets the set-aside folders', the downloads' and the logs' rules, and history's age (KEEP1g); the rest are
            // not theirs here.
            Assert.Equal((string?)kind["id"] is "set-aside" or "downloads" or "logs" or "history", (bool)kind["settable"]!);
        });
        Assert.All(SpaceKinds.Table, kind => Assert.Null(world.Sizes.Of(_home, kind.Id)));
        Assert.Equal(143L << 30, (long)space["volume"]!["free"]!);
        Assert.Null(space["volume"]!["floor"]);
        Assert.Null(space["run"]);
        Assert.Null(space["held"]);
    }

    /// <summary>§4.3: each kind's walk counts its own places alone, another kind's passed over, and its reading is what the read says.</summary>
    [Fact]
    public void Each_kind_counts_its_own_places_and_passes_over_anothers()
    {
        LayOut();
        var world = World();
        foreach (var kind in SpaceKinds.Table) world.Sizes.MeasureNow(_home, kind.Id, WiringBudget.Default, CancellationToken.None);

        var space = SystemSpace.Read(world);

        var bytes = SpaceKinds.Table.ToDictionary(kind => kind.Id, kind => (long?)KindOf(space, kind.Id)["bytes"]);
        Assert.Equal(new Dictionary<string, long?>
        {
            ["set-aside"] = 2000, ["downloads"] = 300, ["browser-cache"] = 70, ["logs"] = 50, ["history"] = 40, ["trees"] = 1000,
            ["sign-ins"] = 5, ["left-over"] = 9, ["rest"] = 3,
        }, bytes);
        Assert.All(space["kinds"]!.AsArray(), kind =>
        {
            Assert.False((bool)kind!["partial"]!);
            Assert.NotNull(kind["measured"]);
        });
        Assert.Equal([HomeEntry.Toolchain.Name, HomeEntry.ToolVersions.Name], KindOf(space, "downloads")["entries"]!.AsArray().Select(each => (string?)each));
    }

    /// <summary>§4.2: a count that stopped short says what it counted, at least, and why.</summary>
    [Fact]
    public void A_count_past_its_budget_says_at_least_and_why()
    {
        Write(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-01.driver.jsonl"), 50);
        Write(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-02.driver.jsonl"), 50);
        var world = World();

        var reading = world.Sizes.MeasureNow(_home, "logs", new WiringBudget(1, TimeSpan.FromSeconds(60)), CancellationToken.None);
        var logs = KindOf(SystemSpace.Read(world), "logs");

        Assert.Equal(("budget", 1), (reading.Partial, reading.Files));
        Assert.True((bool)logs["partial"]!);
        Assert.Equal("budget", (string?)logs["partialWhy"]);
        Assert.Equal(50, (long)logs["bytes"]!);
    }

    /// <summary>§4.1, §9: the set-aside folders' next is the policy's days after the earliest move, with how many go that day and their bytes once counted.</summary>
    [Fact]
    public void The_set_asides_next_is_the_earliest_folder_by_the_policys_days_its_bytes_from_the_walk()
    {
        Write(Path.Combine(SetAside("s-a", Now.AddDays(-12)), "a"), 600);
        Write(Path.Combine(SetAside("s-b", Now.AddDays(-12).AddHours(1)), "b"), 400);
        Write(Path.Combine(SetAside("s-c", Now.AddDays(-2)), "c"), 900);
        var world = World();

        var before = KindOf(SystemSpace.Read(world), "set-aside")["next"]!;
        world.Sizes.MeasureNow(_home, "set-aside", WiringBudget.Default, CancellationToken.None);
        var after = KindOf(SystemSpace.Read(world), "set-aside")["next"]!;

        Assert.Equal(Now.AddDays(2), DateTimeOffset.Parse((string)before["at"]!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2, (int)before["count"]!);
        Assert.Null(before["bytes"]);
        Assert.Equal(1000, (long)after["bytes"]!);
    }

    /// <summary>§4.1, KEEP1b: the downloads' next is when the rule takes the first version nothing uses, its unused stretch's start and seven days.</summary>
    [Fact]
    public void The_downloads_next_is_the_first_unused_version_by_its_stretch_and_days()
    {
        foreach (var version in new[] { "2.0.1", "2.0.2" })
        {
            var folder = HarnessSettings.ManagedHome(_home, "claude-code", version);
            Write(Path.Combine(folder, "bin", "claude.exe"), 10);
            Directory.SetCreationTimeUtc(folder, Now.UtcDateTime.AddDays(-30));
            Directory.SetLastWriteTimeUtc(folder, Now.UtcDateTime.AddDays(-30));
        }

        var since = Now.AddDays(-3);
        Assert.Null(KeepingRecord.WriteUnused(_home, KeepingRecord.Read(_home), new Dictionary<string, DateTimeOffset?>
        {
            ["agent/claude-code/2.0.1"] = since, ["agent/claude-code/2.0.2"] = since.AddDays(1),
        }));

        var space = SystemSpace.Read(World());
        var next = KindOf(space, "downloads")["next"]!;
        var unread = KindOf(SystemSpace.Read(World() with { Uses = null }), "downloads");

        Assert.Equal(since.AddDays(7), DateTimeOffset.Parse((string)next["at"]!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, (int)next["count"]!);
        Assert.Null(next["bytes"]);
        Assert.Equal((7, "days"), ((int)KindOf(space, "downloads")["rule"]!["days"]!, (string?)KindOf(space, "downloads")["rule"]!["kind"]));
        Assert.Null(unread["next"]);
        Assert.Equal("unread", (string?)unread["nextWhy"]);
    }

    /// <summary>§4.1: the machine log's next is its oldest day's files, gone once that day is thirty days old.</summary>
    [Fact]
    public void The_logs_next_is_the_oldest_days_files_at_thirty_days()
    {
        var logs = DaorisHome.Entry(_home, HomeEntry.Logs);
        Write(Path.Combine(logs, "2026-10-01.driver.jsonl"), 50);
        Write(Path.Combine(logs, "2026-10-01.desktop.jsonl"), 25);
        Write(Path.Combine(logs, "2026-10-05.driver.jsonl"), 80);
        Write(Path.Combine(logs, "notes.txt"), 7);

        var next = KindOf(SystemSpace.Read(World()), "logs")["next"]!;

        Assert.Equal(new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.Parse((string)next["at"]!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal((2, 75L), ((int)next["count"]!, (long)next["bytes"]!));
    }

    /// <summary>§4.1, §9: each rule the card shows already runs, in its words' fields, each at its default where the person set none.</summary>
    [Fact]
    public void Each_kinds_rule_is_the_one_that_runs()
    {
        var space = SystemSpace.Read(World());
        string? Rule(string id, string field) => KindOf(space, id)["rule"]![field]?.ToString();

        Assert.Equal(("days", "14", "false", "true"), (Rule("set-aside", "kind"), Rule("set-aside", "days"), Rule("set-aside", "whole"), Rule("set-aside", "default")));
        Assert.Equal(("days", "30", "true"), (Rule("logs", "kind"), Rule("logs", "days"), Rule("logs", "default")));
        Assert.Equal("press", Rule("history", "kind"));
        Assert.Equal("proof", Rule("trees", "kind"));
        Assert.Equal(("own", "browser"), (Rule("browser-cache", "kind"), Rule("browser-cache", "whose")));
        Assert.Equal(("own", "agents"), (Rule("sign-ins", "kind"), Rule("sign-ins", "whose")));
        Assert.Equal(("own", "person"), (Rule("left-over", "kind"), Rule("left-over", "whose")));
    }

    /// <summary>§2.3, §4.1: a kind's last clean is <c>keeping.json</c>'s; a kind never cleaned has none, and a record that does not read says so.</summary>
    [Fact]
    public void A_kinds_last_is_keeping_jsons_and_an_unread_record_says_so()
    {
        var at = Now.AddDays(-1);
        Assert.Null(KeepingRecord.WriteLast(_home, KeepingRecord.Read(_home), "set-aside",
            new KeepingLast(at, KeepingRun.Look, "run-1", 3, 4096, new Dictionary<string, int> { ["written"] = 1 })));

        var space = SystemSpace.Read(World());
        var last = KindOf(space, "set-aside")["last"]!;

        Assert.Equal(at, DateTimeOffset.Parse((string)last["at"]!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(("look", 3, 4096L, 1), ((string?)last["door"], (int)last["removed"]!["count"]!, (long)last["removed"]!["bytes"]!, (int)last["kept"]!["written"]!));
        Assert.Null(KindOf(space, "downloads")["last"]);
        Assert.Null(KindOf(space, "downloads")["lastWhy"]);

        File.WriteAllText(KeepingRecord.PathOf(_home), "{ not json");
        var unread = SystemSpace.Read(World());
        Assert.Null(KindOf(unread, "set-aside")["last"]);
        Assert.Equal("unreadable", (string?)KindOf(unread, "set-aside")["lastWhy"]);
        Assert.Equal("unreadable", (string?)KindOf(unread, "logs")["lastWhy"]);
        Assert.Equal("unreadable", (string?)KindOf(unread, "history")["lastWhy"]);
        Assert.Null(KindOf(unread, "trees")["lastWhy"]);
    }

    /// <summary>
    /// KEEP1d (§2.2, §4.1): the rules the person set in <c>driver.json</c>'s <c>keep</c> are the ones the card says, none of them the
    /// default; and the machine log's next is its oldest day's files at the person's days.
    /// </summary>
    [Fact]
    public void The_rules_the_person_set_are_said_and_the_logs_next_reads_their_days()
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        DriverConfig.Load(config)
            .WithKeep(new KeepSet("set-aside", Days: 30, Whole: true))
            .WithKeep(new KeepSet("downloads", Never: true))
            .WithKeep(new KeepSet("logs", Days: 14))
            .WithKeep(new KeepSet("history", Days: 365))
            .Save(config);
        Write(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-15.driver.jsonl"), 50);

        var space = SystemSpace.Read(World());
        string? Rule(string id, string field) => KindOf(space, id)["rule"]![field]?.ToString();

        Assert.Equal(("days", "30", "true", "false"), (Rule("set-aside", "kind"), Rule("set-aside", "days"), Rule("set-aside", "whole"), Rule("set-aside", "default")));
        Assert.Equal(("never", null, "false"), (Rule("downloads", "kind"), Rule("downloads", "days"), Rule("downloads", "default")));
        Assert.Equal(("days", "14", "false"), (Rule("logs", "kind"), Rule("logs", "days"), Rule("logs", "default")));
        Assert.Equal("{\"min\":1,\"max\":90,\"whole\":true,\"never\":false}", KindOf(space, "set-aside")["bounds"]!.ToJsonString());
        Assert.Equal("{\"min\":0,\"max\":365,\"whole\":false,\"never\":true}", KindOf(space, "downloads")["bounds"]!.ToJsonString());
        Assert.Equal("{\"min\":7,\"max\":30,\"whole\":false,\"never\":false}", KindOf(space, "logs")["bounds"]!.ToJsonString());
        // KEEP1g: history's age, its default the press alone.
        Assert.Equal(("days", "365", "false"), (Rule("history", "kind"), Rule("history", "days"), Rule("history", "default")));
        Assert.Equal("{\"min\":30,\"max\":3650,\"whole\":false,\"never\":true}", KindOf(space, "history")["bounds"]!.ToJsonString());
        Assert.Null(KindOf(space, "trees")["bounds"]);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero),
            DateTimeOffset.Parse((string)KindOf(space, "logs")["next"]!["at"]!, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>§3.1, §3.3: the run lock's holder from its file, a run under way, and one whose process is gone read as stopped before its end.</summary>
    [Fact]
    public void The_run_locks_holder_is_said_and_a_gone_one_as_stopped()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(KeepingRun.HolderPathOf(_home))!);
        File.WriteAllText(KeepingRun.HolderPathOf(_home), """{ "pid": 4242, "since": "2026-10-22T08:00:00.000Z", "door": "terminal" }""");

        var running = SystemSpace.Read(World() with { Lives = _ => true })["held"]!;
        var gone = SystemSpace.Read(World() with { Lives = _ => false })["held"]!;

        Assert.Equal((4242, "terminal", false), ((int)running["pid"]!, (string?)running["door"], (bool)running["gone"]!));
        Assert.True((bool)gone["gone"]!);
    }

    /// <summary>§4.3: a background walk counts the kinds asked one after another, telling the home's listener as each ends.</summary>
    [Fact]
    public async Task A_background_walk_tells_its_homes_listener_as_each_kind_ends()
    {
        LayOut();
        var sizes = new SpaceSizes();
        var told = new List<string>();
        sizes.Tell(_home, kind => { lock (told) told.Add(kind); });

        Assert.True(sizes.Start(_home, ["logs", "set-aside", "nowhere"], WiringBudget.Default));
        await sizes.Finished(_home).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(["set-aside", "logs"], told);
        Assert.Equal(50, sizes.Of(_home, "logs")!.Bytes);
        Assert.False(sizes.Counting(_home, "logs"));
        Assert.False(sizes.Start(_home, ["nowhere"], WiringBudget.Default));
    }

    /// <summary>§3.1 step 6: the pass counts each kind at most once a day by itself, and at a run's end the kinds it removed from.</summary>
    [Fact]
    public async Task The_pass_counts_a_kind_at_most_daily_and_what_it_removed_from()
    {
        LayOut();
        var sizes = new SpaceSizes();
        var told = new List<string>();
        sizes.Tell(_home, kind => { lock (told) told.Add(kind); });

        sizes.StartDue(_home, DateTimeOffset.UtcNow, [], WiringBudget.Default);
        await sizes.Finished(_home).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(SpaceKinds.Table.Select(kind => kind.Id), told);

        told.Clear();
        Assert.False(sizes.StartDue(_home, DateTimeOffset.UtcNow, [], WiringBudget.Default));
        Assert.True(sizes.StartDue(_home, DateTimeOffset.UtcNow, ["downloads"], WiringBudget.Default));
        await sizes.Finished(_home).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(["downloads"], told);

        told.Clear();
        sizes.StartDue(_home, DateTimeOffset.UtcNow.AddDays(1), [], WiringBudget.Default);
        await sizes.Finished(_home).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(SpaceKinds.Table.Select(kind => kind.Id), told);
    }

    /// <summary>§4.4: the terminal prints the same read, <c>--json</c> field for field; <c>--measure</c> counts first; a word it does not take is exit 2.</summary>
    [Fact]
    public void The_terminal_prints_the_read_and_its_json_field_for_field()
    {
        LayOut();
        var world = World();

        Assert.Equal(0, SpaceCommand.Run(["--json"], world, out var json));
        Assert.Equal(SystemSpace.Json(SystemSpace.Read(world)), json);
        Assert.Equal(0, SpaceCommand.Run([], world, out var before));
        Assert.Contains("set-aside: not counted yet (--measure counts it)\n", before);
        Assert.Contains("  rule: kept 14 days; its package folders go an hour after the move\n", before);
        Assert.Contains("  next: 1 on 2026-10-24\n", before);
        Assert.Contains("  last: nothing removed yet\n", before);
        Assert.Contains("history: not counted yet (--measure counts it)\n  rule: kept until you clear it\n", before);
        Assert.Contains("free space: 143.0 GB of 1024.0 GB, on the volume of ", before);

        Assert.Equal(0, SpaceCommand.Run(["--measure"], world, out var measured));
        Assert.Contains("set-aside: 2 KB in 1 files, counted ", measured);
        Assert.Contains("  next: 1 on 2026-10-24, 2 KB\n", measured);

        Assert.Equal(2, SpaceCommand.Run(["--free"], world, out var refused));
        Assert.Equal($"space: no option `--free`.\n{SpaceCommand.Usage}\n", refused);
    }
}
