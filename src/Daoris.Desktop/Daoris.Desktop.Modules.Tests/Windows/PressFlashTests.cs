namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NOTIFY2b (D196 point 4, the notification design §3.3): while a terminal's press waits unshown with no main window of Daoris in
/// front, the taskbar button of the main window last in front is lit, and stays lit for the ten minutes a press waits to be found,
/// which the balloon's few seconds do not; it is stopped when a main window comes forward, or when the last press waiting unshown
/// ends or is shown. What is here is that judgement, held apart from the windows, which flash in the application.
/// </summary>
public sealed class PressFlashTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly List<(string Window, bool On)> _told = [];

    private PressFlash Flash() => new((window, on) => _told.Add((window, on)));

    private static PipeConfirmation Press(string id, bool shown = false) => new(
        id, DriverModule.Name, "SESSION_INPUT", [], new PipeAsker("bash", 4120), At, At.AddMinutes(10), shown ? At.AddMinutes(1) : null);

    private static PipeConfirmationsNow Waiting(params PipeConfirmation[] presses) => new(presses.Length, presses);

    [Fact]
    public void A_press_asked_while_no_main_window_is_in_front_lights_the_main_window_last_in_front_once()
    {
        var flash = Flash();

        flash.Told(Waiting(Press("p1")));
        flash.Told(Waiting(Press("p1"), Press("p2")));

        Assert.Equal([(MainWindows.First, true)], _told);
        Assert.Equal(MainWindows.First, flash.Lit);
    }

    [Fact]
    public void The_window_lit_is_the_main_window_last_in_front_and_never_a_utility_window()
    {
        var flash = Flash();
        var second = SecondaryWindow.ForMain(2);
        flash.CameForward(second);
        flash.WentBehind(second);
        // A detached session's window coming forward is the person at Daoris, but not where the card is: the main window is lit.
        flash.CameForward(SecondaryWindow.ForSession("a1b2c3d4"));

        flash.Told(Waiting(Press("p1")));

        Assert.Equal([(second, true)], _told);
    }

    [Fact]
    public void A_main_window_coming_forward_stops_it_and_lights_nothing_again_for_the_presses_it_was_lit_for()
    {
        var flash = Flash();
        flash.Told(Waiting(Press("p1")));

        flash.CameForward(MainWindows.First);
        flash.WentBehind(MainWindows.First);
        flash.Told(Waiting(Press("p1")));

        Assert.Equal([(MainWindows.First, true), (MainWindows.First, false)], _told);
        Assert.Null(flash.Lit);

        // A press asked after it is lit for anew.
        flash.Told(Waiting(Press("p1"), Press("p2")));
        Assert.Equal((MainWindows.First, true), _told[^1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_last_press_waiting_unshown_ending_or_shown_stops_it(bool ends)
    {
        var flash = Flash();
        flash.Told(Waiting(Press("p1"), Press("p2")));

        flash.Told(ends ? Waiting(Press("p2")) : Waiting(Press("p1", shown: true), Press("p2")));
        Assert.Equal(MainWindows.First, flash.Lit);
        flash.Told(ends ? Waiting() : Waiting(Press("p1", shown: true), Press("p2", shown: true)));

        Assert.Equal([(MainWindows.First, true), (MainWindows.First, false)], _told);
        Assert.Null(flash.Lit);
    }

    [Fact]
    public void A_press_asked_while_a_main_window_is_in_front_lights_nothing_since_the_window_says_it()
    {
        var flash = Flash();
        flash.CameForward(MainWindows.First);

        flash.Told(Waiting(Press("p1")));
        flash.WentBehind(MainWindows.First);
        flash.Told(Waiting(Press("p1")));

        Assert.Empty(_told);
    }
}
