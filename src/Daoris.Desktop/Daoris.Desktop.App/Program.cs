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
//   DAORIS_SERVICE_URL     where the service is, and what the WebView shows   (default: http://localhost:5177)
//   DAORIS_SERVICE_KEY     sent as a bearer token when set                     (absent: local trust, D21)
//   DAORIS_DRIVER_CONFIG   the person's standing choices                       (default: $DAORIS_HOME/driver.json)
//   DAORIS_HTTP_HOST       the host executable, when it lives somewhere unusual
//   DAORIS_REMOTE_URL      one workspace's remote, with its key                (or $DAORIS_HOME/remotes.json — D48 §5)
//   DAORIS_REMOTE_KEY        either env var present means the environment is the answer, whole,
//   DAORIS_REMOTE_WORKSPACE  for the workspace named here                      (absent: `default`)
//   DAORIS_REMOTE_CONFIG   where the map is                                    (default: $DAORIS_HOME/remotes.json)
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // The home before anything else (D63): an install's `data/` folder is the Daoris home for this
        // process and every host and session it spawns. Before the builder, because every module
        // captures its path at construction — and a workspace build is left alone, so the dev loop's
        // scratch home stays the dev loop's.
        var home = InstallHome.Establish(AppContext.BaseDirectory);

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

        var builder = ShenoraApplication.CreateBuilder(new ShenoraApplicationOptions
        {
            Args = args,
            ApplicationName = "Daoris",
        });

        builder.Services.AddSingleton(sp => new WebViewEnvironmentOptions
        {
            UserDataFolder = sp.GetRequiredService<ShenoraPaths>().DataArea("webview2"),
            IsDevelopment = sp.GetRequiredService<ShenoraEnvironment>().IsDevelopment,
        });

        builder.Services.AddSingleton(sp => new WebViewHostOptions
        {
            Environment = sp.GetRequiredService<WebViewEnvironmentOptions>(),
            // The server-backed profile: the platform is the HTTP host's own bundle, the same bytes a
            // browser gets — one UI, two shells (D38). The same URL serves dev mode, because the dev
            // loop's server IS the host.
            ProductionUrl = serviceUrl,
            DevUrl = serviceUrl,
            BackgroundColor = Color.FromArgb(30, 30, 30),
        });

        builder.Services.AddSingleton(new HostSupervisor(serviceUrl));
        builder.Services.AddSingleton(sp => new DriverLoop(
            sp.GetRequiredService<Shenora.Core.Events.IEventBus>(),
            sp.GetRequiredService<HostSupervisor>(),
            serviceUrl,
            home));
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
        // Daoris's own browser (D78): one more of those windows, in an environment of its own, with its
        // profile under the home — the directory `driver.json` sits in, as every machine file's is.
        builder.Services.AddSingleton(sp => new BrowserHost(
            sp.GetRequiredService<SecondaryWindows>(),
            sp.GetRequiredService<ShenoraPaths>(),
            Path.GetDirectoryName(Path.GetFullPath(Daoris.Driver.DriverConfig.ResolvePath()))!));
        builder.Services.AddSingleton<Daoris.Driver.IInAppBrowser>(sp => sp.GetRequiredService<BrowserHost>());
        builder.Services.AddSingleton(new PlatformAddress(serviceUrl));
        builder.Services.AddIpcModule<WindowsModule>();

        // The loop starts with the app, not with the window: the driver watches whether or not the
        // person is looking, which is the whole point of a driver.
        builder.OnStarting(app => app.Services.GetRequiredService<DriverLoop>().Start());
        builder.OnStopping(app =>
        {
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
