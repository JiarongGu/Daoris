using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's row draws its run's passage through its bound workflow version: the steps passed, the one it stands at and those to
/// come, on the run's newest session alone; a session waiting on its check stays in Working and never needs the person.
/// </summary>
public sealed class RunPassageGroupsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static JsonObject Record(string id, string state, string quest, int at) => new()
    {
        ["id"] = id,
        ["repository"] = "engine",
        ["adapter"] = "claude-code",
        ["state"] = state,
        ["kind"] = "driven",
        ["quest"] = quest,
        ["said"] = new JsonArray(),
        ["created"] = T0.AddMinutes(at).ToString("O"),
        ["updated"] = T0.AddMinutes(at + 1).ToString("O"),
    };

    private static readonly RunPassage Checking = new("q1", "integrated", 3,
    [
        new PassageStep("content", "work", RunPassageReader.Passed),
        new PassageStep("check", "check", RunPassageReader.Current),
        new PassageStep("holds", "holds", RunPassageReader.Next),
        new PassageStep("landing", "land", RunPassageReader.Next),
    ]);

    private static SessionLook Look(params JsonObject[] records) =>
        SessionLook.From(new JsonArray([.. records]).ToJsonString(), [new QuestView("q1", "game", "engine", "The work", "A body.", "Taken")], [], _ => 0)
            with { Passages = [Checking] };

    [Fact]
    public void A_session_waiting_on_its_check_draws_its_run_s_steps_and_stays_in_working()
    {
        var row = SessionGroups.Read(Look(Record("s1", "failed", "q1", 0), Record("s2", "awaiting-step", "q1", 5))).Single(each => each.Session == "s2");

        Assert.Equal((SessionGroup.Working, ShownState.AwaitingStep), (row.Group, row.Shown));
        Assert.Equal(
            [("content", RunPassageReader.Passed), ("check", RunPassageReader.Current), ("holds", RunPassageReader.Next), ("landing", RunPassageReader.Next)],
            row.Passage!.Steps.Select(step => (step.Node, step.Mark)));
    }

    [Fact]
    public void Only_the_run_s_newest_session_draws_it_and_the_answer_names_each_step_s_mark()
    {
        var look = Look(Record("s1", "failed", "q1", 0), Record("s2", "awaiting-step", "q1", 5));
        var rows = SessionGroups.Read(look);

        Assert.Null(rows.Single(each => each.Session == "s1").Passage);
        using var written = JsonDocument.Parse(SessionsJson.Json(rows));
        var passage = written.RootElement.GetProperty("sessions").EnumerateArray().Single(each => each.GetProperty("session").GetString() == "s2").GetProperty("passage");
        Assert.Equal(("integrated", 3, "check", "current"), (passage.GetProperty("workflow").GetString(), passage.GetProperty("version").GetInt32(),
            passage.GetProperty("steps")[1].GetProperty("node").GetString(), passage.GetProperty("steps")[1].GetProperty("mark").GetString()));
    }

    [Theory]
    [InlineData(NodeStates.Reached, RunPassageReader.Passed)]
    [InlineData(NodeStates.Skipped, RunPassageReader.Passed)]
    [InlineData(NodeStates.Active, RunPassageReader.Current)]
    [InlineData(NodeStates.Unknown, RunPassageReader.Current)]
    [InlineData(NodeStates.Failed, RunPassageReader.Failed)]
    [InlineData(NodeStates.Blocked, RunPassageReader.Next)]
    public void A_step_s_state_reads_as_its_mark(string state, string mark) => Assert.Equal(mark, RunPassageReader.MarkOf(state));
}
