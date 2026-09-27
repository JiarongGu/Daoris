using Microsoft.Web.WebView2.Core;
using Shenora.Windows;
using WebView2Control = Microsoft.Web.WebView2.WinForms.WebView2;

namespace Daoris.Desktop;

/// <summary>
/// Daoris's own browser (D78): one framed window with an address bar, in a WebView2 environment of
/// its own — the page the person signs in to and a session drives over CDP.
/// </summary>
/// <remarks>
/// <para>🔴 <b>No bridge, and never the app's environment.</b> A standard browser MCP attached over CDP
/// drove the app's own WebView2 and navigated its page away. The page holds the bridge, and an agent
/// under that would hold the machine. So this window has its own user-data folder, which is its own
/// browser process, and the debug port it opens reaches nothing else. It never builds a
/// <c>WebViewIpcBridge</c>.</para>
///
/// <para><b>Shown without taking focus when an agent brings it up.</b> The person opens it to sign in
/// and to watch. A session brings it up because a server needs its endpoint, and a window stealing
/// the keyboard mid-sentence would be the driver interrupting the person.</para>
///
/// <para>Native chrome, like the monitor (D55 §b), and on the OS theme for the same reason: it has no
/// channel to be told the viewer's choice (WINDOW2).</para>
/// </remarks>
public sealed class BrowserForm : OptimizedForm
{
    private const string AdditionalArgumentsVariable = "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";

    private readonly WebView2Control _webView;
    private readonly TextBox _address;
    private readonly string _profile;
    private readonly int _port;
    private readonly bool _activate;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The sign-in kept across a restart (BRW10): put back before the first page, kept after each.</summary>
    private readonly BrowserSessionCookies _cookies;

    /// <summary>Keeps the sign-in while the window is open, for what changes without a page loading.</summary>
    private readonly System.Windows.Forms.Timer _keeping = new() { Interval = 30_000 };

    private bool _keepingNow;

    /// <summary>
    /// Why a kept sign-in was not put back, under the bar until the person loads a page. Not the bar's
    /// placeholder: a bar the person just opened has the focus, and a focused box shows none.
    /// </summary>
    private readonly Label _notice;
    private readonly Panel _noticeStrip;

    /// <summary>Completes once the browser listens on its port — what a server's attach waits on.</summary>
    public Task Ready => _ready.Task;

    public BrowserForm(string profile, int port, bool activate, BrowserSessionCookies cookies)
        : base(new OptimizedFormOptions
        {
            FramelessChrome = false,
            BackColor = ChromePalette.For(MainForm.OperatingSystemPrefersDark()).Page,
            DwmBorderColor = ChromePalette.For(MainForm.OperatingSystemPrefersDark()).Line,
            ImmersiveDarkMode = MainForm.OperatingSystemPrefersDark(),
        })
    {
        _profile = profile;
        _port = port;
        _activate = activate;
        _cookies = cookies;
        var palette = ChromePalette.For(MainForm.OperatingSystemPrefersDark());

        Text = "Daoris — Browser";
        StartPosition = FormStartPosition.CenterScreen;

        _webView = new WebView2Control { Dock = DockStyle.Fill, DefaultBackgroundColor = palette.Page };

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = palette.Surface,
            Padding = new Padding(6, 5, 8, 5),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        bar.Controls.Add(Glyph("", "Back", palette, () => { if (_webView.CanGoBack) _webView.GoBack(); }), 0, 0);
        bar.Controls.Add(Glyph("", "Forward", palette, () => { if (_webView.CanGoForward) _webView.GoForward(); }), 1, 0);
        bar.Controls.Add(Glyph("", "Reload", palette, () => _webView.Reload()), 2, 0);

        _address = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = palette.Page,
            ForeColor = palette.Ink,
            Font = new Font("Segoe UI", 10f),
            Margin = new Padding(6, 3, 0, 3),
            AccessibleName = "Address",
            // An empty bar says what it is for: about:blank and data: pages report no address.
            PlaceholderText = "Type an address. What you sign in to here, sessions use.",
        };
        _address.KeyDown += (_, key) =>
        {
            if (key.KeyCode != Keys.Enter) return;
            key.SuppressKeyPress = true;
            // A page, or nothing: the bar is for web pages, and what is not one stays typed (D78).
            if (InAppBrowser.Address(_address.Text) is { } page && _webView.CoreWebView2 is { } core) core.Navigate(page);
        };
        bar.Controls.Add(_address, 3, 0);

