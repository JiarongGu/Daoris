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

    private static NodeAttempt Tried(string node, int number, string outcome, string? code) =>
        new(node, number, "pull-request", new WorkPerformCall("run:q1", "pull-request/merge", 1, $"i{number}", $"k{number}", "r1", [], T0), AttemptStates.Settled)
        {
            RecordedAt = T0.AddMinutes(number), Outcome = outcome, Code = code,
        };

    [Fact]
    public void A_step_s_passage_names_its_role_its_newest_attempt_its_start_what_failed_and_the_attempts_before_it()
    {
        var graph = new NodeGraph(
        [
            new NodeDeclaration("merge", "pull-request/merge", 1, NodeReach.Performed),
            new NodeDeclaration("release", "pull-request/release", 1, NodeReach.Performed) { Role = StepRoles.ReleaseManager },
        ], [], ["merge"], "release");
        var binding = new WorkflowRunBinding("q1", "engine", "pull-request-release", "docs", WorkflowLevels.Repository, T0) { Version = 2, Digest = "gph-0123456789abcdef0123456789abcdef" };
        var run = new GraphRun(binding, new DecodedVersion(2, binding.Digest, graph, binding.Digest, null), null);
        var record = new NodeRecord("run:q1")
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["merge"] = new("run:q1", "merge", NodeStates.Failed) { Attempt = 2, Tries = 2, Code = "conflict", Since = T0.AddMinutes(2) },
            },
            Attempts = [Tried("merge", 1, WorkStates.Failed, "checks"), Tried("merge", 2, WorkStates.Failed, "conflict")],
        };

        var passage = Assert.Single(RunPassageReader.Of([run], _ => record));
        var merge = passage.Steps[0];
        Assert.Equal((RunPassageReader.Failed, StepRoles.Default("pull-request/merge"), 2, T0.AddMinutes(2), "conflict"), (merge.Mark, merge.Role, merge.Attempt, merge.Since, merge.Code));
        var earlier = Assert.Single(merge.Earlier!);
        Assert.Equal((1, T0.AddMinutes(1), WorkStates.Failed, "checks"), (earlier.Attempt, earlier.At, earlier.Outcome, earlier.Code));
        var release = passage.Steps[1];
        Assert.Equal((RunPassageReader.Next, StepRoles.ReleaseManager, null, null), (release.Mark, release.Role, release.Attempt, release.Earlier));

        using var written = JsonDocument.Parse(SessionsJson.Json(SessionGroups.Read(Look(Record("s2", "awaiting-step", "q1", 5)) with { Passages = [passage] })));
        var step = written.RootElement.GetProperty("sessions")[0].GetProperty("passage").GetProperty("steps")[0];
        Assert.Equal((StepRoles.Default("pull-request/merge"), 2, "conflict", 1, "checks"), (step.GetProperty("role").GetString(), step.GetProperty("attempt").GetInt32(),
            step.GetProperty("code").GetString(), step.GetProperty("earlier")[0].GetProperty("attempt").GetInt32(), step.GetProperty("earlier")[0].GetProperty("code").GetString()));
        Assert.Equal(T0.AddMinutes(2), step.GetProperty("since").GetDateTimeOffset());
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
