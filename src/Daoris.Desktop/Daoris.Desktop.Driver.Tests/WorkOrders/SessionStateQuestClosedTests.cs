using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A driven session parked to ask the person about its own quest no longer needs them once that quest is closed.</summary>
public sealed class SessionStateQuestClosedTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static JsonObject Park(string kind = "driven", string? quest = "q1") => new()
    {
        ["id"] = "s1",
        ["repository"] = "engine",
        ["adapter"] = "claude-code",
        ["state"] = "awaiting-person",
        ["kind"] = kind,
        ["quest"] = quest,
        ["noteParts"] = new JsonArray(new JsonObject { ["code"] = "ended.parked-asked", ["values"] = new JsonObject(), ["text"] = "A line." }),
        ["said"] = new JsonArray(),
        ["created"] = T0.ToString("O"),
        ["updated"] = T0.AddMinutes(1).ToString("O"),
    };

    private static SessionGrouping Row(JsonObject record, string questStatus) =>
        SessionGroups.Read(SessionLook.From(
            new JsonArray(record).ToJsonString(), [new("q1", "game", "engine", "The work of #q1", "A body.", questStatus)], [], _ => 0)).Single();

    [Theory]
    [InlineData("Done")]
    [InlineData("Declined")]
    public void A_park_on_its_own_quest_reads_ended_once_the_quest_is_closed(string status)
    {
        var row = Row(Park(), status);

        Assert.Equal(SessionGroup.Done, row.State);
        Assert.Equal(SessionGroup.Done, row.Group);
    }

    [Theory]
    [InlineData("Taken")]
    [InlineData("Open")]
    public void A_park_on_a_quest_still_open_or_taken_waits_on_the_person(string status) =>
        Assert.Equal(SessionGroup.NeedsYou, Row(Park(), status).State);

    [Fact]
    public void A_wait_not_about_its_quest_is_unchanged()
    {
        Assert.Equal(SessionGroup.NeedsYou, Row(Park(kind: "chat"), "Done").State);
        Assert.Equal(SessionGroup.NeedsYou, Row(Park(quest: null), "Done").State);
    }
}
