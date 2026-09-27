using Microsoft.Web.WebView2.Core;
using Shenora.Windows;
using WebView2Control = Microsoft.Web.WebView2.WinForms.WebView2;

namespace Daoris.Desktop;

/// <summary>
/// Daoris's own browser (D78): one framed window with tabs and an address bar, in a WebView2
/// environment of its own — the pages the person signs in to and a session drives over CDP.
/// </summary>
/// <remarks>
/// <para>🔴 <b>No bridge, and never the app's environment.</b> A standard browser MCP attached over CDP
/// drove the app's own WebView2 and navigated its page away. The page holds the bridge, and an agent
/// under that would hold the machine. So this window has its own user-data folder, which is its own
/// browser process, and the debug port it opens reaches nothing else. It never builds a
/// <c>WebViewIpcBridge</c>.</para>
///
/// <para><b>Tabs (BRW4)</b>, a WebView2 each on the one environment, so each is a CDP target on the same
/// port. Where a tab opens and which is in front after one closes is <see cref="BrowserTabs"/>'s. A
/// page's new window becomes a tab beside it, handed back to the page as its window, so
/// <c>window.opener</c> still works.</para>
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
    private const string WindowTitle = "Daoris — Browser";

    private readonly ChromePalette _palette;
    private readonly TextBox _address;
    private readonly FlowLayoutPanel _strip;
    private readonly Button _newTab;
    private readonly Panel _content;
    private readonly string _profile;
    private readonly int _port;
    private readonly bool _activate;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The tabs' order and which is in front (BRW4); the pages and their strip entries by id.</summary>
    private readonly BrowserTabs _tabs = new();
    private readonly Dictionary<int, WebView2Control> _pages = [];
    private readonly Dictionary<int, Tab> _chips = [];

    /// <summary>One environment for every tab: one browser process, one profile, one CDP port.</summary>
    private CoreWebView2Environment? _environment;

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
        _palette = ChromePalette.For(MainForm.OperatingSystemPrefersDark());
        var palette = _palette;

        Text = WindowTitle;
        StartPosition = FormStartPosition.CenterScreen;

        _content = new Panel { Dock = DockStyle.Fill, BackColor = palette.Page };

        // The tabs, above the bar. The one in front wears the bar's colour and joins it, as in any
        // browser, so the page the address belongs to is the one that reads as attached to it.
        _strip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = palette.Page,
            Padding = new Padding(6, 5, 6, 0),
            AccessibleName = "Tabs",
        };
        _newTab = Glyph("", "New tab (Ctrl+T)", palette, () => _ = OpenTabAsync(opener: null, address: "about:blank", focus: true));
        _newTab.BackColor = palette.Page;
        _strip.Controls.Add(_newTab);
        _strip.Resize += (_, _) => Fit();

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

        // 🔴 Each glyph is a private-use character, which most views show as nothing: a rewrite that
        // copied these from a read wrote empty strings, and the bar lost its buttons (BRW4, seen on
        // the window). Check the bytes after touching one.
        bar.Controls.Add(Glyph("", "Back", palette, () => { if (Front is { CanGoBack: true } page) page.GoBack(); }), 0, 0);
        bar.Controls.Add(Glyph("", "Forward", palette, () => { if (Front is { CanGoForward: true } page) page.GoForward(); }), 1, 0);
        bar.Controls.Add(Glyph("", "Reload", palette, () => Front?.Reload()), 2, 0);

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
            if (InAppBrowser.Address(_address.Text) is { } page && Front?.CoreWebView2 is { } core) core.Navigate(page);
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

        // Docked from the last added: the tabs at the top, the bar under them, the notice under that,
        // and the page in the rest.
        Controls.Add(_content);
        Controls.Add(_noticeStrip);
        Controls.Add(bar);
        Controls.Add(_strip);

        Load += async (_, _) => await BringUpAsync();
    }

    /// <summary>Brought up by a session, it does not take the keyboard from the person.</summary>
    protected override bool ShowWithoutActivation => !_activate;

    /// <summary>The page in front, or null before the first tab is up.</summary>
    private WebView2Control? Front => _tabs.Front is { } id ? _pages.GetValueOrDefault(id) : null;

    /// <summary>The window's keys while the bar or the strip has the keyboard.</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) =>
        Shortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);

    /// <summary>
    /// A browser's keys: a new tab, closing one, the next and previous, and the address. True when the
    /// key was one of them.
    /// </summary>
    /// <remarks>
    /// 🔴 Heard twice over, because a page has the keyboard most of the time and the form never sees
    /// its keys. The WebView2 control raises its own <c>KeyDown</c> for them: its controller's
    /// accelerator handler builds the key from <c>VirtualKey | ModifierKeys</c>, calls <c>OnKeyDown</c>,
    /// and hands <c>Handled</c> back to the browser (read from the control's IL, WebView2 1.0.3800.47). So each
    /// page's <c>KeyDown</c> is wired here, and <see cref="ProcessCmdKey"/> covers the bar and the strip.
    /// </remarks>
    private bool Shortcut(Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.T:
                _ = OpenTabAsync(opener: null, address: "about:blank", focus: true);
                return true;
            case Keys.Control | Keys.W:
                if (_tabs.Front is { } front) CloseTab(front);
                return true;
            case Keys.Control | Keys.Tab:
                Bring(_tabs.Step(+1));
                return true;
            case Keys.Control | Keys.Shift | Keys.Tab:
                Bring(_tabs.Step(-1));
                return true;
            case Keys.Control | Keys.L:
                _address.Focus();
                _address.SelectAll();
                return true;
            default:
                return false;
        }
    }

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

            // 🔴 The dev loop opens the APP's debug port through this variable, for the instruments, and
            // the variable reaches every environment this process creates. Left in place, this browser
            // would ask for the app's port as well, and one of the two would lose it. So for this one
            // creation the variable says this browser's port and nothing else, and it is put back at
            // once. A published build never sets it, and then it is not touched at all.
            var inherited = Environment.GetEnvironmentVariable(AdditionalArgumentsVariable);
            try
            {
                if (!string.IsNullOrEmpty(inherited)) Environment.SetEnvironmentVariable(AdditionalArgumentsVariable, arguments);
                _environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: _profile,
                    options: new CoreWebView2EnvironmentOptions(additionalBrowserArguments: arguments));
            }
            finally
            {
                if (!string.IsNullOrEmpty(inherited)) Environment.SetEnvironmentVariable(AdditionalArgumentsVariable, inherited);
            }

            // The first tab, and the sign-in back before its first page loads (BRW10).
            await OpenTabAsync(opener: null, address: null, focus: _activate);
            if (Front?.CoreWebView2 is { } first)
            {
                await RestoreAsync(first);
                first.Navigate("about:blank");
            }

            // Kept on a timer too, for what a page changes without loading (BRW10).
            _keeping.Tick += (_, _) => Keep();
            _keeping.Start();

            _ready.TrySetResult();
        }
        catch (Exception error)
        {
            _ready.TrySetException(new InvalidOperationException(
                $"the in-app browser did not come up: {error.Message}", error));
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = error.Message,
                ForeColor = _palette.Ink,
                Padding = new Padding(24),
                Font = new Font("Segoe UI", 10.5f),
            });
        }
    }

    /// <summary>
    /// A new tab, in front: the person's, going to <paramref name="address"/>, or a page's new window
    /// beside its <paramref name="opener"/>, handed back to that page as the window it asked for.
    /// </summary>
    private async Task OpenTabAsync(
        int? opener, string? address, bool focus = false, CoreWebView2NewWindowRequestedEventArgs? asked = null)
    {
        if (_environment is null) return;

        var id = _tabs.Open(opener);
        var page = new WebView2Control { Dock = DockStyle.Fill, DefaultBackgroundColor = _palette.Page };
        page.KeyDown += (_, key) => key.Handled = Shortcut(key.KeyData);
        _pages[id] = page;
        _content.Controls.Add(page);
        _chips[id] = new Tab(this, id);
        Arrange();
        Bring(id);

        await page.EnsureCoreWebView2Async(_environment);
        var core = page.CoreWebView2;
        Wire(id, core);

        if (asked is not null) asked.NewWindow = core;
        else if (address is not null) core.Navigate(address);

        if (focus)
        {
            _address.Focus();
            _address.SelectAll();
        }
    }

    /// <summary>What a tab's page tells the window: its address, its title, a new window, its close.</summary>
    private void Wire(int id, CoreWebView2 core)
    {
        core.NewWindowRequested += async (_, asked) =>
        {
            // A page's new window is a tab beside it (BRW4), and only ever a web page: a script, a file
            // or data asked for as a window goes nowhere, as it would from the address bar.
            if (asked.Uri is not ("" or "about:blank") && InAppBrowser.Address(asked.Uri) is null)
            {
                asked.Handled = true;
                return;
            }

            var deferral = asked.GetDeferral();
            try
            {
                await OpenTabAsync(opener: id, address: null, asked: asked);
            }
            catch (Exception)
            {
                // A tab that could not open: the page's window.open returns nothing, as a blocked popup does.
                asked.Handled = true;
            }
            finally
            {
                deferral.Complete();
            }
        };
        core.SourceChanged += (_, _) => Named(id, core);
        core.DocumentTitleChanged += (_, _) => Named(id, core);
        core.WindowCloseRequested += (_, _) => CloseTab(id);
        core.NavigationCompleted += (_, _) =>
        {
            if (core.Source.StartsWith("http", StringComparison.OrdinalIgnoreCase)) _noticeStrip.Visible = false;
            // Kept after every page, which is where a sign-in ends (BRW10).
            Keep();
        };
    }

    /// <summary>A tab's name in the strip, and the window's title and the address when it is in front.</summary>
    private void Named(int id, CoreWebView2 core)
    {
        var name = BrowserTabs.Name(core.DocumentTitle, core.Source);
        if (_chips.TryGetValue(id, out var chip)) chip.Name(name);
        if (_tabs.Front != id) return;
        _address.Text = core.Source == "about:blank" ? string.Empty : core.Source;
        Text = name == "New tab" ? WindowTitle : $"{WindowTitle} · {name}";
    }

    /// <summary>Bring a tab to the front: its page shown, its entry joined to the bar, its address in the bar.</summary>
    private void Bring(int? id)
    {
        if (id is not { } front || !_pages.ContainsKey(front)) return;
        _tabs.Bring(front);
        foreach (var (key, page) in _pages) page.Visible = key == front;
        foreach (var (key, chip) in _chips) chip.Front(key == front);
        if (_pages[front].CoreWebView2 is { } core) Named(front, core);
        else
        {
            _address.Text = string.Empty;
            Text = WindowTitle;
        }
    }

    /// <summary>Close a tab; the last one closing closes the window, as in any browser.</summary>
    private void CloseTab(int id)
    {
        if (!_pages.Remove(id, out var page)) return;
        var stays = _tabs.Close(id);
        if (_chips.Remove(id, out var chip)) chip.Dispose();
        _content.Controls.Remove(page);
        page.Dispose();

        if (!stays)
        {
            Close();
            return;
        }

        Arrange();
        Bring(_tabs.Front);
    }

    /// <summary>The strip's entries in the tabs' order, the new-tab button after them.</summary>
    private void Arrange()
    {
        _strip.SuspendLayout();
        var at = 0;
        foreach (var id in _tabs.Order)
        {
            if (_chips.TryGetValue(id, out var chip)) _strip.Controls.SetChildIndex(chip.Entry, at++);
        }

        _strip.Controls.SetChildIndex(_newTab, at);
        _strip.ResumeLayout();
        Fit();
    }

    /// <summary>
    /// Each entry as wide as a tab's name reads well, and narrower as tabs are added, so the strip
    /// holds them all and the new-tab button stays in reach, as a browser's tabs shrink.
    /// </summary>
    private void Fit()
    {
        if (_chips.Count == 0) return;
        var room = _strip.ClientSize.Width - _strip.Padding.Horizontal - _newTab.Width - 8;
        var each = Math.Clamp(room / _chips.Count, Tab.Narrowest(Font), Tab.Widest(Font));
        foreach (var chip in _chips.Values) chip.Width(each);
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
        // Any tab's cookie manager is the profile's: one environment holds them all.
        if (_keepingNow || Front?.CoreWebView2 is not { } core) return;
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

    /// <summary>One tab's entry in the strip: its name, which brings it forward, and its close.</summary>
    private sealed class Tab : IDisposable
    {
        private readonly BrowserForm _form;
        private readonly Button _title;
        private readonly Button _close;

        public FlowLayoutPanel Entry { get; }

        public Tab(BrowserForm form, int id)
        {
            _form = form;
            var palette = form._palette;
            _title = new Button
            {
                Text = "New tab",
                Font = new Font("Segoe UI", 9.5f),
                FlatStyle = FlatStyle.Flat,
                ForeColor = palette.Ink,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                AutoSize = false,
                Height = TextRenderer.MeasureText("Ag", new Font("Segoe UI", 9.5f)).Height + 12,
                Margin = Padding.Empty,
                Padding = new Padding(6, 0, 0, 0),
                TabStop = false,
                // 🔴 A button, not a PageTab: that role made it a tab item with no pattern at all, so
                // nothing that reads the window could press it (seen through UI Automation). What it
                // is and whether it is in front is its description.
            };
            _title.FlatAppearance.BorderSize = 0;
            _title.FlatAppearance.MouseOverBackColor = palette.Hover;
            _title.Click += (_, _) => form.Bring(id);
            // A middle click closes a tab, as in any browser.
            _title.MouseUp += (_, press) => { if (press.Button == MouseButtons.Middle) form.CloseTab(id); };

            _close = Glyph("", "Close tab (Ctrl+W)", palette, () => form.CloseTab(id));
            _close.Font = new Font("Segoe Fluent Icons", 8f);
            _close.Padding = new Padding(4, 2, 4, 2);
            _close.Margin = new Padding(0, 0, 4, 0);

            Entry = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0, 0, 2, 0),
                Padding = Padding.Empty,
            };
            Entry.Controls.Add(_title);
            Entry.Controls.Add(_close);
            _close.Anchor = AnchorStyles.None;
            form._strip.Controls.Add(Entry);
            // Named from the start: a blank tab's page may report no title or address to name it by.
            Name("New tab");
            Front(false);
        }

        /// <summary>The narrowest a tab gets: its close button and a few letters of its name.</summary>
        public static int Narrowest(Font font) => TextRenderer.MeasureText("Mmmm", font).Width;

        /// <summary>The widest: enough to read a page's title.</summary>
        public static int Widest(Font font) => TextRenderer.MeasureText("A page title of a fair length here", font).Width;

        public void Name(string name)
        {
            _title.Text = name;
            _title.AccessibleName = name;
            _close.AccessibleName = $"Close {name}";
        }

        /// <summary>In front, it wears the bar's colour; behind, the strip's.</summary>
        public void Front(bool front)
        {
            var colour = front ? _form._palette.Surface : _form._palette.Page;
            Entry.BackColor = colour;
            _title.BackColor = colour;
            _close.BackColor = colour;
            _title.AccessibleDescription = front ? "Tab, in front" : "Tab";
        }

        /// <summary>The whole entry's width, the close button's share taken off the name.</summary>
        public void Width(int width) => _title.Width = Math.Max(24, width - _close.Width - _close.Margin.Horizontal);

        public void Dispose()
        {
            _form._strip.Controls.Remove(Entry);
            Entry.Dispose();
        }
    }
}
