using System.Runtime.InteropServices;
using Shenora.Core.Events;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// Bring another main window (<c>main:&lt;n&gt;</c>) in front of the person by its name, from any thread, never blocking (NOTIFY2c):
/// false where it is not open. Resolved at call time, since the windows are made after the notifier is.
/// </summary>
public delegate bool BringForward(string window);

/// <summary>
/// The OS notification (SURF5b, working-surface design §4) — a session parked, or one that ended
/// without the person asking for it, or a quest parked on its failed sessions (SESSUX1i).
/// </summary>
/// <remarks>
/// <para><b>This closes driver design open question 5.</b> That question was whether a session
/// parking while the window is closed should reach anybody, and its answer said the shell would have
/// to grow its own toast because the runtime deliberately never learns what an operation is. It has.
/// `TrayIcon` carries a menu and no balloon, so this is a <see cref="NotifyIcon"/> of its own —
/// which is also what §4 means by "the shell's own code over the window toolkit it already has".</para>
///
/// <para><b>It decides nothing about what is worth saying.</b> `AttentionWatch` in the driver does,
/// so a machine with no screen reaches the same answer (D50) and the judgement is testable where a
/// balloon is not. This is delivery, and only delivery.</para>
///
/// <para><b>It is quiet while the person is looking.</b> The same event reaches the page over the
/// bridge, which raises the platform's own toast — so an OS balloon on top of that would be the same
/// news twice. One of this application's windows in the foreground is the test, because that is what "they are
/// already looking" actually means.</para>
///
/// <para>🔴 <b>The icon has to be visible for a balloon to show at all</b>, so Daoris now has a tray
/// icon. It is not close-to-tray: this window still closes when it is closed, and changing that
/// would be a lifecycle change nobody asked for.</para>
/// </remarks>
public sealed class SessionNotifier : IDisposable
{
    private readonly Form _window;
    private readonly NotifyIcon _icon;
    private readonly IDisposable _subscription;

    /// <summary>The session the last balloon was about — what clicking it opens.</summary>
    private string? _about;

    /// <summary>The quest the last balloon was about, where it names one (a quest's park): its workspace where its session's is not known.</summary>
    private string? _aboutQuest;

    /// <summary>Asked before the window is shown: a window put away is taken back first (MAINWINDOWS2). Null asks nothing.</summary>
    private readonly Action? _takingBack;

    /// <summary>Which main window a balloon's click opens, by the session and quest it is about (NOTIFY2c). Null: this window.</summary>
    private readonly Func<string?, string?, string>? _picking;

    /// <summary>Brings another main window forward by its name (NOTIFY2c); false where it is not open. Null: none is.</summary>
    private readonly BringForward? _forwarding;

    /// <summary>The words a main window's page handed, in its language (NOTIFY2d); null, or none told yet, says English.</summary>
    private readonly NoticeWords? _words;

