namespace Daoris.Desktop;

/// <summary>
/// A later launch of the executable, handed to the instance that runs (ONEINSTANCE1, D171 point 2): it opens a new main
/// window there. The windows of one instance share one driver, one loop and one state hub (D169), so a second process would
/// be a second writer of the same home; the kit's single-instance guard keeps it from starting, and hands its launch here.
/// </summary>
/// <remarks>
/// <para><b>How the launch arrives.</b> The kit's guard (<c>WindowsHostOptions.SingleInstance</c>, on by default) answers a
/// later launch by bringing this instance's main window forward and then, on the window's thread, calling the app's
/// <c>OnActivated</c> with that launch's arguments and folder. The application hands it to <see cref="OpenAsync"/>.</para>
///
/// <para><b>Every launch opens a window</b>, whatever it carries: Daoris takes no file or link on its command line, and the
/// flags a native launcher adds (<c>--app-root</c>, <c>--startup-screen</c>) say nothing about what to open.</para>
///
/// <para><b>Not before the service answers</b>: the page is the host's, and a page opened on a port nobody holds reads as
/// "the app is broken" (<c>MainForm</c> waits the same way). Where the service never came up, the main window already says
/// why, and no window opens.</para>
/// </remarks>
public sealed class SecondLaunch(ISecondaryWindows windows, PlatformAddress platform, Func<Task<bool>> hostReady,
    // The shell's machine log (LOG1b, D94): a launch handed over, and the window it opened. Null writes none.
    Daoris.Driver.MachineLog? log = null)
{
    /// <summary>Open a new main window for a later launch, once the service answers: its name, or null when none opened.</summary>
    /// <param name="arguments">The later launch's arguments, which say nothing about what to open today; their count is logged.</param>
    public async Task<string?> OpenAsync(IReadOnlyList<string> arguments)
    {
        bool up;
        try
        {
            up = await hostReady().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The host's start faulted: the main window says why, as for false.
            up = false;
        }

        if (!up)
        {
            log?.Info("window.launched", ("opened", false), ("arguments", arguments.Count));
            return null;
        }

        // Never blocks: the shell opens each window on its own thread (SecondaryWindowHost).
        var name = SecondaryWindow.NextMain(windows.Opened);
        windows.Open(name, DesktopPage.PathFor(platform.Url, name));
        log?.Info("window.launched", ("opened", true), ("window", name), ("arguments", arguments.Count));
        return name;
    }
}
