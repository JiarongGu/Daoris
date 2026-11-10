using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A plugin server pinned to an exact prerelease is that version's folder, not a range and not the newest installed.</summary>
public sealed class ServerPrereleaseTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "server-prerelease", Guid.NewGuid().ToString("N")[..8]);

    public ServerPrereleaseTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public async Task A_prerelease_pin_is_the_folder_of_that_version_and_not_the_newest_installed()
    {
        var pinned = SessionServersTests.Lay(_home, "@acme/tickets", "1.0.0-rc.1");
        SessionServersTests.Lay(_home, "@acme/tickets", "2.0.0");
        var npx = NpxCommand.Read("npx", ["-y", "@acme/tickets@1.0.0-rc.1"]).Command!;

        var script = await ServerPackages.ScriptAsync(_home, npx, null, CancellationToken.None, npm: ["no-such-package-manager"]);

        Assert.Equal(pinned, script);
    }

    [Theory]
    [InlineData("1.0.0-rc.1", true)]
    [InlineData("2.3.4", true)]
    [InlineData("1.0.0-beta", true)]
    [InlineData("^1.0.0", false)]
    [InlineData("latest", false)]
    [InlineData("1.0.0-", false)]
    public void An_exact_server_version_is_numeric_or_a_complete_semver_with_its_prerelease(string version, bool exact) =>
        Assert.Equal(exact, ServerPackages.IsExact(version));
}
