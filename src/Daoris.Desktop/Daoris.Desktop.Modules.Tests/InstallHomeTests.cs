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

    /// <summary>What the environment already says is respected — a gate's scratch home above all.</summary>
    [Fact]
    public void A_home_already_named_is_respected_and_nothing_is_set()
    {
        Assert.Null(Establish(Install(), already: Path.Combine(_root, "elsewhere")));
        Assert.Equal(Path.Combine(_root, "elsewhere"), _process[DaorisHome.Variable]);
        Assert.Null(_user);
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

    [Fact]
    public void No_legacy_directory_is_nothing_to_move_and_the_notice_says_only_where_the_home_is()
    {
        var established = Establish(Install());

        Assert.Empty(established!.Moved);
        Assert.DoesNotContain("moved", established.Notice);
        Assert.Contains(established.Home, established.Notice);
    }
}
