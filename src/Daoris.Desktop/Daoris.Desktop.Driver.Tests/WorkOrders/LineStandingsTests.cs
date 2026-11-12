using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A line of work's state, read through its membership: working where any part or co-worker works, needing the person where any
/// waits on them, else as its newest part ended; every part's row carries it, naming the part or co-worker it is read from.
/// </summary>
public sealed class LineStandingsTests
{
    private static JsonObject Record(string id, string state, int minute, string? line = null, string? from = null, string? fromWhy = null) => new()
    {
        ["id"] = id, ["repository"] = "engine", ["adapter"] = "claude-code", ["state"] = state, ["kind"] = "chat",
        ["entry"] = $"session:{line ?? id}/engine", ["line"] = line ?? id, ["from"] = from, ["fromWhy"] = fromWhy,
        ["created"] = new DateTimeOffset(2026, 11, 12, 9, minute, 0, TimeSpan.Zero).ToString("O"),
        ["updated"] = new DateTimeOffset(2026, 11, 12, 9, minute + 1, 0, TimeSpan.Zero).ToString("O"),
    };

    private static JsonObject Serving(string id, string state, int minute, string serves, string? opinion = null)
    {
        var record = Record(id, state, minute);
        record["serves"] = serves;
        record["opinion"] = opinion;
        return record;
    }

    private static SessionLook Look(params JsonObject[] records) => SessionLook.From(new JsonArray(records).ToJsonString(), [], [], _ => 0);

    private static SessionGrouping Row(SessionLook look, string session) => Assert.Single(SessionGroups.Read(look), row => row.Session == session);

    [Fact]
    public void A_line_whose_newest_part_works_while_its_earlier_part_ended_is_working_on_every_part()
    {
        var look = Look(
            Record("a674bf6a", "stopped", 0),
            Record("80c78acf", "working", 10, line: "a674bf6a", from: "a674bf6a", fromWhy: "agent"));

        var earlier = Row(look, "a674bf6a");

        Assert.Equal(SessionGroup.Done, earlier.State);
        Assert.Equal(new LineStanding(SessionGroup.Working, SessionGroup.Working, "working", "80c78acf", LineRole.Part), earlier.Line);
        Assert.Equal(earlier.Line, Row(look, "80c78acf").Line);
        var entry = Assert.Single(SessionGroups.ReadEntries(look));
        Assert.Equal((SessionGroup.Working, "80c78acf"), (entry.Group, entry.Leading));
    }

    [Fact]
    public void A_session_whose_helper_works_is_working_with_the_helper_named_and_the_helper_is_no_entry_of_its_own()
    {
        var look = Look(Record("5e55a000", "completed", 0), Serving("4e1b0001", "working", 5, serves: "5e55a000"));

        Assert.Equal(new LineStanding(SessionGroup.Working, SessionGroup.Working, "working", "4e1b0001", LineRole.Helper), Row(look, "5e55a000").Line);
        var entry = Assert.Single(SessionGroups.ReadEntries(look));
        Assert.Equal((SessionGroup.Working, "5e55a000"), (entry.Group, entry.Leading));
        var coworker = Assert.Single(entry.Coworkers);
        Assert.Equal(("4e1b0001", LineRole.Helper, "5e55a000", SessionGroup.Working), (coworker.Session, coworker.Role, coworker.Serves, coworker.State));
    }

    [Fact]
    public void A_line_needs_the_person_where_its_reviewer_waits_on_them_before_a_part_that_works()
    {
        var look = Look(
            Record("5e55a000", "stopped", 0),
            Record("5e55a001", "working", 10, line: "5e55a000", from: "5e55a000", fromWhy: "press"),
            Serving("7e71e000", "awaiting-person", 12, serves: "5e55a000", opinion: "o1"));

        var line = Row(look, "5e55a000").Line!;

        Assert.Equal((SessionGroup.NeedsYou, "7e71e000", LineRole.Reviewer), (line.State, line.By, line.Role));
    }

    [Fact]
    public void A_line_whose_newest_part_is_a_left_quests_session_reads_left_on_every_part()
    {
        var left = new QuestView("q1", "game", "engine", "The work of #q1", "A body.", "Done")
        {
            Answers = [new(1, "met", null, null), new(2, null, "the part is held", "their words")],
            Held = true,
        };
        var newest = Record("5e55a001", "completed", 10, line: "5e55a000", from: "5e55a000", fromWhy: "press");
        newest["quest"] = "q1";
        var look = SessionLook.From(new JsonArray(Record("5e55a000", "stopped", 0), newest).ToJsonString(), [left], [], _ => 0);

        var earlier = Row(look, "5e55a000");

        Assert.Equal(SessionGroup.Done, earlier.State);
        Assert.Equal(new LineStanding(SessionGroup.NeedsYou, SessionGroup.NeedsYou, SessionReason.Left, "5e55a001", LineRole.Part), earlier.Line);
        Assert.Equal(earlier.Line, Row(look, "5e55a001").Line);
    }

    /// <summary>The person's yes on the departure is their word on it: the line reads done, part left, and asks nothing.</summary>
    [Fact]
    public void A_line_whose_quest_the_person_accepted_a_departure_of_reads_done_part_left_on_every_part()
    {
        var left = new QuestView("q1", "game", "engine", "The work of #q1", "A body.", "Done")
        {
            Answers = [new(1, "met", null, null), new(2, null, "the part is held", "their words")],
        };
        var newest = Record("5e55a001", "completed", 10, line: "5e55a000", from: "5e55a000", fromWhy: "press");
        newest["quest"] = "q1";
        var look = SessionLook.From(new JsonArray(Record("5e55a000", "stopped", 0), newest).ToJsonString(), [left], [], _ => 0);

        var earlier = Row(look, "5e55a000");

        Assert.Equal(new LineStanding(SessionGroup.Done, SessionGroup.Done, SessionReason.PartLeft, "5e55a001", LineRole.Part), earlier.Line);
        Assert.Equal(earlier.Line, Row(look, "5e55a001").Line);
    }

    [Fact]
    public void An_ended_line_is_as_its_newest_part_ended()
    {
        var look = Look(
            Record("5e55a000", "completed", 0),
            Record("5e55a001", "failed", 10, line: "5e55a000", from: "5e55a000", fromWhy: "retry"));

        Assert.Equal((SessionGroup.Failed, "5e55a001"), (Row(look, "5e55a000").Line!.State, Row(look, "5e55a000").Line!.By));
    }
}
