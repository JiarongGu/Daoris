using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The installed application's own home (D63): `data/` beside the executable, made the Daoris home
/// for this process and everything it spawns, offered to the user's environment so a terminal meets
/// the same machine — and a `~/.daoris` from before the decision moved in, once.
/// </summary>
/// <remarks>
/// Every effect is injected — the process variable, the user variable, the legacy directory — so
/// these tests set nothing on the machine running them. The one thing they touch is a scratch tree.
/// </remarks>
public sealed class InstallHomeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-install-home-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string> _process = new(StringComparer.Ordinal);
    private string? _user;

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Install(bool marked = true)
    {
        var install = Path.Combine(_root, "install");
        Directory.CreateDirectory(install);
        if (marked) File.WriteAllText(Path.Combine(install, InstallHome.Marker), "# Daoris — installed desktop\n");
        return install;
    }

    private HomeEstablished? Establish(
        string install, string? legacy = null, string? already = null, TimeSpan? leaseWait = null, IReadOnlyList<int>? others = null)
    {
        if (already is not null) _process[DaorisHome.Variable] = already;
        return InstallHome.Establish(
            install,
            name => _process.TryGetValue(name, out var value) ? value : null,
            (name, value) => _process[name] = value,
            () => _user,
            value => _user = value,
            legacy ?? Path.Combine(_root, "no-legacy"),
            leaseWait,
            othersRunning: _ => others ?? []);
    }

    /// <summary>A file under the install, its folders made, holding its own name.</summary>
    private static string Lay(string install, string relative)
    {
        var path = Path.Combine([install, .. relative.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, relative);
        return path;
    }

    /// <summary>Every file under the install, by its path relative to it, `/`-separated, sorted.</summary>
    private static IReadOnlyList<string> Files(string install) =>
        [.. Directory.EnumerateFiles(install, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(install, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

    /// <summary>An install as a build from before HOMELAYOUT3 left it: every entry in its <c>data/</c>.</summary>
    private static readonly string[] OldLayout =
    [
        "data/driver.json", "data/keys.json", "data/remotes.json", "data/logs/2026-10-19.desktop.jsonl",
        "data/toolchain/claude-code/2.1.300/claude.exe", "data/tools/git/2.51.0/git.exe", "data/running.json",
        "data/locks/records/landings.lock", "data/spawn/s-1.mcp.json", "data/starts/s-1.json", "data/hooks/guard.json",
        "data/harnesses/.probing/claude-code/work.lock", "data/sessions/requests/s-1.ask.json", "data/sessions/s-1.pid",
        "data/browser/engine/Default/Preferences", "data/browser/edge/Default/Preferences", "data/browser/edge-session-cookies.bin",
        "data/browser/edge.json", "data/browser/settings.json", "data/pipe/2026-10-19.jsonl", "data/usage-read/.keep",
        "data/brainstorm-rooms/b1/g1/packet.json", "data/chromium/Default/Cookies", "data/config/window-state.json",
        "data/harnesses/claude-code/work/.claude.json", "data/trees/default/engine/s-1/README.md", "data/help/CLAUDE.md",
        "data/intake/default/AGENTS.md", "data/quests/q1/attachments/a.png", "data/bin/daoris-knowledge.exe",
        "data/sessions/s-1.jsonl",
    ];

    /// <summary>
    /// 🔴 HOMELAYOUT3 (the install layout design §7, D172 point 6): at a start with the swap at rest and nothing else of the folder
    /// running, each entry D172 moves is renamed once from its old place into its new, nothing else: the logs, the downloaded
    /// versions, the keys, the browsers' profiles, the window's memory as one, what a run holds. The person's records stay in
    /// <c>data/</c>; an account home, a tree, a room, a kept file and <c>bin/</c> are never moved; a pid stays with the sessions'
    /// folder it is read in. Nothing is deleted, and the account's <c>data/</c> spelling of the home moves it as well.
    /// </summary>
    [Fact]
    public void An_old_installs_entries_move_into_their_places_once_and_the_rest_stays()
    {
        var install = Install();
        foreach (var file in OldLayout) Lay(install, file);
        var own = Path.Combine(install, "data");

        var established = Inherited(install, own);

        Assert.NotNull(established);
        Assert.Equal(
            ["brainstorm-rooms", "browser-edge", "browser-engine", "edge-cookies", "edge-record", "hooks", "keys", "locks", "logs", "pipe",
             "probing", "remotes", "requests", "running", "spawn", "starts", "tool-versions", "toolchain", "usage-read", "window"],
            established!.Placed.Select(move => move.Entry).Order(StringComparer.Ordinal));
        Assert.Empty(established.NotPlaced);
        Assert.True(established.Worth);
        Assert.Contains("keys", established.Notice);
        var expected = OldLayout.Select(file => file
                .Replace("data/keys.json", "profiles/keys.json")
                .Replace("data/remotes.json", "profiles/remotes.json")
                .Replace("data/logs/", "logs/")
                .Replace("data/toolchain/", "cache/toolchain/")
                .Replace("data/tools/", "cache/tools/")
                .Replace("data/running.json", "run/running.json")
                .Replace("data/locks/", "run/locks/")
                .Replace("data/spawn/", "run/spawn/")
                .Replace("data/starts/", "run/starts/")
                .Replace("data/hooks/", "run/hooks/")
                .Replace("data/harnesses/.probing/", "run/probing/")
                .Replace("data/sessions/requests/", "run/requests/")
                .Replace("data/browser/engine/", "profiles/browser/engine/")
                .Replace("data/browser/edge/", "profiles/browser/edge/")
                .Replace("data/browser/edge-session-cookies.bin", "profiles/browser/edge-session-cookies.bin")
                .Replace("data/browser/edge.json", "run/edge.json")
                .Replace("data/pipe/", "run/pipe/")
                .Replace("data/usage-read/", "work/usage-read/")
                .Replace("data/brainstorm-rooms/", "work/brainstorm-rooms/")
                .Replace("data/chromium/", "profiles/window/chromium/")
                .Replace("data/config/", "profiles/window/config/"))
            .Append(InstallHome.Marker)
            // The lease the move held records the home's id, minted where it had none (HOMEID1).
            .Append("data/home.json")
            .Order(StringComparer.Ordinal);
        Assert.Equal(expected, Files(install));
        Assert.False(File.Exists(Path.Combine(install, "run", "home-move.json")), "the journal is gone once the move ends");

        // Each entry now reads at its new place, and a second start finds nothing left to move.
        Assert.Equal(Path.Combine(install, "profiles", "keys.json"), DaorisHome.Entry(own, HomeEntry.Keys));
        Assert.Equal(Path.Combine(install, "profiles", "window"), DaorisHome.Entry(own, HomeEntry.Window));
        Assert.Equal(Path.Combine(own, "harnesses", "claude-code", "work"), DaorisHome.Entry(own, HomeEntry.AgentHomes, "claude-code", "work"));
        Assert.Null(Inherited(install, own));
    }

    /// <summary>
    /// 🔴 The first start after a swap moves nothing (D139): until the new build confirmed and <c>update/previous/</c> is gone, a
    /// roll-back may still put the build before back, which reads only the old places. A swap under way is the same.
    /// </summary>
    [Theory]
    [InlineData("previous")]
    [InlineData("started")]
    [InlineData("confirmed")]
    public void A_start_with_a_swap_not_at_rest_moves_nothing(string how)
    {
        var install = Install();
        foreach (var file in OldLayout) Lay(install, file);
        var before = Files(install);
        if (how == "previous") Directory.CreateDirectory(Path.Combine(install, StagedBuild.Folder, StagedBuild.Previous));
        else StagedBuild.WriteJournal(install, new SwapRecord(how, "b1", "1.0.0", "abc", DateTimeOffset.UtcNow, []));

        var established = Inherited(install, Path.Combine(install, "data"));

        Assert.Empty(established?.Placed ?? []);
        Assert.Equal(before, Files(install).Where(file => !file.StartsWith("update/", StringComparison.Ordinal)).ToList());
    }

    /// <summary>
    /// 🔴 Nothing moves while another process of the folder runs (the design §7.1): a host adopted from an earlier start, a
    /// browser, a connector from <c>bin/</c> may hold a path it took once; the next start tries again. Nor does a home named for
    /// this start alone move: it is not this install's.
    /// </summary>
    [Fact]
    public void Nothing_moves_while_another_process_of_the_folder_runs_or_on_another_home()
    {
        var install = Install();
        foreach (var file in OldLayout) Lay(install, file);
        var before = Files(install);
        _user = Path.Combine(install, "data");

        var busy = Establish(install, already: _user, others: [4242]);

        Assert.Empty(busy!.Placed);
        Assert.Contains("4242", busy.PlaceHeld);
        Assert.False(busy.Worth);
        Assert.Equal(before, Files(install));

        _process.Clear();
        Assert.Null(Establish(install, already: Path.Combine(_root, "scratch-home")));
        Assert.Equal(before, Files(install));
    }

    /// <summary>
    /// 🔴 A crash between the window's two folders (the second opinion's journal): the move journals what it is about to rename,
    /// and the next start, before anything resolves, puts back what went into <c>profiles/window.moving/</c> where its old place
    /// is free, then moves the window's memory again, as one.
    /// </summary>
    [Fact]
    public void A_crash_between_the_windows_two_folders_is_put_back_and_moved_again_as_one()
    {
        var install = Install();
        Lay(install, "data/driver.json");
        Lay(install, "data/config/window-state.json");
        Lay(install, "data/chromium/Default/Cookies");
        // As a start that stopped after its first rename left it: chromium in the staging folder, config not yet, the journal.
        var moving = Path.Combine(install, "profiles", "window.moving");
        Directory.CreateDirectory(moving);
        Directory.Move(Path.Combine(install, "data", "chromium"), Path.Combine(moving, "chromium"));
        Directory.CreateDirectory(Path.Combine(install, "run"));
        File.WriteAllText(Path.Combine(install, "run", "home-move.json"), new System.Text.Json.Nodes.JsonObject
        {
            ["moves"] = new System.Text.Json.Nodes.JsonArray(
                new System.Text.Json.Nodes.JsonObject
                {
                    ["entry"] = "window", ["from"] = Path.Combine(install, "data", "chromium"), ["to"] = Path.Combine(moving, "chromium"),
                },
                new System.Text.Json.Nodes.JsonObject
                {
                    ["entry"] = "window", ["from"] = Path.Combine(install, "data", "config"), ["to"] = Path.Combine(moving, "config"),
                }),
        }.ToJsonString());

        var established = Inherited(install, Path.Combine(install, "data"));

        Assert.Contains("window", established!.Placed.Select(move => move.Entry));
        Assert.Equal(
            ["INSTALLED.md", "data/driver.json", "data/home.json", "profiles/window/chromium/Default/Cookies",
             "profiles/window/config/window-state.json"],
            Files(install).Where(file => !file.StartsWith("run/", StringComparison.Ordinal)).ToList());
        Assert.False(Directory.Exists(moving));
        Assert.False(File.Exists(Path.Combine(install, "run", "home-move.json")));
    }

    /// <summary>
    /// A held lease is never renamed under itself, and a live act holding the home moves nothing this start (HOMELEASE1): an older
    /// build's lease at the old place stays where it is, and the next start, the act ended, moves the rest.
    /// </summary>
    [Fact]
    public void A_live_lease_holds_the_move_off_and_is_never_moved()
    {
        var install = Install();
        foreach (var file in OldLayout) Lay(install, file);
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        var older = Path.Combine(install, "data", HomeLease.FileName);
        File.WriteAllText(older, new System.Text.Json.Nodes.JsonObject
        {
            ["kind"] = "verb", ["pid"] = Environment.ProcessId, ["started"] = self.StartTime.ToUniversalTime().Ticks,
            ["since"] = "2026-10-20T08:00:00+00:00", ["what"] = "daoris driver cap",
        }.ToJsonString());

        var held = Inherited(install, Path.Combine(install, "data"));

        Assert.Empty(held?.Placed ?? []);
        Assert.Contains("daoris driver cap", held!.PlaceHeld);
        Assert.True(File.Exists(older));

        File.WriteAllText(older, new System.Text.Json.Nodes.JsonObject
        {
            ["kind"] = "verb", ["pid"] = 0x7FFFFFFC, ["started"] = 1, ["since"] = "2026-10-20T08:00:00+00:00",
        }.ToJsonString());
        var moved = Inherited(install, Path.Combine(install, "data"));
        Assert.Contains("keys", moved!.Placed.Select(move => move.Entry));
        Assert.True(File.Exists(older), "a stale lease at the old place is left where it is, never moved");
    }

    /// <summary>
    /// The window's memory, the kit's data area (D172 point 4): an install's <c>data/</c> while its engine's profile or its
    /// geometry is still there, <c>profiles/window/</c> once moved or for a new install, and the kit's own for a root that is no
    /// install.
    /// </summary>
    [Fact]
    public void The_windows_memory_is_the_installs_window_entry_where_it_is()
    {
        var install = Install();
        Assert.Equal(Path.Combine(install, "profiles", "window"), InstallHome.WindowMemory(install));

        Lay(install, "data/config/window-state.json");
        Assert.Equal(Path.Combine(install, "data"), InstallHome.WindowMemory(install));

        Inherited(install, Path.Combine(install, "data"));
        Assert.Equal(Path.Combine(install, "profiles", "window"), InstallHome.WindowMemory(install));
        Assert.True(File.Exists(Path.Combine(install, "profiles", "window", "config", "window-state.json")));

        Assert.Null(InstallHome.WindowMemory(Path.Combine(_root, "bare")));
    }

    /// <summary>
    /// A development run's window memory (HOMELAYOUT4, the design §6.1): the run is told its one root with <c>--app-root</c>, a
    /// folder that is no install, and its window's memory is that root's <c>profiles/window/</c> all the same, never the kit's
    /// own <c>data/</c> beneath it.
    /// </summary>
    [Theory]
    [InlineData("--app-root", "X:/daoris/scratch")]
    [InlineData("--app-root=X:/daoris/scratch", null)]
    public void A_dev_runs_root_is_the_one_its_argument_names(string first, string? second)
    {
        string[] args = second is null ? ["--other", first] : ["--other", first, second];
        Assert.Equal(Path.GetFullPath("X:/daoris/scratch"), InstallHome.AppRootOf(args));
        Assert.Null(InstallHome.AppRootOf(["--other"]));
        Assert.Null(InstallHome.AppRootOf(["--app-root"]));
    }

    [Fact]
    public void A_dev_runs_window_memory_is_its_roots_window_entry()
    {
        var root = Path.Combine(_root, "scratch");
        Directory.CreateDirectory(root);
        Assert.Equal(Path.Combine("profiles", "window"), Path.GetRelativePath(root, InstallHome.WindowMemoryOfRun(root)));

        Lay(root, "chromium/Default/Preferences");
        Assert.Equal(root, InstallHome.WindowMemoryOfRun(root));
    }

    /// <summary>
    /// An entry whose new place is there too is not moved: both places hold it, which its readers refuse naming both (D172 point
    /// 5), and the notice says so; an empty folder at the new place is no entry, and the move takes its place.
    /// </summary>
    [Fact]
    public void An_entry_in_both_places_stays_and_is_said_and_an_empty_new_folder_is_taken()
    {
        var install = Install();
        Lay(install, "data/keys.json");
        Lay(install, "profiles/keys.json");
        Lay(install, "data/locks/records/r.lock");
        Directory.CreateDirectory(Path.Combine(install, "run", "locks"));

        var established = Inherited(install, Path.Combine(install, "data"));

        var keys = Assert.Single(established!.NotPlaced, move => move.Entry == "keys");
        Assert.Contains("both", keys.Reason);
        Assert.Contains("locks", established.Placed.Select(move => move.Entry));
        Assert.True(File.Exists(Path.Combine(install, "run", "locks", "records", "r.lock")));
        Assert.Contains("could not", established.Notice);
    }

    /// <summary>A workspace build is not an install: the dev loop names the home itself, and nothing here applies.</summary>
    [Fact]
    public void A_folder_without_the_publish_marker_is_left_alone()
    {
        Assert.Null(Establish(Install(marked: false)));
        Assert.Empty(_process);
        Assert.Null(_user);
    }

    /// <summary>
    /// A home named for this start alone is respected — a gate's scratch home above all. It is not the
    /// account's (HOME1, D105): the account has none, or names another folder.
    /// </summary>
    [Fact]
    public void A_home_named_for_this_start_alone_is_respected_and_nothing_is_set()
    {
        Assert.Null(Establish(Install(), already: Path.Combine(_root, "elsewhere")));
        Assert.Equal(Path.Combine(_root, "elsewhere"), _process[DaorisHome.Variable]);
        Assert.Null(_user);

        _user = Path.Combine(_root, "first", "data");
        Assert.Null(Establish(Install(), already: Path.Combine(_root, "scratch-home")));
        Assert.Equal(Path.Combine(_root, "scratch-home"), _process[DaorisHome.Variable]);
        Assert.Equal(Path.Combine(_root, "first", "data"), _user);
    }

    /// <summary>
    /// 🔴 HOME1 (D105): a second install started with the account's variable in its environment — the
    /// variable the first install set — runs on its OWN `data/`, and says so. The account's variable
    /// is left exactly as it was: it is the person's, and a start that rewrote it would move every
    /// terminal to whichever install was opened last.
    /// </summary>
    [Fact]
    public void A_second_install_runs_on_its_own_data_over_the_home_its_account_names_and_says_so()
    {
        var first = Path.Combine(_root, "first", "data");
        Directory.CreateDirectory(first);
        var install = Install();

        var established = Inherited(install, first);

        Assert.NotNull(established);
        Assert.Equal(Path.Combine(install, "data"), established.Home);
        Assert.True(Directory.Exists(established.Home));
        Assert.Equal(established.Home, _process[DaorisHome.Variable]);
        Assert.Equal(first, established.Overrode);
        Assert.Equal(first, _user);
        Assert.False(established.SetForUser);
        // Worth saying, so it rides the state and the one-time notice.
        Assert.True(established.Worth);
        Assert.Contains(established.Home, established.Notice);
        Assert.Contains(first, established.Notice);
        Assert.Contains("left as it is", established.Notice);
    }

    /// <summary>
    /// A moved install is the same case: the account's variable names the folder it moved from, which
    /// may no longer exist, and the install's own `data/` — which moved with it — is the home.
    /// </summary>
    [Fact]
    public void A_moved_install_runs_on_its_own_data_not_the_folder_it_moved_from()
    {
        var install = Install();
        Directory.CreateDirectory(Path.Combine(install, "data"));
        File.WriteAllText(Path.Combine(install, "data", "driver.json"), "{}");
        var before = Path.Combine(_root, "where-it-was", "data");

        var established = Inherited(install, before);

        Assert.Equal(Path.Combine(install, "data"), established!.Home);
        Assert.Equal(before, established.Overrode);
        Assert.Equal(before, _user);
        Assert.False(Directory.Exists(before));
        Assert.Equal("{}", File.ReadAllText(DaorisHome.Entry(established.Home, HomeEntry.DriverConfig)));
    }

    /// <summary>
    /// The first install's own starts, once the account names it: the variable already says this
    /// install's `data/` — spelled with a trailing separator, or on Windows in another case, as a
    /// person might have — and there is nothing to establish or to say.
    /// </summary>
    [Fact]
    public void An_install_its_account_already_names_establishes_nothing_and_says_nothing()
    {
        var install = Install();
        var own = Path.Combine(install, "data");

        Assert.Null(Inherited(install, own));
        Assert.Null(Inherited(install, own + Path.DirectorySeparatorChar));
        Assert.Equal(own + Path.DirectorySeparatorChar, _user);
        if (OperatingSystem.IsWindows()) Assert.Null(Inherited(install, own.ToUpperInvariant()));
    }

    /// <summary>
    /// The account's variable, in this process's environment too — what a start from the file manager
    /// or a terminal opened after the first install inherits.
    /// </summary>
    private HomeEstablished? Inherited(string install, string accountHome)
    {
        _user = accountHome;
        _process.Clear();
        return Establish(install, already: accountHome);
    }

    [Fact]
    public void An_install_makes_data_beside_the_executable_its_home_for_this_process_and_its_children()
    {
        var install = Install();

        var established = Establish(install);

        Assert.NotNull(established);
        Assert.Equal(Path.Combine(install, "data"), established.Home);
        Assert.True(Directory.Exists(established.Home));
        Assert.Equal(established.Home, _process[DaorisHome.Variable]);
    }

    /// <summary>
    /// 🔴 A variable, not a file: nothing of Daoris's goes under the user profile. Set once, when the
    /// user's environment has none, so a terminal's `daoris` and a session's MCP host meet the same
    /// machine — and never over one the person set themselves.
    /// </summary>
    [Fact]
    public void The_user_s_environment_gains_the_home_once_and_is_never_overwritten()
    {
        var install = Install();

        var first = Establish(install);
        Assert.True(first!.SetForUser);
        Assert.Equal(first.Home, _user);

        _user = "D:/somewhere/else";
        _process.Clear();
        var second = Establish(install);
        Assert.False(second!.SetForUser);
        Assert.Equal("D:/somewhere/else", _user);
    }

    /// <summary>
    /// A `~/.daoris` from before D63 moves in on the first start with an empty home — the state, and
    /// only the state: `bin/` is the CLI's own install and stays where the CLI put it.
    /// </summary>
    [Fact]
    public void A_legacy_profile_directory_s_state_moves_in_once_and_bin_stays()
    {
        var install = Install();
        var legacy = Path.Combine(_root, "legacy");
        Directory.CreateDirectory(Path.Combine(legacy, "harnesses", "claude-code", "work"));
        Directory.CreateDirectory(Path.Combine(legacy, "bin"));
        File.WriteAllText(Path.Combine(legacy, "driver.json"), "{}");
        File.WriteAllText(Path.Combine(legacy, "knowledge.db"), "db");
        File.WriteAllText(Path.Combine(legacy, "bin", "daoris-knowledge.exe"), "");

        var established = Establish(install, legacy);

        Assert.Equal(["driver.json", "harnesses", "knowledge.db"], established!.Moved.Order(StringComparer.Ordinal));
        Assert.True(File.Exists(DaorisHome.Entry(established.Home, HomeEntry.DriverConfig)));
        Assert.True(Directory.Exists(DaorisHome.Entry(established.Home, HomeEntry.AgentHomes, "claude-code", "work")));
        Assert.False(File.Exists(Path.Combine(legacy, "driver.json")));
        Assert.True(File.Exists(Path.Combine(legacy, "bin", "daoris-knowledge.exe")));
        Assert.Contains("moved", established.Notice);

        // A second start moves nothing: the home has state, and the legacy tree is left as it is.
        File.WriteAllText(Path.Combine(legacy, "driver.json"), "{ \"later\": true }");
        _process.Clear();
        var again = Establish(install, legacy);
        Assert.Empty(again!.Moved);
        Assert.Equal("{}", File.ReadAllText(DaorisHome.Entry(established.Home, HomeEntry.DriverConfig)));
    }

    /// <summary>
    /// D93: the shell runs from the install's <c>app/</c>, where the launcher at the root starts it, and
    /// the home is still the install's <c>data/</c> — never a <c>data/</c> inside <c>app/</c>.
    /// </summary>
    [Fact]
    public void A_shell_running_from_the_install_s_app_folder_makes_the_install_s_data_its_home()
    {
        var install = Install();
        var app = Path.Combine(install, "app");
        Directory.CreateDirectory(app);

        Assert.Equal(install, InstallHome.RootOf(app));
        var established = Establish(app);

        Assert.Equal(Path.Combine(install, "data"), established!.Home);
        Assert.False(Directory.Exists(Path.Combine(app, "data")));
    }

    /// <summary>An <c>app</c> folder is the install's only when the install is marked: a workspace's is not.</summary>
    [Fact]
    public void An_app_folder_with_no_marked_install_above_it_is_no_install()
    {
        var bare = Path.Combine(_root, "bare", "app");
        Directory.CreateDirectory(bare);

        Assert.Equal(bare, InstallHome.RootOf(bare));
        Assert.Null(Establish(bare));
    }

    /// <summary>
    /// APPCWD1 (D139 §5): a shell standing in its install's <c>app/</c>, or below it, stands in the install's root instead, so a
    /// child that names no working folder does not hold <c>app/</c>; a working folder elsewhere is left as it was.
    /// </summary>
    [Fact]
    public void A_shell_standing_in_the_install_s_app_folder_stands_in_the_install_s_root()
    {
        var install = Install();
        var app = Path.Combine(install, "app");
        Directory.CreateDirectory(Path.Combine(app, "host"));

        Assert.Equal(install, InstallHome.WorkingFolder(app, app));
        Assert.Equal(install, InstallHome.WorkingFolder(app + Path.DirectorySeparatorChar, app.ToUpperInvariant()));
        Assert.Equal(install, InstallHome.WorkingFolder(app, Path.Combine(app, "host")));
        Assert.Null(InstallHome.WorkingFolder(app, install));
        Assert.Null(InstallHome.WorkingFolder(app, _root));
        Assert.Null(InstallHome.WorkingFolder(app, app + "-other"));
    }

    /// <summary>APPCWD1: a workspace build, an install's root or an unmarked <c>app</c>, is no install's <c>app/</c>, and is left where it stands.</summary>
    [Fact]
    public void A_shell_outside_an_install_s_app_folder_is_left_where_it_stands()
    {
        var install = Install();
        var bare = Path.Combine(_root, "bare", "app");
        Directory.CreateDirectory(bare);

        Assert.Null(InstallHome.WorkingFolder(install, install));
        Assert.Null(InstallHome.WorkingFolder(bare, bare));
    }

    /// <summary>
    /// APPCWD1, held on the sources because nothing else can hold them: the application moves out of <c>app/</c> before it starts
    /// anything, and the launcher starts it in the install's root.
    /// </summary>
    [Fact]
    public void The_application_leaves_app_before_it_starts_anything_and_the_launcher_starts_it_outside_it()
    {
        var desktop = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");
        var program = File.ReadAllText(Path.Combine(desktop, "Daoris.Desktop.App", "Program.cs"));
        var main = program[program.IndexOf("static void Main(", StringComparison.Ordinal)..];
        var moved = main.IndexOf("InstallHome.WorkingFolder(AppContext.BaseDirectory, Environment.CurrentDirectory)", StringComparison.Ordinal);
        Assert.True(moved > 0, "Main does not leave app/");
        foreach (var later in new[] { "BrowserProcess.Run(", "InstallHome.Establish(", "CreateBuilder(" })
        {
            Assert.True(main.IndexOf(later, StringComparison.Ordinal) > moved, $"`{later}` runs before Main has left app/");
        }

        var launcher = File.ReadAllText(Path.Combine(desktop, "Daoris.Desktop.Launcher", "Program.cs"));
        Assert.Contains("WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(shell)!)!,", launcher);
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>The engine's profile is written before Daoris has any state, and does not stop a legacy move.</summary>
    [Fact]
    public void The_engine_s_own_profile_is_not_state()
    {
        var install = Install();
        Directory.CreateDirectory(Path.Combine(install, "data", "chromium"));
        var legacy = Path.Combine(_root, "legacy");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "driver.json"), "{}");

        Assert.Equal(["driver.json"], Establish(install, legacy)!.Moved);
    }

    /// <summary>
    /// HOMELEASE1: a verb the install carries may have run before the first start, minting the home's id or leaving a lease of
    /// its own; neither is state, and neither stops the move. The move leaves no lease behind.
    /// </summary>
    [Fact]
    public void The_homes_id_and_lease_are_not_state()
    {
        var install = Install();
        var data = Path.Combine(install, "data");
        HomeIdentity.Ensure(data);
        Directory.CreateDirectory(Path.GetDirectoryName(HomeLease.PathIn(data))!);
        File.WriteAllText(HomeLease.PathIn(data), "{ \"kind\": \"verb\", \"pid\": 2147483644, \"started\": 1 }");
        var legacy = Path.Combine(_root, "legacy");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "driver.json"), "{}");

        Assert.Equal(["driver.json"], Establish(install, legacy)!.Moved);
        Assert.Null(HomeLease.HeldBy(data));
        Assert.False(File.Exists(HomeLease.PathIn(data)));
    }

    /// <summary>
    /// 🔴 HOMELEASE1: the launcher's swap of <c>app/</c> holds the install's home's lease (the terminal RPC design §5, D139 §5): it
    /// compiles the lease and the home's id in, as it does the swap, and takes the lease as a swap before the swap's first move,
    /// replacing nothing while an act holds it. Read off its source, as <c>TaskbarIdentityTests</c> reads its names: the
    /// launcher's <c>Main</c> starts processes.
    /// </summary>
    [Fact]
    public void The_launchers_swap_holds_the_installs_home_lease()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        var launcher = Path.Combine(directory!.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Launcher");

        var project = File.ReadAllText(Path.Combine(launcher, "Daoris.Desktop.Launcher.csproj"));
        Assert.Contains(@"Include=""..\Daoris.Desktop.Driver\Platform\HomeLease.cs""", project);
        Assert.Contains(@"Include=""..\Daoris.Desktop.Driver\Platform\HomeIdentity.cs""", project);
        // The home's table names the lease's place and the id's (HOMELAYOUT2), so the launcher compiles it too.
        Assert.Contains(@"Include=""..\Daoris.Desktop.Driver\Platform\DaorisHome.cs""", project);

        var program = File.ReadAllText(Path.Combine(launcher, "Program.cs"));
        var leased = program.IndexOf("HomeLease.Await(", StringComparison.Ordinal);
        Assert.True(leased > 0, "the launcher takes no lease");
        Assert.Contains($"Path.Combine(root, \"{InstallHome.Folder}\"), LeaseKind.Swap", program[leased..]);
        Assert.True(leased < program.IndexOf("swap.Run(rest)", StringComparison.Ordinal), "the swap runs before the lease is taken");
    }

    /// <summary>
    /// 🔴 HOMELEASE1: the first-start move holds the home's lease while it moves, so it never moves files under a verb acting
    /// alone. A verb holding the home for the whole of its wait: nothing moves, the older home stays where it was, and the notice
    /// says which act held it; the next start moves it.
    /// </summary>
    [Fact]
    public void The_move_waits_on_a_verbs_hold_and_moves_nothing_under_it()
    {
        var install = Install();
        var data = Path.Combine(install, "data");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(Path.GetDirectoryName(HomeLease.PathIn(data))!);
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        File.WriteAllText(HomeLease.PathIn(data), new System.Text.Json.Nodes.JsonObject
        {
            ["kind"] = "verb", ["pid"] = Environment.ProcessId, ["started"] = self.StartTime.ToUniversalTime().Ticks,
            ["since"] = "2026-10-16T08:00:00+00:00", ["what"] = "daoris-driver trees sync",
        }.ToJsonString());
        var legacy = Path.Combine(_root, "legacy");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "driver.json"), "{}");

        var established = Establish(install, legacy, leaseWait: TimeSpan.FromMilliseconds(300));

        Assert.Empty(established!.Moved);
        Assert.True(established.Worth);
        Assert.True(File.Exists(Path.Combine(legacy, "driver.json")));
        Assert.Contains("could not move anything, since a terminal's `daoris-driver trees sync` (pid", established.Notice);
        Assert.Contains($"held this home; the next start moves it; still at {legacy}", established.Notice);

        // The verb ends; the next start moves it.
        File.Delete(HomeLease.PathIn(data));
        _process.Clear();
        Assert.Equal(["driver.json"], Establish(install, legacy)!.Moved);
    }

    /// <summary>
    /// How the home stands to the account's DAORIS_HOME (LEFT2), which is the folder a terminal's daoris reads: the
    /// page's hint says a terminal reads this folder only when it is the same. Each start D105 names, judged after it
    /// established, with the account's variable as it then stands.
    /// </summary>
    [Fact]
    public void The_home_stands_to_the_account_as_the_same_folder_overridden_or_named_for_this_start_alone()
    {
        var install = Install();
        var own = Path.Combine(install, "data");

        // Set for the account on this start: a terminal reads it from now on.
        var set = Establish(install);
        Assert.Equal(HomeAccount.Same, InstallHome.AccountOf(_process[DaorisHome.Variable], set, _user));

        // The account already names it, in a spelling of its own: nothing established, and still the same folder.
        Assert.Null(Inherited(install, own + Path.DirectorySeparatorChar));
        Assert.Equal(HomeAccount.Same, InstallHome.AccountOf(_process[DaorisHome.Variable], null, _user));

        // Inherited from an account that names another install's home: overridden, and the notice says which.
        var overrode = Inherited(install, Path.Combine(_root, "first", "data"));
        Assert.Equal(HomeAccount.Overridden, InstallHome.AccountOf(_process[DaorisHome.Variable], overrode, _user));

        // 🔴 Named for this start alone: D105 respects it and writes no notice, and a terminal reads the account's.
        _process.Clear();
        Assert.Null(Establish(install, already: Path.Combine(_root, "scratch-home")));
        Assert.Equal(HomeAccount.ThisStart, InstallHome.AccountOf(_process[DaorisHome.Variable], null, _user));
        // …and where the account names none, a terminal reads no home at all, which is not this one either.
        Assert.Equal(HomeAccount.ThisStart, InstallHome.AccountOf(_process[DaorisHome.Variable], null, account: null));
    }

    [Fact]
    public void No_legacy_directory_is_nothing_to_move_and_the_notice_says_only_where_the_home_is()
    {
        var established = Establish(Install());

        Assert.Empty(established!.Moved);
        Assert.DoesNotContain("moved", established.Notice);
        Assert.Contains(established.Home, established.Notice);
        // Nothing inherited, so nothing overridden and nothing said about it.
        Assert.Null(established.Overrode);
        Assert.DoesNotContain("left as it is", established.Notice);
    }
}
