namespace Daoris.Desktop;

/// <summary>
/// Which main window's taskbar button is lit for a terminal's press waiting unshown (NOTIFY2b, D196 point 4, the notification
/// design §3.3): the main window last in front, while no main window is, from the press's ask until a main window comes forward, or
/// the last press waiting unshown ends or is shown. Kept here, in plain <c>net10.0</c>, so that is tested; the application flashes
/// the button (<c>FlashWindowEx</c>) as it is told.
/// </summary>
/// <remarks>
/// <para><b>Why a lit button.</b> The balloon passes in seconds into the system's own list, and five presses once waited their ten
/// minutes to be found with the person away (2026-10-21); the button stays lit while they wait. A press is never named on it.</para>
///
/// <para><b>Lit once per press.</b> A press asked while a main window is in front is said there (the bell's hue and its toast),
/// and lights nothing; a press the button was lit for lights it no more once a main window came forward. A utility window (the
/// monitor, a detached session) in front is the person at Daoris but not where the card is, so the main window is still lit.</para>
///
/// <para>Told from any thread: the pipe tells what waits on the thread that changed it, and each window its focus on its own; one
/// lock holds the state, and the windows are told outside it.</para>
/// </remarks>
/// <param name="flash">Lights (<c>true</c>) or stops (<c>false</c>) the taskbar button of the main window it names, from any thread.</param>
public sealed class PressFlash(Action<string, bool> flash)
{
    private readonly object _gate = new();
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private string _last = MainWindows.First;
    private string? _front;
    private string? _lit;

    /// <summary>The main window whose button is lit now, or null.</summary>
    public string? Lit
    {
        get
        {
            lock (_gate) return _lit;
        }
    }

    /// <summary>What waits, as the pipe tells every window (<see cref="PipeConfirmations.Event"/>).</summary>
    public void Told(PipeConfirmationsNow now)
    {
        var unshown = now.Waiting.Where(each => each.ShownAt is null).Select(each => each.Id).ToHashSet(StringComparer.Ordinal);
        (string Window, bool On)? act = null;
        lock (_gate)
        {
            var asked = unshown.Any(id => !_known.Contains(id));
            _known.IntersectWith(unshown);
            _known.UnionWith(unshown);
            if (unshown.Count == 0 && _lit is { } stop)
            {
                _lit = null;
                act = (stop, false);
            }
            else if (asked && _front is null && _lit is null)
            {
                _lit = _last;
                act = (_last, true);
            }
        }

        if (act is { } told) flash(told.Window, told.On);
    }

    /// <summary>A window came in front of the person: a main window is the one last in front, and stops the button lit.</summary>
    public void CameForward(string window)
    {
        if (!IsMain(window)) return;
        string? stop;
        lock (_gate)
        {
            _last = window;
            _front = window;
            stop = _lit;
            _lit = null;
        }

        if (stop is not null) flash(stop, false);
    }

    /// <summary>A window went behind another, or was minimized: no main window is in front, until one comes forward.</summary>
    public void WentBehind(string window)
    {
        if (!IsMain(window)) return;
        lock (_gate)
        {
            if (_front == window) _front = null;
        }
    }

    private static bool IsMain(string window) => window == MainWindows.First || SecondaryWindow.IsMain(window);
}
