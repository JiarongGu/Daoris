using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEP1e (D195 point 4; the cleanup design §3.3, §4.4, §9): <i>Free up space…</i> and <c>daoris-driver space free</c>, listed first, then
/// pressed. A list that changes nothing; a press removing only the units listed, one changed since kept and counted; the browser's caches
/// only while the shell says its browser is not running, and kept with no Daoris; an earlier build's folder a program holds kept as in use;
/// a second press joining the run under way, and another holder refusing it; the terminal's exits by the run's end, alone and over the
/// pipe, confirmed and refused in the window, Daoris stopping mid-run said as not known, and a session's terminal refused alone. Files
/// only, in a home under <c>_fixtures/</c>.
/// </summary>
public sealed class SpaceFreeTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "space-free", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private InstanceStandIn? _standIn;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_home);
        SpaceFreeCommand.Every = TimeSpan.FromMilliseconds(20);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_standIn is not null) await _standIn.DisposeAsync();
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SpaceFreeWorld World(IBrowserCaches? caches = null, Action<string, string>? move = null) => new(_home)
    {
        Sizes = new SpaceSizes(),
        Now = () => Now,
        Caches = caches,
        Move = move,
        Judge = () => new DownloadJudge(() => DownloadUses.Read(_home, new HarnessSettings(), null), () => Now),
    };

    private static string Write(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private string Trees => DaorisHome.Entry(_home, HomeEntry.Trees);

    private string SetAside(string name, DateTimeOffset moved) =>
        Path.Combine(Trees, ".tidied", "games", "Daoris", $"{name}-{moved.UtcDateTime:yyyyMMdd'T'HHmmssfff'Z'}");

    /// <summary>Every entry under <paramref name="folder"/>, and it, made and written at <paramref name="at"/>: nothing written since its move.</summary>
    private static void Settle(string folder, DateTimeOffset at)
    {
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", SearchOption.AllDirectories).Append(new DirectoryInfo(folder)))
        {
            entry.CreationTimeUtc = at.UtcDateTime;
            entry.LastWriteTimeUtc = at.UtcDateTime;
        }
    }

    /// <summary>A folder set aside <paramref name="days"/> ago, nothing written in it since, with a file of a known size.</summary>
    private string Folder(string name, int days, int bytes = 100)
    {
        var folder = SetAside(name, Now.AddDays(-days));
        Write(Path.Combine(folder, "README.md"), bytes);
        Settle(folder, Now.AddDays(-days - 1));
        return folder;
    }

    private string Engine => DaorisHome.Entry(_home, HomeEntry.BrowserEngine);

    /// <summary>What an earlier build left, by its exact path as the card names it (no entry of the table holds it).</summary>
    private string LeftOver(string path) => SpaceKinds.Places(_home, "left-over").Places[SpaceKinds.EarlierBuild.ToList().IndexOf(path)];

    /// <summary>A home laid out with one unit of each kind and group: what the list names, and what it must leave.</summary>
    private void LayOut()
    {
        Folder("past", 20);
        Folder("early", 3);
        var own = Folder("own", 2);
        Write(Path.Combine(own, "local", "notes.md"), 7);
        var version = HarnessSettings.ManagedHome(_home, "claude-code", "2.0.1");
        Write(Path.Combine(version, "bin", "claude.exe"), 300);
        Settle(version, Now.AddDays(-2));
        Write(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-09-01.driver.jsonl"), 50);
        Write(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-21.driver.jsonl"), 60);
        Write(Path.Combine(Engine, "Default", "Cache", "data_0"), 70);
        Write(Path.Combine(Engine, "GrShaderCache", "data_1"), 20);
        Write(Path.Combine(Engine, "Default", "Cookies"), 5);
        Write(Path.Combine(Engine, "Default", "Preferences"), 4);
        Write(Path.Combine(LeftOver("webview2"), "EBWebView", "x"), 9);
        Write(Path.Combine(LeftOver("browser/profile"), "Cookies"), 11);
    }

    /// <summary>Every file under the home with its size and write time: what a list must leave as it was.</summary>
    private IReadOnlyList<string> Files() =>
        [.. Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(file => $"{Path.GetRelativePath(_home, file)}|{new FileInfo(file).Length}|{File.GetLastWriteTimeUtc(file):O}")];

    private static IReadOnlyList<SpaceAsked> Asked(SpacePlan plan, Func<SpaceUnit, bool> which) =>
        [.. plan.Units.Where(which).Select(unit => new SpaceAsked(unit.Kind, unit.Id, unit.Early, unit.Why, unit.Own == true))];

    private static async Task<SpaceRun> Ran(SpaceStart start)
    {
        var run = Assert.IsType<SpaceRun>(start.Run);
        await run.Finished.WaitAsync(TimeSpan.FromMinutes(2));
        return run;
    }

    private static SpaceRunKind KindOf(SpaceRun run, string id) => run.Kinds.Single(kind => kind.Id == id);

    /// <summary>§3.3 step 1, §6.5: the list names what each rule takes now and each group that could go early, and changes nothing.</summary>
    [Fact]
    public void The_list_names_each_rules_now_and_each_group_early_and_changes_nothing()
    {
        LayOut();
        var before = Files();

        var plan = SpaceFree.Plan(World());

        Assert.Equal(before, Files());
        Assert.False(File.Exists(KeepingRun.PathOf(_home)));
        var now = plan.Units.Where(unit => !unit.Early).Select(unit => $"{unit.Kind}:{unit.Id}:{unit.Why}").ToList();
        Assert.Contains($"set-aside:games/Daoris/{Path.GetFileName(SetAside("past", Now.AddDays(-20)))}:past", now);
        Assert.Contains("logs:2026-09-01.driver.jsonl:past", now);
        Assert.DoesNotContain(now, unit => unit.Contains("2026-10-21", StringComparison.Ordinal));
        var early = plan.Units.Where(unit => unit.Early).ToList();
        Assert.Equal(["set-aside", "downloads", "browser-cache", "left-over"], SpacePlan.Groups.Where(group => early.Any(unit => unit.Kind == group)));
        var folders = early.Where(unit => unit.Kind == "set-aside").ToList();
        Assert.Equal(2, folders.Count);
        Assert.True(folders.Single(unit => unit.Id.Contains("/own-", StringComparison.Ordinal)).Own);
        Assert.False(folders.Single(unit => unit.Id.Contains("/early-", StringComparison.Ordinal)).Own);
        Assert.Equal("Daoris", folders[0].Repository);
        Assert.Contains(early, unit => unit is { Kind: "downloads", Id: "agent/claude-code/2.0.1", Bytes: 300 });
        Assert.Equal(["Default/Cache", "GrShaderCache"], early.Where(unit => unit.Kind == "browser-cache").Select(unit => unit.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["browser/profile", "webview2"], early.Where(unit => unit.Kind == "left-over").Select(unit => unit.Id).Order(StringComparer.Ordinal));

        var json = SpaceFree.Json(plan);
        Assert.Equal(["history", "trees"], json["doors"]!.AsArray().Select(door => (string?)door));
        var group = json["early"]!.AsArray().Select(node => node!.AsObject()).Single(each => (string?)each["group"] == "set-aside");
        Assert.Equal(2, (int)group["count"]!);
        Assert.Equal(1, (int)group["own"]!);
    }

    /// <summary>§3.3 step 2: a press sends exactly the units listed, each judged again; one changed since is kept and counted.</summary>
    [Fact]
    public async Task A_press_removes_only_the_listed_units_and_keeps_one_changed_since_the_list()
    {
        LayOut();
        var world = World();
        var plan = SpaceFree.Plan(world);
        // After the list: a write in a folder the list said held nothing of its own, and a folder set aside the list never showed.
        var early = SetAside("early", Now.AddDays(-3));
        Write(Path.Combine(early, "late.txt"), 3);
        var unlisted = Folder("unlisted", 25);

        var asked = Asked(plan, unit => !unit.Early || unit.Kind == "set-aside");
        var run = await Ran(new SpaceRuns().Start(world, KeepingRun.Press, asked));

        Assert.False(Directory.Exists(SetAside("past", Now.AddDays(-20))));
        Assert.False(Directory.Exists(SetAside("own", Now.AddDays(-2))));
        Assert.True(Directory.Exists(early));
        Assert.True(Directory.Exists(unlisted));
        Assert.False(File.Exists(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-09-01.driver.jsonl")));
        Assert.True(File.Exists(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-21.driver.jsonl")));
        Assert.True(Directory.Exists(HarnessSettings.ManagedHome(_home, "claude-code", "2.0.1")));
        Assert.True(Directory.Exists(LeftOver("webview2")));

        var setAside = KindOf(run, "set-aside");
        Assert.Equal(("done", 2), (setAside.State, setAside.Removed));
        Assert.Equal(new Dictionary<string, int> { [SpaceKept.Changed] = 1 }, setAside.Kept);
        Assert.Equal(1, KindOf(run, "logs").Removed);
        Assert.Equal("kept", run.Outcome);
        var last = KeepingRecord.Read(_home).Last["set-aside"];
        Assert.Equal((KeepingRun.Press, run.Id, 2), (last.Door, last.Run, last.Removed));
        Assert.Null(KeepingRun.Holder(_home));
    }

    /// <summary>A browser host's stand-in: whether its browser runs, and how often it was asked.</summary>
    private sealed class Browser(bool running, Task? until = null) : IBrowserCaches
    {
        public int Asked { get; private set; }

        public async Task<T?> WhileStoppedAsync<T>(Func<T> clear, CancellationToken ct) where T : class
        {
            Asked++;
            if (until is not null) await until.WaitAsync(ct);
            return running ? null : clear();
        }
    }

    /// <summary>§3.2, §3.3: the caches go only while the shell says its browser is not running; with no Daoris to say, they stay.</summary>
    [Fact]
    public async Task The_browsers_caches_go_only_while_it_does_not_run_and_stay_with_no_Daoris()
    {
        LayOut();
        var caches = Asked(SpaceFree.Plan(World()), unit => unit.Kind == "browser-cache");

        var alone = await Ran(new SpaceRuns().Start(World(), KeepingRun.Terminal, caches));
        Assert.Equal(new Dictionary<string, int> { [SpaceKept.NoShell] = 2 }, KindOf(alone, "browser-cache").Kept);

        var runs = new Browser(running: true);
        var running = await Ran(new SpaceRuns().Start(World(runs), KeepingRun.Press, caches));
        Assert.Equal(1, runs.Asked);
        Assert.Equal(new Dictionary<string, int> { [SpaceKept.Running] = 2 }, KindOf(running, "browser-cache").Kept);
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cache", "data_0")));

        var stopped = await Ran(new SpaceRuns().Start(World(new Browser(running: false)), KeepingRun.Press, caches));
        Assert.Equal((2, 90L), (KindOf(stopped, "browser-cache").Removed, KindOf(stopped, "browser-cache").Bytes));
        Assert.False(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
        Assert.False(Directory.Exists(Path.Combine(Engine, "GrShaderCache")));
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cookies")));
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Preferences")));
        Assert.Equal("done", stopped.Outcome);
    }

    /// <summary>§3.2: a path not in the table is never touched, and a profile a browser still holds keeps every cache.</summary>
    [Fact]
    public void The_clear_takes_the_tables_paths_alone_and_none_while_the_profile_is_held()
    {
        LayOut();
        var cleared = BrowserCacheClearing.Clear(_home, ["Default/Cookies", "Default/../Default/Cache"]);
        Assert.Empty(cleared.Removed);
        Assert.Equal(SpaceKept.Refused, cleared.Kept["Default/Cookies"]);
        Assert.True(File.Exists(Path.Combine(Engine, "Default", "Cookies")));

        if (!OperatingSystem.IsWindows()) return;
        using (new FileStream(Write(Path.Combine(Engine, BrowserCacheClearing.ProfileLock), 0), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var held = BrowserCacheClearing.Clear(_home, ["Default/Cache"]);
            Assert.Equal(SpaceKept.Running, held.Kept["Default/Cache"]);
        }

        Assert.True(Directory.Exists(Path.Combine(Engine, "Default", "Cache")));
    }

    /// <summary>§3.5: an earlier build's folder a program holds keeps as in use, its rename refused; the other goes, its sign-ins with it.</summary>
    [Fact]
    public async Task An_earlier_builds_folder_a_program_holds_is_kept_as_in_use()
    {
        LayOut();
        var webview = LeftOver("webview2");
        void Move(string from, string to)
        {
            if (string.Equals(Path.GetFullPath(from), Path.GetFullPath(webview), StringComparison.OrdinalIgnoreCase)) throw new IOException("held");
            Directory.Move(from, to);
        }

        var world = World(move: Move);
        var run = await Ran(new SpaceRuns().Start(world, KeepingRun.Press, Asked(SpaceFree.Plan(world), unit => unit.Kind == "left-over")));

        Assert.True(Directory.Exists(webview));
        Assert.False(Directory.Exists(LeftOver("browser/profile")));
        var kind = KindOf(run, "left-over");
        Assert.Equal((1, 11L), (kind.Removed, kind.Bytes));
        Assert.Equal(new Dictionary<string, int> { [SpaceKept.InUse] = 1 }, kind.Kept);
    }

    /// <summary>§3.1, §3.3 step 3: a press while a run is under way here joins it; another holder of the run lock refuses one, named.</summary>
    [Fact]
    public async Task A_second_press_joins_the_run_under_way_and_another_holder_refuses_it()
    {
        LayOut();
        var release = new TaskCompletionSource();
        var world = World(new Browser(running: false, release.Task));
        var runs = new SpaceRuns();
        var caches = Asked(SpaceFree.Plan(world), unit => unit.Kind == "browser-cache");

        var first = runs.Start(world, KeepingRun.Press, caches);
        var second = runs.Start(world, KeepingRun.Press, caches);

        Assert.False(first.Joined);
        Assert.True(second.Joined);
        Assert.Same(first.Run, second.Run);
        Assert.Equal(KeepingRun.Press, KeepingRun.Holder(_home)!.Door);
        release.SetResult();
        await Ran(first);
        Assert.Same(first.Run, runs.Last(_home));
        Assert.Equal(first.Run!.Id, (string?)SystemSpace.Read(new SpaceWorld(_home) { Runs = runs, Sizes = new SpaceSizes() })["run"]!["id"]);

        using var look = KeepingRun.TryTake(_home, KeepingRun.Look);
        var refused = runs.Start(world, KeepingRun.Press, caches);
        Assert.Null(refused.Run);
        Assert.Equal(KeepingRun.Look, refused.Held!.Door);
    }

    private InstanceWorld Terminal(Func<string, string?>? environment = null) => new()
    {
        Home = _home,
        Environment = environment ?? (_ => null),
        // A workspace build reads no address file: alone unless the pipe is named.
        BaseDirectory = AppContext.BaseDirectory,
        Out = _out,
        Error = _error,
        Verb = "daoris-driver",
        Answer = TimeSpan.FromMinutes(2),
        Wait = TimeSpan.FromMinutes(2),
    };

    /// <summary>§4.4: without <c>--yes</c> the terminal lists and changes nothing, exit 0.</summary>
    [Fact]
    public async Task The_terminal_lists_and_changes_nothing_without_yes()
    {
        LayOut();
        var before = Files();

        var exit = await SpaceFreeCommand.RunAsync(["--early", "set-aside"], World(), Terminal(), CancellationToken.None, new SpaceRuns());

        Assert.Equal(0, exit);
        Assert.Equal(before, Files());
        var said = _out.ToString();
        Assert.Contains("what goes now, by the rules: 2 items", said);
        Assert.Contains("set-aside: 2 items", said);
        Assert.Contains("1 holds files of its own", said);
        Assert.Contains("daoris-driver history clear", said);
        Assert.Contains("nothing was removed", said);
    }

    /// <summary>§4.4: alone, exit 0 where every unit named went, 1 where one was kept, each kind said as it ends; a word it does not answer, 2.</summary>
    [Fact]
    public async Task The_terminal_alone_exits_by_the_runs_end()
    {
        LayOut();
        var done = await SpaceFreeCommand.RunAsync(["--yes", "--kind", "logs"], World(), Terminal(), CancellationToken.None, new SpaceRuns());
        Assert.Equal(0, done);
        Assert.Contains("  logs: removed 1 (50 bytes)", _out.ToString());
        Assert.Contains("done: every unit named was removed.", _out.ToString());
        Assert.Equal(KeepingRun.Terminal, KeepingRecord.Read(_home).Last["logs"].Door);

        void Refuse(string from, string to) => throw new IOException("held");
        var kept = await SpaceFreeCommand.RunAsync(["--yes", "--kind", "logs", "--early", "left-over"], World(move: Refuse), Terminal(),
            CancellationToken.None, new SpaceRuns());
        Assert.Equal(1, kept);
        Assert.Contains("left-over: removed 0 (0 bytes); kept 2 (in-use)", _out.ToString());

        Assert.Equal(2, await SpaceFreeCommand.RunAsync(["--early", "everything"], World(), Terminal(), CancellationToken.None, new SpaceRuns()));
        Assert.Contains(SpaceFreeCommand.Usage, _error.ToString());
    }

    /// <summary>§3.3 step 4, D194 point 5: from a session's environment the removal alone is refused, exit 1, and nothing is done.</summary>
    [Fact]
    public async Task A_sessions_terminal_is_refused_alone_and_nothing_is_done()
    {
        LayOut();
        var before = Files();

        var exit = await SpaceFreeCommand.RunAsync(["--yes"], World(), Terminal(name => name == DriverChoicesCommand.SessionVariable ? "s-1" : null),
            CancellationToken.None, new SpaceRuns());

        Assert.Equal(1, exit);
        Assert.Equal(before, Files());
        Assert.Contains("DAORIS_SESSION_ID", _error.ToString());
    }

    /// <summary>A running Daoris's stand-in, named by <see cref="InstanceDoor.Variable"/>, answering the press and the reads of its run.</summary>
    private InstanceWorld Served(Func<string, JsonElement, object?> respond, Action<InstanceStandIn>? set = null)
    {
        var id = HomeIdentity.Ensure(_home)!;
        var pipe = PipeName.For(id, Random.Shared.Next(100_000, 999_999));
        _standIn = new InstanceStandIn(pipe, id) { Respond = respond };
        set?.Invoke(_standIn);
        return Terminal(name => name == InstanceDoor.Variable ? pipe : null);
    }

    private static object Space(string? outcome) => new
    {
        run = new
        {
            id = "r1", door = "press", outcome,
            kinds = new[] { new { id = "logs", state = "done", removed = new { count = 1, bytes = 50L }, kept = new Dictionary<string, int>() } },
        },
    };

    /// <summary>§4.4: with Daoris running the verb is its client, confirmed in the window, then reads the run each second to its end.</summary>
    [Fact]
    public async Task A_terminals_press_is_confirmed_in_the_window_and_read_to_the_runs_end()
    {
        if (!OperatingSystem.IsWindows()) return;
        LayOut();
        var reads = 0;
        var world = Served((route, _) => route switch
        {
            "SYSTEM_SPACE_FREE" => new { run = "r1", joined = false },
            _ => Interlocked.Increment(ref reads) < 3 ? Space(null) : Space("done"),
        }, standIn => standIn.Confirming = true);

        var exit = await SpaceFreeCommand.RunAsync(["--yes", "--kind", "logs"], World(), world, CancellationToken.None, new SpaceRuns());

        Assert.Equal(0, exit);
        var press = _standIn!.Presses.First(each => each.GetProperty("type").GetString() == "SYSTEM_SPACE_FREE");
        Assert.Equal("logs", press.GetProperty("payload").GetProperty("units")[0].GetProperty("kind").GetString());
        Assert.Equal(1, _out.ToString().Split("  logs: removed 1").Length - 1);
        // Nothing was done here: the instance's run removes it.
        Assert.True(File.Exists(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-09-01.driver.jsonl")));
    }

    /// <summary>RPC1c: the person's refusal in the window is exit 1, nothing done here.</summary>
    [Fact]
    public async Task A_press_the_person_refused_in_the_window_is_exit_1()
    {
        if (!OperatingSystem.IsWindows()) return;
        LayOut();
        var world = Served((_, _) => null, standIn => (standIn.Confirming, standIn.Press, standIn.Code) = (true, StandInPress.Refuse, InstanceDoor.Refused));

        var exit = await SpaceFreeCommand.RunAsync(["--yes", "--kind", "logs"], World(), world, CancellationToken.None, new SpaceRuns());

        Assert.Equal(1, exit);
        Assert.True(File.Exists(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-09-01.driver.jsonl")));
    }

    /// <summary>§4.4: Daoris stopping while its run is under way is said as not known, exit 2.</summary>
    [Fact]
    public async Task Daoris_stopping_mid_run_is_said_as_not_known()
    {
        if (!OperatingSystem.IsWindows()) return;
        LayOut();
        InstanceWorld? world = null;
        world = Served((route, _) =>
        {
            if (route == "SYSTEM_SPACE_FREE") return new { run = "r1", joined = false };
            _standIn!.RefuseHello = "stopping";
            return Space(null);
        });

        var exit = await SpaceFreeCommand.RunAsync(["--yes", "--kind", "logs"], World(), world, CancellationToken.None, new SpaceRuns());

        Assert.Equal(2, exit);
        Assert.Contains("how it ended is not known", _error.ToString());
    }
}
