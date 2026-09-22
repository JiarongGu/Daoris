using Shenora.Core.Events;
using Shenora.Core.Ipc;
using Shenora.Core.Shell;
using Shenora.Windows;
using WebView2Control = Microsoft.Web.WebView2.WinForms.WebView2;

namespace Daoris.Desktop;

/// <summary>
/// The one window: the platform the HTTP host serves, in a WebView — the same bytes a browser gets,
/// which is the "one UI, two shells" rule holding (D38). The bridge is wired in the runtime's
/// load-bearing order: constructed before init so early events buffer, attached after init and before
/// navigation so the page's first messages are never lost.
/// </summary>
/// <remarks>
/// <para><b>It is frameless, and the page's app strip IS the title bar</b> (SURF7 / D56). No OS
/// caption: the strip drags the window, double-click maximizes it, and the room it reserves at its
/// right edge is handed to the OS as real caption buttons — which <b>this window paints</b>
/// (<c>NativeCaptionButtons</c>), not the page. D56 as amended has the reason.</para>
///
/// <para>🔴 <b><see cref="Form.WindowState"/> is a lie about this window.</b> The frameless maximize
/// is manual — sized to the monitor work area, keeping <c>WindowState.Normal</c> — so
/// <see cref="OptimizedForm.AppPlacement"/> is the truth. Reading the WinForms property instead makes
/// restore a permanent no-op, which is why the framework's own window-state stack reads
/// <c>IAppMaximizable</c> and why the IPC module is handed the same answer.</para>
/// </remarks>
public sealed class MainForm : OptimizedForm
{
    private readonly WebViewHost _host;
    private readonly WebView2Control _webView;
    private readonly WebViewIpcBridge _bridge;
    private readonly SplashPanel _splash;
    private readonly DriverLoop _driver;
    private readonly HostSupervisor _supervisor;
    private readonly SessionNotifier _notifier;
    private ChromePalette _palette;
    private Label? _trouble;

    public MainForm(
        WebViewHostOptions hostOptions,
        IMessageDispatcher dispatcher,
        IEventBus eventBus,
        DriverLoop driver,
        HostSupervisor supervisor)
        : base(new OptimizedFormOptions
        {
            FramelessChrome = true,
            // The window owns the caption pixels and paints them (D56 as amended by SURF7's reading
            // of the framework). Inert until the page reports where it left the room.
            NativeCaptionButtons = true,
            BackColor = StartingPalette.Page,
            // The 1px Win11 border line. The system default is light, which is visibly wrong on the
            // dark theme and slightly wrong on the light one.
            DwmBorderColor = StartingPalette.Line,
            ImmersiveDarkMode = StartingPalette.IsDark,
        })
    {
        _driver = driver;
        _supervisor = supervisor;
        _palette = StartingPalette;

        // Still set: it is the taskbar's label and the caption of the one MessageBox left.
        Text = "Daoris (道衍)";

        // 🔴 The WINDOW's icon, which `ApplicationIcon` alone does not set: that property gives the
        // executable its face in Explorer and the taskbar, and a WinForms form still opens wearing
        // the framework's default unless told otherwise. Taken from the running executable rather
        // than embedded a second time, so the two can never disagree.
        //
        // It also fixes the tray: `SessionNotifier` reads `window.Icon ?? SystemIcons.Application`,
        // so every balloon this app raised wore a generic Windows glyph until now.
        try
        {
            if (Environment.ProcessPath is { Length: > 0 } self)
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(self);
            }
        }
        catch (Exception)
        {
            // A face is not worth failing to open a window over. The default is still a face.
        }
        MinimumSize = new Size(960, 600);
        CaptionButtonColors = CaptionColors(_palette);

        _webView = new WebView2Control { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        _splash = new SplashPanel(new SplashPanelOptions { BackColor = _palette.Page });
        Controls.Add(_splash);
        _splash.BringToFront();

        _bridge = new WebViewIpcBridge(_webView, new WebViewIpcBridgeOptions
        {
            Dispatcher = dispatcher,
            EventBus = eventBus,
            // Capabilities are promises the page may render buttons for, so only what is actually
            // mapped is advertised. The driver's controls deliberately do NOT ride this list: the page
            // gates them on the bridge being present and DAORIS.DRIVER answering STATE, which is the
            // stronger test — a capability string could outlive the module it promises.
            Shell = new ShellInfo { Name = "daoris-desktop", Capabilities = [] },
        });

        _host = new WebViewHost(_webView, hostOptions);

        // 🔴 MAPPED LATE, from where the window is created, because this facade needs the LIVE form —
        // never from the dispatcher's configure callback, which runs at provider-build time when no
        // form exists. Late mapping is safe while requests are in flight.
        dispatcher.MapModule(new WindowCommandModule(new WindowCommandOptions
        {
            Window = this,
            // The manual path, both times. The defaults toggle and read `Form.WindowState`, which is
            // correct for a framed window and wrong for this one on every call.
            ToggleMaximize = ToggleMaximize,
            IsMaximized = () => AppPlacement == WindowPlacement.Maximized,
            ApplyTheme = ApplyTheme,
            SetCaptionButtons = SetCaptionButtons,
            // The page's rectangles are relative to the WEB VIEW, and its DeviceDpi is what converts
            // them — per-monitor, so a process-wide scale would be wrong on a mixed-DPI desktop.
            CoordinateSpace = _webView,
        }));

        // The OS notification (SURF5b), which closes driver design open question 5: a session that
        // parks while nobody is looking at this window now reaches the person. It decides nothing —
        // `AttentionWatch` in the driver does, so a machine with no screen answers the same question.
        _notifier = new SessionNotifier(this, eventBus);
        _notifier.Attend += session => eventBus.Emit(
            new EventMessage { Module = "DAORIS", Type = "ATTEND_SESSION", Payload = new { Session = session } });

        Load += async (_, _) => await BringUpAsync();
        FormClosed += (_, _) => _notifier.Dispose();
    }

