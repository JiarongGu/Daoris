using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run at its graph's end that a session still waits on is ended again through the run's one end, at each look, whatever answered its
/// first end; one with nothing waiting says so once, and a run not at its end is left alone.
/// </summary>
public sealed class RunReEndsTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-15T10:00:00Z");

    private static QuestView HeldQuest() =>
        new("q1", "Asker", "Owner", "Do the thing", "Here is why.", "Taken")
        {
            Closure = "run:q1", Held = true, Hold = EvidenceCodes.Departed, Submission = new QuestSubmissionView("m1", 3, At, "Built."),
        };

    private static SessionRecord Waiting(string run) =>
        new("s-work", "Owner", SessionStates.AwaitingStep) { NoteParts = new StepWait(run, "work").Note().Parts };

    private static RunEnd Completed() => new("q1", At) { Outcome = RunOutcomes.Completed };

    private sealed class Ends(IReadOnlyList<SessionRecord> records)
    {
        public List<RunFinish> Sent { get; } = [];

        public List<(string Code, string Said)> Logged { get; } = [];

        public RunEndPorts Ports() =>
            new("home-" + Guid.NewGuid().ToString("N")[..8])
            {
                Finish = (end, _) => { Sent.Add(end); return Task.FromResult<RunEndResponse?>(new RunEndResponse(null, "Ended.", [.. end.Quests.Select(quest => quest.Id)], end.Sessions)); },
                Sessions = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(records),
                Log = (code, said) => Logged.Add((code, said)),
            };
    }

    [Fact]
    public async Task A_run_at_its_end_with_a_session_waiting_on_it_is_ended_again_closing_what_its_end_decides()
    {
        var ends = new Ends([Waiting("q1")]);

        await RunReEnds.AgainAsync(ends.Ports(), "q1", Completed(), [HeldQuest()], passed: true, [], CancellationToken.None);

        var finish = Assert.Single(ends.Sent);
        Assert.Equal([new RunEndQuest("q1", "m1", 3, Passed: true)], finish.Quests);
        Assert.Equal(["s-work"], finish.Sessions);
        Assert.Equal("completed", finish.State);
    }

    [Fact]
    public async Task A_run_at_its_end_with_nothing_waiting_sends_nothing_and_says_so_once()
    {
        var ends = new Ends([Waiting("q9")]);
        var ports = ends.Ports();

        await RunReEnds.AgainAsync(ports, "q1", Completed(), [HeldQuest()], passed: true, [], CancellationToken.None);
        await RunReEnds.AgainAsync(ports, "q1", Completed(), [HeldQuest()], passed: true, [], CancellationToken.None);

        Assert.Empty(ends.Sent);
        var (code, said) = Assert.Single(ends.Logged);
        Assert.Equal(EmptyRunEnds.Event, code);
        Assert.Contains($"({EmptyRunEnds.Nothing})", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_not_at_its_end_is_left_alone()
    {
        var ends = new Ends([Waiting("q1")]);

        await RunReEnds.AgainAsync(ends.Ports(), "q1", null, [HeldQuest()], passed: false, [], CancellationToken.None);

        Assert.Empty(ends.Sent);
        Assert.Empty(ends.Logged);
    }
}
