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
//   DAORIS_SERVICE_URL     where the service is, and what the WebView shows   (default: http://localhost:5177)
//   DAORIS_SERVICE_KEY     sent as a bearer token when set                     (absent: local trust, D21)
//   DAORIS_DRIVER_CONFIG   the person's standing choices                       (default: ~/.daoris/driver.json)
//   DAORIS_HTTP_HOST       the host executable, when it lives somewhere unusual
//   DAORIS_REMOTE_URL      the machine's remote, with its key                  (or ~/.daoris/remote.json — D47 §9)
//   DAORIS_REMOTE_KEY        either env var present means the environment is the answer, whole
//   DAORIS_REMOTE_CONFIG   where that file is                                  (default: ~/.daoris/remote.json)
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
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
            serviceUrl));
        builder.Services.AddSingleton<MainForm>();
        // The session-control surface's host half: the page's driver controls land here (D46 §6).
        builder.Services.AddIpcModule<DriverModule>();

        // The loop starts with the app, not with the window: the driver watches whether or not the
        // person is looking, which is the whole point of a driver.
        builder.OnStarting(app => app.Services.GetRequiredService<DriverLoop>().Start());
        builder.OnStopping(app =>
        {
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
