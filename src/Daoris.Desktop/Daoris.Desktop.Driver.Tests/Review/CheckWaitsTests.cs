using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session waiting on its own check: its turn ends <c>completed</c> by <c>ended.awaits-check</c> holding its tree, the verdict is
/// its next word, and a check Daoris asked on its own never counts as a wait of the agent's.
/// </summary>
public sealed class CheckWaitsTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T10:00:00Z");

    private static CheckAsk Ask(string id, string session = "s1", string? gate = "tests") =>
        new(id, session, gate, null, At);

    private static Func<string, CheckState?> States(params CheckState[] states) => id => states.FirstOrDefault(state => state.Id == id);

    [Fact]
    public void A_check_is_open_until_its_verdict_is_handed()
    {
        var asks = new[] { Ask("a1"), Ask("a2"), Ask("a3"), Ask("a5", session: "s2") };
        var states = States(
            new CheckState("a2", CheckStates.Reached, At), new CheckState("a3", CheckStates.Failed, At) { Handed = "w1" });

        Assert.Equal(["a1", "a2"], CheckWaits.Open("s1", asks, states).Select(ask => ask.Id));
    }

    [Fact]
    public void A_turn_that_ended_on_its_own_open_check_waits_on_it_and_holds_its_tree()
    {
        foreach (var conclusion in new[]
                 {
                     Observation.Conclude(0, "Taken", took: true, lastWords: "Asked for the tests.", awaitsCheck: true),
                     Observation.Resumed(0, "completed", "Taken", "Taken", lastWords: "Asked for the tests.", awaitsCheck: true),
                 })
        {
            Assert.Equal("completed", conclusion.State);
            Assert.Equal(EndNoteCodes.EndedAwaitsCheck.Code, Assert.Single(conclusion.Parts!).Code);
            Assert.True(CheckWaits.Ended(conclusion.Parts));
            Assert.DoesNotContain("Asked for the tests.", conclusion.Note);
        }

        Assert.Equal("ended.done", Observation.Conclude(0, "Done", took: true, awaitsCheck: true).Parts![0].Code);
        Assert.Equal(EndNoteCodes.EndedTakenExit.Code, Observation.Conclude(1, "Taken", took: true, awaitsCheck: true).Parts![0].Code);
        Assert.Equal("stood-down", Observation.Conclude(0, "Taken", took: false, awaitsCheck: true).State);

        var waiting = new PriorSession("s1", "trees/s-1", "completed") { Quest = "q1", NoteParts = [EndNoteCodes.EndedAwaitsCheck.Part("a line.", [])], Said = [] };
        Assert.True(HelperWaits.HoldsTree(waiting));
        Assert.True(CheckWaits.Waits(waiting));
        // The helper's own return to the person must never read a check's wait as its own.
        Assert.False(HelperWaits.Waits(waiting));
        Assert.False(CheckWaits.Waits(waiting with { Said = [new SaidWordView("w1", "verdict", At, [], true)] }));
    }

    [Fact]
    public void An_end_waiting_on_a_check_is_its_own_class_the_person_is_not_told()
    {
        var row = new JournalEntry(
            3, 1_761_800_000_000, "s1", "sessions", "state", "Working", "Completed", false, false, ["ended.awaits-check"], ["driven"], "team", "q1");

        var ended = PartEnds.Judge(row, "store-1");

        Assert.Equal("awaits-check", ended!.Class);
        Assert.Equal("agent", PartEnds.Causes["awaits-check"]);
    }

    [Fact]
    public void The_verdict_word_says_the_gate_the_exit_and_the_tail_of_a_red_log()
    {
        var red = CheckWaits.Words(Ask("a1"), new CheckState("a1", CheckStates.Failed, At) { Exit = 1, Tail = "Assert.Equal failed\nat Foo.Bar" });
        var green = CheckWaits.Words(Ask("a2", gate: null), new CheckState("a2", CheckStates.Reached, At) { Exit = 0, Tail = "12 passed" });

        Assert.Contains("`tests`", red);
        Assert.Contains("failed", red);
        Assert.Contains("Assert.Equal failed", red);
        Assert.Contains("reached", green);
        Assert.DoesNotContain("12 passed", green);
        Assert.Equal("check/a1", CheckWaits.Key(Ask("a1")));
    }
}
