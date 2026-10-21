using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEP1f (D195; the cleanup design §3.2, §9): below the floor, Daoris frees what it can make again and says the rest. A stand-in
/// free-space read below and above the floor; the three acts in order and nothing else, each with its own guards (a slim within its hour
/// not run, a <c>never</c> download kept, a running browser's caches kept); the caches cleared once in a stretch, not an hour later, and
/// again after <c>space.ok</c> and a new stretch; a trees home on another volume judged by its own volume's read; said once and cleared
/// at the margin; a floor out of its bounds refused; the system's read by the volume's identity; a failed read running nothing. Files
/// only, in a home under <c>_fixtures/</c>.
/// </summary>
public sealed class SpaceFloorTests : IDisposable
{
    private static readonly DateTimeOffset Moved = new(2026, 10, 22, 6, 0, 0, TimeSpan.Zero);
    private const long Gb = 1L << 30;

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "space-floor", Guid.NewGuid().ToString("N")[..8]);
    private DateTimeOffset _now = Moved.AddHours(2);

    // Two stand-in volumes, the home's and another the trees may lie on, each 100 GB, their free space set by each test.
    private readonly Dictionary<string, long> _free = new(StringComparer.Ordinal) { ["A"] = 50 * Gb, ["B"] = 50 * Gb };
    private bool _treesOnB;
    private bool _unreadable;

    public SpaceFloorTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Trees => DaorisHome.Entry(_home, HomeEntry.Trees);

    private string Engine => DaorisHome.Entry(_home, HomeEntry.BrowserEngine);

    private VolumeReading? Volume(string path)
    {
        if (_unreadable) return null;
        var id = _treesOnB && Path.GetFullPath(path).StartsWith(Path.GetFullPath(Trees), StringComparison.OrdinalIgnoreCase) ? "B" : "A";
        return new VolumeReading($@"\\?\Volume{{{id}}}\", id == "A" ? @"C:\" : @"D:\", _free[id], 100 * Gb);
    }

    private FloorWorld World(IBrowserCaches? caches = null, KeepPolicy? policy = null, SpaceFloors? floors = null) => new(_home)
    {
        Volume = Volume,
        Now = () => _now,
        Policy = _ => policy ?? KeepPolicy.Default,
        Judge = () => new DownloadJudge(() => DownloadUses.Read(_home, new HarnessSettings(), null), () => _now),
        Caches = caches,
        Log = new MachineLog(_home, "driver", () => _now),
        Floors = floors ?? _floors,
        Could = () => 3 * Gb,
    };

    private readonly SpaceFloors _floors = new();

    private static string Write(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private static void Settle(string folder, DateTimeOffset at)
    {
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", SearchOption.AllDirectories).Append(new DirectoryInfo(folder)))
        {
            entry.CreationTimeUtc = at.UtcDateTime;
            entry.LastWriteTimeUtc = at.UtcDateTime;
        }
    }

    /// <summary>A folder set aside at <paramref name="moved"/>, nothing written in it since, with a package folder its manifest names, at <c>moved</c>.</summary>
    private string Aside(string name, DateTimeOffset moved)
    {
        var folder = Path.Combine(Trees, ".tidied", "games", "Daoris", $"{name}-{moved.UtcDateTime:yyyyMMdd'T'HHmmssfff'Z'}");
        Write(Path.Combine(folder, "package.json"), 10);
        Write(Path.Combine(folder, "node_modules", "left-pad", "index.js"), 400);
        Write(Path.Combine(folder, "README.md"), 20);
        Settle(folder, moved.AddMinutes(-5));
        new SlimManifest(SlimStates.Moved, moved.AddSeconds(-1), moved, "Daoris", "daoris/s-1", [new SlimPath("node_modules", SlimStates.Pending)])
            .Write(SlimManifest.PathFor(folder));
        return folder;
    }

    /// <summary>A downloaded version nothing pins, its folder made at <paramref name="at"/>.</summary>
    private string Version(string version, DateTimeOffset at)
    {
        var folder = HarnessSettings.ManagedHome(_home, "claude-code", version);
        Write(Path.Combine(folder, "bin", "claude.exe"), 300);
        Settle(folder, at);
        return folder;
    }

    private void Caches()
    {
        Write(Path.Combine(Engine, "Default", "Cache", "data_0"), 70);
        Write(Path.Combine(Engine, "GrShaderCache", "data_1"), 20);
        Write(Path.Combine(Engine, "Default", "Cookies"), 5);
        Write(Path.Combine(Engine, "Default", "Preferences"), 4);
    }

    private sealed class Browser(bool running) : IBrowserCaches
    {
        public int Asked { get; private set; }

        public Task<T?> WhileStoppedAsync<T>(Func<T> clear, CancellationToken ct) where T : class
        {
            Asked++;
            return Task.FromResult(running ? null : clear());
        }
    }

    /// <summary>The machine log's lines of an event, in order.</summary>
    private IReadOnlyList<string> Logged(string evt)
    {
        var logs = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(logs)) return [];
        return [.. Directory.EnumerateFiles(logs, "*.jsonl").Order(StringComparer.Ordinal).SelectMany(Lines)
            .Where(line => line.Contains($"\"event\":\"{evt}\"", StringComparison.Ordinal))];

        // The log holds its day's file open as it writes, sharing it with readers.
        static IEnumerable<string> Lines(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    /// <summary>§3.2: the default is the smaller of 10% of the volume and 20 GB; a size is at most half of each volume; the margin the smaller of 5 GB and 5%.</summary>
    [Fact]
    public void Each_volumes_floor_is_its_own_by_the_rule()
    {
        Assert.Equal(20 * Gb, FloorPolicy.Default.Of(1024 * Gb));
        Assert.Equal(128 * Gb / 100 * 10, FloorPolicy.Default.Of(128 * Gb));
        Assert.Equal(30 * Gb, new FloorPolicy(30, null, false).Of(100 * Gb));
        Assert.Equal(20 * Gb, new FloorPolicy(30, null, false).Of(40 * Gb));
        Assert.Equal(100 * Gb / 100 * 25, new FloorPolicy(null, 25, false).Of(100 * Gb));
        Assert.Null(FloorPolicy.None.Of(100 * Gb));
        Assert.Equal(5 * Gb, FloorPolicy.Margin(1024 * Gb));
        Assert.Equal(40 * Gb / 100 * 5, FloorPolicy.Margin(40 * Gb));
        Assert.Equal(["default", "size", "percent", "off"], new[] { FloorPolicy.Default, new(3, null, false), new(null, 3, false), FloorPolicy.None }.Select(each => each.From));
    }

    /// <summary>§2.2, §3.2: a floor out of its bounds is refused, nothing written; within them it is written under <c>keep.floor</c> and read back.</summary>
    [Theory]
    [InlineData("0GB")]
    [InlineData("51GB")]
    [InlineData("0%")]
    [InlineData("51%")]
    public void A_floor_out_of_its_bounds_is_refused(string word)
    {
        var set = KeepRules.Asked("floor", word) with { Volume = 100 * Gb };

        var refused = Assert.IsType<ChoiceRefusal>(Assert.ThrowsAny<DriverException>(() => KeepRules.Apply(DriverConfig.Empty, set)));

        Assert.Equal((ChoiceRefusal.KeepOutOfRange, ChoiceCodes.KeepOutOfRange), (refused.Check, ChoiceCodes.Of(refused)));
        Assert.Contains("the floor takes", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("50GB", "{\"gb\":50}", "size")]
    [InlineData("12%", "{\"percent\":12}", "percent")]
    [InlineData("off", "{\"off\":true}", "off")]
    public void A_floor_within_its_bounds_is_written_and_read_back(string word, string written, string from)
    {
        var path = Path.Combine(_home, "driver.json");
        DriverConfig.Empty.WithKeep(KeepRules.Asked("floor", word) with { Volume = 100 * Gb }).Save(path);

        var keep = JsonNode.Parse(File.ReadAllText(path))!["keep"]!["floor"]!.ToJsonString();
        Assert.Equal(written, keep);
        Assert.Equal(from, KeepPolicy.From(DriverConfig.Load(path).Keep).Floor.From);

        DriverConfig.Load(path).WithKeep(KeepRules.Asked("floor", "default")).Save(path);
        Assert.Null(JsonNode.Parse(File.ReadAllText(path))!["keep"]);
    }

    /// <summary>Above the floor, nothing runs: no run lock taken, nothing removed, nothing said.</summary>
    [Fact]
    public async Task Above_the_floor_nothing_runs()
    {
        var folder = Aside("s-1", Moved);
        Version("2.0.1", Moved.AddDays(-3));
        Caches();
        _free["A"] = 21 * Gb;

        var pass = await SpaceFloor.PassAsync(World(new Browser(running: false)));

        Assert.Empty(pass.Acts);
        Assert.Empty(pass.Low);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules")));
        Assert.True(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
        Assert.False(File.Exists(KeepingRun.PathOf(_home)));
        Assert.Empty(Logged("space.low"));
    }

    /// <summary>
    /// 🔴 §3.2: below the floor the three acts run at once, in order, and nothing else, each with its own guards: the slim due goes and one
    /// within its hour stays; an unused download goes and one downloaded within the hour stays (<c>recent</c>); the browser's caches go by
    /// the table's paths while it does not run, its cookies and preferences kept; a folder past its days, the machine log, stay their rules'.
    /// </summary>
    [Fact]
    public async Task Below_the_floor_the_three_acts_run_in_order_and_nothing_else()
    {
        var due = Aside("s-due", Moved);
        var fresh = Aside("s-fresh", _now.AddMinutes(-30));
        var past = Aside("s-past", Moved.AddDays(-20));
        var unused = Version("2.0.1", Moved.AddDays(-3));
        var recent = Version("2.0.2", _now.AddMinutes(-10));
        Caches();
        var log = Write(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-08-01.driver.jsonl"), 50);
        _free["A"] = 9 * Gb;
        var browser = new Browser(running: false);

        var pass = await SpaceFloor.PassAsync(World(browser));

        Assert.Equal(["set-aside", "downloads", "browser-cache"], pass.Acts.Select(act => act.Kind));
        Assert.False(Directory.Exists(Path.Combine(due, "node_modules")));
        Assert.True(File.Exists(Path.Combine(due, "README.md")));
        Assert.True(Directory.Exists(Path.Combine(fresh, "node_modules")));
        Assert.True(Directory.Exists(past), "the purge is the look's at its days, never the floor's");
        Assert.False(Directory.Exists(unused));
        Assert.True(Directory.Exists(recent));
        Assert.False(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
        Assert.False(Directory.Exists(Path.Combine(Engine, "GrShaderCache")));
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cookies")));
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Preferences")));
        Assert.True(File.Exists(log));
        Assert.Equal(1, browser.Asked);
        var record = KeepingRecord.Read(_home);
        Assert.Equal(KeepingRun.Floor, record.Last["downloads"].Door);
        Assert.Contains(Logged("space.freed"), line => line.Contains("\"door\":\"floor\"", StringComparison.Ordinal) && line.Contains("\"early\":true", StringComparison.Ordinal));
    }

    /// <summary>🔴 §3.2 (Codex 3.1): the person's <c>never</c> is the press alone, below the floor too.</summary>
    [Fact]
    public async Task A_never_download_is_kept_below_the_floor()
    {
        var unused = Version("2.0.1", Moved.AddDays(-30));
        _free["A"] = 1 * Gb;
        var policy = KeepPolicy.From(KeepChoices.None with { DownloadsNever = true });

        var pass = await SpaceFloor.PassAsync(World(policy: policy));

        Assert.True(Directory.Exists(unused));
        var downloads = Assert.Single(pass.Acts, act => act.Kind == "downloads");
        Assert.Equal((0, 1), (downloads.Removed, downloads.Kept["never"]));
    }

    /// <summary>🔴 §3.2, §3.5: a running browser's caches are kept, and with no Daoris to say whether it runs, they are kept too.</summary>
    [Fact]
    public async Task A_running_browsers_caches_are_kept_and_with_no_shell_too()
    {
        Caches();
        _free["A"] = 1 * Gb;

        var running = await SpaceFloor.PassAsync(World(new Browser(running: true)));
        var shellless = await SpaceFloor.PassAsync(World(caches: null, floors: new SpaceFloors()));

        Assert.True(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
        Assert.Equal(1, Assert.Single(running.Acts, act => act.Kind == "browser-cache").Kept[SpaceKept.Running]);
        Assert.Equal(1, Assert.Single(shellless.Acts, act => act.Kind == "browser-cache").Kept[SpaceKept.NoShell]);
    }

    /// <summary>
    /// 🔴 §3.2 (Fable F4.1): the browser's caches are cleared once in each stretch below the floor, not an hour later though the acts run
    /// again; and again once the stretch ended at <c>space.ok</c> and a new one began.
    /// </summary>
    [Fact]
    public async Task The_caches_are_cleared_once_a_stretch_and_again_after_space_ok()
    {
        Caches();
        _free["A"] = 1 * Gb;
        var browser = new Browser(running: false);

        await SpaceFloor.PassAsync(World(browser));
        Caches();
        _now = _now.AddMinutes(30);
        var within = await SpaceFloor.PassAsync(World(browser));
        _now = _now.AddHours(1);
        var hourLater = await SpaceFloor.PassAsync(World(browser));

        Assert.Empty(within.Acts);
        Assert.Contains(hourLater.Acts, act => act.Kind == "set-aside" || act.Kind == "downloads");
        Assert.DoesNotContain(hourLater.Acts, act => act.Kind == "browser-cache");
        Assert.Equal(1, browser.Asked);
        Assert.True(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));

        _free["A"] = 20 * Gb;
        _now = _now.AddMinutes(5);
        var ok = await SpaceFloor.PassAsync(World(browser));
        _free["A"] = 2 * Gb;
        _now = _now.AddMinutes(5);
        var again = await SpaceFloor.PassAsync(World(browser));

        Assert.Single(ok.Ok);
        Assert.Contains(again.Acts, act => act.Kind == "browser-cache");
        Assert.Equal(2, browser.Asked);
        Assert.False(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
    }

    /// <summary>
    /// 🔴 §3.2: said once each time the free space falls below (<c>space.low</c>, the stretch said), never again while it stays; still
    /// said above the floor within the margin; cleared once above the floor by the smaller of 5 GB and 5% (<c>space.ok</c>).
    /// </summary>
    [Fact]
    public async Task Said_once_and_cleared_at_the_margin()
    {
        _free["A"] = 4 * Gb;

        var first = await SpaceFloor.PassAsync(World());
        _now = _now.AddHours(2);
        var second = await SpaceFloor.PassAsync(World());
        _free["A"] = 10 * Gb + 5 * Gb - 1;
        var withinMargin = await SpaceFloor.PassAsync(World());

        var low = Assert.Single(first.Low);
        Assert.Equal((4 * Gb, 3 * Gb), (low.Read!.Read.Free, low.Could));
        Assert.Empty(second.Low);
        Assert.Empty(withinMargin.Ok);
        Assert.Single(Logged("space.low"));
        Assert.Single(_floors.Said());

        _free["A"] = 15 * Gb;
        var cleared = await SpaceFloor.PassAsync(World());

        Assert.Single(cleared.Ok);
        Assert.Single(Logged("space.ok"));
        Assert.Empty(_floors.Said());
        Assert.Contains("\"floor\":10737418240", Logged("space.ok")[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 §3.2 (Fable F4.3): a trees home on another volume (as through a junction) is judged by its own volume's read: there the slim runs,
    /// while the Daoris folder's volume, above its floor, runs none of its acts; the card says that volume on the trees' rows.
    /// </summary>
    [Fact]
    public async Task A_trees_home_on_another_volume_is_judged_by_its_own()
    {
        _treesOnB = true;
        var due = Aside("s-due", Moved);
        var unused = Version("2.0.1", Moved.AddDays(-3));
        Caches();
        _free["A"] = 60 * Gb;
        _free["B"] = 2 * Gb;

        var pass = await SpaceFloor.PassAsync(World(new Browser(running: false)));

        Assert.Equal(["set-aside"], pass.Acts.Select(act => act.Kind));
        Assert.False(Directory.Exists(Path.Combine(due, "node_modules")));
        Assert.True(Directory.Exists(unused));
        Assert.True(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
        Assert.Equal(@"D:\", Assert.Single(pass.Low).Read!.Read.Root);

        var space = SystemSpace.Read(new SpaceWorld(_home)
        {
            Sizes = new SpaceSizes(), Now = () => _now, Volume = _ => (60 * Gb, 100 * Gb), Reading = Volume, Floors = _floors,
        });
        Assert.False(space["volume"]!["below"]!.GetValue<bool>());
        var trees = space["kinds"]!.AsArray().Single(kind => kind!["id"]!.GetValue<string>() == "set-aside")!;
        Assert.Equal(@"D:\", trees["volume"]!["path"]!.GetValue<string>());
        Assert.True(trees["volume"]!["below"]!.GetValue<bool>());
        Assert.Equal(10 * Gb, trees["volume"]!["floor"]!["bytes"]!.GetValue<long>());
        Assert.Null(space["kinds"]!.AsArray().Single(kind => kind!["id"]!.GetValue<string>() == "downloads")!["volume"]);
    }

    /// <summary>
    /// §3.1, §3.2: the look's Keeping pass runs the floor at its pace: below it, the caches cleared and the volume said once in the report,
    /// with what the press could free; a look later says nothing more; above the floor by the margin, said clear.
    /// </summary>
    [Fact]
    public async Task The_look_s_pass_runs_the_floor_and_says_it_once()
    {
        Caches();
        _free["A"] = 4 * Gb;
        using var ledger = new StandInLedger();
        var tidying = new BranchTidying(_home)
        {
            Pace = TimeSpan.Zero, Clock = () => _now, Volume = Volume, Caches = new Browser(running: false), Floors = _floors,
        };
        using var log = new MachineLog(_home, "driver", () => _now);

        var said = await tidying.LookAsync(ledger.Client(), log, CancellationToken.None);
        _now = _now.AddMinutes(5);
        var again = await tidying.LookAsync(ledger.Client(), log, CancellationToken.None);
        _free["A"] = 16 * Gb;
        _now = _now.AddMinutes(5);
        var ok = await tidying.LookAsync(ledger.Client(), log, CancellationToken.None);

        Assert.Contains(said, line => line.StartsWith("tidy  the free space is below its floor: removed 2 of the browser's cache early (", StringComparison.Ordinal));
        Assert.Contains(said, line => line.StartsWith(@"tidy  C:\ has 4096.0 MB free, below its floor of 10240.0 MB: Daoris freed ", StringComparison.Ordinal)
                                      && line.Contains("Free up space… on This Daoris", StringComparison.Ordinal));
        Assert.Empty(again);
        Assert.Equal([@"tidy  C:\ has 16384.0 MB free again, above its floor of 10240.0 MB."], ok);
        Assert.False(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
    }

    /// <summary>🔴 §6 point 7: a free-space read that fails runs no floor act, says nothing, and takes no lock.</summary>
    [Fact]
    public async Task A_failed_read_runs_nothing()
    {
        var due = Aside("s-due", Moved);
        _unreadable = true;

        var pass = await SpaceFloor.PassAsync(World(new Browser(running: false)));

        Assert.Empty(pass.Acts);
        Assert.Empty(pass.Low);
        Assert.Equal(SpaceFloor.Entries, pass.Unread);
        Assert.True(Directory.Exists(Path.Combine(due, "node_modules")));
        Assert.False(File.Exists(KeepingRun.PathOf(_home)));
    }

    /// <summary>§3.1: another run holding the home's run, the floor's acts wait for a later pass, and the stretch is not said yet.</summary>
    [Fact]
    public async Task A_held_run_lock_runs_no_act_and_says_nothing_yet()
    {
        var due = Aside("s-due", Moved);
        _free["A"] = 1 * Gb;
        using (KeepingRun.TryTake(_home, KeepingRun.Press))
        {
            var held = await SpaceFloor.PassAsync(World());

            Assert.True(held.WasHeld);
            Assert.Empty(held.Low);
            Assert.True(Directory.Exists(Path.Combine(due, "node_modules")));
        }

        var after = await SpaceFloor.PassAsync(World());
        Assert.False(Directory.Exists(Path.Combine(due, "node_modules")));
        Assert.Single(after.Low);
    }

    /// <summary>The floor <c>off</c>: nothing runs or is said, and a stretch said before is forgotten.</summary>
    [Fact]
    public async Task With_the_floor_off_nothing_runs_and_a_stretch_is_forgotten()
    {
        _free["A"] = 1 * Gb;
        await SpaceFloor.PassAsync(World());
        Assert.Single(_floors.Said());

        var off = await SpaceFloor.PassAsync(World(policy: KeepPolicy.From(KeepChoices.None with { FloorOff = true })));

        Assert.Empty(off.Acts);
        Assert.Empty(_floors.Said());
    }

    /// <summary>
    /// The system's read (§3.2, §10): a folder is read by its volume's identity, the same for a folder and one not made yet beneath it, and
    /// a folder reached through a junction answers for the volume its target lies on. A volume mounted at a folder is read the same way by
    /// the system's contract (<c>GetFinalPathNameByHandleW</c> to the volume's GUID), not mounted here.
    /// </summary>
    [Fact]
    public void The_systems_read_names_the_volume_holding_the_folder()
    {
        var read = SpaceVolumes.Read(_home);
        var deeper = SpaceVolumes.Read(Path.Combine(_home, "not", "made", "yet"));

        Assert.NotNull(read);
        Assert.Equal(read!.Id, deeper!.Id);
        Assert.True(read.Total > 0 && read.Free >= 0 && read.Free <= read.Total);
        if (!OperatingSystem.IsWindows()) return;
        Assert.StartsWith(@"\\?\Volume{", read.Id, StringComparison.OrdinalIgnoreCase);

        var target = Directory.CreateDirectory(Path.Combine(_home, "target")).FullName;
        var junction = Path.Combine(_home, "junction");
        using var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{target}\"")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
        })!;
        mklink.WaitForExit();
        if (!Directory.Exists(junction)) return;
        Assert.Equal(SpaceVolumes.Read(target)!.Id, SpaceVolumes.Read(junction)!.Id);
    }
}
