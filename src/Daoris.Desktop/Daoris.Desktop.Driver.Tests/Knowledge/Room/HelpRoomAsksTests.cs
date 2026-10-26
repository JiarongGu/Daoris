using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomAsks"/>: the asks not closed, by id.</summary>
public sealed class HelpRoomAsksTests
{
    /// <summary>HELP6: the asks not closed are listed by id, so a delete of one made by mistake can name it.</summary>
    [Fact]
    public void The_room_lists_the_asks_by_id()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine with
        {
            OpenAsks = [new HelpAsk("a1b2c3d4", "fix the chunk streamer's stall", "work", "Published", ["q1a2b3c4"])],
        });

        Assert.Contains("- `#a1b2c3d4` at `work`: “fix the chunk streamer's stall” (Published; quests `#q1a2b3c4`)", agents);
    }
}
