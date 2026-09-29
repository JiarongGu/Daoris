using Shenora.Core.Events;
using Shenora.Core.Ipc;
using Shenora.Windows;
using WebView2Control = Microsoft.Web.WebView2.WinForms.WebView2;

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
/// <para><b>Its own bridge, on the shared dispatcher and the shared bus.</b> Requests go to the same
/// modules the main window talks to, and every event emitted on the bus reaches every attached
/// bridge — which is how a second window watches the same live console without the driver keeping
/// a second buffer (SES1: the buffer holds no cursor, so a reader is a position and not a
/// registration).</para>
///
/// <para>🔴 <b>The bridge is constructed before init and attached after it</b>, in that order — the
/// runtime's load-bearing sequence, so events emitted during the slow WebView2 initialization are
/// buffered rather than lost. The main window does exactly the same thing for the same reason.</para>
/// </remarks>
public sealed class SecondaryForm : OptimizedForm
{
    private readonly WebViewHost _host;
    private readonly WebView2Control _webView;
    private readonly WebViewIpcBridge _bridge;
    private readonly Microsoft.Win32.UserPreferenceChangedEventHandler _themeChanged;
    private bool _pageSaid;
    private bool _pageDark;

    public SecondaryForm(
        string name,
        string address,
        IMessageDispatcher dispatcher,
        IEventBus events,
        WebViewEnvironmentOptions environment)
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
        var palette = ChromePalette.For(MainForm.OperatingSystemPrefersDark());

        // The taskbar's label and the window's own caption — this one HAS a caption, so it is read.
        Text = name == SecondaryWindow.Monitor ? "Daoris — Monitor" : $"Daoris — {name}";
        StartPosition = FormStartPosition.CenterScreen;

        // The OS theme, until the page says which one it is in (WINDOW2: `FollowPage`, over
        // `DAORIS.WINDOWS`). Shenora's window commands route a second window's `SET_THEME` nowhere,
        // so the page tells this form by its name instead. Before it has spoken — the moments while
        // WebView2 comes up — the OS is the best guess, and a person who switches their OS theme
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

        _webView = new WebView2Control { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        _bridge = new WebViewIpcBridge(_webView, new WebViewIpcBridgeOptions
        {
            Dispatcher = dispatcher,
            EventBus = events,
            Shell = new ShellInfo { Name = "daoris-desktop", Capabilities = [] },
        });

        _host = new WebViewHost(_webView, new WebViewHostOptions
        {
            Environment = environment,
            // 🔴 A `CoreWebView2Environment` is AFFINE TO THE THREAD THAT CREATED IT, and this
            // window runs its own STA pump — so it must build its own on the calling thread rather
            // than borrow the main window's. Sharing it opens the window and then fails its
            // bring-up with "CoreWebView2Environment members can only be accessed from the UI
            // thread", which is a sentence in an otherwise empty window and nothing in a log.
            // Same options and same user-data folder, so the two environments still share one
            // browser process — this costs a handle, not a browser.
            UseSharedEnvironment = false,
            // The same bytes the main window shows, with one query parameter saying which window
            // this is — a route into one bundle, never a second frontend (D38's one UI).
            ProductionUrl = address,
            DevUrl = address,
            BackgroundColor = palette.Page,
        });

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
    /// 🔴 And dispose the bridge (REV3). It subscribes to the WHOLE bus, and its flush timer lived on this
    /// window's thread, which ends here — so a closed window's bridge queued every later event, the
    /// sessions' console lines and conversations included, up to its ten-thousand cap, for as long as
    /// the application ran. Once per window the person ever opened.
    /// </remarks>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= _themeChanged;
        _bridge.Dispose();
        base.OnFormClosed(e);
    }

    private async Task BringUpAsync()
    {
        try
        {
            await _host.InitializeAsync();
            _bridge.Attach();
            _host.Navigate();
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
