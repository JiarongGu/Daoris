using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomWorkspaces"/>: each workspace's repositories, or where to begin when there are none.</summary>
public sealed class HelpRoomWorkspacesTests
{
    [Fact]
    public void A_machine_with_nothing_registered_says_so_and_where_to_begin()
    {
        var agents = HelpRoom.Render(new HelpMachine());

        Assert.Contains("No repository is registered on this machine", agents);
        Assert.Contains("daoris connect", agents);
        Assert.Contains("no agent answers asks", agents);
    }
}
