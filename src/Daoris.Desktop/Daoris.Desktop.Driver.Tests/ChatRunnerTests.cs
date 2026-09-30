using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation resolves its harness through the roster's LIVE set (D64): the driver's tick hands
/// the roster the build's adapters plus whatever the plugins declare, and a runner built before a
/// plugin arrived must still find the harness it declared.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ChatRunnerTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-chat-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private AdapterSet Declared()
    {
        var folder = Path.Combine(_home, "plugins", "acme.agent");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }""");
        return AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home, AdapterSet.Built().Names));
    }

    [Fact]
    public async Task A_harness_declared_after_the_runner_was_built_is_found_through_the_roster_s_live_set()
    {
        var built = AdapterSet.Built();
        var roster = new HarnessRoster(built, Path.Combine(_home, "harnesses.json"));
        using var service = new ServiceClient("http://127.0.0.1:1", null);
        var runner = new ChatRunner(service, built, _home, new SessionProcesses(), harnesses: roster);
        var config = DriverConfig.Load(Path.Combine(_home, "driver.json"));

        // Before the tick handed the roster the plugins: the driver's own refusal, naming what exists.
        var unknown = await Assert.ThrowsAsync<DriverException>(() =>
            runner.StartAsync("engine", "acme-agent", config));
        Assert.Contains("unknown adapter 'acme-agent'", unknown.Message);

        // After: the harness resolves, and the conversation gets as far as asking the service —
        // which is nowhere here, so THAT is the failure, and it is not the adapter's.
        roster.Use(Declared());
        await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
            runner.StartAsync("engine", "acme-agent", config));
    }
}
