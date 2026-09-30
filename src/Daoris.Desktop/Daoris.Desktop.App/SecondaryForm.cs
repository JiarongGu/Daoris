using Microsoft.Extensions.Logging;
using Shenora.Chromium;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// A secondary window (D55 §b, SURF8): the same platform bundle, at its own route, in a window of
/// its own — the monitor on a second screen, or one session detached.
/// </summary>
/// <remarks>
/// <para><b>Native chrome, deliberately.</b> The main window is frameless because its app strip IS
/// the title bar (SURF7); this one is not, because <c>WindowCommandModule</c> targets one form and
/// its module name is reserved and singular. Hand-rolling a second dispatcher so a utility window
/// could lose its close button's OS behaviour is a lot of machinery for a loss.</para>
///
/// <para><b>Its page on the same engine, the same dispatcher and the same bus</b> (D92): a
/// <c>ChromiumView</c> bridges its page's IPC itself, on this window's own thread. Requests go to the same
/// modules the main window talks to, and every event emitted on the bus reaches every page — which is
/// how a second window watches the same live console without the driver keeping a second buffer (SES1:
/// the buffer holds no cursor, so a reader is a position and not a registration). Its page's window
/// commands act on this window, not the main one.</para>
/// </remarks>
public sealed class SecondaryForm : OptimizedForm
{
    private readonly ChromiumView _view;
    private readonly Microsoft.Win32.UserPreferenceChangedEventHandler _themeChanged;
    private bool _pageSaid;
    private bool _pageDark;

    public SecondaryForm(
        string name,
        string page,
        ChromiumEngine engine,
        ILogger<ChromiumView> log)
        : base(new OptimizedFormOptions
        {
            // Framed, which is the whole point (D55 §b) — but the DWM dark-mode flag and the border
            // colour are still ours to set, and a light title bar over a dark page is the most
            // visible thing in the window.
            FramelessChrome = false,
            BackColor = ChromePalette.For(MainForm.OperatingSystemPrefersDark()).Page,
            DwmBorderColor = ChromePalette.For(MainForm.OperatingSystemPrefersDark()).Line,
            ImmersiveDarkMode = MainForm.OperatingSystemPrefersDark(),
        })
    {
        // The taskbar's label and the window's own caption — this one HAS a caption, so it is read.
        Text = name == SecondaryWindow.Monitor ? "Daoris — Monitor" : $"Daoris — {name}";
        StartPosition = FormStartPosition.CenterScreen;

        // The main window's taskbar identity (TASKBAR1, D108), or this window would be a button of its
        // own beside the main one's, grouped by the executable the main one no longer is.
        TaskbarWindow.Wear(this);

        // The OS theme, until the page says which one it is in (WINDOW2: `FollowPage`, over
        // `DAORIS.WINDOWS`). Shenora's window commands route a second window's `SET_THEME` nowhere,
        // so the page tells this form by its name instead. Before it has spoken — the moments while
        // the page comes up — the OS is the best guess, and a person who switches their OS theme
        // with the page not yet up still gets a matching title bar.
        _themeChanged = (_, changed) =>
        {
            if (changed.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
            // Once the page has said, it decides: it pushes again when the OS changes and its choice
            // is the system's, and an explicit choice must not be undone by the OS turning.
            if (_pageSaid) return;
            // BeginInvoke, never Invoke: this arrives on the system-events thread and every window
            // here runs its own pump — a blocking marshal is the deadlock the framework warns about.
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() => Theme(MainForm.OperatingSystemPrefersDark()));
        };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += _themeChanged;

        // The same bytes the main window shows, with one query parameter saying which window this is —
        // a route into one bundle, never a second frontend (D38's one UI). Its browser opens as the
        // view's handle is created, on this window's own thread.
        _view = new ChromiumView(engine, log) { Dock = DockStyle.Fill, Path = page };
        Controls.Add(_view);

        Load += async (_, _) => await BringUpAsync();
    }

    /// <summary>
    /// The theme the page in this window is in (WINDOW2) — the viewer's choice where there is one —
    /// painted on the caption and border the page cannot reach. Called on this window's thread.
    /// </summary>
    public void FollowPage(bool dark)
    {
        _pageSaid = true;
        _pageDark = dark;
        Theme(dark);
    }

    /// <summary>
    /// Paint the theme on this window's own frame: the background under the page, and the caption and
    /// border the OS draws.
    /// </summary>
    /// <remarks>
    /// 🔴 <c>OptimizedForm.ApplyChromeTheme</c> sets the DWM caption only for a FRAMELESS form (Shenora
    /// 0.16 and 0.17 alike: <c>if (_options.FramelessChrome &amp;&amp; IsHandleCreated)</c>), and this one is
    /// framed, so its caption never followed any theme — not the OS's either, whatever the handler
    /// above intended. Found by looking at WINDOW2's fix, which set a theme nothing painted. So the
    /// attribute is set here, on this window's own handle, and the frame is told to repaint: a caption
    /// already on screen keeps its old colours until the next activation otherwise.
    /// </remarks>
    private void Theme(bool dark)
    {
        var palette = ChromePalette.For(dark);
        ApplyChromeTheme(palette.Page, palette.Line, dark);
        if (!IsHandleCreated) return;
        var flag = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(Handle, DwmImmersiveDarkMode, ref flag, sizeof(int));
        var border = palette.Line.R | (palette.Line.G << 8) | (palette.Line.B << 16);
        _ = DwmSetWindowAttribute(Handle, DwmBorderColor, ref border, sizeof(int));
        _ = SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, FrameChanged);
    }

    /// <summary>The OS's theme on arrival, painted as soon as there is a window to paint.</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme(_pageSaid ? _pageDark : MainForm.OperatingSystemPrefersDark());
    }

    private const int DwmImmersiveDarkMode = 20;
    private const int DwmBorderColor = 34;
    /// <summary><c>SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED</c>.</summary>
    private const uint FrameChanged = 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    /// <summary>
    /// Unhook the system-events handler. <c>SystemEvents</c> holds a STATIC list, so a window that
    /// closed without unsubscribing is kept alive by it — and every monitor the person ever opened
    /// would still be handling theme changes.
    /// </summary>
    /// <remarks>
    /// 🔴 A closed window's page must stop hearing the bus (REV3: a WebView2 bridge left attached queued
    /// every later event, up to its cap, once per window ever opened). The view closes its browser, and
    /// its page's IPC with it, as its handle goes; disposing it here makes that happen with the window.
    /// </remarks>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= _themeChanged;
        _view.Dispose();
        base.OnFormClosed(e);
    }

    private async Task BringUpAsync()
    {
        try
        {
            if (_view.Browser is { } browser) await browser.Created;
        }
        catch (Exception error)
        {
            // Never a silent dark window. This one is a utility, so the failure is a sentence in it
            // rather than a dialog: closing it is the whole recovery, and the main window is
            // unaffected either way.
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = error.Message,
                ForeColor = ChromePalette.For(MainForm.OperatingSystemPrefersDark()).Ink,
                Padding = new Padding(24),
                Font = new Font("Segoe UI", 10.5f),
            });
        }
    }
}
