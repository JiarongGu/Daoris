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

    private HomeEstablished? Establish(string install, string? legacy = null, string? already = null)
    {
        if (already is not null) _process[DaorisHome.Variable] = already;
        return InstallHome.Establish(
            install,
            name => _process.TryGetValue(name, out var value) ? value : null,
            (name, value) => _process[name] = value,
            () => _user,
            value => _user = value,
            legacy ?? Path.Combine(_root, "no-legacy"));
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
        Assert.Equal("{}", File.ReadAllText(Path.Combine(established.Home, "driver.json")));
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
        Assert.True(File.Exists(Path.Combine(established.Home, "driver.json")));
        Assert.True(Directory.Exists(Path.Combine(established.Home, "harnesses", "claude-code", "work")));
        Assert.False(File.Exists(Path.Combine(legacy, "driver.json")));
        Assert.True(File.Exists(Path.Combine(legacy, "bin", "daoris-knowledge.exe")));
        Assert.Contains("moved", established.Notice);

        // A second start moves nothing: the home has state, and the legacy tree is left as it is.
        File.WriteAllText(Path.Combine(legacy, "driver.json"), "{ \"later\": true }");
        _process.Clear();
        var again = Establish(install, legacy);
        Assert.Empty(again!.Moved);
        Assert.Equal("{}", File.ReadAllText(Path.Combine(established.Home, "driver.json")));
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
