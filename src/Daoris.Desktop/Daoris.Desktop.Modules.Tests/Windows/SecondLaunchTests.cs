namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ONEINSTANCE1 (D171 point 2): a second launch of the executable is handed to the instance that runs by the kit's
/// single-instance guard, and that instance opens a new main window rather than a second process starting a second loop on
/// the same home. What is here is the hand-over's judgement — when a window opens, which name it takes, where it points —
/// held apart from the window itself, which is WinForms and lives in the application.
/// </summary>
public sealed class SecondLaunchTests
{
    private const string Service = "http://localhost:5177";

    /// <summary>The shell's half, stubbed: what was asked for, without a message pump, and a person's close.</summary>
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

        public bool SetTheme(string name, bool dark) => _open.Contains(name);

        public void Close(string name) => _open.Remove(name);
    }

    private readonly Windows _windows = new();

    private SecondLaunch Launch(Func<Task<bool>>? hostReady = null) =>
        new(_windows, new PlatformAddress(Service), hostReady ?? (() => Task.FromResult(true)));

    [Fact]
    public async Task A_second_launch_opens_a_new_main_window_of_the_instance_that_runs()
    {
        Assert.Equal("main:2", await Launch().OpenAsync([]));

        // The whole platform, at the root, its window named so its page tells its own frame the theme: the same bundle.
        Assert.Equal(("main:2", "/?window=main%3A2&host=http%3A%2F%2Flocalhost%3A5177"), _windows.Asked.Single());
    }

    /// <summary>Each launch is a window more, and a number a person closed is taken again, so geometry files do not pile up.</summary>
    [Fact]
    public async Task Each_launch_opens_another_and_a_closed_windows_number_is_taken_again()
    {
        var launch = Launch();
        Assert.Equal("main:2", await launch.OpenAsync([]));
        Assert.Equal("main:3", await launch.OpenAsync([]));

        _windows.Close("main:2");
        Assert.Equal("main:2", await launch.OpenAsync([]));
        Assert.Equal("main:4", await launch.OpenAsync([]));
    }

    [Fact]
    public async Task The_monitor_and_a_detached_session_are_not_main_windows()
    {
        _windows.Open("monitor", "/");
        _windows.Open("session:a1b2c3d4", "/");

        Assert.Equal("main:2", await Launch().OpenAsync([]));
    }

    /// <summary>
    /// A launch while the instance is still starting waits for its service: a page opened on a port nobody holds reads as
    /// "the app is broken". Where the service never came up the main window says why, and no second window says it again.
    /// </summary>
    [Fact]
    public async Task A_launch_waits_for_the_service_and_opens_nothing_where_it_never_came_up()
    {
        var ready = new TaskCompletionSource<bool>();
        var opening = Launch(() => ready.Task).OpenAsync([]);
        await Task.Delay(50);
        Assert.False(opening.IsCompleted);
        Assert.Empty(_windows.Asked);

        ready.SetResult(false);
        Assert.Null(await opening);
        Assert.Empty(_windows.Asked);

        Assert.Null(await Launch(() => Task.FromException<bool>(new InvalidOperationException("faulted"))).OpenAsync([]));
        Assert.Empty(_windows.Asked);
    }

    /// <summary>
    /// MAINWINDOWS2: a launch while the first main window is put away takes that one back rather than opening another, and
    /// does not wait on the service, whose page that window already holds. The next launch opens a new one again.
    /// </summary>
    [Fact]
    public async Task A_launch_while_the_first_is_put_away_takes_it_back_and_opens_none()
    {
        var mains = new MainWindows();
        var shown = 0;
        _windows.Open("main:2", "/");
        mains.Opened("main:2");
        Assert.True(mains.FirstClosing(byPerson: true));
        _windows.Asked.Clear();

        var never = new TaskCompletionSource<bool>();
        var launch = new SecondLaunch(_windows, new PlatformAddress(Service), () => never.Task, mains: mains, showFirst: () => shown++);
        Assert.Equal(MainWindows.First, await launch.OpenAsync([]));
        Assert.Equal(1, shown);
        Assert.Empty(_windows.Asked);
        Assert.False(mains.FirstAway);

        never.SetResult(true);
        Assert.Equal("main:3", await launch.OpenAsync([]));
        Assert.Equal(1, shown);
    }

    /// <summary>The kit brought the put-away first forward before handing the launch over: that is the launch's window.</summary>
    [Fact]
    public async Task A_launch_after_the_kit_showed_the_put_away_first_opens_none()
    {
        var mains = new MainWindows();
        _windows.Open("main:2", "/");
        mains.Opened("main:2");
        Assert.True(mains.FirstClosing(byPerson: true));
        mains.Shown();
        _windows.Asked.Clear();

        var shown = 0;
        Assert.Equal(MainWindows.First, await new SecondLaunch(
            _windows, new PlatformAddress(Service), () => Task.FromResult(true), mains: mains, showFirst: () => shown++).OpenAsync([]));
        Assert.Equal(1, shown);
        Assert.Empty(_windows.Asked);
    }

    [Theory]
    [InlineData("main:2", true)]
    [InlineData("main:10", true)]
    [InlineData("main:1", false)]
    [InlineData("main:0", false)]
    [InlineData("main:02", false)]
    [InlineData("main:", false)]
    [InlineData("main:x", false)]
    [InlineData("main:2/../x", false)]
    public void A_main_windows_name_is_its_number_from_two(string name, bool known)
    {
        Assert.Equal(known, SecondaryWindow.IsKnown(name));
        Assert.Equal(known, SecondaryWindow.IsMain(name));
    }

    [Fact]
    public void Each_main_window_has_its_own_geometry_file() =>
        Assert.Equal("main%3A2.json", SecondaryWindow.StateFile("main:2"));

    /// <summary>
    /// Held on the application's own source, because nothing short of a second process holds it: the kit's guard hands a later
    /// launch to the running instance, which passes it to the hand-over above.
    /// </summary>
    [Fact]
    public void The_application_hands_a_later_launch_to_the_hand_over()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App", "Program.cs"));

        Assert.Contains("SingleInstance = new SingleInstanceHostOptions", program);
        Assert.Contains("OnActivated = ", program);
        Assert.Contains("GetRequiredService<SecondLaunch>().OpenAsync(", program);
    }

    /// <summary>
    /// MAINWINDOWS2, held on the application's own source for the same reason: the first main window puts itself away on the
    /// person's close while another main window is open and closes for good otherwise; every other window's opening and closing
    /// is told; the update and the last main window close it for good; the tray and a launch take it back.
    /// </summary>
    [Fact]
    public void The_application_ends_with_its_last_main_window()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App");
        var program = File.ReadAllText(Path.Combine(app, "Program.cs"));
        var form = File.ReadAllText(Path.Combine(app, "MainForm.cs"));
        var host = File.ReadAllText(Path.Combine(app, "SecondaryWindowHost.cs"));
        var tray = File.ReadAllText(Path.Combine(app, "SessionNotifier.cs"));

        Assert.Contains("new MainWindows(() => sp.GetRequiredService<MainForm>().CloseForGood()", program);
        Assert.Contains("close: () => sp.GetRequiredService<MainForm>().CloseForGood()", program);
        Assert.Contains("showFirst: () => sp.GetRequiredService<MainForm>().TakeBack()", program);
        Assert.DoesNotContain("form.BeginInvoke(form.Close)", program);

        Assert.Contains("_mains?.FirstClosing(byPerson: e.CloseReason == CloseReason.UserClosing && !_forGood) == true", form);
        Assert.Contains("_mains?.Shown()", form);
        Assert.Contains("mains?.Opened(name)", host);
        Assert.Contains("mains?.Closed(name)", host);
        Assert.Contains("_takingBack?.Invoke()", tray);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("the tests run outside a checkout");
    }
}
