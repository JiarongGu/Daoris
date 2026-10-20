using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (D191's settling of the design): under the `on` rule an agent's ask goes to the next agent in the person's order, the
/// roster's order the page's hand-over offer draws from (`otherToolDoors`, MIXAGENT1b-iii). The driver's half of a TWIN: both are
/// held, row for row, to ONE table, <c>fixtures/hand-over-order.json</c>, which the page's <c>handOverOrder.test.ts</c> reads too.
/// </summary>
public sealed class HandOverOrderTests
{
    public static TheoryData<string, string, string, string> Rows()
    {
        var data = new TheoryData<string, string, string, string>();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "hand-over-order.json")));
        foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray())
        {
            data.Add(row[0].GetString()!, row[1].GetString()!, row[2].GetRawText(), row[3].GetRawText());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void The_order_is_the_pages_order(string name, string own, string doors, string order)
    {
        using var listed = JsonDocument.Parse(doors);
        var roster = listed.RootElement.EnumerateArray()
            .Select(door => new HandOverDoor(
                door[0].GetString()!, door[1].GetBoolean(), door[2].ValueKind == JsonValueKind.Null ? null : door[2].GetString()))
            .ToList();
        using var expected = JsonDocument.Parse(order);

        Assert.True(
            expected.RootElement.EnumerateArray().Select(each => each.GetString()!).SequenceEqual(HandOverOrder.Others(own, roster)),
            $"{name}: {string.Join(", ", HandOverOrder.Others(own, roster))}");
    }
}
