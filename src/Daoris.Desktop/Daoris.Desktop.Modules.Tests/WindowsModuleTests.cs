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
    }

    private readonly Windows _windows = new();

    private WindowsModule Module() => new(Bus, _windows, new PlatformAddress(Service));

    [Fact]
    public async Task The_monitor_opens_at_its_own_route_into_the_same_bundle()
    {
        var state = await AnswerAsync(Module(), "OPEN", new { name = "monitor" });

        // The same bytes a browser gets, with one parameter — not a second frontend (D55 §b).
        Assert.Equal(
            ("monitor", "http://localhost:5177/?window=monitor"),
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
        Assert.Equal("http://localhost:5177/?window=session%3Aa1b2c3d4", address);
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
            "http://localhost:5177/?window=session%3Alaptop%2Fa1b2c3d4",
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

    [Fact]
    public async Task State_answers_what_is_open_without_opening_anything()
    {
        await AnswerAsync(Module(), "OPEN", new { name = "session:a1b2c3d4" });
        _windows.Asked.Clear();

        var state = await AnswerAsync(Module(), "STATE");

        Assert.Empty(_windows.Asked);
        Assert.Equal("session:a1b2c3d4", state.GetProperty("windows")[0].GetString());
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
    [InlineData("")]
    public async Task A_name_the_shell_does_not_know_is_refused_and_nothing_is_opened(string name)
    {
        var refusal = await RefusalAsync(Module(), "OPEN", new { name });

        Assert.Contains("WINDOW_UNKNOWN", refusal);
        Assert.Empty(_windows.Asked);
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
