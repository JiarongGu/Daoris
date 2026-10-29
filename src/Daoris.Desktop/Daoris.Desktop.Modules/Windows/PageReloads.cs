namespace Daoris.Desktop;

/// <summary>A window that can reload the page its browser shows (PAGELOOP1 c, D217): a main window or a secondary one.</summary>
public interface IReloadableWindow
{
    /// <summary>The window's name in the machine log: <c>main:1</c> for the first main window, else the name it was opened under.</summary>
    string Name { get; }

    /// <summary>
    /// Reload the page the window shows, posted to the window's own thread and never blocking; false where there is no page to
    /// reload (the window is on its splash or its trouble label, or is closing).
    /// </summary>
    bool ReloadPage();
}

/// <summary>
/// The tray's reload (PAGELOOP1 c, D217): the browser's own reload of the page every open window shows, for a window whose page is
/// blank past what the page's error screen catches. Only pages are reloaded: the host, the driver, the service and every session
/// keep running, and the page's bridge handshakes again as it does on any reload.
/// </summary>
/// <remarks>
/// A window that cannot reload (still on its splash, or on the trouble label that says why no page came) is skipped and not logged;
/// one that throws does not stop the rest. Each reload is said in the machine log by the window's name, as the page's
/// <c>page.error</c> lines say what broke.
/// </remarks>
/// <param name="windows">The windows open now, asked at each reload since they are made after the tray is.</param>
/// <param name="logged">Says one reload in the machine log, by the window's name.</param>
public sealed class PageReloads(Func<IEnumerable<IReloadableWindow>> windows, Action<string> logged)
{
    /// <summary>The event the machine log carries for one reload.</summary>
    public const string Event = "page.reloaded";

    /// <summary>Reload every open window's page; the names of the windows that took it, in the order asked.</summary>
    public IReadOnlyList<string> Reload()
    {
        var reloaded = new List<string>();
        foreach (var window in windows())
        {
            try
            {
                if (!window.ReloadPage()) continue;
            }
            catch (Exception)
            {
                // A window going while it is asked is no reason to leave the others blank.
                continue;
            }

            reloaded.Add(window.Name);
            logged(window.Name);
        }

        return reloaded;
    }
}
