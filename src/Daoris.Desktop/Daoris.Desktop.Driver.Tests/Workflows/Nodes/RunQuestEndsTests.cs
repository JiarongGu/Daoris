using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's end is the one write of its quests' Done and its waiting sessions' end: a run that came to its graph's end names each of its
/// quests standing at a submission, one that ended any other way names none, every session waiting on the run is found by the run it
/// waits on at each write, and an end that closes and ends nothing says so once.
/// </summary>
public sealed class RunQuestEndsTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-15T10:00:00Z");

    private static QuestView Quest(string id, string status, long? submitted, string? closure = "run:q1") =>
        new(id, "Asker", "Owner", "Do the thing", "Here is why.", status)
        {
            Closure = closure, Submission = submitted is { } sequence ? new QuestSubmissionView("m1", sequence, At, "Built.") : null,
        };

    private static QuestView Held(string id, string hold) => Quest(id, "Taken", 3) with { Held = true, Hold = hold };

    private static SessionRecord Waiting(string id, string run) => new(id, "Owner", SessionStates.AwaitingStep) { WaitsOn = run };

    private static RunEnd Completed(string quest) => new(quest, At) { Outcome = RunOutcomes.Completed };

    private sealed class Ends(IReadOnlyList<SessionRecord>? records = null)
    {
        public List<RunFinish> Sent { get; } = [];

        public List<(string Code, string Said)> Logged { get; } = [];

        public List<string> Said { get; } = [];

        public IReadOnlyList<SessionRecord>? Records { get; set; } = records ?? [];

        public Func<RunFinish, RunEndResponse?> Answer { get; set; } = end => new RunEndResponse(null, $"Run `{end.Run}` ended.", [.. end.Quests.Select(quest => quest.Id)], end.Sessions);

        public RunEndPorts Ports(string? home = null, OwedRunEnds? owed = null) =>
            new(home ?? "home-" + Guid.NewGuid().ToString("N")[..8])
            {
                Finish = (end, _) => { Sent.Add(end); return Task.FromResult(Answer(end)); },
                Sessions = _ => Task.FromResult(Records),
                Log = (code, said) => Logged.Add((code, said)),
                Owed = owed,
            };
    }

    [Fact]
    public async Task A_completed_run_s_end_closes_its_submitted_quests_and_ends_the_sessions_found_waiting_on_it()
    {
        var ends = new Ends([Waiting("s1", "q1"), Waiting("s2", "q9"), new SessionRecord("s3", "Owner", "completed") { WaitsOn = "q1" }]);
        var quests = new[] { Quest("q1", "Taken", 3), Quest("q2", "Taken", 5), Quest("q3", "Done", null), Quest("q4", "Taken", null) };

        await RunSessionEnds.FinishAsync(ends.Ports(), "q1", Completed("q2"), quests, passed: false, ends.Said, CancellationToken.None);

        var finish = Assert.Single(ends.Sent);
        Assert.Equal([new RunEndQuest("q1", "m1", 3), new RunEndQuest("q2", "m1", 5)], finish.Quests);
        Assert.Equal(["s1"], finish.Sessions);
        Assert.Equal("completed", finish.State);
        Assert.Contains("Run `q1` ended.", Assert.Single(ends.Said), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_that_ended_any_other_way_closes_nothing_and_stops_its_sessions()
    {
        var ends = new Ends([Waiting("s1", "q1")]);

        await RunSessionEnds.FinishAsync(ends.Ports(), "q1", new RunEnd("q1", At) { Outcome = RunOutcomes.Failed, Node = "check" },
            [Quest("q1", "Taken", 3)], passed: false, ends.Said, CancellationToken.None);

        var finish = Assert.Single(ends.Sent);
        Assert.Empty(finish.Quests);
        Assert.Equal("stopped", finish.State);
        Assert.Equal(["s1"], finish.Sessions);
    }

    [Fact]
    public async Task A_completed_run_whose_holds_the_person_passed_closes_its_held_quest_and_ends_the_session_waiting_on_it()
    {
        // The session serves no quest the run names: it is the run's by the run its wait names.
        var ends = new Ends([Waiting("s-work", "q1")]);

        await RunSessionEnds.FinishAsync(ends.Ports(), "q1", Completed("q1"), [Held("q1", EvidenceCodes.Departed)], passed: true, ends.Said, CancellationToken.None);

        var finish = Assert.Single(ends.Sent);
        Assert.Equal([new RunEndQuest("q1", "m1", 3, Passed: true)], finish.Quests);
        Assert.Equal("completed", finish.State);
        Assert.Equal(["s-work"], finish.Sessions);
    }

    [Theory]
    [InlineData(false, EvidenceCodes.Departed)]
    [InlineData(true, EvidenceCodes.Unreviewed)]
    public async Task A_held_quest_no_yes_lets_through_is_left_as_it_stands_and_the_run_s_sessions_end_completed(bool passed, string hold)
    {
        var ends = new Ends([Waiting("s-work", "q1")]);

        await RunSessionEnds.FinishAsync(ends.Ports(), "q1", Completed("q1"), [Held("q1", hold)], passed, ends.Said, CancellationToken.None);

        var finish = Assert.Single(ends.Sent);
        Assert.Empty(finish.Quests);
        Assert.Equal("completed", finish.State);
        Assert.Equal(["s-work"], finish.Sessions);
    }

    [Fact]
    public void The_person_s_pass_over_a_run_s_holds_is_its_quest_holds_skipped_by_them_with_words()
    {
        var graph = new NodeGraph([new NodeDeclaration("holds", NodeKinds.QuestHolds, 1, NodeReach.Observed)], [], ["holds"], "holds");
        NodeRecord With(string state, string? words) => new("run:q1")
        {
            Statuses = new Dictionary<string, NodeStatus> { ["holds"] = new("run:q1", "holds", state) { By = NodeCause.Person, Words = words } },
        };

        Assert.True(RunEndQuests.Passed(graph, With(NodeStates.Skipped, "Requirement 1 departs with its reason.")));
        Assert.False(RunEndQuests.Passed(graph, With(NodeStates.Skipped, null)));
        Assert.False(RunEndQuests.Passed(graph, With(NodeStates.Reached, "words")));
        Assert.False(RunEndQuests.Passed(graph, new NodeRecord("run:q1")));
    }

    [Fact]
    public async Task A_run_s_end_with_nothing_to_close_or_end_says_its_coded_reason_once()
    {
        var ends = new Ends([Waiting("s-other", "q2")]);
        var ports = ends.Ports();

        await RunSessionEnds.FinishAsync(ports, "q1", Completed("q1"), [Quest("q1", "Done", null)], passed: false, ends.Said, CancellationToken.None);
        await RunSessionEnds.FinishAsync(ports, "q1", Completed("q1"), [Quest("q1", "Done", null)], passed: false, ends.Said, CancellationToken.None);

        Assert.Empty(ends.Sent);
        var (code, said) = Assert.Single(ends.Logged);
        Assert.Equal(EmptyRunEnds.Event, code);
        Assert.Contains($"({EmptyRunEnds.Nothing})", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_s_end_with_no_door_or_one_the_service_refused_says_so_once_each()
    {
        var ends = new Ends([Waiting("s1", "q1")]) { Answer = _ => new RunEndResponse("Held", "Run `q1`'s quests were not closed.", [], []) };
        var ports = ends.Ports();

        await RunSessionEnds.FinishAsync(ports with { Finish = null }, "q1", Completed("q1"), [Quest("q1", "Taken", 3)], passed: false, ends.Said, CancellationToken.None);
        await RunSessionEnds.FinishAsync(ports, "q1", Completed("q1"), [Quest("q1", "Taken", 3)], passed: false, ends.Said, CancellationToken.None);
        await RunSessionEnds.FinishAsync(ports, "q1", Completed("q1"), [Quest("q1", "Taken", 3)], passed: false, ends.Said, CancellationToken.None);

        Assert.Equal([EmptyRunEnds.NoDoor, EmptyRunEnds.Refused], ends.Logged.Select(each => each.Said.Contains($"({EmptyRunEnds.NoDoor})", StringComparison.Ordinal)
            ? EmptyRunEnds.NoDoor : EmptyRunEnds.Refused));
        Assert.Contains("Held", ends.Logged[1].Said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_s_end_the_service_could_not_take_is_asked_again_at_each_look_finding_the_sessions_waiting_then()
    {
        var home = Path.Combine(TestHomes.Root, "daoris-owed-end-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var owed = new OwedRunEnds(home);
            var ends = new Ends([Waiting("s1", "q1")]) { Answer = _ => null };
            var ports = ends.Ports(home, owed);

            await RunSessionEnds.FinishAsync(ports, "q1", Completed("q1"), [Quest("q1", "Taken", 3)], passed: false, ends.Said, CancellationToken.None);
            Assert.Contains("asked again at the next look", Assert.Single(ends.Said), StringComparison.Ordinal);

            await RunSessionEnds.AgainAsync(ports, ends.Said, CancellationToken.None);
            Assert.True(owed.Any);

            // A session that came to wait on the run since is found by the same rule at the write that takes the end.
            ends.Records = [Waiting("s1", "q1"), Waiting("s2", "q1"), Waiting("s3", "q2")];
            ends.Answer = end => new RunEndResponse(null, "Run `q1` ended.", ["q1"], end.Sessions);
            await RunSessionEnds.AgainAsync(ports, ends.Said, CancellationToken.None);
            await RunSessionEnds.AgainAsync(ports, ends.Said, CancellationToken.None);

            Assert.Equal(3, ends.Sent.Count);
            Assert.All(ends.Sent, finish => Assert.Equal([new RunEndQuest("q1", "m1", 3)], finish.Quests));
            Assert.Equal(["s1"], ends.Sent[0].Sessions);
            Assert.Equal(["s1", "s2"], ends.Sent[^1].Sessions);
            Assert.False(owed.Any);
            Assert.Contains("Run `q1` ended.", ends.Said[^1], StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task A_run_s_end_whose_sessions_do_not_read_is_owed_as_one_the_service_could_not_take()
    {
        var home = Path.Combine(TestHomes.Root, "daoris-owed-read-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var owed = new OwedRunEnds(home);
            var ends = new Ends { Records = null };

            await RunSessionEnds.FinishAsync(ends.Ports(home, owed), "q1", Completed("q1"), [Quest("q1", "Taken", 3)], passed: false, ends.Said, CancellationToken.None);

            Assert.Empty(ends.Sent);
            Assert.True(owed.Any);
            Assert.Contains("asked again at the next look", Assert.Single(ends.Said), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void A_session_s_wait_keeps_its_run_node_attempt_and_tip()
    {
        var waits = new StepWait("q1", "check", 2, "a1b2c3d");

        Assert.Equal(waits, StepWaits.From(waits.Note().Parts));
        Assert.Equal(new StepWait("q1"), StepWaits.From(new StepWait("q1").Note().Parts));
    }
}
