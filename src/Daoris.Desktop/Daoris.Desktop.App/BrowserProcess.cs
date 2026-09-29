using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using Shenora.Chromium;

namespace Daoris.Desktop;

/// <summary>
/// `daoris-browser`: Daoris's own browser on the engine it ships (D85, CHR3). Since CHR8 (D99) it is this
/// application's own executable started with <see cref="EngineBrowser.Argument"/> first, run as a browser
/// by the kit's <see cref="ChromiumBrowserProcess"/>, so an install carries one Chromium. It holds no
/// control and no Daoris page. It prepares its profile under the home, opens the engine's own Chromium
/// window over its own debug port, and ends when its last window closes or the shell does.
/// </summary>
/// <remarks>
/// <para><b>Decided before anything else in <c>Main</c>.</b> The kit starts Chromium as the app is
/// composed, and a process runs one Chromium, the window's or the browser's. Nothing here composes the
/// app, reads the install's home, or opens the window's log.</para>
///
/// <para><b>Why a window made over CDP.</b> The engine gives a window with its own tabs, history and
/// devtools only to a target made that way (`docs/2026-09-28-chromium-embedding-evidence.md` §2), and
/// an agent's tabs land in it too. So the first window is made the same way an agent's is, in the
/// background when a session asked for it.</para>
///
/// <para><b>The port it is given is the kit's relay</b> (CHR6, then CHR8): it passes everything through
/// and announces a new tab as a <c>page</c> from the start, so an agent's browser MCP can open tabs. Daoris
/// kept a relay of its own for this until the kit's arrived.</para>
///
/// <para><b>Nothing under the user profile</b> (D63): with no profile folder it refuses, rather than
/// let the engine pick a folder of its own.</para>
/// </remarks>
internal static class BrowserProcess
{
    /// <summary>How long the first window waits for the engine's port before the browser gives up.</summary>
    private static readonly TimeSpan PortLimit = TimeSpan.FromSeconds(30);

    /// <summary>How often it looks whether the shell that started it is still there.</summary>
    private static readonly TimeSpan Look = TimeSpan.FromSeconds(1);

    /// <summary>Run this process as the browser, on the calling (main, STA) thread, and answer its exit code.</summary>
    public static int Run(string[] args)
    {
        // The machine log (LOG1, D94): this process's start, stop and every exception nothing caught, in
        // a file of its own beside the shell's. The home is the shell's, handed down in the environment.
        using var log = Daoris.Driver.MachineLog.Open("browser");
        log.WatchUnhandled();

        var options = EngineBrowserOptions.Parse(args, out var problem);
        if (options is null)
        {
            // A windowed process has no console to say this on; the log is where it is said.
            log.Error("error", ("where", "the browser's arguments"), ("message", problem));
            return 2;
        }

        // Fired by the two watchers below, never here once Chromium has returned, when there is no window
        // left to close. Not disposed: a watcher may still hold it then, and a source that holds no timer
        // has nothing to free before the process ends.
        var stop = new CancellationTokenSource();
        if (options.Parent is { } parent && !WatchShell(parent, stop)) return 0; // the shell that asked has gone

        Directory.CreateDirectory(options.Profile);
        Prepare(options.Profile);

        var started = DateTimeOffset.UtcNow;
        log.Info("app.started");
        _ = OpenFirstWindowAsync(options, stop, log);
        try
        {
            return ChromiumBrowserProcess.Run(
                new ChromiumBrowserProcessOptions
                {
                    // The engine's root, whose `Default` is the profile CHR5 and CHR7 prepared above.
                    UserDataFolder = options.Profile,
                    RemoteDebuggingPort = options.Port,
                    // The sign-in survives a restart with the engine's own setting (the evidence's §5).
                    PersistSessionCookies = true,
                    Locale = EngineBrowser.Locale(CultureInfo.CurrentUICulture.Name),
                },
                stop.Token,
                new MachineLogProvider(log).CreateLogger("Daoris.Browser"));
        }
        catch (InvalidOperationException error)
        {
            // Chromium would not start (the kit's message names its log), or the port is taken.
            log.Error("error", ("where", "the browser's engine"), ("message", error.Message));
            return 2;
        }
        finally
        {
            log.Info("app.stopped", ("uptimeSeconds", (long)(DateTimeOffset.UtcNow - started).TotalSeconds));
        }
    }

    /// <summary>
    /// Stop when the shell that started it has gone, as the shell's own window did. False when it has
    /// gone already, and there is nobody to open a browser for.
    /// </summary>
    private static bool WatchShell(int parent, CancellationTokenSource stop)
    {
        Process shell;
        try
        {
            shell = Process.GetProcessById(parent);
        }
        catch (ArgumentException)
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            using (shell)
            {
                while (!stop.IsCancellationRequested)
                {
                    if (shell.HasExited)
                    {
                        stop.Cancel();
                        return;
                    }

                    try
                    {
                        await Task.Delay(Look, stop.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        });
        return true;
    }

    /// <summary>
    /// The engine's own window, made over its port once the port answers. A browser that cannot make it
    /// stops: a process with no window is nothing the person can see or close, and the shell starts
    /// another when asked.
    /// </summary>
    private static async Task OpenFirstWindowAsync(EngineBrowserOptions options, CancellationTokenSource stop, Daoris.Driver.MachineLog log)
    {
        try
        {
            using var engine = new EngineCdp(options.Port);
            var deadline = DateTime.UtcNow + PortLimit;
            while (!await engine.AnswersAsync(stop.Token).ConfigureAwait(false))
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"its port did not answer within {PortLimit.TotalSeconds:0} seconds");
                }

                await Task.Delay(100, stop.Token).ConfigureAwait(false);
            }

            await engine.NewWindowAsync("chrome://newtab/", options.Background, stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The shell went, or the engine did, before there was a window to make.
        }
        catch (Exception error) when (error is TimeoutException or HttpRequestException or WebSocketException
                                          or InvalidOperationException or JsonException)
        {
            log.Error("error", ("where", "the browser's first window"), ("message", error.Message));
            stop.Cancel();
        }
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
}
