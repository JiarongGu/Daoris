namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NOTIFY2c (D196 point 6, the notification design §2.4 and §9): with two main windows open, one per workspace, a notification's
/// click opens the window whose workspace holds what it is about, or the main window last in front where none or several do. Each
/// main window's page tells the registry its workspace as data; what it is about is found in the records the windows hold. What is
/// here is that judgement, held apart from the windows, which come forward in the application.
/// </summary>
public sealed class MainWindowScopesTests
{
    [Fact]
    public void A_notification_opens_the_one_main_window_whose_workspace_holds_it()
    {
        var scopes = new MainWindowScopes();
        scopes.Told(MainWindows.First, "games");
        scopes.Told("main:2", "work");
        scopes.CameForward("main:2");

        Assert.Equal(MainWindows.First, scopes.Pick("games"));
        Assert.Equal("main:2", scopes.Pick("work"));
        // A workspace compared as the service names it: trimmed, its ASCII letters folded.
        Assert.Equal(MainWindows.First, scopes.Pick(" Games "));
    }

    [Fact]
    public void Where_none_holds_it_the_main_window_last_in_front_opens()
    {
        var scopes = new MainWindowScopes();
        scopes.Told(MainWindows.First, "games");
        scopes.Told("main:2", "work");
        scopes.CameForward(MainWindows.First);
        scopes.CameForward("main:2");

        Assert.Equal("main:2", scopes.Pick("elsewhere"));
        // What is the machine's (a terminal's press, an account's wait) is every window's, so the one last in front.
        Assert.Equal("main:2", scopes.Pick(null));

        scopes.CameForward(MainWindows.First);
        Assert.Equal(MainWindows.First, scopes.Pick("elsewhere"));
        Assert.Equal(MainWindows.First, scopes.Pick(null));
    }

    /// <summary>A window on every workspace holds every workspace's records: where it and another both hold one, the one of them last in front.</summary>
    [Fact]
    public void Where_several_hold_it_the_one_of_them_last_in_front_opens()
    {
        var scopes = new MainWindowScopes();
        scopes.Told(MainWindows.First, "games");
        scopes.Told("main:2", null);
        scopes.Told("main:3", "work");
        scopes.CameForward("main:2");
        scopes.CameForward(MainWindows.First);
        scopes.CameForward("main:3");

        Assert.Equal(MainWindows.First, scopes.Pick("games"));
        Assert.Equal("main:3", scopes.Pick("work"));

        scopes.CameForward("main:2");
        Assert.Equal("main:2", scopes.Pick("games"));
        Assert.Equal("main:2", scopes.Pick("work"));
    }

    /// <summary>A record with no workspace is the default's, as the service names it, and a window on the default holds it.</summary>
    [Fact]
    public void The_default_workspace_is_held_by_the_window_on_it()
    {
        var scopes = new MainWindowScopes();
        scopes.Told(MainWindows.First, "games");
        scopes.Told("main:2", "Default");
        scopes.CameForward(MainWindows.First);

        Assert.Equal("main:2", scopes.Pick("default"));
        Assert.Equal("main:2", scopes.Pick(""));
    }

    /// <summary>A window told again holds what it was told last; a window closed is never picked, and nothing told is the first.</summary>
    [Fact]
    public void A_window_closed_is_never_picked_and_one_told_again_holds_its_last()
    {
        var scopes = new MainWindowScopes();
        Assert.Equal(MainWindows.First, scopes.Pick("games"));

        scopes.Told(MainWindows.First, "games");
        scopes.Told("main:2", "work");
        scopes.CameForward("main:2");
        scopes.Told("main:2", "games");
        scopes.CameForward(MainWindows.First);
        Assert.Equal(MainWindows.First, scopes.Pick("games"));
        Assert.Equal(MainWindows.First, scopes.Pick("work"));

        scopes.Closed(MainWindows.First);
        Assert.Equal("main:2", scopes.Pick("games"));
        Assert.Equal("main:2", scopes.Pick(null));
    }

    /// <summary>
    /// MAINWINDOWS2: the first main window the person closed while another was open is put away, its page kept, and is no window they
    /// have open, so it is never picked while it is away; taken back, it is again.
    /// </summary>
    [Fact]
    public void The_first_put_away_is_never_picked()
    {
        var mains = new MainWindows();
        mains.Opened("main:2");
        var scopes = new MainWindowScopes(mains);
        scopes.Told(MainWindows.First, "games");
        scopes.Told("main:2", "work");
        scopes.CameForward(MainWindows.First);

        Assert.True(mains.FirstClosing(byPerson: true));
        Assert.Equal("main:2", scopes.Pick("games"));
        Assert.Equal("main:2", scopes.Pick(null));

        Assert.True(mains.TakeBackFirst());
        Assert.Equal(MainWindows.First, scopes.Pick("games"));
    }

