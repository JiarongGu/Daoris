using System.Diagnostics;
using System.Globalization;
using CefSharp;
using CefSharp.WinForms;

namespace Daoris.Desktop.Browser;

/// <summary>
/// `daoris-browser`: Daoris's own browser on the engine it ships (D85, CHR3). It holds no control and
/// no Daoris page. It starts the engine on its profile under the home, opens the engine's own Chromium
/// window over its own debug port, and ends when its last window closes or the shell does.
/// </summary>
/// <remarks>
/// <para><b>Why a window made over CDP.</b> The engine gives a window with its own tabs, history and
/// devtools only to a target made that way (`docs/2026-09-28-chromium-embedding-evidence.md` §2), and
/// an agent's tabs land in it too. So the first window is made the same way an agent's is.</para>
///
/// <para><b>The port it is given is a relay's</b> (CHR6): the engine listens on one of its own, and
/// <see cref="CdpRelay"/> answers <c>${browser}</c>, calling a new tab the page it is where the engine
/// says <c>other</c>, so an agent's browser MCP can open tabs.</para>
///
/// <para><b>Nothing under the user profile</b> (D63): with no <c>--profile</c> it refuses, rather than
/// let the engine pick a folder of its own.</para>
/// </remarks>
internal static class Program
{
    /// <summary>How often it looks for windows, and how many empty looks in a row mean the last one closed.</summary>
    private static readonly TimeSpan Look = TimeSpan.FromSeconds(1);
    private const int EmptyLooks = 3;

    [STAThread]
    private static int Main(string[] args)
    {
        // The machine log (LOG1, D94): this process's start, stop and every exception nothing caught, in
        // a file of its own beside the shell's. The home is the shell's, handed down in the environment.
        using var log = Daoris.Driver.MachineLog.Open("browser");
        log.WatchUnhandled();

        var options = EngineBrowserOptions.Parse(args, out var problem);
        if (options is null)
        {
            Console.Error.WriteLine($"daoris-browser: {problem}");
            return 2;
        }

        Directory.CreateDirectory(options.Profile);
        Prepare(options.Profile);

        // The engine listens on a port of its own, and the port the shell hands out is the relay's
        // (CHR6): the one place the engine's announcement of a new tab is said right.
        var enginePort = InAppBrowser.FreePort();
        var settings = new CefSettings
        {
            RootCachePath = options.Profile,
            CachePath = EngineBrowser.ProfileDirectory(options.Profile),
            // The sign-in survives a restart with the engine's own setting (the evidence's §5).
            PersistSessionCookies = true,
            RemoteDebuggingPort = enginePort,
            LogFile = Path.Combine(options.Profile, "engine.log"),
            LogSeverity = LogSeverity.Warning,
            Locale = EngineBrowser.Locale(CultureInfo.CurrentUICulture.Name),
        };
        settings.CefCommandLineArgs.Add("no-default-browser-check");
        settings.CefCommandLineArgs.Add("no-first-run");

        if (!Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null))
        {
            Console.Error.WriteLine("daoris-browser: the engine did not start. Its log is engine.log in the profile.");
            log.Error("error", ("where", "the browser's engine"), ("message", "the engine did not start; its log is engine.log in the profile"));
            return 2;
        }

        var started = DateTimeOffset.UtcNow;
        log.Info("app.started");
        try
        {
            return RunAsync(options, enginePort).GetAwaiter().GetResult();
        }
        finally
        {
            Cef.Shutdown();
            log.Info("app.stopped", ("uptimeSeconds", (long)(DateTimeOffset.UtcNow - started).TotalSeconds));
        }
    }

    private static async Task<int> RunAsync(EngineBrowserOptions options, int enginePort)
    {
        Process? shell = null;
        if (options.Parent is { } parent)
        {
            try
            {
                shell = Process.GetProcessById(parent);
            }
            catch (ArgumentException)
            {
                return 0; // the shell that asked for it has already gone
            }
        }

        using var engine = new EngineCdp(enginePort);
        for (var i = 0; i < 100 && !await engine.AnswersAsync(); i++) await Task.Delay(100);
        await using var relay = CdpRelay.Start(options.Port, enginePort);

        await engine.NewWindowAsync("chrome://newtab/", options.Background);

        var empty = 0;
        while (empty < EmptyLooks)
        {
            if (shell is { HasExited: true })
            {
                await CloseAllAsync(engine);
                return 0;
            }

            await Task.Delay(Look);
            var pages = await engine.PagesAsync();
            empty = pages is { Count: 0 } ? empty + 1 : 0;
        }

        return 0;
    }

    /// <summary>
    /// Before the engine reads its profile: Daoris's favorites become its folder on the bookmarks bar
    /// (CHR5), and other software's Chrome extensions are refused or offered as the settings say
    /// (CHR7). Each file that cannot be read is left as it is, and the browser starts regardless.
    /// </summary>
    private static void Prepare(string profile)
    {
        var home = EngineBrowser.HomeOf(profile);
        Directory.CreateDirectory(EngineBrowser.ProfileDirectory(profile));
        var record = EngineProfile.ReadRecord(ReadText(EngineProfile.RecordPath(profile)));
        var preferences = ReadText(EngineProfile.PreferencesPath(profile));
        var preferencesBefore = preferences;

        var favorites = BrowserFavorites.Read(home);
        if (favorites.Problem is null
            && EngineProfile.WithFavorites(ReadText(EngineProfile.BookmarksPath(profile)), favorites.Favorites, DateTimeOffset.UtcNow)
                is var (bookmarks, created))
        {
            Daoris.Driver.AtomicFile.WriteText(EngineProfile.BookmarksPath(profile), bookmarks);

            // Once, when its folder first appears: a bar that is hidden shows nothing Daoris put there.
            // After that the bar is the person's to hide.
            if (created && !record.BarShown && EngineProfile.ShowBar(preferences) is { } shown)
            {
                preferences = shown;
                record = record with { BarShown = true };
            }
        }

        if (BrowserSettings.Read(home).Extensions == ExtensionsSetting.Refuse)
        {
            if (EngineProfile.Refuse(preferences, ExternalExtensions.Registered(), record.Refused) is var (refusing, refused))
            {
                preferences = refusing;
                record = record with { Refused = refused };
            }
        }
        else if (record.Refused.Count > 0 && EngineProfile.Offer(preferences, record.Refused) is { } offering)
        {
            preferences = offering;
            record = record with { Refused = [] };
        }

        if (preferences is not null && preferences != preferencesBefore)
        {
            Daoris.Driver.AtomicFile.WriteText(EngineProfile.PreferencesPath(profile), preferences);
        }

        Daoris.Driver.AtomicFile.WriteText(EngineProfile.RecordPath(profile), EngineProfile.WriteRecord(record));
    }

    private static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// <summary>The shell has gone: close every window over the engine's own socket, and wait for them to go.</summary>
    private static async Task CloseAllAsync(EngineCdp engine)
    {
        foreach (var page in await engine.PagesAsync() ?? [])
        {
            try
            {
                await engine.BrowserCallAsync("Target.closeTarget", new { targetId = page });
            }
            catch (InvalidOperationException)
            {
                // Already closing.
            }
        }

        for (var i = 0; i < 50 && await engine.PagesAsync() is { Count: > 0 }; i++) await Task.Delay(100);
    }
}
