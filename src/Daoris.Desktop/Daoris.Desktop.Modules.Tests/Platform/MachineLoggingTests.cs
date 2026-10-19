using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shenora.Core.Shell;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ONEINSTANCE2: the container the application composes hands the kit a logger that writes to the machine log. LOG1a registered
/// <see cref="MachineLogProvider"/> as a provider, held alone by its own tests, but nothing in the container made loggers of it:
/// the kit asks for an <c>ILogger&lt;T&gt;</c> and got none, so the single-instance guard's warnings (a channel that would not
/// open, a running instance that could not be reached), the window registry's and the engine's all went nowhere.
/// </summary>
public sealed class MachineLoggingTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-machinelogging-" + Guid.NewGuid().ToString("N")[..8]);

    public MachineLoggingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string[] Lines()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        })];
    }

    /// <summary>The kit's own guard, given the logger the container makes, says a launch it could not hand over.</summary>
    [Fact]
    public void The_kits_single_instance_guard_warns_into_the_machine_log_through_the_container()
    {
        using var log = new MachineLog(_home, "desktop");
        using var services = new ServiceCollection().AddMachineLog(log).BuildServiceProvider();

        var name = "DaorisMachineLogging" + Guid.NewGuid().ToString("N")[..8];
        using var guard = new SingleInstanceGuard(name, _home, services.GetRequiredService<ILogger<SingleInstanceGuard>>());
        // Nobody listens on this scope's channel: the launch is not handed over, and the guard says so.
        Assert.False(guard.ActivateRunning([], TimeSpan.FromMilliseconds(50)));

        var line = Assert.Single(Lines());
        Assert.Contains("\"level\":\"warn\",\"event\":\"log\"", line);
        Assert.Contains($"\"category\":\"{typeof(SingleInstanceGuard).FullName}\"", line);
        Assert.Contains($"The running instance of {name} could not be reached", line);
    }

    /// <summary>A category asked for by its name, as the kit's code asks, and the factory's own, write to the same log.</summary>
    [Fact]
    public void A_logger_by_category_and_one_by_type_both_reach_the_log()
    {
        using var log = new MachineLog(_home, "desktop");
        using var services = new ServiceCollection().AddMachineLog(log).BuildServiceProvider();

        services.GetRequiredService<ILoggerFactory>().CreateLogger("Shenora.Windows").LogError("a window failed to open");
        services.GetRequiredService<ILogger<MachineLoggingTests>>().LogWarning("a page was slow");
        services.GetRequiredService<ILogger<MachineLoggingTests>>().LogInformation("a page opened");

        var lines = Lines();
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"level\":\"error\",\"event\":\"log\",\"data\":{\"category\":\"Shenora.Windows\"", lines[0]);
        Assert.Contains($"\"category\":\"{typeof(MachineLoggingTests).FullName}\",\"message\":\"a page was slow\"", lines[1]);
    }

    /// <summary>
    /// A launch handed over is the kit's information, not a warning: the guard's "this launch starts in its place" and the
    /// engine's "Chromium handed this process a later launch", which is the only trace of a later launch the guard let through.
    /// Those categories, named by the application, are written at information too; every other stays at warning and above.
    /// </summary>
    [Fact]
    public void Information_is_written_from_the_hand_overs_categories_alone()
    {
        using var log = new MachineLog(_home, "desktop");
        using var services = new ServiceCollection()
            .AddMachineLog(log, typeof(SingleInstanceGuard).FullName!, "Shenora.Chromium.ChromiumEngine")
            .BuildServiceProvider();
        var factory = services.GetRequiredService<ILoggerFactory>();

        factory.CreateLogger("Shenora.Chromium.ChromiumEngine").LogInformation("[Shenora.Chromium] Chromium handed this process a later launch of the app");
        services.GetRequiredService<ILogger<SingleInstanceGuard>>().LogDebug("[Shenora] a channel reopened");
        factory.CreateLogger("Shenora.Windows").LogInformation("a window opened");

        var line = Assert.Single(Lines());
        Assert.Contains("\"level\":\"info\",\"event\":\"log\",\"data\":{\"category\":\"Shenora.Chromium.ChromiumEngine\"", line);
    }
}
