namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The second screen (D55 §b, SURF8): a named window the page asks for, opened on its own pump by
/// the shell.
/// </summary>
/// <remarks>
/// <para><b>The name is the part worth testing hardest.</b> It becomes a URL the window navigates to
/// and a filename the geometry store writes, so a name nobody checked would be a page choosing where
/// a window points and what it overwrites. Both derivations are asserted here, against the same
/// string the page's own <c>secondaryWindow()</c> parses back.</para>
///
/// <para>The window itself is WinForms and lives in the shell; what is here is the judgement —
/// which names exist, what each becomes, and what a person gets told when a name is not one.</para>
/// </remarks>
public sealed class WindowsModuleTests : Bridge
{
    private const string Service = "http://localhost:5177";

    /// <summary>The shell's half, stubbed: what the module asked for, without a message pump.</summary>
    private sealed class Windows : ISecondaryWindows
    {
        public readonly List<(string Name, string Address)> Asked = [];
        private readonly HashSet<string> _open = new(StringComparer.Ordinal);

        public bool Open(string name, string address)
        {
            Asked.Add((name, address));
            return _open.Add(name);
        }

        public IReadOnlyList<string> Opened => [.. _open.Order(StringComparer.Ordinal)];

        public readonly List<(string Name, bool Dark)> Themed = [];

        public bool SetTheme(string name, bool dark)
        {
            Themed.Add((name, dark));
            return _open.Contains(name);
        }
    }

    private readonly Windows _windows = new();

    /// <summary>What the main window was told to wear on its taskbar button (NOTIFY1b).</summary>
    private readonly List<(int Count, string? Description)> _badges = [];

    private WindowsModule Module() => new(Bus, _windows, new PlatformAddress(Service),
        badge: (count, description) => _badges.Add((count, description)));

    [Fact]
    public async Task The_monitor_opens_at_its_own_route_into_the_same_bundle()
    {
        var state = await AnswerAsync(Module(), "OPEN", new { name = "monitor" });

        // The same bytes a browser gets, with one parameter — not a second frontend (D55 §b).
        Assert.Equal(
            ("monitor", "/?window=monitor&host=http%3A%2F%2Flocalhost%3A5177"),
            _windows.Asked.Single());
        Assert.True(state.GetProperty("opened").GetBoolean());
        Assert.Equal("monitor", state.GetProperty("windows")[0].GetString());
    }

    [Fact]
    public async Task A_session_window_carries_the_session_in_its_name()
    {
        await AnswerAsync(Module(), "OPEN", new { name = "session:a1b2c3d4" });

        var (name, address) = _windows.Asked.Single();
        Assert.Equal("session:a1b2c3d4", name);
        Assert.Equal("/?window=session%3Aa1b2c3d4&host=http%3A%2F%2Flocalhost%3A5177", address);
    }

    /// <summary>
    /// A record fed from another machine is keyed `origin/id` (D47 §6). It has no console here and
    /// never will — but the name still has to survive the trip rather than become a different URL.
    /// </summary>
    [Fact]
    public async Task A_mirrored_session_id_is_escaped_rather_than_refused()
    {
        await AnswerAsync(Module(), "OPEN", new { name = "session:laptop/a1b2c3d4" });

        Assert.Equal(
            "/?window=session%3Alaptop%2Fa1b2c3d4&host=http%3A%2F%2Flocalhost%3A5177",
            _windows.Asked.Single().Address);
    }

    /// <summary>
    /// One window per name is the framework's contract, and it is what makes "open the monitor" a
    /// safe thing to press twice — the second press brings the window forward.
    /// </summary>
    [Fact]
    public async Task Opening_a_window_that_is_already_open_activates_it_rather_than_duplicating_it()
    {
        await AnswerAsync(Module(), "OPEN", new { name = "monitor" });
        var again = await AnswerAsync(Module(), "OPEN", new { name = "monitor" });

        Assert.False(again.GetProperty("opened").GetBoolean());
        Assert.Single(again.GetProperty("windows").EnumerateArray());
    }

    /// <summary>
    /// The name is a filename as well as a URL, so a name that walked out of the geometry directory
    /// would be a page choosing what the shell overwrites. It is refused, not sanitised.
    /// </summary>
    [Theory]
    [InlineData("session:../../secret")]
    [InlineData("session:a b")]
    [InlineData("session:")]
    [InlineData("session:a:b")]
    [InlineData("monitor?window=other")]
    [InlineData("settings")]
    [InlineData("main:1")]
    [InlineData("main:../x")]
    [InlineData("")]
    public async Task A_name_the_shell_does_not_know_is_refused_and_nothing_is_opened(string name)
    {
        var refusal = await RefusalAsync(Module(), "OPEN", new { name });

        Assert.Contains("WINDOW_UNKNOWN", refusal);
        Assert.Empty(_windows.Asked);
    }

