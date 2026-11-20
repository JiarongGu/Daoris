using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Workflows;

/// <summary>
/// A <c>quest.holds</c> holding a done nothing checked (its ask gave its quest no requirement) waits on its holder's pass with their words:
/// a wait of its own, on the step's role, with the run's skip as its press, so <i>What needs you</i> rows it and the session's page draws it.
/// Seen failing first: the step stood active with its reason and read as no wait at all, so neither place offered the pass, and the owner
/// passed it from the terminal (<c>workflow skip &lt;run&gt; holds "…"</c>).
/// </summary>
public sealed class StepWaitPassTests
{
    private const string Run = "q7";
    private const string Unit = "run:q7";
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private const string Unchecked = "its done is unchecked: ask #a1 gave its quest no requirement, so nothing compared the done with what was "
                                     + "asked; whoever holds the step's role (`asker` where it names none) reads its handback (quest #q7's done note) and "
                                     + "passes this step with their words where it does what was asked";

    private static readonly NodeDeclaration Holds = new("holds", NodeKinds.QuestHolds, 1, NodeReach.Observed);

    private static NodeRecord Standing(string? holds) => new(Unit)
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["holds"] = new(Unit, "holds", NodeStates.Active) { Attempt = 1, Tries = 1, Since = Now, Holds = holds },
        },
    };

    [Fact]
    public void An_unchecked_done_held_at_holds_waits_on_your_pass_with_the_runs_skip_as_its_press()
    {
        var record = Standing(Unchecked);

        var wait = StepWaiting.Read(record, Holds, WorkPerformer.StatusOf(record, "holds"));
        var row = HeldSteps.Step(Run, "engine", "aurora", Run, Holds, record);

        Assert.Equal((PersonWaits.Pass, StepRoles.Asker, StepHolders.Person), (wait?.Why, wait?.Role, wait?.Holder.Kind));
        Assert.NotNull(row);
        Assert.Equal((PersonWaits.Pass, StepHolders.Person), (row.Why, row.Holder));
        Assert.Contains(NodeWordActs.Skip, row.Presses);
    }

    [Fact]
    public void A_holds_step_waiting_on_its_quest_or_a_word_already_given_waits_on_nobody_here()
    {
        var waiting = Standing("its quest's done is not in, or was taken back");
        var worded = Standing(Unchecked) with
        {
            Pending = [new NodeWord("holds", NodeWordActs.Skip, "It does what I asked.", Now)],
        };

        Assert.Null(StepWaiting.Read(waiting, Holds, WorkPerformer.StatusOf(waiting, "holds")));
        Assert.Null(HeldSteps.Step(Run, "engine", "aurora", Run, Holds, worded));
    }
}
