using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's end is the one write of its quests' Done and its waiting sessions' end: a run that came to its graph's end names each of its
/// quests standing at a submission, one that ended any other way names none, and a session's wait keeps the step it waits on.
/// </summary>
public sealed class RunQuestEndsTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-15T10:00:00Z");

    private static QuestView Quest(string id, string status, long? submitted, string? closure = "run:q1") =>
        new(id, "Asker", "Owner", "Do the thing", "Here is why.", status)
        {
            Closure = closure, Submission = submitted is { } sequence ? new QuestSubmissionView("m1", sequence, At, "Built.") : null,
        };

    [Fact]
    public async Task A_completed_run_s_end_closes_its_submitted_quests_with_its_sessions_in_one_finish()
    {
        var sent = new List<RunFinish>();
        var said = new List<string>();
        var quests = new[] { Quest("q1", "Taken", 3), Quest("q2", "Taken", 5), Quest("q3", "Done", null), Quest("q4", "Taken", null) };

        await RunSessionEnds.FinishAsync(
            (finish, _) => { sent.Add(finish); return Task.FromResult<RunEndResponse?>(new RunEndResponse(null, "Run `q1` ended.", ["q1", "q2"], ["s1"])); },
            "q1", new RunEnd("q2", At) { Outcome = RunOutcomes.Completed }, quests, ["s1", "s1"], said, CancellationToken.None);

        var finish = Assert.Single(sent);
        Assert.Equal([new RunEndQuest("q1", "m1", 3), new RunEndQuest("q2", "m1", 5)], finish.Quests);
        Assert.Equal(["s1"], finish.Sessions);
        Assert.Equal("completed", finish.State);
        Assert.Contains("Run `q1` ended.", Assert.Single(said), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_that_ended_any_other_way_closes_nothing_and_stops_its_sessions()
    {
        var sent = new List<RunFinish>();

        await RunSessionEnds.FinishAsync(
            (finish, _) => { sent.Add(finish); return Task.FromResult<RunEndResponse?>(null); },
            "q1", new RunEnd("q1", At) { Outcome = RunOutcomes.Failed, Node = "check" }, [Quest("q1", "Taken", 3)], ["s1"], [], CancellationToken.None);

        Assert.Equal(([], "stopped"), (Assert.Single(sent).Quests.ToArray(), sent[0].State));
    }

    [Fact]
    public void A_session_s_wait_keeps_its_run_node_attempt_and_tip()
    {
        var waits = new StepWait("q1", "check", 2, "a1b2c3d");

        Assert.Equal(waits, StepWaits.From(waits.Note().Parts));
        Assert.Equal(new StepWait("q1"), StepWaits.From(new StepWait("q1").Note().Parts));
    }
}
