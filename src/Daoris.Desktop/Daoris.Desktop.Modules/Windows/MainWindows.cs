namespace Daoris.Desktop;

/// <summary>
/// The application ends with its last main window, not its first (MAINWINDOWS2, D171 point 2).
/// </summary>
/// <remarks>
/// <para><b>Why the first is put away rather than closed.</b> The first main window is the kit's: its message loop runs until
/// that form closes (<c>WindowsHostOptions.MainForm</c>), and the application's services are resolved as that one window (the
/// folder and file pickers' owner, the taskbar's count, the tray, the update's close). A person's close of it while another
/// main window (<c>main:&lt;n&gt;</c>, ONEINSTANCE1) is open hides it instead, its page and its services kept, as the kit's own
/// close-to-tray hides a window and keeps the loop. The last of the others closing then ends the application, the first
/// closed for good. A utility window (the monitor, a detached session) keeps nothing alive.</para>
///
/// <para><b>Taken back.</b> A later launch, or the tray, asks for a window: the first, put away, is theirs again and is the one
/// shown, rather than another opened. The kit brings the main window forward before it hands a launch over, so a first shown
/// while still marked away is that launch's (<see cref="Shown"/>), which the launch then reads (<see cref="TakeBackFirst"/>).</para>
///
/// <para>Called from each window's own thread; one lock holds the count, so two windows closing at once cannot both see the
/// other open. The judgement is here in plain <c>net10.0</c> where it is tested; only the WinForms acts are in the windows.</para>
/// </remarks>
/// <param name="end">Closes the first main window for good, which ends the application: from any thread, never blocking.</param>
/// <param name="log">The shell's machine log: the first put away, and the last main window closed. Null writes none.</param>
public sealed class MainWindows(Action? end = null, Daoris.Driver.MachineLog? log = null)
{
    /// <summary>The first main window's name in a log line or a launch's answer: no secondary window wears it (they are numbered from two).</summary>
    public const string First = "main";

    private readonly object _gate = new();
    private readonly HashSet<string> _others = new(StringComparer.Ordinal);
    private bool _away;
    private bool _cameBack;
    private bool _ending;

    /// <summary>Whether the first main window is put away: hidden, its page and services kept.</summary>
    public bool FirstAway
    {
        get
        {
            lock (_gate) return _away;
        }
    }

    /// <summary>
    /// The first main window went away or came back (MAINWINDOWS2b): the windows that wear the taskbar's count are now others.
    /// Raised on the thread that changed it, outside the lock.
    /// </summary>
    public event Action? AwayChanged;

    /// <summary>
    /// Whether this window wears the taskbar's count (MAINWINDOWS2b): the first main window always, whose button it is, and each
    /// other main window while the first is put away, since a button hidden shows none. A utility window never does.
    /// </summary>
    public bool WearsCount(string name)
    {
        if (name == First) return true;
        return SecondaryWindow.IsMain(name) && FirstAway;
    }

    /// <summary>A window opened, by its name; only another main window is counted.</summary>
    public void Opened(string name)
    {
        if (!SecondaryWindow.IsMain(name)) return;
        lock (_gate) _others.Add(name);
    }

    /// <summary>
    /// The first main window is closing. True: put it away rather than close it, since the close is the person's and another
    /// main window is open. False: let it close, which ends the application.
    /// </summary>
    /// <param name="byPerson">
    /// The person's close (the caption's button, Alt+F4, the taskbar's), never one from code: the kit reports both as
    /// <c>UserClosing</c>, so the window says which (an update's close and the last main window's are for good).
    /// </param>
    public bool FirstClosing(bool byPerson)
    {
        int others;
        lock (_gate)
        {
            if (!byPerson || _ending || _others.Count == 0)
            {
                _ending = true;
                return false;
            }

            _away = true;
            _cameBack = false;
            others = _others.Count;
        }

        log?.Info("window.away", ("others", others));
        AwayChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// A window closed, by its name. True when that ends the application: it was the last main window, the first having been
    /// put away; the end is asked for here, once.
    /// </summary>
    public bool Closed(string name)
    {
        lock (_gate)
        {
            if (!_others.Remove(name) || !_away || _ending || _others.Count > 0) return false;
            _ending = true;
        }

        log?.Info("window.last", ("window", name));
        end?.Invoke();
        return true;
    }

    /// <summary>
    /// The first main window became visible. Shown while still put away, nobody of Daoris's took it back first: the kit
    /// brought it forward for a later launch, which reads that as its window (<see cref="TakeBackFirst"/>).
    /// </summary>
    public void Shown()
    {
        lock (_gate)
        {
            if (!_away) return;
            _away = false;
            _cameBack = true;
        }

        AwayChanged?.Invoke();
    }

    /// <summary>
    /// The person asks for a window back, by a later launch or the tray: true where the first was put away (or the kit has
    /// just shown it for this launch), which is now theirs again and is the one to show, so no other opens. Once.
    /// </summary>
    public bool TakeBackFirst()
    {
        bool wasAway;
        lock (_gate)
        {
            if (_ending || (!_away && !_cameBack)) return false;
            wasAway = _away;
            _away = false;
            _cameBack = false;
        }

        if (wasAway) AwayChanged?.Invoke();
        return true;
    }
}
