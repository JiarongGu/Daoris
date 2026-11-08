using System.Diagnostics;
using System.Globalization;
using Daoris.Driver;
using Shenora.Chromium;

namespace Daoris.Desktop;

/// <summary>
/// `daoris-browser`: Daoris's own browser on the engine it ships. It is this application's own executable started with
/// <see cref="EngineBrowser.Argument"/> first, run as a browser by the kit's <see cref="ChromiumBrowserProcess"/>. It holds no
/// control and no Daoris page: it prepares its profile under the home, opens the engine's own window over its debug port, and ends
/// when its last window closes or the shell does.
/// </summary>
/// <remarks>
/// <para><b>Decided before anything else in <c>Main</c></b>: a process runs one Chromium, the window's or the browser's, so nothing here
/// composes the app, reads the install's home, or opens the window's log. With no profile folder it refuses, rather than let the engine
/// pick one under the user profile.</para>
/// <para><b>The first window is made over CDP</b>, as an agent's is (`docs/2026-09-28-chromium-embedding-evidence.md` §2): only a target
/// made that way gets its own tabs, history and devtools. The port it is given is the kit's relay, which announces a new tab as a
/// <c>page</c> from the start, so an agent's browser MCP can open tabs.</para>
/// <para><b>Every task started here and not awaited is observed</b>: the machine log's <c>Observe</c> writes its failure as a line saying
/// where, since an exception nobody awaits is rethrown by the finalizer. A source test holds the rule.</para>
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
        // The machine log: this process's start, stop and uncaught exceptions, in a file beside the shell's, in the shell's home.
        using var log = Daoris.Driver.MachineLog.Open("browser");
        log.WatchUnhandled();

        var options = EngineBrowserOptions.Parse(args, out var problem);
        if (options is null)
        {
            // A windowed process has no console to say this on; the log is where it is said.
            log.Error("error", ("where", "the browser's arguments"), ("message", problem));
            return 2;
        }

        // Fired by the two watchers below; not disposed, since a watcher may still hold it, and it holds no timer to free.
        var stop = new CancellationTokenSource();
        if (options.Parent is { } parent && !WatchShell(parent, stop, log)) return 0; // the shell that asked has gone

        Directory.CreateDirectory(options.Profile);
        Prepare(options.Profile, log);

        var started = DateTimeOffset.UtcNow;
        log.Info("app.started");

        // Started and not awaited, so observed: a failure is a line saying where, and a browser
        // that cannot make its window stops, since a process with no window is nothing the person can
        // see or close, and the shell starts another when asked.
        _ = log.Observe(FirstWindowAsync(options, stop.Token), EngineBrowser.FirstWindowPlace, stop.Cancel);
        try
        {
            return ChromiumBrowserProcess.Run(
                new ChromiumBrowserProcessOptions
                {
                    // The engine's root, whose `Default` is the profile prepared above.
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
    private static bool WatchShell(int parent, CancellationTokenSource stop, Daoris.Driver.MachineLog log)
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

        // Observed like every task here; a watch that fails is written, and the windows stay the person's to close.
        _ = log.Observe(Task.Run(async () =>
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
        }), EngineBrowser.ShellWatchPlace);
        return true;
    }

    /// <summary>
    /// The engine's own window, made over its port once the port answers (<see cref="EngineCdp.FirstWindowAsync"/>), cancelled when the shell
    /// or the engine went first. 🔴 It lets every exception through: one left to fault a task nobody holds is seen only by the finalizer.
    /// </summary>
    private static async Task FirstWindowAsync(EngineBrowserOptions options, CancellationToken ct)
    {
        using var engine = new EngineCdp(options.Port);
        await engine.FirstWindowAsync(options.Background, PortLimit, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Before the engine reads its profile: Daoris's favorites become its folder on the bookmarks bar
    /// and other software's Chrome extensions are refused or offered as the settings say.
    /// A file another program holds is read as absent and said once in the log, and the browser starts regardless.
    /// </summary>
    private static void Prepare(string profile, Daoris.Driver.MachineLog log) =>
        EngineProfilePrepare.Run(
            profile, ExternalExtensions.Registered, DateTimeOffset.UtcNow,
            file => log.Warn("browser.file.unread", ("file", file)));
}