    /// <summary>A utility window (the monitor, a detached session) holds no workspace a notification opens, and its focus moves nothing here.</summary>
    [Fact]
    public void A_utility_window_is_never_picked()
    {
        var scopes = new MainWindowScopes();
        scopes.Told(SecondaryWindow.Monitor, "games");
        scopes.CameForward(SecondaryWindow.Monitor);
        scopes.Told("main:2", "work");
        scopes.CameForward("main:2");

        Assert.Equal("main:2", scopes.Pick("games"));
        Assert.Equal("main:2", scopes.Pick(null));
    }

    /// <summary>What a notification is about is in the workspace the windows' records hold it in: its session's, or else its quest's.</summary>
    [Fact]
    public void What_a_notification_is_about_is_found_in_the_records_the_windows_hold()
    {
        var sessions = Snapshot(SessionsStateSlice.Name, SessionsStateSlice.Judge("""
            [
              { "id": "s1a2b3c4", "repository": "engine", "workspace": "games" },
              { "id": "s5e6f7a8", "repository": "tools" },
              { "id": "laptop/b05y0000", "repository": "engine", "workspace": "work" }
            ]
            """));
        var quests = Snapshot(QuestsStateSlice.Name, QuestsStateSlice.Judge("""
            [{ "id": "q1", "from": "game", "to": "engine", "title": "Expose a budget", "workspace": "work" }]
            """));

        Assert.Equal("games", MainWindowScopes.WorkspaceOf(sessions, quests, "s1a2b3c4", null));
        Assert.Equal("default", MainWindowScopes.WorkspaceOf(sessions, quests, "s5e6f7a8", null));
        Assert.Equal("work", MainWindowScopes.WorkspaceOf(sessions, quests, "laptop/b05y0000", null));
        // A quest's park names its last session, which may be gone from the records: its quest's then.
        Assert.Equal("work", MainWindowScopes.WorkspaceOf(sessions, quests, "gone0000", "q1"));
        Assert.Equal("work", MainWindowScopes.WorkspaceOf(sessions, quests, "", "q1"));
        // Nothing named, or nothing the records hold: none, which is every window's.
        Assert.Null(MainWindowScopes.WorkspaceOf(sessions, quests, null, null));
        Assert.Null(MainWindowScopes.WorkspaceOf(sessions, quests, "", null));
        Assert.Null(MainWindowScopes.WorkspaceOf(sessions, quests, "gone0000", "q9"));
        // Before the slices are judged.
        Assert.Null(MainWindowScopes.WorkspaceOf(null, null, "s1a2b3c4", "q1"));
    }

    /// <summary>
    /// Held on the application's own source, since no window opens without a desktop session (as <see cref="SecondLaunchTests"/> holds
    /// MAINWINDOWS2's): every main window's focus and close reach the registry, a balloon's click is picked from it and brings that
    /// window forward, the attend names it, and each main window's count goes to its own frame.
    /// </summary>
    [Fact]
    public void The_application_tells_the_registry_and_a_balloon_s_click_reads_it()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App");
        var program = File.ReadAllText(Path.Combine(app, "Program.cs"));
        var first = File.ReadAllText(Path.Combine(app, "MainForm.cs"));
        var host = File.ReadAllText(Path.Combine(app, "SecondaryWindowHost.cs"));
        var tray = File.ReadAllText(Path.Combine(app, "SessionNotifier.cs"));

        Assert.Contains("new MainWindowScopes(sp.GetRequiredService<MainWindows>())", program);
        Assert.Contains("if (window == MainWindows.First) sp.GetRequiredService<MainForm>().ShowWaiting(count, description);", program);
        Assert.Contains("else sp.GetRequiredService<SecondaryWindowHost>().ShowWaiting(window);", program);
        Assert.Contains("Activated += (_, _) => scopes.CameForward(MainWindows.First);", first);
        Assert.Contains("(session, quest) => scopes.PickFor(driver.Slices, session, quest)", first);
        Assert.Contains("Payload = new { Session = session, Window = window }", first);
        Assert.Contains("form.Activated += (_, _) => scopes.CameForward(name);", host);
        Assert.Contains("scopes?.Closed(name);", host);
        Assert.Contains("waiting?.Closed(name);", host);
        Assert.Contains("_icon.BalloonTipClicked += (_, _) => Open(_about, _aboutQuest);", tray);
    }

    private static SliceSnapshot Snapshot(string slice, IReadOnlyList<SliceJudged> judged) =>
        new(slice, "e1", StateSlice.Ready, [.. judged.Select(each => new SlicePartition(each.Partition, 1, StateSlice.Ready, each.Data))]);

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
