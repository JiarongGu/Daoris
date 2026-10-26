using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomMakingAPlugin"/>: a plugin is made as an ask, and only a landed one is added.</summary>
public sealed class HelpRoomMakingAPluginTests
{
    /// <summary>
    /// PLUG9: a plugin runs on this machine as the person, so making one is work. The room sends it to the
    /// repository that holds plugins as an ask, naming every point a plugin may speak on, says the helper
    /// never writes one and never proposes adding one that has not landed, and leaves where plugins live
    /// to the person when no repository says it holds them.
    /// </summary>
    [Fact]
    public void The_room_says_a_plugin_is_made_as_an_ask_and_only_a_landed_one_is_added()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("## Making a plugin", agents);
        Assert.Contains("never write one yourself", agents);
        Assert.Contains("as an ask (`ask_propose`) at the workspace of the repository that holds plugins", agents);
        foreach (var point in HookPoints.All) Assert.Contains($"`{point}`", agents);
        Assert.Contains("makes it with its tests", agents);
        Assert.Contains("the person decides where plugins live (a repository of their own, connected like any other)", agents);
        Assert.Contains("never propose adding one that has not landed", agents);
        Assert.Contains("`plugin_propose`", agents);
        // The doors (D50): the terminal twins of what a plugin card applies.
        Assert.Contains("`daoris plugin add <folder>`, `daoris plugin enable|disable <id>`", agents);
    }
}
