using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris's doors that start nothing (HELP10): each is the code the screen's own route runs, tested here without a
/// process, so a worktree's fast half holds them. <c>HelpDoorsTests</c> holds the doors that start one, or ask the
/// roster again.
/// </summary>
public sealed class HelpFileDoorsTests : Bridge
{
    private DriverModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    /// <summary>
    /// A default on an agent Daoris manages no accounts for is refused in <c>HARNESS_ACTION</c>'s own words, before
    /// anything is written.
    /// </summary>
    [Fact]
    public async Task A_default_on_an_agent_with_no_toolchain_is_refused_as_the_agents_screens_route_refuses_it()
    {
        var refused = await Assert.ThrowsAsync<DriverException>(() =>
            Module().HelpDoors(null).SetDefaultAccountAsync("acp-stub", "work", null, CancellationToken.None));

        Assert.Contains("Daoris manages no toolchain for `acp-stub`", refused.Message);
        Assert.False(File.Exists(HarnessSettingsPath));
    }
}
