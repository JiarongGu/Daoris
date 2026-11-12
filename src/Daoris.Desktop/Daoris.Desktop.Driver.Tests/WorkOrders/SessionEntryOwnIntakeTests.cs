using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>How a part began where its quest came from its own line's ask: the line's own work is not a sibling's request.</summary>
public sealed class SessionEntryOwnIntakeTests
{
    private static JsonObject Record(string id, string? quest, string entry, string? ask, int minute) => new()
    {
        ["id"] = id, ["repository"] = ask is null ? "engine" : "ask #841c38", ["adapter"] = "claude-code", ["state"] = "completed",
        ["kind"] = ask is null ? "driven" : "chat", ["quest"] = quest, ["ask"] = ask, ["entry"] = entry, ["line"] = quest ?? id,
        ["created"] = new DateTimeOffset(2026, 10, 2, 9, minute, 0, TimeSpan.Zero).ToString("O"),
        ["updated"] = new DateTimeOffset(2026, 10, 2, 9, minute + 1, 0, TimeSpan.Zero).ToString("O"),
    };

    private static QuestView Quest(string id, string from, string? publishedBy) =>
        new(id, from, "engine", $"The work of #{id}", "A body.", "Done") { PublishedBy = publishedBy };

    [Fact]
    public void A_quest_the_lines_own_intake_published_opens_its_part_and_one_another_session_published_is_asked()
    {
        var records = new JsonArray(
            Record("1n7a8e00", null, "ask:841c38/intake", "841c38", 0),
            Record("5e55a000", "q9", "ask:841c38/engine", null, 10),
            Record("5e55a001", "q8", "ask:841c38/engine", null, 20));
        var look = SessionLook.From(
            records.ToJsonString(), [Quest("q9", "ask #841c38", "1n7a8e00"), Quest("q8", "db", "5e55b000")], [], _ => 0);

        var entries = SessionGroups.ReadEntries(look);

        Assert.Equal(["ask:841c38/engine", "ask:841c38/engine"], entries.Select(entry => entry.Key));
        Assert.Equal(
            [PartBegan.Asked, PartBegan.Opened], entries.Select(entry => Assert.Single(entry.Parts).Began).Order(StringComparer.Ordinal));
    }
}