    /// <summary>What the OS is set to, for the first paint — the page corrects it a round trip later.</summary>
    private static ChromePalette StartingPalette { get; } = ChromePalette.For(OperatingSystemPrefersDark());

    /// <summary>
    /// The page's effective theme, arriving over <c>SET_THEME</c> on every change.
    /// </summary>
    /// <remarks>
    /// Not optional for an app that follows the OS theme: the DWM border, the form fill and the
    /// caption buttons are painted natively and never see the page's CSS, so without this a runtime
    /// light↔dark switch leaves a window wearing half of each.
    /// </remarks>
    private void ApplyTheme(bool dark)
    {
        _palette = ChromePalette.For(dark);
        CaptionButtonColors = CaptionColors(_palette);
        ApplyChromeTheme(_palette.Page, _palette.Line, immersiveDarkMode: dark);

        if (!_splash.IsDisposed) _splash.BackColor = _palette.Page;
        if (_trouble is { IsDisposed: false })
        {
            _trouble.BackColor = _palette.Page;
            _trouble.ForeColor = _palette.Ink;
        }
    }

    private static CaptionButtonColors CaptionColors(ChromePalette palette) => new()
    {
        Surface = palette.Surface,
        Hover = palette.Hover,
        Pressed = palette.Pressed,
        Glyph = palette.Ink,
        CloseHover = palette.CloseHover,
        ClosePressed = palette.ClosePressed,
        CloseGlyphHot = palette.CloseGlyphHot,
    };

    /// <summary>
    /// Whether the OS is in dark mode. Only for the first paint: the page is authoritative and says
    /// so within a round trip.
    /// </summary>
    /// <remarks>
    /// Dark is the fallback when the value is missing or the hive is unreadable — it was this shell's
    /// fill before any of this, and a light flash on a dark desktop is the more jarring mistake.
    /// </remarks>
    internal static bool OperatingSystemPrefersDark()
    {
        try
        {
            var value = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                null);

            return value is not int light || light == 0;
        }
        catch
        {
            // A policy-locked hive is not a reason to fail to open a window.
            return true;
        }
    }

    private async Task BringUpAsync()
    {
        if (!WebViewEnvironment.IsRuntimeAvailable())
        {
            MessageBox.Show(
                "The WebView2 Runtime is not installed.\n\n"
                + "Install the Evergreen WebView2 Runtime from Microsoft and start Daoris again.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        try
        {
            await _host.InitializeAsync();
            _bridge.Attach();

            // Navigate only once the service answers — a WebView pointed at a port nobody holds
            // renders a browser error page, which reads as "the app is broken" rather than "the
            // host is still starting".
            if (!await _driver.HostReady)
            {
                ShowTrouble(_supervisor.Trouble ?? "The local service host did not come up.");
                return;
            }

            _webView.CoreWebView2.NavigationCompleted += (_, completed) =>
            {
                if (!_splash.IsDisposed)
                {
                    Controls.Remove(_splash);
                    _splash.Dispose();
                }

                if (!completed.IsSuccess)
                {
                    // Never a silent dark window: say what failed, name the likely cause.
                    ShowTrouble(
                        $"The platform failed to load ({completed.WebErrorStatus}).\n\n"
                        + "The service host answered its status probe but did not serve the page — "
                        + "check that its bundle was built (npm --prefix src/Daoris.Web run build).");
                }
            };
            _host.Navigate();
        }
        catch (Exception error)
        {
            ShowTrouble(error.Message);
        }
    }

    /// <summary>
    /// Trouble is a page, never a modal. A MessageBox owned by this window DISABLES it — measured:
    /// the close button stops working, the app cannot be exited until someone finds the dialog, and
    /// posting WM_CLOSE reports success while doing nothing. The one modal kept is the WebView2
    /// runtime check, which closes the app immediately after.
    /// </summary>
    private void ShowTrouble(string message)
    {
        if (!_splash.IsDisposed)
        {
            Controls.Remove(_splash);
            _splash.Dispose();
        }

        _trouble = new Label
        {
            Dock = DockStyle.Fill,
            Text = message,
            ForeColor = _palette.Ink,
            BackColor = _palette.Page,
            // Clear of the strip: the window has no title bar, so text docked to the top would start
            // under the caption buttons the window paints there.
            Padding = new Padding(28, 56, 28, 28),
            Font = new Font("Segoe UI", 10.5f),
        };
        Controls.Add(_trouble);
        _trouble.BringToFront();
    }

    /// <summary>
    /// The native folder dialog, for adding a repository (D48 §7). Null is the person cancelling.
    /// </summary>
    /// <remarks>
    /// <para>This is the ONE thing the page cannot do for itself: a browser may never learn a path on
    /// this machine (D46/D47 §4), so the shell picks and the page registers what it was handed.</para>
    ///
    /// <para>Marshalled onto the UI thread, because an IPC request arrives on whatever thread the
    /// bridge dispatched it on and a dialog shown from anywhere else either throws or opens with no
    /// owner — a modal floating free of its window, which is the shape of a hang. Owned by this form
    /// for the same reason, so it cannot end up behind it.</para>
    /// </remarks>
    public string? PickFolder()
    {
        if (InvokeRequired) return (string?)Invoke(PickFolder);

        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a repository to add to this machine's registry",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : null;
    }
}
