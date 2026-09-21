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

        // 🔴 This window follows the OS THEME DIRECTLY, because it has no channel to be told.
        // `WindowCommandModule` — where the main window gets `SET_THEME` from — targets one form and
        // its module name is reserved and singular (D55 §b), so the page in here cannot reach its
        // own frame. Without this a person who switches their theme with a monitor open keeps a
        // light title bar over a dark page until they close and reopen it.
        _themeChanged = (_, changed) =>
        {
            if (changed.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
            // BeginInvoke, never Invoke: this arrives on the system-events thread and every window
            // here runs its own pump — a blocking marshal is the deadlock the framework warns about.
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() =>
            {
                var next = ChromePalette.For(MainForm.OperatingSystemPrefersDark());
                ApplyChromeTheme(next.Page, next.Line, MainForm.OperatingSystemPrefersDark());
            });
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
    /// Unhook the system-events handler. <c>SystemEvents</c> holds a STATIC list, so a window that
    /// closed without unsubscribing is kept alive by it — and every monitor the person ever opened
    /// would still be handling theme changes.
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= _themeChanged;
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
