using Daoris.Desktop;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;
using Shenora.Windows;

// The local driver's shell (D45 part 2, D46 §7): one window that brings up the local service host —
// adopting one already running rather than double-starting — carries the platform the host serves,
// and runs the driver loop in-process. Built on the desktop runtime sibling at a released version
// (D22); the runtime's single-instance guard also keeps two shells from fighting over the port.
//
//   DAORIS_HOME            where every machine-local file lives (D63)          (an install: `data/` beside this exe, set here)
//   DAORIS_SERVICE_URL     where the service is, which the page calls         (default: http://localhost:5177)
//   DAORIS_DEVTOOLS_PORT   the engine's DevTools port, in development only     (absent: none)
//   DAORIS_SERVICE_KEY     sent as a bearer token when set                     (absent: local trust, D21)
//   DAORIS_DRIVER_CONFIG   the person's standing choices                       (default: $DAORIS_HOME/driver.json)
//   DAORIS_HTTP_HOST       the host executable, when it lives somewhere unusual
//   DAORIS_REMOTE_URL      one workspace's remote, with its key                (or $DAORIS_HOME/remotes.json — D48 §5)
//   DAORIS_REMOTE_KEY        either env var present means the environment is the answer, whole,
//   DAORIS_REMOTE_WORKSPACE  for the workspace named here                      (absent: `default`)
//   DAORIS_REMOTE_CONFIG   where the map is                                    (default: $DAORIS_HOME/remotes.json)
internal static class Program
{
    /// <summary>The DevTools port a development run opens on its engine (D92) — the dev loop's instruments attach there.</summary>
    private const string DevToolsVariable = "DAORIS_DEVTOOLS_PORT";

