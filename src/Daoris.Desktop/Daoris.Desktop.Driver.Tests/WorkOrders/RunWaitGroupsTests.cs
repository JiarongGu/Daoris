using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's step waiting on the person puts the run's newest session here in Needs you, whatever its ending or archive mark, with
/// the waits on its row; the terminal's listing says each and names the run it opens.
/// </summary>
public sealed class RunWaitGroupsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static JsonObject Record(string id, string state, string quest, int at, string repository = "engine") => new()
    {
        ["id"] = id,
        ["repository"] = repository,
        ["adapter"] = "claude-code",
        ["state"] = state,
        ["kind"] = "driven",
        ["quest"] = quest,
        ["said"] = new JsonArray(),
        ["created"] = T0.AddMinutes(at).ToString("O"),
        ["updated"] = T0.AddMinutes(at + 1).ToString("O"),
    };

    private static QuestView Quest(string id, string status = "Done", string to = "engine", string? parent = null) =>
        new(id, "game", to, $"The work of #{id}", "A body.", status) { Parent = parent };

    private static SessionLook Look(JsonObject[] records, QuestView[] quests, PersonWait[] waits, string[]? archived = null) =>
        SessionLook.From(new JsonArray([.. records]).ToJsonString(), quests, [], _ => 0) with
        {
            Archived = (archived ?? []).ToDictionary(id => id, _ => T0, StringComparer.Ordinal),
            RunWaits = waits,
        };

    private static PersonWait Waits(string run, string node, string why) => new(run, node, why, "engine", T0.AddMinutes(30));

    private static SessionGrouping Row(SessionLook look, string session) => SessionGroups.Read(look).Single(row => row.Session == session);

    [Fact]
    public void A_session_whose_run_waits_on_an_Accept_needs_your_review_though_it_ended_and_is_archived()
    {
        var look = Look([Record("s1", "completed", "q1", 0)], [Quest("q1")], [Waits("q1", "review", PersonWaits.Accept)], archived: ["s1"]);

        var row = Row(look, "s1");

        Assert.Equal((SessionGroup.NeedsYou, SessionReason.Review), (row.Group, row.Reason));
        Assert.Equal([("q1", "review", PersonWaits.Accept)], row.Waits!.Select(wait => (wait.Run, wait.Node, wait.Why)));
    }

    [Fact]
    public void A_run_waiting_on_an_approval_puts_its_newest_session_in_needs_you_and_leaves_the_earlier_one_as_it_ended()
    {
        var look = Look(
            [Record("s1", "failed", "q1", 0), Record("s2", "completed", "q1", 5)], [Quest("q1")], [Waits("q1", "approve", PersonWaits.Approval)]);

        Assert.Equal((SessionGroup.NeedsYou, PersonWaits.Approval), (Row(look, "s2").Group, Row(look, "s2").Reason));
        Assert.Equal(SessionGroup.Failed, Row(look, "s1").Group);
        Assert.Null(Row(look, "s1").Waits);
    }

    [Fact]
    public void A_later_quest_of_the_chain_in_the_repository_stands_for_its_run_and_a_run_waiting_on_nothing_places_nothing()
    {
        var look = Look(
            [Record("s1", "completed", "q1", 0), Record("s2", "completed", "q2", 5), Record("s3", "completed", "q3", 9, repository: "game")],
            [Quest("q1"), Quest("q2", parent: "q1"), Quest("q3", to: "game")],
            [Waits("q1", "review", PersonWaits.Accept), Waits("q1", "check", PersonWaits.Held)]);

        var row = Row(look, "s2");
        Assert.Equal(SessionGroup.NeedsYou, row.Group);
        Assert.Equal([PersonWaits.Accept, PersonWaits.Held], row.Waits!.Select(wait => wait.Why));
        Assert.Equal(SessionGroup.Done, Row(look, "s3").Group);
    }

    [Fact]
    public void A_live_session_of_a_waiting_run_stays_working()
    {
        var look = Look([Record("s1", "working", "q1", 0)], [Quest("q1", "Taken")], [Waits("q1", "review", PersonWaits.Accept)]);

        Assert.Equal(SessionGroup.Working, Row(look, "s1").Group);
    }

    [Fact]
    public void The_terminals_needs_you_group_lists_each_waiting_run_with_what_it_waits_for_and_the_run_it_opens()
    {
        var look = Look(
            [Record("s1", "completed", "q1", 0), Record("s2", "completed", "q2", 3), Record("s3", "completed", "q3", 6)],
            [Quest("q1"), Quest("q2"), Quest("q3")],
            [Waits("q1", "review", PersonWaits.Accept), Waits("q2", "go", PersonWaits.GoAhead)],
            archived: ["s1"]);

        var you = string.Join("\n", SessionsListing.ListLines(look, new SessionsAsk("list") { Group = SessionGroup.NeedsYou }, TestHomes.Root, T0));

        Assert.Contains("run q1 waits for your Accept at `review` · daoris workflow run --session s1", you, StringComparison.Ordinal);
        Assert.Contains("run q2 waits for your go-ahead at `go` · daoris workflow run --session s2", you, StringComparison.Ordinal);
        Assert.DoesNotContain("s3", you, StringComparison.Ordinal);
    }

    [Fact]
    public void The_written_row_carries_its_waits_and_a_row_with_none_writes_null()
    {
        var look = Look(
            [Record("s1", "completed", "q1", 0), Record("s2", "completed", "q2", 3)], [Quest("q1"), Quest("q2")], [Waits("q1", "review", PersonWaits.Accept)]);

        var rows = JsonNode.Parse(SessionsJson.Json(SessionGroups.Read(look)))!["sessions"]!.AsArray();
        var waits = rows.Single(row => (string?)row!["session"] == "s1")!["waits"]!.AsArray().Single()!;

        Assert.Equal(("q1", "review", "accept"), ((string?)waits["run"], (string?)waits["node"], (string?)waits["why"]));
        Assert.Equal(T0.AddMinutes(30), DateTimeOffset.Parse((string)waits["since"]!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null(rows.Single(row => (string?)row!["session"] == "s2")!["waits"]);
    }
}
