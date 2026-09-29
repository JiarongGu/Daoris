using Daoris.Driver;
using Microsoft.Extensions.Logging;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The logging framework's own lines into the machine log (LOG1a, D94): its warnings and errors, from
/// every category — the kit's, the engine's, the modules' — which went nowhere a person could read.
/// </summary>
public sealed class MachineLogProviderTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-logprov-" + Guid.NewGuid().ToString("N")[..8]);

    public MachineLogProviderTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string[] Lines()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
    }

    [Fact]
    public void A_warning_or_an_error_from_any_category_is_written_as_a_log_line()
    {
        using var log = new MachineLog(_home, "desktop");
        using var provider = new MachineLogProvider(log);

        provider.CreateLogger("Shenora.Chromium").LogWarning("the engine restarted a renderer");
        provider.CreateLogger(typeof(MachineLogProviderTests).FullName!)
            .LogError(new InvalidOperationException("wedged"), "a window failed to open");

        var lines = Lines();
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"level\":\"warn\",\"event\":\"log\",\"data\":{\"category\":\"Shenora.Chromium\",\"message\":\"the engine restarted a renderer\"", lines[0]);
        Assert.Contains("\"level\":\"error\",\"event\":\"log\"", lines[1]);
        Assert.Contains("\"message\":\"a window failed to open\"", lines[1]);
        Assert.Contains("System.InvalidOperationException: wedged", lines[1]);
    }

    /// <summary>Information and below stay out: the framework's chatter is not what the log is for.</summary>
    [Fact]
    public void Information_and_below_are_not_written()
    {
        using var log = new MachineLog(_home, "desktop");
        using var provider = new MachineLogProvider(log);

        var logger = provider.CreateLogger("Shenora");
        logger.LogInformation("a window opened");
        logger.LogDebug("a frame painted");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.Empty(Lines());
    }
}
