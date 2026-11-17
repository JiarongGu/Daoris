using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The rooms a lanes file declares. A lane with <c>builds</c> or a <c>room</c> is a room, or part
/// of the one its <c>room</c> names, so two lanes keep their names and are one room; a laneless group that names a room is part of it;
/// a lane or a group that is neither is said to be in none. One whose <c>room</c> lists several is in each, each of its paths in the
/// room of the unit whose folder holds it. A malformed declaration is refused with its reason.
/// </summary>
public sealed class LaneRoomsTests
{
    private const string Declared = """
        {
          "lanes": [
            { "id": "web-shell", "room": "web", "paths": ["web/**", "!web/settings/**"], "builds": { "units": ["web/package.json"] } },
            { "id": "web-settings", "room": "web", "paths": ["web/settings/**"] },
            { "id": "api", "paths": ["api/**"], "builds": { "units": ["api/Api.csproj"], "reads": ["Directory.Build.props"] } },
            { "id": "misc", "paths": ["misc/**"] }
          ],
          "laneless": [
            { "what": "The documents.", "room": "docs", "paths": ["docs/**", "Directory.Build.props"] },
            { "what": "The harness's settings.", "paths": [".mcp.json"] }
          ]
        }
        """;

    [Fact]
    public void A_room_names_two_lanes_a_laneless_group_joins_one_and_the_rest_is_in_none()
    {
        var read = LaneRooms.Read(Declared);

        Assert.Empty(read.Problems);
        Assert.Equal(["web", "api", "docs"], read.Rooms.Select(room => room.Id));
        var web = read.Rooms[0];
        Assert.Equal(["web-shell", "web-settings"], web.Members);
        Assert.True(web.Owns("web/settings/Settings.tsx"));
        Assert.True(web.Owns("web/package.json"));
        Assert.Equal(["web/package.json"], web.Units);
        Assert.Equal(["Directory.Build.props"], read.Rooms[1].Reads);
        Assert.True(read.Rooms[2].Owns("Directory.Build.props"));
        Assert.Equal(["misc", ".mcp.json"], read.InNone);
        Assert.True(read.Placed("misc/a.txt"));
        Assert.False(read.Placed("elsewhere.txt"));
    }

    [Fact]
    public void A_lane_naming_several_rooms_is_in_each_and_each_path_in_the_room_of_the_unit_whose_folder_holds_it()
    {
        var read = LaneRooms.Read("""
            {
              "lanes": [
                { "id": "driver", "paths": ["driver/**", "!driver/Help/**"], "builds": { "units": ["driver/Driver.csproj"] } },
                { "id": "web", "paths": ["web/**", "!web/help/**"], "builds": { "units": ["web/package.json"] } },
                { "id": "assistant", "room": ["driver", "web"], "paths": ["driver/Help/**", "web/help/**", "help.md"] }
              ]
            }
            """);

        Assert.Empty(read.Problems);
        Assert.Equal(["driver", "web"], read.Rooms.Select(room => room.Id));
        Assert.Equal(["driver", "assistant"], read.Rooms[0].Members);
        Assert.Equal(["web", "assistant"], read.Rooms[1].Members);
        Assert.Equal("driver", read.RoomOf("driver/Help/HelpRoom.cs"));
        Assert.Equal("web", read.RoomOf("web/help/Ask.tsx"));
        Assert.True(read.Rooms[1].Owns("web/help/Ask.tsx"));
        Assert.False(read.Rooms[0].Owns("web/help/Ask.tsx"));
        Assert.Empty(read.InNone);
        Assert.Equal("", read.RoomOf("help.md"));
        Assert.Equal("lane 'assistant', in rooms driver and web", read.Astray("help.md"));
        Assert.Null(read.Astray("web/help/Ask.tsx"));
    }

    [Fact]
    public void Lanes_with_no_builds_and_no_room_declare_no_rooms()
    {
        var read = LaneRooms.Read("""{ "lanes": [ { "id": "core", "paths": ["src/**"] } ] }""");

        Assert.Empty(read.Problems);
        Assert.False(read.Declares);
        Assert.False(LaneRooms.Read(null).Declares);
        Assert.Empty(LaneRooms.Read(null).Problems);
    }

    [Theory]
    [InlineData("""{ "lanes": [ { "id": "a", "paths": ["a/**"], "builds": ["a/A.csproj"] } ] }""", "lane 'a': 'builds' is not an object")]
    [InlineData("""{ "lanes": [ { "id": "a", "paths": ["a/**"], "builds": { "units": ["a/*.csproj"] } } ] }""", "lane 'a': the unit 'a/*.csproj' is one file, never a pattern")]
    [InlineData("""{ "lanes": [ { "id": "a", "paths": ["a/**"], "builds": { "reads": ["../other/"] } } ] }""", "lane 'a': 'builds.reads' path '../other/' leaves the repository")]
    [InlineData("""{ "lanes": [ { "id": "a", "paths": ["a/**"], "builds": { "makes": [] } } ] }""", "lane 'a': 'builds.makes' is not units, reads or writes")]
    [InlineData("""{ "lanes": [ { "id": "a", "room": "Web", "paths": ["a/**"] } ] }""", "lane 'a': its room must be lower-case letters, digits and dashes, starting with a letter")]
    [InlineData("""{ "lanes": [ { "id": "a", "room": ["web", "Driver"], "paths": ["a/**"] } ] }""", "lane 'a': its room must be lower-case letters, digits and dashes, starting with a letter")]
    [InlineData("""{ "lanes": [ { "id": "a", "room": ["web"], "paths": ["a/**"] } ] }""", "lane 'a': a list of rooms names two or more, each once")]
    [InlineData("""{ "lanes": [ { "id": "a", "room": ["web", "web"], "paths": ["a/**"] } ] }""", "lane 'a': a list of rooms names two or more, each once")]
    [InlineData("""{ "lanes": [ { "id": "a", "room": ["web", "driver"], "paths": ["a/**"], "builds": {} } ] }""", "lane 'a': it names several rooms and has builds, which are one room's")]
    [InlineData("""{ "lanes": [], "laneless": [ { "what": "x", "paths": ["x/**"], "builds": { "units": [] } } ] }""", "laneless group 1: it has builds and names no room")]
    [InlineData("{ not json", "daoris.lanes.json is not valid JSON")]
    public void A_malformed_declaration_is_refused_with_its_reason(string text, string problem)
    {
        var read = LaneRooms.Read(text);

        Assert.Contains(problem, read.Problems);
        Assert.Empty(read.Rooms);
    }
}