    /// <param name="window">The first main window: the tray's, and what a click opens where no other is picked.</param>
    /// <param name="events">The bus the attention is told on.</param>
    /// <param name="takingBack">Asked before the first is shown, so a window put away is taken back first (MAINWINDOWS2).</param>
    /// <param name="picking">
    /// The main window a balloon's click opens (NOTIFY2c, the notification design §9): the one whose workspace holds what it is about,
    /// or the main window last in front where none or several do (<see cref="MainWindowScopes"/>).
    /// </param>
    /// <param name="forwarding">Brings another main window forward by its name.</param>
    /// <param name="words">
    /// The words each balloon is said in, as a main window's page handed them over <c>SET_NOTICE_WORDS</c> (NOTIFY2d, the notification
    /// design §3.3): read at each balloon, so a change of language is said from the next.
    /// </param>
    public SessionNotifier(
        Form window, IEventBus events, Action? takingBack = null, Func<string?, string?, string>? picking = null, BringForward? forwarding = null,
        NoticeWords? words = null)
    {
        _window = window;
        _takingBack = takingBack;
        _picking = picking;
        _forwarding = forwarding;
        _words = words;
        _icon = new NotifyIcon
        {
            // The window's own icon where it has one, so the tray matches the taskbar.
            Icon = window.Icon ?? SystemIcons.Application,
            Text = "Daoris",
            Visible = true,
        };

        // Double-click is the gesture every tray icon in Windows has taught people: the first main window, taken back where it was
        // put away (MAINWINDOWS2). A balloon's click opens the main window holding what it is about (NOTIFY2c).
        _icon.DoubleClick += (_, _) => Show(null);
        _icon.BalloonTipClicked += (_, _) => Open(_about, _aboutQuest);

        _subscription = events.Subscribe("DAORIS", "SESSION_ATTENTION", message =>
        {
            Raise(message.Payload);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Show a balloon for one attention event.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Marshalled, and never blocking.</b> The bus delivers on whatever thread emitted, and a
    /// <see cref="Control.Invoke"/> from the driver's loop thread into a UI thread that is waiting on
    /// the same loop is a deadlock. <c>BeginInvoke</c> is the same rule every window in this shell
    /// follows.
    /// </remarks>
    private void Raise(object? payload)
    {
        if (payload is null || _window.IsDisposed || !_window.IsHandleCreated) return;

        var headline = Read(payload, "Headline");
        if (string.IsNullOrWhiteSpace(headline)) return;

        var session = Read(payload, "Session");
        var quest = Read(payload, "Quest");
        var facts = new BalloonFacts(Read(payload, "Kind"), headline, Read(payload, "Detail"))
        {
            Repository = Read(payload, "Repository"),
            State = Read(payload, "State"),
            Quest = quest,
            Strikes = payload.GetType().GetProperty("Strikes")?.GetValue(payload) as int?,
            Module = Read(payload, "Module"),
            Route = Read(payload, "Route"),
        };

        _window.BeginInvoke(() =>
        {
            // Quiet while they are already looking — the page's own toast has it.
            if (IsLookingAtIt()) return;

            _about = session;
            _aboutQuest = quest;
            // Its words and icon by kind are the modules' judgement (NOTIFY1d), in the words the page handed (NOTIFY2d): the window's
            // language, the event's facts filled in, what a session wrote as written; English where no page has handed any.
            var said = SessionBalloon.Words(facts, _words?.Now);
            _icon.ShowBalloonTip(
                // Ignored by Windows since Vista, which picks its own duration; passed because the
                // overload requires it.
                timeout: 10_000,
                tipTitle: said.Title,
                tipText: said.Body,
                tipIcon: said.Icon == BalloonIcon.Warning ? ToolTipIcon.Warning : ToolTipIcon.Info);
        });
    }

    /// <summary>
    /// A balloon's click (NOTIFY2c): the main window whose workspace holds what it was about comes forward on it, or the main window
    /// last in front where none or several do; the first main window, as before, where that is the one or another cannot come.
    /// </summary>
    private void Open(string? session, string? quest)
    {
        var window = _picking?.Invoke(session, quest) ?? MainWindows.First;
        if (window != MainWindows.First && _forwarding?.Invoke(window) == true)
        {
            if (session is { Length: > 0 } id) Attend?.Invoke(id, window);
            return;
        }

        Show(session);
    }

    /// <summary>Bring the first main window forward, and attend that session if one was named.</summary>
    /// <remarks>
    /// A notification is a door (design §4: every attention row is one), so clicking it lands on the
    /// session rather than on whatever the person last had open. The page is told over the bridge,
    /// because which session is attended is the page's state to hold.
    /// </remarks>
    private void Show(string? session)
    {
        if (_window.IsDisposed) return;

        // The person asked for it: where the first main window was put away it is theirs again (MAINWINDOWS2), said before it
        // is shown, so its showing is not read as a later launch's.
        _takingBack?.Invoke();

        // In the order `MainForm.Forward` gives: un-minimized before it is activated, then brought forward on the OS's side.
        MainForm.Forward(_window);

        if (session is { Length: > 0 } id) Attend?.Invoke(id, MainWindows.First);
    }

    /// <summary>
    /// Asked to attend a session, from a balloon the person clicked: the session, and the main window brought forward on it, whose page
    /// alone attends it (NOTIFY2c).
    /// </summary>
    public event Action<string, string>? Attend;

    /// <summary>
    /// Whether the person is already looking at one of this application's windows — the test for
    /// staying quiet.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Focused is not enough; it must also be on screen.</b> A minimized window can still be
    /// the foreground window for a moment after it is minimized (measured), and "minimized" is the
    /// most obvious case of nobody looking — so a foreground test alone would swallow the balloon in
    /// exactly the situation this whole feature exists for. <c>IsIconic</c> is the other half.
    /// </remarks>
    private bool IsLookingAtIt()
    {
        try
        {
            // 🔴 ANY of this application's windows (REV3): a secondary window — the monitor, a detached
            // session — carries the same page, which raises its own toast, so testing the main window
            // alone sent the same news twice, as a balloon and as a toast.
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || IsIconic(foreground)) return false;
            GetWindowThreadProcessId(foreground, out var owner);
            return owner == (uint)Environment.ProcessId;
        }
        catch
        {
            // Never a reason not to notify: erring toward saying something is the safe half here.
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    /// <summary>
    /// One field off the anonymous payload the loop emitted.
    /// </summary>
    /// <remarks>
    /// Reflection rather than a shared type, because the payload crosses the bus as <c>object</c> and
    /// the loop that builds it lives in a package this one references rather than shares a contract
    /// with. A renamed field therefore reads as absent, which is why the headline being empty means
    /// "say nothing" rather than "show a blank balloon".
    /// </remarks>
    private static string? Read(object payload, string name) =>
        payload.GetType().GetProperty(name)?.GetValue(payload) as string;

    public void Dispose()
    {
        _subscription.Dispose();
        // Hide before releasing, or the shell keeps a ghost icon until somebody hovers over it.
        _icon.Visible = false;
        _icon.Dispose();
    }
}
