using System.Runtime.InteropServices;
using Shenora.Core.Events;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// The OS notification (SURF5b, working-surface design §4) — a session parked, or one that ended
/// without the person asking for it.
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

    public SessionNotifier(Form window, IEventBus events)
    {
        _window = window;
        _icon = new NotifyIcon
        {
            // The window's own icon where it has one, so the tray matches the taskbar.
            Icon = window.Icon ?? SystemIcons.Application,
            Text = "Daoris",
            Visible = true,
        };

        // Double-click is the gesture every tray icon in Windows has taught people.
        _icon.DoubleClick += (_, _) => Show(null);
        _icon.BalloonTipClicked += (_, _) => Show(_about);

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

        var detail = Read(payload, "Detail");
        var session = Read(payload, "Session");
        var parked = string.Equals(Read(payload, "Kind"), "Parked", StringComparison.Ordinal);

        _window.BeginInvoke(() =>
        {
            // Quiet while they are already looking — the page's own toast has it.
            if (IsLookingAtIt()) return;

            _about = session;
            _icon.ShowBalloonTip(
                // Ignored by Windows since Vista, which picks its own duration; passed because the
                // overload requires it.
                timeout: 10_000,
                tipTitle: headline,
                // A balloon with an empty body renders as a title with a gap under it.
                tipText: string.IsNullOrWhiteSpace(detail)
                    ? (parked ? "Open Daoris to answer it." : "Open Daoris to see what it did.")
                    : detail,
                // Warn for a park: it is the one that is WAITING on somebody. An ending is news.
                tipIcon: parked ? ToolTipIcon.Warning : ToolTipIcon.Info);
        });
    }

    /// <summary>Bring the window forward, and attend that session if one was named.</summary>
    /// <remarks>
    /// A notification is a door (design §4: every attention row is one), so clicking it lands on the
    /// session rather than on whatever the person last had open. The page is told over the bridge,
    /// because which session is attended is the page's state to hold.
    /// </remarks>
    private void Show(string? session)
    {
        if (_window.IsDisposed) return;

        // 🔴 THE ORDER IS THE PART PEOPLE GET WRONG, and the framework's own `WindowActivation`
        // documents it — un-minimize BEFORE activating, because activating a minimized window leaves
        // it minimized; then the managed show/activate; then `SetForegroundWindow` for the OS side,
        // without which restoring while another app holds the foreground leaves the window behind
        // everything, visible only in the taskbar. That type is internal to the package, so the
        // sequence is written out here rather than reached for.
        if (_window.WindowState == FormWindowState.Minimized) _window.WindowState = FormWindowState.Normal;
        _window.Show();
        _window.Activate();
        _window.BringToFront();
        SetForegroundWindow(_window.Handle);

        if (session is { Length: > 0 } id) Attend?.Invoke(id);
    }

    /// <summary>Asked to attend a session, from a balloon the person clicked.</summary>
    public event Action<string>? Attend;

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

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
