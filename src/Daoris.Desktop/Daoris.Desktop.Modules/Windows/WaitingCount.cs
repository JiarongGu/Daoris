namespace Daoris.Desktop;

/// <summary>
/// The count each main window's page last told (NOTIFY1b), by the window's name: what that window wears on its own taskbar button
/// and in its title (NOTIFY2c, D196 point 6, the notification design §2.4). Kept here, in plain <c>net10.0</c>, so that is tested; the
/// windows only draw what they are told.
/// </summary>
/// <remarks>
/// <para><b>Each its own.</b> Every main window's page tells its own workspace's count (<c>useTaskbarCount</c>), and its window wears
/// that. Until NOTIFY2c the first main window's page alone told one count, which another main window wore while the first was put
/// away (MAINWINDOWS2b), whichever workspace it held. The first put away now takes its button and its count with it, and every other
/// main window wears its own.</para>
///
/// <para>Kept so a window wears its count again where it is made after its page told it, and forgotten as the window closes, so one
/// opened again under its name wears nothing until its own page tells it. Called from any thread: one lock holds the map.</para>
/// </remarks>
public sealed class WaitingCount
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (int Count, string? Description)> _told = new(StringComparer.Ordinal);

    /// <summary>
    /// The page of the main window named <paramref name="window"/> told how much waits: a count below none is none, and words with
    /// none are none. A utility window's is no count.
    /// </summary>
    public void Told(string window, int count, string? description)
    {
        if (!MainWindows.IsMain(window)) return;
        var worn = Math.Max(0, count);
        lock (_gate) _told[window] = (worn, worn > 0 ? description : null);
    }

    /// <summary>What the window named <paramref name="window"/> wears now: nothing where its page has told nothing.</summary>
    public (int Count, string? Description) For(string window)
    {
        lock (_gate) return _told.TryGetValue(window, out var told) ? told : (0, null);
    }

    /// <summary>The window named <paramref name="window"/> closed: its count goes with it.</summary>
    public void Closed(string window)
    {
        lock (_gate) _told.Remove(window);
    }
}