    [STAThread]
    private static void Main(string[] args)
    {
        // The home before anything else (D63): an install's `data/` folder is the Daoris home for this
        // process and every host and session it spawns. Before the builder, because every module
        // captures its path at construction — and a workspace build is left alone, so the dev loop's
        // scratch home stays the dev loop's.
        var home = InstallHome.Establish(AppContext.BaseDirectory);

        // The machine log (LOG1, D94), opened as soon as there is a home to put it in, so an exception
        // nothing else catches is written before the process ends; with no home it writes nothing.
        using var log = Daoris.Driver.MachineLog.Open("desktop");
        log.WatchUnhandled();
        var started = DateTimeOffset.UtcNow;

        // 🔴 No home is a sentence, not a silent exit (REV3). A workspace build started with no
        // DAORIS_HOME threw from the loop's construction, before any window existed, and a windowed
        // program has no console to say it on — so nothing appeared and nothing said why.
        try
        {
            Daoris.Driver.DriverConfig.ResolvePath();
        }
        catch (Daoris.Driver.DriverException error)
        {
            MessageBox.Show(
                $"{error.Message}\n\n`npm run desktop -- run` gives a development run a home of its own.",
                "Daoris cannot start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 2;
            return;
        }

        var serviceUrl = Environment.GetEnvironmentVariable(Daoris.Driver.ServiceClient.UrlVariable)
            ?? "http://localhost:5177";

        // 🔴 The kit anchors its data area (the engine's profile, the window's geometry) at the bundle's
        // root, which it takes to be the executable's folder unless told. An install's application runs
        // from `app/` (D93), so it is told the install's root, and its data lands in the install's
        // `data/` rather than a second one inside `app/`. A `--app-root` given still wins.
        var installRoot = InstallHome.RootOf(AppContext.BaseDirectory);
        var inInstallApp = !string.Equals(
            installRoot, Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase);

        var builder = ShenoraApplication.CreateBuilder(new ShenoraApplicationOptions
        {
            Args = args,
            ApplicationName = "Daoris",
            Paths = inInstallApp
                ? new ShenoraPathsOptions { ExplicitRoot = AppRootArgument.Resolve(args, installRoot) }
                : new ShenoraPathsOptions(),
        });

        // The engine every window renders on (D92, CHR2): the Chromium the install ships. The pages are
        // the HTTP host's own bundle, the same bytes a browser gets (D38's one UI), served from the
        // `wwwroot` beside the host this shell would start, on the engine's app origin — and each page
        // reaches the host at its loopback address, which a local host allows (`DesktopPage`).
        var located = Daoris.Driver.ServiceHostLocator.Locate(
            Environment.GetEnvironmentVariable(Daoris.Driver.ServiceHostLocator.PathVariable),
            Daoris.Driver.DaorisHome.Resolve(),
            AppContext.BaseDirectory);
        var bundle = new PlatformBundle(located is null ? null : DesktopPage.BundleOf(located));
        builder.Services.AddSingleton(bundle);
        builder.UseChromiumEngine(new Shenora.Chromium.ChromiumEngineOptions
        {
            // A folder that exists either way: a missing bundle is the window's sentence, not an engine
            // that refused to start and left nothing to say it in.
            ContentRoot = bundle.Folder is { } folder && Directory.Exists(folder) ? folder : AppContext.BaseDirectory,
            VirtualHost = DesktopPage.VirtualHost,
            // The instruments' port (`npm run desktop`), honoured in development only: a published app
            // has none, so the page that holds the bridge is out of CDP's reach (D78 §3.1).
            DevToolsPort = int.TryParse(Environment.GetEnvironmentVariable(DevToolsVariable), out var port) ? port : 0,
            Shell = new Shenora.Core.Ipc.ShellInfo { Name = "daoris-desktop", Capabilities = [] },
        });

        // The kit's, the engine's and the modules' warnings and errors, into the same log.
        builder.Services.AddSingleton(log);
        builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(new MachineLogProvider(log));

        builder.Services.AddSingleton(new HostSupervisor(serviceUrl));
        builder.Services.AddSingleton(sp => new DriverLoop(
            sp.GetRequiredService<Shenora.Core.Events.IEventBus>(),
            sp.GetRequiredService<HostSupervisor>(),
            serviceUrl,
            home,
            // Daoris's own browser (D78), for a plugin server that drives it.
            sp.GetRequiredService<EngineBrowserHost>(),
            // What the person runs, and how long it takes, into the same log (LOG1b).
            log));
        builder.Services.AddSingleton<MainForm>();
        // The session-control surface's host half: the page's driver controls land here (D46 §6).
        builder.Services.AddIpcModule<DriverModule>();
        // The one thing a page cannot do: name a directory on this machine (D48 §7). Everything else
        // about managing a repository is an ordinary call to the loopback host.
        builder.Services.AddSingleton<Func<string?>>(sp => () =>
            sp.GetRequiredService<MainForm>().PickFolder());
        builder.Services.AddIpcModule<RegistryModule>();
        // The machine's wiring — which deployment serves each workspace (D48 §5, D50). The same file
        // `daoris remote` edits; the service has no door onto it, deliberately.
        builder.Services.AddIpcModule<RemotesModule>();

        // The second screen (D55 §b, SURF8): named windows on their own STA pumps, each carrying
        // this same bundle at its own route. The other thing a page cannot do for itself.
        builder.Services.AddSingleton(sp => new SecondaryWindows(
            sp.GetService<Microsoft.Extensions.Logging.ILogger<SecondaryWindows>>()));
        builder.Services.AddSingleton<SecondaryWindowHost>();
        builder.Services.AddSingleton<ISecondaryWindows>(
            sp => sp.GetRequiredService<SecondaryWindowHost>());
        // Daoris's own browser (D78, D85): the engine's own window, in a process of its own
        // (`daoris-browser`), with its profile under the home — the directory `driver.json` sits in,
        // as every machine file's is.
        builder.Services.AddSingleton(_ => new EngineBrowserHost(
            Path.GetDirectoryName(Path.GetFullPath(Daoris.Driver.DriverConfig.ResolvePath()))!));
        builder.Services.AddSingleton<Daoris.Driver.IInAppBrowser>(sp => sp.GetRequiredService<EngineBrowserHost>());
        // Its favorites and settings, as a Settings domain (CHR5, CHR7): the files `daoris browser`
        // edits, read by `daoris-browser` each time it starts.
        builder.Services.AddIpcModule<BrowserModule>();
        builder.Services.AddSingleton(new PlatformAddress(serviceUrl));
        builder.Services.AddIpcModule<WindowsModule>();
        // The page's own report into the machine log (LOG1b, D94): what is used and what fails on the
        // screen, taken only as the catalogue names it — the module is where no word gets through.
        builder.Services.AddIpcModule<LogModule>();
        // And every refusal the bridge answers, by its code: a middleware in the kit's application slot,
        // so every module's answer passes it. Registered before Build, whose own call is a TryAdd.
        builder.Services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));

        // The loop starts with the app, not with the window: the driver watches whether or not the
        // person is looking, which is the whole point of a driver.
        builder.OnStarting(app =>
        {
            log.Info("app.started",
                ("version", typeof(Program).Assembly
                    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion),
                ("installed", inInstallApp));
            app.Services.GetRequiredService<DriverLoop>().Start();
        });
        builder.OnStopping(app =>
        {
            log.Info("app.stopped", ("uptimeSeconds", (long)(DateTimeOffset.UtcNow - started).TotalSeconds));
            // The secondary windows first, and disposed rather than abandoned: their threads are
            // BACKGROUND, so an unwaited exit kills them before the geometry saves their own
            // FormClosed handlers run. Bounded, so a wedged window cannot hang shutdown.
            app.Services.GetRequiredService<SecondaryWindowHost>().Dispose();
            // Order matters: end the loop first (an in-flight session is ended and recorded
            // `stopped` by the driver itself), and only then the host it reports to.
            app.Services.GetRequiredService<DriverLoop>().Stop();
            app.Services.GetRequiredService<HostSupervisor>().Stop();
        });

        builder.UseWindows(new WindowsHostOptions
        {
            MainForm = sp => sp.GetRequiredService<MainForm>(),
            WindowState = new WindowStateHostOptions
            {
                Store = sp => new JsonFileWindowStateStore(
                    Path.Combine(sp.GetRequiredService<ShenoraPaths>().DataArea("config"), "window-state.json")),
            },
        });

        using var app = builder.Build();
        app.Run();
    }
}
