using Daoris.Desktop;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;
using Shenora.Core.Shell;
using Shenora.Windows;

// The local driver's shell (D45 part 2, D46 §7): one window that brings up the local service host —
// adopting one already running rather than double-starting — carries the platform the host serves,
// and runs the driver loop in-process. Built on the desktop runtime sibling at a released version
// (D22); the runtime's single-instance guard also keeps two shells from fighting over the port.
//
//   DAORIS_HOME            where every machine-local file lives (D63)          (an install: `data/` beside this exe, set here)
//   DAORIS_SERVICE_URL     where the service is, which the page calls         (default: http://localhost:5177)
//                          — left in $DAORIS_HOME/running.json while it runs, for a terminal the install's
//                          launchers set nothing for (FINDDAORIS1, D171)
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
        // Daoris's browser is this executable started with the browser's argument (CHR8, D99), decided
        // before anything else: the kit starts Chromium as the app is composed, a process runs one
        // Chromium, and the browser is not the window, its home or its log.
        if (EngineBrowser.IsBrowserProcess(args))
        {
            Environment.ExitCode = BrowserProcess.Run(args);
            return;
        }

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
            // The colour scheme's route is mounted below, and the kit asks that an app mounting it say so.
            Shell = new Shenora.Core.Ipc.ShellInfo { Name = "daoris-desktop", Capabilities = [ShellCapability.ColorScheme] },
        });

        // The viewer's chosen theme on the engine every window renders on (THEMESCHEME1): `UseWindows` registers the
        // app's colour scheme, the engine applies it to Chromium's global context, which the main window's browser and
        // each secondary window's take, and each window's page sets it to the viewer's choice over this route. So
        // Chromium's own UI and `prefers-color-scheme` follow an explicit light or dark, and *System* still follows the OS.
        // The in-app browser is a process of its own, which this does not reach.
        builder.Services.AddShenoraColorScheme();

        // The kit's, the engine's and the modules' warnings and errors, into the same log: loggers the container makes, since the
        // kit asks it for an ILogger<T>, and a provider alone made none (ONEINSTANCE2). And a later launch handed over, which the
        // single-instance guard and the engine say as information: the engine's line is the only trace of a launch the guard
        // let through and Chromium's own singleton turned away.
        builder.Services.AddSingleton(log);
        builder.Services.AddMachineLog(log,
            typeof(SingleInstanceGuard).FullName!, typeof(Shenora.Chromium.ChromiumEngine).FullName!);

        builder.Services.AddSingleton(new HostSupervisor(serviceUrl));
        builder.Services.AddSingleton(sp => new DriverLoop(
            sp.GetRequiredService<Shenora.Core.Events.IEventBus>(),
            sp.GetRequiredService<HostSupervisor>(),
            serviceUrl,
            home,
            // Daoris's own browser (D78), for a plugin server that drives it.
            sp.GetRequiredService<EngineBrowserHost>(),
            // What the person runs, and how long it takes, into the same log (LOG1b).
            log,
            // A set-up step's tab in that browser, and its build served there until the person's verdict (REVIEWENV1d).
            reviews: sp.GetRequiredService<ReviewTabs>()));
        builder.Services.AddSingleton(sp => new ReviewTabs(sp.GetRequiredService<EngineBrowserHost>()));
        // The install's update (UPDATE1, D139): what is staged beside the install drains the loop and, once the work allows,
        // starts the launcher to swap `app/` and closes this application as the person's close does. A workspace build is no
        // install, and never updates this way.
        builder.Services.AddSingleton(sp => new InstallUpdater(
            inInstallApp ? installRoot : null,
            Path.GetDirectoryName(Path.GetFullPath(Daoris.Driver.DriverConfig.ResolvePath()))!,
            log,
            work: () => sp.GetRequiredService<DriverLoop>().Work(),
            relaunch: ct => UpdateLauncher.StartAsync(installRoot, Environment.ProcessId, ct),
            // For good (MAINWINDOWS2): a close from code reads as the person's, which only puts the first away while another
            // main window is open, and an update has to end the application whatever is open.
            close: () => sp.GetRequiredService<MainForm>().CloseForGood(),
            bus: sp.GetRequiredService<Shenora.Core.Events.IEventBus>()));
        builder.Services.AddIpcModule<UpdateModule>();
        builder.Services.AddSingleton<MainForm>();
        // A tool named as a file (TOOLS7): the system's file picker, owned by the window whose page asked (MAINWINDOWS2c), as the
        // folder picker is: the first main window is hidden while put away, and a dialog owned by it would open behind nobody.
        builder.Services.AddSingleton<PickFile>(sp => (title, window) => Pickers.File(
            Pickers.Owner(sp.GetRequiredService<MainForm>(), sp.GetRequiredService<SecondaryWindowHost>(), window), title));
        // The session-control surface's host half: the page's driver controls land here (D46 §6).
        builder.Services.AddIpcModule<DriverModule>();
        // The one thing a page cannot do: name a directory on this machine (D48 §7). Everything else
        // about managing a repository is an ordinary call to the loopback host.
        builder.Services.AddSingleton<PickFolder>(sp => window => Pickers.Folder(
            Pickers.Owner(sp.GetRequiredService<MainForm>(), sp.GetRequiredService<SecondaryWindowHost>(), window)));
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
        // A later launch of the executable, handed here by the kit's single-instance guard (ONEINSTANCE1, D171 point 2): a new
        // main window of this instance once its service answers, rather than a second process and a second loop on this home.
        // A put-away first main window is that launch's window, shown again rather than another opened (MAINWINDOWS2).
        builder.Services.AddSingleton(sp => new SecondLaunch(
            sp.GetRequiredService<ISecondaryWindows>(), sp.GetRequiredService<PlatformAddress>(),
            () => sp.GetRequiredService<DriverLoop>().HostReady, log,
            mains: sp.GetRequiredService<MainWindows>(),
            showFirst: () => sp.GetRequiredService<MainForm>().TakeBack()));
        // The application ends with its last main window, not its first (MAINWINDOWS2, D171 point 2): the first, whose form the
        // kit's loop runs on and as which the pickers, the taskbar's count, the tray and the update are resolved, is put away
        // on the person's close while another main window is open, and closed for good when the last of them closes.
        builder.Services.AddSingleton(sp => new MainWindows(() => sp.GetRequiredService<MainForm>().CloseForGood(), log));
        // Daoris's own browser (D78, D85): the engine's own window, in a process of its own
        // (`daoris-browser`: this executable with the browser's argument, CHR8), with its profile under
        // the home — the directory `driver.json` sits in, as every machine file's is.
        builder.Services.AddSingleton(_ => new EngineBrowserHost(
            Path.GetDirectoryName(Path.GetFullPath(Daoris.Driver.DriverConfig.ResolvePath()))!));
        builder.Services.AddSingleton<Daoris.Driver.IInAppBrowser>(sp => sp.GetRequiredService<EngineBrowserHost>());
        // Its favorites and settings, as a Settings domain (CHR5, CHR7): the files `daoris browser`
        // edits, read by `daoris-browser` each time it starts.
        builder.Services.AddIpcModule<BrowserModule>();
        builder.Services.AddSingleton(new PlatformAddress(serviceUrl));
        // The main window's taskbar count (NOTIFY1b): the window is resolved as the page tells it, never as the module
        // is made, since the window is made after the modules and maps one of its own.
        // And, while the first is put away and its button is gone, each other main window's (MAINWINDOWS2b).
        builder.Services.AddSingleton(sp => new WaitingCount(sp.GetRequiredService<MainWindows>()));
        builder.Services.AddSingleton<ShowWaiting>(sp => (count, description) =>
        {
            sp.GetRequiredService<WaitingCount>().Told(count, description);
            sp.GetRequiredService<MainForm>().ShowWaiting(count, description);
            sp.GetRequiredService<SecondaryWindowHost>().ShowWaiting();
        });
        // And the workspace each main window's page holds, worn in its title (MAINWINDOWS3b): no name is the first main window.
        builder.Services.AddSingleton<ShowWorkspace>(sp => (name, workspace) =>
        {
            if (name is null) sp.GetRequiredService<MainForm>().ShowWorkspace(workspace);
            else sp.GetRequiredService<SecondaryWindowHost>().ShowWorkspace(name, workspace);
        });
        builder.Services.AddIpcModule<WindowsModule>();
        // The page's own report into the machine log (LOG1b, D94): what is used and what fails on the
        // screen, taken only as the catalogue names it — the module is where no word gets through. And
        // Settings → Logs reading it back (LOG1c), with the file manager for Open the folder: the kit's
        // shell launcher, which `UseWindows` registers, opened only on the folder the module names.
        builder.Services.AddSingleton<OpenFolder>(sp =>
            folder => sp.GetRequiredService<IShellLauncher>().OpenDirectory(folder));
        builder.Services.AddIpcModule<LogModule>();
        // The person's own shells for the terminal view (CONSOLE4a, D96): each under a pseudo-console, typed at
        // and read over the bridge alone. The container disposes the module as the app ends, and every
        // terminal with it; each shell's job object ends with this process besides.
        builder.Services.AddSingleton<Daoris.Driver.ITerminalFactory, Daoris.Driver.PseudoConsoleTerminals>();
        builder.Services.AddIpcModule<TerminalModule>();
        // How late the window's thread answers, and what held it (FREEZE1, D56's note): the strip is the caption, moved on
        // that thread, so a late thread is a frozen bar while the page works on. Composed here for its middleware, which
        // names the route on the thread; the form starts it once it is shown (MainForm).
        builder.Services.AddSingleton(sp => new UiStallWatch(log, bus: sp.GetRequiredService<Shenora.Core.Events.IEventBus>()));
        // And every refusal the bridge answers, by its code: a middleware in the kit's application slot,
        // so every module's answer passes it. Registered before Build, whose own call is a TryAdd.
        // Then a Daoris route leaves the window's thread at its first wait (UIFREEZE1): after the stall watch, which still
        // times and names the part before it, so the window is never where a route's git fan-out goes on.
        // And what a route declares it changed reaches every window as STATE_CHANGED once it has answered (STATEHUB1b, D169):
        // innermost, so it publishes off the window's thread and only what the route itself answered. What is under way it holds
        // in the loop's list, which the driver's UNDER_WAY route reads (STATEHUB1c2).
        builder.Services.UseMessageDispatcher((sp, dispatcher) => dispatcher
            .Use(RefusalLog.Middleware(log))
            .Use(sp.GetRequiredService<UiStallWatch>().Middleware)
            .Use(OffTheWindow.Middleware)
            .Use(StatePublisher.Middleware(
                sp.GetRequiredService<Shenora.Core.Events.IEventBus>(), sp.GetRequiredService<DriverLoop>().UnderWay)));

        var version = typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion;

        // The terminal's pipe (RPC1a, D171 point 3): a verb run in a terminal is this Daoris's client, its request run through the
        // dispatcher composed above, which every window's page uses, so its press is the window's: the same handler, the same
        // middleware, under way and heard in every window. The loop serves it once it holds the home and takes it down at its stop.
        builder.Services.AddSingleton(sp => new InstancePipe(
            sp.GetRequiredService<IMessageDispatcher>(), sp.GetRequiredService<Shenora.Core.Events.IEventBus>(),
            sp.GetRequiredService<DriverLoop>().Home, log, version: version));

        // The loop starts with the app, not with the window: the driver watches whether or not the
        // person is looking, which is the whole point of a driver.
        builder.OnStarting(app =>
        {
            log.Info("app.started", ("version", version), ("installed", inInstallApp));
            var loop = app.Services.GetRequiredService<DriverLoop>();
            // Composed: a swap waiting for this start is confirmed here, so the launcher keeps this build (D139 §5), and
            // the last swap's outcome is said once. Then the drain watches what is staged, holding the loop's starts.
            var updater = app.Services.GetRequiredService<InstallUpdater>();
            loop.Draining = () => updater.Draining;
            // An update's drain is a stop's beginning for the pipe too: a terminal is told Daoris is updating (§6.3).
            var pipe = app.Services.GetRequiredService<InstancePipe>();
            pipe.Draining = () => updater.Draining;
            loop.Pipe = pipe;
            // Its look may start the launcher for a build said *now*: through the start gate, off this thread
            // (UPDATERELAUNCH1). The drain is set before that wait, so before the loop starts; a failed look is said as the
            // updater's own loop says one.
            _ = updater.StartedAsync().ContinueWith(
                look => log.Warn("update.look", ("message", look.Exception!.GetBaseException().Message)),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            updater.Start();
            // FABRIC-EVENT2d: the system's resume is told to the loop's change watch, which reopens its roots and judges them;
            // SystemEvents raises on its own thread, and the handler lives as long as the process does.
            var power = new PowerResume();
            loop.Power = power;
            Microsoft.Win32.SystemEvents.PowerModeChanged += (_, change) =>
            {
                if (change.Mode == Microsoft.Win32.PowerModes.Resume) power.Raise();
            };
            loop.Start();
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

        // The viewer's colour scheme as the last run kept it (THEMESTART1, D22's THEMESCHEME1 note), beside the window's
        // geometry: the kit keeps the scheme in memory only and asks the app to pass the saved one at its start.
        var keptScheme = Path.Combine(builder.Paths.DataArea("config"), KeptScheme.FileName);

        builder.UseWindows(new WindowsHostOptions
        {
            MainForm = sp => sp.GetRequiredService<MainForm>(),
            // The app's scheme starts at the kept choice, so the engine and the window's first frame wear it before the
            // page has said it (the window's palette reads it, `MainForm.StartingPalette`), rather than the OS's and then a flip.
            ColorScheme = KeptScheme.Read(keptScheme),
            WindowState = new WindowStateHostOptions
            {
                Store = sp => new JsonFileWindowStateStore(
                    Path.Combine(sp.GetRequiredService<ShenoraPaths>().DataArea("config"), "window-state.json")),
            },
            // One instance per install, the kit's default, and a later launch is a new main window of it (ONEINSTANCE1, D171
            // point 2): the guard brings this instance's main window forward and hands the launch over on the window's thread,
            // and the hand-over opens the window off it, once the service answers. Written as it arrives, and every failure on
            // the way written too (ONEINSTANCE2).
            SingleInstance = LaterLaunch.Options(log),
        });

        using var app = builder.Build();
        // And each choice the page sets from here, for the next start.
        using var keeping = KeptScheme.Keep(app.Services.GetRequiredService<IColorScheme>(), keptScheme, log);
        app.Run();
    }
}
