using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The places of the machine's waiting items, composed by the terminal's reader (<see cref="QueueRows.Placed"/>) from the rows the
/// queues' reader gives, read from the shared table <c>fixtures/queue-places.json</c> row for row, as the page's twin
/// (<c>queuePlaces.test.ts</c>) reads it: so the window and <c>daoris queue</c> give one place for one item.
/// </summary>
public sealed class QueueRowsTwinTests
{
    public static IEnumerable<object[]> Cases => Table().Select(each => new object[] { each.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Each_item_takes_the_place_the_shared_table_gives(string name)
    {
        var table = Table().Single(each => each.GetProperty("name").GetString() == name);
        var rows = table.GetProperty("rows").EnumerateArray().Select(QueueRows.Parse).ToList();
        Assert.All(rows, row => Assert.NotNull(row));

        var placed = QueueRows.Placed(rows!).Select(spot => JsonSerializer.Serialize(new
        {
            queue = spot.Row.Queue, item = spot.Row.Item, position = spot.Position, of = spot.Of, ahead = spot.Ahead.Select(each => each.Item),
        }));
        var expected = table.GetProperty("places").EnumerateArray().Select(each => JsonSerializer.Serialize(new
        {
            queue = each.GetProperty("queue").GetString(), item = each.GetProperty("item").GetString(), position = each.GetProperty("position").GetInt32(),
            of = each.GetProperty("of").GetInt32(), ahead = each.GetProperty("ahead").EnumerateArray().Select(ahead => ahead.GetString()),
        }));

        Assert.Equal(expected, placed);
    }

    private static List<JsonElement> Table()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "queue-places.json")));
        return [.. table.RootElement.GetProperty("cases").EnumerateArray().Select(each => each.Clone())];
    }
}