        _notice = new Label
        {
            AutoSize = true,
            BackColor = palette.Surface,
            ForeColor = palette.Ink,
            Font = new Font("Segoe UI", 9.5f),
        };
        // In a strip of its own, the bar's colour across the window: a docked label that sizes to its
        // text paints only as far as the text goes (seen on the window).
        _noticeStrip = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Visible = false,
            BackColor = palette.Surface,
            Padding = new Padding(10, 0, 10, 6),
        };
        _noticeStrip.Controls.Add(_notice);

        // Docked from the last added: the bar at the top, the notice under it, the page in the rest.
        Controls.Add(_webView);
        Controls.Add(_noticeStrip);
        Controls.Add(bar);

        Load += async (_, _) => await BringUpAsync();
    }

    /// <summary>Brought up by a session, it does not take the keyboard from the person.</summary>
    protected override bool ShowWithoutActivation => !_activate;

    private static Button Glyph(string glyph, string name, ChromePalette palette, Action press)
    {
        // Sized from its font, never in pixels: a font is in points and follows the screen's scale, and
        // a 34px button was a sliver on a 200% screen (seen on the window).
        var button = new Button
        {
            Text = glyph,
            Font = new Font("Segoe Fluent Icons", 11f),
            FlatStyle = FlatStyle.Flat,
            ForeColor = palette.Ink,
            BackColor = palette.Surface,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6, 3, 6, 3),
            Margin = new Padding(0, 0, 2, 0),
            AccessibleName = name,
            TabStop = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = palette.Hover;
        button.FlatAppearance.MouseDownBackColor = palette.Pressed;
        button.Click += (_, _) => press();
        return button;
    }

    private async Task BringUpAsync()
    {
        try
        {
            var arguments = InAppBrowser.Arguments(_port);
            CoreWebView2Environment environment;

            // 🔴 The dev loop opens the APP's debug port through this variable, for the instruments, and
            // the variable reaches every environment this process creates. Left in place, this browser
            // would ask for the app's port as well, and one of the two would lose it. So for this one
            // creation the variable says this browser's port and nothing else, and it is put back at
            // once. A published build never sets it, and then it is not touched at all.
            var inherited = Environment.GetEnvironmentVariable(AdditionalArgumentsVariable);
            try
            {
                if (!string.IsNullOrEmpty(inherited)) Environment.SetEnvironmentVariable(AdditionalArgumentsVariable, arguments);
                environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: _profile,
                    options: new CoreWebView2EnvironmentOptions(additionalBrowserArguments: arguments));
            }
            finally
            {
                if (!string.IsNullOrEmpty(inherited)) Environment.SetEnvironmentVariable(AdditionalArgumentsVariable, inherited);
            }

            await _webView.EnsureCoreWebView2Async(environment);
            var core = _webView.CoreWebView2;

            // One page per window in v1 (D78 §3.2): a link that asks for a new window opens here.
            core.NewWindowRequested += (_, asked) =>
            {
                asked.Handled = true;
                if (InAppBrowser.Address(asked.Uri) is { } page) core.Navigate(page);
            };
            core.SourceChanged += (_, _) => _address.Text = core.Source;
            core.DocumentTitleChanged += (_, _) =>
                Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Daoris — Browser" : $"Daoris — Browser · {core.DocumentTitle}";

            // The sign-in back before the first page loads (BRW10), and kept after every page, which
            // is where a sign-in ends, and on a timer for what a page changes without loading.
            await RestoreAsync(core);
            core.NavigationCompleted += (_, _) =>
            {
                if (core.Source.StartsWith("http", StringComparison.OrdinalIgnoreCase)) _noticeStrip.Visible = false;
                Keep();
            };
            _keeping.Tick += (_, _) => Keep();
            _keeping.Start();

            core.Navigate("about:blank");
            _ready.TrySetResult();
            if (_activate) _address.Focus();
        }
        catch (Exception error)
        {
            _ready.TrySetException(new InvalidOperationException(
                $"the in-app browser did not come up: {error.Message}", error));
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

    /// <summary>
    /// Put the kept session cookies back, each one the browser does not hold already. One the browser
    /// refuses is skipped, and a file that could not be opened says so under the bar.
    /// </summary>
    private async Task RestoreAsync(CoreWebView2 core)
    {
        var kept = _cookies.Load();
        if (_cookies.Problem is { } problem)
        {
            _notice.Text = $"Signed out: {problem.TrimEnd('.')}. Sign in again, and it is kept from then on.";
            _noticeStrip.Visible = true;
        }
        if (kept.Count == 0) return;

        var present = (await core.CookieManager.GetCookiesAsync(string.Empty)).Select(Cookie);
        foreach (var cookie in BrowserSessionCookies.ToRestore(kept, present))
        {
            try
            {
                var restored = core.CookieManager.CreateCookie(cookie.Name, cookie.Value, cookie.Domain, cookie.Path);
                restored.IsSecure = cookie.Secure;
                restored.IsHttpOnly = cookie.HttpOnly;
                restored.SameSite = Enum.TryParse<CoreWebView2CookieSameSiteKind>(cookie.SameSite, out var kind)
                    ? kind
                    : CoreWebView2CookieSameSiteKind.Lax;
                core.CookieManager.AddOrUpdateCookie(restored);
            }
            catch (ArgumentException)
            {
                // A cookie the browser will not take back: the site asks for a sign-in, as before BRW10.
            }
        }
    }

    /// <summary>
    /// Keep the browser's session cookies now. A save that fails costs the next restart its sign-in,
    /// never the window: the person is in the middle of using it.
    /// </summary>
    private async void Keep()
    {
        if (_keepingNow || _webView.CoreWebView2 is not { } core) return;
        _keepingNow = true;
        try
        {
            var cookies = await core.CookieManager.GetCookiesAsync(string.Empty);
            _cookies.Save(cookies.Select(Cookie));
        }
        catch (Exception)
        {
            // Closed mid-read, the file held, the seal refused: the next page or tick tries again.
        }
        finally
        {
            _keepingNow = false;
        }
    }

    private static BrowserCookie Cookie(CoreWebView2Cookie cookie) => new(
        cookie.Name, cookie.Value, cookie.Domain, cookie.Path, cookie.IsSecure, cookie.IsHttpOnly,
        cookie.SameSite.ToString(), cookie.IsSession);

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _keeping.Stop();
        _keeping.Dispose();
        // A window closed before it came up must not leave a waiter hanging.
        _ready.TrySetException(new InvalidOperationException("the in-app browser was closed."));
        base.OnFormClosed(e);
    }
}