    /// <summary>
    /// WINDOW2: a secondary window's page tells its own frame which theme it is in, so its caption
    /// follows the viewer's choice rather than the OS. By name, since every window's page talks to
    /// the one module — and a window not open says so rather than failing.
    /// </summary>
    [Fact]
    public async Task A_windows_page_tells_its_own_frame_its_theme_by_name()
    {
        await AnswerAsync(Module(), "OPEN", new { name = "monitor" });

        var told = await AnswerAsync(Module(), "SET_THEME", new { name = "monitor", dark = true });
        Assert.True(told.GetProperty("applied").GetBoolean());
        Assert.Equal(("monitor", true), _windows.Themed.Single());

        var gone = await AnswerAsync(Module(), "SET_THEME", new { name = "session:a1b2c3d4", dark = false });
        Assert.False(gone.GetProperty("applied").GetBoolean());
    }

    /// <summary>
    /// ONEINSTANCE1: another main window, which a later launch opened, is frameless as the main one is, and the kit's
    /// <c>SET_THEME</c> has no route to it, so its page tells its frame the theme by its name here, as a utility window's does.
    /// </summary>
    [Fact]
    public async Task Another_main_windows_page_tells_its_frame_its_theme_by_name()
    {
        await AnswerAsync(Module(), "OPEN", new { name = "main:2" });

        Assert.Equal(("main:2", "/?window=main%3A2&host=http%3A%2F%2Flocalhost%3A5177"), _windows.Asked.Single());
        var told = await AnswerAsync(Module(), "SET_THEME", new { name = "main:2", dark = false });
        Assert.True(told.GetProperty("applied").GetBoolean());
        Assert.Equal(("main:2", false), _windows.Themed.Single());
    }

    [Fact]
    public async Task A_theme_for_a_window_the_shell_does_not_know_is_refused()
    {
        var refusal = await RefusalAsync(Module(), "SET_THEME", new { name = "session:../x", dark = true });

        Assert.Contains("WINDOW_UNKNOWN", refusal);
        Assert.Empty(_windows.Themed);
    }

    /// <summary>
    /// NOTIFY1b: the main window's page tells its frame how much waits on the person (Overview's *What needs you*), for
    /// the taskbar button's overlay and the window's title, with the words a screen reader says in the page's language.
    /// </summary>
    [Fact]
    public async Task The_main_windows_page_tells_its_taskbar_button_what_waits()
    {
        var told = await AnswerAsync(Module(), "SET_BADGE", new { count = 3, description = "3 waiting on you" });

        Assert.True(told.GetProperty("applied").GetBoolean());
        Assert.Equal((3, "3 waiting on you"), _badges.Single());
    }

    [Fact]
    public async Task Nothing_waiting_takes_the_badge_off_and_a_count_below_none_is_none()
    {
        await AnswerAsync(Module(), "SET_BADGE", new { count = 0 });
        await AnswerAsync(Module(), "SET_BADGE", new { count = -2, description = "" });

        Assert.Equal([(0, null), (0, null)], _badges);
    }

    /// <summary>A host with no main window (a test, a headless composition) has no button to badge, and says so.</summary>
    [Fact]
    public async Task A_host_with_no_window_to_badge_answers_that_it_did_not()
    {
        var told = await AnswerAsync(
            new WindowsModule(Bus, _windows, new PlatformAddress(Service)), "SET_BADGE", new { count = 2 });

        Assert.False(told.GetProperty("applied").GetBoolean());
    }

    /// <summary>
    /// The overlay is a small icon, so past nine it says *9+*; the title has the room the page's own badge has, and
    /// past ninety-nine says *99+* as that badge does (`ui.tsx`). Nothing waiting is the plain name and no overlay.
    /// </summary>
    [Theory]
    [InlineData(0, null, "Daoris (道衍)")]
    [InlineData(1, "1", "(1) Daoris (道衍)")]
    [InlineData(9, "9", "(9) Daoris (道衍)")]
    [InlineData(10, "9+", "(10) Daoris (道衍)")]
    [InlineData(99, "9+", "(99) Daoris (道衍)")]
    [InlineData(100, "9+", "(99+) Daoris (道衍)")]
    [InlineData(-1, null, "Daoris (道衍)")]
    public void The_badge_and_the_title_say_the_count_as_their_room_allows(int count, string? label, string title)
    {
        Assert.Equal(label, WaitingBadge.Label(count));
        Assert.Equal(title, WaitingBadge.Title(count));
    }

    /// <summary>
    /// The geometry store keys on the name, and the escape that makes an address also makes a
    /// filename — injective, because `%` is not in the alphabet an id may use.
    /// </summary>
    [Fact]
    public void Each_window_name_has_its_own_geometry_file()
    {
        Assert.Equal("monitor.json", SecondaryWindow.StateFile("monitor"));
        Assert.Equal("session%3Aa1b2c3d4.json", SecondaryWindow.StateFile("session:a1b2c3d4"));

        // Two names a careless `-` substitution would have collided into one file.
        Assert.NotEqual(
            SecondaryWindow.StateFile("session:laptop/a1b2"),
            SecondaryWindow.StateFile("session:laptop-a1b2"));
    }
}
