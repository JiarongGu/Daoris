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

    public static TheoryData<string, string, string, string, string> ActorRows()
    {
        var data = new TheoryData<string, string, string, string, string>();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "hand-over-order.json")));
        foreach (var row in document.RootElement.GetProperty("actorRows").EnumerateArray())
        {
            data.Add(row[0].GetString()!, row[1].GetString()!, row[2].GetRawText(), row[3].GetRawText(), row[4].GetRawText());
        }

        return data;
    }

    /// <summary>
    /// MODELROLE1e (D205 §3.5(e), round 4's Opus N1): once the session's actor has an order of its own, a hand-over goes to the actor's next
    /// entry on another agent with that entry's model, and no maker outside its entries is ever a step. The page's <c>nextChoices</c> is
    /// held to the same rows.
    /// </summary>
    [Theory]
    [MemberData(nameof(ActorRows))]
    public void The_actors_order_is_the_pages_order(string name, string own, string doors, string entries, string steps)
    {
        using var listed = JsonDocument.Parse(doors);
        var roster = listed.RootElement.EnumerateArray()
            .Select(door => new HandOverDoor(
                door[0].GetString()!, door[1].GetBoolean(), door[2].ValueKind == JsonValueKind.Null ? null : door[2].GetString()))
            .ToList();
        using var kept = JsonDocument.Parse(entries);
        var order = kept.RootElement.EnumerateArray()
            .Select(entry => new ActorEffective(entry[0].GetString()!, entry[1].GetString(), null, entry[2].GetString()!))
            .ToList();
        using var expected = JsonDocument.Parse(steps);

        var taken = HandOverOrder.Steps(own, roster, order);

        Assert.True(
            expected.RootElement.EnumerateArray()
                .Select(each => new HandOverStep(each[0].GetString()!, each[1].GetString(), null, each[2].GetString()!))
                .SequenceEqual(taken),
            $"{name}: {string.Join(", ", taken.Select(step => $"{step.Harness}/{step.Model}/{step.From}"))}");
    }
}
