using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MAINWINDOWS2 (D171 point 2): the application ends with its last main window, not its first. The first main window holds
/// the kit's message loop, and its services (the pickers, the taskbar's count, the tray) are resolved as that one window, so a
/// person's close of it while another main window is open puts it away (hidden, its page and services kept) rather than ending
/// the loop; the last of the others closing then ends the application, and a later launch, or the tray, takes the first back.
/// What is here is that judgement, held apart from the windows, which are WinForms and live in the application.
/// </summary>
public sealed class MainWindowsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-mainwindows2-" + Guid.NewGuid().ToString("N")[..8]);
    private int _ended;

    public MainWindowsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private MainWindows Windows(MachineLog? log = null) => new(() => _ended++, log);

    [Fact]
    public void With_no_other_main_window_the_first_ones_close_ends_the_application()
    {
        var windows = Windows();
        Assert.False(windows.FirstClosing(byPerson: true));
        Assert.False(windows.FirstAway);

        // A utility window is no main window: the monitor and a detached session close with the application.
        windows.Opened(SecondaryWindow.Monitor);
        windows.Opened(SecondaryWindow.ForSession("a1b2c3d4"));
        Assert.False(windows.FirstClosing(byPerson: true));
        Assert.False(windows.FirstAway);
    }

    [Fact]
    public void The_persons_close_of_the_first_puts_it_away_while_another_main_window_is_open()
    {
        var windows = Windows();
        windows.Opened("main:2");

        Assert.True(windows.FirstClosing(byPerson: true));
        Assert.True(windows.FirstAway);
        Assert.Equal(0, _ended);
    }

    /// <summary>
    /// A close that is not the person's ends the application whatever else is open: an update's close, the machine's
    /// shutdown, the task manager. The kit reports a close from code as the person's, so the application says which it is.
    /// </summary>
    [Fact]
    public void A_close_that_is_not_the_persons_ends_the_application_whatever_is_open()
    {
        var windows = Windows();
        windows.Opened("main:2");

        Assert.False(windows.FirstClosing(byPerson: false));
        Assert.False(windows.FirstAway);
    }

    [Fact]
    public void The_last_main_window_closing_with_the_first_put_away_ends_the_application_once()
    {
        var windows = Windows();
        windows.Opened("main:2");
        windows.Opened("main:3");
        windows.Opened(SecondaryWindow.Monitor);
        Assert.True(windows.FirstClosing(byPerson: true));

        Assert.False(windows.Closed("main:2"));
        Assert.Equal(0, _ended);

        // The monitor keeps nothing alive, and its close ends nothing.
        Assert.False(windows.Closed(SecondaryWindow.Monitor));
        Assert.Equal(0, _ended);

        Assert.True(windows.Closed("main:3"));
        Assert.Equal(1, _ended);

        // The application's own end closes what is left; nothing ends it twice, and the first is not taken back.
        Assert.False(windows.Closed("main:3"));
        Assert.False(windows.TakeBackFirst());
        Assert.Equal(1, _ended);
    }

    [Fact]
    public void Another_main_window_closing_while_the_first_is_shown_ends_nothing()
    {
        var windows = Windows();
        windows.Opened("main:2");

        Assert.False(windows.Closed("main:2"));
        Assert.Equal(0, _ended);
    }

    /// <summary>
    /// The person asks for a window back, by a later launch or the tray: the first, put away, is theirs again and is the one
    /// shown, once; the last of the others closing then ends nothing.
    /// </summary>
    [Fact]
    public void Taking_the_first_back_holds_it_until_it_is_closed_again()
    {
        var windows = Windows();
        windows.Opened("main:2");
        Assert.False(windows.TakeBackFirst());

        Assert.True(windows.FirstClosing(byPerson: true));
        Assert.True(windows.TakeBackFirst());
        Assert.False(windows.FirstAway);
        Assert.False(windows.TakeBackFirst());

        Assert.False(windows.Closed("main:2"));
        Assert.Equal(0, _ended);
    }

    /// <summary>
    /// The kit brings the first window forward before it hands the launch over, so a put-away first is shown again before
    /// the launch is asked what to open: shown while away is that launch's taking it back, which the launch then reads, so it
    /// opens no other. Nothing else shows it unasked; the tray takes it back first.
    /// </summary>
    [Fact]
    public void A_first_shown_while_put_away_is_the_next_launchs_window()
    {
        var windows = Windows();
        windows.Opened("main:2");
        Assert.True(windows.FirstClosing(byPerson: true));

        windows.Shown();
        Assert.False(windows.FirstAway);
        Assert.False(windows.Closed("main:2"));
        Assert.Equal(0, _ended);

        Assert.True(windows.TakeBackFirst());
        Assert.False(windows.TakeBackFirst());

        // Shown while not put away is nothing to take back.
        windows.Shown();
        Assert.False(windows.TakeBackFirst());
    }

    /// <summary>MAINWINDOWS2b: another main window wears the taskbar's count only while the first, whose button it is, is put away.</summary>
    [Fact]
    public void Another_main_window_wears_the_count_only_while_the_first_is_put_away()
    {
        var windows = Windows();
        windows.Opened("main:2");

        Assert.True(windows.WearsCount(MainWindows.First));
        Assert.False(windows.WearsCount("main:2"));
        Assert.False(windows.WearsCount(SecondaryWindow.Monitor));

        Assert.True(windows.FirstClosing(byPerson: true));
        Assert.True(windows.WearsCount("main:2"));
        Assert.False(windows.WearsCount(SecondaryWindow.Monitor));
        Assert.False(windows.WearsCount(SecondaryWindow.ForSession("a1b2c3d4")));

        windows.Shown();
        Assert.False(windows.WearsCount("main:2"));
    }

    [Fact]
    public void The_windows_are_told_each_time_the_first_goes_away_or_comes_back()
    {
        var windows = Windows();
        var told = 0;
        windows.AwayChanged += () => told++;
        windows.Opened("main:2");

        Assert.True(windows.FirstClosing(byPerson: true));
        Assert.Equal(1, told);

        Assert.True(windows.TakeBackFirst());
        Assert.Equal(2, told);

        // Nothing to take back, and shown while not away: nothing changed, nothing told.
        Assert.False(windows.TakeBackFirst());
        windows.Shown();
        Assert.Equal(2, told);

        Assert.True(windows.FirstClosing(byPerson: true));
        windows.Shown();
        Assert.Equal(4, told);
    }

    /// <summary>MAINWINDOWS2b: what each window is to show of the count the page last told.</summary>
    [Fact]
    public void The_count_the_page_told_is_shown_by_the_windows_that_wear_it()
    {
        var windows = Windows();
        var count = new WaitingCount(windows);
        windows.Opened("main:2");
        count.Told(3, "3 waiting on you");

        Assert.Equal((3, "3 waiting on you"), count.For(MainWindows.First));
        Assert.Equal((0, null), count.For("main:2"));

        windows.FirstClosing(byPerson: true);
        Assert.Equal((3, "3 waiting on you"), count.For("main:2"));
        Assert.Equal((0, null), count.For(SecondaryWindow.Monitor));

        count.Told(0, "stale words");
        Assert.Equal((0, null), count.For("main:2"));
    }

    [Fact]
    public void The_machine_log_says_the_first_put_away_and_the_last_main_window_closed()
    {
        using (var log = new MachineLog(_home, "desktop"))
        {
            var windows = Windows(log);
            windows.Opened("main:2");
            Assert.True(windows.FirstClosing(byPerson: true));
            Assert.True(windows.Closed("main:2"));
        }

        var lines = Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(File.ReadAllLines).ToArray();
        Assert.Contains(lines, line => line.Contains("\"event\":\"window.away\"") && line.Contains("\"others\":1"));
        Assert.Contains(lines, line => line.Contains("\"event\":\"window.last\"") && line.Contains("\"window\":\"main:2\""));
    }
}
