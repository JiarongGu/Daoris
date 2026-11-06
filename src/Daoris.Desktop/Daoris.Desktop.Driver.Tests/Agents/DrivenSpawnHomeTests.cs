using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven start is handed the driver's own home, never the process's: the session's empty <c>gh</c> and <c>az</c> folders are made under
/// it. A driver on another home's configuration, or a test's driver beside the host's home, handed its sessions that other home's.
/// </summary>
public sealed class DrivenSpawnHomeTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "driven-spawn-home-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData("acp-stub")]
    [InlineData("claude-code")]
    public void A_driven_start_is_handed_the_driver_s_home_and_not_the_process_s(string name)
    {
        var adapter = ProviderRegistry.Built().Door(name);
        using var service = new ServiceClient("http://ledger.test", null);
        var driver = new Daoris.Driver.Driver(
            service,
            DriverConfig.Empty with { Commands = new Dictionary<string, IReadOnlyList<string>> { [name] = ["node", "stand-in.mjs"] } },
            ProviderRegistry.Built(),
            _home);
        var target = new SessionTarget("q-1", "title", "body", "asker", "engine", Path.Combine(_home, "engine"), "http://ledger.test");

        var info = driver.Prepare(adapter, target, new HarnessSelection(null)).Info;

        Assert.NotEqual(_home, DaorisHome.Resolve());
        Assert.Equal(GitSessionHolds.Empty(_home, "gh"), Handed(info, GitSessionHolds.GhFolder));
        Assert.Equal(GitSessionHolds.Empty(_home, "az"), Handed(info, GitSessionHolds.AzFolder));
    }

    private static string? Handed(ProcessStartInfo info, string name) => info.Environment.TryGetValue(name, out var value) ? value : null;
}
