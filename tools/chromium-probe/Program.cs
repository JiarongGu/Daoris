using System.Text.Json;
using CefSharp;
using CefSharp.WinForms;

// CHR1's probe host (docs/2026-09-28-chromium-host-design.md §5). One window embedding Chromium
// through CefSharp, in the two roles a Daoris window would give it:
//
//   --page <url>     the APP's page, in the global request context (<root>/page) — the one that would
//                    hold the bridge. It answers `CefSharp.PostMessage(n)` with `window.__reply(n)`.
//   --browser <url>  the in-app browser, in a request context of its own (<root>/browser).
//   --browser-global the browser in the GLOBAL request context instead (a process with no page).
//
//   --root <dir>     the root cache path, under _fixtures (D63's rule, in miniature)
//   --port <n>       a remote debugging port; there is no per-context one, which is what is measured
//   --persist        keep session cookies across a restart, the engine's own setting
//   --style chrome   Chrome-styled browsers instead of CefSharp's default Alloy
//   --log <file>     one JSON line per event, for tools/chromium-probe.mjs to read
//
// Nothing here is Daoris's shape; it is the smallest host that answers the probe's questions.
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var options = Options.Parse(args);
        var log = new Log(options.LogFile);

        var settings = new CefSettings
        {
            RootCachePath = options.Root,
            // With --browser-global the browser is the global request context's, which is where a
            // target a CDP client creates lands.
            CachePath = Path.Combine(options.Root, options.BrowserGlobal ? "browser" : "page"),
            PersistSessionCookies = options.Persist,
            LogFile = Path.Combine(options.Root, "cef.log"),
            LogSeverity = LogSeverity.Warning,
        };
        if (options.Port > 0) settings.RemoteDebuggingPort = options.Port;
        if (options.Style == "chrome") CefSharpSettings.RuntimeStyle = CefRuntimeStyle.Chrome;

        if (!Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null))
        {
            log.Write("host", "initialize-failed", new { });
            return 2;
        }
        log.Write("host", "initialized", new
        {
            pid = Environment.ProcessId,
            cefSharp = Cef.CefSharpVersion,
            cef = Cef.CefVersion,
            chromium = Cef.ChromiumVersion,
            style = options.Style,
            port = options.Port,
            persist = options.Persist,
        });

        ApplicationConfiguration.Initialize();
        var form = new Form { Text = options.Title, Width = 1400, Height = 850 };
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 700 };
        form.Controls.Add(split);

        ChromiumWebBrowser Make(string role, string url, IRequestContext? context)
        {
            var control = new ChromiumWebBrowser(url, context) { Dock = DockStyle.Fill };
            control.LifeSpanHandler = new Spans(role, log);
            control.FrameLoadEnd += (_, loaded) =>
            {
                if (loaded.Frame.IsMain) log.Write(role, "loaded", new { url = loaded.Url, status = loaded.HttpStatusCode });
            };
            var received = 0;
            control.JavascriptMessageReceived += (_, message) =>
            {
                try
                {
                    if (Interlocked.Increment(ref received) == 1)
                        log.Write(role, "first-message", new { type = message.Message?.GetType().Name, value = message.Message });
                    if (message.Message is string text && text == "quit")
                    {
                        form.BeginInvoke(form.Close);
                        return;
                    }
                    control.ExecuteScriptAsync("window.__reply", message.Message);
                }
                catch (Exception error)
                {
                    log.Write(role, "message-failed", new { error = error.GetType().Name, error.Message });
                }
            };
            return control;
        }

        if (options.Page is not null) split.Panel1.Controls.Add(Make("page", options.Page, null));
        else split.Panel1Collapsed = true;

        if (options.Browser is not null && options.BrowserGlobal)
        {
            split.Panel2.Controls.Add(Make("browser", options.Browser, null));
        }
        else if (options.Browser is not null)
        {
            var context = new RequestContext(new RequestContextSettings
            {
                CachePath = Path.Combine(options.Root, "browser"),
                PersistSessionCookies = options.Persist,
            });
            split.Panel2.Controls.Add(Make("browser", options.Browser, context));
        }
        else split.Panel2Collapsed = true;

        Application.Run(form);
        Cef.Shutdown();
        log.Write("host", "shutdown", new { });
        return 0;
    }
}

/// <summary>Says when Chromium asks the app about a window, and when one is made.</summary>
sealed class Spans(string role, Log log) : ILifeSpanHandler
{
    public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl,
        string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures,
        IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
    {
        log.Write(role, "before-popup", new { targetUrl, disposition = targetDisposition.ToString(), userGesture });
        newBrowser = null!;
        return false; // CEF makes its own window: what is measured is only that the app was asked
    }

    public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser) =>
        log.Write(role, "after-created", new { id = browser.Identifier, popup = browser.IsPopup });

    public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) => false;

    public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser) =>
        log.Write(role, "before-close", new { id = browser.Identifier });
}

sealed class Log(string? path)
{
    private readonly object _gate = new();

    public void Write(string role, string what, object detail)
    {
        if (path is null) return;
        var line = JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, role, what, detail });
        lock (_gate) File.AppendAllText(path, line + "\n");
    }
}

sealed record Options(string Root, int Port, bool Persist, string Style, string? Page, string? Browser, bool BrowserGlobal, string? LogFile, string Title)
{
    public static Options Parse(string[] args)
    {
        string? Value(string name)
        {
            var at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        return new Options(
            Root: Path.GetFullPath(Value("--root") ?? throw new ArgumentException("--root is required")),
            Port: int.TryParse(Value("--port"), out var port) ? port : 0,
            Persist: args.Contains("--persist"),
            Style: Value("--style") ?? "alloy",
            Page: Value("--page"),
            Browser: Value("--browser"),
            BrowserGlobal: args.Contains("--browser-global"),
            LogFile: Value("--log"),
            Title: Value("--title") ?? "chromium-probe");
    }
}
