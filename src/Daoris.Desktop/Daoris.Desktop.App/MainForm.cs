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
public sealed class MainForm : Form
{
    private static readonly Color Paper = Color.FromArgb(30, 30, 30);

    private readonly WebViewHost _host;
    private readonly WebView2Control _webView;
    private readonly WebViewIpcBridge _bridge;
    private readonly SplashPanel _splash;
    private readonly DriverLoop _driver;
    private readonly HostSupervisor _supervisor;

    public MainForm(
        WebViewHostOptions hostOptions,
        IMessageDispatcher dispatcher,
        IEventBus eventBus,
        DriverLoop driver,
        HostSupervisor supervisor)
    {
        _driver = driver;
        _supervisor = supervisor;

        Text = "Daoris (道衍)";
        MinimumSize = new Size(960, 600);
        BackColor = Paper;

        _webView = new WebView2Control { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        _splash = new SplashPanel(new SplashPanelOptions { BackColor = Paper });
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
        Load += async (_, _) => await BringUpAsync();
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

        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = message,
            ForeColor = Color.FromArgb(220, 220, 224),
            BackColor = Paper,
            Padding = new Padding(28),
            Font = new Font("Segoe UI", 10.5f),
        });
        Controls[^1].BringToFront();
    }
}
